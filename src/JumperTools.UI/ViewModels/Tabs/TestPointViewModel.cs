using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.Input;
using JumperTools.Core;
using JumperTools.Models;
using JumperTools.Services;
using JumperTools.UI.ViewModels.Shared;
using Microsoft.Win32;

namespace JumperTools.UI.ViewModels.Tabs
{
    public class TestPointViewModel : TabViewModelBase
    {
        private readonly TestPointRepository _testPointRepo;
        private readonly JumperToolsApiClient _apiClient;
        private string _testPointSearchQuery = string.Empty;
        private TestPointInfo? _selectedTestPoint;

        public ObservableCollection<TestPointInfo> TestPoints { get; }

        public ICommand SearchTestPointCommand { get; }
        public ICommand SyncTestPointsCommand { get; }
        public ICommand OpenTestPointFolderCommand { get; }
        public ICommand ImportTestPointImageCommand { get; }
        public ICommand RemoveTestPointImageCommand { get; }

        public TestPointViewModel(ISharedWorkspaceContext context, TestPointRepository testPointRepo, JumperToolsApiClient apiClient)
            : base(context)
        {
            _testPointRepo = testPointRepo;
            _apiClient = apiClient;

            TestPoints = new ObservableCollection<TestPointInfo>(_testPointRepo.GetAll());
            if (TestPoints.Count > 0)
            {
                _selectedTestPoint = TestPoints[0];
            }

            SearchTestPointCommand = new RelayCommand<string?>(SearchTestPoint);
            SyncTestPointsCommand = new AsyncRelayCommand(SyncTestPointsAsync);
            OpenTestPointFolderCommand = new RelayCommand(OpenTestPointFolder);
            ImportTestPointImageCommand = new RelayCommand(ImportTestPointImage);
            RemoveTestPointImageCommand = new RelayCommand(RemoveTestPointImage);
        }

        public string TestPointSearchQuery
        {
            get => _testPointSearchQuery;
            set
            {
                if (_testPointSearchQuery != value)
                {
                    _testPointSearchQuery = value;
                    OnPropertyChanged();
                    SearchTestPoint(value);
                }
            }
        }

        public TestPointInfo? SelectedTestPoint
        {
            get => _selectedTestPoint;
            set
            {
                if (_selectedTestPoint != value)
                {
                    _selectedTestPoint = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(TestPointImageSource));
                    OnPropertyChanged(nameof(HasNoTestPointImage));
                    OnPropertyChanged(nameof(HasTestPointImage));
                }
            }
        }

        public ImageSource? TestPointImageSource
        {
            get
            {
                if (SelectedTestPoint == null) return null;

                var resolvedPath = ResolveTestPointImagePath(SelectedTestPoint);
                if (string.IsNullOrEmpty(resolvedPath) || !File.Exists(resolvedPath))
                {
                    return null;
                }

                try
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.UriSource = new Uri(resolvedPath, UriKind.Absolute);
                    bitmap.EndInit();
                    bitmap.Freeze();
                    return bitmap;
                }
                catch
                {
                    return null;
                }
            }
        }

        public bool HasNoTestPointImage => TestPointImageSource == null;
        public bool HasTestPointImage => TestPointImageSource != null;

        private string? ResolveTestPointImagePath(TestPointInfo tp)
        {
            if (!string.IsNullOrEmpty(tp.LocalImagePath) && File.Exists(tp.LocalImagePath))
            {
                return tp.LocalImagePath;
            }

            var searchDirs = new[]
            {
                Path.Combine(AppPathResolver.DatabaseDirectory, "testpoint"),
                Path.Combine(AppPathResolver.DatabaseDirectory, "isp_pinouts"),
                AppPathResolver.DatabaseDirectory,
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "database"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "database", "testpoint"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "database", "isp_pinouts"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "resources", "images", "testpoints"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "images", "testpoint")
            };

            var extensions = new[] { ".jpg", ".jpeg", ".png", ".webp", ".bmp" };
            var brand = (tp.Brand ?? "generic").ToLowerInvariant();

            var candidates = new[]
            {
                $"{tp.Brand}_{tp.MarketingName}_tp",
                $"{tp.Brand}_{tp.MarketingName}",
                tp.MarketingName,
                tp.ModelName,
                $"{tp.Brand}_{tp.ModelName}"
            };

            foreach (var dir in searchDirs)
            {
                if (!Directory.Exists(dir)) continue;

                // 0. RelativePath direct match
                if (!string.IsNullOrEmpty(tp.RelativePath))
                {
                    var relNorm = tp.RelativePath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
                    var relPath = Path.Combine(dir, relNorm);
                    if (File.Exists(relPath))
                    {
                        tp.LocalImagePath = relPath;
                        return relPath;
                    }
                }

                // 1. Direct candidate match in brand subfolder AND root dir
                var searchSubDirs = new[]
                {
                    Path.Combine(dir, brand),
                    dir
                };

                foreach (var sDir in searchSubDirs)
                {
                    if (!Directory.Exists(sDir)) continue;

                    foreach (var c in candidates)
                    {
                        var sanitized = c.Replace('/', '_').Replace('\\', '_').Replace(' ', '_').ToLowerInvariant();
                        var raw = c.Replace('/', '_').Replace('\\', '_');

                        foreach (var ext in extensions)
                        {
                            var p1 = Path.Combine(sDir, sanitized + ext);
                            if (File.Exists(p1)) { tp.LocalImagePath = p1; return p1; }

                            var p2 = Path.Combine(sDir, raw + ext);
                            if (File.Exists(p2)) { tp.LocalImagePath = p2; return p2; }
                        }
                    }
                }

                // 2. Intelligent keyword fallback match (Recursive search across all subdirectories)
                try
                {
                    var allFiles = Directory.GetFiles(dir, "*.*", SearchOption.AllDirectories);
                    var modelKeywords = (tp.MarketingName ?? string.Empty).ToLowerInvariant()
                        .Split(new[] { ' ', '/', '(', ')', '-', '_', '+' }, StringSplitOptions.RemoveEmptyEntries)
                        .Where(k => k.Length >= 2 && k != brand && k != "galaxy" && k != "redmi" && k != "honor" && k != "series")
                        .ToList();

                    if (modelKeywords.Count > 0)
                    {
                        foreach (var f in allFiles)
                        {
                            var fn = Path.GetFileNameWithoutExtension(f).ToLowerInvariant();
                            if (fn.Contains(brand) && modelKeywords.Any(k => fn.Contains(k)))
                            {
                                tp.LocalImagePath = f;
                                return f;
                            }
                        }
                    }
                }
                catch { }
            }

            return null;
        }

        public void ImportTestPointImage()
        {
            if (SelectedTestPoint == null)
            {
                AddLog("[Peringatan] Pilih salah satu model HP terlebih dahulu.");
                return;
            }

            var dialog = new OpenFileDialog
            {
                Title = $"Pilih Foto Motherboard Test Point untuk {SelectedTestPoint.MarketingName}",
                Filter = "File Gambar (*.jpg;*.jpeg;*.png;*.webp;*.bmp)|*.jpg;*.jpeg;*.png;*.webp;*.bmp|Semua File (*.*)|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var cleanBrand = (SelectedTestPoint.Brand ?? "generic").ToLowerInvariant();
                    var targetDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "database", "testpoint", cleanBrand);
                    if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);

                    var ext = Path.GetExtension(dialog.FileName).ToLowerInvariant();
                    var cleanModel = (SelectedTestPoint.MarketingName ?? "model").Replace(' ', '_').Replace('/', '_').ToLowerInvariant();
                    var destFileName = $"{cleanBrand}_{cleanModel}_tp{ext}";
                    var destPath = Path.Combine(targetDir, destFileName);

                    File.Copy(dialog.FileName, destPath, overwrite: true);

                    SelectedTestPoint.LocalImagePath = destPath;
                    SelectedTestPoint.RelativePath = $"{cleanBrand}/{destFileName}";
                    _testPointRepo.AddOrUpdate(SelectedTestPoint);

                    OnPropertyChanged(nameof(TestPointImageSource));
                    OnPropertyChanged(nameof(HasNoTestPointImage));

                    AddLog($"[Test Point] Foto diagram berhasil dipasang untuk {SelectedTestPoint.MarketingName}: {cleanBrand}/{destFileName}");
                }
                catch (Exception ex)
                {
                    AddLog($"[Error Import Foto] {ex.Message}");
                }
            }
        }

        public void RemoveTestPointImage()
        {
            if (SelectedTestPoint == null) return;

            try
            {
                if (!string.IsNullOrEmpty(SelectedTestPoint.LocalImagePath) && File.Exists(SelectedTestPoint.LocalImagePath))
                {
                    File.Delete(SelectedTestPoint.LocalImagePath);
                }
            }
            catch { }

            SelectedTestPoint.LocalImagePath = null;
            _testPointRepo.AddOrUpdate(SelectedTestPoint);

            OnPropertyChanged(nameof(TestPointImageSource));
            OnPropertyChanged(nameof(HasNoTestPointImage));

            AddLog($"[Test Point] Foto motherboard dilepas untuk {SelectedTestPoint.MarketingName}. Menggunakan blueprint skematik.");
        }

        public void SearchTestPoint(string? query)
        {
            var results = _testPointRepo.Search(query ?? "");
            TestPoints.Clear();
            foreach (var item in results)
            {
                TestPoints.Add(item);
            }

            if (TestPoints.Count > 0)
            {
                if (SelectedTestPoint == null || !TestPoints.Contains(SelectedTestPoint))
                {
                    SelectedTestPoint = TestPoints[0];
                }
            }
            else
            {
                SelectedTestPoint = null;
            }
        }

        public async Task SyncTestPointsAsync()
        {
            AddLog("[Cloud Sync] Memeriksa pembaruan diagram Test Point dari JumperTools Server...");
            try
            {
                var cloudItems = await _apiClient.GetTestPointsAsync();
                if (cloudItems.Count > 0)
                {
                    AddLog($"[Cloud Sync] Mengunduh {cloudItems.Count} diagram baru dari server.");
                    SearchTestPoint("");
                }
                else
                {
                    AddLog("[Cloud Sync] Database lokal sudah mutakhir (Up to Date).");
                }
            }
            catch (Exception ex)
            {
                AddLog($"[Cloud Sync] Info: {ex.Message}");
            }
        }

        public void OpenTestPointFolder()
        {
            try
            {
                var baseDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "database", "testpoint");
                if (!Directory.Exists(baseDir)) Directory.CreateDirectory(baseDir);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = baseDir,
                    UseShellExecute = true
                });
                AddLog($"[Test Point Hub] Membuka direktori penyimpanan skematik: {baseDir}");
            }
            catch (Exception ex)
            {
                AddLog($"[Folder Error] {ex.Message}");
            }
        }
    }
}
