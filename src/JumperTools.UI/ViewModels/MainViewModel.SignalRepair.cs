using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using JumperTools.Core;
using JumperTools.Models;

namespace JumperTools.UI.ViewModels
{
    public partial class MainViewModel
    {
        // ==================== SIGNAL & IMEI REPAIR WORKSPACE ====================

        // Baseband & Radio Status Properties (bound to SignalRepairTabView.xaml)
        private string? _basebandVersion;
        public string? BasebandVersion
        {
            get => _basebandVersion;
            set { _basebandVersion = value; OnPropertyChanged(nameof(BasebandVersion)); }
        }

        private string? _imeiStatusSummary;
        public string? ImeiStatusSummary
        {
            get => _imeiStatusSummary;
            set { _imeiStatusSummary = value; OnPropertyChanged(nameof(ImeiStatusSummary)); }
        }

        private string? _radioPowerState;
        public string? RadioPowerState
        {
            get => _radioPowerState;
            set { _radioPowerState = value; OnPropertyChanged(nameof(RadioPowerState)); }
        }

        private string? _signalStatusMessage;
        public string? SignalStatusMessage
        {
            get => _signalStatusMessage;
            set { _signalStatusMessage = value; OnPropertyChanged(nameof(SignalStatusMessage)); }
        }

        /// <summary>
        /// Check Baseband / Radio status via ADB shell getprop commands.
        /// Reads: ro.baseband, gsm.version.baseband, IMEI via Phone Manager.
        /// </summary>
        public async Task CheckBasebandRadioAsync()
        {
            CurrentOperation = "Memeriksa Status Radio Modem & Baseband...";
            OperationProgress = 20;

            var serial = SelectedDevice?.DeviceId;
            var adbPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "platform-tools", "adb.exe");
            var prefix = string.IsNullOrEmpty(serial) ? "" : $"-s {serial} ";

            AddLog("╔════════════════════════════════════════════════════════════════╗");
            AddLog("║         CEK STATUS RADIO MODEM & BASEBAND                      ║");
            AddLog("╚════════════════════════════════════════════════════════════════╝");

            try
            {
                if (!System.IO.File.Exists(adbPath))
                {
                    AddLog($"[Error] adb.exe tidak ditemukan di {adbPath}");
                    OperationProgress = 0;
                    return;
                }

                // Read baseband version
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = adbPath,
                    Arguments = $"{prefix}shell getprop gsm.version.baseband",
                    RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true
                };
                using (var proc = System.Diagnostics.Process.Start(psi))
                {
                    var bb = (await proc!.StandardOutput.ReadToEndAsync()).Trim();
                    BasebandVersion = string.IsNullOrEmpty(bb) ? "Unknown" : bb;
                    AddLog($"[Baseband] {BasebandVersion}");
                }

                OperationProgress = 50;

                // Read IMEI via Phone state (requires ADB access)
                psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = adbPath,
                    Arguments = $"{prefix}shell service call iphonesubinfo 1",
                    RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true
                };
                using (var proc = System.Diagnostics.Process.Start(psi))
                {
                    var imeiRaw = (await proc!.StandardOutput.ReadToEndAsync()).Trim();
                    var imei = DeviceDetectionEngine.ExtractImei(imeiRaw);
                    ImeiStatusSummary = imei != "Not found" ? $"IMEI: {imei}" : "IMEI tidak terbaca (perlu root/ADB)";
                    AddLog($"[IMEI] {ImeiStatusSummary}");
                }

                OperationProgress = 80;

                // Check radio power state
                psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = adbPath,
                    Arguments = $"{prefix}shell getprop gsm.operator.numeric",
                    RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true
                };
                using (var proc = System.Diagnostics.Process.Start(psi))
                {
                    var op = (await proc!.StandardOutput.ReadToEndAsync()).Trim();
                    RadioPowerState = string.IsNullOrEmpty(op) ? "No Signal / Radio Off" : $"ON | Operator: {op}";
                    AddLog($"[Radio] {RadioPowerState}");
                }

                OperationProgress = 100;
                CurrentOperation = "Status Radio Selesai Diperiksa";
                AddLog("\n[OK] Pemeriksaan baseband & radio selesai.");
            }
            catch (Exception ex)
            {
                AddLog($"[Error] Gagal membaca status radio: {ex.Message}");
                BasebandVersion = "Error";
                ImeiStatusSummary = "Error";
                RadioPowerState = "Error";
                OperationProgress = 0;
            }
        }

        public ObservableCollection<QcnFileInfo> QcnFiles { get; } = new();
        public ObservableCollection<NvramFileInfo> NvramFiles { get; } = new();
        public ObservableCollection<string> DiagComPorts { get; } = new();

        private QcnFileInfo? _selectedQcnFile;
        public QcnFileInfo? SelectedQcnFile
        {
            get => _selectedQcnFile;
            set
            {
                _selectedQcnFile = value;
                OnPropertyChanged(nameof(SelectedQcnFile));
                // Notify IsEnabled binding di XAML tombol Write QCN
                OnPropertyChanged(nameof(HasSelectedDiagPortAndQcn));
            }
        }

        private NvramFileInfo? _selectedNvramFile;
        public NvramFileInfo? SelectedNvramFile
        {
            get => _selectedNvramFile;
            set
            {
                _selectedNvramFile = value;
                OnPropertyChanged(nameof(SelectedNvramFile));
            }
        }

        private string? _selectedDiagPort;
        public string? SelectedDiagPort
        {
            get => _selectedDiagPort;
            set
            {
                _selectedDiagPort = value;
                OnPropertyChanged(nameof(SelectedDiagPort));
                // PERBAIKAN P2-3: Notifikasi tombol Write QCN agar enable/disable sesuai kondisi
                OnPropertyChanged(nameof(HasSelectedDiagPortAndQcn));
            }
        }

        private string? _customQcnFilePath;
        public string? CustomQcnFilePath
        {
            get => _customQcnFilePath;
            set
            {
                _customQcnFilePath = value;
                OnPropertyChanged(nameof(CustomQcnFilePath));
                OnPropertyChanged(nameof(HasSelectedDiagPortAndQcn));
            }
        }

        /// <summary>
        /// PERBAIKAN P2-3: Computed property untuk enable/disable tombol Write QCN di XAML.
        /// Tombol hanya aktif jika Diag Port sudah dipilih DAN ada file QCN yang siap.
        /// </summary>
        public bool HasSelectedDiagPortAndQcn =>
            !string.IsNullOrEmpty(SelectedDiagPort) &&
            (SelectedQcnFile != null || (!string.IsNullOrEmpty(CustomQcnFilePath) && System.IO.File.Exists(CustomQcnFilePath)));

        public void BrowseCustomQcn()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Pilih File Kalibrasi QCN Kustom",
                Filter = "QCN / NV Calibration Files (*.qcn;*.nvup;*.bin)|*.qcn;*.nvup;*.bin|All Files (*.*)|*.*",
                CheckFileExists = true
            };

            if (dialog.ShowDialog() == true)
            {
                CustomQcnFilePath = dialog.FileName;
                AddLog($"[Signal Repair] File QCN kustom dipilih: {System.IO.Path.GetFileName(dialog.FileName)}");
            }
        }

        public void ClearCustomQcn()
        {
            CustomQcnFilePath = null;
            AddLog("[Signal Repair] File QCN kustom dihapus.");
        }

        public async Task RefreshSignalDatabaseAsync()
        {
            CurrentOperation = "Memindai Basis Data QCN & NVRAM...";
            OperationProgress = 30;

            await Task.Run(() =>
            {
                var qcnList = _signalEngine.ScanAvailableQcnFiles();
                var nvramList = _signalEngine.ScanAvailableNvramFiles();
                var ports = SignalRepairEngine.ScanQualcommDiagPorts();

                App.Current?.Dispatcher?.Invoke(() =>
                {
                    QcnFiles.Clear();
                    foreach (var q in qcnList) QcnFiles.Add(q);

                    NvramFiles.Clear();
                    foreach (var n in nvramList) NvramFiles.Add(n);

                    DiagComPorts.Clear();
                    foreach (var p in ports) DiagComPorts.Add(p);
                    if (DiagComPorts.Count > 0 && string.IsNullOrEmpty(SelectedDiagPort))
                    {
                        SelectedDiagPort = DiagComPorts[0];
                    }
                });
            });

            OperationProgress = 0;
            CurrentOperation = "Siap Operasi";
            AddLog($"[Signal Repair] Ditemukan {QcnFiles.Count} file QCN dan {NvramFiles.Count} backup NVRAM di folder database.");
        }

        public async Task EnableQualcommDiagPortAsync()
        {
            CurrentOperation = "Mengaktifkan Qualcomm Diag COM Port...";
            OperationProgress = 25;

            var serial = SelectedDevice?.DeviceId;
            var ok = await _signalEngine.EnableQualcommDiagPortViaAdbAsync(serial);
            OperationProgress = 100;
            CurrentOperation = ok ? "Diag Port Berhasil Diaktifkan" : "Diag Port Perlu Kode Manual";

            await ScanDiagPortsAsync();
        }

        public async Task ScanDiagPortsAsync()
        {
            var ports = await Task.Run(() => SignalRepairEngine.ScanQualcommDiagPorts());
            DiagComPorts.Clear();
            foreach (var p in ports) DiagComPorts.Add(p);

            if (DiagComPorts.Count > 0)
            {
                SelectedDiagPort = DiagComPorts[0];
                AddLog($"[Diag Scan] Terdeteksi Qualcomm Diagnostics Port: {string.Join(", ", DiagComPorts)}");
            }
            else
            {
                AddLog("[Diag Scan] Belum ada Qualcomm Diagnostic COM Port yang terdeteksi.");
            }
        }

        public async Task RestoreSelectedQcnAsync()
        {
            if (SelectedQcnFile == null)
            {
                AddLog("[Peringatan] Pilih file QCN kalibrasi terlebih dahulu.");
                return;
            }

            var port = SelectedDiagPort;
            if (string.IsNullOrEmpty(port))
            {
                AddLog("[Peringatan] Pilih atau aktifkan Qualcomm Diag Port (COM) terlebih dahulu.");
                return;
            }

            var device = SelectedDevice ?? new Device
            {
                DeviceId = port,
                Brand = SelectedQcnFile.Brand,
                Model = SelectedQcnFile.Model,
                Mode = DeviceMode.Normal,
                PortName = port,
                Chipset = "Qualcomm Snapdragon"
            };

            var result = await _operationOrchestrator.RunOperationAsync(
                device,
                OperationType.RepairEfs,
                $"Restorasi Kalibrasi QCN ({SelectedQcnFile.FileName})",
                executeAction: async (jobId, ct) =>
                {
                    return await _signalEngine.WriteQcnToDiagPortAsync(port, SelectedQcnFile.FilePath);
                },
                targetPartition: "modem_qcn");

            OperationStatus = result.State == OperationState.Completed ? "Completed" : "Failed";
            CurrentOperation = result.State == OperationState.Completed ? "Penulisan QCN Selesai" : "Gagal Menulis QCN";
        }

        public async Task BackupMtkNvramFromHubAsync()
        {
            var device = SelectedDevice ?? new Device
            {
                DeviceId = "MTK_BROM",
                Brand = "MediaTek",
                Model = "Universal MTK",
                Mode = DeviceMode.Unknown, // MTK BROM mode — no dedicated enum value
                PortName = "BROM_COM",
                Chipset = "MediaTek"
            };

            var backupDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "backups", "nvram");

            var result = await _operationOrchestrator.RunOperationAsync(
                device,
                OperationType.BackupPartition,
                "Backup NVRAM & NVDATA MediaTek",
                executeAction: async (jobId, ct) =>
                {
                    var res = await _signalEngine.BackupMediaTekNvramAsync(backupDir);
                    return res != null;
                },
                targetPartition: "nvram",
                requiredToolName: "mtkclient");

            OperationStatus = result.State == OperationState.Completed ? "Completed" : "Failed";
            if (result.State == OperationState.Completed)
            {
                await RefreshSignalDatabaseAsync();
            }
        }

        public async Task RestoreMtkNvramFromHubAsync()
        {
            string? backupPath = null;
            if (SelectedNvramFile != null && File.Exists(SelectedNvramFile.FilePath))
            {
                backupPath = Path.GetDirectoryName(SelectedNvramFile.FilePath);
            }

            if (string.IsNullOrEmpty(backupPath))
            {
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "Pilih Salah Satu File Backup NVRAM (.bin)",
                    Filter = "NVRAM Binary (*.bin)|*.bin|All Files (*.*)|*.*",
                    CheckFileExists = true
                };

                if (dialog.ShowDialog() == true)
                {
                    backupPath = Path.GetDirectoryName(dialog.FileName);
                }
                else
                {
                    AddLog("[Batal] Pemilihan folder backup NVRAM dibatalkan.");
                    return;
                }
            }

            var device = SelectedDevice ?? new Device
            {
                DeviceId = "MTK_BROM",
                Brand = "MediaTek",
                Model = "Universal MTK",
                Mode = DeviceMode.Unknown, // MTK BROM mode — no dedicated enum value
                PortName = "BROM_COM",
                Chipset = "MediaTek"
            };

            var result = await _operationOrchestrator.RunOperationAsync(
                device,
                OperationType.RestorePartition,
                "Restore NVRAM & NVDATA MediaTek",
                executeAction: async (jobId, ct) =>
                {
                    return await _signalEngine.RestoreMediaTekNvramAsync(backupPath!);
                },
                targetPartition: "nvram",
                requiredToolName: "mtkclient");

            OperationStatus = result.State == OperationState.Completed ? "Completed" : "Failed";
        }
    }
}
