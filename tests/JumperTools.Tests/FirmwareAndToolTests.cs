using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using JumperTools.Core;
using JumperTools.Models;
using Xunit;

namespace JumperTools.Tests
{
    public class FirmwareAndToolTests
    {
        [Fact]
        public void FirmwareCompatibility_BrandMismatch_ShouldBlock()
        {
            var engine = new FirmwareCompatibilityEngine();
            var device = new DeviceCapabilityProfile { Brand = "Samsung", Model = "Galaxy S21", SocPlatform = "Exynos" };
            var fw = new FirmwarePackageMetadata { Brand = "Xiaomi", TargetModel = "Mi 11", SocPlatform = "Snapdragon" };

            var result = engine.ValidateCompatibility(device, fw);

            Assert.Equal(PreflightLevel.Block, result.Level);
            Assert.True(result.IsBlocked);
            Assert.Contains(result.BlockingReasons, r => r.Contains("Brand"));
        }

        [Fact]
        public void FirmwareCompatibility_SocMismatch_ShouldBlock()
        {
            var engine = new FirmwareCompatibilityEngine();
            var device = new DeviceCapabilityProfile { Brand = "Samsung", Model = "A12", SocPlatform = "MediaTek" };
            var fw = new FirmwarePackageMetadata { Brand = "Samsung", TargetModel = "A12", SocPlatform = "Exynos" };

            var result = engine.ValidateCompatibility(device, fw);

            Assert.Equal(PreflightLevel.Block, result.Level);
            Assert.True(result.IsBlocked);
            Assert.Contains(result.BlockingReasons, r => r.Contains("Chipset"));
        }

        [Fact]
        public async Task ToolRegistry_CorruptedZeroByteFile_ShouldBeFlaggedCorrupted()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), $"test_tools_{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path.Combine(tempDir, "platform-tools"));
            var dummyAdb = Path.Combine(tempDir, "platform-tools", "adb.exe");
            await File.WriteAllBytesAsync(dummyAdb, Array.Empty<byte>()); // 0 bytes

            try
            {
                var registry = new ToolRegistry(tempDir);
                var status = await registry.AuditToolIntegrityAsync("adb");

                Assert.Equal(ToolStatus.Corrupted, status);
            }
            finally
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public async Task ProcessExecutionService_RunEcho_ShouldCaptureOutputAndExitZero()
        {
            var service = new ProcessExecutionService();
            var cmdExe = Path.Combine(Environment.SystemDirectory, "cmd.exe");

            var result = await service.ExecuteAsync(cmdExe, "/c echo FOKUSKONTEN_PRO_TEST");

            Assert.True(result.Success);
            Assert.Equal(0, result.ExitCode);
            Assert.Contains("FOKUSKONTEN_PRO_TEST", result.StandardOutput);
        }

        [Fact]
        public async Task ProcessExecutionService_Timeout_ShouldTerminateGracefully()
        {
            var service = new ProcessExecutionService();
            var cmdExe = Path.Combine(Environment.SystemDirectory, "cmd.exe");

            // Ping with wait of 10 seconds, but timeout is set to 500ms
            var result = await service.ExecuteAsync(cmdExe, "/c ping 127.0.0.1 -n 10", timeoutMs: 500);

            Assert.False(result.Success);
            Assert.True(result.TimedOut);
        }

        [Fact]
        public async Task ToolExecutionEngine_RealPythonMtkHelp_ShouldReturnValidOutput()
        {
            var toolsDir = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "tools"));
            if (!Directory.Exists(toolsDir))
            {
                toolsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools");
            }

            var registry = new ToolRegistry(toolsDir);
            var engine = new ToolExecutionEngine(toolsDir, registry);

            var result = await engine.ExecuteMtkClientAsync("--help", timeoutMs: 15000);
            if (File.Exists(Path.Combine(toolsDir, "runtime", "python", "python.exe")))
            {
                Assert.True(result.Success);
                Assert.NotNull(result.Output);
                Assert.Contains("MTK Flash/Exploit Client", result.Output);
            }
        }

        [Fact]
        public async Task ToolExecutionEngine_RealPythonEdlHelp_ShouldReturnValidOutput()
        {
            var toolsDir = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "tools"));
            if (!Directory.Exists(toolsDir))
            {
                toolsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools");
            }

            var registry = new ToolRegistry(toolsDir);
            var engine = new ToolExecutionEngine(toolsDir, registry);

            var result = await engine.ExecuteQualcommEdlAsync(null, "--help", timeoutMs: 15000);
            if (File.Exists(Path.Combine(toolsDir, "runtime", "python", "python.exe")))
            {
                Assert.True(result.Success);
                Assert.NotNull(result.Output);
                Assert.Contains("Qualcomm", result.Output);
            }
        }

        [Fact]
        public void LoaderManager_Initialization_ShouldFindSeedModelsAndLoaders()
        {
            var loaderMgr = new LoaderManager();
            var allModels = loaderMgr.GetAllModels();
            var allBrands = loaderMgr.GetAllBrands();

            Assert.NotEmpty(allModels);
            Assert.Contains("Xiaomi", allBrands);
            Assert.Contains("Samsung", allBrands);

            var rn8 = loaderMgr.FindModel("Redmi Note 8");
            Assert.NotNull(rn8);
            var loader = loaderMgr.GetLoaderForModel(rn8!);
            Assert.NotNull(loader);
        }

        [Fact]
        public async Task FullStack_GUI_To_Engine_To_PhysicalAssets_Paripurna_ShouldVerifyAllAssets()
        {
            // 1. Verify AppPathResolver SSOT Root
            var root = AppPathResolver.RootDirectory;
            Assert.True(Directory.Exists(root), $"App root directory does not exist: {root}");
            Assert.True(File.Exists(Path.Combine(root, "JumperTools.sln")), "JumperTools.sln must exist in root");

            // 2. Verify Physical Tool Binaries
            Assert.True(File.Exists(AppPathResolver.AdbExecutablePath), $"adb.exe missing at {AppPathResolver.AdbExecutablePath}");
            Assert.True(File.Exists(AppPathResolver.FastbootExecutablePath), $"fastboot.exe missing at {AppPathResolver.FastbootExecutablePath}");
            Assert.True(File.Exists(AppPathResolver.PythonExecutablePath), $"python.exe missing at {AppPathResolver.PythonExecutablePath}");

            // 3. Verify Database Mappings
            Assert.True(File.Exists(AppPathResolver.TestPointsMappingJson), $"testpoints_mapping.json missing at {AppPathResolver.TestPointsMappingJson}");
            Assert.True(File.Exists(AppPathResolver.IspPinoutsMappingJson), $"isp_pinouts_mapping.json missing at {AppPathResolver.IspPinoutsMappingJson}");

            // 4. Verify TestPointRepository Ingestion (3,078 TP + 1,565 ISP = 4,643 items)
            var repo = new TestPointRepository();
            var allItems = repo.GetAll();
            Assert.True(allItems.Count >= 4600, $"Expected >= 4,600 items in physical catalog, but found {allItems.Count}");

            // 5. Verify ToolRegistry Audits All Engines as Ready
            var registry = new ToolRegistry(AppPathResolver.ToolsDirectory);
            var adbStatus = await registry.AuditToolIntegrityAsync("adb");
            var fastbootStatus = await registry.AuditToolIntegrityAsync("fastboot");
            var pythonStatus = await registry.AuditToolIntegrityAsync("python");
            var mtkStatus = await registry.AuditToolIntegrityAsync("mtkclient");
            var edlStatus = await registry.AuditToolIntegrityAsync("qcom-edl");

            Assert.Equal(ToolStatus.Ready, adbStatus);
            Assert.Equal(ToolStatus.Ready, fastbootStatus);
            Assert.Equal(ToolStatus.Ready, pythonStatus);
            Assert.Equal(ToolStatus.Ready, mtkStatus);
            Assert.Equal(ToolStatus.Ready, edlStatus);

            // 6. Verify Physical Hardware Loaders (Firehose + DA)
            var firehoseFiles = Directory.GetFiles(AppPathResolver.FirehoseDirectory, "*.*", SearchOption.AllDirectories);
            var daFiles = Directory.GetFiles(AppPathResolver.DaDirectory, "*.*", SearchOption.AllDirectories);
            Assert.True(firehoseFiles.Length >= 4000, $"Expected >= 4,000 firehose loaders, found {firehoseFiles.Length}");
            Assert.True(daFiles.Length >= 400, $"Expected >= 400 MTK DA loaders, found {daFiles.Length}");
        }
    }
}
