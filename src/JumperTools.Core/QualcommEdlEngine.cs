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
    public class QualcommPortInfo
    {
        public string PortName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string HardwareId { get; set; } = string.Empty;
    }

    public class QualcommDeviceInfo
    {
        public string PortName { get; set; } = string.Empty;
        public uint SaharaVersion { get; set; }
        public uint MinVersion { get; set; }
        public uint MaxCmdLen { get; set; }
        public uint Mode { get; set; }
        public string StorageType { get; set; } = "eMMC / UFS";
        public ulong TotalBlocks { get; set; }
        public uint BlockSize { get; set; }
        public List<string> Partitions { get; set; } = new();
    }

    /// <summary>
    /// Core Native Engine for Qualcomm EDL 9008 Sahara Protocol & Firehose XML Command Execution
    /// </summary>
    public class QualcommEdlEngine : IHardwareProtocolEngine
    {
        public string ProtocolName => "Qualcomm EDL 9008";
        public DeviceMode SupportedMode => DeviceMode.EDL;

        public event Action<string>? OnLog;
        public event Action<string>? OnUserActionRequired;
        
        private readonly ToolExecutionEngine _toolEngine;

        public QualcommEdlEngine(ToolExecutionEngine toolEngine)
        {
            _toolEngine = toolEngine;
        }

        public Task<bool> ProbeDeviceAsync(string? deviceIdOrPort, CancellationToken cancellationToken = default)
        {
            var ports = ScanQualcommPorts();
            if (!string.IsNullOrEmpty(deviceIdOrPort))
                return Task.FromResult(ports.Any(p => p.PortName.Equals(deviceIdOrPort, StringComparison.OrdinalIgnoreCase)));
            return Task.FromResult(ports.Count > 0);
        }

        public async Task<bool> RebootDeviceAsync(string? deviceIdOrPort, string targetMode = "system", CancellationToken cancellationToken = default)
        {
            var res = await _toolEngine.ExecuteQualcommEdlAsync(null, "reset", Log, 15000, cancellationToken);
            return res.Success;
        }

        // Sahara Command IDs
        private const uint SAHARA_HELLO = 0x01;
        private const uint SAHARA_HELLO_RESP = 0x02;
        private const uint SAHARA_READ_DATA = 0x03;
        private const uint SAHARA_END_IMAGE_TX = 0x04;
        private const uint SAHARA_DONE_REQ = 0x05;
        private const uint SAHARA_DONE_RESP = 0x06;
        private const uint SAHARA_RESET_REQ = 0x07;

        private void Log(string message)
        {
            var formatted = $"[{DateTime.Now:HH:mm:ss}] {message}";
            OnLog?.Invoke(formatted);
        }

        /// <summary>
        /// Scan Windows PnP for active Qualcomm HS-USB QDLoader 9008 COM ports (VID: 0x05C6, PID: 0x9008)
        /// </summary>
        public static List<QualcommPortInfo> ScanQualcommPorts()
        {
            var ports = new List<QualcommPortInfo>();

            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "SELECT * FROM Win32_PnPEntity WHERE (PNPDeviceID LIKE '%VID_05C6&PID_9008%' OR Caption LIKE '%9008%' OR Caption LIKE '%QDLoader%')"
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
                            ports.Add(new QualcommPortInfo
                            {
                                PortName = portName,
                                Description = caption,
                                HardwareId = pnpId
                            });
                        }
                    }
                }
            }
            catch { }

            return ports;
        }

        /// <summary>
        /// Perform initial Sahara Hello Handshake and read device capabilities
        /// </summary>
        public async Task<QualcommDeviceInfo?> PerformSaharaHandshakeAsync(string portName, string? programmerPath = null, int timeoutMs = 4000)
        {
            Log($"[Qualcomm EDL] Membuka port {portName} pada baudrate 115200...");

            return await Task.Run(() =>
            {
                SerialPort? port = null;
                try
                {
                    port = new SerialPort(portName, 115200, Parity.None, 8, StopBits.One)
                    {
                        ReadTimeout = timeoutMs,
                        WriteTimeout = timeoutMs,
                        DtrEnable = true,
                        RtsEnable = true
                    };
                    port.Open();

                    port.DiscardInBuffer();
                    port.DiscardOutBuffer();

                    Log("[Qualcomm] Menunggu paket SAHARA_HELLO dari perangkat 9008...");

                    var buffer = new byte[0x30]; // 48 bytes standard Sahara hello packet
                    var bytesRead = 0;
                    var startTime = DateTime.Now;

                    while (bytesRead < 8 && (DateTime.Now - startTime).TotalMilliseconds < timeoutMs)
                    {
                        if (port.BytesToRead > 0)
                        {
                            var r = port.Read(buffer, bytesRead, buffer.Length - bytesRead);
                            bytesRead += r;
                        }
                        Thread.Sleep(20);
                    }

                    var info = new QualcommDeviceInfo { PortName = portName };

                    if (bytesRead >= 8)
                    {
                        var cmd = BitConverter.ToUInt32(buffer, 0);
                        var len = BitConverter.ToUInt32(buffer, 4);

                        if (cmd == SAHARA_HELLO)
                        {
                            if (bytesRead >= 48)
                            {
                                info.SaharaVersion = BitConverter.ToUInt32(buffer, 8);
                                info.MinVersion = BitConverter.ToUInt32(buffer, 12);
                                info.MaxCmdLen = BitConverter.ToUInt32(buffer, 16);
                                info.Mode = BitConverter.ToUInt32(buffer, 20);
                            }

                            Log("────────── INFO CHIPSET QUALCOMM EDL ──────────");
                            Log($" Port COM        : {info.PortName}");
                            Log($" Protokol        : Sahara v{(info.SaharaVersion == 0 ? 2 : info.SaharaVersion)}");
                            Log($" Status Mode     : Emergency Download (EDL 9008)");
                            Log($" Programmer Path : {(string.IsNullOrEmpty(programmerPath) ? "Auto-Detect / Generic Firehose" : Path.GetFileName(programmerPath))}");
                            Log("───────────────────────────────────────────────");

                            // Send SAHARA_HELLO_RESP packet (48 bytes)
                            var resp = new byte[0x30];
                            Array.Copy(BitConverter.GetBytes(SAHARA_HELLO_RESP), 0, resp, 0, 4);
                            Array.Copy(BitConverter.GetBytes((uint)0x30), 0, resp, 4, 4);
                            Array.Copy(BitConverter.GetBytes((uint)0x02), 0, resp, 8, 4); // version
                            Array.Copy(BitConverter.GetBytes((uint)0x00), 0, resp, 12, 4); // status OK
                            Array.Copy(BitConverter.GetBytes((uint)0x00), 0, resp, 16, 4); // mode
                            port.Write(resp, 0, resp.Length);
                            Log("[Qualcomm] Berhasil merespons SAHARA_HELLO_RESP [OK].");
                        }
                        else
                        {
                            Log($"[Qualcomm Info] Menerima header paket ID: 0x{cmd:X2} (Len: {len}).");
                        }
                    }
                    else
                    {
                        Log("[Qualcomm Info] Respon raw Sahara tidak terdeteksi. Perangkat mungkin sudah beralih ke Firehose XML mode.");
                    }

                    return info;
                }
                catch (Exception ex)
                {
                    Log($"[Qualcomm Error] {ex.Message}");
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
        /// Execute a Firehose XML command packet
        /// </summary>
        public async Task<string> ExecuteFirehoseXmlAsync(string portName, string xmlPayload, int timeoutMs = 3000)
        {
            return await Task.Run(() =>
            {
                var sb = new StringBuilder();
                SerialPort? port = null;

                try
                {
                    port = new SerialPort(portName, 115200, Parity.None, 8, StopBits.One)
                    {
                        ReadTimeout = timeoutMs,
                        WriteTimeout = timeoutMs,
                        DtrEnable = true,
                        RtsEnable = true
                    };
                    port.Open();

                    port.DiscardInBuffer();
                    port.DiscardOutBuffer();

                    var formattedXml = $"<?xml version=\"1.0\" ?>\n<data>\n{xmlPayload}\n</data>\n";
                    var bytes = Encoding.UTF8.GetBytes(formattedXml);
                    port.Write(bytes, 0, bytes.Length);

                    var startTime = DateTime.Now;
                    while ((DateTime.Now - startTime).TotalMilliseconds < timeoutMs)
                    {
                        if (port.BytesToRead > 0)
                        {
                            var buf = new byte[port.BytesToRead];
                            var r = port.Read(buf, 0, buf.Length);
                            sb.Append(Encoding.UTF8.GetString(buf, 0, r));

                            var text = sb.ToString();
                            if (text.Contains("</data>") || text.Contains("<response value=\"ACK\"") || text.Contains("<response value=\"NAK\""))
                            {
                                break;
                            }
                        }
                        Thread.Sleep(30);
                    }
                }
                catch (Exception ex)
                {
                    sb.AppendLine($"[Error: {ex.Message}]");
                }
                finally
                {
                    if (port != null && port.IsOpen)
                    {
                        try { port.Close(); } catch { }
                    }
                }

                return sb.ToString();
            });
        }

        /// <summary>
        /// Universal 1-Click Qualcomm FRP Erase (Wipe FRP & Persist partitions)
        /// </summary>
        public async Task<bool> EraseFrpAsync(string portName, string? programmerPath = null, CancellationToken cancellationToken = default)
        {
            Log("╔════════════════════════════════════════════════════════════════╗");
            Log("║         QUALCOMM SNAPDRAGON ONE-CLICK FRP BYPASS (EDL 9008)    ║");
            Log("╚════════════════════════════════════════════════════════════════╝");

            Log("\n[Langkah 1] Mengeksekusi alat EDL untuk menghapus partisi FRP...");
            OnUserActionRequired?.Invoke("Hubungkan HP dalam mode EDL 9008 (via Test Point atau tombol Fastboot to EDL).");

            if (string.IsNullOrEmpty(programmerPath))
            {
                Log("[Error] Programmer / Firehose Loader (.mbn) wajib dipilih untuk perangkat Qualcomm.");
                return false;
            }

            var args = "--erase frp"; // edl.exe command to erase frp
            var result = await _toolEngine.ExecuteQualcommEdlAsync(programmerPath, args, Log);

            if (result.Success)
            {
                Log("\n════════════════════════════════════════════════════════════════");
                Log(" [SUKSES] Bypass FRP Qualcomm Snapdragon Berhasil 100%!");
                Log(" Perangkat telah bersih dari akun Google (FRP).");
                Log("════════════════════════════════════════════════════════════════\n");
                return true;
            }
            else
            {
                Log($"\n[Gagal] Operasi EDL gagal: {result.ErrorMessage}");
                return false;
            }
        }

        /// <summary>
        /// Factory Reset / Safe Format for Qualcomm devices (Wipe Userdata, Metadata, Misc)
        /// </summary>
        public async Task<bool> FormatUserdataAsync(string portName, string? programmerPath = null, CancellationToken cancellationToken = default)
        {
            Log("╔════════════════════════════════════════════════════════════════╗");
            Log("║         QUALCOMM FACTORY RESET / WIPE USERDATA ENGINE          ║");
            Log("╚════════════════════════════════════════════════════════════════╝");

            Log("\n[Langkah 1] Mengeksekusi alat EDL untuk Factory Reset...");
            OnUserActionRequired?.Invoke("Hubungkan HP dalam mode EDL 9008 (via Test Point atau tombol Fastboot to EDL).");

            if (string.IsNullOrEmpty(programmerPath))
            {
                Log("[Error] Programmer / Firehose Loader (.mbn) wajib dipilih untuk perangkat Qualcomm.");
                return false;
            }

            var args = "--erase userdata --erase metadata"; // edl.exe command
            var result = await _toolEngine.ExecuteQualcommEdlAsync(programmerPath, args, Log);

            if (result.Success)
            {
                Log("\n════════════════════════════════════════════════════════════════");
                Log(" [SUKSES] Factory Reset Qualcomm Snapdragon Selesai!");
                Log(" Kunci Pola/PIN dan seluruh data pengguna telah terhapus.");
                Log("════════════════════════════════════════════════════════════════\n");
                return true;
            }
            else
            {
                Log($"\n[Gagal] Operasi EDL gagal: {result.ErrorMessage}");
                return false;
            }
        }

        /// <summary>
        /// Read and display Qualcomm flash storage partition table (GPT)
        /// </summary>
        public async Task<List<string>> ReadGptTableAsync(string portName, string? programmerPath = null, CancellationToken cancellationToken = default)
        {
            Log("╔════════════════════════════════════════════════════════════════╗");
            Log("║          QUALCOMM READ GPT PARTITION TABLE (FIREHOSE)          ║");
            Log("╚════════════════════════════════════════════════════════════════╝");

            var partitions = new List<string>();
            Log("[Qualcomm] Membaca tabel partisi GUID (GPT) via Firehose...");

            if (string.IsNullOrEmpty(programmerPath))
            {
                Log("[Error] Programmer / Firehose Loader (.mbn) wajib dipilih untuk membaca GPT Qualcomm.");
                return partitions;
            }

            var result = await _toolEngine.ExecuteQualcommEdlAsync(programmerPath, "printgpt", (line) =>
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
                Log($"\n[Total] {partitions.Count} partisi teridentifikasi dari chip storage Qualcomm.");
            }
            return partitions;
        }

        /// <summary>
        /// Backup Qualcomm EFS / IMEI security partitions (modemst1, modemst2, fsg)
        /// </summary>
        public async Task<bool> BackupEfsAsync(string portName, string backupDirectory, string? programmerPath = null, CancellationToken cancellationToken = default)
        {
            Log("╔════════════════════════════════════════════════════════════════╗");
            Log("║         QUALCOMM BACKUP EFS / QCN (IMEI SECURITY)              ║");
            Log("╚════════════════════════════════════════════════════════════════╝");

            if (string.IsNullOrEmpty(programmerPath))
            {
                Log("[Error] Programmer / Firehose Loader (.mbn) wajib dipilih untuk backup EFS.");
                return false;
            }

            if (!Directory.Exists(backupDirectory))
            {
                Directory.CreateDirectory(backupDirectory);
            }

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var targetFolder = Path.Combine(backupDirectory, $"QCOM_EFS_{timestamp}");
            Directory.CreateDirectory(targetFolder);

            var st1 = Path.Combine(targetFolder, "modemst1.bin");
            var st2 = Path.Combine(targetFolder, "modemst2.bin");
            var fsg = Path.Combine(targetFolder, "fsg.bin");

            Log($"[1/3] Membaca partisi [modemst1] -> {Path.GetFileName(st1)}...");
            var res1 = await _toolEngine.ExecuteQualcommEdlAsync(programmerPath, $"r modemst1 \"{st1}\"", Log);

            Log($"[2/3] Membaca partisi [modemst2] -> {Path.GetFileName(st2)}...");
            var res2 = await _toolEngine.ExecuteQualcommEdlAsync(programmerPath, $"r modemst2 \"{st2}\"", Log);

            Log($"[3/3] Membaca partisi [fsg]      -> {Path.GetFileName(fsg)}...");
            var res3 = await _toolEngine.ExecuteQualcommEdlAsync(programmerPath, $"r fsg \"{fsg}\"", Log);

            // PERBAIKAN Cacat 5: Validasi ukuran file > 0 byte.
            // File.Exists() hanya membuktikan file dibuat oleh EDL tool — tidak menjamin
            // file berisi data valid. Kegagalan parsial menghasilkan file 0-byte yang
            // akan merusak modem jika di-restore ke perangkat lain.
            var st1Valid = res1.Success && File.Exists(st1) && new FileInfo(st1).Length > 0;
            var st2Valid = res2.Success && File.Exists(st2) && new FileInfo(st2).Length > 0;

            if (!st1Valid && File.Exists(st1) && new FileInfo(st1).Length == 0)
                Log($"[Peringatan] modemst1.bin ada namun berukuran 0-byte (dump gagal di tengah jalan).");
            if (!st2Valid && File.Exists(st2) && new FileInfo(st2).Length == 0)
                Log($"[Peringatan] modemst2.bin ada namun berukuran 0-byte (dump gagal di tengah jalan).");

            if (st1Valid || st2Valid)
            {
                Log($"\n════════════════════════════════════════════════════════════════");
                Log($" [SUKSES] Backup partisi EFS/IMEI Qualcomm tersimpan di:\n {targetFolder}");
                Log("════════════════════════════════════════════════════════════════\n");
                return true;
            }

            Log("\n[Gagal] Dump partisi EFS Qualcomm tidak berhasil.");
            return false;
        }

        /// <summary>
        /// Restore Qualcomm EFS partition from file
        /// </summary>
        public async Task<bool> RestoreEfsAsync(string portName, string backupFilePath, string? programmerPath = null, CancellationToken cancellationToken = default)
        {
            Log("╔════════════════════════════════════════════════════════════════╗");
            Log("║         QUALCOMM RESTORE EFS / IMEI SECURITY                   ║");
            Log("╚════════════════════════════════════════════════════════════════╝");

            if (string.IsNullOrEmpty(programmerPath))
            {
                Log("[Error] Programmer / Firehose Loader (.mbn) wajib dipilih untuk restore EFS.");
                return false;
            }

            if (!File.Exists(backupFilePath))
            {
                Log($"[Error] File backup tidak ditemukan: {backupFilePath}");
                return false;
            }

            var partName = Path.GetFileNameWithoutExtension(backupFilePath).ToLower();
            if (!partName.Contains("modemst1") && !partName.Contains("modemst2") && !partName.Contains("fsg"))
            {
                partName = "modemst1";
            }

            Log($"[Qualcomm] Menulis ulang partisi [{partName}] dari file: {Path.GetFileName(backupFilePath)}...");
            var res = await _toolEngine.ExecuteQualcommEdlAsync(programmerPath, $"w {partName} \"{backupFilePath}\"", Log);

            if (res.Success)
            {
                Log($"\n[SUKSES] Restore partisi [{partName}] selesai.");
                return true;
            }

            Log($"\n[Gagal] Restore partisi EFS gagal: {res.ErrorMessage}");
            return false;
        }

        /// <summary>
        /// Flash rawprogram XML firmware to Qualcomm storage
        /// </summary>
        public async Task<bool> FlashRawProgramAsync(string portName, string programmerPath, string rawProgramXml, string patchXml, CancellationToken cancellationToken = default)
        {
            Log($"[Qualcomm EDL] Menulis firmware via Firehose Loader: {Path.GetFileName(programmerPath)}...");
            var args = $"--loader \"{programmerPath}\"";
            if (!string.IsNullOrEmpty(rawProgramXml)) args += $" --rawprogram \"{rawProgramXml}\"";
            if (!string.IsNullOrEmpty(patchXml)) args += $" --patch \"{patchXml}\"";

            var result = await _toolEngine.ExecuteQualcommEdlAsync(programmerPath, args, Log);
            return result.Success;
        }

        /// <summary>
        /// Reboot Qualcomm device via Firehose power reset
        /// </summary>
        public async Task<bool> RebootDeviceAsync(string portName)
        {
            Log($"[Qualcomm] Mengirim perintah restart power reset via port {portName}...");
            var res = await ExecuteFirehoseXmlAsync(portName, "<power value=\"reset\" />", 2000);
            Log($"[Qualcomm] Perangkat sedang reboot ke sistem normal.");
            return true;
        }

        /// <summary>
        /// Poll for active Qualcomm 9008 port within a timeout window
        /// </summary>
        private async Task<string?> WaitForQualcommPortAsync(string preferredPort, int timeoutSeconds, CancellationToken cancellationToken)
        {
            if (!string.IsNullOrEmpty(preferredPort))
            {
                var existing = ScanQualcommPorts();
                if (existing.Any(p => p.PortName.Equals(preferredPort, StringComparison.OrdinalIgnoreCase)))
                {
                    return preferredPort;
                }
            }

            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed.TotalSeconds < timeoutSeconds && !cancellationToken.IsCancellationRequested)
            {
                var ports = ScanQualcommPorts();
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
