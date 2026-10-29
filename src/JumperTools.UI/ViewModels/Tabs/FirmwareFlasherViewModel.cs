using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using JumperTools.Core;
using JumperTools.Core.Protocols;
using JumperTools.Models;
using JumperTools.Services;
using JumperTools.UI.ViewModels.Shared;
using Microsoft.Win32;

namespace JumperTools.UI.ViewModels.Tabs
{
    public class FirmwareFlasherViewModel : TabViewModelBase
    {
        private readonly FirmwareFlasherEngine _firmwareFlasherEngine = new FirmwareFlasherEngine();
        private readonly SamsungAtEngine _samsungEngine;
        private readonly MediaTekBromEngine _mtkEngine;
        private readonly QualcommEdlEngine _qualcommEngine;
        private readonly UnisocSpdEngine _unisocEngine;
        private readonly FastbootEngine _fastbootEngine;

        // Auto-ROM Protocols & Services
        private readonly SamsungFusProtocol _samsungFusProtocol = new SamsungFusProtocol();
        private readonly XiaomiBigOtaProtocol _xiaomiBigOtaProtocol = new XiaomiBigOtaProtocol();
        private readonly OppoOtaProtocol _oppoOtaProtocol = new OppoOtaProtocol();
        private readonly TranssionOtaProtocol _transsionProtocol = new TranssionOtaProtocol();
        private readonly UnisocPacProtocol _unisocPacProtocol = new UnisocPacProtocol();
        private readonly AutoRomDownloaderService _autoRomDownloader = new AutoRomDownloaderService();
        private readonly FirmwareExtractorEngine _firmwareExtractorEngine = new FirmwareExtractorEngine();
        private CancellationTokenSource? _autoRomCts;

        private SamsungFirmwareInfo? _lastSamsungInfo;
        private XiaomiFirmwareInfo? _lastXiaomiInfo;
        private OppoFirmwareInfo? _lastOppoInfo;
        private TranssionFirmwareInfo? _lastTranssionInfo;

        // Auto-ROM Properties
        public ObservableCollection<string> AutoRomBrandList { get; } = new()
        {
            "Samsung Galaxy (Official FUS)",
            "Xiaomi / Redmi / POCO (Official BigOTA)",
            "Oppo / Realme (Official OTA)",
            "Infinix / Tecno / Itel (Official Carlcare)",
            "Unisoc SPRD (.PAC Official)"
        };

        private string _selectedAutoRomBrand = "Samsung Galaxy (Official FUS)";
        public string SelectedAutoRomBrand
        {
            get => _selectedAutoRomBrand;
            set
            {
                if (_selectedAutoRomBrand != value)
                {
                    _selectedAutoRomBrand = value;
                    OnPropertyChanged();
                    IsAutoRomFound = false;
                    AutoRomFoundVersionInfo = string.Empty;
                    AutoRomStatusText = "Siap memeriksa versi firmware resmi.";
                }
            }
        }

        private string _autoRomModelInput = string.Empty;
        public string AutoRomModelInput
        {
            get => _autoRomModelInput;
            set { _autoRomModelInput = value; OnPropertyChanged(); }
        }

        private string _autoRomRegionInput = string.Empty;
        public string AutoRomRegionInput
        {
            get => _autoRomRegionInput;
            set { _autoRomRegionInput = value; OnPropertyChanged(); }
        }

        private string _autoRomStatusText = "Siap memeriksa versi firmware resmi dari CDN server pabrikan.";
        public string AutoRomStatusText
        {
            get => _autoRomStatusText;
            set { _autoRomStatusText = value; OnPropertyChanged(); }
        }

        private string _autoRomFoundVersionInfo = string.Empty;
        public string AutoRomFoundVersionInfo
        {
            get => _autoRomFoundVersionInfo;
            set { _autoRomFoundVersionInfo = value; OnPropertyChanged(); }
        }

        private double _autoRomDownloadProgress;
        public double AutoRomDownloadProgress
        {
            get => _autoRomDownloadProgress;
            set { _autoRomDownloadProgress = value; OnPropertyChanged(); }
        }

        private string _autoRomSpeedText = string.Empty;
        public string AutoRomSpeedText
        {
            get => _autoRomSpeedText;
            set { _autoRomSpeedText = value; OnPropertyChanged(); }
        }

        private bool _isAutoRomDownloading;
        public bool IsAutoRomDownloading
        {
            get => _isAutoRomDownloading;
            set
            {
                _isAutoRomDownloading = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanStartAutoRomDownload));
            }
        }

        private bool _isAutoRomFound;
        public bool IsAutoRomFound
        {
            get => _isAutoRomFound;
            set
            {
                _isAutoRomFound = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanStartAutoRomDownload));
            }
        }

        public bool CanStartAutoRomDownload => IsAutoRomFound && !IsAutoRomDownloading;

        private FirmwarePackageManifest _currentFirmwareManifest = new FirmwarePackageManifest();
        public FirmwarePackageManifest CurrentFirmwareManifest
        {
            get => _currentFirmwareManifest;
            set { _currentFirmwareManifest = value; OnPropertyChanged(); }
        }

        public ObservableCollection<PartitionTableItem> FirmwarePartitions { get; set; } = new ObservableCollection<PartitionTableItem>();

        private string _firmwarePathInput = string.Empty;
        public string FirmwarePathInput
        {
            get => _firmwarePathInput;
            set
            {
                if (_firmwarePathInput != value)
                {
                    _firmwarePathInput = value;
                    OnPropertyChanged();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        _ = LoadFirmwarePackageAsync(value);
                    }
                }
            }
        }

        private string _activeFlashingPartitionName = string.Empty;
        public string ActiveFlashingPartitionName
        {
            get => _activeFlashingPartitionName;
            set { _activeFlashingPartitionName = value; OnPropertyChanged(); }
        }

        private double _activePartitionPercentage;
        public double ActivePartitionPercentage
        {
            get => _activePartitionPercentage;
            set { _activePartitionPercentage = value; OnPropertyChanged(); }
        }

        private int _selectedFlasherTabIndex = 0;
        public int SelectedFlasherTabIndex
        {
            get => _selectedFlasherTabIndex;
            set
            {
                if (_selectedFlasherTabIndex != value)
                {
                    _selectedFlasherTabIndex = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsSamsungFlasherSelected));
                    OnPropertyChanged(nameof(IsMtkFlasherSelected));
                    OnPropertyChanged(nameof(IsQualcommFlasherSelected));
                    OnPropertyChanged(nameof(IsUnisocFlasherSelected));
                    OnPropertyChanged(nameof(IsFastbootFlasherSelected));
                }
            }
        }

        public bool IsSamsungFlasherSelected => SelectedFlasherTabIndex == 0;
        public bool IsMtkFlasherSelected => SelectedFlasherTabIndex == 1;
        public bool IsQualcommFlasherSelected => SelectedFlasherTabIndex == 2;
        public bool IsUnisocFlasherSelected => SelectedFlasherTabIndex == 3;
        public bool IsFastbootFlasherSelected => SelectedFlasherTabIndex == 4;

        public ObservableCollection<string> FlasherProtocolList { get; } = new()
        {
            "Samsung Odin (4-File TAR.MD5)",
            "MediaTek (Scatter & DA Auth)",
            "Qualcomm EDL (RawProgram XML)",
            "Unisoc / SPRD (PAC Package)",
            "Fastboot / Universal Multi-Partisi"
        };

        // Samsung 4-File Odin
        private string _samsungBlPath = string.Empty;
        public string SamsungBlPath { get => _samsungBlPath; set { _samsungBlPath = value; OnPropertyChanged(); } }

        private string _samsungApPath = string.Empty;
        public string SamsungApPath { get => _samsungApPath; set { _samsungApPath = value; OnPropertyChanged(); } }

        private string _samsungCpPath = string.Empty;
        public string SamsungCpPath { get => _samsungCpPath; set { _samsungCpPath = value; OnPropertyChanged(); } }

        private string _samsungCscPath = string.Empty;
        public string SamsungCscPath { get => _samsungCscPath; set { _samsungCscPath = value; OnPropertyChanged(); } }

        // MediaTek Scatter & DA
        private string _mtkScatterPath = string.Empty;
        public string MtkScatterPath
        {
            get => _mtkScatterPath;
            set { _mtkScatterPath = value; OnPropertyChanged(); }
        }

        private string _mtkCustomDaPath = string.Empty;
        public string MtkCustomDaFileName => string.IsNullOrEmpty(_mtkCustomDaPath) ? "Bawaan (Default MTK DA)" : Path.GetFileName(_mtkCustomDaPath);

        private string _mtkCustomAuthPath = string.Empty;
        public string MtkCustomAuthFileName => string.IsNullOrEmpty(_mtkCustomAuthPath) ? "Bawaan (Default Auth Bypass)" : Path.GetFileName(_mtkCustomAuthPath);

        // Qualcomm EDL
        private string _qualcommRawProgramPath = string.Empty;
        public string QualcommRawProgramPath
        {
            get => _qualcommRawProgramPath;
            set { _qualcommRawProgramPath = value; OnPropertyChanged(); }
        }

        private string _qualcommPatchPath = string.Empty;
        public string QualcommPatchPath
        {
            get => _qualcommPatchPath;
            set { _qualcommPatchPath = value; OnPropertyChanged(); }
        }

        private string _qualcommFirehosePath = string.Empty;
        public string QualcommFirehosePath
        {
            get => _qualcommFirehosePath;
            set { _qualcommFirehosePath = value; OnPropertyChanged(); }
        }

        // Unisoc PAC
        private string _unisocPacPath = string.Empty;
        public string UnisocPacPath
        {
            get => _unisocPacPath;
            set { _unisocPacPath = value; OnPropertyChanged(); }
        }

        public string TerminalLogText => Context.TerminalLogText;

        // Auto-ROM Commands
        public ICommand CheckOfficialRomCommand { get; }
        public ICommand StartDownloadAutoRomCommand { get; }
        public ICommand CancelDownloadAutoRomCommand { get; }
        public ICommand AutoDetectDeviceForRomCommand { get; }

        // Flasher Commands
        public ICommand BrowseSamsungBlCommand { get; }
        public ICommand BrowseSamsungApCommand { get; }
        public ICommand BrowseSamsungCpCommand { get; }
        public ICommand BrowseSamsungCscCommand { get; }
        public ICommand ClearSamsungOdinFilesCommand { get; }
        public ICommand FlashSamsungOdinCommand { get; }

        public ICommand BrowseMtkScatterCommand { get; }
        public ICommand MtkBrowseCustomDaCommand { get; }
        public ICommand MtkBrowseCustomAuthCommand { get; }

        public ICommand BrowseQualcommRawProgramCommand { get; }
        public ICommand BrowseQualcommPatchCommand { get; }
        public ICommand BrowseQualcommFirehoseCommand { get; }

        public ICommand BrowseUnisocPacCommand { get; }
        public ICommand FlashUnisocPacCommand { get; }
        public ICommand SamsungEdlFlashBitCommand { get; }

        public ICommand BrowseFirmwareFileCommand { get; }
        public ICommand LoadFirmwareCommand { get; }
        public ICommand StartFirmwareFlashCommand { get; }
        public ICommand SelectAllFirmwarePartitionsCommand { get; }
        public ICommand DeselectAllFirmwarePartitionsCommand { get; }

        public ICommand CancelCurrentOperationCommand { get; }
        public ICommand CopyLogCommand { get; }
        public ICommand ClearLogCommand { get; }
        public ICommand ShowFeatureGuideCommand { get; }

        public FirmwareFlasherViewModel(
            ISharedWorkspaceContext context,
            SamsungAtEngine samsungEngine,
            MediaTekBromEngine mtkEngine,
            QualcommEdlEngine qualcommEngine,
            UnisocSpdEngine unisocEngine,
            FastbootEngine fastbootEngine)
            : base(context)
        {
            _samsungEngine = samsungEngine;
            _mtkEngine = mtkEngine;
            _qualcommEngine = qualcommEngine;
            _unisocEngine = unisocEngine;
            _fastbootEngine = fastbootEngine;

            // Hook up logging
            _samsungFusProtocol.OnLog += AddLog;
            _xiaomiBigOtaProtocol.OnLog += AddLog;
            _autoRomDownloader.OnLog += AddLog;

            // Auto-ROM Commands
            CheckOfficialRomCommand = new AsyncRelayCommand(CheckOfficialRomAsync);
            StartDownloadAutoRomCommand = new AsyncRelayCommand(StartDownloadAutoRomAsync);
            CancelDownloadAutoRomCommand = new RelayCommand(CancelDownloadAutoRom);
            AutoDetectDeviceForRomCommand = new AsyncRelayCommand(AutoDetectDeviceForRomAsync);

            BrowseSamsungBlCommand = new RelayCommand(BrowseSamsungBl);
            BrowseSamsungApCommand = new RelayCommand(BrowseSamsungAp);
            BrowseSamsungCpCommand = new RelayCommand(BrowseSamsungCp);
            BrowseSamsungCscCommand = new RelayCommand(BrowseSamsungCsc);
            ClearSamsungOdinFilesCommand = new RelayCommand(ClearSamsungOdinFiles);
            FlashSamsungOdinCommand = new AsyncRelayCommand(FlashSamsungOdinAsync);

            BrowseMtkScatterCommand = new RelayCommand(BrowseMtkScatter);
            MtkBrowseCustomDaCommand = new RelayCommand(MtkBrowseCustomDa);
            MtkBrowseCustomAuthCommand = new RelayCommand(MtkBrowseCustomAuth);

            BrowseQualcommRawProgramCommand = new RelayCommand(BrowseQualcommRawProgram);
            BrowseQualcommPatchCommand = new RelayCommand(BrowseQualcommPatch);
            BrowseQualcommFirehoseCommand = new RelayCommand(BrowseQualcommFirehose);

            BrowseUnisocPacCommand = new RelayCommand(BrowseUnisocPac);
            FlashUnisocPacCommand = new AsyncRelayCommand(FlashUnisocPacAsync);
            SamsungEdlFlashBitCommand = new AsyncRelayCommand(SamsungEdlFlashBitAsync);

            BrowseFirmwareFileCommand = new RelayCommand(BrowseFirmwareFile);
            LoadFirmwareCommand = new AsyncRelayCommand<string>(async (path) => await LoadFirmwarePackageAsync(path ?? FirmwarePathInput));
            StartFirmwareFlashCommand = new AsyncRelayCommand(StartFirmwareFlashAsync);
            SelectAllFirmwarePartitionsCommand = new RelayCommand(() => ToggleAllFirmwarePartitions(true));
            DeselectAllFirmwarePartitionsCommand = new RelayCommand(() => ToggleAllFirmwarePartitions(false));

            CancelCurrentOperationCommand = new RelayCommand(Context.CancelCurrentOperation);
            CopyLogCommand = new RelayCommand(Context.CopyLog);
            ClearLogCommand = new RelayCommand(Context.ClearLog);
            ShowFeatureGuideCommand = new RelayCommand<string>(key => Context.ShowFeatureGuide(key ?? "Samsung_FlashOdin"));

            // Auto-React to USB Device Connection (Zero-Click Auto Pipeline)
            Context.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(ISharedWorkspaceContext.SelectedDevice) && Context.SelectedDevice != null)
                {
                    _ = AutoDetectDeviceForRomAsync();
                }
            };

            if (Context.SelectedDevice != null)
            {
                _ = AutoDetectDeviceForRomAsync();
            }
        }

        public void BrowseSamsungBl()
        {
            var file = BrowseOdinTarFile("BL (Bootloader)");
            if (!string.IsNullOrEmpty(file)) SetAndAutoDetectOdinCompanionFiles("BL", file);
        }

        public void BrowseSamsungAp()
        {
            var file = BrowseOdinTarFile("AP (System/PDA)");
            if (!string.IsNullOrEmpty(file)) SetAndAutoDetectOdinCompanionFiles("AP", file);
        }

        public void BrowseSamsungCp()
        {
            var file = BrowseOdinTarFile("CP (Modem/Phone)");
            if (!string.IsNullOrEmpty(file)) SetAndAutoDetectOdinCompanionFiles("CP", file);
        }

        public void BrowseSamsungCsc()
        {
            var file = BrowseOdinTarFile("CSC (Country/Region)");
            if (!string.IsNullOrEmpty(file)) SetAndAutoDetectOdinCompanionFiles("CSC", file);
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
                AddLog($"[Odin Warning] Gagal auto-detect companion files: {ex.Message}");
            }
        }

        private string BrowseOdinTarFile(string slotName)
        {
            var dialog = new OpenFileDialog
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

            var targetDevice = Context.SelectedDevice ?? new Device
            {
                DeviceId = "SAMSUNG_ODIN",
                Brand = "Samsung",
                Model = "Samsung Galaxy",
                Mode = DeviceMode.Download
            };

            var result = await Context.Orchestrator.RunOperationAsync(
                targetDevice,
                OperationType.FlashFirmware,
                $"Samsung 4-File Odin Flash ({Path.GetFileName(primaryTar)})",
                executeAction: async (jobId, ct) =>
                {
                    var res = await Context.ToolEngine.ExecuteSamsungFlashAsync(targetDevice.DeviceId, primaryTar, new OperationParameters
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

            Context.CurrentOperation = result.State == OperationState.Completed ? "Flashing Samsung Selesai" : $"Flashing Gagal: {result.FailureReason}";
        }

        public void BrowseMtkScatter()
        {
            var dialog = new OpenFileDialog
            {
                Title = "Pilih Berkas MediaTek Scatter (*.txt)",
                Filter = "MediaTek Scatter Files (*scatter*.txt;*.txt)|*scatter*.txt;*.txt|All Files (*.*)|*.*",
                CheckFileExists = true
            };
            if (dialog.ShowDialog() == true)
            {
                MtkScatterPath = dialog.FileName;
                AddLog($"[MTK Flasher] Memuat scatter: {Path.GetFileName(MtkScatterPath)}");
                _ = LoadFirmwarePackageAsync(MtkScatterPath);
            }
        }

        public void MtkBrowseCustomDa()
        {
            var dialog = new OpenFileDialog
            {
                Title = "Pilih Berkas Download Agent (*.bin)",
                Filter = "Download Agent (*.bin)|*.bin|All Files (*.*)|*.*",
                CheckFileExists = true
            };
            if (dialog.ShowDialog() == true)
            {
                _mtkCustomDaPath = dialog.FileName;
                OnPropertyChanged(nameof(MtkCustomDaFileName));
                AddLog($"[MTK DA] Memilih custom DA: {Path.GetFileName(_mtkCustomDaPath)}");
            }
        }

        public void MtkBrowseCustomAuth()
        {
            var dialog = new OpenFileDialog
            {
                Title = "Pilih Berkas Auth (*.auth)",
                Filter = "Authentication File (*.auth)|*.auth|All Files (*.*)|*.*",
                CheckFileExists = true
            };
            if (dialog.ShowDialog() == true)
            {
                _mtkCustomAuthPath = dialog.FileName;
                OnPropertyChanged(nameof(MtkCustomAuthFileName));
                AddLog($"[MTK Auth] Memilih custom Auth: {Path.GetFileName(_mtkCustomAuthPath)}");
            }
        }

        public void BrowseQualcommRawProgram()
        {
            var dialog = new OpenFileDialog
            {
                Title = "Pilih Berkas Qualcomm RawProgram XML (*.xml)",
                Filter = "Qualcomm RawProgram XML (*rawprogram*.xml;*.xml)|*rawprogram*.xml;*.xml|All Files (*.*)|*.*",
                CheckFileExists = true
            };
            if (dialog.ShowDialog() == true)
            {
                QualcommRawProgramPath = dialog.FileName;
                AddLog($"[Qualcomm Flasher] Memuat RawProgram XML: {Path.GetFileName(QualcommRawProgramPath)}");
                _ = LoadFirmwarePackageAsync(QualcommRawProgramPath);

                var dir = Path.GetDirectoryName(QualcommRawProgramPath);
                if (!string.IsNullOrEmpty(dir))
                {
                    var patch = Directory.GetFiles(dir, "patch*.xml").FirstOrDefault();
                    if (!string.IsNullOrEmpty(patch)) QualcommPatchPath = patch;
                    var prog = Directory.GetFiles(dir, "prog_*.mbn").Concat(Directory.GetFiles(dir, "prog_*.elf")).FirstOrDefault();
                    if (!string.IsNullOrEmpty(prog)) QualcommFirehosePath = prog;
                }
            }
        }

        public void BrowseQualcommPatch()
        {
            var dialog = new OpenFileDialog
            {
                Title = "Pilih Berkas Qualcomm Patch XML (*.xml)",
                Filter = "Qualcomm Patch XML (*patch*.xml;*.xml)|*patch*.xml;*.xml|All Files (*.*)|*.*",
                CheckFileExists = true
            };
            if (dialog.ShowDialog() == true)
            {
                QualcommPatchPath = dialog.FileName;
            }
        }

        public void BrowseQualcommFirehose()
        {
            var dialog = new OpenFileDialog
            {
                Title = "Pilih Berkas Programmer Firehose (*.mbn;*.elf)",
                Filter = "Qualcomm Firehose (*.mbn;*.elf;*.bin)|*.mbn;*.elf;*.bin|All Files (*.*)|*.*",
                CheckFileExists = true
            };
            if (dialog.ShowDialog() == true)
            {
                QualcommFirehosePath = dialog.FileName;
            }
        }

        public async Task BrowseUnisocPacAsync()
        {
            var dialog = new OpenFileDialog
            {
                Title = "Pilih Berkas Firmware Unisoc/Spreadtrum (.pac)",
                Filter = "Unisoc/Spreadtrum PAC Firmware (*.pac)|*.pac|All Files (*.*)|*.*",
                CheckFileExists = true
            };
            if (dialog.ShowDialog() == true)
            {
                UnisocPacPath = dialog.FileName;
                AddLog($"[Unisoc PAC] Memilih berkas firmware: {Path.GetFileName(UnisocPacPath)}");

                // Parse PAC manifest and populate partition table
                var manifest = await _unisocPacProtocol.ParsePacHeaderAsync(UnisocPacPath);
                if (manifest.IsValid)
                {
                    FirmwarePartitions.Clear();
                    int idx = 1;
                    foreach (var part in manifest.Partitions)
                    {
                        FirmwarePartitions.Add(new PartitionTableItem
                        {
                            Index = idx++,
                            PartitionName = part.PartitionName,
                            FileName = part.FileName,
                            SizeFormatted = part.FileSizeFormatted,
                            IsSelected = true
                        });
                    }
                    AddLog($"[Unisoc PAC] ✅ Berhasil memetakan {manifest.Partitions.Count} partisi ke tabel flasher.");
                }
            }
        }

        public void BrowseUnisocPac() => _ = BrowseUnisocPacAsync();

        public async Task FlashUnisocPacAsync()
        {
            if (string.IsNullOrEmpty(UnisocPacPath) || !File.Exists(UnisocPacPath))
            {
                AddLog("[Peringatan] Pilih berkas firmware Unisoc (.pac) terlebih dahulu.");
                BrowseUnisocPac();
                if (string.IsNullOrEmpty(UnisocPacPath)) return;
            }

            var targetDevice = Context.SelectedDevice ?? new Device
            {
                DeviceId = "UNISOC_BSL",
                Brand = "Unisoc",
                Model = "Unisoc Phone",
                Mode = DeviceMode.Download,
                Chipset = "Unisoc SPD"
            };

            var result = await Context.Orchestrator.RunOperationAsync(
                targetDevice,
                OperationType.FlashFirmware,
                $"Unisoc PAC Flasher ({Path.GetFileName(UnisocPacPath)})",
                executeAction: async (jobId, ct) =>
                {
                    return await _unisocEngine.FlashPacFirmwareAsync(UnisocPacPath);
                },
                firmwareFilePath: UnisocPacPath,
                requiredToolName: "spd_research");

            Context.CurrentOperation = result.State == OperationState.Completed ? "Flashing PAC Selesai" : $"Flashing PAC Gagal: {result.FailureReason}";
        }

        public async Task SamsungEdlFlashBitAsync()
        {
            var ports = QualcommEdlEngine.ScanQualcommPorts();
            var port = Context.SelectedDevice?.PortName;
            if (string.IsNullOrEmpty(port) || port.Equals("COM_EDL", StringComparison.OrdinalIgnoreCase))
            {
                port = ports.FirstOrDefault()?.PortName;
            }

            if (string.IsNullOrEmpty(port))
            {
                AddLog("[Qualcomm EDL] [GAGAL] Perangkat Qualcomm EDL 9008 tidak terdeteksi di port USB.");
                AddLog("[Petunjuk] Buka backdoor dan sambungkan titik Test Point EDL atau gunakan EDL Cable 9008.");
                Context.CurrentOperation = "Gagal: Port 9008 Tidak Terdeteksi";
                return;
            }

            AddLog($"[Qualcomm EDL] Menyiapkan Flash Bit Loader untuk mode 9008 di port [{port}]...");
            Context.CurrentOperation = "Mengirim Bit Loader via EDL 9008...";
            Context.OperationProgress = 30;

            // Resolve Samsung EDL Loader
            string? programmerPath = null;
            var samsungLoadersDir = Path.Combine(AppPathResolver.FirehoseDirectory, "samsung_edl_loaders");
            if (Directory.Exists(samsungLoadersDir))
            {
                var files = Directory.GetFiles(samsungLoadersDir, "*.*", SearchOption.AllDirectories);
                programmerPath = files.FirstOrDefault(f => f.EndsWith(".mbn", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".elf", StringComparison.OrdinalIgnoreCase));
            }

            var devInfo = await _qualcommEngine.PerformSaharaHandshakeAsync(port, programmerPath);
            if (devInfo != null)
            {
                Context.OperationProgress = 100;
                Context.CurrentOperation = "Flash Bit Selesai";
                AddLog($"[Qualcomm EDL] [SUKSES] Payload Bit Loader berhasil dialirkan ke port [{port}]. Storage: {devInfo.StorageType} (Sahara v{devInfo.SaharaVersion})");
            }
            else
            {
                Context.OperationProgress = 0;
                Context.CurrentOperation = "Gagal Mengirim Bit Loader";
                AddLog($"[Qualcomm EDL] [GAGAL] Sahara handshake gagal pada port [{port}]. Periksa kabel data USB dan driver QDLoader.");
            }
        }

        public void BrowseFirmwareFile()
        {
            var dialog = new OpenFileDialog
            {
                Title = "Pilih Berkas Firmware atau Folder ROM",
                Filter = "Firmware Package (*.zip;*.tar;*.tar.md5;*.bin;*.xml;*.txt)|*.zip;*.tar;*.tar.md5;*.bin;*.xml;*.txt|All Files (*.*)|*.*",
                CheckFileExists = true
            };
            if (dialog.ShowDialog() == true)
            {
                FirmwarePathInput = dialog.FileName;
            }
        }

        public async Task LoadFirmwarePackageAsync(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || (!File.Exists(filePath) && !Directory.Exists(filePath)))
            {
                AddLog("[Flasher] Path firmware tidak valid atau file tidak ditemukan.");
                return;
            }

            AddLog($"[Flasher] Menganalisis paket firmware: {filePath}...");
            await Task.Run(() =>
            {
                var manifest = _firmwareFlasherEngine.AnalyzeFirmwarePackage(filePath);
                System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                {
                    CurrentFirmwareManifest = manifest;
                    FirmwarePartitions.Clear();
                    foreach (var item in manifest.Items) FirmwarePartitions.Add(item);
                });
            });

            AddLog($"[Flasher] Paket firmware dimuat: {CurrentFirmwareManifest.PackageName} ({CurrentFirmwareManifest.Items.Count} Partisi, {CurrentFirmwareManifest.TotalPackageSizeFormatted})");
        }

        private void ToggleAllFirmwarePartitions(bool select)
        {
            foreach (var item in FirmwarePartitions)
            {
                item.IsSelected = select;
            }
        }

        public async Task StartFirmwareFlashAsync()
        {
            if (FirmwarePartitions.Count == 0)
            {
                AddLog("[Flasher Error] Belum ada paket firmware yang dimuat!");
                return;
            }

            var selected = FirmwarePartitions.Where(p => p.IsSelected).ToList();
            if (selected.Count == 0)
            {
                AddLog("[Flasher Error] Centang minimal 1 partisi yang ingin di-flash!");
                return;
            }

            var port = Context.SelectedDevice?.DeviceId ?? "DEVICE_PORT";
            AddLog($"[Flasher] 🚀 Memulai penulisan {selected.Count} partisi firmware ke {port}...");
            Context.CurrentOperation = $"Menulis {selected.Count} partisi firmware...";
            Context.OperationProgress = 0;

            _firmwareFlasherEngine.OnProgress = (p) =>
            {
                Context.OperationProgress = (int)p;
            };

            _firmwareFlasherEngine.OnPartitionFlashingProgress = (partName, p) =>
            {
                ActiveFlashingPartitionName = partName;
                ActivePartitionPercentage = p;
            };

            try
            {
                CurrentFirmwareManifest.Items = FirmwarePartitions.ToList();
                var ok = await _firmwareFlasherEngine.ExecuteFlashingAsync(CurrentFirmwareManifest, port);
                if (ok)
                {
                    Context.CurrentOperation = "Flashing Firmware Berhasil 100%";
                    AddLog("[Flasher] ✅ Seluruh proses flashing selesai dengan sukses!");
                }
            }
            catch (Exception ex)
            {
                AddLog($"[Flasher Error] {ex.Message}");
            }
        }

        #region Auto-ROM Cloud Downloader Logic

        public async Task CheckOfficialRomAsync()
        {
            if (string.IsNullOrWhiteSpace(AutoRomModelInput))
            {
                AutoRomStatusText = "Masukkan Model atau Codename HP terlebih dahulu.";
                AddLog("[Auto-ROM] Harap masukkan Model / Codename.");
                return;
            }

            IsAutoRomFound = false;
            AutoRomFoundVersionInfo = string.Empty;
            AutoRomStatusText = "Menghubungi CDN Server Resmi Pabrikan...";

            if (SelectedAutoRomBrand.Contains("Samsung"))
            {
                var info = await _samsungFusProtocol.CheckLatestFirmwareAsync(AutoRomModelInput, AutoRomRegionInput);
                _lastSamsungInfo = info;

                if (info.IsValid)
                {
                    IsAutoRomFound = true;
                    AutoRomFoundVersionInfo = $"Versi: {info.PdaVersion} | CSC: {info.CscVersion} | {info.BinaryBit} | OS: {info.AndroidVersion}";
                    AutoRomStatusText = $"Ditemukan: {info.EncryptedFileName} ({info.FileSizeFormatted})";
                    AddLog($"[Auto-ROM Samsung] ✅ Firmware resmi ditemukan: {info.FullVersionString}");
                }
                else
                {
                    AutoRomStatusText = info.Message;
                }
            }
            else if (SelectedAutoRomBrand.Contains("Xiaomi"))
            {
                var info = await _xiaomiBigOtaProtocol.CheckLatestFastbootRomAsync(AutoRomModelInput, AutoRomRegionInput);
                _lastXiaomiInfo = info;

                if (info.IsValid)
                {
                    IsAutoRomFound = true;
                    AutoRomFoundVersionInfo = $"Versi: {info.VersionTag} | {info.AndroidVersion} | {info.FileName} ({info.FileSizeFormatted})";
                    AutoRomStatusText = $"Siap Diunduh dari BigOTA ({info.FileSizeFormatted})";
                    AddLog($"[Auto-ROM Xiaomi] ✅ Fastboot ROM resmi ditemukan: {info.DownloadUrl}");
                }
                else
                {
                    AutoRomStatusText = info.Message;
                }
            }
            else if (SelectedAutoRomBrand.Contains("Oppo"))
            {
                var info = await _oppoOtaProtocol.CheckOfficialOppoFirmwareAsync(AutoRomModelInput);
                _lastOppoInfo = info;

                if (info.IsValid)
                {
                    IsAutoRomFound = true;
                    AutoRomFoundVersionInfo = $"Model: {info.ModelSku} | {info.ColorOsVersion} | {info.FileName} ({info.FileSizeFormatted})";
                    AutoRomStatusText = $"Ditemukan di CDN Oppo/Realme ({info.FileSizeFormatted})";
                    AddLog($"[Auto-ROM Oppo] ✅ Firmware resmi ditemukan: {info.DownloadUrl}");
                }
                else
                {
                    AutoRomStatusText = info.Message;
                }
            }
            else if (SelectedAutoRomBrand.Contains("Infinix") || SelectedAutoRomBrand.Contains("Tecno") || SelectedAutoRomBrand.Contains("Itel"))
            {
                var brandName = SelectedAutoRomBrand.Contains("Tecno") ? "Tecno" : SelectedAutoRomBrand.Contains("Itel") ? "Itel" : "Infinix";
                var info = await _transsionProtocol.CheckOfficialFirmwareAsync(AutoRomModelInput, brandName);
                _lastTranssionInfo = info;

                if (info.IsValid)
                {
                    IsAutoRomFound = true;
                    AutoRomFoundVersionInfo = $"Model: {info.ModelSku} | {info.BuildVersion} | {info.FileName} ({info.FileSizeFormatted})";
                    AutoRomStatusText = $"Ditemukan di Server Carlcare ({info.FileSizeFormatted})";
                    AddLog($"[Auto-ROM Transsion] ✅ Firmware resmi ditemukan: {info.DownloadUrl}");
                }
                else
                {
                    AutoRomStatusText = info.Message;
                }
            }
            else if (SelectedAutoRomBrand.Contains("Unisoc"))
            {
                IsAutoRomFound = true;
                AutoRomFoundVersionInfo = $"Model: {AutoRomModelInput} | Format: .PAC Standby | Region: {AutoRomRegionInput}";
                AutoRomStatusText = $"Unisoc PAC Parser Siap untuk {AutoRomModelInput}";
                AddLog($"[Auto-ROM Unisoc] ✅ Siap memproses berkas PAC untuk {AutoRomModelInput}.");
            }
        }

        private async Task<string> ExecuteCliAsync(string fileName, string arguments)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = new Process { StartInfo = psi };
                process.Start();
                var output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();
                return output;
            }
            catch
            {
                return string.Empty;
            }
        }

        public async Task AutoDetectDeviceForRomAsync()
        {
            AddLog("╔════════════════════════════════════════════════════════════════╗");
            AddLog("║       AUTO-DETECT DEVICE -> AUTO-FILL OFFICIAL ROM FORM        ║");
            AddLog("╚════════════════════════════════════════════════════════════════╝");

            AutoRomStatusText = "Memindai perangkat yang terhubung (MTP / ADB / Fastboot)...";

            var toolsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools");
            var adbPath = Path.Combine(toolsPath, "platform-tools", "adb.exe");
            var fastbootPath = Path.Combine(toolsPath, "platform-tools", "fastboot.exe");

            try
            {
                // 1. Coba deteksi via Fastboot
                if (File.Exists(fastbootPath))
                {
                    var fbDevices = await ExecuteCliAsync(fastbootPath, "devices");
                    if (!string.IsNullOrWhiteSpace(fbDevices) && fbDevices.Contains("fastboot"))
                    {
                        var product = await ExecuteCliAsync(fastbootPath, "getvar product");
                        var match = Regex.Match(product, @"product:\s*([^\r\n]+)", RegexOptions.IgnoreCase);
                        if (match.Success)
                        {
                            var codename = match.Groups[1].Value.Trim().ToLowerInvariant();
                            SelectedAutoRomBrand = "Xiaomi / Redmi / POCO (Official BigOTA)";
                            AutoRomModelInput = codename;
                            AutoRomRegionInput = "id";
                            AddLog($"[Auto-Detect Fastboot] ✅ Terdeteksi Xiaomi Codename: [{codename}]");
                            await CheckOfficialRomAsync();
                            return;
                        }
                    }
                }

                // 2. Coba deteksi via ADB
                if (File.Exists(adbPath))
                {
                    var adbDevices = await ExecuteCliAsync(adbPath, "devices");
                    if (adbDevices.Contains("device") && !adbDevices.Trim().Equals("List of devices attached"))
                    {
                        var brand = (await ExecuteCliAsync(adbPath, "shell getprop ro.product.brand")).Trim();
                        var model = (await ExecuteCliAsync(adbPath, "shell getprop ro.product.model")).Trim();
                        var csc = (await ExecuteCliAsync(adbPath, "shell getprop ro.csc.sales_code")).Trim();

                        if (brand.Contains("samsung", StringComparison.OrdinalIgnoreCase))
                        {
                            SelectedAutoRomBrand = "Samsung Galaxy (Official FUS)";
                            AutoRomModelInput = model;
                            AutoRomRegionInput = string.IsNullOrWhiteSpace(csc) ? "XID" : csc.ToUpperInvariant();
                            AddLog($"[Auto-Detect ADB] ✅ Terdeteksi Samsung: {model} (CSC: {AutoRomRegionInput})");
                            await CheckOfficialRomAsync();
                            return;
                        }
                        else if (brand.Contains("xiaomi", StringComparison.OrdinalIgnoreCase) || brand.Contains("redmi", StringComparison.OrdinalIgnoreCase) || brand.Contains("poco", StringComparison.OrdinalIgnoreCase))
                        {
                            var device = (await ExecuteCliAsync(adbPath, "shell getprop ro.product.device")).Trim();
                            SelectedAutoRomBrand = "Xiaomi / Redmi / POCO (Official BigOTA)";
                            AutoRomModelInput = string.IsNullOrWhiteSpace(device) ? model : device;
                            AutoRomRegionInput = "id";
                            AddLog($"[Auto-Detect ADB] ✅ Terdeteksi Xiaomi: {AutoRomModelInput}");
                            await CheckOfficialRomAsync();
                            return;
                        }
                        else if (brand.Contains("oppo", StringComparison.OrdinalIgnoreCase) || brand.Contains("realme", StringComparison.OrdinalIgnoreCase))
                        {
                            SelectedAutoRomBrand = "Oppo / Realme (Official OTA)";
                            AutoRomModelInput = model;
                            AutoRomRegionInput = "Global";
                            AddLog($"[Auto-Detect ADB] ✅ Terdeteksi Oppo/Realme: {model}");
                            await CheckOfficialRomAsync();
                            return;
                        }
                        else if (brand.Contains("infinix", StringComparison.OrdinalIgnoreCase) || brand.Contains("tecno", StringComparison.OrdinalIgnoreCase) || brand.Contains("itel", StringComparison.OrdinalIgnoreCase))
                        {
                            SelectedAutoRomBrand = "Infinix / Tecno / Itel (Official Carlcare)";
                            AutoRomModelInput = model;
                            AutoRomRegionInput = "GL";
                            AddLog($"[Auto-Detect ADB] ✅ Terdeteksi Transsion: {model}");
                            await CheckOfficialRomAsync();
                            return;
                        }
                    }
                }

                // 3. Coba deteksi via Samsung AT Modem Port
                var modemPorts = SamsungAtEngine.ScanSamsungPorts();
                if (modemPorts.Count > 0)
                {
                    var samInfo = await _samsungEngine.ReadDeviceInfoAsync(modemPorts[0].PortName);
                    if (samInfo != null && !string.IsNullOrEmpty(samInfo.Model))
                    {
                        SelectedAutoRomBrand = "Samsung Galaxy (Official FUS)";
                        AutoRomModelInput = samInfo.Model;
                        AutoRomRegionInput = !string.IsNullOrEmpty(samInfo.Csc) ? samInfo.Csc : "XID";
                        AddLog($"[Auto-Detect Modem] ✅ Terdeteksi Samsung: {samInfo.Model} (CSC: {AutoRomRegionInput})");
                        await CheckOfficialRomAsync();
                        return;
                    }
                }

                AutoRomStatusText = "Tidak ada perangkat USB terdeteksi. Silakan pilih brand & ketik manual.";
                AddLog("[Auto-Detect] Tidak ada perangkat USB aktif terdeteksi (ADB/Fastboot/Modem).");
            }
            catch (Exception ex)
            {
                AutoRomStatusText = $"Gagal auto-detect: {ex.Message}";
                AddLog($"[Auto-Detect Error] {ex.Message}");
            }
        }

        public async Task StartDownloadAutoRomAsync()
        {
            if (!IsAutoRomFound)
            {
                AddLog("[Auto-ROM] Periksa versi firmware terlebih dahulu sebelum mengunduh.");
                return;
            }

            _autoRomCts = new CancellationTokenSource();
            IsAutoRomDownloading = true;
            AutoRomDownloadProgress = 0;
            AutoRomSpeedText = "Memulai...";

            var brandFolder = SelectedAutoRomBrand.Contains("Samsung") ? "Samsung" :
                              SelectedAutoRomBrand.Contains("Xiaomi") ? "Xiaomi" :
                              SelectedAutoRomBrand.Contains("Oppo") ? "Oppo" :
                              SelectedAutoRomBrand.Contains("Unisoc") ? "Unisoc" : "Transsion";

            var baseFirmwareDir = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "storage",
                "firmwares",
                brandFolder,
                AutoRomModelInput.ToUpperInvariant());

            if (!Directory.Exists(baseFirmwareDir)) Directory.CreateDirectory(baseFirmwareDir);

            var progressReporter = new Progress<DownloadProgressInfo>(report =>
            {
                AutoRomDownloadProgress = report.ProgressPercentage;
                AutoRomSpeedText = report.SpeedFormatted;
                AutoRomStatusText = report.StatusText;
            });

            try
            {
                if (SelectedAutoRomBrand.Contains("Samsung") && _lastSamsungInfo != null)
                {
                    AddLog($"[Auto-ROM] Memulai unduhan Samsung ROM ({_lastSamsungInfo.Model}_{_lastSamsungInfo.Csc})...");
                    
                    var dlUrl = !string.IsNullOrEmpty(_lastSamsungInfo.DownloadUrl) 
                        ? _lastSamsungInfo.DownloadUrl 
                        : $"http://fota-cloud-dn.ospserver.net/firmware/{_lastSamsungInfo.Csc}/{_lastSamsungInfo.Model}/{_lastSamsungInfo.EncryptedFileName}";
                    
                    var targetFile = Path.Combine(baseFirmwareDir, _lastSamsungInfo.EncryptedFileName);
                    var ok = await _autoRomDownloader.DownloadFileWithResumeAsync(dlUrl, targetFile, progressReporter, _autoRomCts.Token);

                    if (ok && File.Exists(targetFile))
                    {
                        var zipToExtract = targetFile;

                        // 1. Dekripsi AES-128/256-ECB jika berkas terenkripsi Samsung .enc4 / .enc2
                        if (targetFile.EndsWith(".enc4", StringComparison.OrdinalIgnoreCase) || targetFile.EndsWith(".enc2", StringComparison.OrdinalIgnoreCase))
                        {
                            AutoRomStatusText = "Mendekripsi paket firmware Samsung (AES-128-ECB)...";
                            AddLog($"[Auto-ROM Samsung] 🔓 Mendekripsi berkas {Path.GetFileName(targetFile)}...");

                            var decryptedZip = targetFile.Substring(0, targetFile.LastIndexOf(".enc", StringComparison.OrdinalIgnoreCase));
                            if (!decryptedZip.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) decryptedZip += ".zip";

                            using (var encStream = File.OpenRead(targetFile))
                            using (var decStream = File.Create(decryptedZip))
                            {
                                var decryptOk = await _samsungFusProtocol.DecryptEncryptedFirmwareStreamAsync(
                                    encStream,
                                    decStream,
                                    _lastSamsungInfo.LogicCheck,
                                    _lastSamsungInfo.Model,
                                    _lastSamsungInfo.Csc,
                                    null,
                                    _autoRomCts.Token);

                                if (decryptOk)
                                {
                                    zipToExtract = decryptedZip;
                                    AddLog($"[Auto-ROM Samsung] ✅ Dekripsi berhasil: {Path.GetFileName(decryptedZip)}");
                                }
                                else
                                {
                                    AddLog("[Auto-ROM Samsung Error] Gagal mendekripsi berkas .enc4.");
                                }
                            }
                        }

                        // 2. Ekstraksi arsip ZIP hasil dekripsi ke folder penyimpanan
                        AutoRomStatusText = "Mengekstrak arsip firmware...";
                        if (File.Exists(zipToExtract) && zipToExtract.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                        {
                            await _firmwareExtractorEngine.ExtractZipArchiveAsync(zipToExtract, baseFirmwareDir, null, _autoRomCts.Token);
                        }

                        // 3. Auto-detect 4 berkas paket Odin: BL, AP, CP, CSC
                        var apFile = Directory.GetFiles(baseFirmwareDir, "AP_*.tar*").FirstOrDefault();
                        if (!string.IsNullOrEmpty(apFile))
                        {
                            SetAndAutoDetectOdinCompanionFiles("AP", apFile);
                        }
                        else
                        {
                            SetAndAutoDetectOdinCompanionFiles("AP", zipToExtract);
                        }

                        // 4. Auto-extract PIT dari CSC jika tersedia
                        var cscFile = Directory.GetFiles(baseFirmwareDir, "CSC_*.tar*").FirstOrDefault();
                        if (!string.IsNullOrEmpty(cscFile))
                        {
                            var pitDestDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "database", "pit", "Samsung");
                            await _firmwareExtractorEngine.ExtractPitFromTarMd5Async(cscFile, pitDestDir, _autoRomCts.Token);
                        }

                        SelectedFlasherTabIndex = 0; // Beralih ke tab Odin
                        AddLog("[Auto-ROM] ✅ Firmware Samsung resmi berhasil didekripsi, diekstrak & dipasang ke 4 Slot Odin!");
                    }
                }
                else if (SelectedAutoRomBrand.Contains("Xiaomi") && _lastXiaomiInfo != null)
                {
                    AddLog($"[Auto-ROM] Memulai unduhan Xiaomi Fastboot TGZ ({_lastXiaomiInfo.Codename} - {_lastXiaomiInfo.VersionTag})...");
                    var targetFile = Path.Combine(baseFirmwareDir, _lastXiaomiInfo.FileName);

                    var ok = await _autoRomDownloader.DownloadFileWithResumeAsync(_lastXiaomiInfo.DownloadUrl, targetFile, progressReporter, _autoRomCts.Token);

                    if (ok && File.Exists(targetFile))
                    {
                        AutoRomStatusText = "Unduhan selesai. Mengekstrak paket Fastboot TGZ...";
                        
                        if (targetFile.EndsWith(".tgz", StringComparison.OrdinalIgnoreCase) || targetFile.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase))
                        {
                            await _firmwareExtractorEngine.ExtractTarGzAsync(targetFile, baseFirmwareDir, null, _autoRomCts.Token);
                        }

                        // Deteksi subfolder "images" jika diekstrak bersarang
                        var imagesDir = Directory.GetDirectories(baseFirmwareDir, "images", SearchOption.AllDirectories).FirstOrDefault();
                        var targetLoadDir = imagesDir != null ? Directory.GetParent(imagesDir)?.FullName ?? baseFirmwareDir : baseFirmwareDir;

                        await LoadFirmwarePackageAsync(targetLoadDir);
                        SelectedFlasherTabIndex = 4; // Beralih ke tab Fastboot
                        AddLog($"[Auto-ROM] ✅ Fastboot ROM Xiaomi resmi berhasil diekstrak & dimuat ke tabel partisi dari [{Path.GetFileName(targetLoadDir)}]!");
                    }
                }
                else if (SelectedAutoRomBrand.Contains("Oppo") && _lastOppoInfo != null)
                {
                    AddLog($"[Auto-ROM] Memulai unduhan Oppo/Realme Firmware ({_lastOppoInfo.ModelSku})...");
                    var targetFile = Path.Combine(baseFirmwareDir, _lastOppoInfo.FileName);

                    var ok = await _autoRomDownloader.DownloadFileWithResumeAsync(_lastOppoInfo.DownloadUrl, targetFile, progressReporter, _autoRomCts.Token);

                    if (ok && File.Exists(targetFile))
                    {
                        AutoRomStatusText = "Unduhan selesai. Membaca struktur paket...";
                        await LoadFirmwarePackageAsync(targetFile);
                        AddLog("[Auto-ROM] ✅ Firmware Oppo/Realme resmi berhasil dimuat!");
                    }
                }
                else if ((SelectedAutoRomBrand.Contains("Infinix") || SelectedAutoRomBrand.Contains("Tecno") || SelectedAutoRomBrand.Contains("Itel") || SelectedAutoRomBrand.Contains("Transsion")) && _lastTranssionInfo != null)
                {
                    AddLog($"[Auto-ROM] Memulai unduhan Transsion Firmware ({_lastTranssionInfo.ModelSku})...");
                    var targetFile = Path.Combine(baseFirmwareDir, _lastTranssionInfo.FileName);

                    var ok = await _autoRomDownloader.DownloadFileWithResumeAsync(_lastTranssionInfo.DownloadUrl, targetFile, progressReporter, _autoRomCts.Token);

                    if (ok && File.Exists(targetFile))
                    {
                        AutoRomStatusText = "Unduhan selesai. Membaca firmware...";
                        await LoadFirmwarePackageAsync(targetFile);
                        AddLog("[Auto-ROM] ✅ Firmware Transsion resmi berhasil dimuat!");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                AutoRomStatusText = "Unduhan dibatalkan.";
                AddLog("[Auto-ROM] Unduhan dibatalkan oleh pengguna.");
            }
            catch (Exception ex)
            {
                AutoRomStatusText = $"Gagal mengunduh: {ex.Message}";
                AddLog($"[Auto-ROM Error] {ex.Message}");
            }
            finally
            {
                IsAutoRomDownloading = false;
                _autoRomCts = null;
            }
        }

        public void CancelDownloadAutoRom()
        {
            if (_autoRomCts != null && !_autoRomCts.IsCancellationRequested)
            {
                _autoRomCts.Cancel();
                AddLog("[Auto-ROM] Mengirim sinyal pembatalan unduhan...");
            }
        }

        #endregion
    }
}

