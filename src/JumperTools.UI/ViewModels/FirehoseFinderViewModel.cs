using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Win32;
using CommunityToolkit.Mvvm.Input;
using JumperTools.Models;
using JumperTools.Services;

namespace JumperTools.UI.ViewModels
{
    public class FirehoseFinderViewModel : INotifyPropertyChanged
    {
        private readonly FirehoseFinderService _firehoseService;
        private List<FirehoseLoaderData> _allLoaders;
        public ObservableCollection<FirehoseLoaderData> Loaders { get; }
        public ObservableCollection<string> EdlPorts { get; }
        
        public event Action<string>? OnLog;

        public FirehoseFinderViewModel()
        {
            var dbPath = System.IO.Path.Combine(JumperTools.Core.AppPathResolver.FirehoseDirectory, "ForFilter.xml");
            _firehoseService = new FirehoseFinderService(dbPath);
            _allLoaders = new List<FirehoseLoaderData>();
            Loaders = new ObservableCollection<FirehoseLoaderData>();
            EdlPorts = new ObservableCollection<string> { "COM1" };
            
            SearchCommand = new AsyncRelayCommand(ExecuteSearchAsync);
            SendLoaderCommand = new AsyncRelayCommand(ExecuteSendLoaderAsync, CanExecuteSendLoader);
            BrowseLoaderCommand = new RelayCommand(ExecuteBrowseLoader);
            ClearLoaderCommand = new RelayCommand(ExecuteClearLoader);
            AutoDetectLoaderCommand = new AsyncRelayCommand(ExecuteAutoDetectLoaderAsync);
            
            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            IsBusy = true;
            StatusMessage = "Memuat database loader...";
            try
            {
                _allLoaders = await _firehoseService.LoadDatabaseAsync();
                await ExecuteSearchAsync();
                StatusMessage = $"Database: {_allLoaders.Count:N0} Loaders Aktif";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private string _searchQuery = string.Empty;
        public string SearchQuery
        {
            get => _searchQuery;
            set
            {
                if (_searchQuery != value)
                {
                    _searchQuery = value;
                    OnPropertyChanged();
                    // Optional: trigger search as you type, but let's just use the button for now.
                }
            }
        }

        private FirehoseLoaderData? _selectedLoader;
        public FirehoseLoaderData? SelectedLoader
        {
            get => _selectedLoader;
            set
            {
                if (_selectedLoader != value)
                {
                    _selectedLoader = value;
                    OnPropertyChanged();
                    ((AsyncRelayCommand)SendLoaderCommand).NotifyCanExecuteChanged();
                }
            }
        }
        
        private string _targetComPort = string.Empty;
        public string TargetComPort
        {
            get => _targetComPort;
            set
            {
                if (_targetComPort != value)
                {
                    _targetComPort = value;
                    OnPropertyChanged();
                    ((AsyncRelayCommand)SendLoaderCommand).NotifyCanExecuteChanged();
                }
            }
        }

        private string _customLoaderPath = string.Empty;
        public string CustomLoaderPath
        {
            get => _customLoaderPath;
            set
            {
                if (_customLoaderPath != value)
                {
                    _customLoaderPath = value;
                    OnPropertyChanged();
                    ((AsyncRelayCommand)SendLoaderCommand).NotifyCanExecuteChanged();
                }
            }
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set { if (_isBusy != value) { _isBusy = value; OnPropertyChanged(); } }
        }

        private string _statusMessage = "Siap";
        public string StatusMessage
        {
            get => _statusMessage;
            set { if (_statusMessage != value) { _statusMessage = value; OnPropertyChanged(); } }
        }

        public bool HasNoResults => Loaders.Count == 0;

        public ICommand SearchCommand { get; }
        public ICommand SendLoaderCommand { get; }
        public ICommand BrowseLoaderCommand { get; }
        public ICommand ClearLoaderCommand { get; }
        public ICommand AutoDetectLoaderCommand { get; }

        private async Task ExecuteAutoDetectLoaderAsync()
        {
            if (string.IsNullOrWhiteSpace(TargetComPort))
            {
                StatusMessage = "Pilih COM Port EDL (9008) terlebih dahulu.";
                return;
            }

            IsBusy = true;
            StatusMessage = $"Mendeteksi HWID di {TargetComPort}...";
            OnLog?.Invoke($"[Firehose Finder] Mendeteksi HWID via Sahara...");

            try
            {
                string hwid = await _firehoseService.GetDeviceHwidAsync(TargetComPort);
                if (string.IsNullOrEmpty(hwid))
                {
                    StatusMessage = "Gagal mendeteksi HWID. Pastikan HP di EDL mode dan QSaharaServer bekerja.";
                    OnLog?.Invoke($"[Firehose Finder] ERROR: HWID tidak ditemukan.");
                    return;
                }

                StatusMessage = $"HWID Terdeteksi: {hwid.ToUpper()}. Mencari loader di semua koleksi...";
                OnLog?.Invoke($"[Firehose Finder] HWID: {hwid.ToUpper()}. Memindai folder database...");

                string matchedFile = await _firehoseService.FindLoaderByHwidAsync(hwid);
                if (!string.IsNullOrEmpty(matchedFile))
                {
                    CustomLoaderPath = matchedFile;
                    StatusMessage = $"Loader ditemukan: {System.IO.Path.GetFileName(matchedFile)}";
                    OnLog?.Invoke($"[Firehose Finder] SUKSES: Loader otomatis ditemukan!");
                }
                else
                {
                    StatusMessage = $"Loader untuk HWID {hwid.ToUpper()} tidak ditemukan di koleksi manapun.";
                    OnLog?.Invoke($"[Firehose Finder] GAGAL: Loader tidak ditemukan.");
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error auto-detect: {ex.Message}";
                OnLog?.Invoke($"[Firehose Finder] Error: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void ExecuteBrowseLoader()
        {
            var openFileDialog = new OpenFileDialog
            {
                Title = "Pilih Custom Firehose Loader",
                Filter = "Firehose Loader (*.mbn;*.elf)|*.mbn;*.elf|Semua File (*.*)|*.*",
                CheckFileExists = true
            };

            if (openFileDialog.ShowDialog() == true)
            {
                CustomLoaderPath = openFileDialog.FileName;
            }
        }

        private void ExecuteClearLoader()
        {
            CustomLoaderPath = string.Empty;
        }

        private async Task ExecuteSearchAsync()
        {
            IsBusy = true;
            try
            {
                var results = await _firehoseService.SearchLoadersAsync(SearchQuery, _allLoaders);
                Loaders.Clear();
                foreach (var item in results)
                {
                    Loaders.Add(item);
                }
                StatusMessage = string.IsNullOrWhiteSpace(SearchQuery) 
                    ? $"Database: {_allLoaders.Count:N0} Loaders Aktif" 
                    : $"Ditemukan {results.Count:N0} dari {_allLoaders.Count:N0} Loaders";
                OnPropertyChanged(nameof(HasNoResults));
            }
            finally
            {
                IsBusy = false;
            }
        }

        private bool CanExecuteSendLoader()
        {
            bool hasValidTarget = !string.IsNullOrWhiteSpace(TargetComPort);
            bool hasSelectedLoader = SelectedLoader != null && SelectedLoader.HasUrl;
            bool hasCustomLoader = !string.IsNullOrWhiteSpace(CustomLoaderPath);

            return hasValidTarget && (hasSelectedLoader || hasCustomLoader);
        }

        private async Task ExecuteSendLoaderAsync()
        {
            if (!CanExecuteSendLoader()) return;

            IsBusy = true;
            
            try
            {
                string loaderPath = !string.IsNullOrWhiteSpace(CustomLoaderPath) 
                    ? CustomLoaderPath 
                    : (SelectedLoader?.CleanUrl ?? "");
                
                string msg = $"Mengirim loader ({System.IO.Path.GetFileName(loaderPath)}) ke port {TargetComPort} via Sahara...";
                StatusMessage = msg;
                OnLog?.Invoke($"[Firehose Finder] {msg}");
                
                bool success = await _firehoseService.SendLoaderViaSaharaAsync(TargetComPort, loaderPath);
                
                if (success)
                {
                    StatusMessage = "Berhasil mengirim loader via Sahara.";
                    OnLog?.Invoke($"[Firehose Finder] SUKSES: Loader berhasil dikirim.");
                }
                else
                {
                    StatusMessage = "Gagal mengirim loader via Sahara.";
                    OnLog?.Invoke($"[Firehose Finder] ERROR: Gagal mengirim loader via Sahara. Periksa koneksi EDL atau versi loader.");
                }
            }
            catch(Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
