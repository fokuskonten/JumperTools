using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using JumperTools.Core;

namespace JumperTools.UI.ViewModels
{
    public partial class MainViewModel
    {
        // ==================== DRIVER ASSISTANT OPERATIONS ====================

        public async Task CheckDriversAsync()
        {
            CurrentOperation = "Memeriksa Driver Servis Windows...";
            OperationProgress = 30;

            try
            {
                var list = await _driverEngine.CheckAllDriversAsync();
                Drivers.Clear();
                foreach (var d in list)
                {
                    Drivers.Add(d);
                }
                OperationProgress = 100;
                CurrentOperation = "Pemeriksaan Driver Selesai";
            }
            catch (Exception ex)
            {
                AddLog($"[Driver Error] {ex.Message}");
                CurrentOperation = "Error Driver Check";
            }
        }

        public async Task InstallUsbDkAsync()
        {
            CurrentOperation = "Memasang UsbDk Driver...";
            OperationProgress = 50;
            var result = await _driverEngine.InstallUsbDkAsync();
            OperationProgress = 100;
            
            if (result.Success)
            {
                CurrentOperation = "UsbDk Terpasang";
                UserGuidanceMessage = "Pemasangan UsbDk sukses! Silakan restart PC Anda untuk kestabilan BROM.";
                await CheckDriversAsync();
            }
            else
            {
                CurrentOperation = "Gagal Memasang UsbDk";
                AddLog($"[Driver Installer Error] {result.ErrorMessage}");
            }
        }

        public async Task InstallLibUsbAsync()
        {
            CurrentOperation = "Memasang LibUSB Driver...";
            OperationProgress = 50;
            var result = await _driverEngine.InstallLibUsbAsync();
            OperationProgress = 100;

            if (result.Success)
            {
                CurrentOperation = "LibUSB Terpasang";
                await CheckDriversAsync();
            }
            else
            {
                CurrentOperation = "Gagal Memasang LibUSB";
                AddLog($"[Driver Installer Error] {result.ErrorMessage}");
            }
        }

        public void OpenDeviceManager()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "devmgmt.msc",
                    UseShellExecute = true
                });
                AddLog("[Driver Assistant] Membuka Windows Device Manager...");
            }
            catch (Exception ex)
            {
                AddLog($"[Driver Error] Gagal membuka Device Manager: {ex.Message}");
            }
        }

        private string GetDriversBasePath()
        {
            var candidates = new[]
            {
                System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "drivers"),
                System.IO.Path.Combine(_toolsBasePath, "drivers"),
                System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "build", "Release", "tools", "drivers")
            };

            return candidates.FirstOrDefault(System.IO.Directory.Exists) ?? candidates[0];
        }

        public async Task InstallDriverItemAsync(DriverStatusItem? item)
        {
            if (item == null) return;

            AddLog($"[Driver Assistant] 🚀 Meluncurkan installer: {item.DriverName}...");
            CurrentOperation = $"Menjalankan Installer {item.DriverName}...";
            OperationProgress = 40;

            var driversBase = GetDriversBasePath();
            var installerName = !string.IsNullOrWhiteSpace(item.InstallerFileName) ? item.InstallerFileName : "";
            var targetInstaller = System.IO.Path.Combine(driversBase, installerName);

            if (System.IO.File.Exists(targetInstaller) && new System.IO.FileInfo(targetInstaller).Length > 10000)
            {
                var fi = new System.IO.FileInfo(targetInstaller);
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
                    var subfolder = "Qualcomm";
                    if (item.TargetChipset.Contains("MediaTek") || item.HardwareId.Contains("0E8D")) subfolder = "MediaTek";
                    else if (item.TargetChipset.Contains("Unisoc") || item.HardwareId.Contains("1782")) subfolder = "Unisoc";
                    else if (item.TargetChipset.Contains("Apple") || item.HardwareId.Contains("05AC")) subfolder = "Apple";
                    else if (item.TargetChipset.Contains("Sony") || item.HardwareId.Contains("0FCE")) subfolder = "Sony";
                    else if (item.TargetChipset.Contains("ADB") || item.HardwareId.Contains("18D1")) subfolder = "ADB";

                    p = Process.Start(new ProcessStartInfo
                    {
                        FileName = targetInstaller,
                        Arguments = $"--chipset \"{item.DriverName}\" --folder \"{subfolder}\"",
                        WorkingDirectory = System.IO.Path.GetDirectoryName(targetInstaller),
                        UseShellExecute = true
                    });
                }

                if (p != null) await p.WaitForExitAsync();

                OperationProgress = 100;
                CurrentOperation = $"Pemasangan {item.DriverName} Selesai";
                AddLog($"[Driver Assistant] ✅ Wizard instalasi {item.DriverName} selesai.");
                await CheckDriversAsync();
                return;
            }

            AddLog($"[Driver Assistant Error] Berkas installer {item.DriverName} ({installerName}) tidak ditemukan atau tidak valid.");
            CurrentOperation = "Installer Tidak Ditemukan";
            OperationProgress = 100;
        }

        public async Task InstallAllDriversAsync()
        {
            AddLog("[Driver Assistant] ⚡ Memulai pemasangan Paket Driver Lengkap...");
            CurrentOperation = "Memasang Seluruh Paket Driver...";
            OperationProgress = 20;

            var driversBase = GetDriversBasePath();

            // 1. Pasang UsbDk
            var usbdk = System.IO.Path.Combine(driversBase, "UsbDK_Filter_x64_Setup.msi");
            if (System.IO.File.Exists(usbdk))
            {
                var p = Process.Start(new ProcessStartInfo { FileName = "msiexec.exe", Arguments = $"/i \"{usbdk}\" /passive /norestart", UseShellExecute = true });
                if (p != null) await p.WaitForExitAsync();
            }

            // 2. Pasang Universal ADB
            var adb = System.IO.Path.Combine(driversBase, "Android_ADB_Driver_Setup.msi");
            if (System.IO.File.Exists(adb))
            {
                var p = Process.Start(new ProcessStartInfo { FileName = "msiexec.exe", Arguments = $"/i \"{adb}\" /passive /norestart", UseShellExecute = true });
                if (p != null) await p.WaitForExitAsync();
            }

            // 3. Pasang Samsung
            var samsung = System.IO.Path.Combine(driversBase, "Samsung_USB_Driver_Setup.exe");
            if (System.IO.File.Exists(samsung))
            {
                var p = Process.Start(new ProcessStartInfo { FileName = samsung, Arguments = "/s", UseShellExecute = true });
                if (p != null) await p.WaitForExitAsync();
            }

            // 4. Daftarkan INF DriverStore
            var psi = new ProcessStartInfo
            {
                FileName = "pnputil.exe",
                Arguments = $"/add-driver \"{System.IO.Path.Combine(driversBase, "*.inf")}\" /subdirs /install",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            var pInf = Process.Start(psi);
            if (pInf != null) await pInf.WaitForExitAsync();

            OperationProgress = 100;
            CurrentOperation = "Paket Driver Lengkap Terpasang";
            AddLog("[Driver Assistant] ✅ Seluruh paket driver resmi selesai dipasang.");
            await CheckDriversAsync();
        }

        public void OpenDriversFolder()
        {
            try
            {
                var dir = GetDriversBasePath();

                if (System.IO.Directory.Exists(dir))
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
    }
}
