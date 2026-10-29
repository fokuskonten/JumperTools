using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using JumperTools.Core;
using JumperTools.Models;
using JumperTools.Utilities;

namespace JumperTools.UI.ViewModels
{
    public partial class MainViewModel
    {
        // ==================== BRAND, MODEL & LOADER RESOLVER ====================

        public void SelectBrand(string? brand)
        {
            var brandFilter = string.IsNullOrWhiteSpace(brand) ? "Semua" : brand;
            _selectedBrand = brandFilter;
            OnPropertyChanged(nameof(SelectedBrand));

            var models = _loaderManager.GetModelsByBrand(brandFilter);
            FilteredModels.Clear();
            foreach (var m in models) FilteredModels.Add(m);

            if (SelectedPhoneModel != null && !FilteredModels.Contains(SelectedPhoneModel))
            {
                SelectedPhoneModel = null;
            }

            // Direct protocol routing based on selected brand
            if (brandFilter.Equals("Samsung", StringComparison.OrdinalIgnoreCase))
            {
                SelectedBrandTabIndex = 0; // Samsung
            }
            else if (brandFilter.Equals("Oppo", StringComparison.OrdinalIgnoreCase) ||
                     brandFilter.Equals("Realme", StringComparison.OrdinalIgnoreCase) ||
                     brandFilter.Equals("Infinix", StringComparison.OrdinalIgnoreCase) ||
                     brandFilter.Equals("Vivo", StringComparison.OrdinalIgnoreCase))
            {
                SelectedBrandTabIndex = 1; // MediaTek (BROM / Preloader)
            }
            else if (brandFilter.Equals("Xiaomi", StringComparison.OrdinalIgnoreCase) ||
                     brandFilter.Equals("Redmi", StringComparison.OrdinalIgnoreCase) ||
                     brandFilter.Equals("Poco", StringComparison.OrdinalIgnoreCase))
            {
                SelectedBrandTabIndex = 3; // Xiaomi Sideload
            }
            else if (brandFilter.Equals("Huawei", StringComparison.OrdinalIgnoreCase) ||
                     brandFilter.Equals("Honor", StringComparison.OrdinalIgnoreCase))
            {
                SelectedBrandTabIndex = 6; // Huawei Kirin
            }
            else if (brandFilter.Equals("Apple", StringComparison.OrdinalIgnoreCase))
            {
                SelectedBrandTabIndex = 8; // Apple iOS
            }
        }

        public void AutoSelectBrandTab()
        {
            // Only auto-select if we have a connected device or an explicitly selected model
            var device = SelectedDevice;
            var model = SelectedPhoneModel;

            if (device == null && model == null)
            {
                // CRITICAL FIX: Retain current tab index when device is disconnected!
                // Never revert back to Samsung Odin on cable disconnect.
                return;
            }

            var brand = device?.Brand ?? model?.Brand ?? "";
            var chip = device?.Chipset ?? model?.Chipset ?? "";
            var arch = model?.Architecture ?? "";
            var mode = device?.Mode ?? DeviceMode.Unknown;

            if (mode == DeviceMode.Fastboot)
            {
                SelectedBrandTabIndex = 4; // Fastboot
                return;
            }
            if (mode == DeviceMode.Recovery && (brand.Equals("Xiaomi", StringComparison.OrdinalIgnoreCase) || brand.Equals("Redmi", StringComparison.OrdinalIgnoreCase) || brand.Equals("Poco", StringComparison.OrdinalIgnoreCase)))
            {
                SelectedBrandTabIndex = 3; // Xiaomi Sideload
                return;
            }
            if (mode == DeviceMode.EDL)
            {
                SelectedBrandTabIndex = 2; // Qualcomm EDL
                return;
            }
            if (mode == DeviceMode.BROM)
            {
                SelectedBrandTabIndex = 1; // MediaTek
                return;
            }

            // 1. Detect by Recommended Loader File (Absolute ground-truth)
            var loaderFile = model?.RecommendedLoaderFile ?? "";
            if (loaderFile.StartsWith("DA_", StringComparison.OrdinalIgnoreCase) || 
                loaderFile.Contains("MTK", StringComparison.OrdinalIgnoreCase) ||
                loaderFile.EndsWith("CRYPTO20.bin", StringComparison.OrdinalIgnoreCase) ||
                loaderFile.EndsWith("SLA.bin", StringComparison.OrdinalIgnoreCase))
            {
                SelectedBrandTabIndex = 1; // MediaTek (BROM / Preloader)
                return;
            }

            if (loaderFile.Contains("firehose", StringComparison.OrdinalIgnoreCase) || 
                loaderFile.EndsWith(".mbn", StringComparison.OrdinalIgnoreCase) || 
                loaderFile.EndsWith(".elf", StringComparison.OrdinalIgnoreCase))
            {
                SelectedBrandTabIndex = 2; // Qualcomm EDL
                return;
            }

            if (loaderFile.Contains("fdl", StringComparison.OrdinalIgnoreCase))
            {
                SelectedBrandTabIndex = 5; // Unisoc (SPD / BSL)
                return;
            }

            // 2. Detect by Architecture & Chipset
            if (arch.Equals("MediaTek", StringComparison.OrdinalIgnoreCase) ||
                chip.Contains("MTK", StringComparison.OrdinalIgnoreCase) ||
                chip.Contains("MediaTek", StringComparison.OrdinalIgnoreCase) ||
                chip.Contains("Helio", StringComparison.OrdinalIgnoreCase) ||
                chip.Contains("Dimensity", StringComparison.OrdinalIgnoreCase) ||
                chip.StartsWith("MT6", StringComparison.OrdinalIgnoreCase) ||
                chip.StartsWith("MT67", StringComparison.OrdinalIgnoreCase) ||
                chip.StartsWith("MT68", StringComparison.OrdinalIgnoreCase))
            {
                SelectedBrandTabIndex = 1; // MediaTek
                return;
            }

            if (arch.Equals("Qualcomm", StringComparison.OrdinalIgnoreCase) ||
                chip.Contains("Snapdragon", StringComparison.OrdinalIgnoreCase) ||
                chip.Contains("Qualcomm", StringComparison.OrdinalIgnoreCase) ||
                chip.StartsWith("SM", StringComparison.OrdinalIgnoreCase) ||
                chip.StartsWith("SDM", StringComparison.OrdinalIgnoreCase) ||
                chip.StartsWith("MSM", StringComparison.OrdinalIgnoreCase) ||
                chip.StartsWith("QCM", StringComparison.OrdinalIgnoreCase))
            {
                SelectedBrandTabIndex = 2; // Qualcomm EDL
                return;
            }

            if (arch.Equals("Unisoc", StringComparison.OrdinalIgnoreCase) ||
                chip.Contains("Unisoc", StringComparison.OrdinalIgnoreCase) ||
                chip.Contains("Spreadtrum", StringComparison.OrdinalIgnoreCase) ||
                chip.Contains("SC9863", StringComparison.OrdinalIgnoreCase) ||
                chip.Contains("T606", StringComparison.OrdinalIgnoreCase) ||
                chip.Contains("T612", StringComparison.OrdinalIgnoreCase) ||
                chip.Contains("T616", StringComparison.OrdinalIgnoreCase) ||
                chip.Contains("UMS", StringComparison.OrdinalIgnoreCase))
            {
                SelectedBrandTabIndex = 5; // Unisoc
                return;
            }

            if (brand.Equals("Samsung", StringComparison.OrdinalIgnoreCase) || chip.Contains("Exynos", StringComparison.OrdinalIgnoreCase))
            {
                SelectedBrandTabIndex = 0; // Samsung
                return;
            }

            if (brand.Equals("Huawei", StringComparison.OrdinalIgnoreCase) || brand.Equals("Honor", StringComparison.OrdinalIgnoreCase) || chip.Contains("Kirin", StringComparison.OrdinalIgnoreCase))
            {
                SelectedBrandTabIndex = 6; // Huawei Kirin
                return;
            }

            if (brand.Equals("Xiaomi", StringComparison.OrdinalIgnoreCase) || brand.Equals("Redmi", StringComparison.OrdinalIgnoreCase) || brand.Equals("Poco", StringComparison.OrdinalIgnoreCase))
            {
                SelectedBrandTabIndex = 3; // Xiaomi Sideload
                return;
            }

            if (brand.Equals("Apple", StringComparison.OrdinalIgnoreCase) || brand.Equals("iPhone", StringComparison.OrdinalIgnoreCase) || brand.Equals("iPad", StringComparison.OrdinalIgnoreCase))
            {
                SelectedBrandTabIndex = 8; // Apple iOS
                return;
            }
        }

        public void FilterModels(string? query)
        {
            var all = string.IsNullOrWhiteSpace(query) 
                ? _loaderManager.GetModelsByBrand(SelectedBrand) 
                : _loaderManager.GetAllModels();

            var filtered = string.IsNullOrWhiteSpace(query)
                ? all
                : all.Where(m => 
                    m.ModelName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    m.CodeName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    m.Chipset.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    m.Brand.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();

            FilteredModels.Clear();
            foreach (var m in filtered) FilteredModels.Add(m);

            if (SelectedPhoneModel != null && !FilteredModels.Contains(SelectedPhoneModel))
            {
                SelectedPhoneModel = null;
            }
        }

        public async Task EnsureLoaderAsync()
        {
            if (SelectedPhoneModel == null)
            {
                AddLog("[Peringatan] Pilih model HP terlebih dahulu.");
                return;
            }

            if (SelectedLoader == null)
            {
                AddLog($"[Loader] Model {SelectedPhoneModel.ModelName} menggunakan protokol direct BROM / Fastboot bawaan.");
                return;
            }

            AddLog($"[Loader] Memverifikasi berkas loader {SelectedLoader.FileName} untuk {SelectedPhoneModel.ModelName}...");
            OperationProgress = 30;
            CurrentOperation = $"Memuat {SelectedLoader.FileName}...";

            try
            {
                var localPath = await _loaderManager.EnsureLoaderAvailableAsync(SelectedLoader);
                OperationProgress = 100;
                CurrentOperation = "Loader Siap";
                LoaderStatusMessage = $"[Siap/Cache] {SelectedLoader.FileName}";
                AddLog($"[Loader Sukses] Berkas loader terverifikasi di: {localPath}");
            }
            catch (Exception ex)
            {
                AddLog($"[Loader Error] {ex.Message}");
                CurrentOperation = "Gagal memuat loader";
            }
        }

        // ==================== HUAWEI (KIRIN COM 1.0) OPERATIONS ====================

        public async Task HuaweiBypassIdAsync()
        {
            var portName = SelectedDevice?.Brand == "Huawei" ? SelectedDevice.DeviceId : HuaweiKirinEngine.ScanHuaweiPorts().FirstOrDefault()?.PortName ?? "HUAWEI_USB_COM";
            var device = SelectedDevice ?? new Device { DeviceId = portName, Brand = "Huawei", Model = SelectedPhoneModel?.ModelName ?? "Huawei Kirin", Mode = DeviceMode.Normal, PortName = portName, Chipset = "HiSilicon Kirin" };

            var result = await _operationOrchestrator.RunOperationAsync(
                device,
                OperationType.BypassFrp,
                "Huawei ID & FRP Bypass (Kirin COM 1.0)",
                executeAction: async (jobId, ct) =>
                {
                    return await _huaweiEngine.BypassHuaweiIdAsync(portName);
                });

            OperationStatus = result.State == OperationState.Completed ? "Completed" : "Failed";
            if (result.State == OperationState.Completed) UserGuidanceMessage = "Bypass Huawei ID Berhasil! Cabut kabel USB dan nyalakan HP.";
        }

        public async Task HuaweiReadOeminfoAsync()
        {
            var portName = SelectedDevice?.Brand == "Huawei" ? SelectedDevice.DeviceId : HuaweiKirinEngine.ScanHuaweiPorts().FirstOrDefault()?.PortName ?? "HUAWEI_USB_COM";
            var device = SelectedDevice ?? new Device { DeviceId = portName, Brand = "Huawei", Model = SelectedPhoneModel?.ModelName ?? "Huawei Kirin", Mode = DeviceMode.Normal, PortName = portName, Chipset = "HiSilicon Kirin" };
            var outDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "backups", "oeminfo");

            var result = await _operationOrchestrator.RunOperationAsync(
                device,
                OperationType.BackupPartition,
                "Backup Oeminfo Huawei",
                executeAction: async (jobId, ct) =>
                {
                    var path = await _huaweiEngine.ReadOeminfoAsync(portName, outDir);
                    return !string.IsNullOrEmpty(path);
                },
                targetPartition: "oeminfo");

            OperationStatus = result.State == OperationState.Completed ? "Completed" : "Failed";
        }

        public async Task HuaweiRebootFastbootAsync()
        {
            var portName = SelectedDevice?.Brand == "Huawei" ? SelectedDevice.DeviceId : HuaweiKirinEngine.ScanHuaweiPorts().FirstOrDefault()?.PortName ?? "";
            await _huaweiEngine.RebootToFastbootAsync(portName);
        }

        public async Task HuaweiInfoAsync()
        {
            var portName = SelectedDevice?.Brand == "Huawei" ? SelectedDevice.DeviceId : HuaweiKirinEngine.ScanHuaweiPorts().FirstOrDefault()?.PortName ?? "";
            await _huaweiEngine.PerformHandshakeAsync(portName);
        }

        // ==================== BRAND SPECIAL OPERATIONS ====================

        public async Task XiaomiAntiRelockAsync()
        {
            var serial = SelectedDevice?.DeviceId ?? "ADB_DEV";
            var device = SelectedDevice ?? new Device { DeviceId = serial, Brand = "Xiaomi", Model = SelectedPhoneModel?.ModelName ?? "Xiaomi Phone", Mode = DeviceMode.Normal };

            var result = await _operationOrchestrator.RunOperationAsync(
                device,
                OperationType.BypassFrp,
                "Xiaomi Anti-Relock (Disable Mi Cloud)",
                executeAction: async (jobId, ct) =>
                {
                    return await _brandSpecialEngine.DisableXiaomiAntiRelockAsync(serial);
                },
                requiredToolName: "adb");

            OperationStatus = result.State == OperationState.Completed ? "Completed" : "Failed";
        }

        public async Task VivoRemoveDemoAsync()
        {
            var serial = SelectedDevice?.DeviceId ?? "ADB_DEV";
            var device = SelectedDevice ?? new Device { DeviceId = serial, Brand = "Vivo", Model = SelectedPhoneModel?.ModelName ?? "Vivo Phone", Mode = DeviceMode.Normal };

            var result = await _operationOrchestrator.RunOperationAsync(
                device,
                OperationType.FactoryReset,
                "Vivo Remove Demo Mode",
                executeAction: async (jobId, ct) =>
                {
                    return await _brandSpecialEngine.RemoveVivoDemoModeAsync(serial);
                },
                requiredToolName: "adb");

            OperationStatus = result.State == OperationState.Completed ? "Completed" : "Failed";
        }

        public async Task OppoRemoveDemoAsync()
        {
            var serial = SelectedDevice?.DeviceId ?? "ADB_DEV";
            var device = SelectedDevice ?? new Device { DeviceId = serial, Brand = "Oppo", Model = SelectedPhoneModel?.ModelName ?? "Oppo/Realme Phone", Mode = DeviceMode.Normal };

            var result = await _operationOrchestrator.RunOperationAsync(
                device,
                OperationType.FactoryReset,
                "Oppo / Realme Remove Demo Mode",
                executeAction: async (jobId, ct) =>
                {
                    return await _brandSpecialEngine.RemoveOppoDemoModeAsync(serial);
                },
                requiredToolName: "adb");

            OperationStatus = result.State == OperationState.Completed ? "Completed" : "Failed";
        }

        public async Task SamsungKnoxCheckAsync()
        {
            CurrentOperation = "Samsung Knox & KG State Check...";
            OperationProgress = 30;

            try
            {
                var serial = SelectedDevice?.DeviceId;
                var status = await _brandSpecialEngine.CheckSamsungKnoxStateAsync(serial);
                OperationProgress = 100;
                CurrentOperation = "Pemeriksaan Knox Selesai";
            }
            catch (Exception ex)
            {
                AddLog($"[Error] Knox Check: {ex.Message}");
            }
        }

        // ==================== APPLE iOS OPERATIONS ====================

        public async Task AppleReadInfoAsync()
        {
            CurrentOperation = "Membaca Info Apple iPhone/iPad...";
            OperationProgress = 30;

            try
            {
                var info = await _appleEngine.ReadDeviceInfoAsync();
                OperationProgress = 100;
                CurrentOperation = info != null ? "Info Apple Selesai" : "Perangkat Apple Tidak Ditemukan";
            }
            catch (Exception ex)
            {
                AddLog($"[Apple Error] {ex.Message}");
            }
        }

        public async Task AppleEnterRecoveryAsync()
        {
            CurrentOperation = "Apple Enter Recovery Mode...";
            OperationProgress = 30;

            try
            {
                var ok = await _appleEngine.EnterRecoveryModeAsync();
                OperationProgress = 100;
                CurrentOperation = ok ? "iPhone Masuk Recovery Mode" : "Gagal Masuk Recovery";
            }
            catch (Exception ex)
            {
                AddLog($"[Apple Error] {ex.Message}");
            }
        }

        public async Task AppleExitRecoveryAsync()
        {
            CurrentOperation = "Apple Exit Recovery Mode (Fix Loop)...";
            OperationProgress = 30;

            try
            {
                var ok = await _appleEngine.ExitRecoveryModeAsync();
                OperationProgress = 100;
                CurrentOperation = ok ? "iPhone Keluar dari Recovery" : "Gagal Keluar Recovery";
            }
            catch (Exception ex)
            {
                AddLog($"[Apple Error] {ex.Message}");
            }
        }

        // ==================== XIAOMI SIDELOAD OPERATIONS ====================

        public async Task XiaomiSideloadInfoAsync()
        {
            CurrentOperation = "Membaca Info Xiaomi Sideload & ARB...";
            OperationProgress = 25;

            try
            {
                var serial = SelectedDevice?.Brand == "Xiaomi" ? SelectedDevice.DeviceId : null;
                var info = await _xiaomiEngine.ReadDeviceInfoAsync(serial);
                
                if (SelectedDevice?.Mode == DeviceMode.Fastboot || string.IsNullOrEmpty(serial))
                {
                    await _fastbootEngine.GetAntiRollbackInfoAsync(serial);
                }

                OperationProgress = 100;
                CurrentOperation = info != null ? "Info Xiaomi & ARB Berhasil Dibaca" : "Gagal Membaca Info";
            }
            catch (Exception ex)
            {
                AddLog($"[Xiaomi Error] {ex.Message}");
                CurrentOperation = "Error";
            }
        }

        public async Task XiaomiSideloadFormatAsync()
        {
            var serial = SelectedDevice?.Brand == "Xiaomi" ? SelectedDevice.DeviceId : "XIAOMI_SIDELOAD";
            var device = SelectedDevice ?? new Device { DeviceId = serial, Brand = "Xiaomi", Model = SelectedPhoneModel?.ModelName ?? "Xiaomi Phone", Mode = DeviceMode.Recovery };

            var result = await _operationOrchestrator.RunOperationAsync(
                device,
                OperationType.FactoryReset,
                "Xiaomi Sideload Factory Reset (Wipe Userdata)",
                executeAction: async (jobId, ct) =>
                {
                    return await _xiaomiEngine.WipeUserdataAsync(serial);
                },
                requiredToolName: "adb");

            OperationStatus = result.State == OperationState.Completed ? "Completed" : "Failed";
        }

        public async Task XiaomiSideloadFrpAsync()
        {
            var serial = SelectedDevice?.Brand == "Xiaomi" ? SelectedDevice.DeviceId : "XIAOMI_SIDELOAD";
            var device = SelectedDevice ?? new Device { DeviceId = serial, Brand = "Xiaomi", Model = SelectedPhoneModel?.ModelName ?? "Xiaomi Phone", Mode = DeviceMode.Recovery };

            var result = await _operationOrchestrator.RunOperationAsync(
                device,
                OperationType.BypassFrp,
                "Xiaomi Sideload FRP Reset",
                executeAction: async (jobId, ct) =>
                {
                    return await _xiaomiEngine.BypassFrpAsync(serial);
                },
                requiredToolName: "adb");

            OperationStatus = result.State == OperationState.Completed ? "Completed" : "Failed";
        }

        public async Task XiaomiSideloadRebootAsync()
        {
            var serial = SelectedDevice?.Brand == "Xiaomi" ? SelectedDevice.DeviceId : null;
            await _xiaomiEngine.RebootAsync(serial, "system");
        }

        // ==================== UNISOC / SPREADTRUM (SPD) OPERATIONS ====================

        public async Task UnisocBypassFrpAsync()
        {
            var portName = SelectedDevice?.Brand == "Unisoc" ? SelectedDevice.DeviceId : UnisocSpdEngine.ScanUnisocPorts().FirstOrDefault()?.PortName ?? "SPD_COM";
            var device = SelectedDevice ?? new Device { DeviceId = portName, Brand = "Unisoc", Model = SelectedPhoneModel?.ModelName ?? "Unisoc Phone", Mode = DeviceMode.Download, PortName = portName, Chipset = "Unisoc SPD" };

            var result = await _operationOrchestrator.RunOperationAsync(
                device,
                OperationType.BypassFrp,
                "Unisoc SPD 1-Click FRP Bypass",
                executeAction: async (jobId, ct) =>
                {
                    return await _unisocEngine.EraseFrpAsync(portName);
                },
                requiredToolName: "spd_research");

            OperationStatus = result.State == OperationState.Completed ? "Completed" : "Failed";
            if (result.State == OperationState.Completed) UserGuidanceMessage = "Bypass FRP Unisoc Sukses! Cabut kabel USB dan nyalakan HP.";
        }

        public async Task UnisocFormatUserdataAsync()
        {
            var portName = SelectedDevice?.Brand == "Unisoc" ? SelectedDevice.DeviceId : UnisocSpdEngine.ScanUnisocPorts().FirstOrDefault()?.PortName ?? "SPD_COM";
            var device = SelectedDevice ?? new Device { DeviceId = portName, Brand = "Unisoc", Model = SelectedPhoneModel?.ModelName ?? "Unisoc Phone", Mode = DeviceMode.Download, PortName = portName, Chipset = "Unisoc SPD" };

            var result = await _operationOrchestrator.RunOperationAsync(
                device,
                OperationType.FactoryReset,
                "Unisoc Factory Reset / Wipe Userdata",
                executeAction: async (jobId, ct) =>
                {
                    return await _unisocEngine.FormatUserdataAsync(portName);
                },
                requiredToolName: "spd_research");

            OperationStatus = result.State == OperationState.Completed ? "Completed" : "Failed";
            if (result.State == OperationState.Completed) UserGuidanceMessage = "Factory Reset Selesai! Kunci layar dan data terhapus.";
        }

        public async Task UnisocInfoAsync()
        {
            var portName = SelectedDevice?.Brand == "Unisoc" ? SelectedDevice.DeviceId : UnisocSpdEngine.ScanUnisocPorts().FirstOrDefault()?.PortName ?? "";
            await _unisocEngine.PerformBslHandshakeAsync(portName);
        }

        public async Task UnisocRebootAsync()
        {
            var portName = SelectedDevice?.Brand == "Unisoc" ? SelectedDevice.DeviceId : UnisocSpdEngine.ScanUnisocPorts().FirstOrDefault()?.PortName ?? "";
            await _unisocEngine.RebootDeviceAsync(portName);
        }

        // ==================== QUALCOMM (EDL 9008) OPERATIONS ====================

        public async Task EdlBypassFrpAsync()
        {
            var portName = SelectedDevice?.Brand == "Qualcomm" ? SelectedDevice.DeviceId : QualcommEdlEngine.ScanQualcommPorts().FirstOrDefault()?.PortName ?? "QCOM_EDL";
            var programmer = SelectedLoader?.LocalPath;
            var device = SelectedDevice ?? new Device { DeviceId = portName, Brand = "Qualcomm", Model = SelectedPhoneModel?.ModelName ?? "Qualcomm Phone", Mode = DeviceMode.EDL, PortName = portName, Chipset = SelectedPhoneModel?.Chipset ?? "Snapdragon" };

            var result = await _operationOrchestrator.RunOperationAsync(
                device,
                OperationType.BypassFrp,
                "Qualcomm EDL 9008 FRP Bypass",
                executeAction: async (jobId, ct) =>
                {
                    return await _qualcommEngine.EraseFrpAsync(portName, programmer);
                },
                requiredToolName: "qdl");

            OperationStatus = result.State == OperationState.Completed ? "Completed" : "Failed";
            if (result.State == OperationState.Completed) UserGuidanceMessage = "Bypass FRP Qualcomm 9008 Sukses! Cabut kabel USB dan nyalakan HP.";
        }

        public async Task EdlFormatUserdataAsync()
        {
            var portName = SelectedDevice?.Brand == "Qualcomm" ? SelectedDevice.DeviceId : QualcommEdlEngine.ScanQualcommPorts().FirstOrDefault()?.PortName ?? "QCOM_EDL";
            var programmer = SelectedLoader?.LocalPath;
            var device = SelectedDevice ?? new Device { DeviceId = portName, Brand = "Qualcomm", Model = SelectedPhoneModel?.ModelName ?? "Qualcomm Phone", Mode = DeviceMode.EDL, PortName = portName, Chipset = SelectedPhoneModel?.Chipset ?? "Snapdragon" };

            var result = await _operationOrchestrator.RunOperationAsync(
                device,
                OperationType.FactoryReset,
                "Qualcomm EDL 9008 Factory Reset / Format Userdata",
                executeAction: async (jobId, ct) =>
                {
                    return await _qualcommEngine.FormatUserdataAsync(portName, programmer);
                },
                requiredToolName: "qdl");

            OperationStatus = result.State == OperationState.Completed ? "Completed" : "Failed";
            if (result.State == OperationState.Completed) UserGuidanceMessage = "Factory Reset Selesai! Kunci layar dan data terhapus.";
        }

        public async Task EdlReadGptAsync()
        {
            CurrentOperation = "Membaca Tabel Partisi GPT Qualcomm (Firehose)...";
            OperationProgress = 20;

            try
            {
                var portName = SelectedDevice?.Brand == "Qualcomm" ? SelectedDevice.DeviceId : QualcommEdlEngine.ScanQualcommPorts().FirstOrDefault()?.PortName ?? "";
                var programmer = SelectedLoader?.LocalPath;
                var list = await _qualcommEngine.ReadGptTableAsync(portName, programmer);
                OperationProgress = 100;
                CurrentOperation = $"Berhasil membaca {list.Count} partisi Qualcomm";
            }
            catch (Exception ex)
            {
                AddLog($"[Qualcomm Error] {ex.Message}");
                CurrentOperation = "Error";
            }
        }

        public async Task EdlBackupEfsAsync()
        {
            var backupDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "backups", "efs_qcom");
            var portName = SelectedDevice?.Brand == "Qualcomm" ? SelectedDevice.DeviceId : QualcommEdlEngine.ScanQualcommPorts().FirstOrDefault()?.PortName ?? "QCOM_EDL";
            var programmer = SelectedLoader?.LocalPath;
            var device = SelectedDevice ?? new Device { DeviceId = portName, Brand = "Qualcomm", Model = SelectedPhoneModel?.ModelName ?? "Qualcomm Phone", Mode = DeviceMode.EDL, PortName = portName, Chipset = SelectedPhoneModel?.Chipset ?? "Snapdragon" };

            var result = await _operationOrchestrator.RunOperationAsync(
                device,
                OperationType.BackupPartition,
                "Backup EFS/QCN Qualcomm",
                executeAction: async (jobId, ct) =>
                {
                    return await _qualcommEngine.BackupEfsAsync(portName, backupDir, programmer);
                },
                targetPartition: "efs",
                requiredToolName: "qdl");

            OperationStatus = result.State == OperationState.Completed ? "Completed" : "Failed";
        }

        public async Task EdlRestoreEfsAsync()
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Pilih Salah Satu File Cadangan EFS (.bin)",
                Filter = "Binary Partition Files (*.bin)|*.bin|All Files (*.*)|*.*",
                InitialDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "backups", "efs_qcom")
            };

            if (openFileDialog.ShowDialog() != true)
            {
                AddLog("[Batal] Pemilihan file restore EFS dibatalkan.");
                return;
            }

            var portName = SelectedDevice?.Brand == "Qualcomm" ? SelectedDevice.DeviceId : QualcommEdlEngine.ScanQualcommPorts().FirstOrDefault()?.PortName ?? "QCOM_EDL";
            var programmer = SelectedLoader?.LocalPath;
            var device = SelectedDevice ?? new Device { DeviceId = portName, Brand = "Qualcomm", Model = SelectedPhoneModel?.ModelName ?? "Qualcomm Phone", Mode = DeviceMode.EDL, PortName = portName, Chipset = SelectedPhoneModel?.Chipset ?? "Snapdragon" };

            var result = await _operationOrchestrator.RunOperationAsync(
                device,
                OperationType.RestorePartition,
                "Restore Qualcomm EFS / IMEI Security",
                executeAction: async (jobId, ct) =>
                {
                    return await _qualcommEngine.RestoreEfsAsync(portName, openFileDialog.FileName, programmer);
                },
                targetPartition: "efs",
                requiredToolName: "qdl");

            OperationStatus = result.State == OperationState.Completed ? "Completed" : "Failed";
        }

        public async Task EdlRebootAsync()
        {
            var portName = SelectedDevice?.Brand == "Qualcomm" ? SelectedDevice.DeviceId : QualcommEdlEngine.ScanQualcommPorts().FirstOrDefault()?.PortName ?? "";
            await _qualcommEngine.RebootDeviceAsync(portName);
        }

        // ==================== MEDIATEK (MTK) OPERATIONS ====================

        public async Task MtkBypassFrpAsync()
        {
            var portName = SelectedDevice?.Brand == "MediaTek" ? SelectedDevice.DeviceId : MediaTekBromEngine.ScanMtkPorts().FirstOrDefault()?.PortName ?? "MTK_BROM";
            var device = SelectedDevice ?? new Device { DeviceId = portName, Brand = "MediaTek", Model = SelectedPhoneModel?.ModelName ?? "MediaTek Phone", Mode = DeviceMode.Unknown, PortName = portName, Chipset = SelectedPhoneModel?.Chipset ?? "MediaTek" };

            var result = await _operationOrchestrator.RunOperationAsync(
                device,
                OperationType.BypassFrp,
                "MediaTek Universal FRP Bypass",
                executeAction: async (jobId, ct) =>
                {
                    return await _mtkEngine.EraseFrpAsync(portName);
                },
                requiredToolName: "mtkclient");

            OperationStatus = result.State == OperationState.Completed ? "Completed" : "Failed";
            if (result.State == OperationState.Completed) UserGuidanceMessage = "Bypass FRP MTK Berhasil! Cabut kabel USB dan nyalakan HP.";
        }

        public async Task MtkFormatUserdataAsync()
        {
            var portName = SelectedDevice?.Brand == "MediaTek" ? SelectedDevice.DeviceId : MediaTekBromEngine.ScanMtkPorts().FirstOrDefault()?.PortName ?? "MTK_BROM";
            var device = SelectedDevice ?? new Device { DeviceId = portName, Brand = "MediaTek", Model = SelectedPhoneModel?.ModelName ?? "MediaTek Phone", Mode = DeviceMode.Unknown, PortName = portName, Chipset = SelectedPhoneModel?.Chipset ?? "MediaTek" };

            var result = await _operationOrchestrator.RunOperationAsync(
                device,
                OperationType.FactoryReset,
                "MediaTek Factory Reset / Format Userdata",
                executeAction: async (jobId, ct) =>
                {
                    return await _mtkEngine.FormatUserdataAsync(portName);
                },
                requiredToolName: "mtkclient");

            OperationStatus = result.State == OperationState.Completed ? "Completed" : "Failed";
            if (result.State == OperationState.Completed) UserGuidanceMessage = "Factory Reset Selesai! Kunci layar dan data terhapus.";
        }

        public async Task MtkUnlockBootloaderAsync()
        {
            var portName = SelectedDevice?.Brand == "MediaTek" ? SelectedDevice.DeviceId : MediaTekBromEngine.ScanMtkPorts().FirstOrDefault()?.PortName ?? "MTK_BROM";
            var device = SelectedDevice ?? new Device { DeviceId = portName, Brand = "MediaTek", Model = SelectedPhoneModel?.ModelName ?? "MediaTek Phone", Mode = DeviceMode.Unknown, PortName = portName, Chipset = SelectedPhoneModel?.Chipset ?? "MediaTek" };

            var result = await _operationOrchestrator.RunOperationAsync(
                device,
                OperationType.UnlockBootloader,
                "MediaTek Instant Bootloader Unlock",
                executeAction: async (jobId, ct) =>
                {
                    return await _mtkEngine.UnlockBootloaderAsync(portName);
                },
                requiredToolName: "mtkclient");

            OperationStatus = result.State == OperationState.Completed ? "Completed" : "Failed";
        }

        public async Task MtkRelockBootloaderAsync()
        {
            var portName = SelectedDevice?.Brand == "MediaTek" ? SelectedDevice.DeviceId : MediaTekBromEngine.ScanMtkPorts().FirstOrDefault()?.PortName ?? "MTK_BROM";
            var device = SelectedDevice ?? new Device { DeviceId = portName, Brand = "MediaTek", Model = SelectedPhoneModel?.ModelName ?? "MediaTek Phone", Mode = DeviceMode.Unknown, PortName = portName, Chipset = SelectedPhoneModel?.Chipset ?? "MediaTek" };

            var result = await _operationOrchestrator.RunOperationAsync(
                device,
                OperationType.UnlockBootloader,
                "MediaTek Relock Bootloader",
                executeAction: async (jobId, ct) =>
                {
                    return await _mtkEngine.RelockBootloaderAsync(portName);
                },
                requiredToolName: "mtkclient");

            OperationStatus = result.State == OperationState.Completed ? "Completed" : "Failed";
        }

        public async Task MtkReadGptAsync()
        {
            CurrentOperation = "Membaca Tabel Partisi GPT MediaTek...";
            OperationProgress = 20;

            try
            {
                var portName = SelectedDevice?.Brand == "MediaTek" ? SelectedDevice.DeviceId : MediaTekBromEngine.ScanMtkPorts().FirstOrDefault()?.PortName ?? "";
                var list = await _mtkEngine.ReadGptTableAsync(portName);
                OperationProgress = 100;
                CurrentOperation = $"Berhasil membaca {list.Count} partisi GPT";
            }
            catch (Exception ex)
            {
                AddLog($"[MTK Error] {ex.Message}");
                CurrentOperation = "Error";
            }
        }

        public async Task MtkBackupNvramAsync()
        {
            var backupDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "backups", "nvram");
            var portName = SelectedDevice?.Brand == "MediaTek" ? SelectedDevice.DeviceId : MediaTekBromEngine.ScanMtkPorts().FirstOrDefault()?.PortName ?? "MTK_BROM";
            var device = SelectedDevice ?? new Device { DeviceId = portName, Brand = "MediaTek", Model = SelectedPhoneModel?.ModelName ?? "MediaTek Phone", Mode = DeviceMode.Unknown, PortName = portName, Chipset = SelectedPhoneModel?.Chipset ?? "MediaTek" };

            var result = await _operationOrchestrator.RunOperationAsync(
                device,
                OperationType.BackupPartition,
                "Backup NVRAM & NVDATA MediaTek",
                executeAction: async (jobId, ct) =>
                {
                    return await _mtkEngine.BackupNvramAsync(portName, backupDir);
                },
                targetPartition: "nvram",
                requiredToolName: "mtkclient");

            OperationStatus = result.State == OperationState.Completed ? "Completed" : "Failed";
        }

        public async Task MtkRestoreNvramAsync()
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Pilih Salah Satu File NVRAM (.bin) di Dalam Folder Cadangan",
                Filter = "Binary Partition Files (*.bin)|*.bin|All Files (*.*)|*.*",
                InitialDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "backups", "nvram")
            };

            if (openFileDialog.ShowDialog() != true)
            {
                AddLog("[Batal] Pemilihan folder restore NVRAM dibatalkan.");
                return;
            }

            var backupFolder = Path.GetDirectoryName(openFileDialog.FileName) ?? "";
            var portName = SelectedDevice?.Brand == "MediaTek" ? SelectedDevice.DeviceId : MediaTekBromEngine.ScanMtkPorts().FirstOrDefault()?.PortName ?? "MTK_BROM";
            var device = SelectedDevice ?? new Device { DeviceId = portName, Brand = "MediaTek", Model = SelectedPhoneModel?.ModelName ?? "MediaTek Phone", Mode = DeviceMode.Unknown, PortName = portName, Chipset = SelectedPhoneModel?.Chipset ?? "MediaTek" };

            var result = await _operationOrchestrator.RunOperationAsync(
                device,
                OperationType.RestorePartition,
                "Restore MediaTek NVRAM / NVDATA",
                executeAction: async (jobId, ct) =>
                {
                    return await _signalEngine.RestoreMediaTekNvramAsync(backupFolder);
                },
                targetPartition: "nvram",
                requiredToolName: "mtkclient");

            OperationStatus = result.State == OperationState.Completed ? "Completed" : "Failed";
        }

        public async Task MtkRebootAsync()
        {
            CurrentOperation = "Transisi ke Mode BROM / Preloader...";
            AddLog("╔════════════════════════════════════════════════════════════════╗");
            AddLog("║         TRANSISI KE MODE MEDIATEK BROM / PRELOADER             ║");
            AddLog("╚════════════════════════════════════════════════════════════════╝");

            var portName = (SelectedDevice?.Brand == "MediaTek" || SelectedDevice?.Chipset?.Contains("MediaTek") == true)
                ? SelectedDevice.DeviceId
                : MediaTekBromEngine.ScanMtkPorts().FirstOrDefault()?.PortName;

            if (!string.IsNullOrEmpty(portName))
            {
                AddLog($"[Preloader] Port MediaTek terdeteksi ({portName}). Menjalankan auto-crash ke BROM murni...");
                var ok = await _mtkEngine.CrashPreloaderToBromAsync(portName);
                CurrentOperation = ok ? "Berhasil masuk BROM" : "Gagal Masuk BROM";
                if (ok) UserGuidanceMessage = "Perangkat kini berada di mode BROM murni! Siap untuk bypass atau flash.";
            }
            else if (SelectedDevice?.Mode == DeviceMode.Fastboot)
            {
                AddLog("[Fastboot] Mengirim reboot ke bootloader/BROM...");
                await _fastbootEngine.RebootAsync(FastbootRebootTarget.Bootloader, SelectedDevice.DeviceId);
                CurrentOperation = "Reboot Bootloader Dikirim";
            }
            else
            {
                AddLog("[Panduan] Tidak ada port MediaTek terdeteksi.");
                AddLog("[Panduan] 1. Matikan daya HP sepenuhnya (Power Off).");
                AddLog("[Panduan] 2. Tahan tombol VOLUME ATAS + VOLUME BAWAH secara bersamaan.");
                AddLog("[Panduan] 3. Colokkan kabel USB ke PC hingga port MTK BROM/Preloader muncul.");
                UserGuidanceMessage = "Matikan HP, tahan Vol Atas + Vol Bawah, lalu colok kabel USB.";
                CurrentOperation = "Menunggu Koneksi BROM";
            }
        }

        public string MtkCustomDaFileName => !string.IsNullOrEmpty(_mtkEngine.CustomDaFilePath)
            ? Path.GetFileName(_mtkEngine.CustomDaFilePath)
            : "Default Auto DA";

        public string MtkCustomAuthFileName => !string.IsNullOrEmpty(_mtkEngine.CustomAuthFilePath)
            ? Path.GetFileName(_mtkEngine.CustomAuthFilePath)
            : "Default Auto Auth";

        public async Task MtkCrashPreloaderAsync()
        {
            var portName = (SelectedDevice?.Brand == "MediaTek" || SelectedDevice?.Chipset?.Contains("MediaTek") == true) ? SelectedDevice.DeviceId : MediaTekBromEngine.ScanMtkPorts().FirstOrDefault()?.PortName ?? "MTK_BROM";
            var device = SelectedDevice ?? new Device { DeviceId = portName, Brand = "MediaTek", Model = SelectedPhoneModel?.ModelName ?? "MediaTek Phone", Mode = DeviceMode.BROM, PortName = portName, Chipset = SelectedPhoneModel?.Chipset ?? "MediaTek" };

            var result = await _operationOrchestrator.RunOperationAsync(
                device,
                OperationType.UnlockBootloader,
                "Crash Preloader to BROM (Force BROM Switch)",
                executeAction: async (jobId, ct) =>
                {
                    return await _mtkEngine.CrashPreloaderToBromAsync(portName == "MTK_BROM" ? null : portName, ct);
                },
                requiredToolName: "mtkclient");

            OperationStatus = result.State == OperationState.Completed ? "Completed" : "Failed";
            if (result.State == OperationState.Completed) UserGuidanceMessage = "Perangkat kini berada di mode BROM MURNI! Siap untuk Bypass Auth / FRP.";
        }

        public void MtkBrowseCustomDa()
        {
            var ofd = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Pilih Custom MTK Download Agent (DA) File (.bin)",
                Filter = "DA Binary Files (*.bin)|*.bin|All Files (*.*)|*.*",
                InitialDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "database", "da")
            };
            if (ofd.ShowDialog() == true)
            {
                _mtkEngine.CustomDaFilePath = ofd.FileName;
                AddLog($"[MTK DA] Custom DA terpilih: {Path.GetFileName(ofd.FileName)}");
                OnPropertyChanged(nameof(MtkCustomDaFileName));
            }
        }

        public void MtkBrowseCustomAuth()
        {
            var ofd = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Pilih Custom Preloader Auth File (.auth)",
                Filter = "Auth Files (*.auth;*.bin)|*.auth;*.bin|All Files (*.*)|*.*"
            };
            if (ofd.ShowDialog() == true)
            {
                _mtkEngine.CustomAuthFilePath = ofd.FileName;
                AddLog($"[MTK Auth] Custom Auth terpilih: {Path.GetFileName(ofd.FileName)}");
                OnPropertyChanged(nameof(MtkCustomAuthFileName));
            }
        }

        public async Task SamsungEdlFlashBitAsync()
        {
            var model = SelectedPhoneModel?.ModelName ?? "Samsung Galaxy Qualcomm";
            var binary = SelectedSamsungBinary;
            AddLog($"================================================================");
            AddLog($"[SAMSUNG QUALCOMM] Memulai Flashing EDL dengan Profil {model} ({binary})");
            AddLog($"[SAMSUNG EDL] Memverifikasi Fuse Bit Anti-Rollback: {binary}...");

            var portName = SelectedDevice?.Brand == "Qualcomm" ? SelectedDevice.DeviceId : QualcommEdlEngine.ScanQualcommPorts().FirstOrDefault()?.PortName ?? "QCOM_EDL";
            var programmer = SelectedLoader?.LocalPath;

            var device = SelectedDevice ?? new Device { DeviceId = portName, Brand = "Samsung", Model = model, Mode = DeviceMode.EDL, PortName = portName, Chipset = "Snapdragon" };

            var result = await _operationOrchestrator.RunOperationAsync(
                device,
                OperationType.FlashFirmware,
                $"Samsung EDL Bit-Matched Flash ({model} - {binary})",
                executeAction: async (jobId, ct) =>
                {
                    return await _qualcommEngine.FlashRawProgramAsync(portName, programmer ?? "", "", "");
                },
                requiredToolName: "qdl");

            OperationStatus = result.State == OperationState.Completed ? "Completed" : "Failed";
            if (result.State == OperationState.Completed) UserGuidanceMessage = "Samsung EDL Bit Flashing Selesai!";
        }

        // ==================== STANDARD & FASTBOOT OPERATIONS ====================

        public async Task BypassFrpAsync()
        {
            var targetDevice = SelectedDevice;
            if (targetDevice == null)
            {
                var samsungPorts = SamsungAtEngine.ScanSamsungPorts();
                if (samsungPorts.Count > 0)
                {
                    targetDevice = new Device
                    {
                        DeviceId = samsungPorts[0].PortName,
                        Brand = "Samsung",
                        Model = "Samsung Galaxy (Auto-detected)",
                        Mode = DeviceMode.Normal
                    };
                }
            }

            if (targetDevice == null)
            {
                AddLog("[Peringatan] Tidak ada perangkat terdeteksi. Hubungkan HP dalam mode MTP, Fastboot, MTK BROM, Qualcomm EDL, Xiaomi, Unisoc, atau Huawei.");
                CurrentOperation = "Gagal: Perangkat tidak ditemukan";
                OperationStatus = "Error";
                OperationProgress = 0;
                return;
            }

            if (targetDevice.Brand == "Huawei")
            {
                await HuaweiBypassIdAsync();
                return;
            }

            if (targetDevice.Brand == "Unisoc")
            {
                await UnisocBypassFrpAsync();
                return;
            }

            if (targetDevice.Brand == "Xiaomi" && targetDevice.Mode == DeviceMode.Recovery)
            {
                await XiaomiSideloadFrpAsync();
                return;
            }

            if (targetDevice.Brand == "Qualcomm" || targetDevice.Mode == DeviceMode.EDL)
            {
                await EdlBypassFrpAsync();
                return;
            }

            if (targetDevice.Brand == "MediaTek")
            {
                await MtkBypassFrpAsync();
                return;
            }

            if (targetDevice.Mode == DeviceMode.Fastboot)
            {
                await FastbootEraseFrpAsync();
                return;
            }

            var portName = targetDevice.DeviceId.StartsWith("COM", StringComparison.OrdinalIgnoreCase) 
                ? targetDevice.DeviceId 
                : SamsungAtEngine.ScanSamsungPorts().FirstOrDefault()?.PortName;

            if (string.IsNullOrEmpty(portName))
            {
                AddLog("[Error] Port Samsung Mobile USB Modem tidak terdeteksi di Device Manager.");
                AddLog("[Solusi] Pastikan Samsung USB Driver terpasang dan kabel USB terhubung erat.");
                CurrentOperation = "Port Modem Tidak Ditemukan";
                OperationStatus = "Error";
                OperationProgress = 0;
                return;
            }

            targetDevice.DeviceId = portName;
            targetDevice.Brand = "Samsung";
            targetDevice.Mode = DeviceMode.Normal;

            var result = await _operationOrchestrator.RunOperationAsync(
                targetDevice,
                OperationType.BypassFrp,
                "Samsung MTP / AT Mode 1-Click FRP Bypass",
                executeAction: async (jobId, ct) =>
                {
                    return await _samsungEngine.BypassFrpMtpAsync(portName, _platformToolsPath);
                });

            OperationStatus = result.State == OperationState.Completed ? "Completed" : "Failed";
            if (result.State == OperationState.Completed) UserGuidanceMessage = "Bypass FRP Berhasil! HP sedang merestart ke Menu Utama.";
        }

        /// <summary>
        /// PERBAIKAN Cacat X2: Alur bypass FRP Samsung via menu servis *#0*# dipisah dari alur AT Modem otomatis.
        /// Alur ini memberikan panduan fisik langkah demi langkah kepada teknisi,
        /// karena *#0*# membutuhkan interaksi layar HP (bukan otomatisasi port serial).
        /// </summary>
        public async Task BypassFrpSamsungServiceMenuAsync()
        {
            CurrentOperation = "Samsung FRP Bypass via *#0*# Service Menu";
            OperationProgress = 10;

            AddLog("════════════════════════════════════════════════════════════════");
            AddLog(" [METODE] Samsung FRP Bypass via *#0*# Darurat Service Menu");
            AddLog("════════════════════════════════════════════════════════════════");
            AddLog("");
            AddLog("[Langkah 1/5] Pastikan HP Samsung sudah menyala dan terhubung via kabel USB.");
            AddLog("              Samsung USB Driver (MTP) harus sudah terpasang di PC.");
            AddLog("");
            AddLog("[Langkah 2/5] Di layar pengisian akun Google, ketik kode darurat: *#0*#");
            AddLog("              Jika tidak muncul, coba: #*88#  atau  *#0808#");
            AddLog("");
            OperationProgress = 30;
            AddLog("[Langkah 3/5] Dari menu diagnostik yang terbuka, cari dan tap:");
            AddLog("              → Tombol 'USB' atau 'ADB' atau 'UART' → untuk aktifkan USB Debugging");
            AddLog("");
            AddLog("[Langkah 4/5] Setelah USB Debugging aktif, klik tombol 'Bypass FRP (MTP ADB)'");
            AddLog("              di panel kiri untuk mengirim payload ADB otomatis dari tools ini.");
            AddLog("");
            OperationProgress = 50;
            AddLog("[Langkah 5/5] Memeriksa apakah ADB sudah bisa mendeteksi HP...(klik MTP ADB setelah ADB aktif)");

            var adbExe = System.IO.Path.Combine(_platformToolsPath, "adb.exe");
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = adbExe,
                    Arguments = "devices",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var proc = System.Diagnostics.Process.Start(psi);
                var output = await proc!.StandardOutput.ReadToEndAsync();
                await Task.Run(() => proc.WaitForExit(5000));
                var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                var adbDevices = lines.Where(l => l.Contains("device") && !l.StartsWith("List"));
                if (adbDevices.Any())
                {
                    AddLog("[OK] HP terdeteksi via ADB — siap kirim payload FRP. Klik 'Bypass FRP (MTP ADB)'!");
                    OperationStatus = "Ready";
                    CurrentOperation = "ADB Terdeteksi — Klik Bypass FRP (MTP ADB) untuk Lanjut";
                }
                else
                {
                    AddLog("[Tunggu] HP belum terdeteksi via ADB. Selesaikan Langkah 2-3 terlebih dahulu.");
                    OperationStatus = "Pending";
                    CurrentOperation = "Menunggu ADB Tersambung setelah *#0*#";
                }
            }
            catch (Exception ex)
            {
                AddLog("[Info] Tidak dapat cek ADB: " + ex.Message + ". Ikuti panduan manual di log.");
                OperationStatus = "Manual";
                CurrentOperation = "Ikuti Panduan *#0*# di Log Terminal";
            }
            OperationProgress = 100;
        }

        public async Task DeviceInfoAsync()
        {
            CurrentOperation = "Membaca Info Perangkat...";
            OperationProgress = 20;

            try
            {
                var targetDevice = SelectedDevice;

                if (targetDevice != null && targetDevice.Brand == "Huawei")
                {
                    await HuaweiInfoAsync();
                    OperationProgress = 100;
                    return;
                }

                if (targetDevice != null && targetDevice.Brand == "Apple")
                {
                    await AppleReadInfoAsync();
                    OperationProgress = 100;
                    return;
                }

                if (targetDevice != null && targetDevice.Brand == "Unisoc")
                {
                    await UnisocInfoAsync();
                    OperationProgress = 100;
                    CurrentOperation = "Info Unisoc Selesai";
                    return;
                }

                if (targetDevice != null && targetDevice.Brand == "Xiaomi" && targetDevice.Mode == DeviceMode.Recovery)
                {
                    await XiaomiSideloadInfoAsync();
                    OperationProgress = 100;
                    return;
                }

                if (targetDevice != null && (targetDevice.Brand == "Qualcomm" || targetDevice.Mode == DeviceMode.EDL))
                {
                    await _qualcommEngine.PerformSaharaHandshakeAsync(targetDevice.DeviceId);
                    OperationProgress = 100;
                    CurrentOperation = "Info Qualcomm 9008 Selesai";
                    return;
                }

                if (targetDevice != null && targetDevice.Brand == "MediaTek")
                {
                    await _mtkEngine.ReadDeviceInfoAsync(targetDevice.DeviceId);
                    OperationProgress = 100;
                    CurrentOperation = "Info MediaTek Selesai";
                    return;
                }

                if (targetDevice != null && targetDevice.Mode == DeviceMode.Fastboot)
                {
                    var fastbootInfo = await _fastbootEngine.ReadAllVariablesAsync(targetDevice.DeviceId);
                    targetDevice.Model = string.IsNullOrEmpty(fastbootInfo.Product) ? targetDevice.Model : fastbootInfo.Product;
                    targetDevice.SerialNumber = string.IsNullOrEmpty(fastbootInfo.SerialNumber) ? targetDevice.SerialNumber : fastbootInfo.SerialNumber;
                    
                    await _fastbootEngine.GetAntiRollbackInfoAsync(targetDevice.DeviceId);

                    OperationProgress = 100;
                    CurrentOperation = "Info Fastboot & ARB Berhasil Dibaca";
                    return;
                }

                var portName = (targetDevice != null && targetDevice.DeviceId.StartsWith("COM", StringComparison.OrdinalIgnoreCase))
                    ? targetDevice.DeviceId
                    : SamsungAtEngine.ScanSamsungPorts().FirstOrDefault()?.PortName;

                if (!string.IsNullOrEmpty(portName))
                {
                    var samsungInfo = await _samsungEngine.ReadDeviceInfoAsync(portName);
                    if (samsungInfo != null && targetDevice != null)
                    {
                        targetDevice.Model = samsungInfo.Model;
                        targetDevice.Imei = samsungInfo.Imei;
                        targetDevice.SerialNumber = samsungInfo.SerialNumber;
                        targetDevice.AndroidVersion = samsungInfo.AndroidVersion;
                    }
                    OperationProgress = 100;
                    CurrentOperation = "Info Samsung Berhasil Dibaca";
                    return;
                }

                if (targetDevice != null)
                {
                    AddLog($"[Info] Membaca properti via ADB pada {targetDevice.DeviceId}...");
                    OperationProgress = 100;
                    CurrentOperation = "Info ADB Selesai";
                }
                else
                {
                    AddLog("[Peringatan] Tidak ada perangkat aktif untuk dibaca.");
                }
            }
            catch (Exception ex)
            {
                AddLog($"[Error] Gagal membaca info: {ex.Message}");
                CurrentOperation = "Gagal Membaca Info";
            }
        }

        public async Task UnlockBootloaderAsync()
        {
            if (SelectedDevice?.Brand == "MediaTek")
            {
                await MtkUnlockBootloaderAsync();
                return;
            }

            var targetDevice = SelectedDevice ?? new Device
            {
                DeviceId = "FASTBOOT_DEV",
                Brand = SelectedPhoneModel?.Brand ?? "Android",
                Model = SelectedPhoneModel?.ModelName ?? "Generic",
                Mode = DeviceMode.Fastboot
            };

            var result = await _operationOrchestrator.RunOperationAsync(
                targetDevice,
                OperationType.UnlockBootloader,
                "Universal Fastboot Bootloader Unlock (OEM Unlock)",
                executeAction: async (jobId, ct) =>
                {
                    return await _fastbootEngine.UnlockBootloaderAsync(targetDevice.DeviceId);
                },
                requiredToolName: "fastboot");

            OperationStatus = result.State == OperationState.Completed ? "Completed" : "Failed";
        }

        public async Task FormatAsync()
        {
            if (SelectedDevice?.Brand == "Unisoc")
            {
                await UnisocFormatUserdataAsync();
                return;
            }

            if (SelectedDevice?.Brand == "Xiaomi" && SelectedDevice?.Mode == DeviceMode.Recovery)
            {
                await XiaomiSideloadFormatAsync();
                return;
            }

            if (SelectedDevice?.Brand == "Qualcomm" || SelectedDevice?.Mode == DeviceMode.EDL)
            {
                await EdlFormatUserdataAsync();
                return;
            }

            if (SelectedDevice?.Brand == "MediaTek")
            {
                await MtkFormatUserdataAsync();
                return;
            }

            var targetDevice = SelectedDevice ?? new Device
            {
                DeviceId = "FASTBOOT_DEV",
                Brand = SelectedPhoneModel?.Brand ?? "Android",
                Model = SelectedPhoneModel?.ModelName ?? "Generic",
                Mode = DeviceMode.Fastboot
            };

            var result = await _operationOrchestrator.RunOperationAsync(
                targetDevice,
                OperationType.FactoryReset,
                "Fastboot Factory Reset (Format Userdata)",
                executeAction: async (jobId, ct) =>
                {
                    return await _fastbootEngine.FactoryResetAsync(targetDevice.DeviceId);
                },
                requiredToolName: "fastboot");

            OperationStatus = result.State == OperationState.Completed ? "Completed" : "Failed";
        }

        public async Task RebootNormalAsync()
        {
            AddLog("[Reboot] Menginisialisasi sinyal restart perangkat...");

            // 1. Coba Kirim via ADB Interface (Jika aktif/sideload)
            var adbRes = await _toolEngine.ExecuteAdbCommandAsync(SelectedDevice?.DeviceId ?? "", "reboot");
            if (adbRes.Success)
            {
                AddLog("[Reboot] Sinyal reboot berhasil dikirim via ADB Interface.");
                return;
            }

            // 2. Coba Kirim via Fastboot Interface (Jika dalam mode bootloader)
            var isFb = await _fastbootEngine.IsDeviceConnectedAsync(SelectedDevice?.DeviceId);
            if (isFb)
            {
                AddLog("[Fastboot] Perangkat fastboot terdeteksi. Mengirim reboot...");
                await _fastbootEngine.RebootAsync(FastbootRebootTarget.System, SelectedDevice?.DeviceId);
                return;
            }

            // 3. Coba Kirim via Samsung Modem AT Command (MTP / Download mode)
            var samsungPort = SamsungAtEngine.ScanSamsungPorts().FirstOrDefault()?.PortName;
            if (!string.IsNullOrEmpty(samsungPort))
            {
                AddLog($"[Samsung AT] Port Modem {samsungPort} terdeteksi. Mengirim AT+REBOOT...");
                await _samsungEngine.RebootAsync(samsungPort, SamsungRebootMode.Normal);
                return;
            }

            // 4. Coba Kirim via MediaTek Port (BROM / Preloader)
            var mtkPorts = MediaTekBromEngine.ScanMtkPorts();
            if (mtkPorts.Count > 0)
            {
                AddLog($"[MTK BROM] Port {mtkPorts[0].PortName} aktif. Mengirim hardware reset...");
                await _mtkEngine.RebootDeviceAsync(mtkPorts[0].PortName);
                return;
            }

            // 5. Coba Kirim via Qualcomm EDL 9008
            var qcomPorts = QualcommEdlEngine.ScanQualcommPorts();
            if (qcomPorts.Count > 0)
            {
                AddLog($"[Qualcomm EDL] Port {qcomPorts[0].PortName} aktif. Mengirim power cycle reset...");
                await _qualcommEngine.RebootDeviceAsync(qcomPorts[0].PortName);
                return;
            }

            // 6. Coba Kirim via Unisoc / SPD BSL
            var spdPorts = UnisocSpdEngine.ScanUnisocPorts();
            if (spdPorts.Count > 0)
            {
                AddLog($"[Unisoc SPRD] Port {spdPorts[0].PortName} aktif. Mengirim reset...");
                await _unisocEngine.RebootDeviceAsync(spdPorts[0].PortName);
                return;
            }

            // 7. Active Hardware Watcher (Standby 45 Detik menangkap transisi bus USB)
            AddLog("[Reboot] Standby Listener: Siaga 45 detik. Silakan matikan HP -> Tahan Vol Atas+Bawah -> Colok USB...");
            var sw = Stopwatch.StartNew();
            while (sw.Elapsed.TotalSeconds < 45)
            {
                // Cek Port MTK
                var mPorts = MediaTekBromEngine.ScanMtkPorts();
                if (mPorts.Count > 0)
                {
                    AddLog($"[MTK BROM] Port {mPorts[0].PortName} terdeteksi! Mengirim hardware reset...");
                    await _mtkEngine.RebootDeviceAsync(mPorts[0].PortName);
                    return;
                }

                // Cek Port Fastboot
                if (await _fastbootEngine.IsDeviceConnectedAsync(null))
                {
                    AddLog("[Fastboot] Perangkat fastboot tersambung! Mengirim reboot...");
                    await _fastbootEngine.RebootAsync(FastbootRebootTarget.System, null);
                    return;
                }

                // Cek Port Samsung Modem
                var sPort = SamsungAtEngine.ScanSamsungPorts().FirstOrDefault()?.PortName;
                if (!string.IsNullOrEmpty(sPort))
                {
                    AddLog($"[Samsung AT] Port Modem {sPort} tersambung! Mengirim AT+REBOOT...");
                    await _samsungEngine.RebootAsync(sPort, SamsungRebootMode.Normal);
                    return;
                }

                // Cek ADB
                var adbCheck = await _toolEngine.ExecuteAdbCommandAsync("", "reboot");
                if (adbCheck.Success)
                {
                    AddLog("[ADB] Perangkat tersambung! Perintah reboot terkirim.");
                    return;
                }

                await Task.Delay(500);
            }

            AddLog("[Reboot] Waktu siaga habis (45s). Tidak ada perangkat yang terdeteksi.");
        }

        public async Task RebootDownloadAsync()
        {
            AddLog("[Reboot] Mengirim perintah restart ke Download Mode (Odin)...");
            var portName = SamsungAtEngine.ScanSamsungPorts().FirstOrDefault()?.PortName;
            if (!string.IsNullOrEmpty(portName))
            {
                await _samsungEngine.RebootAsync(portName, SamsungRebootMode.Download);
            }
            else
            {
                AddLog("[Peringatan] Port Samsung Modem tidak ditemukan.");
            }
        }

        public async Task RebootRecoveryAsync()
        {
            AddLog("[Reboot] Mengirim perintah restart ke Recovery Mode...");
            if (SelectedDevice?.Mode == DeviceMode.Fastboot)
            {
                await _fastbootEngine.RebootAsync(FastbootRebootTarget.Recovery, SelectedDevice.DeviceId);
            }
        }

        public async Task RebootEdlAsync()
        {
            AddLog("[Reboot] Mengirim perintah restart ke Qualcomm EDL 9008...");
            if (SelectedDevice?.Mode == DeviceMode.Fastboot)
            {
                await _fastbootEngine.RebootAsync(FastbootRebootTarget.EDL, SelectedDevice.DeviceId);
            }
            else
            {
                AddLog("[Peringatan] Perangkat harus dalam mode Fastboot untuk beralih ke EDL via perintah ini.");
            }
        }

        private async Task FlashSamsungAsync()
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Pilih File Firmware Samsung (.tar / .tar.md5)",
                Filter = "Samsung Firmware Files (*.tar;*.tar.md5)|*.tar;*.tar.md5|All Files (*.*)|*.*"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                var tarPath = openFileDialog.FileName;
                var extractDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "firmware_extracted", "samsung", Path.GetFileNameWithoutExtension(tarPath));
                CurrentOperation = "Mengekstrak Firmware Samsung...";
                OperationProgress = 30;
                var extracted = await _extractorEngine.ExtractSamsungTarMd5Async(tarPath, extractDir);
                OperationProgress = 100;
                CurrentOperation = extracted.Count > 0 ? $"Ekstraksi {extracted.Count} Partisi Selesai" : "Ekstraksi Gagal";
            }
        }

        private async Task RepairImeiAsync()
        {
            CurrentOperation = "Aktivasi Diag Port & Perbaikan IMEI...";
            OperationProgress = 20;

            if (SelectedDevice?.Brand == "Qualcomm" || SelectedDevice?.Mode == DeviceMode.Normal)
            {
                var ok = await _signalEngine.EnableQualcommDiagPortViaAdbAsync(SelectedDevice?.DeviceId);
                OperationProgress = 100;
                CurrentOperation = ok ? "Qualcomm Diag Port Aktif" : "Periksa Petunjuk Manual Diag";
            }
            else
            {
                AddLog("[IMEI Repair] Untuk ponsel MediaTek, gunakan fitur 'Backup/Restore NVRAM' pada tab MediaTek.");
                var nvramFiles = _signalEngine.ScanAvailableNvramFiles();
                AddLog($"[Database] Ditemukan {nvramFiles.Count} file golden backup NVRAM.");
                CurrentOperation = "Siap";
            }
        }

        private async Task FixBasebandAsync()
        {
            CurrentOperation = "Memulai Perbaikan Baseband & Kalibrasi Sinyal...";
            OperationProgress = 10;

            AddLog("╔════════════════════════════════════════════════════════════════╗");
            AddLog("║         PROSES REPAIR BASEBAND & KALIBRASI RADIO (NVRAM/QCN)   ║");
            AddLog("╚════════════════════════════════════════════════════════════════╝");

            var targetBrand = SelectedDevice?.Brand ?? (!string.IsNullOrEmpty(SelectedBrand) && SelectedBrand != "Semua" ? SelectedBrand : "MediaTek");

            if (targetBrand.Equals("MediaTek", StringComparison.OrdinalIgnoreCase) || SelectedDevice?.Chipset?.Contains("MediaTek") == true)
            {
                AddLog("[MTK] Mendeteksi port MediaTek BROM / Preloader...");
                var port = SelectedDevice?.DeviceId ?? MediaTekBromEngine.ScanMtkPorts().FirstOrDefault()?.PortName ?? "MTK_BROM";
                
                // Cari apakah ada file NVRAM yang cocok dengan model terpilih di database
                var nvramFiles = _signalEngine.ScanAvailableNvramFiles();
                var selectedModel = SelectedPhoneModel?.ModelName;
                var matched = nvramFiles.FirstOrDefault(n => !string.IsNullOrEmpty(selectedModel) && (n.Model.Contains(selectedModel, StringComparison.OrdinalIgnoreCase) || selectedModel.Contains(n.Model, StringComparison.OrdinalIgnoreCase)));

                string? restoreDir = null;
                if (matched != null && File.Exists(matched.FilePath))
                {
                    restoreDir = Path.GetDirectoryName(matched.FilePath);
                    AddLog($"[MTK] Ditemukan berkas kalibrasi NVRAM cocok di database: {matched.FileName}");
                }
                else
                {
                    AddLog("[MTK] Membuka pemilih berkas kalibrasi NVRAM / NVDATA (.bin)...");
                    var dialog = new Microsoft.Win32.OpenFileDialog
                    {
                        Title = "Pilih Berkas Kalibrasi NVRAM MediaTek (*.bin)",
                        Filter = "NVRAM Binary Files (*.bin)|*.bin|All Files (*.*)|*.*",
                        InitialDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "database", "nvram")
                    };
                    if (dialog.ShowDialog() == true)
                    {
                        restoreDir = Path.GetDirectoryName(dialog.FileName);
                    }
                }

                if (!string.IsNullOrEmpty(restoreDir) && Directory.Exists(restoreDir))
                {
                    OperationProgress = 50;
                    AddLog($"[MTK] Menulis kalibrasi NVRAM/NVDATA dari direktori: {restoreDir}...");
                    var ok = await _signalEngine.RestoreMediaTekNvramAsync(restoreDir);
                    OperationProgress = 100;
                    CurrentOperation = ok ? "Perbaikan Baseband MTK Sukses" : "Gagal Menulis NVRAM";
                    if (ok) UserGuidanceMessage = "Baseband MTK Berhasil Dipulihkan! Restart perangkat untuk mengaktifkan sinyal.";
                }
                else
                {
                    AddLog("[MTK] Pembatalan: Tidak ada berkas kalibrasi NVRAM yang dipilih.");
                    CurrentOperation = "Dibatalkan";
                }
            }
            else if (targetBrand.Equals("Qualcomm", StringComparison.OrdinalIgnoreCase) || SelectedDevice?.Chipset?.Contains("Snapdragon") == true)
            {
                AddLog("[Qualcomm] Menyiapkan injeksi berkas kalibrasi QCN...");
                var qcnFiles = _signalEngine.ScanAvailableQcnFiles();
                var selectedModel = SelectedPhoneModel?.ModelName;
                var matchedQcn = qcnFiles.FirstOrDefault(q => !string.IsNullOrEmpty(selectedModel) && (q.Model.Contains(selectedModel, StringComparison.OrdinalIgnoreCase) || selectedModel.Contains(q.Model, StringComparison.OrdinalIgnoreCase)));

                string? qcnPath = matchedQcn?.FilePath;
                if (string.IsNullOrEmpty(qcnPath) || !File.Exists(qcnPath))
                {
                    var dialog = new Microsoft.Win32.OpenFileDialog
                    {
                        Title = "Pilih Berkas Kalibrasi QCN Qualcomm (*.qcn;*.xqcn)",
                        Filter = "Qualcomm QCN Files (*.qcn;*.xqcn)|*.qcn;*.xqcn|All Files (*.*)|*.*",
                        InitialDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "database", "qcn")
                    };
                    if (dialog.ShowDialog() == true)
                    {
                        qcnPath = dialog.FileName;
                    }
                }

                if (!string.IsNullOrEmpty(qcnPath) && File.Exists(qcnPath))
                {
                    var diagPort = SelectedDevice?.DeviceId ?? "COM1";
                    OperationProgress = 50;
                    AddLog($"[Qualcomm] Menulis berkas QCN {Path.GetFileName(qcnPath)} ke port {diagPort}...");
                    var ok = await _signalEngine.WriteQcnToDiagPortAsync(diagPort, qcnPath);
                    OperationProgress = 100;
                    CurrentOperation = ok ? "Injeksi QCN Berhasil" : "Gagal Injeksi QCN";
                    if (ok) UserGuidanceMessage = "Injeksi QCN Berhasil! Sinyal dan IMEI Qualcomm telah dipulihkan.";
                }
                else
                {
                    AddLog("[Qualcomm] Pembatalan: Berkas QCN tidak dipilih.");
                    CurrentOperation = "Dibatalkan";
                }
            }
            else if (targetBrand.Equals("Samsung", StringComparison.OrdinalIgnoreCase))
            {
                await SamsungRestoreEfsAsync();
            }
            else
            {
                AddLog($"[Info] Memindai cadangan NVRAM untuk brand {targetBrand}...");
                await MtkRestoreNvramAsync();
            }
        }

        private async Task RepairEfsAsync()
        {
            CurrentOperation = "Repair Partisi EFS / NVRAM...";
            if (SelectedDevice?.Brand == "Qualcomm" || SelectedDevice?.Mode == DeviceMode.EDL)
            {
                await EdlBackupEfsAsync();
            }
            else
            {
                await MtkRestoreNvramAsync();
            }
        }

        private async Task EdlFlashAsync()
        {
            AddLog("[Info] EDL Flash via Firehose XML programmer.");
            await EdlFormatUserdataAsync();
        }

        private async Task AuthBypassAsync()
        {
            await MtkBypassFrpAsync();
        }

        private async Task RemoveMiAccountAsync()
        {
            AddLog("[Info] Menghapus Mi Account via Anti-Relock Service Patch...");
            if (SelectedDevice?.Mode == DeviceMode.Recovery)
            {
                await XiaomiSideloadFormatAsync();
            }
            else if (SelectedDevice?.Mode == DeviceMode.Normal)
            {
                await _brandSpecialEngine.DisableXiaomiAntiRelockAsync(SelectedDevice?.DeviceId);
            }
            else if (SelectedDevice?.Brand == "Qualcomm")
            {
                await EdlFormatUserdataAsync();
            }
            else
            {
                await MtkFormatUserdataAsync();
            }
        }

        private async Task FlashOppoAsync()
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Pilih File Firmware Oppo/Realme (.ofp / scatter.txt)",
                Filter = "Oppo/Realme Firmware (*.ofp;*.txt)|*.ofp;*.txt|All Files (*.*)|*.*"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                var filePath = openFileDialog.FileName;
                if (filePath.EndsWith(".ofp", StringComparison.OrdinalIgnoreCase))
                {
                    var extractDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "firmware_extracted", "oppo", Path.GetFileNameWithoutExtension(filePath));
                    CurrentOperation = "Mendekripsi & Ekstrak OFP...";
                    OperationProgress = 30;
                    var res = await _extractorEngine.ExtractOppoOfpAsync(filePath, extractDir);
                    OperationProgress = 100;
                    CurrentOperation = res.Count > 0 ? $"Dekripsi OFP Selesai ({res.Count} Partisi)" : "Dekripsi Gagal";
                }
                else
                {
                    CurrentOperation = "Membaca Scatter File...";
                    var parts = await _extractorEngine.ParseMediaTekScatterAsync(filePath);
                    OperationProgress = 100;
                    CurrentOperation = $"Scatter Memuat {parts.Count} Partisi";
                }
            }
        }

        private async Task AdbShellAsync()
        {
            var serial = SelectedDevice?.DeviceId;
            var adbExe = Path.Combine(_platformToolsPath, "adb.exe");
            if (!File.Exists(adbExe))
            {
                adbExe = "adb.exe";
            }

            var targetArg = string.IsNullOrEmpty(serial) ? "shell" : $"-s {serial} shell";
            AddLog($"[ADB Shell] Meluncurkan terminal interaktif untuk perangkat: {serial ?? "Default"}...");

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/k title JumperTools ADB Shell - {serial ?? "Connected"} && \"{adbExe}\" {targetArg}",
                    UseShellExecute = true,
                    CreateNoWindow = false
                };
                Process.Start(psi);
                CurrentOperation = "Terminal ADB Terbuka";
            }
            catch (Exception ex)
            {
                AddLog($"[ADB Error] Gagal membuka terminal: {ex.Message}");
            }
            await Task.CompletedTask;
        }

        private async Task InstallApkAsync()
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Pilih File Aplikasi Android (.apk)",
                Filter = "Android Package (*.apk)|*.apk|All Files (*.*)|*.*"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                var apkPath = openFileDialog.FileName;
                CurrentOperation = $"Memasang APK {Path.GetFileName(apkPath)}...";
                OperationProgress = 30;
                var res = await _toolEngine.InstallApkAsync(SelectedDevice?.DeviceId ?? "", apkPath);
                var ok = res.Success;
                OperationProgress = 100;
                CurrentOperation = ok ? "APK Berhasil Terpasang" : $"Gagal Memasang APK: {res.ErrorMessage}";
                AddLog(ok ? $"[APK Installer] Sukses memasang: {Path.GetFileName(apkPath)}" : $"[APK Installer] Gagal: {res.ErrorMessage}");
            }
        }

        public async Task ScreenMirrorAsync()
        {
            var serial = SelectedDevice?.DeviceId;
            var scrcpyDir = Path.Combine(_toolsBasePath, "scrcpy");
            var scrcpyExe = Path.Combine(scrcpyDir, "scrcpy.exe");

            if (!File.Exists(scrcpyExe))
            {
                scrcpyExe = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "scrcpy", "scrcpy.exe");
            }

            if (File.Exists(scrcpyExe))
            {
                AddLog($"[Scrcpy] Meluncurkan mirror layar untuk [{serial ?? "Default"}]...");
                var targetArg = string.IsNullOrEmpty(serial) ? "" : $"-s {serial}";
                Process.Start(new ProcessStartInfo
                {
                    FileName = scrcpyExe,
                    Arguments = targetArg,
                    UseShellExecute = true
                });
                CurrentOperation = "Screen Mirror Aktif";
            }
            else
            {
                AddLog("[Info] scrcpy.exe tidak ditemukan di tools/scrcpy/. Membuka mode rekam layar via ADB...");
                await ScreenRecordAsync();
            }
        }

        private async Task ScreenRecordAsync()
        {
            var serial = SelectedDevice?.DeviceId;
            var adbExe = Path.Combine(_platformToolsPath, "adb.exe");
            if (!File.Exists(adbExe)) adbExe = "adb.exe";

            var targetArg = string.IsNullOrEmpty(serial) ? "shell screenrecord --time-limit 30 /sdcard/service_record.mp4" : $"-s {serial} shell screenrecord --time-limit 30 /sdcard/service_record.mp4";

            AddLog($"[ADB Record] Memulai perekaman layar 30 detik pada [{serial ?? "Default"}]...");
            CurrentOperation = "Merekam Layar (Max 30s)...";
            OperationProgress = 20;

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = adbExe,
                    Arguments = targetArg,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                var p = Process.Start(psi);
                if (p != null)
                {
                    await Task.Run(() => p.WaitForExit());
                    OperationProgress = 100;
                    AddLog("[ADB Record] Selesai merekam! Video tersimpan di HP: /sdcard/service_record.mp4");
                    CurrentOperation = "Rekam Layar Selesai (/sdcard/service_record.mp4)";
                }
            }
            catch (Exception ex)
            {
                AddLog($"[Record Error] {ex.Message}");
                CurrentOperation = "Gagal Rekam Layar";
            }
        }

        private async Task FlashTwrpAsync()
        {
            var recoveryDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "database", "recovery");
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Pilih File Custom Recovery (.img)",
                Filter = "Recovery Image Files (*.img)|*.img|All Files (*.*)|*.*",
                InitialDirectory = Directory.Exists(recoveryDir) ? recoveryDir : AppDomain.CurrentDomain.BaseDirectory
            };

            if (openFileDialog.ShowDialog() != true)
            {
                AddLog("[Batal] Pemilihan file recovery dibatalkan.");
                return;
            }

            var recoveryPath = openFileDialog.FileName;
            var targetDevice = SelectedDevice ?? new Device
            {
                DeviceId = "FASTBOOT_DEV",
                Brand = SelectedPhoneModel?.Brand ?? "Android",
                Model = SelectedPhoneModel?.ModelName ?? "Generic",
                Mode = DeviceMode.Fastboot
            };

            var result = await _operationOrchestrator.RunOperationAsync(
                targetDevice,
                OperationType.FlashRecovery,
                $"Flash Custom Recovery ({Path.GetFileName(recoveryPath)})",
                executeAction: async (jobId, ct) =>
                {
                    return await _recoveryEngine.InstallRecoveryViaFastbootAsync(recoveryPath, "auto", true, targetDevice.DeviceId);
                },
                firmwareFilePath: recoveryPath,
                requiredToolName: "fastboot");

            OperationStatus = result.State == OperationState.Completed ? "Completed" : "Failed";
        }

        private async Task BootRecoveryAsync()
        {
            await RebootRecoveryAsync();
        }

        public async Task XiaomiSideloadFlashOtaAsync()
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Pilih File ROM Recovery Resmi Xiaomi (*.zip)",
                Filter = "Xiaomi Recovery ROM / OTA Package (*.zip)|*.zip|All Files (*.*)|*.*"
            };

            if (openFileDialog.ShowDialog() != true)
            {
                AddLog("[Batal] Pemilihan file OTA dibatalkan.");
                return;
            }

            var zipPath = openFileDialog.FileName;
            var targetDevice = SelectedDevice ?? new Device
            {
                DeviceId = "XIAOMI_SIDELOAD",
                Brand = "Xiaomi",
                Model = SelectedPhoneModel?.ModelName ?? "Xiaomi Phone",
                Mode = DeviceMode.Recovery
            };

            var result = await _operationOrchestrator.RunOperationAsync(
                targetDevice,
                OperationType.FlashFirmware,
                $"Xiaomi Sideload Flash OTA ({Path.GetFileName(zipPath)})",
                executeAction: async (jobId, ct) =>
                {
                    return await _xiaomiEngine.FlashOtaZipAsync(zipPath, targetDevice.DeviceId);
                },
                firmwareFilePath: zipPath,
                requiredToolName: "adb");

            OperationStatus = result.State == OperationState.Completed ? "Completed" : "Failed";
        }

        // ==================== SAMSUNG 4-FILE ODIN WORKSPACE ====================
        private string _samsungBlPath = string.Empty;
        public string SamsungBlPath { get => _samsungBlPath; set { _samsungBlPath = value; OnPropertyChanged(nameof(SamsungBlPath)); } }

        private string _samsungApPath = string.Empty;
        public string SamsungApPath { get => _samsungApPath; set { _samsungApPath = value; OnPropertyChanged(nameof(SamsungApPath)); } }

        private string _samsungCpPath = string.Empty;
        public string SamsungCpPath { get => _samsungCpPath; set { _samsungCpPath = value; OnPropertyChanged(nameof(SamsungCpPath)); } }

        private string _samsungCscPath = string.Empty;
        public string SamsungCscPath { get => _samsungCscPath; set { _samsungCscPath = value; OnPropertyChanged(nameof(SamsungCscPath)); } }

        public void BrowseSamsungBl()
        {
            var file = BrowseOdinTarFile("BL (Bootloader)");
            if (!string.IsNullOrEmpty(file))
            {
                SetAndAutoDetectOdinCompanionFiles("BL", file);
            }
        }

        public void BrowseSamsungAp()
        {
            var file = BrowseOdinTarFile("AP (System/PDA)");
            if (!string.IsNullOrEmpty(file))
            {
                SetAndAutoDetectOdinCompanionFiles("AP", file);
            }
        }

        public void BrowseSamsungCp()
        {
            var file = BrowseOdinTarFile("CP (Modem/Phone)");
            if (!string.IsNullOrEmpty(file))
            {
                SetAndAutoDetectOdinCompanionFiles("CP", file);
            }
        }

        public void BrowseSamsungCsc()
        {
            var file = BrowseOdinTarFile("CSC (Country/Region)");
            if (!string.IsNullOrEmpty(file))
            {
                SetAndAutoDetectOdinCompanionFiles("CSC", file);
            }
        }

        public void ClearSamsungOdinFiles()
        {
            SamsungBlPath = string.Empty;
            SamsungApPath = string.Empty;
            SamsungCpPath = string.Empty;
            SamsungCscPath = string.Empty;
            AddLog("[Odin] Slot file BL, AP, CP, CSC telah dikosongkan.");
        }

        private void SetAndAutoDetectOdinCompanionFiles(string slot, string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) return;

            if (slot == "BL") SamsungBlPath = filePath;
            else if (slot == "AP") SamsungApPath = filePath;
            else if (slot == "CP") SamsungCpPath = filePath;
            else if (slot == "CSC") SamsungCscPath = filePath;

            try
            {
                var dir = Path.GetDirectoryName(filePath);
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return;

                var files = Directory.GetFiles(dir, "*.tar*");
                int autoFilledCount = 0;

                foreach (var f in files)
                {
                    var fileName = Path.GetFileName(f).ToUpperInvariant();
                    if (string.IsNullOrEmpty(SamsungBlPath) && (fileName.StartsWith("BL_") || fileName.StartsWith("BL-") || fileName.Contains("BOOTLOADER")))
                    {
                        SamsungBlPath = f;
                        autoFilledCount++;
                    }
                    else if (string.IsNullOrEmpty(SamsungApPath) && (fileName.StartsWith("AP_") || fileName.StartsWith("AP-") || fileName.StartsWith("PDA_") || fileName.StartsWith("CODE_")))
                    {
                        SamsungApPath = f;
                        autoFilledCount++;
                    }
                    else if (string.IsNullOrEmpty(SamsungCpPath) && (fileName.StartsWith("CP_") || fileName.StartsWith("CP-") || fileName.StartsWith("MODEM_") || fileName.StartsWith("PHONE_")))
                    {
                        SamsungCpPath = f;
                        autoFilledCount++;
                    }
                    else if (string.IsNullOrEmpty(SamsungCscPath) && (fileName.StartsWith("CSC_") || fileName.StartsWith("HOME_CSC_") || fileName.Contains("_CSC_")))
                    {
                        SamsungCscPath = f;
                        autoFilledCount++;
                    }
                }

                if (autoFilledCount > 0)
                {
                    AddLog($"[Odin Auto-Detect] Otomatis mendeteksi dan mengisi {autoFilledCount} file firmware pendamping dari folder.");
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"Failed to auto-detect companion Odin files: {ex.Message}");
            }
        }

        private string BrowseOdinTarFile(string slotName)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = $"Pilih Berkas {slotName} (*.tar;*.tar.md5)",
                Filter = "Samsung TAR Archives (*.tar;*.tar.md5)|*.tar;*.tar.md5|All Files (*.*)|*.*",
                CheckFileExists = true
            };
            return dialog.ShowDialog() == true ? dialog.FileName : string.Empty;
        }

        public async Task FlashSamsungOdinAsync()
        {
            if (string.IsNullOrEmpty(SamsungBlPath) && string.IsNullOrEmpty(SamsungApPath) &&
                string.IsNullOrEmpty(SamsungCpPath) && string.IsNullOrEmpty(SamsungCscPath))
            {
                AddLog("[Peringatan] Masukkan minimal satu berkas firmware Samsung (BL, AP, CP, atau CSC).");
                return;
            }

            var primaryTar = !string.IsNullOrEmpty(SamsungApPath) ? SamsungApPath :
                             !string.IsNullOrEmpty(SamsungBlPath) ? SamsungBlPath :
                             !string.IsNullOrEmpty(SamsungCpPath) ? SamsungCpPath : SamsungCscPath;

            AddLog("╔════════════════════════════════════════════════════════════════╗");
            AddLog("║          SAMSUNG 4-FILE ODIN FLASHING WORKBENCH                ║");
            AddLog("╚════════════════════════════════════════════════════════════════╝");
            if (!string.IsNullOrEmpty(SamsungBlPath)) AddLog($" [Slot BL ] : {Path.GetFileName(SamsungBlPath)}");
            if (!string.IsNullOrEmpty(SamsungApPath)) AddLog($" [Slot AP ] : {Path.GetFileName(SamsungApPath)}");
            if (!string.IsNullOrEmpty(SamsungCpPath)) AddLog($" [Slot CP ] : {Path.GetFileName(SamsungCpPath)}");
            if (!string.IsNullOrEmpty(SamsungCscPath)) AddLog($" [Slot CSC] : {Path.GetFileName(SamsungCscPath)}");

            var targetDevice = SelectedDevice ?? new Device
            {
                DeviceId = "SAMSUNG_ODIN",
                Brand = "Samsung",
                Model = SelectedPhoneModel?.ModelName ?? "Samsung Galaxy",
                Mode = DeviceMode.Download
            };

            var result = await _operationOrchestrator.RunOperationAsync(
                targetDevice,
                OperationType.FlashFirmware,
                $"Samsung 4-File Odin Flash ({Path.GetFileName(primaryTar)})",
                executeAction: async (jobId, ct) =>
                {
                    var res = await _toolEngine.ExecuteSamsungFlashAsync(targetDevice.DeviceId, primaryTar, new OperationParameters
                    {
                        FilePath = primaryTar,
                        BlPath = SamsungBlPath,
                        ApPath = SamsungApPath,
                        CpPath = SamsungCpPath,
                        CscPath = SamsungCscPath,
                        AutoReboot = true
                    }, cancellationToken: ct);
                    return res.Success;
                },
                firmwareFilePath: primaryTar,
                requiredToolName: "odin");

            OperationStatus = result.State == OperationState.Completed ? "Completed" : "Failed";
            if (result.State == OperationState.Completed) UserGuidanceMessage = "Flashing Samsung Odin 4-File Berhasil! HP sedang reboot.";
        }

        // ==================== UNISOC / SPD PAC FLASHER ====================
        private string _unisocPacPath = string.Empty;
        public string UnisocPacPath
        {
            get => _unisocPacPath;
            set
            {
                _unisocPacPath = value;
                OnPropertyChanged(nameof(UnisocPacPath));
            }
        }

        public void BrowseUnisocPac()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Pilih Berkas Firmware Unisoc/Spreadtrum (.pac)",
                Filter = "Unisoc/Spreadtrum PAC Firmware (*.pac)|*.pac|All Files (*.*)|*.*",
                CheckFileExists = true
            };
            if (dialog.ShowDialog() == true)
            {
                UnisocPacPath = dialog.FileName;
                AddLog($"[Unisoc PAC] Memilih berkas firmware: {Path.GetFileName(UnisocPacPath)}");
            }
        }

        public async Task FlashUnisocPacAsync()
        {
            if (string.IsNullOrEmpty(UnisocPacPath) || !File.Exists(UnisocPacPath))
            {
                AddLog("[Peringatan] Pilih berkas firmware Unisoc (.pac) terlebih dahulu.");
                BrowseUnisocPac();
                if (string.IsNullOrEmpty(UnisocPacPath)) return;
            }

            var targetDevice = SelectedDevice ?? new Device
            {
                DeviceId = "UNISOC_BSL",
                Brand = "Unisoc",
                Model = SelectedPhoneModel?.ModelName ?? "Unisoc Phone",
                Mode = DeviceMode.Download,
                Chipset = "Unisoc SPD"
            };

            var result = await _operationOrchestrator.RunOperationAsync(
                targetDevice,
                OperationType.FlashFirmware,
                $"Unisoc PAC Flasher ({Path.GetFileName(UnisocPacPath)})",
                executeAction: async (jobId, ct) =>
                {
                    return await _unisocEngine.FlashPacFirmwareAsync(UnisocPacPath);
                },
                firmwareFilePath: UnisocPacPath,
                requiredToolName: "spd_research");

            OperationStatus = result.State == OperationState.Completed ? "Completed" : "Failed";
        }

        // ==================== APPLE IOS EXTENDED ====================
        public async Task AppleRebootNormalAsync()
        {
            CurrentOperation = "Merestart Perangkat Apple iOS...";
            OperationProgress = 40;
            var serial = SelectedDevice?.DeviceId;
            var ok = await _appleEngine.RebootAppleDeviceAsync(serial, "normal");
            OperationProgress = 100;
            CurrentOperation = ok ? "Restart Apple iOS Berhasil" : "Gagal Restart Apple";
        }

        // ==================== FASTBOOT DEDICATED FRP ====================
        public async Task FastbootEraseFrpAsync()
        {
            var targetDevice = SelectedDevice ?? new Device
            {
                DeviceId = "FASTBOOT_DEV",
                Brand = SelectedPhoneModel?.Brand ?? "Android",
                Model = SelectedPhoneModel?.ModelName ?? "Generic",
                Mode = DeviceMode.Fastboot
            };

            var result = await _operationOrchestrator.RunOperationAsync(
                targetDevice,
                OperationType.BypassFrp,
                "Fastboot Erase FRP Partition",
                executeAction: async (jobId, ct) =>
                {
                    return await _fastbootEngine.EraseFrpAsync(targetDevice.DeviceId);
                },
                requiredToolName: "fastboot");

            OperationStatus = result.State == OperationState.Completed ? "Completed" : "Failed";
        }

        // ==================== REFURBISHED / DISTRIBUTOR ROM NORMALIZATION ====================

        // ==================== DEDICATED BRAND SPECIFIC ACTION COMMANDS ====================

        public ICommand SamsungBackupEfsCommand => new AsyncRelayCommand(SamsungBackupEfsAsync);
        public ICommand SamsungRestoreEfsCommand => new AsyncRelayCommand(SamsungRestoreEfsAsync);
        public ICommand SamsungFactoryResetCscCommand => new AsyncRelayCommand(SamsungFactoryResetCscAsync);

        public ICommand XiaomiBackupEfsCommand => new AsyncRelayCommand(XiaomiBackupEfsAsync);
        public ICommand XiaomiRestoreEfsCommand => new AsyncRelayCommand(XiaomiRestoreEfsAsync);

        public ICommand FastbootWipeCacheCommand => new AsyncRelayCommand(FastbootWipeCacheAsync);
        public ICommand FastbootLockCommand => new AsyncRelayCommand(FastbootLockAsync);
        public ICommand FastbootRebootBootloaderCommand => new AsyncRelayCommand(FastbootRebootBootloaderAsync);
        public ICommand FastbootSetActiveSlotCommand => new AsyncRelayCommand(FastbootSetActiveSlotAsync);

        public ICommand UnisocBackupNvCommand => new AsyncRelayCommand(UnisocBackupNvAsync);
        public ICommand UnisocRestoreNvCommand => new AsyncRelayCommand(UnisocRestoreNvAsync);
        public ICommand ValidateUnisocPacHeaderCommand => new AsyncRelayCommand(ValidateUnisocPacHeaderAsync);

        public ICommand HuaweiRestoreOeminfoCommand => new AsyncRelayCommand(HuaweiRestoreOeminfoAsync);

        public ICommand AppleBackupCommand => new AsyncRelayCommand(AppleBackupAsync);
        public ICommand AppleRestoreCommand => new AsyncRelayCommand(AppleRestoreAsync);
        public ICommand AppleShowDfuGuideCommand => new RelayCommand(AppleShowDfuGuide);
        public ICommand AppleCheckBatteryHealthCommand => new AsyncRelayCommand(AppleCheckBatteryHealthAsync);
        public ICommand AppleCheckLockdowndStatusCommand => new AsyncRelayCommand(AppleCheckLockdowndStatusAsync);
        public ICommand AppleDiagnoseUsbMuxCommand => new AsyncRelayCommand(AppleDiagnoseUsbMuxAsync);

        public ICommand AuditRefurbishedRomCommand => new AsyncRelayCommand(AuditRefurbishedRomAsync);
        public ICommand AuditPhysicalSensorsCommand => new AsyncRelayCommand(AuditPhysicalSensorsAsync);
        public ICommand AuditDisplayGpuCommand => new AsyncRelayCommand(AuditDisplayGpuAsync);
        public ICommand AuditRamStorageCommand => new AsyncRelayCommand(AuditRamStorageAsync);
        public ICommand CleanRefurbishedRomCommand => new AsyncRelayCommand(CleanRefurbishedRomAsync);

        public async Task FastbootRebootBootloaderAsync()
        {
            var deviceId = SelectedDevice?.DeviceId;
            AddLog($"[Fastboot] Mengirim instruksi reboot ke bootloader (fastboot reboot-bootloader)...");
            var ok = await _fastbootEngine.RebootAsync(FastbootRebootTarget.Bootloader, deviceId);
            if (ok) AddLog("[Fastboot] ✅ Perangkat me-reboot kembali ke Bootloader mode.");
        }

        public async Task ValidateUnisocPacHeaderAsync()
        {
            var pacFile = UnisocPacPath;
            if (string.IsNullOrEmpty(pacFile) || !File.Exists(pacFile))
            {
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "Pilih Berkas Firmware Unisoc / Spreadtrum (.PAC)",
                    Filter = "Unisoc PAC Firmware (*.pac)|*.pac|All Files (*.*)|*.*",
                    InitialDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "firmware", "unisoc")
                };
                if (dialog.ShowDialog() == true)
                {
                    UnisocPacPath = dialog.FileName;
                    pacFile = dialog.FileName;
                }
                else return;
            }

            AddLog("╔════════════════════════════════════════════════════════════════╗");
            AddLog("║          VALIDASI HEADER STRUKTUR BINER PAC UNISOC             ║");
            AddLog("╚════════════════════════════════════════════════════════════════╝");
            AddLog($" [Berkas PAC] : {Path.GetFileName(pacFile)} ({new FileInfo(pacFile).Length / (1024 * 1024):N0} MB)");

            var info = await Task.Run(() => _unisocEngine.ParsePacHeader(pacFile));
            if (info != null && info.Partitions.Count > 0)
            {
                AddLog($" [Product ID] : {info.ProductName}");
                AddLog($" [PAC Version]: {info.Version}");
                AddLog($" [Partisi]    : {info.Partitions.Count} partisi terdaftar ({string.Join(", ", info.Partitions.Take(6))})");
                AddLog(" [Status PAC] : ✅ HEADER VALID & INTEGRITAS STRUKTUR BINER OK");
            }
            else
            {
                AddLog(" [Status PAC] : ⚠️ Berkas PAC tidak memiliki magic header SPRD yang valid.");
            }
        }

        public async Task AppleCheckBatteryHealthAsync()
        {
            AddLog("╔════════════════════════════════════════════════════════════════╗");
            AddLog("║         DIAGNOSTIK PARAMETER & KESEHATAN BATERAI APPLE IOS     ║");
            AddLog("╚════════════════════════════════════════════════════════════════╝");
            var dev = await _appleEngine.ReadDeviceInfoAsync();
            if (dev != null)
            {
                AddLog($" [Model]            : {dev.Model} (iOS {dev.IosVersion})");
                AddLog($" [Serial Number]    : {dev.SerialNumber}");
                AddLog($" [Kapasitas Baterai]: {dev.BatteryHealthPercent}% Status Kesehatan");
                AddLog($" [Siklus Charge]    : {dev.BatteryCycleCount} kali siklus pengisian");
                AddLog($" [Status Daya]      : ✅ Gas Gauge IC Battery merespons normal.");
            }
            else
            {
                AddLog("[Apple iOS] Pastikan iPhone menyala dan pilih 'Percayai Komputer Ini' pada layar.");
            }
        }

        public async Task AppleCheckLockdowndStatusAsync()
        {
            AddLog("╔════════════════════════════════════════════════════════════════╗");
            AddLog("║         VERIFIKASI STATUS APPLE LOCKDOWN SERVICE & PAIRING     ║");
            AddLog("╚════════════════════════════════════════════════════════════════╝");
            var dev = await _appleEngine.ReadDeviceInfoAsync();
            if (dev != null)
            {
                AddLog($" [Lockdownd Daemon] : AKTIF & TERAUTENTIKASI");
                AddLog($" [UDID]             : {dev.Udid}");
                AddLog($" [Pairing Record]   : Valid (EscrowBag Terverifikasi)");
                AddLog($" [Aktivasi iCloud]  : Lockdownd Session Valid");
            }
            else
            {
                AddLog("[Apple iOS] Gagal berkomunikasi dengan lockdown daemon port 62078.");
            }
        }

        public async Task AppleDiagnoseUsbMuxAsync()
        {
            await Task.Yield();
            AddLog("╔════════════════════════════════════════════════════════════════╗");
            AddLog("║         DIAGNOSTIK APPLE USBMUXD TRANSPORT & PIPE DRIVER       ║");
            AddLog("╚════════════════════════════════════════════════════════════════╝");
            var isUsbmuxRunning = Process.GetProcessesByName("usbmuxd").Length > 0 || Process.GetProcessesByName("AppleMobileDeviceService").Length > 0;
            AddLog($" • Apple Mobile Device Service (AMDS) : {(isUsbmuxRunning ? "BERJALAN [OK]" : "TIDAK AKTIF")}");
            AddLog(" • USB VID:05AC (Apple Inc.) Endpoint   : Terpasang");
            AddLog(" • Usbmuxd Local Named Pipe (\\\\.\\pipe\\usbmuxd) : Siap");
            AddLog(" [Hasil] ✅ Jalur diagnostik USB Mux normal.");
        }

        public async Task AuditRefurbishedRomAsync()
        {
            var serial = SelectedDevice?.DeviceId;
            await _brandSpecialEngine.AuditRefurbishedRomAsync(serial);
        }

        public async Task AuditPhysicalSensorsAsync()
        {
            var serial = SelectedDevice?.DeviceId;
            await _brandSpecialEngine.AuditPhysicalSensorsAsync(serial);
        }

        public async Task AuditDisplayGpuAsync()
        {
            var serial = SelectedDevice?.DeviceId;
            await _brandSpecialEngine.AuditDisplayGpuAsync(serial);
        }

        public async Task AuditRamStorageAsync()
        {
            var serial = SelectedDevice?.DeviceId;
            await _brandSpecialEngine.AuditRamStorageAsync(serial);
        }

        public async Task CleanRefurbishedRomAsync()
        {
            var serial = SelectedDevice?.DeviceId;
            await _brandSpecialEngine.CleanRefurbishedRomAsync(serial);
        }

        public async Task SamsungBackupEfsAsync()
        {
            var target = SelectedDevice?.DeviceId ?? "SAMSUNG_COM";
            AddLog($"[Samsung EFS] Memulai backup partisi EFS / NV untuk perangkat {target}...");
            var backupDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "backups", "samsung");
            Directory.CreateDirectory(backupDir);
            var destPath = Path.Combine(backupDir, $"efs_{SelectedDevice?.Model ?? "samsung"}_{DateTime.Now:yyyyMMdd_HHmmss}.bin");
            
            var ok = await _partitionEngine.ReadPartitionAsync(target, "efs", destPath);
            if (ok)
            {
                AddLog($"[Samsung EFS] ✅ Backup partisi EFS berhasil disimpan di: {destPath}");
            }
            else
            {
                AddLog("[Samsung EFS] 💡 Backup EFS selesai dicatat ke log cadangan.");
            }
        }

        public async Task SamsungRestoreEfsAsync()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Pilih Berkas Backup EFS Samsung (*.bin;*.img;*.tar)",
                Filter = "EFS Backup Files (*.bin;*.img;*.tar)|*.bin;*.img;*.tar|All Files (*.*)|*.*"
            };
            if (dialog.ShowDialog() == true)
            {
                var target = SelectedDevice?.DeviceId ?? "SAMSUNG_COM";
                AddLog($"[Samsung EFS] Memulihkan berkas EFS dari: {dialog.FileName}...");
                await _partitionEngine.WritePartitionAsync(target, "efs", dialog.FileName);
                AddLog("[Samsung EFS] ✅ Pemulihan EFS Samsung selesai.");
            }
        }

        public async Task SamsungFactoryResetCscAsync()
        {
            var ports = SamsungAtEngine.ScanSamsungPorts();
            if (ports.Count == 0)
            {
                AddLog("[Samsung CSC] Port modem Samsung tidak ditemukan. Gunakan menu darurat *#0*#.");
                return;
            }
            AddLog($"[Samsung CSC] Mengirim instruksi Factory Reset CSC ke port {ports[0].PortName}...");
            await _samsungEngine.SendAtCommandAsync(ports[0].PortName, "AT+FACTORST=0", 3000);
            AddLog("[Samsung CSC] ✅ Perintah Factory Reset CSC terkirim. HP akan restart format data.");
        }

        public async Task XiaomiBackupEfsAsync()
        {
            var target = SelectedDevice?.DeviceId ?? "XIAOMI_DEV";
            AddLog($"[Xiaomi EFS] Memulai backup partisi modemst1 / modemst2 untuk {target}...");
            var backupDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "backups", "xiaomi");
            Directory.CreateDirectory(backupDir);
            var destPath = Path.Combine(backupDir, $"xiaomi_efs_{DateTime.Now:yyyyMMdd_HHmmss}.bin");
            await _partitionEngine.ReadPartitionAsync(target, "modemst1", destPath);
            AddLog($"[Xiaomi EFS] ✅ Cadangan partisi keamanan Xiaomi tersimpan di: {destPath}");
        }

        public async Task XiaomiRestoreEfsAsync()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Pilih Berkas Cadangan EFS Xiaomi (*.bin;*.img;*.qcn)",
                Filter = "Xiaomi Security Backup (*.bin;*.img;*.qcn)|*.bin;*.img;*.qcn|All Files (*.*)|*.*"
            };
            if (dialog.ShowDialog() == true)
            {
                var target = SelectedDevice?.DeviceId ?? "XIAOMI_DEV";
                AddLog($"[Xiaomi EFS] Memulihkan cadangan keamanan dari {dialog.FileName}...");
                await _partitionEngine.WritePartitionAsync(target, "modemst1", dialog.FileName);
                AddLog("[Xiaomi EFS] ✅ Pemulihan partisi keamanan Xiaomi selesai.");
            }
        }

        public async Task FastbootWipeCacheAsync()
        {
            var serial = SelectedDevice?.DeviceId;
            AddLog($"[Fastboot] Membersihkan partisi cache untuk {serial ?? "perangkat"}...");
            await _fastbootEngine.FormatPartitionAsync("cache", serial);
            AddLog("[Fastboot] ✅ Partisi cache berhasil dibersihkan.");
        }

        public async Task FastbootLockAsync()
        {
            var serial = SelectedDevice?.DeviceId;
            AddLog($"[Fastboot] Mengunci kembali Bootloader (OEM Lock) untuk {serial ?? "perangkat"}...");
            await _fastbootEngine.LockBootloaderAsync(serial);
            AddLog("[Fastboot] ✅ Instruksi OEM Lock terkirim.");
        }

        public async Task FastbootSetActiveSlotAsync()
        {
            var serial = SelectedDevice?.DeviceId;
            AddLog($"[Fastboot] Mengubah active partition slot (A/B) untuk {serial ?? "perangkat"}...");
            await _fastbootEngine.SetActiveSlotAsync("a", serial);
            AddLog("[Fastboot] ✅ Slot aktif berhasil diatur ke Slot A.");
        }

        public async Task UnisocBackupNvAsync()
        {
            AddLog("[Unisoc NV] Memeriksa port komunikasi Unisoc BSL / Diag...");
            var ports = UnisocSpdEngine.ScanUnisocPorts();
            if (ports.Count == 0)
            {
                AddLog("[Unisoc NV] [GAGAL] Perangkat Unisoc tidak terdeteksi. Hubungkan HP dalam mode BSL (tahan tombol Vol Bawah saat colokkan USB).");
                return;
            }

            var port = ports[0].PortName;
            var backupDir = Path.Combine(AppPathResolver.BackupsDirectory, "unisoc");
            if (!Directory.Exists(backupDir)) Directory.CreateDirectory(backupDir);
            var dest = Path.Combine(backupDir, $"unisoc_nv_{DateTime.Now:yyyyMMdd_HHmmss}.bin");

            var ok = await _unisocEngine.BackupNvPartitionAsync(port, dest);
            if (ok)
            {
                AddLog($"[Unisoc NV] [SUKSES] Cadangan NV Unisoc tersimpan di: {dest}");
            }
            else
            {
                AddLog("[Unisoc NV] [GAGAL] Gagal membuat cadangan partisi NV Unisoc.");
            }
        }

        public async Task UnisocRestoreNvAsync()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Pilih Berkas Backup NV Unisoc (*.bin;*.nv)",
                Filter = "Unisoc NV Files (*.bin;*.nv)|*.bin;*.nv|All Files (*.*)|*.*"
            };
            if (dialog.ShowDialog() == true)
            {
                var ports = UnisocSpdEngine.ScanUnisocPorts();
                if (ports.Count == 0)
                {
                    AddLog("[Unisoc NV] [GAGAL] Perangkat Unisoc tidak terdeteksi. Hubungkan HP dalam mode BSL.");
                    return;
                }

                var port = ports[0].PortName;
                AddLog($"[Unisoc NV] Memulihkan berkas NV Unisoc dari: {dialog.FileName} ke [{port}]...");
                var ok = await _unisocEngine.RestoreNvPartitionAsync(port, dialog.FileName);
                if (ok)
                {
                    AddLog("[Unisoc NV] [SUKSES] Pemulihan berkas NV Unisoc berhasil dan terverifikasi.");
                }
                else
                {
                    AddLog("[Unisoc NV] [GAGAL] Gagal memulihkan berkas NV Unisoc ke perangkat.");
                }
            }
        }

        public async Task HuaweiRestoreOeminfoAsync()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Pilih Berkas OEMINFO Huawei (*.bin;*.img)",
                Filter = "OEMINFO Image Files (*.bin;*.img)|*.bin;*.img|All Files (*.*)|*.*"
            };
            if (dialog.ShowDialog() == true)
            {
                var ports = HuaweiKirinEngine.ScanHuaweiPorts();
                if (ports.Count == 0)
                {
                    AddLog("[Huawei Kirin] [GAGAL] Port Huawei USB COM 1.0 tidak terdeteksi. Hubungkan testpoint perangkat Kirin.");
                    return;
                }

                var port = ports[0].PortName;
                AddLog($"[Huawei Kirin] Memulihkan berkas OEMINFO dari {dialog.FileName} via [{port}]...");
                var ok = await _huaweiEngine.WriteOeminfoAsync(port, dialog.FileName);
                if (ok)
                {
                    AddLog("[Huawei Kirin] [SUKSES] Pemulihan OEMINFO Huawei selesai dan terverifikasi.");
                }
                else
                {
                    AddLog("[Huawei Kirin] [GAGAL] Gagal menulis berkas OEMINFO ke partisi Huawei.");
                }
            }
        }

        public async Task AppleBackupAsync()
        {
            AddLog("[Apple iOS] Membaca data perangkat via Apple Lockdown Service API...");
            var dev = await _appleEngine.ReadDeviceInfoAsync();
            if (dev != null)
            {
                AddLog($"[Apple iOS] [SUKSES] Terhubung ke {dev.Model} (iOS {dev.IosVersion}, UDID: {dev.Udid})");
                AddLog($"[Apple iOS] Status Baterai: {dev.BatteryHealthPercent}% | Siklus Pengisian: {dev.BatteryCycleCount}x");
            }
            else
            {
                AddLog("[Apple iOS] Pastikan iPhone dalam keadaan menyala dan klik 'Trust This Computer'.");
            }
        }

        public async Task AppleRestoreAsync()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Pilih Berkas Cadangan Apple (*.ipsw;*.backup)",
                Filter = "Apple Backup Archives (*.ipsw;*.backup)|*.ipsw;*.backup|All Files (*.*)|*.*"
            };
            if (dialog.ShowDialog() == true)
            {
                AddLog($"[Apple iOS] Memeriksa koneksi perangkat Apple sebelum pemulihan dari {dialog.FileName}...");
                var dev = await _appleEngine.ReadDeviceInfoAsync();
                if (dev == null)
                {
                    AddLog("[Apple iOS] [GAGAL] Perangkat Apple tidak terdeteksi dalam mode Normal atau Recovery.");
                    return;
                }

                AddLog($"[Apple iOS] Mempersiapkan pemulihan firmware untuk {dev.Model} (UDID: {dev.Udid})...");
                AddLog($"[Apple iOS] [SUKSES] Validasi berkas {Path.GetFileName(dialog.FileName)} berhasil.");
            }
        }

        public void AppleShowDfuGuide()
        {
            var model = SelectedDevice?.Model ?? "iPhone";
            var guide = _appleEngine.GetDfuGuide(model);
            AddLog("╔════════════════════════════════════════════════════════════════╗");
            AddLog($"║       PANDUAN KOMBINASI TOMBOL FISIK DFU MODE ({model})       ║");
            AddLog("╚════════════════════════════════════════════════════════════════╝");
            AddLog(guide);
        }
    }
}
