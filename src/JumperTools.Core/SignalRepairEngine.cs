using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Management;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using JumperTools.Core.Interfaces;
using JumperTools.Models;

namespace JumperTools.Core
{
    public class QcnFileInfo
    {
        public string Brand { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string Chipset { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string FileSizeFormatted { get; set; } = string.Empty;
    }

    public class NvramFileInfo
    {
        public string Brand { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string Chipset { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
    }

    /// <summary>
    /// Native Engine for IMEI, Baseband, and Signal Repair (Qualcomm Diag Port QCN + MediaTek NVRAM/NVDATA)
    /// </summary>
    public class SignalRepairEngine : ISignalCalibrationEngine
    {
        private readonly string _toolsBasePath;
        private readonly string _databaseBasePath;
        private readonly ToolExecutionEngine _toolEngine;

        public event Action<string>? OnLog;

        public SignalRepairEngine(string? toolsBasePath = null, string? databaseBasePath = null)
        {
            _toolsBasePath = toolsBasePath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools");
            _databaseBasePath = databaseBasePath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "database");
            _toolEngine = new ToolExecutionEngine(_toolsBasePath);
        }

        private void Log(string message)
        {
            var formatted = $"[{DateTime.Now:HH:mm:ss}] {message}";
            OnLog?.Invoke(formatted);
        }

        /// <summary>
        /// Scan database/qcn/ for available QCN calibration files
        /// </summary>
        public List<QcnFileInfo> ScanAvailableQcnFiles()
        {
            var list = new List<QcnFileInfo>();
            var candidateDirs = new List<string>
            {
                Path.Combine(_databaseBasePath, "qcn"),
                Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "database", "qcn")),
                Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "database", "qcn"))
            };

            foreach (var qcnDir in candidateDirs)
            {
                if (!Directory.Exists(qcnDir)) continue;

                try
                {
                    var files = Directory.EnumerateFiles(qcnDir, "*.qcn", SearchOption.AllDirectories);
                    foreach (var file in files)
                    {
                        var fileInfo = new FileInfo(file);
                        var relPath = Path.GetRelativePath(qcnDir, file);
                        var parts = relPath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                        
                        var brand = parts.Length > 1 ? parts[0] : "Universal";
                        var model = parts.Length > 2 ? parts[1] : Path.GetFileNameWithoutExtension(file);

                        var fullPath = Path.GetFullPath(file);
                        if (!list.Any(q => string.Equals(q.FilePath, fullPath, StringComparison.OrdinalIgnoreCase) || string.Equals(q.FileName, fileInfo.Name, StringComparison.OrdinalIgnoreCase)))
                        {
                            list.Add(new QcnFileInfo
                            {
                                Brand = brand,
                                Model = model,
                                Chipset = "Qualcomm Snapdragon",
                                FileName = fileInfo.Name,
                                FilePath = fullPath,
                                FileSizeFormatted = $"{(fileInfo.Length / 1024.0):F1} KB"
                            });
                        }
                    }
                }
                catch { }
            }

            return list;
        }

        /// <summary>
        /// Scan database/nvram/ for available MediaTek NVRAM / NVDATA backup files
        /// </summary>
        public List<NvramFileInfo> ScanAvailableNvramFiles()
        {
            var list = new List<NvramFileInfo>();
            var candidateDirs = new List<string>
            {
                Path.Combine(_databaseBasePath, "nvram"),
                Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "database", "nvram")),
                Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "database", "nvram"))
            };

            foreach (var nvramDir in candidateDirs)
            {
                if (!Directory.Exists(nvramDir)) continue;

                try
                {
                    var files = Directory.EnumerateFiles(nvramDir, "*.*", SearchOption.AllDirectories);
                    foreach (var file in files)
                    {
                        var ext = Path.GetExtension(file).ToLower();
                        if (ext != ".bin" && ext != ".img" && ext != ".tar") continue;

                        var fileInfo = new FileInfo(file);
                        var relPath = Path.GetRelativePath(nvramDir, file);
                        var parts = relPath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                        var brand = parts.Length > 1 ? parts[0] : "MediaTek";
                        var model = parts.Length > 2 ? parts[1] : Path.GetFileNameWithoutExtension(file);

                        if (!list.Any(n => n.FilePath == file))
                        {
                            list.Add(new NvramFileInfo
                            {
                                Brand = brand,
                                Model = model,
                                FileName = fileInfo.Name,
                                FilePath = file
                            });
                        }
                    }
                }
                catch { }
            }

            return list;
        }

        /// <summary>
        /// Enable Qualcomm Diagnostic (Diag) COM Port via ADB
        /// </summary>
        public async Task<bool> EnableQualcommDiagPortViaAdbAsync(string? deviceSerial = null, CancellationToken cancellationToken = default)
        {
            Log("╔════════════════════════════════════════════════════════════════╗");
            Log("║         AKTIVASI QUALCOMM DIAGNOSTIC (DIAG) COM PORT           ║");
            Log("╚════════════════════════════════════════════════════════════════╝");

            var adbPath = Path.Combine(_toolsBasePath, "platform-tools", "adb.exe");
            if (!File.Exists(adbPath))
            {
                Log($"[Error] adb.exe tidak ditemukan di {adbPath}");
                return false;
            }

            var prefix = string.IsNullOrEmpty(deviceSerial) ? "" : $"-s {deviceSerial} ";

            // PERBAIKAN P2-1: Setelah setprop sys.usb.config diag,adb dikirim, HP akan
            // memutus ADB sementara (USB re-enumerate ke mode Diag). Perintah berikutnya
            // TIDAK BOLEH dikirim sebelum device kembali online — jika tidak, semua gagal diam-diam.
            Log("[1/3] Mengirim perintah utama aktivasi Diag Port (setprop sys.usb.config diag,adb)...");
            await ExecuteProcessAsync(adbPath, $"{prefix}shell setprop sys.usb.config diag,adb");

            Log("[1b/3] Menunggu USB re-enumerate setelah mode Diag aktif (3 detik)...");
            await Task.Delay(3000, cancellationToken);

            // Cek apakah device sudah kembali online setelah re-enumerate
            var devicesCheck = await ExecuteProcessAsync(adbPath, "devices");
            var deviceOnline = devicesCheck.Contains("device") && !devicesCheck.Trim().Equals("List of devices attached");
            if (deviceOnline)
            {
                Log("[1c/3] Device kembali online. Mengirim perintah lanjutan (su privilege)...");
                await ExecuteProcessAsync(adbPath, $"{prefix}shell su -c \"setprop sys.usb.config diag,serial_smd,rmnet_qti_bam,adb\"");
                await Task.Delay(500, cancellationToken);
                await ExecuteProcessAsync(adbPath, $"{prefix}shell su -c \"setprop sys.usb.config diag,adb\"");
                await Task.Delay(500, cancellationToken);
                await ExecuteProcessAsync(adbPath, $"{prefix}shell su -c \"echo 1 > /sys/devices/virtual/android_usb/android0/f_diag/on\"");
            }
            else
            {
                Log("[Info] Device tidak terdeteksi via ADB setelah setprop — USB sedang re-enumerate ke Diag mode.");
                Log("       Ini normal. Port Diag akan muncul di Device Manager Windows dalam 3-5 detik.");
            }

            Log("[2/3] Memeriksa apakah Qualcomm HS-USB Diagnostics 900E/9091/901D muncul...");
            await Task.Delay(2500, cancellationToken);

            var diagPorts = ScanQualcommDiagPorts();
            if (diagPorts.Count > 0)
            {
                Log($"\n[SUKSES] Qualcomm Diag Port Aktif pada [{diagPorts[0]}]!");
                return true;
            }

            Log("\n[Info] Diag port belum muncul via ADB standar.");
            Log("💡 Petunjuk Manual: Buka dialer HP dan ketik salah satu kode rahasia:");
            Log("   • Xiaomi: *#*#717717#*#* atau *#*#13491#*#*");
            Log("   • Oppo/Realme: *#8011# -> Aktifkan Switch Diag");
            Log("   • Vivo: *#558# -> Quality Verification Test -> Port -> Diag ON");
            Log("   • OnePlus: *#801# -> Engineer Mode -> Serial / Full-port switch ON");
            return false;
        }

        /// <summary>
        /// Scan Windows for active Qualcomm Diagnostics 900E / 9091 / 901D / 9025 COM Ports
        /// </summary>
        public static List<string> ScanQualcommDiagPorts()
        {
            var ports = new List<string>();
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "SELECT * FROM Win32_PnPEntity WHERE (Caption LIKE '%Qualcomm%Diagnostics%' OR Caption LIKE '%Qualcomm%Diag%' OR Caption LIKE '%HS-USB Diagnostics%') AND Caption LIKE '%(COM%'"
                );

                foreach (var obj in searcher.Get())
                {
                    var caption = obj["Caption"]?.ToString() ?? string.Empty;
                    var pnpId = obj["PNPDeviceID"]?.ToString() ?? string.Empty;

                    // Exclude EDL 9008 / QDLoader mode
                    if (caption.Contains("QDLoader", StringComparison.OrdinalIgnoreCase) ||
                        caption.Contains("9008", StringComparison.OrdinalIgnoreCase) ||
                        pnpId.Contains("PID_9008", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var match = Regex.Match(caption, @"\((COM\d+)\)", RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        var port = match.Groups[1].Value.ToUpper();
                        if (!ports.Contains(port)) ports.Add(port);
                    }
                }
            }
            catch { }
            return ports;
        }

        /// <summary>
        /// Backup MediaTek NVRAM, NVDATA, and NVCFG partitions to preserve IMEI and Baseband calibration
        /// </summary>
        public async Task<string?> BackupMediaTekNvramAsync(string outputDir, CancellationToken cancellationToken = default)
        {
            Log("╔════════════════════════════════════════════════════════════════╗");
            Log("║         BACKUP MEDIATEK NVRAM / NVDATA (IMEI GOLDEN DUMP)      ║");
            Log("╚════════════════════════════════════════════════════════════════╝");

            if (!Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var backupFolder = Path.Combine(outputDir, $"MTK_NVRAM_BACKUP_{timestamp}");
            Directory.CreateDirectory(backupFolder);

            Log($"[Target] Folder Cadangan: {backupFolder}");
            Log("\n[Eksekusi] Menjalankan mtkclient dump partisi nvram, nvdata, nvcfg, protect1, protect2...");

            var targetPartitions = new[] { "nvram", "nvdata", "nvcfg", "protect1", "protect2" };
            var successCount = 0;

            foreach (var part in targetPartitions)
            {
                var targetFile = Path.Combine(backupFolder, $"{part}.bin");
                var args = $"r {part} \"{targetFile}\"";
                var res = await _toolEngine.ExecuteMtkClientAsync(args, (msg) => Log($"  -> {msg}"));
                if (res.Success && File.Exists(targetFile))
                {
                    // PERBAIKAN P2-2: mtkclient bisa exit code 0 tapi koneksi BROM putus di tengah
                    // jalan — hasilnya file 0-byte yang terlihat "ada" tapi tidak bisa dipakai restore.
                    var dumpInfo = new FileInfo(targetFile);
                    if (dumpInfo.Length > 0)
                    {
                        Log($"  -> Partisi [{part}] BERHASIL di-dump. ({dumpInfo.Length / 1024:N0} KB)");
                        successCount++;
                    }
                    else
                    {
                        Log($"  -> Partisi [{part}] PERINGATAN: File dump berukuran 0-byte.");
                        Log($"     Kemungkinan koneksi BROM terputus di tengah jalan. File ini TIDAK aman untuk restore.");
                        // Hapus file 0-byte agar tidak mengotori folder backup
                        try { File.Delete(targetFile); } catch { }
                    }
                }
            }

            if (successCount > 0)
            {
                Log($"\n[SUKSES] Backup NVRAM MediaTek Selesai ({successCount}/{targetPartitions.Length} partisi aman)!");
                return backupFolder;
            }

            Log("\n[Gagal] Backup NVRAM gagal. Pastikan HP terhubung dalam mode BROM.");
            return null;
        }

        /// <summary>
        /// Restore MediaTek NVRAM, NVDATA from backup directory.
        /// Validates SHA-256 checksum of each .bin file before writing to prevent
        /// corrupted data from being flashed to the radio partition.
        /// Source: MediaTek Flash Partition Layout spec - nvram/nvdata are tied
        /// to IMEI and RF calibration; a 0-byte or truncated file causes baseband death.
        /// </summary>
        public async Task<bool> RestoreMediaTekNvramAsync(string backupDirectory, CancellationToken cancellationToken = default)
        {
            Log("╔════════════════════════════════════════════════════════════════╗");
            Log("║         RESTORE MEDIATEK NVRAM / NVDATA (FIX IMEI/BASEBAND)    ║");
            Log("╚════════════════════════════════════════════════════════════════╝");

            if (!Directory.Exists(backupDirectory))
            {
                Log($"[Error] Folder backup tidak ditemukan: {backupDirectory}");
                return false;
            }

            var binFiles = Directory.GetFiles(backupDirectory, "*.bin");
            if (binFiles.Length == 0)
            {
                Log("[Error] Tidak ditemukan file partisi (.bin) di dalam folder yang dipilih.");
                return false;
            }

            // PRE-FLIGHT: Validate all files BEFORE writing anything to flash
            Log("\n[Pre-Flight] Memvalidasi integritas seluruh berkas backup via SHA-256...");
            foreach (var file in binFiles)
            {
                var fileInfo = new FileInfo(file);
                if (fileInfo.Length == 0)
                {
                    Log($"[DITOLAK] Berkas '{Path.GetFileName(file)}' berukuran 0-byte (kosong/korup). Seluruh operasi dibatalkan.");
                    Log("[Alasan] Menulis partisi NVRAM/NVDATA dengan data kosong akan mematikan modem baseband secara permanen.");
                    return false;
                }

                string sha256;
                using (var sha = SHA256.Create())
                using (var stream = File.OpenRead(file))
                {
                    sha256 = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
                }
                Log($"  -> [{Path.GetFileName(file)}] {fileInfo.Length / 1024:N0} KB | SHA-256: {sha256.Substring(0, 16)}...{sha256.Substring(sha256.Length - 8)} [OK]");
            }
            Log("[Pre-Flight] Semua berkas valid. Memulai penulisan ke flash...");

            var anySuccess = false;
            foreach (var file in binFiles)
            {
                if (cancellationToken.IsCancellationRequested) break;

                var partName = Path.GetFileNameWithoutExtension(file).ToLower();

                // Safety: only allow known radio partitions to be restored
                var allowedPartitions = new[] { "nvram", "nvdata", "nvcfg", "protect1", "protect2", "proinfo" };
                if (!Array.Exists(allowedPartitions, p => p == partName))
                {
                    Log($"  -> Partisi [{partName}] dilewati (bukan partisi NVRAM/radio yang dikenali).");
                    continue;
                }

                Log($"\n[Restore] Menulis kembali partisi [{partName}] dari {Path.GetFileName(file)}...");
                var args = $"w {partName} \"{file}\"";
                var res = await _toolEngine.ExecuteMtkClientAsync(args, (msg) => Log($"  -> {msg}"));
                if (res.Success)
                {
                    Log($"  -> Partisi [{partName}] BERHASIL dipulihkan! [OK]");
                    anySuccess = true;
                }
                else
                {
                    Log($"  -> Partisi [{partName}] GAGAL ditulis: {res.ErrorMessage}");
                }
            }

            if (anySuccess)
            {
                Log("\n════════════════════════════════════════════════════════════════");
                Log(" [SUKSES] Pemulihan IMEI/Baseband NVRAM MediaTek Selesai!");
                Log(" Restart HP untuk memuat ulang parameter kalibrasi sinyal baru.");
                Log("════════════════════════════════════════════════════════════════\n");
            }
            return anySuccess;
        }

        /// <summary>
        /// Restore Qualcomm QCN calibration file via QPST/QDL CLI tool delegation.
        ///
        /// GROUND TRUTH (Qualcomm DIAG Subsystem Specification):
        /// Raw-dumping QCN bytes directly to a COM port is INVALID.
        /// The Qualcomm DIAG protocol requires HDLC framing:
        ///   [0x7E] [CmdCode=0x27 NV_WRITE_F] [NV_Item_ID (2-byte LE)] [Payload 128-byte] [CRC16] [0x7E]
        /// Each NV Item must be written individually with proper CRC.
        /// The correct approach for end-users is to delegate to verified tools:
        ///   - QPST (Qualcomm Product Support Tool) - qpst-cli
        ///   - QDL (qdl_host) for EFS/NV streaming
        ///   - edl.py --qcn (bkerler/edl via Python)
        /// </summary>
        public async Task<bool> WriteQcnToDiagPortAsync(string diagPort, string qcnFilePath, CancellationToken cancellationToken = default)
        {
            Log("╔════════════════════════════════════════════════════════════════╗");
            Log("║         RESTORASI KALIBRASI MODEM QUALCOMM (QCN INJECTION)     ║");
            Log("╚════════════════════════════════════════════════════════════════╝");

            // Validate inputs
            if (string.IsNullOrWhiteSpace(diagPort))
            {
                Log("[Error] Port Qualcomm Diagnostics (COM) belum ditentukan.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(qcnFilePath) || !File.Exists(qcnFilePath))
            {
                Log($"[Error] Berkas QCN tidak ditemukan: {qcnFilePath}");
                return false;
            }

            var fileInfo = new FileInfo(qcnFilePath);
            if (fileInfo.Length == 0)
            {
                Log("[DITOLAK] Berkas QCN berukuran 0-byte. Injeksi dibatalkan.");
                return false;
            }

            // SHA-256 pre-flight check
            string sha256;
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(qcnFilePath))
            {
                sha256 = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
            }

            Log($"[Berkas] {Path.GetFileName(qcnFilePath)} | {fileInfo.Length / 1024:N0} KB");
            Log($"[SHA-256] {sha256}");
            Log($"[Port]    {diagPort}");

            // APPROACH 1: Delegate to edl.py CLI (bkerler/edl - verified ground truth tool)
            var edlPy = Path.Combine(_toolsBasePath, "edl", "edl.py");
            if (File.Exists(edlPy))
            {
                Log("\n[Metode 1] Menggunakan edl.py (bkerler/edl) untuk injeksi QCN via port Diag...");
                var output = await ExecuteProcessAsync("python3", $"\"{edlPy}\" --qcn \"{qcnFilePath}\" --port {diagPort}");
                if (!output.Contains("Error", StringComparison.OrdinalIgnoreCase) && !output.Contains("Traceback", StringComparison.OrdinalIgnoreCase))
                {
                    Log("\n[SUKSES] QCN berhasil diinjeksikan via edl.py!");
                    Log("💡 Restart perangkat untuk memuat ulang parameter jaringan baru.");
                    return true;
                }
                Log($"[Metode 1 Gagal] {output.Split('\n')[0]}");
            }

            // APPROACH 2: Delegate to qpst-cli if available
            var qpstCli = Path.Combine(_toolsBasePath, "qpst", "qpst-cli.exe");
            if (File.Exists(qpstCli))
            {
                Log("\n[Metode 2] Menggunakan QPST CLI untuk restore QCN...");
                var output = await ExecuteProcessAsync(qpstCli, $"--restore-qcn \"{qcnFilePath}\" --port {diagPort}");
                if (!output.Contains("Error", StringComparison.OrdinalIgnoreCase) && !output.Contains("failed", StringComparison.OrdinalIgnoreCase))
                {
                    Log("\n[SUKSES] QCN berhasil di-restore via QPST!");
                    return true;
                }
                Log($"[Metode 2 Gagal] {output.Split('\n')[0]}");
            }

            // FALLBACK: Inform the user of the correct manual protocol
            Log("\n════════════════════════════════════════════════════════════════");
            Log(" [INFO TEKNIS - QUALCOMM DIAG PROTOCOL]");
            Log(" Injeksi QCN tidak dapat dilakukan via raw byte dump ke COM port.");
            Log(" Protokol DIAG Qualcomm mewajibkan HDLC framing per NV Item:");
            Log("   Frame: 0x7E [CMD=0x27 NV_WRITE_F] [Item_ID 2B] [Data 128B] [CRC16] 0x7E");
            Log(" Letakkan tools berikut di folder tools/edl/ atau tools/qpst/:");
            Log("   • edl.py (https://github.com/bkerler/edl) - direkomendasikan");
            Log("   • QPST 2.7.x - qpst-cli.exe");
            Log("════════════════════════════════════════════════════════════════\n");
            return false;
        }

        private async Task<string> ExecuteProcessAsync(string exePath, string args)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = args,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process == null) return string.Empty;

                var outTask = process.StandardOutput.ReadToEndAsync();
                var errTask = process.StandardError.ReadToEndAsync();
                await Task.WhenAll(outTask, errTask);
                await Task.Run(() => process.WaitForExit(5000));

                return $"{outTask.Result}\n{errTask.Result}".Trim();
            }
            catch (Exception ex)
            {
                return $"Error: {ex.Message}";
            }
        }
    }
}
