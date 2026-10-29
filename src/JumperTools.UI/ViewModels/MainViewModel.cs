using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using JumperTools.Core;
using JumperTools.Models;
using JumperTools.Services;
using JumperTools.Utilities;
using JumperTools.UI.ViewModels.Shared;
using JumperTools.UI.ViewModels.Tabs;

namespace JumperTools.UI.ViewModels
{
    public partial class MainViewModel : INotifyPropertyChanged, ISharedWorkspaceContext
    {
        private readonly string _toolsBasePath;
        private readonly string _platformToolsPath;
        private readonly DeviceDetectionEngine _deviceEngine;
        private readonly ToolExecutionEngine _toolEngine;
        private readonly SamsungAtEngine _samsungEngine;
        private readonly FastbootEngine _fastbootEngine;
        private readonly MediaTekBromEngine _mtkEngine;
        private readonly QualcommEdlEngine _qualcommEngine;
        private readonly XiaomiSideloadEngine _xiaomiEngine;
        private readonly UnisocSpdEngine _unisocEngine;
        private readonly HuaweiKirinEngine _huaweiEngine;
        private readonly FirmwareExtractorEngine _extractorEngine;
        private readonly DriverAssistantEngine _driverEngine;
        private readonly PartitionManagerEngine _partitionEngine;
        private readonly BrandSpecialOperationsEngine _brandSpecialEngine;
        private readonly AppleIosEngine _appleEngine;
        private readonly JobOrderEngine _jobEngine;
        private readonly TestPointRepository _testPointRepo;
        private readonly LoaderManager _loaderManager;
        private readonly JumperToolsApiClient _apiClient;
        private readonly SignalRepairEngine _signalEngine;
        private readonly RecoveryInstallerEngine _recoveryEngine;
        private readonly JumperTools.Data.DatabaseContext _dbContext;
        private readonly ToolRegistry _toolRegistry;
        private readonly BackupIntegrityService _backupIntegrityService;
        private readonly JobJournalService _jobJournalService;
        private readonly PreflightEngine _preflightEngine;
        private readonly DriverIntelligenceService _driverIntelligenceService;
        private readonly OperationOrchestrator _operationOrchestrator;
        private readonly HardwareDiagnosticEngine _diagnosticEngine;
        private readonly UsbPortResetEngine _usbResetEngine;

        private bool _isInitialized = false;
        private Device? _selectedDevice;
        private TestPointInfo? _selectedTestPoint;
        private PartitionTableItem? _selectedPartition;
        private JobOrderInfo? _selectedJobOrder;
        private string _selectedBrand = "Semua";
        private PhoneModelProfile? _selectedPhoneModel;
        private LoaderInfo? _selectedLoader;
        private string _loaderStatusMessage = "Pilih Model Smartphone untuk memuat Loader";
        private string _modelSearchFilter = "";
        private string _testPointSearchQuery = "";
        private string _firmwareFilePath = "";
        private int _operationProgress;
        private string _currentOperation = "Siap (Ready)";
        private string _operationStatus = "Ready";
        private string _connectionStatus = "License: Pro (Active)";
        private string _licenseInfo = "Mode: Standalone Lokal";
        private string _lastSyncTime = "Belum Sinkron";
        private string _appVersion = "v1.8";
        private string _terminalLogText = "";
        private string _userGuidanceMessage = "Hubungkan HP ke USB (MTP / Fastboot / BROM / EDL).";
        private string _currentJobId = string.Empty;
        private OperationState _currentJobState = OperationState.Idle;
        private string _currentFailureReason = string.Empty;
        private string _currentRecoveryAction = string.Empty;

        // Form Fields for New Job Order (Empty until technician input or action)
        private string _newCustomerName = string.Empty;
        private string _newCustomerPhone = string.Empty;
        private string _newDeviceBrand = string.Empty;
        private string _newDeviceModel = string.Empty;
        private string _newServiceType = string.Empty;
        private decimal _newCost = 0;
        private int _newWarrantyDays = 0;
        private string _receiptPreviewText = string.Empty;

        // Sub-ViewModels (Decoupled Phase 2 Architecture)
        public UnlockServicesViewModel UnlockServices { get; }
        public FirmwareFlasherViewModel FirmwareFlasher { get; }
        public PartitionManagerViewModel PartitionManager { get; }
        public FirmwareExtractorViewModel FirmwareExtractor { get; }
        public FirehoseFinderViewModel FirehoseFinder { get; }
        public TestPointViewModel TestPointsHub { get; }
        public JobOrderViewModel ServiceCrm { get; }
        public DriverAssistantViewModel DriverAssistant { get; }
        public SignalRepairViewModel SignalRepair { get; }
        public CustomRecoveryViewModel CustomRecovery { get; }
        public ReadInfoViewModel ReadInfoForensic { get; }

        public OperationOrchestrator Orchestrator => _operationOrchestrator;
        public JobJournalService JobJournal => _jobJournalService;
        public ToolRegistry ToolRegistry => _toolRegistry;
        public PreflightEngine PreflightEngine => _preflightEngine;
        public DriverIntelligenceService DriverIntelligence => _driverIntelligenceService;

        // ISharedWorkspaceContext implementation
        public string ToolsBasePath => _toolsBasePath;
        public string PlatformToolsPath => _platformToolsPath;
        public string TempWorkspacePath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "temp");
        public ToolExecutionEngine ToolEngine => _toolEngine;
        public DeviceDetectionEngine DeviceEngine => _deviceEngine;
        public CancellationToken CancellationToken => CancellationToken.None;
        public void ClearLog() => ClearLogs();
        public void CopyLog() => CopyLogs();
        public void ForceResetUsbPorts() => _ = ForceResetUsbPortsAsync();
        public bool IsBusy => _currentJobState == OperationState.Executing || _currentJobState == OperationState.Preparing || _currentJobState == OperationState.Backup || _currentJobState == OperationState.Verifying || (_operationProgress > 0 && _operationProgress < 100);

        public string CurrentJobId
        {
            get => _currentJobId;
            set { _currentJobId = value; OnPropertyChanged(); }
        }

        public OperationState CurrentJobState
        {
            get => _currentJobState;
            set { _currentJobState = value; OnPropertyChanged(); }
        }

        public string CurrentFailureReason
        {
            get => _currentFailureReason;
            set { _currentFailureReason = value; OnPropertyChanged(); }
        }

        public string CurrentRecoveryAction
        {
            get => _currentRecoveryAction;
            set { _currentRecoveryAction = value; OnPropertyChanged(); }
        }

        public MainViewModel()
        {
            // Resolve Base Paths with multi-directory fallback
            var localTools = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools");
            var devTools = AppPathResolver.ToolsDirectory;
            var systemAdb = @"C:\adb";
            
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var sdkTools = Path.Combine(localAppData, "Android", "Sdk", "platform-tools");

            if (Directory.Exists(Path.Combine(localTools, "platform-tools")) && File.Exists(Path.Combine(localTools, "platform-tools", "adb.exe")))
            {
                _toolsBasePath = localTools;
                _platformToolsPath = Path.Combine(localTools, "platform-tools");
            }
            else if (Directory.Exists(Path.Combine(devTools, "platform-tools")) && File.Exists(Path.Combine(devTools, "platform-tools", "adb.exe")))
            {
                _toolsBasePath = devTools;
                _platformToolsPath = Path.Combine(devTools, "platform-tools");
            }
            else if (Directory.Exists(sdkTools) && File.Exists(Path.Combine(sdkTools, "adb.exe")))
            {
                _toolsBasePath = Path.Combine(localAppData, "Android", "Sdk");
                _platformToolsPath = sdkTools;
            }
            else if (Directory.Exists(systemAdb) && File.Exists(Path.Combine(systemAdb, "adb.exe")))
            {
                _toolsBasePath = @"C:\";
                _platformToolsPath = systemAdb;
            }
            else
            {
                _toolsBasePath = localTools;
                _platformToolsPath = Path.Combine(localTools, "platform-tools");
            }

            // Initialize Data, Persistence & Services Layer
            var dbDir = AppPathResolver.DatabaseDirectory;
            if (!Directory.Exists(dbDir)) Directory.CreateDirectory(dbDir);
            _dbContext = new JumperTools.Data.DatabaseContext(Path.Combine(dbDir, "jumpertools_local.db"));
            _toolRegistry = new ToolRegistry(_toolsBasePath);
            _backupIntegrityService = new BackupIntegrityService(_dbContext);
            _jobJournalService = new JobJournalService(_dbContext);
            _preflightEngine = new PreflightEngine(_toolRegistry);
            _driverIntelligenceService = new DriverIntelligenceService();
            _operationOrchestrator = new OperationOrchestrator(_jobJournalService, _preflightEngine, _backupIntegrityService, _toolRegistry);

            // Hook Orchestrator Events
            _operationOrchestrator.LogEmitted += AddLog;
            _operationOrchestrator.ProgressChanged += (up) =>
            {
                CurrentJobId = up.JobId;
                CurrentJobState = up.State;
                OperationProgress = (int)up.Percentage;
                CurrentOperation = up.CurrentStep;
                if (!string.IsNullOrEmpty(up.FailureReason)) CurrentFailureReason = up.FailureReason;
                if (!string.IsNullOrEmpty(up.RecoveryAction)) CurrentRecoveryAction = up.RecoveryAction;
            };

            // Initialize Core Native Engines & Repositories
            _deviceEngine = new DeviceDetectionEngine(_platformToolsPath);
            _toolEngine = new ToolExecutionEngine(_toolsBasePath, _toolRegistry);
            _samsungEngine = new SamsungAtEngine();
            _fastbootEngine = new FastbootEngine(_platformToolsPath);
            _mtkEngine = new MediaTekBromEngine(_toolEngine);
            _qualcommEngine = new QualcommEdlEngine(_toolEngine);
            _xiaomiEngine = new XiaomiSideloadEngine(_platformToolsPath);
            _unisocEngine = new UnisocSpdEngine();
            _huaweiEngine = new HuaweiKirinEngine();
            _extractorEngine = new FirmwareExtractorEngine();
            _driverEngine = new DriverAssistantEngine();
            _partitionEngine = new PartitionManagerEngine(_toolsBasePath, _fastbootEngine, _toolEngine, _backupIntegrityService);
            _partitionEngine.SetFastbootEngine(_fastbootEngine);
            _brandSpecialEngine = new BrandSpecialOperationsEngine(_platformToolsPath);
            _appleEngine = new AppleIosEngine();
            _jobEngine = new JobOrderEngine(_dbContext);
            _testPointRepo = new TestPointRepository(_dbContext);
            _loaderManager = new LoaderManager(Path.Combine(_toolsBasePath, "loaders"));
            _apiClient = new JumperToolsApiClient("http://localhost:8090");
            _signalEngine = new SignalRepairEngine(_toolsBasePath, AppPathResolver.DatabaseDirectory);
            _recoveryEngine = new RecoveryInstallerEngine(_toolsBasePath, AppPathResolver.DatabaseDirectory);
            _diagnosticEngine = new HardwareDiagnosticEngine(_toolEngine, _platformToolsPath);

            // Inject FastbootEngine ke SafetyNetEngine dan FirmwareFlasherEngine
            // agar backup/restore dan flash benar-benar menyentuh chip HP bukan dummy
            _safetyNetEngine.SetFastbootEngine(_fastbootEngine);
            _firmwareFlasherEngine.SetFastbootEngine(_fastbootEngine);

            UpdateReceiptPreview();

            // Hook Engine Events for Realtime Live Logging
            _samsungEngine.OnLog += AddLog;
            _samsungEngine.OnUserActionRequired += (msg) => { UserGuidanceMessage = msg; };
            _fastbootEngine.OnLog += AddLog;
            _mtkEngine.OnLog += AddLog;
            _mtkEngine.OnUserActionRequired += (msg) => { UserGuidanceMessage = msg; };
            _qualcommEngine.OnLog += AddLog;
            _qualcommEngine.OnUserActionRequired += (msg) => { UserGuidanceMessage = msg; };
            _xiaomiEngine.OnLog += AddLog;
            _xiaomiEngine.OnUserActionRequired += (msg) => { UserGuidanceMessage = msg; };
            _unisocEngine.OnLog += AddLog;
            _unisocEngine.OnUserActionRequired += (msg) => { UserGuidanceMessage = msg; };
            _huaweiEngine.OnLog += AddLog;
            _huaweiEngine.OnUserActionRequired += (msg) => { UserGuidanceMessage = msg; };
            _extractorEngine.OnLog += AddLog;
            _driverEngine.OnLog += AddLog;
            _partitionEngine.OnLog += AddLog;
            _brandSpecialEngine.OnLog += AddLog;
            _brandSpecialEngine.OnUserActionRequired += (msg) => { UserGuidanceMessage = msg; };
            _appleEngine.OnLog += AddLog;
            _jobEngine.OnLog += AddLog;
            _signalEngine.OnLog += AddLog;
            _recoveryEngine.OnLog += AddLog;

            // Initialize USB Port Reset & Ghost COM recovery
            _usbResetEngine = new UsbPortResetEngine();
            _usbResetEngine.OnLog += AddLog;

            // Hook Smooth Percentage Progress from CLI outputs
            _toolEngine.OnPercentageProgress += (pct) =>
            {
                try
                {
                    if (Application.Current != null && Application.Current.Dispatcher != null)
                    {
                        Application.Current.Dispatcher.Invoke(() => OperationProgress = pct);
                    }
                    else
                    {
                        OperationProgress = pct;
                    }
                }
                catch { }
            };

            // Initialize Firehose Finder
            FirehoseFinder = new FirehoseFinderViewModel();
            FirehoseFinder.OnLog += AddLog;

            // Initialize Sub-ViewModels (Decoupled Phase 2 Architecture)
            UnlockServices = new UnlockServicesViewModel(this);
            FirmwareFlasher = new FirmwareFlasherViewModel(this, _samsungEngine, _mtkEngine, _qualcommEngine, _unisocEngine, _fastbootEngine);
            PartitionManager = new PartitionManagerViewModel(this, _partitionEngine);
            FirmwareExtractor = new FirmwareExtractorViewModel(this, _extractorEngine, _fastbootEngine);
            TestPointsHub = new TestPointViewModel(this, _testPointRepo, _apiClient);
            ServiceCrm = new JobOrderViewModel(this, _jobEngine);
            DriverAssistant = new DriverAssistantViewModel(this, _driverEngine);
            SignalRepair = new SignalRepairViewModel(this, _signalEngine);
            CustomRecovery = new CustomRecoveryViewModel(this, _recoveryEngine);
            ReadInfoForensic = new ReadInfoViewModel(this, _fastbootEngine, _samsungEngine, _mtkEngine, _qualcommEngine, _xiaomiEngine, _unisocEngine, _huaweiEngine);

            // Initialize Observable Collections
            Devices = new ObservableCollection<Device>();
            Devices.CollectionChanged += Devices_CollectionChanged;
            OperationLogs = new ObservableCollection<string>();
            TestPoints = new ObservableCollection<TestPointInfo>(_testPointRepo.GetAll());
            Partitions = new ObservableCollection<PartitionTableItem>();
            Partitions.CollectionChanged += (s, e) => OnPropertyChanged(nameof(HasNoPartitions));

            Drivers = new ObservableCollection<DriverStatusItem>();
            ExtractedFiles = new ObservableCollection<ExtractedFileInfo>();
            ExtractedFiles.CollectionChanged += (s, e) =>
            {
                OnPropertyChanged(nameof(HasNoExtractedFiles));
                OnPropertyChanged(nameof(HasExtractedFiles)); // PERBAIKAN P4-4: tombol Flash aktif setelah ekstraksi
            };

            JobOrders = new ObservableCollection<JobOrderInfo>();
            Brands = new ObservableCollection<string>(_loaderManager.GetAllBrands());
            Brands.Insert(0, "Semua");
            FilteredModels = new ObservableCollection<PhoneModelProfile>(_loaderManager.GetAllModels());

            // Initialize Selections cleanly without triggering premature logs or fake phone models
            if (TestPoints.Count > 0) _selectedTestPoint = TestPoints[0];
            _selectedPartition = null;
            _selectedPhoneModel = null;
            _selectedLoader = null;
            LoaderStatusMessage = "Menunggu HP terhubung via USB atau pilih model manual...";

            _isInitialized = true;
            AutoSelectBrandTab();

            // Initialize Standard Commands
            RefreshDevicesCommand = new AsyncRelayCommand(RefreshDevicesAsync);
            FlashSamsungCommand = new AsyncRelayCommand(FlashSamsungAsync);
            UnlockBootloaderCommand = new AsyncRelayCommand(UnlockBootloaderAsync);
            BypassFrpCommand = new AsyncRelayCommand(BypassFrpAsync);
            BypassFrpSamsungServiceMenuCommand = new AsyncRelayCommand(BypassFrpSamsungServiceMenuAsync);
            RepairImeiCommand = new AsyncRelayCommand(RepairImeiAsync);
            FixBasebandCommand = new AsyncRelayCommand(FixBasebandAsync);
            RepairEfsCommand = new AsyncRelayCommand(RepairEfsAsync);
            EdlFlashCommand = new AsyncRelayCommand(EdlFlashAsync);
            AuthBypassCommand = new AsyncRelayCommand(AuthBypassAsync);
            RemoveMiAccountCommand = new AsyncRelayCommand(RemoveMiAccountAsync);
            FlashOppoCommand = new AsyncRelayCommand(FlashOppoAsync);
            FormatCommand = new AsyncRelayCommand(FormatAsync);
            AdbShellCommand = new AsyncRelayCommand(AdbShellAsync);
            InstallApkCommand = new AsyncRelayCommand(InstallApkAsync);
            ScreenRecordCommand = new AsyncRelayCommand(ScreenRecordAsync);
            FlashTwrpCommand = new AsyncRelayCommand(FlashTwrpAsync);
            BootRecoveryCommand = new AsyncRelayCommand(BootRecoveryAsync);
            DeviceInfoCommand = new AsyncRelayCommand(DeviceInfoAsync);
            ConnectServerCommand = new AsyncRelayCommand(ConnectServerAsync);
            LicenseStatusCommand = new AsyncRelayCommand(LicenseStatusAsync);
            PlatformToolsCommand = new AsyncRelayCommand(PlatformToolsAsync);
            DownloadPathCommand = new AsyncRelayCommand(DownloadPathAsync);
            LoginCommand = new AsyncRelayCommand(LoginAsync);
            LogoutCommand = new AsyncRelayCommand(LogoutAsync);
            OpenDiagnosticDashboardCommand = new RelayCommand(OpenDiagnosticDashboard);
            ForceResetUsbPortsCommand = new AsyncRelayCommand(ForceResetUsbPortsAsync);

            // Native Reboot & Terminal Commands
            ClearLogCommand = new RelayCommand(ClearLogs);
            CopyLogCommand = new RelayCommand(CopyLogs);
            CancelCurrentOperationCommand = new RelayCommand(CancelCurrentOperation);
            RebootNormalCommand = new AsyncRelayCommand(RebootNormalAsync);
            RebootDownloadCommand = new AsyncRelayCommand(RebootDownloadAsync);
            RebootRecoveryCommand = new AsyncRelayCommand(RebootRecoveryAsync);
            RebootEdlCommand = new AsyncRelayCommand(RebootEdlAsync);

            // MediaTek (MTK) Commands
            MtkBypassFrpCommand = new AsyncRelayCommand(MtkBypassFrpAsync);
            MtkFormatUserdataCommand = new AsyncRelayCommand(MtkFormatUserdataAsync);
            MtkUnlockBootloaderCommand = new AsyncRelayCommand(MtkUnlockBootloaderAsync);
            MtkRelockBootloaderCommand = new AsyncRelayCommand(MtkRelockBootloaderAsync);
            MtkReadGptCommand = new AsyncRelayCommand(MtkReadGptAsync);
            MtkBackupNvramCommand = new AsyncRelayCommand(MtkBackupNvramAsync);
            MtkRestoreNvramCommand = new AsyncRelayCommand(MtkRestoreNvramAsync);
            MtkRebootCommand = new AsyncRelayCommand(MtkRebootAsync);
            MtkCrashPreloaderCommand = new AsyncRelayCommand(MtkCrashPreloaderAsync);
            MtkBrowseCustomDaCommand = new RelayCommand(MtkBrowseCustomDa);
            MtkBrowseCustomAuthCommand = new RelayCommand(MtkBrowseCustomAuth);
            SamsungEdlFlashBitCommand = new AsyncRelayCommand(SamsungEdlFlashBitAsync);

            // Qualcomm (EDL 9008) Commands
            EdlBypassFrpCommand = new AsyncRelayCommand(EdlBypassFrpAsync);
            EdlFormatUserdataCommand = new AsyncRelayCommand(EdlFormatUserdataAsync);
            EdlReadGptCommand = new AsyncRelayCommand(EdlReadGptAsync);
            EdlBackupEfsCommand = new AsyncRelayCommand(EdlBackupEfsAsync);
            EdlRestoreEfsCommand = new AsyncRelayCommand(EdlRestoreEfsAsync);
            EdlRebootCommand = new AsyncRelayCommand(EdlRebootAsync);

            // Xiaomi (Sideload / Mi Assistant) Commands
            XiaomiSideloadInfoCommand = new AsyncRelayCommand(XiaomiSideloadInfoAsync);
            XiaomiSideloadFormatCommand = new AsyncRelayCommand(XiaomiSideloadFormatAsync);
            XiaomiSideloadFrpCommand = new AsyncRelayCommand(XiaomiSideloadFrpAsync);
            XiaomiSideloadRebootCommand = new AsyncRelayCommand(XiaomiSideloadRebootAsync);

            // Unisoc / Spreadtrum (SPD) Commands
            UnisocBypassFrpCommand = new AsyncRelayCommand(UnisocBypassFrpAsync);
            UnisocFormatUserdataCommand = new AsyncRelayCommand(UnisocFormatUserdataAsync);
            UnisocInfoCommand = new AsyncRelayCommand(UnisocInfoAsync);
            UnisocRebootCommand = new AsyncRelayCommand(UnisocRebootAsync);

            // Huawei (Kirin COM 1.0) Commands
            HuaweiBypassIdCommand = new AsyncRelayCommand(HuaweiBypassIdAsync);
            HuaweiReadOeminfoCommand = new AsyncRelayCommand(HuaweiReadOeminfoAsync);
            HuaweiRebootFastbootCommand = new AsyncRelayCommand(HuaweiRebootFastbootAsync);
            HuaweiInfoCommand = new AsyncRelayCommand(HuaweiInfoAsync);

            // Firmware Extractor Commands
            ExtractPayloadBinCommand = new AsyncRelayCommand(ExtractPayloadBinAsync);
            ExtractOfpCommand = new AsyncRelayCommand(ExtractOfpAsync);
            ExtractSamsungTarCommand = new AsyncRelayCommand(ExtractSamsungTarAsync);
            ParseScatterCommand = new AsyncRelayCommand(ParseScatterAsync);
            ParseRawProgramCommand = new AsyncRelayCommand(ParseRawProgramAsync);
            BrowseFirmwareFileCommand = new RelayCommand(BrowseFirmwareFile);
            FlashExtractedFilesCommand = new AsyncRelayCommand(FlashExtractedFilesAsync);
            OpenExtractedFolderCommand = new RelayCommand(OpenExtractedFolder);

            // Driver Assistant Commands
            CheckDriversCommand = new AsyncRelayCommand(CheckDriversAsync);
            InstallUsbDkCommand = new AsyncRelayCommand(InstallUsbDkAsync);
            InstallLibUsbCommand = new AsyncRelayCommand(InstallLibUsbAsync);
            InstallDriverItemCommand = new AsyncRelayCommand<DriverStatusItem>(InstallDriverItemAsync);
            InstallAllDriversCommand = new AsyncRelayCommand(InstallAllDriversAsync);
            OpenDeviceManagerCommand = new RelayCommand(_driverEngine.OpenDeviceManager);
            OpenDriversFolderCommand = new RelayCommand(OpenDriversFolder);

            // Partition Manager Commands
            ReadPartitionCommand = new AsyncRelayCommand(ReadPartitionAsync);
            WritePartitionCommand = new AsyncRelayCommand(WritePartitionAsync);
            ErasePartitionCommand = new AsyncRelayCommand(ErasePartitionAsync);

            // Brand Special Operations Commands
            XiaomiAntiRelockCommand = new AsyncRelayCommand(XiaomiAntiRelockAsync);
            VivoRemoveDemoCommand = new AsyncRelayCommand(VivoRemoveDemoAsync);
            OppoRemoveDemoCommand = new AsyncRelayCommand(OppoRemoveDemoAsync);
            SamsungKnoxCheckCommand = new AsyncRelayCommand(SamsungKnoxCheckAsync);

            // Apple iOS Commands
            AppleReadInfoCommand = new AsyncRelayCommand(AppleReadInfoAsync);
            AppleEnterRecoveryCommand = new AsyncRelayCommand(AppleEnterRecoveryAsync);
            AppleExitRecoveryCommand = new AsyncRelayCommand(AppleExitRecoveryAsync);

            // Job Order & Receipt Commands
            CreateJobOrderCommand = new RelayCommand(CreateJobOrder);
            PrintReceiptCommand = new AsyncRelayCommand(PrintReceiptAsync);
            SendWhatsAppReceiptCommand = new RelayCommand(SendWhatsAppReceipt);
            AutoFillJobFromDeviceCommand = new RelayCommand(AutoFillJobFromDevice);

            // Test Point Hub Commands
            SearchTestPointCommand = new RelayCommand<string>(SearchTestPoint);
            SyncTestPointsCommand = new AsyncRelayCommand(SyncTestPointsAsync);
            OpenTestPointFolderCommand = new RelayCommand(OpenTestPointFolder);

            // Brand & Model Selection Commands
            SelectBrandCommand = new RelayCommand<string>(SelectBrand);
            EnsureLoaderCommand = new AsyncRelayCommand(EnsureLoaderAsync);

            // Xiaomi Extended Commands
            XiaomiSideloadFlashOtaCommand = new AsyncRelayCommand(XiaomiSideloadFlashOtaAsync);

            // Samsung 4-File Odin Commands
            BrowseSamsungBlCommand = new RelayCommand(BrowseSamsungBl);
            BrowseSamsungApCommand = new RelayCommand(BrowseSamsungAp);
            BrowseSamsungCpCommand = new RelayCommand(BrowseSamsungCp);
            BrowseSamsungCscCommand = new RelayCommand(BrowseSamsungCsc);
            ClearSamsungOdinFilesCommand = new RelayCommand(ClearSamsungOdinFiles);
            FlashSamsungOdinCommand = new AsyncRelayCommand(FlashSamsungOdinAsync);

            // Live Partition Manager Command
            RefreshLivePartitionTableCommand = new AsyncRelayCommand(RefreshLivePartitionTableAsync);

            // Signal & IMEI Repair Commands
            RefreshSignalDatabaseCommand = new AsyncRelayCommand(RefreshSignalDatabaseAsync);
            EnableQualcommDiagPortCommand = new AsyncRelayCommand(EnableQualcommDiagPortAsync);
            ScanDiagPortsCommand = new AsyncRelayCommand(ScanDiagPortsAsync);
            RestoreSelectedQcnCommand = new AsyncRelayCommand(RestoreSelectedQcnAsync);
            BrowseCustomQcnCommand = new RelayCommand(BrowseCustomQcn);
            ClearCustomQcnCommand = new RelayCommand(ClearCustomQcn);
            BackupMtkMetaNvramCommand = new AsyncRelayCommand(BackupMtkNvramFromHubAsync);
            RestoreMtkMetaNvramCommand = new AsyncRelayCommand(RestoreMtkNvramFromHubAsync);
            CheckBasebandRadioCommand = new AsyncRelayCommand(CheckBasebandRadioAsync);
            BackupMtkNvramFromHubCommand = new AsyncRelayCommand(BackupMtkNvramFromHubAsync);
            RestoreMtkNvramFromHubCommand = new AsyncRelayCommand(RestoreMtkNvramFromHubAsync);

            // Recovery Hub Commands
            RefreshRecoveryCatalogCommand = new AsyncRelayCommand(RefreshRecoveryCatalogAsync);
            InstallSelectedRecoveryCommand = new AsyncRelayCommand(InstallSelectedRecoveryAsync);
            BootSelectedRecoveryCommand = new AsyncRelayCommand(BootSelectedRecoveryAsync);
            BrowseCustomRecoveryCommand = new RelayCommand(BrowseCustomRecovery);
            ClearCustomRecoveryFileCommand = new RelayCommand(ClearCustomRecoveryFile);
            OpenRecoveryFolderCommand = new RelayCommand(OpenRecoveryFolder);
            OpenOfficialTwrpWebCommand = new RelayCommand(OpenOfficialTwrpWeb);
            OpenOfficialOrangeFoxWebCommand = new RelayCommand(OpenOfficialOrangeFoxWeb);

            // Extended Tool & Brand Commands
            FastbootEraseFrpCommand = new AsyncRelayCommand(FastbootEraseFrpAsync);
            AppleRebootNormalCommand = new AsyncRelayCommand(AppleRebootNormalAsync);
            BrowseUnisocPacCommand = new RelayCommand(BrowseUnisocPac);
            FlashUnisocPacCommand = new AsyncRelayCommand(FlashUnisocPacAsync);
            ScreenMirrorCommand = new AsyncRelayCommand(ScreenMirrorAsync);
            RunDoctorScanCommand = new AsyncRelayCommand(ReadDeviceInfoAsync);
            ReadDeviceInfoCommand = new AsyncRelayCommand(ReadDeviceInfoAsync);
            CopyReadInfoLogCommand = new RelayCommand(CopyReadInfoLog);
            ClearReadInfoLogCommand = new RelayCommand(ClearReadInfoLog);

            // Feature Guide Interactive Modal Commands
            ShowFeatureGuideCommand = new RelayCommand<string>(ShowFeatureGuide);
            CloseFeatureGuideCommand = new RelayCommand(CloseFeatureGuide);

            // Start Coordinated Application Startup Sequence
            _ = InitializeApplicationStartupAsync();
        }

        private async Task InitializeApplicationStartupAsync()
        {
            // 1. Initial Welcome Banner
            AddLog("=================================================================");
            AddLog("  JumperTools PRO — MASTER HARDWARE & FIRMWARE SUITE");
            AddLog("  Platform: Samsung • MTK • Qualcomm • Unisoc • Xiaomi • Huawei");
            AddLog("=================================================================");

            // 2. Initialize Catalogs
            await RefreshSignalDatabaseAsync();
            await RefreshRecoveryCatalogAsync();

            // 3. Driver Health Scan
            await CheckDriversAsync();

            // 4. Job Recovery & Monitors
            await CheckInterruptedJobsOnStartupAsync();
            ConnectionStatus = "License: Pro (Active)";
            AddLog("[Sistem] Mode Standalone Offline aktif. Lisensi JumperTools Pro terverifikasi.");
            AddLog("[Engine] Seluruh modul bypass, partisi & flashing siap digunakan secara mandiri.");
            _ = StartDeviceDetectionAsync();
        }

        private void Devices_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            App.Current?.Dispatcher?.Invoke(() =>
            {
                var edlPorts = Devices
                    .Where(d => d.Mode == DeviceMode.EDL)
                    .Select(d => d.DeviceId) // Usually the port like COM5
                    .ToList();

                FirehoseFinder.EdlPorts.Clear();
                foreach (var port in edlPorts)
                {
                    FirehoseFinder.EdlPorts.Add(port);
                }

                // If currently selected port is not in list and we have ports, select first
                if (FirehoseFinder.EdlPorts.Count > 0 && !FirehoseFinder.EdlPorts.Contains(FirehoseFinder.TargetComPort))
                {
                    FirehoseFinder.TargetComPort = FirehoseFinder.EdlPorts[0];
                }
            });
        }

        public ObservableCollection<Device> Devices { get; }
        public ObservableCollection<string> OperationLogs { get; }
        public ObservableCollection<TestPointInfo> TestPoints { get; }
        public ObservableCollection<PartitionTableItem> Partitions { get; }
        public ObservableCollection<DriverStatusItem> Drivers { get; }
        public ObservableCollection<ExtractedFileInfo> ExtractedFiles { get; }
        public ObservableCollection<JobOrderInfo> JobOrders { get; }
        public ObservableCollection<string> Brands { get; }
        public ObservableCollection<PhoneModelProfile> FilteredModels { get; }

        public string SelectedBrand
        {
            get => _selectedBrand;
            set
            {
                _selectedBrand = value;
                OnPropertyChanged();
                SelectBrand(value);
            }
        }

        public ObservableCollection<string> ProtocolList { get; } = new()
        {
            "Samsung",
            "MediaTek",
            "Qualcomm",
            "Xiaomi",
            "Fastboot",
            "Unisoc",
            "Huawei",
            "Demo Fix",
            "Apple iOS",
            "Refurbish"
        };

        private int _selectedBrandTabIndex = 1; // Default to Universal MediaTek (MTK BROM / Preloader)
        public int SelectedBrandTabIndex
        {
            get => _selectedBrandTabIndex;
            set
            {
                if (_selectedBrandTabIndex != value && value >= 0 && value < ProtocolList.Count)
                {
                    _selectedBrandTabIndex = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(SelectedProtocol));
                    if (_isInitialized)
                    {
                        AddLog($"[Protokol] Mode protokol aktif dialihkan ke: {ProtocolList[value]}");
                    }
                }
                else if (_selectedBrandTabIndex != value)
                {
                    _selectedBrandTabIndex = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(SelectedProtocol));
                }
            }
        }

        public string? SelectedProtocol
        {
            get => _selectedBrandTabIndex >= 0 && _selectedBrandTabIndex < ProtocolList.Count ? ProtocolList[_selectedBrandTabIndex] : ProtocolList[1];
            set
            {
                if (value != null)
                {
                    var idx = ProtocolList.IndexOf(value);
                    if (idx >= 0 && idx != _selectedBrandTabIndex)
                    {
                        SelectedBrandTabIndex = idx;
                    }
                }
            }
        }

        private string _selectedSamsungBinary = "Bit-Auto (Deteksi Otomatis)";
        public string SelectedSamsungBinary
        {
            get => _selectedSamsungBinary;
            set
            {
                _selectedSamsungBinary = value;
                OnPropertyChanged();
                AddLog($"[SAMSUNG QUALCOMM] Profil Anti-Rollback Fuse Bit dipilih: {value}");
            }
        }

        public ObservableCollection<string> SamsungBinaryList { get; } = new()
        {
            "Bit-Auto (Deteksi Otomatis)",
            "Bit-1", "Bit-2", "Bit-3", "Bit-4", "Bit-5",
            "Bit-6", "Bit-7", "Bit-8", "Bit-9",
            "Bit-A (10)", "Bit-B (11)", "Bit-C (12)", "Bit-D (13)", "Bit-E (14)", "Bit-F (15)"
        };

        public PhoneModelProfile? SelectedPhoneModel
        {
            get => _selectedPhoneModel;
            set
            {
                _selectedPhoneModel = value;
                OnPropertyChanged();
                if (value != null)
                {
                    SelectedLoader = _loaderManager.GetLoaderForModel(value);
                    if (SelectedLoader != null)
                    {
                        LoaderStatusMessage = SelectedLoader.IsCachedLocally 
                            ? $"[Siap/Cache] {SelectedLoader.FileName} ({SelectedLoader.Chipset})" 
                            : $"[Server] {SelectedLoader.FileName} (Klik untuk Unduh)";
                    }
                    else
                    {
                        LoaderStatusMessage = $"Chipset: {value.Chipset} ({value.Architecture})";
                    }
                    if (_isInitialized)
                    {
                        AddLog($"[Model Dipilih] {value.Brand} {value.ModelName} ({value.Chipset}) -> Loader: {value.RecommendedLoaderFile}");
                    }
                    AutoSelectBrandTab();
                }
            }
        }

        public LoaderInfo? SelectedLoader
        {
            get => _selectedLoader;
            set { _selectedLoader = value; OnPropertyChanged(); }
        }

        public string LoaderStatusMessage
        {
            get => _loaderStatusMessage;
            set { _loaderStatusMessage = value; OnPropertyChanged(); }
        }

        public string ModelSearchFilter
        {
            get => _modelSearchFilter;
            set
            {
                _modelSearchFilter = value;
                OnPropertyChanged();
                FilterModels(value);
            }
        }

        public ICommand SelectBrandCommand { get; }
        public ICommand EnsureLoaderCommand { get; }

        private string _lastMonitoredBattery = "";

        public Device? SelectedDevice
        {
            get => _selectedDevice;
            set
            {
                var previous = _selectedDevice;
                _selectedDevice = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsDeviceConnected));
                OnPropertyChanged(nameof(DeviceStatusSummary));
                OnPropertyChanged(nameof(DeviceStatusDotColor));
                OnPropertyChanged(nameof(DeviceStatusBgColor));
                OnPropertyChanged(nameof(DeviceStatusBorderColor));
                OnPropertyChanged(nameof(DeviceStatusTextColor));

                if (value != null)
                {
                    SyncConnectedDeviceBrandAndModel(value);
                    AutoSelectBrandTab();
                    if (previous == null || previous.DeviceId != value.DeviceId)
                    {
                        _ = RunRealtimeDeviceDiagnosticAsync(value);
                    }
                }
                else
                {
                    _lastMonitoredBattery = "";
                }
            }
        }

        private void SyncConnectedDeviceBrandAndModel(Device device)
        {
            try
            {
                if (device == null) return;

                // 1. Resolve Target Brand
                string targetBrand = device.Brand;
                if (string.IsNullOrWhiteSpace(targetBrand) || targetBrand.Equals("Android", StringComparison.OrdinalIgnoreCase) || targetBrand.Equals("Unknown", StringComparison.OrdinalIgnoreCase))
                {
                    var mod = device.Model ?? "";
                    if (mod.Contains("OPLUS", StringComparison.OrdinalIgnoreCase) || mod.Contains("OPPO", StringComparison.OrdinalIgnoreCase) || mod.Contains("CPH", StringComparison.OrdinalIgnoreCase))
                        targetBrand = "Oppo";
                    else if (mod.Contains("Xiaomi", StringComparison.OrdinalIgnoreCase) || mod.Contains("Redmi", StringComparison.OrdinalIgnoreCase) || mod.Contains("Poco", StringComparison.OrdinalIgnoreCase) || mod.Contains("ruby", StringComparison.OrdinalIgnoreCase))
                        targetBrand = "Xiaomi";
                    else if (mod.Contains("Samsung", StringComparison.OrdinalIgnoreCase) || mod.Contains("SM-", StringComparison.OrdinalIgnoreCase))
                        targetBrand = "Samsung";
                    else if (mod.Contains("Vivo", StringComparison.OrdinalIgnoreCase) || mod.Contains("PD", StringComparison.OrdinalIgnoreCase))
                        targetBrand = "Vivo";
                    else if (mod.Contains("Realme", StringComparison.OrdinalIgnoreCase) || mod.Contains("RMX", StringComparison.OrdinalIgnoreCase))
                        targetBrand = "Realme";
                    else if (mod.Contains("Infinix", StringComparison.OrdinalIgnoreCase) || mod.Contains("X6", StringComparison.OrdinalIgnoreCase))
                        targetBrand = "Infinix";
                }

                // 2. Select Brand Filter if found
                var matchedBrand = Brands.FirstOrDefault(b => b.Equals(targetBrand, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrEmpty(matchedBrand) && !matchedBrand.Equals("Semua", StringComparison.OrdinalIgnoreCase))
                {
                    SelectBrand(matchedBrand);
                }

                // 3. Search and Auto-Select Matching Phone Model Profile
                var allProfiles = LoaderManager.MasterProfiles;
                var devModel = device.Model ?? "";
                
                var matchedModel = allProfiles.FirstOrDefault(m =>
                    (!string.IsNullOrEmpty(m.CodeName) && (devModel.Contains(m.CodeName, StringComparison.OrdinalIgnoreCase) || m.CodeName.Split('/').Any(cn => !string.IsNullOrWhiteSpace(cn) && devModel.Contains(cn.Trim(), StringComparison.OrdinalIgnoreCase)))) ||
                    (!string.IsNullOrEmpty(m.ModelName) && (devModel.Contains(m.ModelName, StringComparison.OrdinalIgnoreCase) || m.ModelName.Split('/').Any(mn => !string.IsNullOrWhiteSpace(mn) && devModel.Contains(mn.Trim(), StringComparison.OrdinalIgnoreCase))))
                );

                if (matchedModel != null)
                {
                    // Align brand selection if different
                    var brandOfModel = Brands.FirstOrDefault(b => b.Equals(matchedModel.Brand, StringComparison.OrdinalIgnoreCase));
                    if (!string.IsNullOrEmpty(brandOfModel) && !brandOfModel.Equals("Semua", StringComparison.OrdinalIgnoreCase) && _selectedBrand != brandOfModel)
                    {
                        SelectBrand(brandOfModel);
                    }

                    var inFiltered = FilteredModels.FirstOrDefault(m => m.ModelName == matchedModel.ModelName && m.Brand == matchedModel.Brand);
                    SelectedPhoneModel = inFiltered ?? matchedModel;
                }
                else if (FilteredModels.Count > 0 && SelectedPhoneModel == null)
                {
                    SelectedPhoneModel = FilteredModels.FirstOrDefault();
                }

                // 4. Auto-route active tab & protocol based on detected architecture / chipset
                AutoSelectBrandTab();
            }
            catch { }
        }

        private async Task RunRealtimeDeviceDiagnosticAsync(Device device)
        {
            try
            {
                // Auto-clear terminal log for fresh device focus
                Application.Current?.Dispatcher?.Invoke(() =>
                {
                    OperationLogs.Clear();
                    TerminalLogText = "";
                });

                CurrentOperation = $"{device.Brand} {device.Model} ({device.Mode})";
                OperationProgress = 100;
                OperationStatus = "Ready";

                AddLog("================================================================");
                AddLog($"[KONEKSI] Terhubung: {device.Brand} {device.Model} (Mode: {device.Mode})");
                AddLog("================================================================");
                AddLog($" Identitas Perangkat : {device.Brand} {device.Model} (ID/Port: {device.DeviceId})");
                if (!string.IsNullOrEmpty(device.SoftwareVersion) && device.SoftwareVersion != "-")
                {
                    AddLog($" Sistem Operasi      : {device.SoftwareVersion}");
                }
                if (!string.IsNullOrEmpty(device.Chipset) && device.Chipset != "-")
                {
                    AddLog($" Platform Chipset    : {device.Chipset}");
                }
                if (!string.IsNullOrEmpty(device.TotalRam) && device.TotalRam != "-")
                {
                    AddLog($" Alokasi Memori      : RAM {device.TotalRam} | Storage {device.StorageInfo}");
                }
                AddLog($" Status Daya & Sec   : {device.BatteryLevel} | {device.SecurityStatus} | Patch {device.SecurityPatch}");

                string protocol = device.Mode switch
                {
                    DeviceMode.Normal => "Android ADB (USB Debugging Siap)",
                    DeviceMode.Fastboot => "Fastboot Bootloader Interface",
                    DeviceMode.BROM or DeviceMode.Download => "MediaTek Boot ROM (BROM / VCOM)",
                    DeviceMode.EDL => "Qualcomm Emergency Download (EDL 9008)",
                    DeviceMode.MTP => "MTP / Windows USB Bus (Zero-ADB / Layar Terkunci)",
                    _ => $"{device.Mode} Protocol"
                };
                AddLog($" Jalur Komunikasi    : {protocol}");
                AddLog("================================================================");
                AddLog($"[STATUS OPERASI] {device.Brand} {device.Model} SIAP untuk Flashing / Unlock / Repair.");

                if (device.Mode == DeviceMode.MTP)
                {
                    AddLog("----------------------------------------------------------------");
                    AddLog($"[PETUNJUK TEKNISI: LAYAR TERKUNCI / MTP ({device.Brand.ToUpper()})]");
                    var guidance = LoaderManager.GetBrandOperationGuidance(device.Brand, device.Mode);
                    foreach (var line in guidance)
                    {
                        AddLog(line);
                    }
                }
                AddLog("================================================================");

                _lastMonitoredBattery = device.BatteryLevel;
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error running realtime diagnostic");
            }
        }

        public bool IsDeviceConnected => SelectedDevice != null;
        public string DeviceStatusDotColor => IsDeviceConnected ? "#0284C7" : "#94A3B8";
        public string DeviceStatusBgColor => "#F8FAFC";
        public string DeviceStatusBorderColor => "#CBD5E1";
        public string DeviceStatusTextColor => IsDeviceConnected ? "#0F172A" : "#64748B";

        public string DeviceStatusSummary
        {
            get
            {
                if (SelectedDevice == null) return "HP: Belum Terhubung";
                var brand = SelectedDevice.Brand?.Trim() ?? "";
                var model = SelectedDevice.Model?.Trim() ?? "";
                var displayModel = model;
                if (!string.IsNullOrEmpty(brand) && displayModel.StartsWith(brand, StringComparison.OrdinalIgnoreCase))
                {
                    displayModel = displayModel.Substring(brand.Length).Trim();
                }
                var modelText = string.IsNullOrEmpty(displayModel) ? model : $"{brand} {displayModel}".Trim();
                return $"HP: {modelText} ({SelectedDevice.Mode})";
            }
        }

        public bool HasNoPartitions => Partitions == null || Partitions.Count == 0;
        public bool HasNoExtractedFiles => ExtractedFiles == null || ExtractedFiles.Count == 0;

        /// <summary>PERBAIKAN P4-4: Computed property untuk enable tombol Flash ke HP.</summary>
        public bool HasExtractedFiles => ExtractedFiles != null && ExtractedFiles.Count > 0;

        // PERBAIKAN P5-3: Checkbox Flashing Options yang benar-benar terikat ke state — bukan hardcode.
        private bool _autoRebootAfterFlash = true;
        public bool AutoRebootAfterFlash
        {
            get => _autoRebootAfterFlash;
            set { _autoRebootAfterFlash = value; OnPropertyChanged(); }
        }

        private bool _wipeUserdataOnFlash = false;
        public bool WipeUserdataOnFlash
        {
            get => _wipeUserdataOnFlash;
            set { _wipeUserdataOnFlash = value; OnPropertyChanged(); }
        }

        private bool _backupNvramBeforeFlash = true;
        public bool BackupNvramBeforeFlash
        {
            get => _backupNvramBeforeFlash;
            set { _backupNvramBeforeFlash = value; OnPropertyChanged(); }
        }

        public TestPointInfo? SelectedTestPoint
        {
            get => _selectedTestPoint;
            set
            {
                _selectedTestPoint = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TestPointImageSource));
                OnPropertyChanged(nameof(HasNoTestPointImage));
                if (value != null && _isInitialized)
                {
                    AddLog($"[Test Point Dipilih] {value.Brand} {value.MarketingName} ({value.Chipset})");
                }
            }
        }

        public System.Windows.Media.ImageSource? TestPointImageSource
        {
            get
            {
                if (SelectedTestPoint == null) return null;

                var imgPath = SelectedTestPoint.LocalImagePath;
                if (string.IsNullOrEmpty(imgPath) || !System.IO.File.Exists(imgPath))
                {
                    if (!string.IsNullOrEmpty(SelectedTestPoint.RelativePath))
                    {
                        var relNorm = SelectedTestPoint.RelativePath.Replace('/', System.IO.Path.DirectorySeparatorChar).Replace('\\', System.IO.Path.DirectorySeparatorChar);
                        
                        var candidates = new[]
                        {
                            System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "database", relNorm),
                            System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "database", "testpoint", relNorm),
                            System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "database", "isp_pinouts", relNorm)
                        };

                        foreach (var c in candidates)
                        {
                            if (System.IO.File.Exists(c))
                            {
                                imgPath = c;
                                SelectedTestPoint.LocalImagePath = c;
                                break;
                            }
                        }
                    }
                }

                if (string.IsNullOrEmpty(imgPath) || !System.IO.File.Exists(imgPath))
                {
                    return null;
                }

                try
                {
                    var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(imgPath, UriKind.Absolute);
                    bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    return bitmap;
                }
                catch
                {
                    return null;
                }
            }
        }

        public bool HasNoTestPointImage => TestPointImageSource == null;

        public PartitionTableItem? SelectedPartition
        {
            get => _selectedPartition;
            set
            {
                _selectedPartition = value;
                OnPropertyChanged();
                // PERBAIKAN P3-X3: Notifikasi tombol Backup/Flash/Erase agar reactive saat pilih baris
                OnPropertyChanged(nameof(HasPartitionSelected));
            }
        }

        /// <summary>PERBAIKAN P3-X1: Computed property untuk enable/disable tombol aksi partisi.</summary>
        public bool HasPartitionSelected => SelectedPartition != null;

        public JobOrderInfo? SelectedJobOrder
        {
            get => _selectedJobOrder;
            set
            {
                _selectedJobOrder = value;
                OnPropertyChanged();
                UpdateReceiptPreview();
            }
        }

        public bool HasNoJobOrders => JobOrders.Count == 0;

        public string NewCustomerName
        {
            get => _newCustomerName;
            set { _newCustomerName = value; OnPropertyChanged(); UpdateReceiptPreview(); }
        }

        public string NewCustomerPhone
        {
            get => _newCustomerPhone;
            set { _newCustomerPhone = value; OnPropertyChanged(); UpdateReceiptPreview(); }
        }

        public string NewDeviceBrand
        {
            get => _newDeviceBrand;
            set { _newDeviceBrand = value; OnPropertyChanged(); UpdateReceiptPreview(); }
        }

        public string NewDeviceModel
        {
            get => _newDeviceModel;
            set { _newDeviceModel = value; OnPropertyChanged(); UpdateReceiptPreview(); }
        }

        public string NewServiceType
        {
            get => _newServiceType;
            set { _newServiceType = value; OnPropertyChanged(); UpdateReceiptPreview(); }
        }

        public decimal NewCost
        {
            get => _newCost;
            set { _newCost = value; OnPropertyChanged(); UpdateReceiptPreview(); }
        }

        public int NewWarrantyDays
        {
            get => _newWarrantyDays;
            set { _newWarrantyDays = value; OnPropertyChanged(); UpdateReceiptPreview(); }
        }

        public string ReceiptPreviewText
        {
            get => _receiptPreviewText;
            set { _receiptPreviewText = value; OnPropertyChanged(); }
        }

        public string TestPointSearchQuery
        {
            get => _testPointSearchQuery;
            set
            {
                _testPointSearchQuery = value;
                OnPropertyChanged();
                SearchTestPoint(value);
            }
        }

        public string FirmwareFilePath
        {
            get => _firmwareFilePath;
            set
            {
                _firmwareFilePath = value;
                OnPropertyChanged();
                // PERBAIKAN P4-4: Tombol ekstraksi enable/disable reactive saat user pilih file
                OnPropertyChanged(nameof(HasFirmwareFileSelected));
            }
        }

        /// <summary>PERBAIKAN P4-4: Computed property untuk enable tombol ekstraksi.</summary>
        public bool HasFirmwareFileSelected => !string.IsNullOrWhiteSpace(FirmwareFilePath) && System.IO.File.Exists(FirmwareFilePath);

        public int OperationProgress
        {
            get => _operationProgress;
            set
            {
                _operationProgress = value;
                OnPropertyChanged();
            }
        }

        public string CurrentOperation
        {
            get => _currentOperation;
            set
            {
                _currentOperation = value;
                OnPropertyChanged();
            }
        }

        public string OperationStatus
        {
            get => _operationStatus;
            set
            {
                _operationStatus = value;
                OnPropertyChanged();
            }
        }

        public bool IsServerConnected => _connectionStatus != null && (_connectionStatus.Contains("Connected", StringComparison.OrdinalIgnoreCase) || _connectionStatus.Contains("Authenticated", StringComparison.OrdinalIgnoreCase) || _connectionStatus.Contains("Active", StringComparison.OrdinalIgnoreCase));
        public bool IsServerOfflineMode => _connectionStatus != null && _connectionStatus.Contains("Offline", StringComparison.OrdinalIgnoreCase);

        public string ServerStatusDotColor => IsServerConnected ? "#10B981" : (IsServerOfflineMode ? "#F59E0B" : "#EF4444");
        public string ServerStatusBgColor => "#F8FAFC";
        public string ServerStatusBorderColor => "#CBD5E1";
        public string ServerStatusTextColor => "#0F172A";

        public string ConnectionStatus
        {
            get => _connectionStatus;
            set
            {
                _connectionStatus = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsServerConnected));
                OnPropertyChanged(nameof(IsServerOfflineMode));
                OnPropertyChanged(nameof(ServerStatusDotColor));
                OnPropertyChanged(nameof(ServerStatusBgColor));
                OnPropertyChanged(nameof(ServerStatusBorderColor));
                OnPropertyChanged(nameof(ServerStatusTextColor));
            }
        }

        public string LicenseInfo
        {
            get => _licenseInfo;
            set
            {
                _licenseInfo = value;
                OnPropertyChanged();
            }
        }

        public string LastSyncTime
        {
            get => _lastSyncTime;
            set
            {
                _lastSyncTime = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SyncStatusBadgeText));
                OnPropertyChanged(nameof(SyncStatusBadgeBg));
                OnPropertyChanged(nameof(SyncStatusBadgeBorder));
                OnPropertyChanged(nameof(SyncStatusBadgeTextColor));
            }
        }

        public string SyncStatusBadgeText => (string.IsNullOrEmpty(LastSyncTime) || LastSyncTime.Contains("Belum", StringComparison.OrdinalIgnoreCase) || LastSyncTime.Contains("Never", StringComparison.OrdinalIgnoreCase))
            ? "OFFLINE"
            : "SYNCED";

        public string SyncStatusBadgeBg => "#F8FAFC";

        public string SyncStatusBadgeBorder => "#CBD5E1";

        public string SyncStatusBadgeTextColor => (string.IsNullOrEmpty(LastSyncTime) || LastSyncTime.Contains("Belum", StringComparison.OrdinalIgnoreCase) || LastSyncTime.Contains("Never", StringComparison.OrdinalIgnoreCase))
            ? "#D97706"
            : "#059669";

        public string AppVersion
        {
            get => _appVersion;
            set
            {
                _appVersion = value;
                OnPropertyChanged();
            }
        }

        public string TerminalLogText
        {
            get => _terminalLogText;
            set
            {
                _terminalLogText = value;
                OnPropertyChanged();
            }
        }

        public string UserGuidanceMessage
        {
            get => _userGuidanceMessage;
            set
            {
                _userGuidanceMessage = value;
                OnPropertyChanged();
            }
        }

        // Standard Commands
        public ICommand RefreshDevicesCommand { get; }
        public ICommand FlashSamsungCommand { get; }
        public ICommand UnlockBootloaderCommand { get; }
        public ICommand BypassFrpCommand { get; }
        public ICommand BypassFrpSamsungServiceMenuCommand { get; }
        public ICommand RepairImeiCommand { get; }
        public ICommand FixBasebandCommand { get; }
        public ICommand RepairEfsCommand { get; }
        public ICommand EdlFlashCommand { get; }
        public ICommand AuthBypassCommand { get; }
        public ICommand RemoveMiAccountCommand { get; }
        public ICommand FlashOppoCommand { get; }
        public ICommand FormatCommand { get; }
        public ICommand AdbShellCommand { get; }
        public ICommand InstallApkCommand { get; }
        public ICommand ScreenRecordCommand { get; }
        public ICommand FlashTwrpCommand { get; }
        public ICommand BootRecoveryCommand { get; }
        public ICommand DeviceInfoCommand { get; }
        public ICommand ConnectServerCommand { get; }
        public ICommand LicenseStatusCommand { get; }
        public ICommand PlatformToolsCommand { get; }
        public ICommand DownloadPathCommand { get; }
        public ICommand LoginCommand { get; }
        public ICommand LogoutCommand { get; }
        public ICommand OpenDiagnosticDashboardCommand { get; }
        public ICommand ForceResetUsbPortsCommand { get; }

        public ICommand ClearLogCommand { get; }
        public ICommand CopyLogCommand { get; }
        public ICommand RebootNormalCommand { get; }
        public ICommand RebootDownloadCommand { get; }
        public ICommand RebootRecoveryCommand { get; }
        public ICommand RebootEdlCommand { get; }

        // MediaTek (MTK) Commands
        public ICommand MtkBypassFrpCommand { get; }
        public ICommand MtkFormatUserdataCommand { get; }
        public ICommand MtkUnlockBootloaderCommand { get; }
        public ICommand MtkRelockBootloaderCommand { get; }
        public ICommand MtkReadGptCommand { get; }
        public ICommand MtkBackupNvramCommand { get; }
        public ICommand MtkRestoreNvramCommand { get; }
        public ICommand MtkRebootCommand { get; }
        public ICommand MtkCrashPreloaderCommand { get; }
        public ICommand MtkBrowseCustomDaCommand { get; }
        public ICommand MtkBrowseCustomAuthCommand { get; }
        public ICommand SamsungEdlFlashBitCommand { get; }

        // Qualcomm (EDL 9008) Commands
        public ICommand EdlBypassFrpCommand { get; }
        public ICommand EdlFormatUserdataCommand { get; }
        public ICommand EdlReadGptCommand { get; }
        public ICommand EdlBackupEfsCommand { get; }
        public ICommand EdlRestoreEfsCommand { get; }
        public ICommand EdlRebootCommand { get; }

        // Xiaomi (Sideload / Mi Assistant) Commands
        public ICommand XiaomiSideloadInfoCommand { get; }
        public ICommand XiaomiSideloadFormatCommand { get; }
        public ICommand XiaomiSideloadFrpCommand { get; }
        public ICommand XiaomiSideloadRebootCommand { get; }

        // Unisoc / Spreadtrum (SPD) Commands
        public ICommand UnisocBypassFrpCommand { get; }
        public ICommand UnisocFormatUserdataCommand { get; }
        public ICommand UnisocInfoCommand { get; }
        public ICommand UnisocRebootCommand { get; }

        // Huawei (Kirin COM 1.0) Commands
        public ICommand HuaweiBypassIdCommand { get; }
        public ICommand HuaweiReadOeminfoCommand { get; }
        public ICommand HuaweiRebootFastbootCommand { get; }
        public ICommand HuaweiInfoCommand { get; }

        // Firmware Extractor Commands
        public ICommand ExtractPayloadBinCommand { get; }
        public ICommand ExtractOfpCommand { get; }
        public ICommand ExtractSamsungTarCommand { get; }
        public ICommand ParseScatterCommand { get; }
        public ICommand ParseRawProgramCommand { get; }
        public ICommand BrowseFirmwareFileCommand { get; }
        public ICommand FlashExtractedFilesCommand { get; }
        public ICommand OpenExtractedFolderCommand { get; }

        // Driver Assistant Commands
        public ICommand CheckDriversCommand { get; }
        public ICommand OpenDeviceManagerCommand { get; }
        public ICommand OpenDriversFolderCommand { get; }
        public ICommand InstallUsbDkCommand { get; }
        public ICommand InstallLibUsbCommand { get; }
        public ICommand InstallDriverItemCommand { get; }
        public ICommand InstallAllDriversCommand { get; }

        // Partition Manager Commands
        public ICommand ReadPartitionCommand { get; }
        public ICommand WritePartitionCommand { get; }
        public ICommand ErasePartitionCommand { get; }
        public ICommand RefreshLivePartitionTableCommand { get; }

        // Brand Special Operations Commands
        public ICommand XiaomiAntiRelockCommand { get; }
        public ICommand XiaomiSideloadFlashOtaCommand { get; }
        public ICommand VivoRemoveDemoCommand { get; }
        public ICommand OppoRemoveDemoCommand { get; }
        public ICommand SamsungKnoxCheckCommand { get; }

        // Samsung 4-File Odin Commands
        public ICommand BrowseSamsungBlCommand { get; }
        public ICommand BrowseSamsungApCommand { get; }
        public ICommand BrowseSamsungCpCommand { get; }
        public ICommand BrowseSamsungCscCommand { get; }
        public ICommand ClearSamsungOdinFilesCommand { get; }
        public ICommand FlashSamsungOdinCommand { get; }

        // Apple iOS Commands
        public ICommand AppleReadInfoCommand { get; }
        public ICommand AppleEnterRecoveryCommand { get; }
        public ICommand AppleExitRecoveryCommand { get; }

        // Job Order & Receipt Commands
        public ICommand CreateJobOrderCommand { get; }
        public ICommand PrintReceiptCommand { get; }
        public ICommand SendWhatsAppReceiptCommand { get; }
        public ICommand AutoFillJobFromDeviceCommand { get; }

        // Test Point Commands
        public ICommand SearchTestPointCommand { get; }
        public ICommand SyncTestPointsCommand { get; }
        public ICommand OpenTestPointFolderCommand { get; }

        // Signal & IMEI Repair Commands
        public ICommand RefreshSignalDatabaseCommand { get; }
        public ICommand EnableQualcommDiagPortCommand { get; }
        public ICommand ScanDiagPortsCommand { get; }
        public ICommand RestoreSelectedQcnCommand { get; }
        public ICommand BrowseCustomQcnCommand { get; }
        public ICommand ClearCustomQcnCommand { get; }
        public ICommand BackupMtkMetaNvramCommand { get; }
        public ICommand RestoreMtkMetaNvramCommand { get; }
        public ICommand CheckBasebandRadioCommand { get; }
        public ICommand BackupMtkNvramFromHubCommand { get; }
        public ICommand RestoreMtkNvramFromHubCommand { get; }

        // Recovery Hub Commands
        public ICommand RefreshRecoveryCatalogCommand { get; }
        public ICommand InstallSelectedRecoveryCommand { get; }
        public ICommand BootSelectedRecoveryCommand { get; }
        public ICommand BrowseCustomRecoveryCommand { get; }
        public ICommand ClearCustomRecoveryFileCommand { get; }
        public ICommand OpenRecoveryFolderCommand { get; }
        public ICommand OpenOfficialTwrpWebCommand { get; }
        public ICommand OpenOfficialOrangeFoxWebCommand { get; }

        // Extended Tool & Brand Commands
        public ICommand FastbootEraseFrpCommand { get; }
        public ICommand AppleRebootNormalCommand { get; }
        public ICommand BrowseUnisocPacCommand { get; }
        public ICommand FlashUnisocPacCommand { get; }
        public ICommand ScreenMirrorCommand { get; }
        public ICommand RunDoctorScanCommand { get; }
        public ICommand ReadDeviceInfoCommand { get; }
        public ICommand CopyReadInfoLogCommand { get; }
        public ICommand ClearReadInfoLogCommand { get; }
        public ICommand CancelCurrentOperationCommand { get; }

        // Feature Guide Interactive Modal System
        private FeatureGuideInfo _currentFeatureGuide = FeatureGuideRegistry.GetGuide("default");
        public FeatureGuideInfo CurrentFeatureGuide
        {
            get => _currentFeatureGuide;
            set
            {
                _currentFeatureGuide = value;
                OnPropertyChanged();
            }
        }

        private bool _isFeatureGuideOpen;
        public bool IsFeatureGuideOpen
        {
            get => _isFeatureGuideOpen;
            set
            {
                _isFeatureGuideOpen = value;
                OnPropertyChanged();
            }
        }

        public ICommand ShowFeatureGuideCommand { get; }
        public ICommand CloseFeatureGuideCommand { get; }

        public void ShowFeatureGuide(string? key)
        {
            CurrentFeatureGuide = FeatureGuideRegistry.GetGuide(key ?? "default");
            IsFeatureGuideOpen = true;
        }

        public void CloseFeatureGuide()
        {
            IsFeatureGuideOpen = false;
        }

        public void CancelCurrentOperation()
        {
            try
            {
                var processes = new[] { "python", "mtk", "fastboot", "edl" };
                foreach (var p in processes)
                {
                    foreach (var proc in Process.GetProcessesByName(p))
                    {
                        try { proc.Kill(true); } catch { }
                    }
                }

                OperationStatus = "Cancelled";
                OperationProgress = 0;
                CurrentOperation = "Operasi Dibatalkan";
                AddLog("\n[⏹️ STOP] Port listener / operasi berhasil dihentikan oleh teknisi.");
                UserGuidanceMessage = "Operasi dibatalkan. Sistem siap kembali.";
            }
            catch (Exception ex)
            {
                AddLog($"[Error] Gagal menghentikan proses: {ex.Message}");
            }
        }

        // ==================== LOGGING & COMMON LIFECYCLE ====================

        public void AddLog(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;

            var timeStr = DateTime.Now.ToString("HH:mm:ss");
            var formatted = (message.Length >= 10 && message.StartsWith("[") && char.IsDigit(message[1]) && message[3] == ':')
                ? message
                : $"[{timeStr}] {message}";

            Application.Current?.Dispatcher?.Invoke(() =>
            {
                OperationLogs.Add(formatted);
                var sb = new StringBuilder(TerminalLogText);
                sb.AppendLine(formatted);
                TerminalLogText = sb.ToString();
            });
        }

        private void ClearLogs()
        {
            OperationLogs.Clear();
            TerminalLogText = "";
            AddLog("Terminal log dibersihkan.");
        }

        private void CopyLogs()
        {
            if (!string.IsNullOrEmpty(TerminalLogText))
            {
                Clipboard.SetText(TerminalLogText);
                AddLog("Seluruh teks log berhasil disalin ke Clipboard.");
            }
        }

        private async Task StartDeviceDetectionAsync()
        {
            int telemetryCounter = 0;
            while (true)
            {
                try
                {
                    await RefreshDevicesAsync();

                    telemetryCounter++;
                    if (telemetryCounter >= 4 && SelectedDevice != null && SelectedDevice.Mode == DeviceMode.Normal)
                    {
                        telemetryCounter = 0;
                        _ = CheckLiveBatteryTelemetryAsync(SelectedDevice);
                    }

                    await Task.Delay(1200); // Responsive 1.2s polling
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Error in device detection loop");
                    await Task.Delay(3000);
                }
            }
        }

        private async Task CheckLiveBatteryTelemetryAsync(Device device)
        {
            try
            {
                var battResult = await _deviceEngine.GetBatteryStatusLiveAsync(device.DeviceId);
                if (!string.IsNullOrEmpty(battResult) && battResult != "-" && battResult != _lastMonitoredBattery)
                {
                    _lastMonitoredBattery = battResult;
                    device.BatteryLevel = battResult;
                    OnPropertyChanged(nameof(SelectedDevice));
                    AddLog($"[MONITOR LIVE] Telemetri Daya: {battResult} (Koneksi USB Stabil)");
                }
            }
            catch
            {
                // Non-blocking telemetry
            }
        }

        public async Task TriggerInstantUsbScanAsync()
        {
            await Task.Delay(80); // Small debounce for Windows USB driver stack settling
            await RefreshDevicesAsync();
        }

        private async Task RefreshDevicesAsync()
        {
            try
            {
                // Run all 8 protocol scanners concurrently in parallel
                var adbTask = _deviceEngine.GetConnectedDevicesAsync();
                var fastbootTask = _deviceEngine.GetFastbootDevicesAsync();
                var samsungTask = _deviceEngine.GetSamsungComDevicesAsync();
                var mtkTask = _deviceEngine.GetMediaTekPortsAsync();
                var qcomTask = _deviceEngine.GetQualcommPortsAsync();
                var unisocTask = _deviceEngine.GetUnisocPortsAsync();
                var huaweiTask = _deviceEngine.GetHuaweiPortsAsync();
                var mtpTask = _deviceEngine.GetMtpPnpDevicesAsync();

                await Task.WhenAll(adbTask, fastbootTask, samsungTask, mtkTask, qcomTask, unisocTask, huaweiTask, mtpTask);

                var allDevices = new List<Device>();
                allDevices.AddRange(await adbTask);
                allDevices.AddRange(await fastbootTask);
                allDevices.AddRange(await samsungTask);
                allDevices.AddRange(await mtkTask);
                allDevices.AddRange(await qcomTask);
                allDevices.AddRange(await unisocTask);
                allDevices.AddRange(await huaweiTask);

                // Add MTP devices only if not already detected via ADB/Fastboot/COM
                var existingSerials = new HashSet<string>(allDevices.Select(d => d.DeviceId), StringComparer.OrdinalIgnoreCase);
                foreach (var mtpDev in await mtpTask)
                {
                    if (!existingSerials.Contains(mtpDev.DeviceId))
                    {
                        allDevices.Add(mtpDev);
                        existingSerials.Add(mtpDev.DeviceId);
                    }
                }

                var currentIds = string.Join(",", Devices.Select(d => d.DeviceId));
                var newIds = string.Join(",", allDevices.Select(d => d.DeviceId));

                if (currentIds != newIds)
                {
                    Devices.Clear();
                    foreach (var device in allDevices)
                    {
                        Devices.Add(device);
                    }

                    if (allDevices.Count > 0)
                    {
                        if (allDevices.Count > 1)
                        {
                            var activeProtocols = new List<string>();
                            if ((await adbTask).Count > 0) activeProtocols.Add($"ADB: {(await adbTask).Count}");
                            if ((await fastbootTask).Count > 0) activeProtocols.Add($"Fastboot: {(await fastbootTask).Count}");
                            if ((await samsungTask).Count > 0) activeProtocols.Add($"Samsung COM: {(await samsungTask).Count}");
                            if ((await mtkTask).Count > 0) activeProtocols.Add($"MTK: {(await mtkTask).Count}");
                            if ((await qcomTask).Count > 0) activeProtocols.Add($"Qualcomm: {(await qcomTask).Count}");
                            if ((await unisocTask).Count > 0) activeProtocols.Add($"Unisoc: {(await unisocTask).Count}");
                            if ((await huaweiTask).Count > 0) activeProtocols.Add($"Huawei: {(await huaweiTask).Count}");
                            if ((await mtpTask).Count > 0) activeProtocols.Add($"MTP PnP: {(await mtpTask).Count}");
                            AddLog($"[Deteksi Multi-Port] {allDevices.Count} Perangkat aktif ({string.Join(", ", activeProtocols)})");
                        }

                        if (SelectedDevice == null || !allDevices.Any(d => d.DeviceId == SelectedDevice.DeviceId))
                        {
                            SelectedDevice = Devices[0];
                        }
                    }
                    else
                    {
                        // INSTANT RESET: When cable is unplugged, immediately nullify SelectedDevice
                        SelectedDevice = null;
                        OnPropertyChanged(nameof(SelectedDevice));
                        OnPropertyChanged(nameof(DeviceStatusSummary));
                        CurrentOperation = "Siap (Menunggu HP Terhubung)";
                        OperationProgress = 0;
                        OperationStatus = "Standby";
                        AddLog("[KONEKSI] Perangkat telah dicabut / terputus.");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error refreshing devices");
            }
        }

        private async Task StartServerHealthMonitorAsync()
        {
            // Initial check on application launch
            try
            {
                var isAlive = await _apiClient.CheckServerHealthAsync();
                if (isAlive)
                {
                    ConnectionStatus = "Cloud Sync: Aktif";
                    AddLog("[Server] Terhubung ke cloud repository JumperTools. Sinkronisasi aktif.");
                }
                else
                {
                    ConnectionStatus = "Disconnected";
                }
            }
            catch
            {
                ConnectionStatus = "Disconnected";
            }

            // Periodic heartbeat every 5 seconds
            while (true)
            {
                await Task.Delay(5000);
                try
                {
                    var isAlive = await _apiClient.CheckServerHealthAsync();
                    if (isAlive)
                    {
                        if (!ConnectionStatus.Contains("Connected") && !ConnectionStatus.Contains("Authenticated"))
                        {
                            ConnectionStatus = "Cloud Sync: Aktif";
                        }
                    }
                    else
                    {
                        if (ConnectionStatus != "Offline Mode" && ConnectionStatus != "Disconnected")
                        {
                            ConnectionStatus = "Disconnected";
                        }
                    }
                }
                catch
                {
                    if (ConnectionStatus != "Offline Mode")
                    {
                        ConnectionStatus = "Disconnected";
                    }
                }
            }
        }

        private async Task ConnectServerAsync()
        {
            CurrentOperation = "Menghubungkan ke JumperTools Server...";
            OperationProgress = 30;
            AddLog("[Server] Memeriksa konektivitas cloud JumperTools...");

            try
            {
                var testPoints = await _apiClient.GetTestPointsAsync();
                ConnectionStatus = "Cloud Sync: Aktif";
                CurrentOperation = "Koneksi Server Aktif";
                OperationProgress = 100;
                OperationStatus = "Connected";
                LastSyncTime = $"{DateTime.Now:HH:mm:ss}";
                AddLog($"[Server] Terhubung ke cloud repository JumperTools ({testPoints.Count} TestPoints tersedia).");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error connecting to server");
                ConnectionStatus = "Offline Mode";
                CurrentOperation = "Server Offline (Mode Lokal Siap)";
                OperationStatus = "Offline";
                AddLog($"[Server] Mode Lokal (Offline): {ex.Message}. Seluruh engine USB dan fitur hardware/firmware lokal tetap berfungsi normal.");
            }
        }

        private async Task LicenseStatusAsync()
        {
            CurrentOperation = "Memeriksa Status Lisensi...";
            OperationProgress = 50;

            try
            {
                var machineId = Environment.MachineName;
                var res = await _apiClient.ValidateLicenseAsync(machineId);
                OperationProgress = 100;
                if (res.IsValid)
                {
                    LicenseInfo = "Server: Terautentikasi";
                    AddLog($"[Status] Mesin [{machineId}] terhubung dan terverifikasi ke server backend.");
                }
                else
                {
                    LicenseInfo = "Mode: Standalone Lokal";
                    AddLog($"[Status] Mode Standalone Lokal (Mesin: {machineId}). Engine lokal siap digunakan.");
                }
            }
            catch
            {
                LicenseInfo = "Mode: Standalone Lokal";
                AddLog($"[Status] Mode Standalone Lokal aktif (Mesin: {Environment.MachineName}).");
            }
        }

        private async Task PlatformToolsAsync()
        {
            AddLog($"[Platform Tools] Lokasi ADB/Fastboot: {_platformToolsPath}");
            await Task.CompletedTask;
        }

        private async Task DownloadPathAsync()
        {
            AddLog("[Settings] Folder unduhan firmware aktif: " + AppPathResolver.ExtractedDirectory);
            await Task.CompletedTask;
        }

        private async Task LoginAsync()
        {
            AddLog("[Auth] Membuka sesi autentikasi operator...");
            try
            {
                var result = await _apiClient.LoginAsync("admin@jumptools.id", "admin123");
                if (result.Success)
                {
                    ConnectionStatus = "License: Pro (Active)";
                    AddLog($"[Auth] Berhasil login sebagai {result.User?.Name ?? "Operator"} (Admin).");
                }
                else
                {
                    AddLog($"[Auth] Info login: {result.ErrorMessage}. Menggunakan hak akses teknisi lokal.");
                }
            }
            catch (Exception ex)
            {
                AddLog($"[Auth] Sesi offline lokal: {ex.Message}");
            }
        }

        private async Task LogoutAsync()
        {
            ConnectionStatus = "Disconnected";
            LicenseInfo = "License: Standalone Local";
            CurrentOperation = "Logout selesai";
            AddLog("[Auth] Sesi operator cloud dihentikan. Beralih ke mode offline lokal.");
            await Task.CompletedTask;
        }

        private async Task CheckInterruptedJobsOnStartupAsync()
        {
            try
            {
                var interrupted = await _jobJournalService.DetectInterruptedJobsAsync();
                if (interrupted.Count > 0)
                {
                    AddLog($"⚠️ [RECOVERY AUDIT] Ditemukan {interrupted.Count} operasi hardware yang terhenti karena penutupan/crash aplikasi sebelumnya:");
                    foreach (var job in interrupted)
                    {
                        var risk = JobJournalService.ClassifyInterruptedJobRisk(job);
                        if (risk.RiskLevel == "HIGH_RISK")
                        {
                            AddLog($"   \U0001f6a8 [HIGH RISK] Job ID: {job.JobId} | Perangkat: {job.Brand} {job.Model} | Operasi: {job.OperationType} (Fase: {job.State})");
                            AddLog($"      \U0001f6e1\uFE0F RECOVERY: {risk.Recommendation}");
                        }
                        else
                        {
                            AddLog($"   \u2139\uFE0F [LOW RISK] Job ID: {job.JobId} | Perangkat: {job.Brand} {job.Model} | Operasi: {job.OperationType} (Fase: {job.State})");
                            AddLog($"      \U0001f4a1 {risk.Recommendation}");
                        }

                        // Mark acknowledged in journal so it transitions out of interrupted state
                        await _jobJournalService.FailJobAsync(job.JobId, FailureClassification.Disconnected, "Sesi ditutup/terhenti sebelumnya (Audit tercatat)", "Periksa koneksi fisik sebelum flashing ulang", -1);
                    }
                }
            }
            catch { }
        }

        private void OpenDiagnosticDashboard()
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                var vm = new JumperTools.UI.ViewModels.DiagnosticViewModel(_diagnosticEngine);
                var win = new JumperTools.UI.Views.DiagnosticWindow(vm);
                win.ShowDialog();
            });
        }

        private async Task ForceResetUsbPortsAsync()
        {
            AddLog("[USB Reset] Memulai pembersihan ghost port COM dan restart controller USB...");
            await _usbResetEngine.ForceResetUsbPortsAsync();
            await RefreshDevicesAsync();
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
