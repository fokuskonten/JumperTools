using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using JumperTools.Core;
using JumperTools.Models;
using JumperTools.UI.ViewModels.Shared;
using Microsoft.Win32;

namespace JumperTools.UI.ViewModels.Tabs
{
    public class DriverAssistantViewModel : TabViewModelBase
    {
        private readonly DriverAssistantEngine _driverEngine;

        public ObservableCollection<DriverStatusItem> Drivers { get; }
        public string TerminalLogText => Context.TerminalLogText;

        public ICommand CheckDriversCommand { get; }
        public ICommand InstallUsbDkCommand { get; }
        public ICommand InstallLibUsbCommand { get; }
        public ICommand InstallDriverItemCommand { get; }
        public ICommand InstallAllDriversCommand { get; }
        public ICommand OpenDriversFolderCommand { get; }
        public ICommand OpenDeviceManagerCommand { get; }
        public ICommand CopyLogCommand { get; }
        public ICommand ClearLogCommand { get; }
        public ICommand ShowFeatureGuideCommand { get; }
        public ICommand AdbShellCommand { get; }
        public ICommand InstallApkCommand { get; }
        public ICommand ScreenMirrorCommand { get; }
        public ICommand ScreenRecordCommand { get; }

        public DriverAssistantViewModel(ISharedWorkspaceContext context, DriverAssistantEngine driverEngine)
            : base(context)
        {
            _driverEngine = driverEngine;
            Drivers = new ObservableCollection<DriverStatusItem>();

            CheckDriversCommand = new AsyncRelayCommand(CheckDriversAsync);
            InstallUsbDkCommand = new AsyncRelayCommand(InstallUsbDkAsync);
            InstallLibUsbCommand = new AsyncRelayCommand(InstallLibUsbAsync);
            InstallDriverItemCommand = new AsyncRelayCommand<DriverStatusItem>(InstallDriverItemAsync);
            InstallAllDriversCommand = new AsyncRelayCommand(InstallAllDriversAsync);
            OpenDriversFolderCommand = new RelayCommand(OpenDriversFolder);
            OpenDeviceManagerCommand = new RelayCommand(OpenDeviceManager);
            CopyLogCommand = new RelayCommand(Context.CopyLog);
            ClearLogCommand = new RelayCommand(Context.ClearLog);
            ShowFeatureGuideCommand = new RelayCommand<string>(key => Context.ShowFeatureGuide(key ?? "Tab_UsbDrivers"));
            AdbShellCommand = new RelayCommand(ExecuteAdbShell);
            InstallApkCommand = new AsyncRelayCommand(InstallApkAsync);
            ScreenMirrorCommand = new AsyncRelayCommand(ScreenMirrorAsync);
            ScreenRecordCommand = new AsyncRelayCommand(ScreenRecordAsync);
        }

        public async Task CheckDriversAsync()
        {
            Context.CurrentOperation = "Memeriksa Driver Servis Windows...";
            Context.OperationProgress = 30;

            try
            {
                var list = await _driverEngine.CheckAllDriversAsync();
                Drivers.Clear();
                foreach (var d in list)
                {
                    Drivers.Add(d);
                }
                Context.OperationProgress = 100;
                Context.CurrentOperation = "Pemeriksaan Driver Selesai";
            }
            catch (Exception ex)
            {
                AddLog($"[Driver Error] {ex.Message}");
                Context.CurrentOperation = "Error Driver Check";
            }
        }

        public async Task InstallUsbDkAsync()
        {
            Context.CurrentOperation = "Memasang UsbDk Driver...";
            Context.OperationProgress = 50;
            var result = await _driverEngine.InstallUsbDkAsync();
            Context.OperationProgress = 100;
            
            if (result.Success)
            {
                Context.CurrentOperation = "UsbDk Terpasang";
                AddLog("[Driver Assistant] Pemasangan UsbDk sukses!");
                await CheckDriversAsync();
            }
            else
            {
                Context.CurrentOperation = "Gagal Memasang UsbDk";
                AddLog($"[Driver Installer Error] {result.ErrorMessage}");
            }
        }

        public async Task InstallLibUsbAsync()
        {
            Context.CurrentOperation = "Memasang LibUSB Driver...";
            Context.OperationProgress = 50;
            var result = await _driverEngine.InstallLibUsbAsync();
            Context.OperationProgress = 100;

            if (result.Success)
            {
                Context.CurrentOperation = "LibUSB Terpasang";
                await CheckDriversAsync();
            }
            else
            {
                Context.CurrentOperation = "Gagal Memasang LibUSB";
                AddLog($"[Driver Installer Error] {result.ErrorMessage}");
            }
        }

        public void OpenDeviceManager() => Context.OpenDeviceManager();

        private string GetDriversBasePath()
        {
            var candidates = new[]
            {
                AppPathResolver.DriversDirectory,
                Path.Combine(Context.ToolsBasePath, "drivers"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "drivers"),
                Path.Combine(AppPathResolver.RootDirectory, "build", "Release", "tools", "drivers")
            };

            return candidates.FirstOrDefault(Directory.Exists) ?? candidates[0];
        }

        public async Task InstallDriverItemAsync(DriverStatusItem? item)
        {
            if (item == null) return;

            item.IsInstalling = true;
            try
            {
                AddLog($"[Driver Assistant] 🚀 Memulai instalasi driver: {item.DriverName}...");
                Context.CurrentOperation = $"Memasang {item.DriverName}...";
                Context.OperationProgress = 30;

                var driversBase = GetDriversBasePath();

                // 1. Tangani UsbDk & LibUSB Khusus
                if (item.HardwareId == "USBDK_SERVICE")
                {
                    var res = await _driverEngine.InstallUsbDkAsync();
                    if (res.Success) AddLog("[Driver Assistant] ✅ UsbDk Filter Driver berhasil dipasang.");
                    else AddLog($"[Driver Assistant Error] {res.ErrorMessage}");
                    await CheckDriversAsync();
                    return;
                }
                if (item.HardwareId == "LIBUSB_FILTER")
                {
                    await _driverEngine.LaunchZadigAsync();
                    await CheckDriversAsync();
                    return;
                }

                // 2. Prioritas 1: Cari berkas .INF resmi di subfolder chipset
                string? targetInf = null;
                if (Directory.Exists(driversBase))
                {
                    var subfolder = "Qualcomm";
                    if (item.TargetChipset.Contains("MediaTek") || item.HardwareId.Contains("0E8D")) subfolder = "MediaTek";
                    else if (item.TargetChipset.Contains("Unisoc") || item.HardwareId.Contains("1782")) subfolder = "Unisoc";
                    else if (item.TargetChipset.Contains("Sony") || item.HardwareId.Contains("05AC")) subfolder = "Sony";
                    else if (item.TargetChipset.Contains("Samsung") || item.HardwareId.Contains("04E8")) subfolder = "Samsung";
                    else if (item.TargetChipset.Contains("Android") || item.HardwareId.Contains("18D1")) subfolder = "ADB";

                    var chipsetDir = Path.Combine(driversBase, subfolder);
                    if (Directory.Exists(chipsetDir))
                    {
                        targetInf = Directory.GetFiles(chipsetDir, "*.inf", SearchOption.AllDirectories).FirstOrDefault();
                    }

                    if (targetInf == null)
                    {
                        targetInf = Directory.GetFiles(driversBase, "*.inf", SearchOption.AllDirectories)
                            .FirstOrDefault(f => f.Contains(subfolder, StringComparison.OrdinalIgnoreCase) ||
                                                 (subfolder == "MediaTek" && f.Contains("cdc-acm", StringComparison.OrdinalIgnoreCase)) ||
                                                 (subfolder == "Unisoc" && f.Contains("sprd", StringComparison.OrdinalIgnoreCase)));
                    }
                }

                if (!string.IsNullOrEmpty(targetInf) && File.Exists(targetInf))
                {
                    AddLog($"[Driver Assistant] ⚡ Menemukan berkas driver INF resmi: {Path.GetFileName(targetInf)}");
                    AddLog($"                   Lokasi: {targetInf}");
                    var infResult = await _driverEngine.InstallInfDriverAsync(targetInf);
                    if (infResult.Success)
                    {
                        Context.CurrentOperation = $"Driver {item.DriverName} Terpasang";
                        Context.OperationProgress = 100;
                        AddLog($"[Driver Assistant] ✅ Driver INF {Path.GetFileName(targetInf)} berhasil didaftarkan ke Windows DriverStore!");
                        await CheckDriversAsync();
                        return;
                    }
                }

                // 3. Prioritas 2: Cari installer EXE / MSI
                string? targetInstaller = null;
                if (!string.IsNullOrWhiteSpace(item.InstallerFileName))
                {
                    var directPath = Path.Combine(driversBase, item.InstallerFileName);
                    if (File.Exists(directPath) && new FileInfo(directPath).Length > 10000)
                    {
                        targetInstaller = directPath;
                    }
                }

                if (targetInstaller == null && Directory.Exists(driversBase))
                {
                    var altFiles = Directory.GetFiles(driversBase, "*.*", SearchOption.AllDirectories)
                        .Where(f => (f.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".msi", StringComparison.OrdinalIgnoreCase)) && new FileInfo(f).Length > 10000)
                        .ToList();

                    if (item.TargetChipset.Contains("Samsung") || item.HardwareId.Contains("04E8"))
                        targetInstaller = altFiles.FirstOrDefault(f => f.Contains("Samsung", StringComparison.OrdinalIgnoreCase));
                    else if (item.TargetChipset.Contains("Android") || item.HardwareId.Contains("18D1"))
                        targetInstaller = altFiles.FirstOrDefault(f => f.Contains("ADB", StringComparison.OrdinalIgnoreCase));
                    else if (item.TargetChipset.Contains("Qualcomm") || item.HardwareId.Contains("05C6"))
                        targetInstaller = altFiles.FirstOrDefault(f => f.Contains("Qualcomm", StringComparison.OrdinalIgnoreCase));
                    else if (item.TargetChipset.Contains("MediaTek") || item.HardwareId.Contains("0E8D"))
                        targetInstaller = altFiles.FirstOrDefault(f => f.Contains("MediaTek", StringComparison.OrdinalIgnoreCase));
                    else if (item.TargetChipset.Contains("Unisoc") || item.HardwareId.Contains("1782"))
                        targetInstaller = altFiles.FirstOrDefault(f => f.Contains("Unisoc", StringComparison.OrdinalIgnoreCase));
                }

                if (targetInstaller != null && File.Exists(targetInstaller))
                {
                    var fi = new FileInfo(targetInstaller);
                    AddLog($"[Driver Assistant] Membuka wizard installer: {fi.Name} ({fi.Length / 1024:N0} KB)...");

                    Process? p = null;
                    if (targetInstaller.EndsWith(".msi", StringComparison.OrdinalIgnoreCase))
                    {
                        p = Process.Start(new ProcessStartInfo
                        {
                            FileName = "msiexec.exe",
                            Arguments = $"/i \"{targetInstaller}\"",
                            UseShellExecute = true
                        });
                    }
                    else
                    {
                        p = Process.Start(new ProcessStartInfo
                        {
                            FileName = targetInstaller,
                            WorkingDirectory = Path.GetDirectoryName(targetInstaller),
                            UseShellExecute = true
                        });
                    }

                    if (p != null) await p.WaitForExitAsync();

                    Context.OperationProgress = 100;
                    Context.CurrentOperation = $"Pemasangan {item.DriverName} Selesai";
                    AddLog($"[Driver Assistant] ✅ Installer {item.DriverName} selesai dijalankan.");
                    await CheckDriversAsync();
                    return;
                }

                AddLog($"[Driver Assistant Error] Berkas driver INF atau installer untuk {item.DriverName} tidak ditemukan.");
                Context.CurrentOperation = $"Driver {item.DriverName} Tidak Ditemukan";
                Context.OperationProgress = 100;
            }
            catch (Exception ex)
            {
                AddLog($"[Driver Assistant Error] Gagal memasang driver: {ex.Message}");
            }
            finally
            {
                item.IsInstalling = false;
            }
        }

        public async Task InstallAllDriversAsync()
        {
            AddLog("[Driver Assistant] ⚡ Memulai pemasangan Driver Lengkap Semua Brand (Qualcomm, MTK, Samsung, Unisoc, ADB, UsbDk)...");
            Context.CurrentOperation = "Memasang Seluruh Paket Driver INF & Filter...";
            Context.OperationProgress = 10;

            foreach (var d in Drivers.Where(x => !x.IsInstalled))
            {
                d.IsInstalling = true;
            }

            try
            {
                var driversBase = GetDriversBasePath();

                // 1. Eksekusi batch seluruh INF secara native via DriverEngine
                if (Directory.Exists(driversBase))
                {
                    AddLog("[Driver Assistant] [1/2] Mendaftarkan seluruh berkas INF ke Windows DriverStore (pnputil)...");
                    await _driverEngine.InstallAllInfDriversBatchAsync(driversBase);
                }

                // 2. Pasang UsbDk Filter
                AddLog("[Driver Assistant] [2/2] Memasang UsbDk Filter Driver...");
                await _driverEngine.InstallUsbDkAsync();

                Context.CurrentOperation = "Paket Driver Lengkap Terpasang";
                Context.OperationProgress = 100;
                AddLog("[Driver Assistant] ✅ Seluruh paket driver resmi siap digunakan.");
            }
            catch (Exception ex)
            {
                AddLog($"[Driver Assistant Error] {ex.Message}");
            }
            finally
            {
                foreach (var d in Drivers)
                {
                    d.IsInstalling = false;
                }
                await CheckDriversAsync();
            }
        }

        public void OpenDriversFolder()
        {
            try
            {
                var dir = GetDriversBasePath();

                if (Directory.Exists(dir))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = $"\"{dir}\"",
                        UseShellExecute = true
                    });
                    AddLog("[Driver Assistant] Membuka folder database Driver Windows...");
                }
            }
            catch (Exception ex)
            {
                AddLog($"[Driver Error] Gagal membuka folder driver: {ex.Message}");
            }
        }

        private void ExecuteAdbShell()
        {
            try
            {
                var serial = Context.SelectedDevice?.DeviceId;
                var targetArg = string.IsNullOrEmpty(serial) ? "shell" : $"-s {serial} shell";
                var adbExe = Path.Combine(Context.PlatformToolsPath, "adb.exe");
                if (!File.Exists(adbExe)) adbExe = "adb.exe";

                Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/k \"{adbExe}\" {targetArg}",
                    UseShellExecute = true
                });
                AddLog($"[ADB Terminal] Membuka shell ADB mandiri untuk [{serial ?? "Default"}]...");
            }
            catch (Exception ex)
            {
                AddLog($"[ADB Error] Gagal meluncurkan shell: {ex.Message}");
            }
        }

        public async Task InstallApkAsync()
        {
            var ofd = new OpenFileDialog
            {
                Title = "Pilih Berkas Android Package (*.apk)",
                Filter = "Android Package (*.apk)|*.apk|All Files (*.*)|*.*"
            };

            if (ofd.ShowDialog() == true)
            {
                var apkPath = ofd.FileName;
                AddLog($"[APK Installer] 🚀 Mengirim file {Path.GetFileName(apkPath)} ke HP...");
                Context.CurrentOperation = "Menginstal APK...";
                Context.OperationProgress = 30;

                var res = await Context.ToolEngine.InstallApkAsync(Context.SelectedDevice?.DeviceId ?? "", apkPath);
                var ok = res.Success;
                Context.OperationProgress = 100;
                Context.CurrentOperation = ok ? "APK Berhasil Terpasang" : $"Gagal Memasang APK: {res.ErrorMessage}";
                AddLog(ok ? $"[APK Installer] Sukses memasang: {Path.GetFileName(apkPath)}" : $"[APK Installer] Gagal: {res.ErrorMessage}");
            }
        }

        public async Task ScreenMirrorAsync()
        {
            var serial = Context.SelectedDevice?.DeviceId;
            var scrcpyDir = Path.Combine(Context.ToolsBasePath, "scrcpy");
            var scrcpyExe = Path.Combine(scrcpyDir, "scrcpy.exe");

            if (!File.Exists(scrcpyExe))
            {
                scrcpyExe = Path.Combine(AppPathResolver.ToolsDirectory, "scrcpy", "scrcpy.exe");
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
                Context.CurrentOperation = "Screen Mirror Aktif";
            }
            else
            {
                AddLog("[Info] scrcpy.exe tidak ditemukan di tools/scrcpy/. Membuka mode rekam layar via ADB...");
                await ScreenRecordAsync();
            }
        }

        public async Task ScreenRecordAsync()
        {
            var serial = Context.SelectedDevice?.DeviceId;
            var adbExe = Path.Combine(Context.PlatformToolsPath, "adb.exe");
            if (!File.Exists(adbExe)) adbExe = "adb.exe";

            var targetArg = string.IsNullOrEmpty(serial) ? "shell screenrecord --time-limit 30 /sdcard/service_record.mp4" : $"-s {serial} shell screenrecord --time-limit 30 /sdcard/service_record.mp4";

            AddLog($"[ADB Record] Memulai perekaman layar 30 detik pada [{serial ?? "Default"}]...");
            Context.CurrentOperation = "Merekam Layar (Max 30s)...";
            Context.OperationProgress = 20;

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
                    Context.OperationProgress = 100;
                    AddLog("[ADB Record] Selesai merekam! Video tersimpan di HP: /sdcard/service_record.mp4");
                    Context.CurrentOperation = "Rekam Layar Selesai (/sdcard/service_record.mp4)";
                }
            }
            catch (Exception ex)
            {
                AddLog($"[Record Error] {ex.Message}");
            }
        }
    }
}
