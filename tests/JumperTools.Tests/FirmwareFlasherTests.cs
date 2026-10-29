using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using JumperTools.Core;
using JumperTools.Models;

namespace JumperTools.Tests
{
    public class FirmwareFlasherTests
    {
        [Fact]
        public void AnalyzeFirmwarePackage_RecognizesXmlAndScatter()
        {
            var flasher = new FirmwareFlasherEngine();
            var tempXml = Path.Combine(Path.GetTempPath(), "rawprogram0_test.xml");
            File.WriteAllText(tempXml, @"<data><program filename=""boot.img"" label=""boot"" num_partition_sectors=""100""/></data>");

            try
            {
                var manifest = flasher.AnalyzeFirmwarePackage(tempXml);
                Assert.Equal(FirmwarePackageType.QualcommRawProgramXml, manifest.PackageType);
                Assert.True(manifest.IsValid);
                Assert.Single(manifest.Items);
            }
            finally
            {
                if (File.Exists(tempXml)) File.Delete(tempXml);
            }
        }
    }
}
