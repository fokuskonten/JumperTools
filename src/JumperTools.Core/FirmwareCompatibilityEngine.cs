using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using JumperTools.Models;

namespace JumperTools.Core
{
    /// <summary>
    /// Engine for verifying firmware package compatibility against target device hardware
    /// </summary>
    public class FirmwareCompatibilityEngine
    {
        public PreflightEvaluation ValidateCompatibility(DeviceCapabilityProfile? deviceProfile, FirmwarePackageMetadata firmwareMeta)
        {
            var eval = new PreflightEvaluation();

            if (deviceProfile == null)
            {
                eval.Warnings.Add("Profil kapabilitas perangkat tidak terdefinisi secara spesifik. Melakukan validasi generic.");
                eval.Level = PreflightLevel.Warning;
                return eval;
            }

            // 1. Brand validation
            if (!string.IsNullOrEmpty(firmwareMeta.Brand) && !string.IsNullOrEmpty(deviceProfile.Brand))
            {
                if (!string.Equals(firmwareMeta.Brand, deviceProfile.Brand, StringComparison.OrdinalIgnoreCase))
                {
                    eval.Level = PreflightLevel.Block;
                    eval.BlockingReasons.Add($"Brand firmware ({firmwareMeta.Brand}) tidak cocok dengan brand perangkat ({deviceProfile.Brand}). Flashing dibatalkan demi keamanan.");
                    eval.RecommendedFix = "Gunakan file firmware resmi yang sesuai dengan merek ponsel.";
                    return eval;
                }
            }

            // 2. Model / Variant validation
            if (!string.IsNullOrEmpty(firmwareMeta.TargetModel) && !string.IsNullOrEmpty(deviceProfile.Model))
            {
                if (!deviceProfile.Model.Contains(firmwareMeta.TargetModel, StringComparison.OrdinalIgnoreCase) &&
                    !firmwareMeta.TargetModel.Contains(deviceProfile.Model, StringComparison.OrdinalIgnoreCase))
                {
                    eval.Warnings.Add($"Model firmware [{firmwareMeta.TargetModel}] berbeda dengan model terdeteksi [{deviceProfile.Model}]. Periksa kembali varian hardware.");
                    if (eval.Level == PreflightLevel.Pass) eval.Level = PreflightLevel.Warning;
                }
            }

            // 3. SoC Platform validation
            if (!string.IsNullOrEmpty(firmwareMeta.SocPlatform) && !string.IsNullOrEmpty(deviceProfile.SocPlatform))
            {
                if (!string.Equals(firmwareMeta.SocPlatform, deviceProfile.SocPlatform, StringComparison.OrdinalIgnoreCase))
                {
                    eval.Level = PreflightLevel.Block;
                    eval.BlockingReasons.Add($"Chipset firmware [{firmwareMeta.SocPlatform}] tidak kompatibel dengan SoC ponsel [{deviceProfile.SocPlatform}].");
                    eval.RecommendedFix = $"Pastikan file firmware ditujukan untuk arsitektur SoC {deviceProfile.SocPlatform}.";
                    return eval;
                }
            }

            eval.Messages.Add($"[Compatibility OK] Firmware {firmwareMeta.BuildVersion} cocok untuk {deviceProfile.Brand} {deviceProfile.Model}");
            return eval;
        }

        /// <summary>
        /// Inspects firmware file or directory on disk to verify structural integrity (Scatter, Rawprogram, PAC, or Tar.md5)
        /// </summary>
        public PreflightEvaluation ValidateFirmwarePackagePath(string path, string? expectedSoc = null)
        {
            var eval = new PreflightEvaluation();

            if (string.IsNullOrWhiteSpace(path))
            {
                eval.Level = PreflightLevel.Block;
                eval.BlockingReasons.Add("Jalur berkas firmware kosong.");
                eval.RecommendedFix = "Pilih berkas firmware yang valid.";
                return eval;
            }

            if (!File.Exists(path) && !Directory.Exists(path))
            {
                eval.Level = PreflightLevel.Block;
                eval.BlockingReasons.Add($"Berkas atau direktori firmware tidak ditemukan: {path}");
                eval.RecommendedFix = "Pastikan berkas firmware ada di drive lokal dan tidak terhapus.";
                return eval;
            }

            if (File.Exists(path))
            {
                var fi = new FileInfo(path);
                if (fi.Length == 0)
                {
                    eval.Level = PreflightLevel.Block;
                    eval.BlockingReasons.Add($"Berkas firmware berukuran 0-byte (kosong/korup): {Path.GetFileName(path)}");
                    eval.RecommendedFix = "Unduh ulang berkas firmware dari sumber resmi.";
                    return eval;
                }

                var ext = Path.GetExtension(path).ToLowerInvariant();
                if (ext == ".pac" && expectedSoc != null && !expectedSoc.Contains("unisoc", StringComparison.OrdinalIgnoreCase) && !expectedSoc.Contains("spd", StringComparison.OrdinalIgnoreCase))
                {
                    eval.Warnings.Add("Berkas PAC adalah format Unisoc/Spreadtrum, namun perangkat terdeteksi berarsitektur berbeda.");
                    if (eval.Level == PreflightLevel.Pass) eval.Level = PreflightLevel.Warning;
                }
                else if (ext == ".tar" || ext == ".md5")
                {
                    eval.Messages.Add($"[Samsung Package Detected] {Path.GetFileName(path)}");
                }

                eval.Messages.Add($"[Firmware File Valid] {Path.GetFileName(path)} ({fi.Length / (1024 * 1024):N1} MB)");
            }
            else if (Directory.Exists(path))
            {
                var files = Directory.GetFiles(path);
                var hasScatter = files.Any(f => Path.GetFileName(f).Contains("scatter", StringComparison.OrdinalIgnoreCase));
                var hasRawprogram = files.Any(f => Path.GetFileName(f).Contains("rawprogram", StringComparison.OrdinalIgnoreCase));

                if (hasScatter)
                {
                    eval.Messages.Add("[MediaTek Scatter ROM structure detected]");
                }
                else if (hasRawprogram)
                {
                    eval.Messages.Add("[Qualcomm EDL RawProgram ROM structure detected]");
                }
            }

            return eval;
        }
    }
}
