using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using JumperTools.Core;

namespace JumperTools.UI.ViewModels
{
    public partial class MainViewModel
    {
        // ==================== CUSTOM RECOVERY HUB WORKSPACE ====================

        public ObservableCollection<RecoveryItem> RecoveryItems { get; } = new();

        private RecoveryItem? _selectedRecoveryItem;
        public RecoveryItem? SelectedRecoveryItem
        {
            get => _selectedRecoveryItem;
            set
            {
                _selectedRecoveryItem = value;
                OnPropertyChanged(nameof(SelectedRecoveryItem));
                if (value != null && !string.IsNullOrEmpty(value.FilePath))
                {
                    CustomRecoveryFilePath = value.FilePath;
                }
            }
        }

        private string _customRecoveryFilePath = string.Empty;
        public string CustomRecoveryFilePath
        {
            get => _customRecoveryFilePath;
            set
            {
                _customRecoveryFilePath = value;
                OnPropertyChanged(nameof(CustomRecoveryFilePath));
            }
        }

        public ObservableCollection<string> TargetPartitionList { get; } = new()
        {
            "Auto (Deteksi Cerdas)",
            "recovery",
            "boot (Virtual A/B)",
            "init_boot (Android 13+)",
            "vendor_boot (GKI)"
        };

        private string _selectedTargetPartition = "Auto (Deteksi Cerdas)";
        public string SelectedTargetPartition
        {
            get => _selectedTargetPartition;
            set
            {
                _selectedTargetPartition = value;
                OnPropertyChanged(nameof(SelectedTargetPartition));
            }
        }

        private bool _disableAvbVerityOnFlash = true;
        public bool DisableAvbVerityOnFlash
        {
            get => _disableAvbVerityOnFlash;
            set
            {
                _disableAvbVerityOnFlash = value;
                OnPropertyChanged(nameof(DisableAvbVerityOnFlash));
            }
        }

        public bool HasNoRecoveryItems => RecoveryItems == null || RecoveryItems.Count == 0;

        public async Task RefreshRecoveryCatalogAsync()
        {
            CurrentOperation = "Memindai Katalog Custom Recovery...";
            OperationProgress = 30;

            await Task.Run(() =>
            {
                var list = _recoveryEngine.ScanAvailableRecoveries();
                App.Current?.Dispatcher?.Invoke(() =>
                {
                    RecoveryItems.Clear();
                    foreach (var item in list) RecoveryItems.Add(item);
                });
            });

            OperationProgress = 100;
            CurrentOperation = $"Katalog Recovery Dimuat ({RecoveryItems.Count} File)";
            AddLog($"[Recovery Hub] Memuat {RecoveryItems.Count} custom recovery image dari database lokal.");
        }

        public async Task InstallSelectedRecoveryAsync()
        {
            var imagePath = !string.IsNullOrWhiteSpace(CustomRecoveryFilePath)
                ? CustomRecoveryFilePath
                : SelectedRecoveryItem?.FilePath;

            if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath))
            {
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "Pilih File Image Recovery (*.img)",
                    Filter = "Recovery Image (*.img)|*.img|All Files (*.*)|*.*",
                    CheckFileExists = true
                };

                if (dialog.ShowDialog() == true)
                {
                    imagePath = dialog.FileName;
                    CustomRecoveryFilePath = dialog.FileName;
                }
                else
                {
                    AddLog("[Batal] Pemilihan file recovery dibatalkan.");
                    return;
                }
            }

            CurrentOperation = "Flashing Custom Recovery via Fastboot...";
            OperationProgress = 25;

            var serial = SelectedDevice?.DeviceId;
            var targetPart = SelectedTargetPartition;
            if (targetPart.Contains("recovery")) targetPart = "recovery";
            else if (targetPart.Contains("boot (Virtual")) targetPart = "boot";
            else if (targetPart.Contains("init_boot")) targetPart = "init_boot";
            else if (targetPart.Contains("vendor_boot")) targetPart = "vendor_boot";
            else targetPart = "auto";

            var ok = await _recoveryEngine.InstallRecoveryViaFastbootAsync(imagePath, targetPart, DisableAvbVerityOnFlash, serial);
            OperationProgress = 100;
            CurrentOperation = ok ? "Instalasi Recovery Sukses" : "Instalasi Recovery Gagal";
        }

        public async Task BootSelectedRecoveryAsync()
        {
            var imagePath = !string.IsNullOrWhiteSpace(CustomRecoveryFilePath)
                ? CustomRecoveryFilePath
                : SelectedRecoveryItem?.FilePath;

            if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath))
            {
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "Pilih File Image Recovery untuk Boot Sementara (*.img)",
                    Filter = "Recovery Image (*.img)|*.img|All Files (*.*)|*.*",
                    CheckFileExists = true
                };

                if (dialog.ShowDialog() == true)
                {
                    imagePath = dialog.FileName;
                    CustomRecoveryFilePath = dialog.FileName;
                }
                else
                {
                    AddLog("[Batal] Pemilihan file recovery dibatalkan.");
                    return;
                }
            }

            CurrentOperation = "Booting Temporary Recovery via RAM...";
            OperationProgress = 30;

            var serial = SelectedDevice?.DeviceId;
            var ok = await _recoveryEngine.TemporaryBootRecoveryAsync(imagePath, serial);
            OperationProgress = 100;
            CurrentOperation = ok ? "Boot Temporary Recovery Sukses" : "Boot Temporary Recovery Gagal";
        }

        public void BrowseCustomRecovery()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Pilih Custom Recovery Image (*.img)",
                Filter = "Recovery Image (*.img)|*.img|All Files (*.*)|*.*",
                CheckFileExists = true
            };

            if (dialog.ShowDialog() == true)
            {
                var fileInfo = new FileInfo(dialog.FileName);
                CustomRecoveryFilePath = dialog.FileName;

                var customItem = new RecoveryItem
                {
                    Brand = "Custom",
                    Model = "Manual Selected",
                    Codename = Path.GetFileNameWithoutExtension(dialog.FileName),
                    RecoveryType = "Custom",
                    FileName = fileInfo.Name,
                    FilePath = dialog.FileName,
                    FileSizeFormatted = $"{fileInfo.Length / (1024.0 * 1024.0):F1} MB"
                };

                RecoveryItems.Add(customItem);
                SelectedRecoveryItem = customItem;
                AddLog($"[Recovery Hub] Memilih custom recovery manual: {customItem.FileName}");
            }
        }

        public void ClearCustomRecoveryFile()
        {
            CustomRecoveryFilePath = string.Empty;
            SelectedRecoveryItem = null;
            AddLog("[Recovery Hub] Kolom file recovery telah dibersihkan.");
        }

        public void OpenRecoveryFolder()
        {
            try
            {
                var baseDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "database", "recovery");
                if (!Directory.Exists(baseDir)) Directory.CreateDirectory(baseDir);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = baseDir,
                    UseShellExecute = true
                });
                AddLog($"[Recovery Hub] Membuka direktori penyimpanan database recovery: {baseDir}");
            }
            catch (Exception ex)
            {
                AddLog($"[Folder Error] {ex.Message}");
            }
        }

        public void OpenOfficialTwrpWeb()
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "https://twrp.me/Devices/",
                    UseShellExecute = true
                });
                AddLog("[Recovery Hub] Membuka Portal Resmi TWRP Devices (twrp.me/Devices/)...");
            }
            catch (Exception ex)
            {
                AddLog($"[Browser Error] {ex.Message}");
            }
        }

        public void OpenOfficialOrangeFoxWeb()
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "https://orangefox.download/",
                    UseShellExecute = true
                });
                AddLog("[Recovery Hub] Membuka Portal Resmi OrangeFox Recovery (orangefox.download)...");
            }
            catch (Exception ex)
            {
                AddLog($"[Browser Error] {ex.Message}");
            }
        }
    }
}
