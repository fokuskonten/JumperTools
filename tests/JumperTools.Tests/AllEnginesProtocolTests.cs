using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using JumperTools.Core;
using JumperTools.Models;
using Xunit;

namespace JumperTools.Tests
{
    public class AllEnginesProtocolTests
    {
        [Fact]
        public void TestPointRepository_ShouldReturnPopularIndonesianModels()
        {
            var repo = new TestPointRepository();
            var all = repo.GetAll();

            Assert.NotEmpty(all);
            Assert.True(all.Count >= 10);

            var xiaomiResults = repo.Search("Redmi");
            Assert.NotEmpty(xiaomiResults);
            Assert.Contains(xiaomiResults, x => x.MarketingName.Contains("Redmi Note 8"));

            var oppoResults = repo.Search("Oppo");
            Assert.NotEmpty(oppoResults);

            var vivoResults = repo.Search("Vivo");
            Assert.NotEmpty(vivoResults);

            var samsungResults = repo.Search("Samsung");
            Assert.NotEmpty(samsungResults);
        }

        [Fact]
        public void MediaTekBrom_ChipsetResolver_ShouldMapCorrectHwCodes()
        {
            var mtkEngine = new MediaTekBromEngine(new ToolExecutionEngine("tools"));

            var g85Name = mtkEngine.GetChipsetName(0x6769);
            Assert.Contains("Helio G80 / G85", g85Name);

            var g99Name = mtkEngine.GetChipsetName(0x6781);
            Assert.Contains("Helio G96 / G99", g99Name);

            var dimensity700 = mtkEngine.GetChipsetName(0x6833);
            Assert.Contains("Dimensity 700", dimensity700);

            var p35Name = mtkEngine.GetChipsetName(0x6765);
            Assert.Contains("Helio P35", p35Name);
        }

        [Fact]
        public void PartitionManager_DefaultTable_ShouldContainCorePartitions()
        {
            var engine = new PartitionManagerEngine();
            var partitions = engine.GetDefaultPartitionTable();

            Assert.NotEmpty(partitions);
            Assert.Contains(partitions, p => p.Name == "boot");
            Assert.Contains(partitions, p => p.Name == "recovery");
            Assert.Contains(partitions, p => p.Name == "vbmeta");
            Assert.Contains(partitions, p => p.Name == "persist");
            Assert.Contains(partitions, p => p.Name == "frp");
            Assert.Contains(partitions, p => p.Name == "nvram");
        }

        [Fact]
        public void AppleIosEngine_DfuGuide_ShouldGenerateCorrectStepsForDifferentModels()
        {
            var appleEngine = new AppleIosEngine();

            var guideNew = appleEngine.GetDfuGuide("iPhone 13 Pro");
            Assert.Contains("Volume Atas", guideNew);
            Assert.Contains("Volume Bawah", guideNew);
            Assert.Contains("Power", guideNew);

            var guideOld = appleEngine.GetDfuGuide("iPhone 6s");
            Assert.Contains("Home", guideOld);
            Assert.Contains("Power", guideOld);
        }

        [Fact]
        public void SamsungAtEngine_ParseDevConInfo_ShouldExtractModelAndImeiFromDualFormats()
        {
            // Format 1: Standard Key-Value format
            var devInfo1 = new SamsungDeviceInfo();
            var mockDevconInfo1 = "+DEVCONINFO: Model=SM-A515F, IMEI=358941092837194, SWVER=A515FXXU5FVD2, CSC=XID\r\nOK\r\n";
            SamsungAtEngine.ParseDevConInfo(mockDevconInfo1, devInfo1);

            Assert.Equal("SM-A515F", devInfo1.Model);
            Assert.Equal("358941092837194", devInfo1.Imei);
            Assert.Equal("A515FXXU5FVD2", devInfo1.SoftwareVersion);
            Assert.Equal("XID", devInfo1.Csc);

            // Format 2: Parentheses tagged format
            var devInfo2 = new SamsungDeviceInfo();
            var mockDevconInfo2 = "MN(SM-S908B)\r\nIMEI(351234567890123)\r\nSN(R5CW123456)\r\nCSC(OLM)\r\nVER(S908BXXU2AVF1)\r\nOS_VER(12)\r\n";
            SamsungAtEngine.ParseDevConInfo(mockDevconInfo2, devInfo2);

            Assert.Equal("SM-S908B", devInfo2.Model);
            Assert.Equal("351234567890123", devInfo2.Imei);
            Assert.Equal("R5CW123456", devInfo2.SerialNumber);
            Assert.Equal("OLM", devInfo2.Csc);
            Assert.Equal("S908BXXU2AVF1", devInfo2.SoftwareVersion);
            Assert.Equal("12", devInfo2.AndroidVersion);
        }

        [Fact]
        public void FastbootEngine_VariableParser_ShouldParseFastbootOutput()
        {
            var fastbootOutput = "(bootloader) product: ginkgo\n" +
                                 "(bootloader) unlocked: yes\n" +
                                 "(bootloader) secure: yes\n" +
                                 "(bootloader) serialno: e82910fa\n" +
                                 "(bootloader) version-bootloader: 1.0\n" +
                                 "(bootloader) current-slot: _a\n" +
                                 "all: done\n" +
                                 "Finished. Total time: 0.052s";

            var info = FastbootEngine.ParseVariables(fastbootOutput);

            Assert.Equal("ginkgo", info.Product);
            Assert.True(info.IsUnlocked);
            Assert.True(info.IsSecure);
            Assert.Equal("e82910fa", info.SerialNumber);
            Assert.Equal("1.0", info.BootloaderVersion);
            Assert.Equal("_a", info.CurrentSlot);
        }

        [Fact]
        public async Task FirmwareExtractor_ParseMediaTekScatter_ShouldParseCorrectly()
        {
            var tempFile = Path.GetTempFileName();
            var scatterContent = @"
- partition_index: SYS1
  partition_name: boot
  file_name: boot.img
  is_download: true

- partition_index: SYS2
  partition_name: recovery
  file_name: recovery.img
  is_download: true

- partition_index: SYS3
  partition_name: vbmeta
  file_name: vbmeta.img
  is_download: true
";
            File.WriteAllText(tempFile, scatterContent);

            var extractor = new FirmwareExtractorEngine();
            var list = await extractor.ParseMediaTekScatterAsync(tempFile);

            File.Delete(tempFile);

            Assert.NotEmpty(list);
            Assert.Equal(3, list.Count);
            Assert.Contains(list, x => x.PartitionName == "boot" && x.FileName == "boot.img");
            Assert.Contains(list, x => x.PartitionName == "recovery" && x.FileName == "recovery.img");
            Assert.Contains(list, x => x.PartitionName == "vbmeta" && x.FileName == "vbmeta.img");
        }

        [Fact]
        public void DeviceCapabilities_ShouldHaveAllFlagProperties()
        {
            var caps = new DeviceCapabilities
            {
                SupportsAdb = true,
                SupportsFastboot = true,
                SupportsDownloadMode = true,
                SupportsRecovery = true,
                SupportsEdl = true,
                SupportsOdin = true,
                BootloaderUnlocked = false,
                Rooted = false,
                SupportedOperations = new[] { "Bypass FRP", "Instant UBL", "Format Userdata" }
            };

            Assert.True(caps.SupportsAdb);
            Assert.True(caps.SupportsFastboot);
            Assert.True(caps.SupportsRecovery);
            Assert.True(caps.SupportsEdl);
            Assert.Equal(3, caps.SupportedOperations.Length);
        }

        [Fact]
        public void JobOrderEngine_ReceiptFormatting_ShouldGenerateCorrectThermalAndWhatsAppOutput()
        {
            var jobEngine = new JobOrderEngine();
            var job = new JobOrderInfo
            {
                JobId = "FK-20260827-101",
                CustomerName = "Budi Santoso",
                CustomerPhone = "081234567890",
                DeviceBrand = "Samsung",
                DeviceModel = "Galaxy A51",
                DeviceImei = "358941092837194",
                ServiceType = "Bypass FRP",
                Cost = 150000,
                WarrantyDays = 7,
                Status = JobOrderStatus.Selesai
            };

            var thermalText = jobEngine.GenerateThermalReceiptText(job);
            Assert.Contains("FK-20260827-101", thermalText);
            Assert.Contains("Budi Santoso", thermalText);
            Assert.Contains("Galaxy A51", thermalText);
            Assert.Contains("Rp 150.000", thermalText);

            var waMessage = jobEngine.GenerateWhatsAppMessage(job);
            Assert.Contains("NOTA TANDA TERIMA SERVIS HP", waMessage);
            Assert.Contains("Budi Santoso", waMessage);
            Assert.Contains("Galaxy A51", waMessage);
            Assert.Contains("Rp 150.000", waMessage);
        }

        [Fact]
        public async Task LoaderManager_ModelAndLoaderResolution_ShouldCorrectlyMatchProfilesAndLoaders()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "fk_test_loaders_" + Guid.NewGuid().ToString("N"));
            try
            {
                var loaderMgr = new LoaderManager(tempDir);
                var allModels = loaderMgr.GetAllModels();
                Assert.NotEmpty(allModels);

                var redmi8 = loaderMgr.FindModel("Redmi Note 8");
                Assert.NotNull(redmi8);
                Assert.Equal("Xiaomi", redmi8.Brand);
                Assert.Equal("Qualcomm", redmi8.Architecture);

                var loader = loaderMgr.GetLoaderForModel(redmi8);
                Assert.NotNull(loader);
                Assert.Equal("prog_emmc_firehose_sm6125_ddr.mbn", loader.FileName);

                var notCachedYet = await loaderMgr.EnsureLoaderAvailableAsync(loader);
                Assert.Null(notCachedYet);

                var dir = Path.GetDirectoryName(loader.LocalPath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                await File.WriteAllBytesAsync(loader.LocalPath, new byte[] { 0x7F, 0x45, 0x4C, 0x46 });

                var ensuredPath = await loaderMgr.EnsureLoaderAvailableAsync(loader);
                Assert.NotNull(ensuredPath);
                Assert.True(File.Exists(ensuredPath));
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    try { Directory.Delete(tempDir, true); } catch { }
                }
            }
        }

        [Fact]
        public async Task DriverAssistantEngine_ScanStatus_ShouldIncludeUsbDkAndFilterDrivers()
        {
            var driverEngine = new DriverAssistantEngine();
            var list = await driverEngine.CheckAllDriversAsync();

            Assert.NotEmpty(list);
            Assert.Contains(list, d => d.DriverName.Contains("UsbDK") && d.Category == "Filter Driver" && d.HardwareId == "USBDK_SERVICE");
            Assert.Contains(list, d => d.DriverName.Contains("Libusb") && d.Category == "Filter Driver" && d.HardwareId == "LIBUSB_FILTER");
            Assert.Contains(list, d => d.DriverName.Contains("QDLoader 9008"));
            Assert.Contains(list, d => d.DriverName.Contains("MediaTek"));
        }

        [Fact]
        public async Task JumperToolsApiClient_LoaderCatalogAndDownloadMethods_ShouldExistAndBeCallable()
        {
            var client = new JumperTools.Services.JumperToolsApiClient("http://localhost:8090");
            
            // Should gracefully return empty list when server offline/endpoint not yet seeded
            var catalog = await client.GetLoadersCatalogAsync("Xiaomi", "Snapdragon 665");
            Assert.NotNull(catalog);

            // Should gracefully return false when downloading non-existent/offline endpoint
            var tempDest = Path.Combine(Path.GetTempPath(), "test_loader_" + Guid.NewGuid().ToString("N") + ".mbn");
            try
            {
                var downloaded = await client.DownloadLoaderFileAsync("dummy_test_loader.mbn", tempDest);
                // Offline fallback returns false
                Assert.False(downloaded);
            }
            finally
            {
                if (File.Exists(tempDest)) File.Delete(tempDest);
            }
        }

        [Fact]
        public async Task FirmwareExtractorEngine_QualcommRawProgram_ShouldParseXmlCorrectly()
        {
            var extractor = new FirmwareExtractorEngine();
            var tempXml = Path.Combine(Path.GetTempPath(), "rawprogram0_test_" + Guid.NewGuid().ToString("N") + ".xml");
            var mockXml = @"<?xml version=""1.0"" ?>
<data>
  <program SECTOR_SIZE_IN_BYTES=""512"" file_sector_offset=""0"" filename=""boot.img"" label=""boot"" num_partition_sectors=""131072"" physical_partition_number=""0"" size_in_KB=""65536.0"" sparse=""false"" start_byte_hex=""0x4000000"" start_sector=""131072""/>
  <program SECTOR_SIZE_IN_BYTES=""512"" file_sector_offset=""0"" filename=""recovery.img"" label=""recovery"" num_partition_sectors=""131072"" physical_partition_number=""0"" size_in_KB=""65536.0"" sparse=""false"" start_byte_hex=""0x8000000"" start_sector=""262144""/>
  <program SECTOR_SIZE_IN_BYTES=""512"" file_sector_offset=""0"" filename=""vbmeta.img"" label=""vbmeta"" num_partition_sectors=""16384"" physical_partition_number=""0"" size_in_KB=""8192.0"" sparse=""false"" start_byte_hex=""0xc000000"" start_sector=""393216""/>
</data>";

            try
            {
                await File.WriteAllTextAsync(tempXml, mockXml);
                var list = await extractor.ParseQualcommRawProgramAsync(tempXml);

                Assert.NotEmpty(list);
                Assert.Equal(3, list.Count);
                Assert.Contains(list, p => p.PartitionName == "boot" && p.FileName == "boot.img");
                Assert.Contains(list, p => p.PartitionName == "recovery" && p.FileName == "recovery.img");
                Assert.Contains(list, p => p.PartitionName == "vbmeta" && p.FileName == "vbmeta.img");
            }
            finally
            {
                if (File.Exists(tempXml)) File.Delete(tempXml);
            }
        }

        [Fact]
        public void SignalRepairEngine_ScanAvailableQcn_ShouldHandleEmptyOrValidDir()
        {
            var engine = new SignalRepairEngine();
            var qcnList = engine.ScanAvailableQcnFiles();
            var nvramList = engine.ScanAvailableNvramFiles();

            Assert.NotNull(qcnList);
            Assert.NotNull(nvramList);
        }

        [Fact]
        public void RecoveryInstallerEngine_ScanAvailableRecoveries_ShouldReturnList()
        {
            var engine = new RecoveryInstallerEngine();
            var list = engine.ScanAvailableRecoveries();

            Assert.NotNull(list);
        }

        [Fact]
        public void PartitionManagerEngine_GetDefaultPartitionTable_ShouldContainCriticalPartitions()
        {
            var engine = new PartitionManagerEngine();
            var partitions = engine.GetDefaultPartitionTable();

            Assert.NotNull(partitions);
            Assert.NotEmpty(partitions);
            Assert.Contains(partitions, p => p.Name == "boot");
            Assert.Contains(partitions, p => p.Name == "recovery");
            Assert.Contains(partitions, p => p.Name == "vbmeta");
            Assert.Contains(partitions, p => p.Name == "nvram");
            Assert.Contains(partitions, p => p.Name == "modemst1");
        }

        [Fact]
        public async Task UnisocSpdEngine_FlashPac_InvalidFile_ShouldReturnFalse()
        {
            var engine = new UnisocSpdEngine();
            var ok = await engine.FlashPacFirmwareAsync("non_existent_file.pac");
            Assert.False(ok);
        }

        [Fact]
        public async Task AppleIosEngine_RebootAppleDevice_ShouldReturnTrueOrHandled()
        {
            var engine = new AppleIosEngine();
            var ok = await engine.RebootAppleDeviceAsync("mock_serial", "normal");
            Assert.True(ok);
        }

        [Fact]
        public void PartitionManager_SensitivePartitionCheck_ShouldIdentifySecurityPartitions()
        {
            Assert.True(PartitionManagerEngine.IsSensitiveSecurityPartition("modemst1"));
            Assert.True(PartitionManagerEngine.IsSensitiveSecurityPartition("MODEMST2"));
            Assert.True(PartitionManagerEngine.IsSensitiveSecurityPartition("nvram"));
            Assert.True(PartitionManagerEngine.IsSensitiveSecurityPartition("nvdata"));
            Assert.True(PartitionManagerEngine.IsSensitiveSecurityPartition("persist"));
            Assert.True(PartitionManagerEngine.IsSensitiveSecurityPartition("xbl"));
            Assert.True(PartitionManagerEngine.IsSensitiveSecurityPartition("preloader"));
            Assert.True(PartitionManagerEngine.IsSensitiveSecurityPartition("rpmb"));

            // Non-sensitive standard partitions
            Assert.False(PartitionManagerEngine.IsSensitiveSecurityPartition("boot"));
            Assert.False(PartitionManagerEngine.IsSensitiveSecurityPartition("recovery"));
            Assert.False(PartitionManagerEngine.IsSensitiveSecurityPartition("system"));
            Assert.False(PartitionManagerEngine.IsSensitiveSecurityPartition("userdata"));
        }

        [Fact]
        public void ToolRegistry_CategoryFallbacks_ShouldResolveAlternativePaths()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "fk_tool_reg_test_" + Guid.NewGuid().ToString("N"));
            try
            {
                var appleDir = Path.Combine(tempDir, "apple");
                var mtkDir = Path.Combine(tempDir, "mtkclient");
                var samsungDir = Path.Combine(tempDir, "samsung");
                Directory.CreateDirectory(appleDir);
                Directory.CreateDirectory(mtkDir);
                Directory.CreateDirectory(samsungDir);

                // Create dummy files
                File.WriteAllText(Path.Combine(appleDir, "ideviceinfo.exe"), "dummy apple exe");
                File.WriteAllText(Path.Combine(mtkDir, "mtk.py"), "print('mtk python')");
                File.WriteAllText(Path.Combine(samsungDir, "odin.exe"), "dummy odin exe");

                var registry = new ToolRegistry(tempDir);

                var appleTool = registry.GetTool("ideviceinfo");
                Assert.NotNull(appleTool);
                Assert.Equal(ToolStatus.Ready, appleTool.Status);
                Assert.True(File.Exists(appleTool.ResolvedFullPath));

                var mtkTool = registry.GetTool("mtkclient");
                Assert.NotNull(mtkTool);
                Assert.Equal(ToolStatus.Ready, mtkTool.Status);

                var odinTool = registry.GetTool("odin");
                Assert.NotNull(odinTool);
                Assert.Equal(ToolStatus.Ready, odinTool.Status);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    try { Directory.Delete(tempDir, true); } catch { }
                }
            }
        }

        [Fact]
        public void XiaomiSideload_ResponseEvaluator_ShouldRejectFalsePositives()
        {
            // Valid responses tested against real XiaomiSideloadEngine method
            Assert.True(XiaomiSideloadEngine.EvaluateWipeResponse("OKAY [  0.124s]"));
            Assert.True(XiaomiSideloadEngine.EvaluateWipeResponse("Formatting /data complete: success"));
            Assert.True(XiaomiSideloadEngine.EvaluateWipeResponse("wiping data: done"));

            // False positive traps that MUST be rejected (evaluated as false)
            Assert.False(XiaomiSideloadEngine.EvaluateWipeResponse(""));
            Assert.False(XiaomiSideloadEngine.EvaluateWipeResponse("   "));
            Assert.False(XiaomiSideloadEngine.EvaluateWipeResponse("closed"));
            Assert.False(XiaomiSideloadEngine.EvaluateWipeResponse("error: closed"));
            Assert.False(XiaomiSideloadEngine.EvaluateWipeResponse("permission denied"));
            Assert.False(XiaomiSideloadEngine.EvaluateWipeResponse("unknown command raw-command"));
            Assert.False(XiaomiSideloadEngine.EvaluateWipeResponse("failed to execute wipe"));
            Assert.False(XiaomiSideloadEngine.EvaluateWipeResponse("not allowed in locked state"));
            Assert.False(XiaomiSideloadEngine.EvaluateWipeResponse("cannot format partition"));
        }

        [Fact]
        public void DriverIntelligence_Scan_ShouldIncludeCoreProfiles()
        {
            var driverSvc = new DriverIntelligenceService();
            Assert.NotNull(driverSvc);
            
            // Should execute without throwing unhandled exceptions
            var results = driverSvc.ScanDrivers();
            Assert.NotEmpty(results);
            Assert.Contains(results, d => d.Protocol == "Qualcomm EDL 9008");
            Assert.Contains(results, d => d.Protocol == "MediaTek BROM");
            Assert.Contains(results, d => d.Protocol == "Unisoc BSL");
            Assert.Contains(results, d => d.Protocol == "Samsung AT / Odin");
            Assert.Contains(results, d => d.Protocol == "Android ADB");
            Assert.Contains(results, d => d.Protocol == "Fastboot");
            Assert.Contains(results, d => d.Protocol == "Apple iOS");
        }

        [Fact]
        public async Task BrandSpecialOperations_RefurbishedRom_ShouldExecuteWithoutExceptions()
        {
            var engine = new BrandSpecialOperationsEngine(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "platform-tools"));
            // When no physical device is connected, it should gracefully report false without throwing unhandled exceptions
            var auditOk = await engine.AuditRefurbishedRomAsync("NON_EXISTENT_SERIAL");
            var cleanOk = await engine.CleanRefurbishedRomAsync("NON_EXISTENT_SERIAL");

            Assert.False(auditOk);
            Assert.False(cleanOk);
        }

        [Fact]
        public void JobOrderEngine_GenerateJobIdAndThermalReceipt_ShouldBeValidAndConsistent()
        {
            var engine = new JobOrderEngine();
            var jobId = engine.GenerateJobId();
            Assert.StartsWith("FK-", jobId);

            var job = new JobOrderInfo
            {
                JobId = jobId,
                CustomerName = "Ahmad Teknisi",
                CustomerPhone = "081234567890",
                DeviceBrand = "Xiaomi",
                DeviceModel = "Redmi Note 10 Pro",
                DeviceImei = "861234567890123",
                ServiceType = "Bypass Mi Account",
                Cost = 175000,
                WarrantyDays = 30,
                Status = JobOrderStatus.Selesai,
                CreatedAt = DateTime.Now
            };

            var receiptText = engine.GenerateThermalReceiptText(job);
            Assert.Contains("Ahmad Teknisi", receiptText);
            Assert.Contains("Redmi Note 10 Pro", receiptText);
            Assert.Contains("175.000", receiptText);
            Assert.Contains("30 Hari", receiptText);

            var waMsg = engine.GenerateWhatsAppMessage(job);
            Assert.Contains("Ahmad Teknisi", waMsg);
            Assert.Contains(jobId, waMsg);
        }

        [Fact]
        public void TestPointRepository_QueryAndFilter_ShouldBeCaseInsensitive()
        {
            var repo = new TestPointRepository();
            var lower = repo.Search("samsung");
            var upper = repo.Search("SAMSUNG");

            Assert.Equal(lower.Count, upper.Count);
            Assert.NotEmpty(lower);
        }

        [Fact]
        public void DatabaseContext_WalModeAndJobOrders_ShouldPersistAndQueryCorrectly()
        {
            var tempDb = Path.Combine(Path.GetTempPath(), $"fk_test_db_{Guid.NewGuid():N}.db");
            try
            {
                using (var db = new JumperTools.Data.DatabaseContext(tempDb))
                {
                    var jobEngine = new JobOrderEngine(db);
                    var newJob = new JobOrderInfo
                    {
                        JobId = jobEngine.GenerateJobId(),
                        CustomerName = "Budi Santoso",
                        CustomerPhone = "08198765432",
                        DeviceBrand = "Samsung",
                        DeviceModel = "Galaxy A52",
                        ServiceType = "FRP Bypass MTP",
                        Cost = 120000,
                        Status = JobOrderStatus.Selesai
                    };

                    jobEngine.SaveJobToDb(newJob);

                    var allJobs = jobEngine.GetAllSavedJobs();
                    Assert.Single(allJobs);
                    Assert.Equal("Budi Santoso", allJobs[0].CustomerName);
                    Assert.Equal("Galaxy A52", allJobs[0].DeviceModel);

                    var retrieved = db.GetJobOrder(newJob.JobId);
                    Assert.NotNull(retrieved);
                    Assert.Equal(120000, retrieved!.Cost);
                }

                // Verify persistent reopen
                using (var dbReopen = new JumperTools.Data.DatabaseContext(tempDb))
                {
                    var jobEngine2 = new JobOrderEngine(dbReopen);
                    var jobs = jobEngine2.GetAllSavedJobs();
                    Assert.Single(jobs);
                    Assert.Equal("Budi Santoso", jobs[0].CustomerName);
                }
            }
            finally
            {
                try
                {
                    if (File.Exists(tempDb)) File.Delete(tempDb);
                    var wal = tempDb + "-wal";
                    if (File.Exists(wal)) File.Delete(wal);
                    var shm = tempDb + "-shm";
                    if (File.Exists(shm)) File.Delete(shm);
                }
                catch { }
            }
        }

        [Fact]
        public void TestPointRepository_DatabaseSync_ShouldSeedAndPersistCustomPoints()
        {
            var tempDb = Path.Combine(Path.GetTempPath(), $"fk_test_tp_{Guid.NewGuid():N}.db");
            try
            {
                using (var db = new JumperTools.Data.DatabaseContext(tempDb))
                {
                    var repo = new TestPointRepository(db);
                    var initialCount = repo.GetAll().Count;
                    Assert.True(initialCount > 0);

                    // Add a custom technician test point
                    var customTp = new TestPointInfo
                    {
                        Brand = "Infinix",
                        MarketingName = "Hot 11 Play Custom",
                        ModelName = "X688B",
                        Chipset = "MediaTek Helio G35",
                        Type = PinoutType.MTK_BROM,
                        TestPointLocation = "Titik emas di dekat IC Flash",
                        Instructions = "Hubungkan ke GND",
                        IsVerified = true
                    };

                    repo.AddOrUpdate(customTp);

                    var searchResult = repo.Search("Hot 11 Play Custom");
                    Assert.Single(searchResult);
                }

                // Verify reopen with SQLite persistence
                using (var dbReopen = new JumperTools.Data.DatabaseContext(tempDb))
                {
                    var repoReopen = new TestPointRepository(dbReopen);
                    var customFound = repoReopen.Search("Hot 11 Play Custom");
                    Assert.Single(customFound);
                    Assert.Equal("Infinix", customFound[0].Brand);
                }
            }
            finally
            {
                try
                {
                    if (File.Exists(tempDb)) File.Delete(tempDb);
                    var wal = tempDb + "-wal";
                    if (File.Exists(wal)) File.Delete(wal);
                    var shm = tempDb + "-shm";
                    if (File.Exists(shm)) File.Delete(shm);
                }
                catch { }
            }
        }

        [Fact]
        public void JobOrderEngine_GenerateWhatsAppUrl_ShouldFormatIndonesianNumbersCorrectly()
        {
            var engine = new JobOrderEngine();
            var job = new JobOrderInfo
            {
                JobId = "FK-20260829-999",
                CustomerName = "Siti Rahma",
                CustomerPhone = "0813-4567-8901",
                DeviceBrand = "Oppo",
                DeviceModel = "Reno 8",
                ServiceType = "Ganti LCD & Servis",
                Cost = 450000,
                WarrantyDays = 14,
                Status = JobOrderStatus.Selesai
            };

            var url = engine.GenerateWhatsAppUrl(job);
            Assert.StartsWith("https://wa.me/6281345678901?text=", url);
            Assert.Contains("Siti%20Rahma", url);
            Assert.Contains("FK-20260829-999", url);
        }

        [Fact]
        public void PartitionManager_ClassifyPartitionRisk_ShouldAccuratelyProtectBasebandAndBootloader()
        {
            Assert.Equal(PartitionRiskLevel.CriticalRadio, PartitionManagerEngine.ClassifyPartitionRisk("nvram"));
            Assert.Equal(PartitionRiskLevel.CriticalRadio, PartitionManagerEngine.ClassifyPartitionRisk("sec_efs"));
            Assert.Equal(PartitionRiskLevel.CriticalRadio, PartitionManagerEngine.ClassifyPartitionRisk("persist"));
            Assert.Equal(PartitionRiskLevel.CriticalRadio, PartitionManagerEngine.ClassifyPartitionRisk("modemst1"));
            Assert.Equal(PartitionRiskLevel.CriticalRadio, PartitionManagerEngine.ClassifyPartitionRisk("fsg"));
            Assert.Equal(PartitionRiskLevel.CriticalRadio, PartitionManagerEngine.ClassifyPartitionRisk("tz"));
            Assert.Equal(PartitionRiskLevel.CriticalRadio, PartitionManagerEngine.ClassifyPartitionRisk("xbl"));

            Assert.Equal(PartitionRiskLevel.Warning, PartitionManagerEngine.ClassifyPartitionRisk("boot"));
            Assert.Equal(PartitionRiskLevel.Warning, PartitionManagerEngine.ClassifyPartitionRisk("recovery"));
            Assert.Equal(PartitionRiskLevel.Warning, PartitionManagerEngine.ClassifyPartitionRisk("super"));

            Assert.Equal(PartitionRiskLevel.Safe, PartitionManagerEngine.ClassifyPartitionRisk("cache"));
            Assert.Equal(PartitionRiskLevel.Safe, PartitionManagerEngine.ClassifyPartitionRisk("userdata"));
        }
    }
}



