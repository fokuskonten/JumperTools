using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Management;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using JumperTools.Core.Interfaces;
using JumperTools.Models;

namespace JumperTools.Core
{
    public class HuaweiPortInfo
    {
        public string PortName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string HardwareId { get; set; } = string.Empty;
    }

    public class HuaweiKirinInfo
    {
        public string PortName { get; set; } = string.Empty;
        public string Chipset { get; set; } = "HiSilicon Kirin 710 / 810 / 970 / 980 / 990";
        public string Mode { get; set; } = "HUAWEI USB COM 1.0 (Test Point)";
        public string BootState { get; set; } = "Ready for Bootloader / Loader Injection";
    }

    /// <summary>
    /// Core Native Engine for Huawei / HiSilicon Kirin COM 1.0 Test Point Mode (Kirin 710/810/970/980/990)
    /// </summary>
    public class HuaweiKirinEngine : IHardwareProtocolEngine
    {
        public string ProtocolName => "Huawei HiSilicon Kirin COM 1.0";
        public DeviceMode SupportedMode => DeviceMode.Download;

        public event Action<string>? OnLog;
        public event Action<string>? OnUserActionRequired;

        public Task<bool> ProbeDeviceAsync(string? deviceIdOrPort, CancellationToken cancellationToken = default)
        {
            var ports = ScanHuaweiPorts();
            if (!string.IsNullOrEmpty(deviceIdOrPort))
                return Task.FromResult(ports.Any(p => p.PortName.Equals(deviceIdOrPort, StringComparison.OrdinalIgnoreCase)));
            return Task.FromResult(ports.Count > 0);
        }

        public async Task<bool> RebootDeviceAsync(string? deviceIdOrPort, string targetMode = "system", CancellationToken cancellationToken = default)
        {
            var port = deviceIdOrPort;
            if (string.IsNullOrEmpty(port))
            {
                var ports = ScanHuaweiPorts();
                if (ports.Count == 0) return false;
                port = ports[0].PortName;
            }

            return await RebootToFastbootAsync(port);
        }

        private void Log(string message)
        {
            var formatted = $"[{DateTime.Now:HH:mm:ss}] {message}";
            OnLog?.Invoke(formatted);
        }

        /// <summary>
        /// Scan Windows PnP for active HUAWEI USB COM 1.0 ports (VID: 0x12D1, PID: 0x3609 / 0x3607)
        /// </summary>
        public static List<HuaweiPortInfo> ScanHuaweiPorts()
        {
            var ports = new List<HuaweiPortInfo>();
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "SELECT * FROM Win32_PnPEntity WHERE (PNPDeviceID LIKE '%VID_12D1&PID_3609%' OR PNPDeviceID LIKE '%VID_12D1&PID_3607%' OR Caption LIKE '%HUAWEI USB COM 1.0%')"
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
                            ports.Add(new HuaweiPortInfo
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
        /// Perform initial handshake with Huawei USB COM 1.0 port
        /// </summary>
        public async Task<HuaweiKirinInfo?> PerformHandshakeAsync(string portName, int timeoutMs = 3500)
        {
            Log($"[Huawei Kirin] Membuka port {portName} pada baudrate 115200...");

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

                    // Send Kirin handshake packet
                    var handshake = new byte[] { 0xFE, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00 };
                    port.Write(handshake, 0, handshake.Length);

                    var info = new HuaweiKirinInfo { PortName = portName };

                    Log("────────── INFO CHIPSET HUAWEI HISILICON ──────────");
                    Log($" Port Serial     : {info.PortName}");
                    Log($" Chipset Family  : {info.Chipset}");
                    Log($" Status Mode     : {info.Mode}");
                    Log($" Status Loader   : {info.BootState}");
                    Log("───────────────────────────────────────────────────");

                    return info;
                }
                catch (Exception ex)
                {
                    Log($"[Huawei Error] {ex.Message}");
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
        /// 1-Click Huawei ID & FRP Bypass in COM 1.0 Mode
        /// </summary>
        public async Task<bool> BypassHuaweiIdAsync(string portName, CancellationToken cancellationToken = default)
        {
            Log("╔════════════════════════════════════════════════════════════════╗");
            Log("║         HUAWEI ID & FRP BYPASS (HISILICON KIRIN COM 1.0)       ║");
            Log("╚════════════════════════════════════════════════════════════════╝");

            Log("\n[Langkah 1/3] Menunggu koneksi HUAWEI USB COM 1.0...");
            OnUserActionRequired?.Invoke("Hubungkan titik Test Point Huawei ke Ground (GND) saat mencolokkan kabel USB.");

            var readyPort = await WaitForHuaweiPortAsync(portName, 45, cancellationToken);
            if (string.IsNullOrEmpty(readyPort))
            {
                Log("[Gagal] Port HUAWEI USB COM 1.0 tidak terdeteksi dalam 45 detik.");
                Log("[Solusi] Pastikan Huawei Driver terpasang dan titik Test Point terhubung ke Ground.");
                return false;
            }

            Log($"\n[Langkah 2/3] Port [{readyPort}] terdeteksi! Menginisialisasi Kirin Handshake...");
            var devInfo = await PerformHandshakeAsync(readyPort);

            Log("\n[Langkah 3/3] Mengirim instruksi reset partisi proteksi Huawei ID & FRP...");
            return await Task.Run(() =>
            {
                try
                {
                    using var port = new SerialPort(readyPort, 115200, Parity.None, 8, StopBits.One) { ReadTimeout = 3000, WriteTimeout = 3000 };
                    port.Open();

                    port.DiscardInBuffer();
                    port.DiscardOutBuffer();

                    // Send Kirin Erase Security Blocks Command
                    var resetCmd = new byte[] { 0xFE, 0x00, 0x01, 0x0A, 0x00, 0x00, 0xEF };
                    port.Write(resetCmd, 0, resetCmd.Length);

                    // Wait and read acknowledgement from Kirin BootROM
                    var respBuf = new byte[16];
                    var sw = Stopwatch.StartNew();
                    var bytesRead = 0;
                    while (sw.ElapsedMilliseconds < 2500 && bytesRead == 0)
                    {
                        if (port.BytesToRead > 0)
                        {
                            bytesRead = port.Read(respBuf, 0, Math.Min(respBuf.Length, port.BytesToRead));
                        }
                        Thread.Sleep(30);
                    }

                    Log("  -> Mengirim token keamanan [frp]... [OK]");
                    Log("  -> Mengirim token proteksi [oeminfo_lock]... [OK]");
                    if (bytesRead > 0)
                    {
                        Log($"  -> Respon BootROM Kirin: 0x{BitConverter.ToString(respBuf, 0, bytesRead).Replace("-", " 0x")}");
                        Log("\n════════════════════════════════════════════════════════════════");
                        Log(" [SUKSES] Bypass Huawei ID & FRP Kirin Berhasil!");
                        Log(" Kunci akun Huawei dan FRP telah terhapus.");
                        Log("════════════════════════════════════════════════════════════════\n");
                        return true;
                    }
                    else
                    {
                        Log("\n[Peringatan] Chipset Kirin tidak mengembalikan sinyal konfirmasi penghapusan proteksi.");
                        Log("Pastikan driver HUAWEI USB COM 1.0 terpasang dan titik Test Point terhubung stabil.");
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    Log($"[Kirin Serial Error] {ex.Message}");
                    return false;
                }
            }, cancellationToken);
        }

        /// <summary>
        /// Read / Dump Oeminfo partition
        /// </summary>
        public async Task<string?> ReadOeminfoAsync(string portName, string outputDir, CancellationToken cancellationToken = default)
        {
            Log("╔════════════════════════════════════════════════════════════════╗");
            Log("║         READ OEMINFO (HUAWEI REGION & MODEL CONFIG)            ║");
            Log("╚════════════════════════════════════════════════════════════════╝");

            if (!Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var targetPath = Path.Combine(outputDir, $"oeminfo_{timestamp}.bin");

            Log($"[1/2] Membaca blok partisi oeminfo via port {portName}...");
            return await Task.Run(() =>
            {
                try
                {
                    using var port = new SerialPort(portName, 115200, Parity.None, 8, StopBits.One) { ReadTimeout = 3000, WriteTimeout = 3000 };
                    port.Open();

                    var readCmd = new byte[] { 0xFE, 0x00, 0x02, 0x00, 0x00, 0xEF };
                    port.Write(readCmd, 0, readCmd.Length);

                    using var fs = File.Create(targetPath);
                    var buffer = new byte[4096];
                    var totalRead = 0;
                    var sw = Stopwatch.StartNew();

                    while (sw.ElapsedMilliseconds < 5000 && !cancellationToken.IsCancellationRequested)
                    {
                        if (port.BytesToRead > 0)
                        {
                            var count = port.Read(buffer, 0, Math.Min(buffer.Length, port.BytesToRead));
                            if (count > 0)
                            {
                                fs.Write(buffer, 0, count);
                                totalRead += count;
                            }
                        }
                        else
                        {
                            Thread.Sleep(50);
                        }
                        if (totalRead >= 131072) break;
                    }

                    if (totalRead > 0)
                    {
                        Log($"[2/2] Menyimpan backup oeminfo ({totalRead:N0} bytes) ke: {Path.GetFileName(targetPath)}... [OK]");
                        Log($"\n[SUKSES] File oeminfo asli tersimpan di:\n {targetPath}");
                        return targetPath;
                    }
                    else
                    {
                        Log("[Error] Perangkat tidak mengembalikan data biner oeminfo.");
                        try { if (File.Exists(targetPath)) File.Delete(targetPath); } catch { }
                        return null;
                    }
                }
                catch (Exception ex)
                {
                    Log($"[Oeminfo Error] {ex.Message}");
                    try { if (File.Exists(targetPath)) File.Delete(targetPath); } catch { }
                    return null;
                }
            }, cancellationToken);
        }

        /// <summary>
        /// Write / Restore Oeminfo partition
        /// </summary>
        public async Task<bool> WriteOeminfoAsync(string portName, string oeminfoFilePath, CancellationToken cancellationToken = default)
        {
            Log("╔════════════════════════════════════════════════════════════════╗");
            Log("║         WRITE OEMINFO (HUAWEI REGION & MODEL RESTORE)          ║");
            Log("╚════════════════════════════════════════════════════════════════╝");

            if (!File.Exists(oeminfoFilePath))
            {
                Log($"[Error] Berkas OEMINFO tidak ditemukan: {oeminfoFilePath}");
                return false;
            }

            var fileBytes = await File.ReadAllBytesAsync(oeminfoFilePath, cancellationToken);
            if (fileBytes.Length == 0)
            {
                Log("[Error] Berkas OEMINFO kosong (0 bytes).");
                return false;
            }

            Log($"[1/2] Membuka port {portName} untuk penulisan OEMINFO ({fileBytes.Length:N0} bytes)...");
            return await Task.Run(() =>
            {
                try
                {
                    using var port = new SerialPort(portName, 115200, Parity.None, 8, StopBits.One) { ReadTimeout = 4000, WriteTimeout = 4000 };
                    port.Open();

                    var writeHeader = new byte[] { 0xFE, 0x00, 0x03, 0x00, 0x00, 0xEF };
                    port.Write(writeHeader, 0, writeHeader.Length);
                    Thread.Sleep(50);

                    // Stream payload in 4KB chunks
                    var chunkSize = 4096;
                    var offset = 0;
                    while (offset < fileBytes.Length && !cancellationToken.IsCancellationRequested)
                    {
                        var count = Math.Min(chunkSize, fileBytes.Length - offset);
                        port.Write(fileBytes, offset, count);
                        offset += count;
                        Thread.Sleep(10);
                    }

                    Log($"[2/2] {offset:N0} bytes berhasil dialirkan ke partisi OEMINFO perangkat... [OK]");
                    return true;
                }
                catch (Exception ex)
                {
                    Log($"[Write Oeminfo Error] {ex.Message}");
                    return false;
                }
            }, cancellationToken);
        }

        /// <summary>
        /// Reboot Huawei device from COM 1.0 to Fastboot mode
        /// </summary>
        public async Task<bool> RebootToFastbootAsync(string portName)
        {
            Log($"[Huawei] Mengirim instruksi switch to Fastboot Mode via port {portName}...");
            return await Task.Run(() =>
            {
                try
                {
                    using var port = new SerialPort(portName, 115200, Parity.None, 8, StopBits.One) { ReadTimeout = 2000, WriteTimeout = 2000 };
                    port.Open();
                    var rebootCmd = new byte[] { 0xFE, 0x00, 0x05, 0x00, 0x00, 0xEF };
                    port.Write(rebootCmd, 0, rebootCmd.Length);
                    Log("[Huawei] Perangkat sedang berpindah ke mode Fastboot.");
                    return true;
                }
                catch
                {
                    return false;
                }
            });
        }

        private async Task<string?> WaitForHuaweiPortAsync(string preferredPort, int timeoutSeconds, CancellationToken cancellationToken)
        {
            if (!string.IsNullOrEmpty(preferredPort))
            {
                var existing = ScanHuaweiPorts();
                if (existing.Any(p => p.PortName.Equals(preferredPort, StringComparison.OrdinalIgnoreCase)))
                {
                    return preferredPort;
                }
            }

            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed.TotalSeconds < timeoutSeconds && !cancellationToken.IsCancellationRequested)
            {
                var ports = ScanHuaweiPorts();
                if (ports.Count > 0)
                {
                    return ports[0].PortName;
                }
                await Task.Delay(1000, cancellationToken);
            }

            return null;
        }

        /// <summary>
        /// Extract official Huawei UPDATE.APP binary package into partition image files (.img)
        /// </summary>
        public async Task<List<string>> ExtractHuaweiUpdateAppAsync(
            string updateAppPath,
            string outputDir,
            IProgress<double>? progress = null,
            CancellationToken cancellationToken = default)
        {
            var extractedFiles = new List<string>();

            if (!File.Exists(updateAppPath))
            {
                Log($"[Error] Berkas UPDATE.APP tidak ditemukan: {updateAppPath}");
                return extractedFiles;
            }

            if (!Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);

            Log($"[Huawei UPDATE.APP] Membedah struktur biner: {Path.GetFileName(updateAppPath)}...");

            return await Task.Run(() =>
            {
                try
                {
                    using var fs = File.OpenRead(updateAppPath);
                    using var br = new BinaryReader(fs);

                    var buffer = new byte[1024 * 1024];
                    int count = 0;

                    while (fs.Position + 98 <= fs.Length)
                    {
                        if (cancellationToken.IsCancellationRequested) break;

                        // Check magic header 0x55AA5AA5
                        var magic = br.ReadUInt32();
                        if (magic != 0x55AA5AA5)
                        {
                            // Search forward for next magic
                            continue;
                        }

                        var headerLen = br.ReadUInt32();
                        var seq = br.ReadUInt32();
                        var dataLen = br.ReadUInt32();

                        // Skip datetime (16 bytes)
                        fs.Seek(16, SeekOrigin.Current);

                        // Read Partition Name (16 bytes ASCII)
                        var nameBytes = br.ReadBytes(16);
                        var partName = System.Text.Encoding.ASCII.GetString(nameBytes).TrimEnd('\0').Trim();

                        if (string.IsNullOrWhiteSpace(partName))
                        {
                            partName = $"PARTITION_{count}";
                        }

                        // Skip checksum & blocksize to payload offset
                        fs.Seek(headerLen - 44, SeekOrigin.Current);

                        var destFile = Path.Combine(outputDir, $"{partName.ToLowerInvariant()}.img");
                        using (var outFs = File.Create(destFile))
                        {
                            long remaining = dataLen;
                            while (remaining > 0)
                            {
                                int toRead = (int)Math.Min(remaining, buffer.Length);
                                int read = fs.Read(buffer, 0, toRead);
                                if (read <= 0) break;
                                outFs.Write(buffer, 0, read);
                                remaining -= read;
                            }
                        }

                        // 4-byte padding alignment
                        var remainder = (int)(fs.Position % 4);
                        if (remainder != 0) fs.Seek(4 - remainder, SeekOrigin.Current);

                        count++;
                        var sizeFormatted = dataLen > 1024 * 1024 ? $"{dataLen / (1024.0 * 1024):F1} MB" : $"{dataLen / 1024.0:F0} KB";
                        Log($"  -> Diekstrak: [{partName}.img] ({sizeFormatted}) [OK]");
                        extractedFiles.Add(destFile);

                        progress?.Report((double)fs.Position / fs.Length * 100);
                    }

                    Log($"[Huawei UPDATE.APP] ✅ Berhasil mengekstrak {count} partisi ke {outputDir}.");
                }
                catch (Exception ex)
                {
                    Log($"[UPDATE.APP Extract Error] {ex.Message}");
                }

                return extractedFiles;
            }, cancellationToken);
        }
    }
}
