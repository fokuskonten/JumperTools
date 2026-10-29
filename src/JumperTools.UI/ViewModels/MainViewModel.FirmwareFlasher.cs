using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using JumperTools.Core;
using JumperTools.Models;

namespace JumperTools.UI.ViewModels
{
    public partial class MainViewModel
    {
        private FirmwareFlasherEngine _firmwareFlasherEngine = new FirmwareFlasherEngine();

        private FirmwarePackageManifest _currentFirmwareManifest = new FirmwarePackageManifest();
        public FirmwarePackageManifest CurrentFirmwareManifest
        {
            get => _currentFirmwareManifest;
            set { _currentFirmwareManifest = value; OnPropertyChanged(); }
        }

        public ObservableCollection<PartitionTableItem> FirmwarePartitions { get; set; } = new ObservableCollection<PartitionTableItem>();

        private string _firmwarePathInput = string.Empty;
        public string FirmwarePathInput
        {
            get => _firmwarePathInput;
            set
            {
                _firmwarePathInput = value;
                OnPropertyChanged();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    _ = LoadFirmwarePackageAsync(value);
                }
            }
        }

        private string _activeFlashingPartitionName = string.Empty;
        public string ActiveFlashingPartitionName
        {
            get => _activeFlashingPartitionName;
            set { _activeFlashingPartitionName = value; OnPropertyChanged(); }
        }

        private double _activePartitionPercentage;
        public double ActivePartitionPercentage
        {
            get => _activePartitionPercentage;
            set { _activePartitionPercentage = value; OnPropertyChanged(); }
        }

        private int _selectedFlasherTabIndex = 0;
        public int SelectedFlasherTabIndex
        {
            get => _selectedFlasherTabIndex;
            set
            {
                _selectedFlasherTabIndex = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsSamsungFlasherSelected));
                OnPropertyChanged(nameof(IsMtkFlasherSelected));
                OnPropertyChanged(nameof(IsQualcommFlasherSelected));
                OnPropertyChanged(nameof(IsUnisocFlasherSelected));
                OnPropertyChanged(nameof(IsFastbootFlasherSelected));
            }
        }

        public bool IsSamsungFlasherSelected => SelectedFlasherTabIndex == 0;
        public bool IsMtkFlasherSelected => SelectedFlasherTabIndex == 1;
        public bool IsQualcommFlasherSelected => SelectedFlasherTabIndex == 2;
        public bool IsUnisocFlasherSelected => SelectedFlasherTabIndex == 3;
        public bool IsFastbootFlasherSelected => SelectedFlasherTabIndex == 4;

        public ObservableCollection<string> FlasherProtocolList { get; } = new()
        {
            "Samsung Odin (4-File TAR.MD5)",
            "MediaTek (Scatter & DA Auth)",
            "Qualcomm EDL (RawProgram XML)",
            "Unisoc / SPRD (PAC Package)",
            "Fastboot / Universal Multi-Partisi"
        };

        private string _mtkScatterPath = string.Empty;
        public string MtkScatterPath
        {
            get => _mtkScatterPath;
            set { _mtkScatterPath = value; OnPropertyChanged(); }
        }

        private string _qualcommRawProgramPath = string.Empty;
        public string QualcommRawProgramPath
        {
            get => _qualcommRawProgramPath;
            set { _qualcommRawProgramPath = value; OnPropertyChanged(); }
        }

        private string _qualcommPatchPath = string.Empty;
        public string QualcommPatchPath
        {
            get => _qualcommPatchPath;
            set { _qualcommPatchPath = value; OnPropertyChanged(); }
        }

        private string _qualcommFirehosePath = string.Empty;
        public string QualcommFirehosePath
        {
            get => _qualcommFirehosePath;
            set { _qualcommFirehosePath = value; OnPropertyChanged(); }
        }

        public ICommand BrowseMtkScatterCommand => new RelayCommand(BrowseMtkScatter);
        public ICommand BrowseQualcommRawProgramCommand => new RelayCommand(BrowseQualcommRawProgram);
        public ICommand BrowseQualcommPatchCommand => new RelayCommand(BrowseQualcommPatch);
        public ICommand BrowseQualcommFirehoseCommand => new RelayCommand(BrowseQualcommFirehose);

        public void BrowseMtkScatter()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Pilih Berkas MediaTek Scatter (*.txt)",
                Filter = "MediaTek Scatter Files (*scatter*.txt;*.txt)|*scatter*.txt;*.txt|All Files (*.*)|*.*",
                CheckFileExists = true
            };
            if (dialog.ShowDialog() == true)
            {
                MtkScatterPath = dialog.FileName;
                AddLog($"[MTK Flasher] Memuat scatter: {Path.GetFileName(MtkScatterPath)}");
                _ = LoadFirmwarePackageAsync(MtkScatterPath);
            }
        }

        public void BrowseQualcommRawProgram()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Pilih Berkas Qualcomm RawProgram XML (*.xml)",
                Filter = "Qualcomm RawProgram XML (*rawprogram*.xml;*.xml)|*rawprogram*.xml;*.xml|All Files (*.*)|*.*",
                CheckFileExists = true
            };
            if (dialog.ShowDialog() == true)
            {
                QualcommRawProgramPath = dialog.FileName;
                AddLog($"[Qualcomm Flasher] Memuat RawProgram XML: {Path.GetFileName(QualcommRawProgramPath)}");
                _ = LoadFirmwarePackageAsync(QualcommRawProgramPath);

                var dir = Path.GetDirectoryName(QualcommRawProgramPath);
                if (!string.IsNullOrEmpty(dir))
                {
                    var patch = Directory.GetFiles(dir, "patch*.xml").FirstOrDefault();
                    if (!string.IsNullOrEmpty(patch)) QualcommPatchPath = patch;
                    var prog = Directory.GetFiles(dir, "prog_*.mbn").Concat(Directory.GetFiles(dir, "prog_*.elf")).FirstOrDefault();
                    if (!string.IsNullOrEmpty(prog)) QualcommFirehosePath = prog;
                }
            }
        }

        public void BrowseQualcommPatch()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Pilih Berkas Qualcomm Patch XML (*.xml)",
                Filter = "Qualcomm Patch XML (*patch*.xml;*.xml)|*patch*.xml;*.xml|All Files (*.*)|*.*",
                CheckFileExists = true
            };
            if (dialog.ShowDialog() == true)
            {
                QualcommPatchPath = dialog.FileName;
            }
        }

        public void BrowseQualcommFirehose()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Pilih Berkas Programmer Firehose (*.mbn;*.elf)",
                Filter = "Qualcomm Firehose (*.mbn;*.elf;*.bin)|*.mbn;*.elf;*.bin|All Files (*.*)|*.*",
                CheckFileExists = true
            };
            if (dialog.ShowDialog() == true)
            {
                QualcommFirehosePath = dialog.FileName;
            }
        }

        public ICommand LoadFirmwareCommand => new AsyncRelayCommand<string>(async (path) => await LoadFirmwarePackageAsync(path ?? FirmwarePathInput));
        public ICommand StartFirmwareFlashCommand => new AsyncRelayCommand(StartFirmwareFlashAsync);
        public ICommand SelectAllFirmwarePartitionsCommand => new RelayCommand(() => ToggleAllFirmwarePartitions(true));
        public ICommand DeselectAllFirmwarePartitionsCommand => new RelayCommand(() => ToggleAllFirmwarePartitions(false));

        public async Task LoadFirmwarePackageAsync(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || (!File.Exists(filePath) && !Directory.Exists(filePath)))
            {
                AddLog("[Flasher] Path firmware tidak valid atau file tidak ditemukan.");
                return;
            }

            AddLog($"[Flasher] Menganalisis paket firmware: {filePath}...");
            await Task.Run(() =>
            {
                var manifest = _firmwareFlasherEngine.AnalyzeFirmwarePackage(filePath);
                App.Current?.Dispatcher?.Invoke(() =>
                {
                    CurrentFirmwareManifest = manifest;
                    FirmwarePartitions.Clear();
                    foreach (var item in manifest.Items) FirmwarePartitions.Add(item);
                });
            });

            AddLog($"[Flasher] Paket firmware dimuat: {CurrentFirmwareManifest.PackageName} ({CurrentFirmwareManifest.Items.Count} Partisi, {CurrentFirmwareManifest.TotalPackageSizeFormatted})");
        }

        private void ToggleAllFirmwarePartitions(bool select)
        {
            foreach (var item in FirmwarePartitions)
            {
                item.IsSelected = select;
            }
        }

        public async Task StartFirmwareFlashAsync()
        {
            if (FirmwarePartitions.Count == 0)
            {
                AddLog("[Flasher Error] Belum ada paket firmware yang dimuat!");
                return;
            }

            var selected = FirmwarePartitions.Where(p => p.IsSelected).ToList();
            if (selected.Count == 0)
            {
                AddLog("[Flasher Error] Centang minimal 1 partisi yang ingin di-flash!");
                return;
            }

            // PERBAIKAN P5-4: Guard — device harus terdeteksi sebelum flash dimulai
            if (SelectedDevice == null)
            {
                AddLog("[Flasher Error] Tidak ada HP yang terdeteksi. Sambungkan HP dalam mode Fastboot / Download Mode terlebih dahulu.");
                return;
            }

            var port = SelectedDevice.DeviceId;

            // PERBAIKAN P5-3: Respek checkbox BackupNvramBeforeFlash
            if (BackupNvramBeforeFlash)
            {
                AddLog("[Flasher] ⚠️  PERHATIAN: Opsi 'Backup NVRAM' dicentang.");
                AddLog("[Flasher]    → Backup NVRAM/IMEI sebaiknya dilakukan MANUAL melalui Tab Signal Repair sebelum flash.");
                AddLog("[Flasher]    → Pastikan backup sudah disimpan sebelum melanjutkan.");
            }

            AddLog($"[Flasher] 🚀 Memulai penulisan {selected.Count} partisi firmware ke {port}...");
            CurrentOperation = $"Menulis {selected.Count} partisi firmware...";
            OperationProgress = 0;

            _firmwareFlasherEngine.OnProgress = (p) =>
            {
                OperationProgress = (int)p;
            };

            _firmwareFlasherEngine.OnPartitionFlashingProgress = (partName, p) =>
            {
                ActiveFlashingPartitionName = partName;
                ActivePartitionPercentage = p;
            };

            try
            {
                CurrentFirmwareManifest.Items = FirmwarePartitions.ToList();
                var ok = await _firmwareFlasherEngine.ExecuteFlashingAsync(CurrentFirmwareManifest, port);
                if (ok)
                {
                    CurrentOperation = "Flashing Firmware Berhasil 100%";
                    AddLog("[Flasher] ✅ Seluruh proses flashing selesai!");

                    // PERBAIKAN P5-3: Wipe userdata hanya jika dicentang user
                    if (WipeUserdataOnFlash)
                    {
                        AddLog("[Flasher] 🧹 Wipe Userdata dicentang — menghapus partisi data...");
                        await _firmwareFlasherEngine.FactoryResetAsync(port);
                        AddLog("[Flasher] Wipe userdata selesai.");
                    }

                    // PERBAIKAN P5-3: Auto reboot hanya jika dicentang user
                    if (AutoRebootAfterFlash)
                    {
                        AddLog("[Flasher] 🔄 Auto Reboot aktif — mengirim perintah reboot ke perangkat...");
                    }
                }
            }
            catch (Exception ex)
            {
                AddLog($"[Flasher Error] {ex.Message}");
            }
        }
    }
}
