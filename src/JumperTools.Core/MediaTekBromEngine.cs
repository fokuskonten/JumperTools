using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Management;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using JumperTools.Core.Interfaces;
using JumperTools.Models;

namespace JumperTools.Core
{
    public class MtkPortInfo
    {
        public string PortName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string HardwareId { get; set; } = string.Empty;
        public bool IsBromMode { get; set; }
    }

    public class MtkDeviceInfo
    {
        public string PortName { get; set; } = string.Empty;
        public ushort HwCode { get; set; }
        public string ChipsetName { get; set; } = string.Empty;
        public ushort HwSubCode { get; set; }
        public ushort HwVersion { get; set; }
        public ushort SwVersion { get; set; }
        public byte BlVersion { get; set; }
        public bool AuthBypassed { get; set; }
        public List<string> Partitions { get; set; } = new();
    }

    /// <summary>
    /// Core Native Engine for MediaTek (MTK) BROM & Preloader Protocol, Auth Bypass, and Partition Operations
    /// </summary>
    public class MediaTekBromEngine : IHardwareProtocolEngine
    {
        public string ProtocolName => "MediaTek BROM / Preloader";
        public DeviceMode SupportedMode => DeviceMode.BROM;

        public event Action<string>? OnLog;
        public event Action<string>? OnUserActionRequired;
        
        private readonly ToolExecutionEngine _toolEngine;

        public string? CustomDaFilePath { get; set; }
        public string? CustomAuthFilePath { get; set; }

        public MediaTekBromEngine(ToolExecutionEngine toolEngine)
        {
            _toolEngine = toolEngine;
        }

        public Task<bool> ProbeDeviceAsync(string? deviceIdOrPort, CancellationToken cancellationToken = default)
        {
            var ports = ScanMtkPorts();
            if (!string.IsNullOrEmpty(deviceIdOrPort))
                return Task.FromResult(ports.Any(p => p.PortName.Equals(deviceIdOrPort, StringComparison.OrdinalIgnoreCase)));
            return Task.FromResult(ports.Count > 0);
        }

        public async Task<bool> RebootDeviceAsync(string? deviceIdOrPort, string targetMode = "system", CancellationToken cancellationToken = default)
        {
            var res = await _toolEngine.ExecuteMtkClientAsync("reset", Log, 10000);
            return res.Success;
        }

        public string PrepareMtkArgs(string baseArgs)
        {
            var args = baseArgs;
            // Di standar industri GSM (UnlockTool/Pandora), flag --crash wajib disertakan
            // agar Preloader pada Oppo/Realme/Xiaomi modern otomatis di-glitch jatuh ke BROM murni
            if (!args.Contains("--crash") && !args.StartsWith("reset"))
            {
                args = "--crash " + args;
            }

            if (!string.IsNullOrEmpty(CustomDaFilePath) && File.Exists(CustomDaFilePath))
            {
                args = $"--da \"{CustomDaFilePath}\" " + args;
            }
            if (!string.IsNullOrEmpty(CustomAuthFilePath) && File.Exists(CustomAuthFilePath))
            {
                args = $"--auth \"{CustomAuthFilePath}\" " + args;
            }
            return args;
        }

        public static List<string> ScanAvailableDaFiles(string? databasePath = null)
        {
            var list = new List<string>();
            var baseDir = databasePath ?? AppPathResolver.DaDirectory;
            if (!Directory.Exists(baseDir)) return list;

            try
            {
                var files = Directory.EnumerateFiles(baseDir, "*.bin", SearchOption.AllDirectories);
                list.AddRange(files);
            }
            catch { }

            return list;
        }

        private static readonly Dictionary<ushort, string> ChipsetDatabase = new()
        {
            { 0x6580, "MT6580 (Quad-Core 32-bit)" },
            { 0x6735, "MT6735 (Quad-Core 64-bit)" },
            { 0x6737, "MT6737 (Quad-Core 64-bit)" },
            { 0x6739, "MT6739 (Quad-Core Entry)" },
            { 0x6761, "Helio A22 / A20 (MT6761)" },
            { 0x6762, "Helio P22 / G25 (MT6762)" },
            { 0x6765, "Helio P35 / G35 (MT6765)" },
            { 0x6768, "Helio G70 / G80 / G85 (MT6768)" },
            { 0x6769, "Helio G80 / G85 (MT6769)" },
            { 0x6771, "Helio P60 / P70 (MT6771)" },
            { 0x6779, "Helio P90 (MT6779)" },
            { 0x6781, "Helio G96 / G99 (MT6781)" },
            { 0x6785, "Helio G90 / G90T / G95 (MT6785)" },
            { 0x6833, "Dimensity 700 / 810 (MT6833 5G)" },
            { 0x6853, "Dimensity 720 / 800U (MT6853 5G)" },
            { 0x6877, "Dimensity 900 / 1080 (MT6877 5G)" },
            { 0x6893, "Dimensity 1200 / 1300 (MT6893 5G)" },
            { 0x6983, "Dimensity 9000 / 9200 (MT6983 5G)" }
        };

        public string GetChipsetName(ushort hwCode) => ChipsetDatabase.TryGetValue(hwCode, out var name) ? name : $"Unknown MTK (0x{hwCode:X4})";

        private void Log(string message)
        {
            var formatted = $"[{DateTime.Now:HH:mm:ss}] {message}";
            OnLog?.Invoke(formatted);
        }

        /// <summary>
        /// Scan Windows PnP for active MediaTek USB Ports (VID: 0x0E8D)
        /// </summary>
        public static List<MtkPortInfo> ScanMtkPorts()
        {
            var ports = new List<MtkPortInfo>();

            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "SELECT * FROM Win32_PnPEntity WHERE PNPDeviceID LIKE '%VID_0E8D%' OR Caption LIKE '%MediaTek%' OR Caption LIKE '%MTK%'"
                );

                foreach (var obj in searcher.Get())
                {
                    var caption = obj["Caption"]?.ToString() ?? string.Empty;
                    var pnpId = obj["PNPDeviceID"]?.ToString() ?? string.Empty;

                    var match = Regex.Match(caption, @"\((COM\d+)\)", RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        var portName = match.Groups[1].Value.ToUpper();
                        if (!ports.Any(p => p.PortName.Equals(portName, StringComparison.OrdinalIgnoreCase)))
                        {
                            var isBrom = pnpId.Contains("PID_0003", StringComparison.OrdinalIgnoreCase) || caption.Contains("MediaTek USB Port", StringComparison.OrdinalIgnoreCase);
                            ports.Add(new MtkPortInfo
                            {
                                PortName = portName,
                                Description = caption,
                                HardwareId = pnpId,
                                IsBromMode = isBrom
                            });
                        }
                    }
                }
            }
            catch { }

            return ports;
        }

        /// <summary>
        /// Perform initial BROM handshake (0xA0 0x0A 0x50 0x05)
        /// </summary>
        public async Task<bool> SendBromHandshakeAsync(SerialPort port, int timeoutMs = 3000)
        {
            return await Task.Run(() =>
            {
                try
                {
                    port.DiscardInBuffer();
                    port.DiscardOutBuffer();

                    var handshake = new byte[] { 0xA0, 0x0A, 0x50, 0x05 };
                    port.Write(handshake, 0, handshake.Length);

                    var startTime = DateTime.Now;
                    var expected = new byte[] { 0x5F, 0xF5, 0xAF, 0xFA };
                    var received = new List<byte>();

                    while ((DateTime.Now - startTime).TotalMilliseconds < timeoutMs)
                    {
                        if (port.BytesToRead > 0)
                        {
                            var b = (byte)port.ReadByte();
                            received.Add(b);

                            if (received.Count >= 4)
                            {
                                var last4 = received.Skip(received.Count - 4).ToArray();
                                if (last4.SequenceEqual(expected))
                                {
                                    return true;
                                }
                            }
                        }
                        Thread.Sleep(10);
                    }
                }
                catch { }

                return false;
            });
        }

        /// <summary>
        /// Read Hardware Code from BROM (Command 0xD8)
        /// </summary>
        public async Task<MtkDeviceInfo?> ReadDeviceInfoAsync(string portName)
        {
            Log($"[MTK] Membuka port {portName} pada baudrate 115200...");

            return await Task.Run(async () =>
            {
                SerialPort? port = null;
                try
                {
                    port = new SerialPort(portName, 115200, Parity.None, 8, StopBits.One)
                    {
                        ReadTimeout = 3000,
                        WriteTimeout = 3000,
                        DtrEnable = true,
                        RtsEnable = true
                    };
                    port.Open();

                    Log("[MTK] Mengirim urutan handshake BROM (0xA0 0x0A 0x50 0x05)...");
                    var ok = await SendBromHandshakeAsync(port);
                    if (!ok)
                    {
                        Log("[MTK Info] Respon raw BROM belum diterima (HP mungkin dalam mode Preloader atau butuh Filter LibUSB).");
                    }

                    var info = new MtkDeviceInfo { PortName = portName };

                    // Read HW Code: Send 0xD8
                    port.Write(new byte[] { 0xD8 }, 0, 1);
                    var b1 = (byte)port.ReadByte();
                    var b2 = (byte)port.ReadByte();
                    info.HwCode = (ushort)((b1 << 8) | b2);

                    // Resolve Chipset Name
                    if (ChipsetDatabase.TryGetValue(info.HwCode, out var name))
                    {
                        info.ChipsetName = name;
                    }
                    else
                    {
                        info.ChipsetName = $"MT{info.HwCode:X4} (Chipset MTK)";
                    }

                    Log("────────── INFO CHIPSET MEDIATEK ──────────");
                    Log($" Port COM      : {info.PortName}");
                    Log($" Hardware Code : 0x{info.HwCode:X4}");
                    Log($" Chipset Model : {info.ChipsetName}");
                    Log("───────────────────────────────────────────");

                    return info;
                }
                catch (Exception ex)
                {
                    Log($"[MTK Error] Gagal membaca data BROM: {ex.Message}");
                    return null;
                }
                finally
                {
                    if (port != null && port.IsOpen)
                    {
                        try { port.Close(); } catch { }
                    }
                }
            });
        }

        /// <summary>
        private void EnsurePreflightDrivers()
        {
            var usbdkOk = DriverAssistantEngine.CheckUsbDkInstalled();
            if (!usbdkOk)
            {
                Log("[Pre-Flight Check] ⚠️ Filter UsbDk belum aktif di Windows Anda.");
                Log("[Pre-Flight Check] 💡 Jika HP terputus/masuk logo, buka tab 'Driver Assistant' dan klik 'Pasang Semua Driver Bawaan'.");
            }
        }

        public async Task<bool> EraseFrpAsync(string portName, CancellationToken cancellationToken = default)
        {
            Log("╔════════════════════════════════════════════════════════════════╗");
            Log("║          MEDIATEK (MTK) UNIVERSAL ONE-CLICK FRP BYPASS         ║");
            Log("╚════════════════════════════════════════════════════════════════╝");

            EnsurePreflightDrivers();

            Log("\n[Langkah 1/2] Membuka Port Listener mtkclient (Standby)...");
            Log("💡 [PETUNJUK TEKNISI]: Pastikan HP Mati Total -> Tahan tombol [Vol Up + Vol Down] -> Colokkan kabel USB ke PC.");
            OnUserActionRequired?.Invoke("Matikan HP. Tahan tombol VOLUME ATAS + BAWAH, lalu colokkan kabel USB ke PC.");

            var args = "e frp"; // mtkclient command to erase frp partition
            var result = await _toolEngine.ExecuteMtkClientAsync(PrepareMtkArgs(args), Log);

            if (result.Success)
            {
                Log("\n════════════════════════════════════════════════════════════════");
                Log(" [SUKSES] Bypass FRP MediaTek Berhasil 100%!");
                Log(" Cabut kabel USB dan nyalakan HP secara normal.");
                Log("════════════════════════════════════════════════════════════════\n");
                return true;
            }
            else
            {
                Log($"\n[Gagal] Operasi mtkclient gagal: {result.ErrorMessage}");
                Log("\n💡 [PANDUAN TEKNISI UNTUK CHIPSET MEDIATEK 2024/2025]");
                Log("────────────────────────────────────────────────────────────────");
                Log(" Jika HP menolak koneksi / stuck di Preloader:");
                Log(" 1. Gunakan tombol '⚡ CRASH PRELOADER (AUTO BROM)' di tab MediaTek.");
                Log(" 2. Untuk Dimensity (MT6833/MT6877/MT6878): Buka casing belakang & short");
                Log("    titik Test Point (CLK/DAT0 ke Ground) saat mencolokkan kabel USB.");
                Log("────────────────────────────────────────────────────────────────");
                return false;
            }
        }

        /// <summary>
        /// Execute Safe Format / Factory Reset (Wipe Userdata, Metadata, Misc)
        /// </summary>
        public async Task<bool> FormatUserdataAsync(string portName, CancellationToken cancellationToken = default)
        {
            Log("╔════════════════════════════════════════════════════════════════╗");
            Log("║         MEDIATEK FACTORY RESET / FORMAT USERDATA ENGINE        ║");
            Log("╚════════════════════════════════════════════════════════════════╝");

            Log("\n[Langkah 1/2] Membuka Port Listener mtkclient (Standby)...");
            Log("💡 [PETUNJUK TEKNISI]: Pastikan HP Mati Total -> Tahan tombol [Vol Up + Vol Down] -> Colokkan kabel USB ke PC.");
            OnUserActionRequired?.Invoke("Matikan HP. Tahan tombol VOLUME ATAS + BAWAH, lalu colokkan kabel USB ke PC.");

            var args = "e userdata,metadata"; // erase userdata and metadata partitions
            var result = await _toolEngine.ExecuteMtkClientAsync(PrepareMtkArgs(args), Log);

            if (result.Success)
            {
                Log("\n════════════════════════════════════════════════════════════════");
                Log(" [SUKSES] Factory Reset / Safe Format Selesai!");
                Log(" Kunci pola / PIN dan seluruh data pengguna telah terhapus.");
                Log("════════════════════════════════════════════════════════════════\n");
                return true;
            }
            else
            {
                Log($"\n[Gagal] Operasi mtkclient gagal: {result.ErrorMessage}");
                Log("\n💡 [PANDUAN TEKNISI UNTUK CHIPSET MEDIATEK 2024/2025]");
                Log("────────────────────────────────────────────────────────────────");
                Log(" Jika Preloader mengunci format userdata:");
                Log(" 1. Klik '⚡ CRASH PRELOADER (AUTO BROM)' terlebih dahulu.");
                Log(" 2. Jika tetap ditolak, gunakan mode Recovery fisik untuk 'Wipe Data'.");
                Log("────────────────────────────────────────────────────────────────");
                return false;
            }
        }

        /// <summary>
        /// Instant Bootloader Unlock (UBL Instant) via seccfg modification
        /// <summary>
        /// Instant Bootloader Unlock (UBL Instant) via seccfg modification
        /// </summary>
        public async Task<bool> UnlockBootloaderAsync(string portName, CancellationToken cancellationToken = default)
        {
            Log("╔════════════════════════════════════════════════════════════════╗");
            Log("║          MEDIATEK INSTANT BOOTLOADER UNLOCK (UBL)              ║");
            Log("╚════════════════════════════════════════════════════════════════╝");

            Log("\n[Langkah 1/2] Menunggu HP dalam mode BROM...");
            OnUserActionRequired?.Invoke("Matikan HP. Tahan tombol VOLUME ATAS + BAWAH, lalu colokkan kabel USB ke PC.");

            var args = "da seccfg unlock";
            var result = await _toolEngine.ExecuteMtkClientAsync(PrepareMtkArgs(args), Log);

            if (result.Success)
            {
                Log("\n════════════════════════════════════════════════════════════════");
                Log(" [SUKSES] Instant Bootloader Unlock (UBL) Berhasil!");
                Log(" Perangkat kini dalam status UNLOCKED.");
                Log("════════════════════════════════════════════════════════════════\n");
                return true;
            }
            else
            {
                Log($"\n[Gagal] Unlock bootloader gagal: {result.ErrorMessage}");
                return false;
            }
        }

        /// <summary>
        /// Relock Bootloader via seccfg
        /// </summary>
        public async Task<bool> RelockBootloaderAsync(string portName, CancellationToken cancellationToken = default)
        {
            Log("╔════════════════════════════════════════════════════════════════╗");
            Log("║          MEDIATEK RELOCK BOOTLOADER ENGINE                     ║");
            Log("╚════════════════════════════════════════════════════════════════╝");

            Log("\n[Langkah 1/2] Menunggu HP dalam mode BROM...");
            OnUserActionRequired?.Invoke("Matikan HP. Tahan tombol VOLUME ATAS + BAWAH, lalu colokkan kabel USB ke PC.");

            var args = "da seccfg lock";
            var result = await _toolEngine.ExecuteMtkClientAsync(PrepareMtkArgs(args), Log);

            if (result.Success)
            {
                Log("\n════════════════════════════════════════════════════════════════");
                Log(" [SUKSES] Bootloader MediaTek Berhasil Di-relock!");
                Log("════════════════════════════════════════════════════════════════\n");
                return true;
            }
            else
            {
                Log($"\n[Gagal] Relock bootloader gagal: {result.ErrorMessage}");
                return false;
            }
        }

        /// <summary>
        /// Read and display flash memory partition table (GPT)
        /// </summary>
        public async Task<List<string>> ReadGptTableAsync(string portName, CancellationToken cancellationToken = default)
        {
            Log("╔════════════════════════════════════════════════════════════════╗");
            Log("║          MEDIATEK READ GPT PARTITION TABLE                    ║");
            Log("╚════════════════════════════════════════════════════════════════╝");

            var partitions = new List<string>();
            Log("[MTK] Membaca tabel partisi GUID (GPT) via mtkclient...");

            var result = await _toolEngine.ExecuteMtkClientAsync(PrepareMtkArgs("printgpt"), (line) =>
            {
                Log(line);
                var match = Regex.Match(line, @"^\s*\d+:\s*([a-zA-Z0-9_\-]+)", RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    var name = match.Groups[1].Value;
                    if (!partitions.Contains(name)) partitions.Add(name);
                }
            });

            if (partitions.Count > 0)
            {
                Log($"\n[Total] {partitions.Count} partisi teridentifikasi dari memori flash.");
            }
            return partitions;
        }

        /// <summary>
        /// Backup NVRAM and NVDATA partitions to preserve IMEI and Baseband
        /// </summary>
        public async Task<bool> BackupNvramAsync(string portName, string backupDirectory, CancellationToken cancellationToken = default)
        {
            Log("╔════════════════════════════════════════════════════════════════╗");
            Log("║          MEDIATEK BACKUP NVRAM / IMEI SECURITY                 ║");
            Log("╚════════════════════════════════════════════════════════════════╝");

            if (!Directory.Exists(backupDirectory))
            {
                Directory.CreateDirectory(backupDirectory);
            }

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var targetFolder = Path.Combine(backupDirectory, $"MTK_NVRAM_{timestamp}");
            Directory.CreateDirectory(targetFolder);

            var nvramFile = Path.Combine(targetFolder, "nvram.bin");
            var nvdataFile = Path.Combine(targetFolder, "nvdata.bin");
            var nvcfgFile = Path.Combine(targetFolder, "nvcfg.bin");

            Log($"[1/3] Membaca partisi [nvram] -> {Path.GetFileName(nvramFile)}...");
            var res1 = await _toolEngine.ExecuteMtkClientAsync(PrepareMtkArgs($"r nvram \"{nvramFile}\""), Log);

            Log($"[2/3] Membaca partisi [nvdata] -> {Path.GetFileName(nvdataFile)}...");
            var res2 = await _toolEngine.ExecuteMtkClientAsync(PrepareMtkArgs($"r nvdata \"{nvdataFile}\""), Log);

            Log($"[3/3] Membaca partisi [nvcfg]  -> {Path.GetFileName(nvcfgFile)}...");
            var res3 = await _toolEngine.ExecuteMtkClientAsync(PrepareMtkArgs($"r nvcfg \"{nvcfgFile}\""), Log);

            if ((res1.Success && File.Exists(nvramFile)) || (res2.Success && File.Exists(nvdataFile)))
            {
                Log($"\n════════════════════════════════════════════════════════════════");
                Log($" [SUKSES] Backup partisi IMEI MediaTek tersimpan di:\n {targetFolder}");
                Log("════════════════════════════════════════════════════════════════\n");
                return true;
            }

            Log("\n[Gagal] Dump partisi NVRAM tidak berhasil.");
            return false;
        }

        /// <summary>
        /// Restore NVRAM partition from backup file
        /// </summary>
        public async Task<bool> RestoreNvramAsync(string portName, string backupFilePath, CancellationToken cancellationToken = default)
        {
            Log("╔════════════════════════════════════════════════════════════════╗");
            Log("║          MEDIATEK RESTORE NVRAM / IMEI                         ║");
            Log("╚════════════════════════════════════════════════════════════════╝");

            if (!File.Exists(backupFilePath))
            {
                Log($"[Error] File backup tidak ditemukan: {backupFilePath}");
                return false;
            }

            var partName = Path.GetFileNameWithoutExtension(backupFilePath).ToLower();
            if (!partName.Contains("nvram") && !partName.Contains("nvdata") && !partName.Contains("nvcfg"))
            {
                partName = "nvram";
            }

            Log($"[MTK] Menulis ulang partisi [{partName}] dari file: {Path.GetFileName(backupFilePath)}...");
            var res = await _toolEngine.ExecuteMtkClientAsync(PrepareMtkArgs($"w {partName} \"{backupFilePath}\""), Log);

            if (res.Success)
            {
                Log($"\n[SUKSES] Restore partisi [{partName}] selesai.");
                return true;
            }

            Log($"\n[Gagal] Restore partisi gagal: {res.ErrorMessage}");
            return false;
        }

        /// <summary>
        /// Send hardware reboot command to power cycle device
        /// </summary>
        public async Task<bool> RebootDeviceAsync(string portName)
        {
            Log($"[MTK] Mengirim perintah restart hardware via mtkclient reset...");
            var res = await _toolEngine.ExecuteMtkClientAsync(PrepareMtkArgs("reset"), Log);
            return res.Success;
        }

        /// <summary>
        /// Force crash MediaTek Preloader (VID_0E8D&PID_2000) into native BootROM mode (VID_0E8D&PID_0003)
        /// using hardware glitch watchdog exception or software UART break.
        /// Essential for Samsung MTK 5G (A13/A14/A22) and modern Oppo/Realme/Xiaomi devices.
        /// </summary>
        public async Task<bool> CrashPreloaderToBromAsync(string? portName = null, CancellationToken cancellationToken = default)
        {
            Log("╔════════════════════════════════════════════════════════════════╗");
            Log("║         MEDIATEK AUTO-CRASH PRELOADER TO BROM ENGINE          ║");
            Log("╚════════════════════════════════════════════════════════════════╝");
            Log("[CRASH PL] Memindai port Preloader MediaTek (0E8D:2000)...");

            string? targetPort = portName;
            if (string.IsNullOrEmpty(targetPort))
            {
                var ports = ScanMtkPorts();
                var plPort = ports.FirstOrDefault(p => !p.IsBromMode);
                targetPort = plPort?.PortName ?? ports.FirstOrDefault()?.PortName;
            }

            if (string.IsNullOrEmpty(targetPort))
            {
                Log("[INFO] Hubungkan HP MediaTek dalam kondisi mati sambil menahan tombol Vol Up + Vol Down.");
                Log("[CRASH PL] Menunggu port Preloader COM muncul (Timeout 25s)...");
                targetPort = await WaitForMtkPortAsync("", 25, cancellationToken);
            }

            if (string.IsNullOrEmpty(targetPort))
            {
                Log("[GAGAL] Port MediaTek tidak terdeteksi dalam batas waktu.");
                return false;
            }

            Log($"[CRASH PL] Terdeteksi port target: {targetPort}");
            Log("[CRASH PL] Mengirimkan urutan glitch byte watchdog (CMD_CRASH_PL 0xD4 / 0xF0 buffer)...");

            try
            {
                using var serial = new SerialPort(targetPort, 115200, Parity.None, 8, StopBits.One)
                {
                    ReadTimeout = 1500,
                    WriteTimeout = 1500,
                    DtrEnable = true,
                    RtsEnable = true
                };

                serial.Open();

                // Handshake Start Cmd: 0xA0 0x0A 0x50 0x05
                byte[] startCmd = new byte[] { 0xA0, 0x0A, 0x50, 0x05 };
                serial.Write(startCmd, 0, startCmd.Length);
                await Task.Delay(50, cancellationToken);

                // Crash Preloader command: 0xD4 followed by invalid memory address to force watchdog reset
                byte[] crashPayload = new byte[64];
                crashPayload[0] = 0xD4; // CMD_CRASH_PL
                for (int i = 1; i < crashPayload.Length; i++) crashPayload[i] = 0xF0;

                serial.Write(crashPayload, 0, crashPayload.Length);
                await Task.Delay(100, cancellationToken);

                serial.Close();
                Log("[CRASH PL] Glitch byte terkirim. Preloader mengalami exception crash.");
            }
            catch (Exception ex)
            {
                Log($"[CRASH PL] Catatan Port: {ex.Message} (Proses glitch sedang berjalan)");
            }

            Log("[CRASH PL] Menunggu prosesor MediaTek reboot ke mode BootROM Murni (0E8D:0003)...");
            var bromStopwatch = Stopwatch.StartNew();
            while (bromStopwatch.Elapsed.TotalSeconds < 15 && !cancellationToken.IsCancellationRequested)
            {
                var currentPorts = ScanMtkPorts();
                var bromPort = currentPorts.FirstOrDefault(p => p.IsBromMode);
                if (bromPort != null)
                {
                    Log("════════════════════════════════════════════════════════════════");
                    Log($" [SUKSES] Perangkat berhasil jatuh ke mode BROM MURNI: {bromPort.PortName}");
                    Log(" [BROM] Siap untuk eksekusi Auth Bypass (SLA/DA Disable), FRP & Unlock.");
                    Log("════════════════════════════════════════════════════════════════");
                    return true;
                }
                await Task.Delay(500, cancellationToken);
            }

            Log("[INFO] Mencoba eksekusi payload via mtkclient payload fallback...");
            var mtkRes = await _toolEngine.ExecuteMtkClientAsync(PrepareMtkArgs("payload --crash"), Log);
            if (mtkRes.Success)
            {
                Log("[SUKSES] Preloader crash via mtkclient payload berhasil.");
                return true;
            }

            Log("[INFO] Jika HP tetap berada di Preloader, gunakan testpoint hardware fisik (GND + CLK).");
            return false;
        }

        /// <summary>
        /// Poll for active MediaTek USB port within a timeout window
        /// </summary>
        private async Task<string?> WaitForMtkPortAsync(string preferredPort, int timeoutSeconds, CancellationToken cancellationToken)
        {
            if (!string.IsNullOrEmpty(preferredPort))
            {
                var existing = ScanMtkPorts();
                if (existing.Any(p => p.PortName.Equals(preferredPort, StringComparison.OrdinalIgnoreCase)))
                {
                    return preferredPort;
                }
            }

            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed.TotalSeconds < timeoutSeconds && !cancellationToken.IsCancellationRequested)
            {
                var ports = ScanMtkPorts();
                if (ports.Count > 0)
                {
                    return ports[0].PortName;
                }
                await Task.Delay(1000, cancellationToken);
            }

            return null;
        }
    }
}
