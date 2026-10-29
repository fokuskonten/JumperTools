using System;
using System.Collections.ObjectModel;
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
    public class SignalRepairViewModel : TabViewModelBase
    {
        private readonly SignalRepairEngine _signalEngine;

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
                OnPropertyChanged();
                if (value != null)
                {
                    CustomQcnFilePath = value.FilePath;
                    SignalStatusMessage = $"Dipilih: {value.FileName} ({value.Brand} {value.Model})";
                }
            }
        }

        private NvramFileInfo? _selectedNvramFile;
        public NvramFileInfo? SelectedNvramFile
        {
            get => _selectedNvramFile;
            set
            {
                _selectedNvramFile = value;
                OnPropertyChanged();
            }
        }

        private string? _selectedDiagPort;
        public string? SelectedDiagPort
        {
            get => _selectedDiagPort;
            set
            {
                _selectedDiagPort = value;
                OnPropertyChanged();
            }
        }

        private string _customQcnFilePath = string.Empty;
        public string CustomQcnFilePath
        {
            get => _customQcnFilePath;
            set
            {
                _customQcnFilePath = value;
                OnPropertyChanged();
            }
        }

        private string _signalStatusMessage = "Pilih file QCN atau scan port Diag untuk memulai";
        public string SignalStatusMessage
        {
            get => _signalStatusMessage;
            set
            {
                _signalStatusMessage = value;
                OnPropertyChanged();
            }
        }

        private string _basebandVersion = "Belum dibaca";
        public string BasebandVersion
        {
            get => _basebandVersion;
            set
            {
                _basebandVersion = value;
                OnPropertyChanged();
            }
        }

        private string _imeiStatusSummary = "Standby";
        public string ImeiStatusSummary
        {
            get => _imeiStatusSummary;
            set
            {
                _imeiStatusSummary = value;
                OnPropertyChanged();
            }
        }

        private string _radioPowerState = "Unknown";
        public string RadioPowerState
        {
            get => _radioPowerState;
            set
            {
                _radioPowerState = value;
                OnPropertyChanged();
            }
        }

        public string TerminalLogText => Context.TerminalLogText;

        public ICommand RefreshSignalDatabaseCommand { get; }
        public ICommand EnableQualcommDiagPortCommand { get; }
        public ICommand ScanDiagPortsCommand { get; }
        public ICommand RestoreSelectedQcnCommand { get; }
        public ICommand RestoreSelectedQCNCommand => RestoreSelectedQcnCommand;
        public ICommand BackupMtkMetaNvramCommand { get; }
        public ICommand RestoreMtkMetaNvramCommand { get; }
        public ICommand WriteMtkImeiMetaCommand { get; }
        public ICommand BrowseCustomQcnCommand { get; }
        public ICommand ClearCustomQcnCommand { get; }
        public ICommand CheckBasebandRadioCommand { get; }
        public ICommand CopyLogCommand { get; }
        public ICommand ClearLogCommand { get; }
        public ICommand ShowFeatureGuideCommand { get; }

        public SignalRepairViewModel(ISharedWorkspaceContext context, SignalRepairEngine signalEngine)
            : base(context)
        {
            _signalEngine = signalEngine;

            RefreshSignalDatabaseCommand = new AsyncRelayCommand(RefreshSignalDatabaseAsync);
            EnableQualcommDiagPortCommand = new AsyncRelayCommand(EnableQualcommDiagPortAsync);
            ScanDiagPortsCommand = new AsyncRelayCommand(ScanDiagPortsAsync);
            RestoreSelectedQcnCommand = new AsyncRelayCommand(RestoreSelectedQcnAsync);
            BackupMtkMetaNvramCommand = new AsyncRelayCommand(BackupMtkNvramFromHubAsync);
            RestoreMtkMetaNvramCommand = new AsyncRelayCommand(RestoreMtkNvramFromHubAsync);
            WriteMtkImeiMetaCommand = new AsyncRelayCommand(WriteMtkImeiMetaAsync);
            BrowseCustomQcnCommand = new RelayCommand(BrowseCustomQcn);
            ClearCustomQcnCommand = new RelayCommand(ClearCustomQcn);
            CheckBasebandRadioCommand = new AsyncRelayCommand(CheckBasebandRadioAsync);
            CopyLogCommand = new RelayCommand(Context.CopyLog);
            ClearLogCommand = new RelayCommand(Context.ClearLog);
            ShowFeatureGuideCommand = new RelayCommand<string>(guideKey => Context.ShowFeatureGuide(guideKey ?? "Tab_SignalRepair"));

            _ = RefreshSignalDatabaseAsync();
        }

        public async Task RefreshSignalDatabaseAsync()
        {
            Context.CurrentOperation = "Memindai Basis Data QCN & NVRAM...";
            Context.OperationProgress = 30;

            await Task.Run(() =>
            {
                var qcnList = _signalEngine.ScanAvailableQcnFiles();
                var nvramList = _signalEngine.ScanAvailableNvramFiles();
                var ports = SignalRepairEngine.ScanQualcommDiagPorts();

                System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
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

            Context.OperationProgress = 0;
            Context.CurrentOperation = "Siap Operasi";
            AddLog($"[Signal Repair] Ditemukan {QcnFiles.Count} file QCN dan {NvramFiles.Count} backup NVRAM di folder database.");
        }

        public async Task EnableQualcommDiagPortAsync()
        {
            Context.CurrentOperation = "Mengaktifkan Qualcomm Diag COM Port...";
            Context.OperationProgress = 25;

            var serial = Context.SelectedDevice?.DeviceId;
            var ok = await _signalEngine.EnableQualcommDiagPortViaAdbAsync(serial);
            Context.OperationProgress = 100;
            Context.CurrentOperation = ok ? "Diag Port Berhasil Diaktifkan" : "Diag Port Perlu Kode Manual";

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
            var qcnPath = !string.IsNullOrEmpty(CustomQcnFilePath) ? CustomQcnFilePath : SelectedQcnFile?.FilePath;

            if (string.IsNullOrEmpty(qcnPath) || !File.Exists(qcnPath))
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

            var device = Context.SelectedDevice ?? new Device
            {
                DeviceId = port,
                Brand = SelectedQcnFile?.Brand ?? "Qualcomm",
                Model = SelectedQcnFile?.Model ?? "Snapdragon",
                Mode = DeviceMode.Normal,
                PortName = port,
                Chipset = "Qualcomm Snapdragon"
            };

            var result = await Context.Orchestrator.RunOperationAsync(
                device,
                OperationType.RepairEfs,
                $"Restorasi Kalibrasi QCN ({Path.GetFileName(qcnPath)})",
                executeAction: async (jobId, ct) =>
                {
                    return await _signalEngine.WriteQcnToDiagPortAsync(port, qcnPath);
                },
                targetPartition: "modem_qcn");

            Context.CurrentOperation = result.State == OperationState.Completed ? "Penulisan QCN Selesai" : "Gagal Menulis QCN";
        }

        public async Task BackupMtkNvramFromHubAsync()
        {
            var device = Context.SelectedDevice ?? new Device
            {
                DeviceId = "MTK_BROM",
                Brand = "MediaTek",
                Model = "Universal MTK",
                Mode = DeviceMode.Unknown,
                PortName = "BROM_COM",
                Chipset = "MediaTek"
            };

            var backupDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "backups", "nvram");

            var result = await Context.Orchestrator.RunOperationAsync(
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
                var dialog = new OpenFileDialog
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

            var device = Context.SelectedDevice ?? new Device
            {
                DeviceId = "MTK_BROM",
                Brand = "MediaTek",
                Model = "Universal MTK",
                Mode = DeviceMode.Unknown,
                PortName = "BROM_COM",
                Chipset = "MediaTek"
            };

            var result = await Context.Orchestrator.RunOperationAsync(
                device,
                OperationType.RestorePartition,
                "Restore NVRAM & NVDATA MediaTek",
                executeAction: async (jobId, ct) =>
                {
                    return await _signalEngine.RestoreMediaTekNvramAsync(backupPath!);
                },
                targetPartition: "nvram",
                requiredToolName: "mtkclient");
        }

        public async Task WriteMtkImeiMetaAsync()
        {
            var ports = MediaTekBromEngine.ScanMtkPorts();
            var port = ports.FirstOrDefault()?.PortName ?? Context.SelectedDevice?.PortName;

            AddLog("[MediaTek META] Memeriksa konektivitas perangkat MediaTek META / VCOM Mode...");
            Context.CurrentOperation = "Memeriksa Port MediaTek META...";
            Context.OperationProgress = 20;

            if (string.IsNullOrEmpty(port))
            {
                AddLog("[MediaTek META] [GAGAL] Port MediaTek USB VCOM / META Mode tidak terdeteksi.");
                AddLog("[Petunjuk Fisik] Matikan HP, tahan tombol Volume Bawah (atau Volume Atas+Bawah), lalu sambungkan kabel USB.");
                Context.CurrentOperation = "Gagal: Port MediaTek Tidak Terdeteksi";
                Context.OperationProgress = 0;
                return;
            }

            AddLog($"[MediaTek META] Terdeteksi port [{port}]. Menginisialisasi jabat tangan mode kalibrasi RF/NVRAM...");
            Context.OperationProgress = 50;

            // Jika ada file NVRAM yang dipilih, jalankan pemulihan kalibrasi fisik nyata
            if (SelectedNvramFile != null && File.Exists(SelectedNvramFile.FilePath))
            {
                var targetDir = Path.GetDirectoryName(SelectedNvramFile.FilePath)!;
                AddLog($"[MediaTek META] Menulis data kalibrasi IMEI/NVRAM dari: {SelectedNvramFile.FileName}...");
                var ok = await _signalEngine.RestoreMediaTekNvramAsync(targetDir);
                if (ok)
                {
                    Context.OperationProgress = 100;
                    Context.CurrentOperation = "Tulis IMEI Berhasil";
                    AddLog("[MediaTek META] [SUKSES] Kalibrasi partisi NVRAM/NVDATA berhasil ditulis ke perangkat.");
                }
                else
                {
                    Context.OperationProgress = 0;
                    Context.CurrentOperation = "Gagal Menulis Kalibrasi";
                    AddLog("[MediaTek META] [GAGAL] Penulisan partisi kalibrasi ke perangkat gagal.");
                }
            }
            else
            {
                Context.OperationProgress = 100;
                Context.CurrentOperation = "Port META Terhubung (Pilih File NVRAM)";
                AddLog($"[MediaTek META] Port [{port}] siap menerima injeksi partisi NVRAM/NVDATA. Silakan pilih salah satu file NVRAM di daftar bawah lalu klik 'Restore NVRAM'.");
            }
        }

        public void BrowseCustomQcn()
        {
            var dialog = new OpenFileDialog
            {
                Title = "Pilih Berkas Kalibrasi QCN Qualcomm (*.qcn;*.xqcn)",
                Filter = "Qualcomm QCN Files (*.qcn;*.xqcn)|*.qcn;*.xqcn|All Files (*.*)|*.*",
                CheckFileExists = true
            };

            if (dialog.ShowDialog() == true)
            {
                CustomQcnFilePath = dialog.FileName;
                SignalStatusMessage = $"Berkas kustom dipilih: {Path.GetFileName(dialog.FileName)}";
                AddLog($"[Signal Repair] Berkas QCN kustom dipilih: {dialog.FileName}");
            }
        }

        public void ClearCustomQcn()
        {
            CustomQcnFilePath = string.Empty;
            SignalStatusMessage = "Pilih file QCN atau scan port Diag untuk memulai";
            AddLog("[Signal Repair] Pilihan berkas QCN kustom dibersihkan.");
        }

        public async Task CheckBasebandRadioAsync()
        {
            var serial = Context.SelectedDevice?.DeviceId;
            if (string.IsNullOrEmpty(serial) || Context.SelectedDevice?.Mode != DeviceMode.Normal)
            {
                AddLog("[Radio Diag] Hubungkan HP dalam mode ADB (MTP / USB Debugging aktif) untuk membaca status radio.");
                BasebandVersion = Context.SelectedDevice?.Imei ?? "Tidak terdeteksi (Offline)";
                ImeiStatusSummary = string.IsNullOrEmpty(Context.SelectedDevice?.Imei) ? "Null / Invalid" : "OK";
                RadioPowerState = "Standby";
                return;
            }

            Context.CurrentOperation = "Membaca Status Radio Modem...";
            Context.OperationProgress = 30;

            try
            {
                var adbExe = Path.Combine(Context.PlatformToolsPath, "adb.exe");
                if (!File.Exists(adbExe)) adbExe = "adb.exe";

                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = adbExe,
                    Arguments = $"-s {serial} shell getprop gsm.version.baseband",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };

                using var p = System.Diagnostics.Process.Start(psi);
                if (p != null)
                {
                    var bb = (await p.StandardOutput.ReadToEndAsync()).Trim();
                    BasebandVersion = string.IsNullOrEmpty(bb) ? "Unknown / Null" : bb;
                }

                ImeiStatusSummary = string.IsNullOrEmpty(Context.SelectedDevice?.Imei) ? "Belum Terbaca" : Context.SelectedDevice.Imei;
                RadioPowerState = "Active (ON)";
                Context.OperationProgress = 100;
                Context.CurrentOperation = "Status Radio Terbaca";
                AddLog($"[Radio Diag] Baseband: {BasebandVersion} | IMEI: {ImeiStatusSummary} | Power: {RadioPowerState}");
            }
            catch (Exception ex)
            {
                AddLog($"[Radio Diag Error] {ex.Message}");
                Context.OperationProgress = 0;
            }
        }
    }
}
