using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using JumperTools.Core;
using JumperTools.Models;

namespace JumperTools.Tests
{
    public class PartitionManagerTests
    {
        [Fact]
        public void ClassifyPartitionRisk_IdentifiesCriticalRadioPartitions()
        {
            Assert.Equal(PartitionRiskLevel.CriticalRadio, PartitionManagerEngine.ClassifyPartitionRisk("nvram"));
            Assert.Equal(PartitionRiskLevel.CriticalRadio, PartitionManagerEngine.ClassifyPartitionRisk("nvdata"));
            Assert.Equal(PartitionRiskLevel.CriticalRadio, PartitionManagerEngine.ClassifyPartitionRisk("persist"));
            Assert.Equal(PartitionRiskLevel.CriticalRadio, PartitionManagerEngine.ClassifyPartitionRisk("modemst1"));
            Assert.Equal(PartitionRiskLevel.CriticalRadio, PartitionManagerEngine.ClassifyPartitionRisk("efs"));
        }

        [Fact]
        public void ClassifyPartitionRisk_IdentifiesWarningSystemPartitions()
        {
            Assert.Equal(PartitionRiskLevel.Warning, PartitionManagerEngine.ClassifyPartitionRisk("super"));
            Assert.Equal(PartitionRiskLevel.Warning, PartitionManagerEngine.ClassifyPartitionRisk("system"));
            Assert.Equal(PartitionRiskLevel.Warning, PartitionManagerEngine.ClassifyPartitionRisk("vendor"));
            Assert.Equal(PartitionRiskLevel.Warning, PartitionManagerEngine.ClassifyPartitionRisk("boot"));
        }

        [Fact]
        public void ParseQualcommRawProgramXml_ParsesValidXml()
        {
            var engine = new PartitionManagerEngine();
            var xml = @"<?xml version=""1.0"" ?>
<data>
  <program SECTOR_SIZE_IN_BYTES=""512"" file_sector_offset=""0"" filename=""boot.img"" label=""boot"" num_partition_sectors=""131072"" physical_partition_number=""0"" size_in_KB=""65536.0"" sparse=""false"" start_byte_hex=""0x8000000"" start_sector=""262144""/>
  <program SECTOR_SIZE_IN_BYTES=""512"" file_sector_offset=""0"" filename=""persist.img"" label=""persist"" num_partition_sectors=""65536"" physical_partition_number=""0"" size_in_KB=""32768.0"" sparse=""false"" start_byte_hex=""0x10000000"" start_sector=""524288""/>
</data>";

            var list = engine.ParseQualcommRawProgramXml(xml);
            Assert.Equal(2, list.Count);
            Assert.Equal("boot", list[0].PartitionName);
            Assert.Equal("persist", list[1].PartitionName);
            Assert.Equal(PartitionRiskLevel.CriticalRadio, list[1].RiskLevel);
        }

        [Fact]
        public void ParseMediaTekScatter_ParsesValidScatterText()
        {
            var engine = new PartitionManagerEngine();
            var scatter = @"
- partition_index: SYS0
  partition_name: preloader
  file_name: preloader_raw.bin
  is_download: true
  linear_start_addr: 0x0
  partition_size: 0x40000
- partition_index: SYS1
  partition_name: nvram
  file_name: NONE
  is_download: false
  linear_start_addr: 0x100000
  partition_size: 0x500000
";
            var list = engine.ParseMediaTekScatter(scatter);
            Assert.Equal(2, list.Count);
            Assert.Equal("preloader", list[0].PartitionName);
            Assert.Equal("nvram", list[1].PartitionName);
            Assert.Equal(PartitionRiskLevel.CriticalRadio, list[1].RiskLevel);
        }
    }
}
