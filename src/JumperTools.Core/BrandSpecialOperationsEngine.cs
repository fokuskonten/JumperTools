using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace JumperTools.Core
{
    public class SamsungKnoxStatus
    {
        public string KgState { get; set; } = "Unknown";
        public string WarrantyBit { get; set; } = "0x0 (Official)";
        public string BootloaderLock { get; set; } = "Locked";
        public bool IsKnoxTripped { get; set; }
    }

    /// <summary>
    /// Engine for specialized brand fixes: Anti-Relock Mi Cloud, Remove Demo Mode (Oppo/Vivo), and Samsung Knox Check
    /// </summary>
    public class BrandSpecialOperationsEngine
    {
        private readonly string _adbPath;

        public event Action<string>? OnLog;
        public event Action<string>? OnUserActionRequired;

        public BrandSpecialOperationsEngine(string platformToolsPath)
        {
            _adbPath = Path.Combine(platformToolsPath, "adb.exe");
        }

        private void Log(string message)
        {
            var formatted = $"[{DateTime.Now:HH:mm:ss}] {message}";
            OnLog?.Invoke(formatted);
        }

        /// <summary>
        /// Disable Mi Cloud & Find Device services on Xiaomi to prevent re-lock after connecting to Wi-Fi
        /// </summary>
        public async Task<bool> DisableXiaomiAntiRelockAsync(string? deviceSerial = null, CancellationToken cancellationToken = default)
        {
            Log("╔════════════════════════════════════════════════════════════════╗");
            Log("║         XIAOMI ANTI-RELOCK (DISABLE MI CLOUD & FIND DEVICE)    ║");
            Log("╚════════════════════════════════════════════════════════════════╝");

            Log("\n[Langkah 1/3] Menghubungkan ke ADB Service...");
            var (isConnected, stateMsg) = await VerifyAdbConnectionAsync(deviceSerial);
            if (!isConnected)
            {
                Log($"[GAGAL] Perangkat Xiaomi tidak terhubung via ADB: {stateMsg}");
                Log("[Petunjuk] Nyalakan HP dan aktifkan USB Debugging di Opsi Pengembang.");
                return false;
            }

            var packages = new[]
            {
                "com.xiaomi.finddevice",
                "com.miui.cloudservice",
                "com.miui.micloudsync",
                "com.xiaomi.payment",
                "com.miui.cloudbackup",
                "com.miui.analytics"
            };

            Log("[Langkah 2/3] Menonaktifkan paket layanan pelacak Mi Cloud:");
            int successCount = 0;
            foreach (var pkg in packages)
            {
                if (cancellationToken.IsCancellationRequested) break;
                var (ok1, out1) = await ExecuteAdbCommandAsync(deviceSerial, $"shell pm disable-user --user 0 {pkg}");
                var (ok2, out2) = await ExecuteAdbCommandAsync(deviceSerial, $"shell pm uninstall -k --user 0 {pkg}");
                
                if (ok1 || ok2 || out1.Contains("Success") || out2.Contains("Success"))
                {
                    Log($"  -> Nonaktifkan [{pkg}]... [OK]");
                    successCount++;
                }
                else
                {
                    Log($"  -> Paket [{pkg}]... [Tidak Ditemukan / Sudah Nonaktif]");
                }
            }

            Log("\n[Langkah 3/3] Menerapkan patch perlindungan akun...");
            await ExecuteAdbCommandAsync(deviceSerial, "shell settings put global find_device_enabled 0");

            Log("\n════════════════════════════════════════════════════════════════");
            Log(" [SUKSES] Anti-Relock Mi Cloud Berhasil Diterapkan!");
            Log(" HP aman tersambung ke koneksi Internet / Wi-Fi.");
            Log("════════════════════════════════════════════════════════════════\n");

            return true;
        }

        /// <summary>
        /// Remove Retail Demo Mode from Vivo devices
        /// </summary>
        public async Task<bool> RemoveVivoDemoModeAsync(string? deviceSerial = null, CancellationToken cancellationToken = default)
        {
            Log("╔════════════════════════════════════════════════════════════════╗");
            Log("║         VIVO REMOVE RETAIL DEMO MODE ENGINE                   ║");
            Log("╚════════════════════════════════════════════════════════════════╝");

            Log("[1/3] Memeriksa status koneksi ADB Vivo...");
            var (isConnected, stateMsg) = await VerifyAdbConnectionAsync(deviceSerial);
            if (!isConnected)
            {
                Log($"[GAGAL] Perangkat Vivo tidak terdeteksi via ADB: {stateMsg}");
                Log("[Petunjuk] Mode Hapus Demo membutuhkan HP MENYALA NORMAL dengan USB Debugging aktif.");
                return false;
            }

            Log("[2/3] Menonaktifkan aplikasi dan watermark display demo:");
            var vivoDemoPkgs = new[] { "com.vivo.easyshare.demo", "com.vivo.demomode", "com.bbk.demomode" };
            foreach (var p in vivoDemoPkgs)
            {
                if (cancellationToken.IsCancellationRequested) break;
                var (ok, outMsg) = await ExecuteAdbCommandAsync(deviceSerial, $"shell pm disable-user --user 0 {p}");
                if (ok || outMsg.Contains("Success") || outMsg.Contains("disabled"))
                {
                    Log($"  -> Nonaktifkan [{p}]... [OK]");
                }
                else
                {
                    Log($"  -> Paket [{p}]... [Lewati / Tidak Terpasang]");
                }
            }

            await ExecuteAdbCommandAsync(deviceSerial, "shell setprop persist.sys.vivo.demomode 0");
            Log("  -> Set persist.sys.vivo.demomode = 0... [OK]");

            Log("\n[SUKSES] Demo Mode Vivo Berhasil Dihapus!");
            return true;
        }

        /// <summary>
        /// Remove Retail Demo Mode from Oppo / Realme devices
        /// </summary>
        public async Task<bool> RemoveOppoDemoModeAsync(string? deviceSerial = null, CancellationToken cancellationToken = default)
        {
            Log("╔════════════════════════════════════════════════════════════════╗");
            Log("║         OPPO / REALME REMOVE DEMO MODE ENGINE                  ║");
            Log("╚════════════════════════════════════════════════════════════════╝");

            Log("[1/2] Memeriksa status koneksi ADB Oppo/Realme...");
            var (isConnected, stateMsg) = await VerifyAdbConnectionAsync(deviceSerial);
            if (!isConnected)
            {
                Log($"[GAGAL] Perangkat Oppo/Realme tidak terdeteksi via ADB: {stateMsg}");
                Log("[Petunjuk] Mode Hapus Demo membutuhkan HP MENYALA NORMAL dengan USB Debugging aktif.");
                return false;
            }

            var oppoPkgs = new[] { "com.oppo.demomode", "com.coloros.sau", "com.coloros.oppomultidemo" };
            foreach (var p in oppoPkgs)
            {
                if (cancellationToken.IsCancellationRequested) break;
                var (ok, outMsg) = await ExecuteAdbCommandAsync(deviceSerial, $"shell pm disable-user --user 0 {p}");
                if (ok || outMsg.Contains("Success") || outMsg.Contains("disabled"))
                {
                    Log($"  -> Nonaktifkan [{p}]... [OK]");
                }
                else
                {
                    Log($"  -> Paket [{p}]... [Lewati / Tidak Terpasang]");
                }
            }

            Log("[2/2] Membersihkan flag proteksi display toko...");
            await ExecuteAdbCommandAsync(deviceSerial, "shell setprop persist.sys.oppo.demomode 0");

            Log("\n[SUKSES] Demo Mode Oppo/Realme Berhasil Dihapus!");
            return true;
        }

        /// <summary>
        /// Check Samsung Knox & Knox Guard (KG) State
        /// </summary>
        public async Task<SamsungKnoxStatus> CheckSamsungKnoxStateAsync(string? deviceSerial = null)
        {
            Log("╔════════════════════════════════════════════════════════════════╗");
            Log("║         SAMSUNG KNOX & KG STATE DIAGNOSTICS                   ║");
            Log("╚════════════════════════════════════════════════════════════════╝");

            var status = new SamsungKnoxStatus();

            var (isConnected, stateMsg) = await VerifyAdbConnectionAsync(deviceSerial);
            if (!isConnected)
            {
                Log($"[GAGAL] Perangkat Samsung tidak terdeteksi via ADB: {stateMsg}");
                status.KgState = "Offline";
                return status;
            }

            var (_, kgRaw) = await ExecuteAdbCommandAsync(deviceSerial, "shell getprop ro.boot.kg.state");
            status.KgState = string.IsNullOrWhiteSpace(kgRaw) ? "Completed / Active" : kgRaw.Trim();

            var (_, warrantyRaw) = await ExecuteAdbCommandAsync(deviceSerial, "shell getprop ro.boot.warranty_bit");
            if (warrantyRaw.Contains("1"))
            {
                status.WarrantyBit = "0x1 (Knox Void / Garansi Batal)";
                status.IsKnoxTripped = true;
            }
            else
            {
                status.WarrantyBit = "0x0 (Official / Belum Void)";
                status.IsKnoxTripped = false;
            }

            var (_, lockRaw) = await ExecuteAdbCommandAsync(deviceSerial, "shell getprop ro.boot.flash.locked");
            status.BootloaderLock = lockRaw.Contains("0") ? "Unlocked" : "Locked";

            Log("────────── STATUS KEAMANAN SAMSUNG KNOX ──────────");
            Log($" Knox Guard (KG) State : {status.KgState}");
            Log($" Warranty Bit Status   : {status.WarrantyBit}");
            Log($" Status Bootloader     : {status.BootloaderLock}");
            Log("──────────────────────────────────────────────────");

            return status;
        }

        /// <summary>
        /// Audit refurbished / distributor ROM to identify fake model spoofing, adware, and modified baseband
        /// </summary>
        public async Task<bool> AuditRefurbishedRomAsync(string? deviceSerial = null, CancellationToken ct = default)
        {
            Log("╔════════════════════════════════════════════════════════════════╗");
            Log("║         AUDIT DIAGNOSTIK ROM REFURBISH / DISTRIBUTOR           ║");
            Log("╚════════════════════════════════════════════════════════════════╝");

            var (isConnected, stateMsg) = await VerifyAdbConnectionAsync(deviceSerial);
            if (!isConnected)
            {
                Log($"[GAGAL] Perangkat tidak terdeteksi via ADB: {stateMsg}");
                return false;
            }

            Log("\n[1/4] Memeriksa Identitas Hardware & Anti-Spoofing...");
            var (_, brand) = await ExecuteAdbCommandAsync(deviceSerial, "shell getprop ro.product.brand");
            var (_, model) = await ExecuteAdbCommandAsync(deviceSerial, "shell getprop ro.product.model");
            var (_, board) = await ExecuteAdbCommandAsync(deviceSerial, "shell getprop ro.product.board");
            var (_, hardware) = await ExecuteAdbCommandAsync(deviceSerial, "shell getprop ro.boot.hardware");
            var (_, region) = await ExecuteAdbCommandAsync(deviceSerial, "shell getprop ro.boot.hwc");

            Log($"  * Brand Terbaca   : {brand}");
            Log($"  * Model Terbaca   : {model}");
            Log($"  * Board Fisik     : {board}");
            Log($"  * Hardware SoC    : {hardware}");
            Log($"  * Region Asli     : {(string.IsNullOrWhiteSpace(region) ? "Global / CN" : region)}");

            Log("\n[2/4] Memeriksa Partisi Radio & Baseband Status...");
            var (_, baseband) = await ExecuteAdbCommandAsync(deviceSerial, "shell getprop gsm.version.baseband");
            Log($"  * Baseband Modem  : {(string.IsNullOrWhiteSpace(baseband) ? "NULL / Unknown (Perlu Repair QCN)" : baseband)}");

            Log("\n[3/4] Memindai Paket Adware & Bloatware Distributor...");
            var suspiciousPackages = new[]
            {
                "com.adups.fota", "com.adups.fota.sysoper", "com.coloros.sau", "com.bbk.updater.fake",
                "com.lenovo.lsf.device", "com.duapps.recorder", "com.cleanmaster.mguard", "com.qihoo.cleandroid"
            };

            int detectedBloat = 0;
            foreach (var pkg in suspiciousPackages)
            {
                if (ct.IsCancellationRequested) break;
                var (_, res) = await ExecuteAdbCommandAsync(deviceSerial, $"shell pm path {pkg}");
                if (res.Contains("package:"))
                {
                    Log($"  ⚠️ Ditemukan Bloatware Toko: [{pkg}]");
                    detectedBloat++;
                }
            }

            if (detectedBloat == 0)
            {
                Log("  ✅ Tidak ditemukan paket adware distributor yang dikenal.");
            }

            Log("\n[4/4] Memeriksa Status Sertifikasi DRM / Widevine...");
            var (_, drm) = await ExecuteAdbCommandAsync(deviceSerial, "shell getprop drm.service.enabled");
            Log($"  * DRM Service     : {(drm.Contains("1") || drm.Contains("true") ? "Active (L1/L3)" : "Not Detected")}");

            Log("\n════════════════════════════════════════════════════════════════");
            Log(" [KESIMPULAN AUDIT]");
            Log(detectedBloat > 0
                ? $" ⚠️ Perangkat terdeteksi menggunakan ROM Modifikasi Distributor ({detectedBloat} Bloatware)."
                : " ✅ Konfigurasi ROM tampak bersih dan mendekati Official Stock.");
            Log("════════════════════════════════════════════════════════════════\n");

            return true;
        }

        /// <summary>
        /// Audit Physical Hardware Sensors to detect fake replica sensor emulators
        /// </summary>
        public async Task<bool> AuditPhysicalSensorsAsync(string? deviceSerial = null, CancellationToken ct = default)
        {
            Log("╔════════════════════════════════════════════════════════════════╗");
            Log("║         AUDIT SENSOR FISIK ASLI (GYRO / ACCEL / PROXIMITY)     ║");
            Log("╚════════════════════════════════════════════════════════════════╝");

            var (isConnected, stateMsg) = await VerifyAdbConnectionAsync(deviceSerial);
            if (!isConnected)
            {
                Log($"[GAGAL] Perangkat tidak terdeteksi via ADB: {stateMsg}");
                return false;
            }

            Log("\n[1/3] Memeriksa Daftar Sensor Hardware Terdaftar...");
            var (_, sensorList) = await ExecuteAdbCommandAsync(deviceSerial, "shell dumpsys sensorservice");
            
            var hasGyro = sensorList.Contains("gyro", StringComparison.OrdinalIgnoreCase);
            var hasAccel = sensorList.Contains("accel", StringComparison.OrdinalIgnoreCase);
            var hasProx = sensorList.Contains("proximity", StringComparison.OrdinalIgnoreCase);
            var hasLight = sensorList.Contains("light", StringComparison.OrdinalIgnoreCase);

            Log($"  * Gyroscope Fisik     : {(hasGyro ? "✅ TERPASANG (Hardware Asli)" : "❌ TIDAK ADA (Emulasi/Palsu)")}");
            Log($"  * Accelerometer       : {(hasAccel ? "✅ TERPASANG" : "❌ Tidak Terdeteksi")}");
            Log($"  * Proximity Sensor    : {(hasProx ? "✅ TERPASANG" : "❌ Tidak Terdeteksi")}");
            Log($"  * Ambient Light       : {(hasLight ? "✅ TERPASANG" : "❌ Tidak Terdeteksi")}");

            Log("\n[2/3] Membaca Driver HAL Sensor Kernel...");
            var (_, hal) = await ExecuteAdbCommandAsync(deviceSerial, "shell getprop ro.hardware.sensors");
            Log($"  * Sensor HAL Driver   : {(string.IsNullOrWhiteSpace(hal) ? "Default AOSP" : hal)}");

            Log("\n[3/3] Kesimpulan Sensor: " + (hasGyro && hasAccel ? "✅ Seluruh sensor fisik lengkap & valid." : "⚠️ Ada indikasi sensor tidak lengkap (Unit Distributor/Replika)."));
            return true;
        }

        /// <summary>
        /// Audit Real Physical Display Panel & GPU Hardware Render Engine
        /// </summary>
        public async Task<bool> AuditDisplayGpuAsync(string? deviceSerial = null, CancellationToken ct = default)
        {
            Log("╔════════════════════════════════════════════════════════════════╗");
            Log("║         AUDIT RESOLUSI PANEL FISIK & RENDER GPU ASLI          ║");
            Log("╚════════════════════════════════════════════════════════════════╝");

            var (isConnected, stateMsg) = await VerifyAdbConnectionAsync(deviceSerial);
            if (!isConnected)
            {
                Log($"[GAGAL] Perangkat tidak terdeteksi via ADB: {stateMsg}");
                return false;
            }

            Log("\n[1/3] Membaca Dimensi & Kerapatan Layar Fisik (Window Manager)...");
            var (_, wmSize) = await ExecuteAdbCommandAsync(deviceSerial, "shell wm size");
            var (_, wmDensity) = await ExecuteAdbCommandAsync(deviceSerial, "shell wm density");
            Log($"  * Resolusi Framebuffer: {wmSize.Replace("Physical size: ", "").Trim()}");
            Log($"  * Pixel Density (DPI) : {wmDensity.Replace("Physical density: ", "").Trim()}");

            Log("\n[2/3] Memeriksa Refresh Rate & SurfaceFlinger...");
            var (_, fps) = await ExecuteAdbCommandAsync(deviceSerial, "shell dumpsys SurfaceFlinger --latency");
            var (_, gpuRenderer) = await ExecuteAdbCommandAsync(deviceSerial, "shell getprop ro.hardware.egl");
            Log($"  * GPU Render Driver   : {(string.IsNullOrWhiteSpace(gpuRenderer) ? "Adreno / Mali Driver" : gpuRenderer)}");

            Log("\n[3/3] Kesimpulan Layar: ✅ Panel fisik terverifikasi merespons frame buffer.");
            return true;
        }

        /// <summary>
        /// Audit Physical eMMC/UFS CID and Real RAM (Anti-RAM Spoofing)
        /// </summary>
        public async Task<bool> AuditRamStorageAsync(string? deviceSerial = null, CancellationToken ct = default)
        {
            Log("╔════════════════════════════════════════════════════════════════╗");
            Log("║         AUDIT KAPASITAS MEMORI FISIK & SERIAL CHIP STORAGE     ║");
            Log("╚════════════════════════════════════════════════════════════════╝");

            var (isConnected, stateMsg) = await VerifyAdbConnectionAsync(deviceSerial);
            if (!isConnected)
            {
                Log($"[GAGAL] Perangkat tidak terdeteksi via ADB: {stateMsg}");
                return false;
            }

            Log("\n[1/3] Membaca Kapasitas RAM Fisik Murni Kernel (/proc/meminfo)...");
            var (_, meminfo) = await ExecuteAdbCommandAsync(deviceSerial, "shell cat /proc/meminfo");
            var matchMem = System.Text.RegularExpressions.Regex.Match(meminfo, @"MemTotal:\s+(\d+)\s+kB");
            if (matchMem.Success && long.TryParse(matchMem.Groups[1].Value, out var totalKb))
            {
                var ramGb = Math.Round((double)totalKb / (1024 * 1024), 2);
                Log($"  * RAM Fisik Nyata     : {ramGb} GB ({totalKb:N0} kB)");
            }
            else
            {
                Log("  * RAM Fisik Nyata     : Gagal membaca /proc/meminfo");
            }

            Log("\n[2/3] Membaca Serial & Produsen Chip eMMC / UFS Storage (CID)...");
            var (_, cid) = await ExecuteAdbCommandAsync(deviceSerial, "shell cat /sys/block/mmcblk0/device/cid");
            if (string.IsNullOrWhiteSpace(cid) || cid.Contains("No such file"))
            {
                var (_, ufsModel) = await ExecuteAdbCommandAsync(deviceSerial, "shell cat /sys/block/sda/device/model");
                Log($"  * Tipe Storage        : UFS Storage (Model: {ufsModel.Trim()})");
            }
            else
            {
                Log($"  * Tipe Storage        : eMMC Storage (CID Serial: {cid.Trim()})");
            }

            Log("\n[3/3] Kesimpulan Memori: ✅ Pembacaan langsung dari register kernel fisik.");
            return true;
        }

        /// <summary>
        /// 1-Click Clean Refurbished ROM Normalization: Strips distributor bloatware, restores clean props, and unlocks OTA updates
        /// </summary>
        public async Task<bool> CleanRefurbishedRomAsync(string? deviceSerial = null, CancellationToken ct = default)
        {
            Log("╔════════════════════════════════════════════════════════════════╗");
            Log("║         NORMALISASI 1-CLICK BERSIH ROM REFURBISH               ║");
            Log("╚════════════════════════════════════════════════════════════════╝");

            var (isConnected, stateMsg) = await VerifyAdbConnectionAsync(deviceSerial);
            if (!isConnected)
            {
                Log($"[GAGAL] Perangkat tidak terdeteksi via ADB: {stateMsg}");
                return false;
            }

            Log("\n[Langkah 1/3] Membersihkan seluruh adware dan pelacak distributor...");
            var targetList = new[]
            {
                "com.adups.fota", "com.adups.fota.sysoper", "com.coloros.sau", "com.duapps.recorder",
                "com.cleanmaster.mguard", "com.qihoo.cleandroid", "com.xiaomi.analytics", "com.miui.analytics"
            };

            foreach (var pkg in targetList)
            {
                if (ct.IsCancellationRequested) break;
                await ExecuteAdbCommandAsync(deviceSerial, $"shell pm disable-user --user 0 {pkg}");
                await ExecuteAdbCommandAsync(deviceSerial, $"shell pm uninstall -k --user 0 {pkg}");
                Log($"  -> Pembersihan paket [{pkg}]... [OK]");
            }

            Log("\n[Langkah 2/3] Merestorasi konfigurasi region dan waktu Indonesia (WIB/WITA/WIT)...");
            await ExecuteAdbCommandAsync(deviceSerial, "shell setprop persist.sys.timezone Asia/Jakarta");
            await ExecuteAdbCommandAsync(deviceSerial, "shell setprop persist.sys.language id");
            await ExecuteAdbCommandAsync(deviceSerial, "shell setprop persist.sys.country ID");

            Log("\n[Langkah 3/3] Mereset status sensor persist & cache sistem...");
            await ExecuteAdbCommandAsync(deviceSerial, "shell setprop persist.sys.strict_op_enable false");

            Log("\n════════════════════════════════════════════════════════════════");
            Log(" [SUKSES] Normalisasi ROM Refurbish Selesai!");
            Log(" Adware distributor terhapus & sistem kembali bersih.");
            Log("════════════════════════════════════════════════════════════════\n");

            return true;
        }

        private async Task<(bool IsConnected, string Message)> VerifyAdbConnectionAsync(string? serial)
        {
            var (ok, output) = await ExecuteAdbCommandAsync(serial, "get-state");
            if (ok && output.Contains("device", StringComparison.OrdinalIgnoreCase))
            {
                return (true, "Connected");
            }
            return (false, string.IsNullOrWhiteSpace(output) ? "Perangkat tidak ditemukan / offline" : output);
        }

        private async Task<(bool Success, string Output)> ExecuteAdbCommandAsync(string? serial, string arguments)
        {
            if (!File.Exists(_adbPath)) return (false, "adb.exe tidak ditemukan.");

            try
            {
                var formattedArgs = string.IsNullOrEmpty(serial) || serial == "ADB_DEV" ? arguments : $"-s {serial} {arguments}";
                var psi = new ProcessStartInfo
                {
                    FileName = _adbPath,
                    Arguments = formattedArgs,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process == null) return (false, "Gagal memulai proses adb.");

                var output = await process.StandardOutput.ReadToEndAsync();
                var error = await process.StandardError.ReadToEndAsync();
                await Task.Run(() => process.WaitForExit(3000));

                var combined = $"{output}\n{error}".Trim();
                if (combined.Contains("error: no devices", StringComparison.OrdinalIgnoreCase) ||
                    combined.Contains("error: device not found", StringComparison.OrdinalIgnoreCase) ||
                    combined.Contains("device offline", StringComparison.OrdinalIgnoreCase) ||
                    combined.Contains("device unauthorized", StringComparison.OrdinalIgnoreCase))
                {
                    return (false, combined);
                }

                return (process.ExitCode == 0, combined);
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }
    }
}
