using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using JumperTools.Core;
using JumperTools.Models;
using Xunit;

namespace JumperTools.Tests
{
    public class PreflightEngineTests
    {
        private readonly ToolRegistry _toolRegistry;
        private readonly PreflightEngine _engine;

        public PreflightEngineTests()
        {
            _toolRegistry = new ToolRegistry(Path.GetTempPath());
            _engine = new PreflightEngine(_toolRegistry);
        }

        [Fact]
        public async Task Preflight_NullDevice_ShouldReturnBlock()
        {
            var eval = await _engine.EvaluateAsync(null, OperationType.BypassFrp);
            Assert.Equal(PreflightLevel.Block, eval.Level);
            Assert.True(eval.IsBlocked);
            Assert.NotEmpty(eval.BlockingReasons);
        }

        [Fact]
        public async Task Preflight_RestrictedOperation_ShouldReturnBlock()
        {
            var device = new Device { DeviceId = "DEV_001", Brand = "Oppo", Model = "Reno 5" };
            var capability = new DeviceCapabilityProfile
            {
                Brand = "Oppo",
                Model = "Reno 5",
                RestrictedOperations = new List<OperationType> { OperationType.UnlockBootloader }
            };

            var eval = await _engine.EvaluateAsync(device, OperationType.UnlockBootloader, capabilityProfile: capability);
            Assert.Equal(PreflightLevel.Block, eval.Level);
            Assert.True(eval.IsBlocked);
            Assert.Contains(eval.BlockingReasons, r => r.Contains("dibatasi"));
        }

        [Fact]
        public async Task Preflight_MissingRequiredTool_ShouldReturnBlock()
        {
            var device = new Device { DeviceId = "DEV_002", Brand = "Qualcomm", Model = "Snapdragon 888" };
            var eval = await _engine.EvaluateAsync(device, OperationType.FlashPartition, requiredToolName: "non_existent_special_tool");

            Assert.Equal(PreflightLevel.Block, eval.Level);
            Assert.True(eval.IsBlocked);
            Assert.Contains(eval.BlockingReasons, r => r.Contains("tidak ditemukan"));
        }

        [Fact]
        public async Task Preflight_SensitivePartition_ShouldTriggerWarning()
        {
            var device = new Device { DeviceId = "DEV_003", Brand = "Xiaomi", Model = "Poco X3" };
            var eval = await _engine.EvaluateAsync(device, OperationType.FlashPartition, targetPartition: "modemst1");

            Assert.Equal(PreflightLevel.Warning, eval.Level);
            Assert.False(eval.IsBlocked);
            Assert.NotEmpty(eval.Warnings);
            Assert.Contains(eval.Warnings, w => w.Contains("auto-backup"));
        }

        [Fact]
        public async Task Preflight_NonExistentFirmware_ShouldReturnBlock()
        {
            var device = new Device { DeviceId = "DEV_004", Brand = "Samsung", Model = "A10s" };
            var eval = await _engine.EvaluateAsync(device, OperationType.FlashFirmware, firmwareFilePath: "C:\\non_existent_folder\\firmware.zip");

            Assert.Equal(PreflightLevel.Block, eval.Level);
            Assert.True(eval.IsBlocked);
            Assert.Contains(eval.BlockingReasons, r => r.Contains("tidak ditemukan"));
        }
    }
}
