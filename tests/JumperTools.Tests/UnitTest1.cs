using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using JumperTools.Core;
using JumperTools.Models;
using Xunit;

namespace JumperTools.Tests
{
    public class HardwareProtocolParityTests
    {
        [Fact]
        public async Task HuaweiKirinEngine_RebootDeviceAsync_WhenPortSpecified_ShouldAttemptReboot()
        {
            var engine = new HuaweiKirinEngine();
            // Test with mock port: should gracefully attempt serial open and return false if hardware not connected
            var result = await engine.RebootDeviceAsync("COM999", "fastboot", CancellationToken.None);
            Assert.False(result); // Port doesn't exist, must fail gracefully without unhandled exception
        }

        [Fact]
        public void MediaTekBrom_ChipsetResolver_ShouldSupportFullIndonesianDimensityAndHelioLineup()
        {
            var mtkEngine = new MediaTekBromEngine(new ToolExecutionEngine("tools"));

            // Helio Series
            Assert.Contains("Helio A22", mtkEngine.GetChipsetName(0x6761));
            Assert.Contains("Helio P35", mtkEngine.GetChipsetName(0x6765));
            Assert.Contains("Helio G80", mtkEngine.GetChipsetName(0x6769));
            Assert.Contains("G90T", mtkEngine.GetChipsetName(0x6785));
            Assert.Contains("G99", mtkEngine.GetChipsetName(0x6781));

            // Dimensity Series
            Assert.Contains("Dimensity 700", mtkEngine.GetChipsetName(0x6833));
            Assert.Contains("Dimensity 900", mtkEngine.GetChipsetName(0x6877));
            Assert.Contains("Dimensity 1200", mtkEngine.GetChipsetName(0x6893));
            Assert.Contains("Dimensity 9000", mtkEngine.GetChipsetName(0x6983));

            // Unknown fallback
            Assert.Equal("Unknown MTK (0xFFFF)", mtkEngine.GetChipsetName(0xFFFF));
        }

        [Fact]
        public void SamsungAtEngine_ExtractValue_ShouldHandleParenthesesAndEdgeCases()
        {
            Assert.Equal("SM-A515F", SamsungAtEngine.ExtractValue("MN(SM-A515F)"));
            Assert.Equal("358941092837194", SamsungAtEngine.ExtractValue("IMEI(358941092837194)"));
            Assert.Equal("plain_value", SamsungAtEngine.ExtractValue("plain_value"));
            Assert.Equal("", SamsungAtEngine.ExtractValue("MODEL()"));
        }

        [Fact]
        public void PartitionManager_ClassifyPartitionRisk_ComprehensiveSecurityMatrix()
        {
            // Radio & Security Partitions -> Must be CriticalRadio
            var criticalPartitions = new[] { "nvram", "nvdata", "sec_efs", "efs", "persist", "modemst1", "modemst2", "fsg", "tz", "xbl", "rpmb", "preloader" };
            foreach (var part in criticalPartitions)
            {
                Assert.Equal(PartitionRiskLevel.CriticalRadio, PartitionManagerEngine.ClassifyPartitionRisk(part));
                Assert.True(PartitionManagerEngine.IsSensitiveSecurityPartition(part), $"Partition {part} must be flagged sensitive");
            }

            // Kernel & Boot Partitions -> Must be Warning
            var warningPartitions = new[] { "boot", "recovery", "super", "system", "vendor" };
            foreach (var part in warningPartitions)
            {
                Assert.Equal(PartitionRiskLevel.Warning, PartitionManagerEngine.ClassifyPartitionRisk(part));
                Assert.False(PartitionManagerEngine.IsSensitiveSecurityPartition(part), $"Partition {part} should not be CriticalRadio");
            }

            // Userdata & Cache -> Must be Safe
            Assert.Equal(PartitionRiskLevel.Safe, PartitionManagerEngine.ClassifyPartitionRisk("userdata"));
            Assert.Equal(PartitionRiskLevel.Safe, PartitionManagerEngine.ClassifyPartitionRisk("cache"));
        }

        [Fact]
        public void FastbootEngine_ParseVariables_ShouldExtractAntiRollbackAndSlot()
        {
            var rawOutput = @"
(bootloader) product: sweet
(bootloader) anti: 2
(bootloader) unlocked: no
(bootloader) secure: yes
(bootloader) serialno: 7a8b9c0d
(bootloader) current-slot: _b
(bootloader) max-download-size: 0x20000000
all: done
Finished. Total time: 0.045s
";
            var info = FastbootEngine.ParseVariables(rawOutput);

            Assert.Equal("sweet", info.Product);
            Assert.False(info.IsUnlocked);
            Assert.True(info.IsSecure);
            Assert.Equal("7a8b9c0d", info.SerialNumber);
            Assert.Equal("_b", info.CurrentSlot);
            Assert.Equal("2", info.RawVariables["anti"]);
            Assert.Equal("0x20000000", info.MaxDownloadSize);
        }

        [Fact]
        public void DeviceDetectionEngine_ExtractImei_ShouldDecodeBothPlainAndParcelUtf16Formats()
        {
            // Case 1: Real Android Parcel UTF-16 dump with dots from 'service call iphonesubinfo 1'
            var parcelOutput = @"Result: Parcel(
  0x00000000: 00000000 0000000f 00380036 00310034 '........8.6.1.4.'
  0x00000010: 00330030 00300035 00310032 00330034 '.3.0.0.5.1.2.3.4.'
  0x00000020: 00350036 00000037                   '.5.6.7.........')";
            var imeiParcel = DeviceDetectionEngine.ExtractImei(parcelOutput);
            Assert.Equal("861430051234567", imeiParcel);

            // Case 2: Plain direct 15-digit string
            var plainOutput = "Device IMEI is 864509041234567 reported by modem.";
            var imeiPlain = DeviceDetectionEngine.ExtractImei(plainOutput);
            Assert.Equal("864509041234567", imeiPlain);

            // Case 3: Empty / Not found
            Assert.Equal("Not found", DeviceDetectionEngine.ExtractImei(""));
            Assert.Equal("Not found", DeviceDetectionEngine.ExtractImei("Error: permission denied"));
        }
    }
}
