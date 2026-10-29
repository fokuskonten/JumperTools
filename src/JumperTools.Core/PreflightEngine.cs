using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using JumperTools.Models;

namespace JumperTools.Core
{
    /// <summary>
    /// Centralized pre-execution safety validator evaluating device, drivers, storage, tools, firmware, and partitions
    /// </summary>
    public class PreflightEngine
    {
        private readonly ToolRegistry _toolRegistry;
        private readonly DriverIntelligenceService? _driverService;
        private readonly FirmwareCompatibilityEngine _firmwareEngine;

        public PreflightEngine(
            ToolRegistry toolRegistry, 
            DriverIntelligenceService? driverService = null,
            FirmwareCompatibilityEngine? firmwareEngine = null)
        {
            _toolRegistry = toolRegistry;
            _driverService = driverService;
            _firmwareEngine = firmwareEngine ?? new FirmwareCompatibilityEngine();
        }

        public async Task<PreflightEvaluation> EvaluateAsync(
            Device? device,
            OperationType operation,
            DeviceCapabilityProfile? capabilityProfile = null,
            string? targetPartition = null,
            string? firmwareFilePath = null,
            string? requiredToolName = null,
            long requiredDiskSpaceBytes = 1073741824) // 1 GB default
        {
            var eval = new PreflightEvaluation();

            // 1. Device validation
            var isColdBootOperation = string.Equals(requiredToolName, "mtkclient", StringComparison.OrdinalIgnoreCase) ||
                                     string.Equals(requiredToolName, "qdl", StringComparison.OrdinalIgnoreCase) ||
                                     string.Equals(requiredToolName, "qcom-edl", StringComparison.OrdinalIgnoreCase) ||
                                     string.Equals(requiredToolName, "spd", StringComparison.OrdinalIgnoreCase) ||
                                     string.Equals(requiredToolName, "huawei-kirin", StringComparison.OrdinalIgnoreCase) ||
                                     device?.Mode == DeviceMode.BROM ||
                                     device?.Mode == DeviceMode.EDL ||
                                     (device != null && (device.DeviceId == "MTK_BROM" || device.DeviceId == "QCOM_EDL" || device.DeviceId == "HUAWEI_USB_COM" || device.DeviceId == "SPD_PORT"));

            if (!isColdBootOperation && (device == null || string.IsNullOrWhiteSpace(device.DeviceId)))
            {
                eval.Level = PreflightLevel.Block;
                eval.BlockingReasons.Add("Perangkat tidak terdeteksi. Hubungkan ponsel via USB.");
                eval.RecommendedFix = "Periksa kabel data USB dan pastikan mode koneksi (EDL/BROM/Fastboot/ADB) sesuai.";
                return eval;
            }

            if (isColdBootOperation && (device == null || string.IsNullOrWhiteSpace(device.DeviceId) || device.DeviceId == "MTK_BROM" || device.DeviceId == "QCOM_EDL" || device.DeviceId == "HUAWEI_USB_COM" || device.DeviceId == "SPD_PORT"))
            {
                eval.Messages.Add("[Listener Standby] Mode Boot-Key (BROM / EDL / Preloader) aktif. Sistem standby menunggu koneksi USB saat senam jari...");
            }
            else if (device != null)
            {
                eval.Messages.Add($"[Device OK] {device.Brand} {device.Model} ({device.Mode})");
            }

            // 2. Capability profile check
            if (capabilityProfile != null && !capabilityProfile.IsOperationAllowed(operation))
            {
                eval.Level = PreflightLevel.Block;
                eval.BlockingReasons.Add($"Operasi [{operation}] dibatasi atau tidak didukung pada SoC/Model [{capabilityProfile.Brand} {capabilityProfile.Model}].");
                eval.RecommendedFix = "Pilih operasi yang sesuai dengan arsitektur chipset perangkat.";
                return eval;
            }

            // 3. Free disk space verification on working drive
            try
            {
                var baseDrive = Path.GetPathRoot(AppDomain.CurrentDomain.BaseDirectory) ?? "C:\\";
                var driveInfo = new DriveInfo(baseDrive);
                if (driveInfo.IsReady && driveInfo.AvailableFreeSpace < requiredDiskSpaceBytes)
                {
                    eval.Level = PreflightLevel.Block;
                    eval.BlockingReasons.Add($"Sisa ruang penyimpanan pada Drive {baseDrive} kritis ({driveInfo.AvailableFreeSpace / (1024 * 1024):N0} MB). Diperlukan minimal {requiredDiskSpaceBytes / (1024 * 1024):N0} MB.");
                    eval.RecommendedFix = "Kosongkan ruang disk komputer sebelum melanjutkan operasi.";
                    return eval;
                }
                eval.Messages.Add($"[Storage OK] Ruang disk tersedia: {driveInfo.AvailableFreeSpace / (1024 * 1024 * 1024):N1} GB");
            }
            catch { }

            // 4. Tool presence & integrity verification
            if (!string.IsNullOrEmpty(requiredToolName))
            {
                var tool = _toolRegistry.GetTool(requiredToolName);
                if (tool == null || tool.Status != ToolStatus.Ready)
                {
                    eval.Level = PreflightLevel.Block;
                    eval.BlockingReasons.Add($"Tool binary yang dibutuhkan [{requiredToolName}] tidak ditemukan atau rusak.");
                    eval.RecommendedFix = $"Pastikan file tool terpasang di folder tools/ ({tool?.RelativePath ?? requiredToolName}).";
                    return eval;
                }
                eval.Messages.Add($"[Tool OK] {requiredToolName} siap ({tool.ResolvedFullPath})");
            }

            // 5. Partition Safety validation
            if (!string.IsNullOrEmpty(targetPartition))
            {
                if (PartitionManagerEngine.IsSensitiveSecurityPartition(targetPartition))
                {
                    eval.Warnings.Add($"Target partisi [{targetPartition}] adalah partisi vital keamanan/sinyal/bootloader. Sistem akan memicu auto-backup darurat sebelum eksekusi.");
                    if (eval.Level == PreflightLevel.Pass) eval.Level = PreflightLevel.Warning;
                }
                eval.Messages.Add($"[Partition Target] {targetPartition}");
            }

            // 6. Firmware file verification (if supplied)
            if (!string.IsNullOrEmpty(firmwareFilePath))
            {
                var fwEval = _firmwareEngine.ValidateFirmwarePackagePath(firmwareFilePath, capabilityProfile?.SocPlatform ?? device?.Chipset ?? "Auto");
                if (fwEval.IsBlocked)
                {
                    eval.Level = PreflightLevel.Block;
                    foreach (var b in fwEval.BlockingReasons) eval.BlockingReasons.Add(b);
                    eval.RecommendedFix = fwEval.RecommendedFix;
                    return eval;
                }
                foreach (var w in fwEval.Warnings)
                {
                    eval.Warnings.Add(w);
                    if (eval.Level == PreflightLevel.Pass) eval.Level = PreflightLevel.Warning;
                }
                foreach (var m in fwEval.Messages) eval.Messages.Add(m);
            }

            // 7. Driver Health & Conflict Audit (if service available)
            if (_driverService != null)
            {
                try
                {
                    var driverStatuses = _driverService.ScanDrivers();
                    var conflicted = driverStatuses.FirstOrDefault(d => d.HasConflict);
                    if (conflicted != null)
                    {
                        eval.Warnings.Add($"Terdeteksi konflik driver pada {conflicted.DriverName}: {conflicted.ConflictReason}");
                        if (eval.Level == PreflightLevel.Pass) eval.Level = PreflightLevel.Warning;
                    }
                }
                catch { }
            }

            // 8. Storage Degradation & Weak eMMC Safe Guard
            if (operation == OperationType.BackupPartition || operation == OperationType.RepairEfs || operation == OperationType.RepairImei)
            {
                eval.Messages.Add("[Safe Read Timeout] Proteksi timeout 45 detik aktif untuk mencegah chip eMMC lemah mengalami sudden death / read-lock.");
            }

            // 9. Anti-Cross-Flashing Storage Architecture Guard
            if (operation == OperationType.FlashFirmware && capabilityProfile != null && !string.IsNullOrEmpty(firmwareFilePath))
            {
                var lowerFw = firmwareFilePath.ToLower();
                if (string.Equals(capabilityProfile.StorageType, "UFS", StringComparison.OrdinalIgnoreCase) && (lowerFw.Contains("emmc") || lowerFw.Contains("rawprogram_unsparse_emmc")))
                {
                    eval.Warnings.Add("[Anti-Cross-Flashing Alert] Perangkat menggunakan memori UFS, namun paket firmware mengindikasikan struktur eMMC. Pastikan file rawprogram/scatter sesuai dengan varian memori.");
                    if (eval.Level == PreflightLevel.Pass) eval.Level = PreflightLevel.Warning;
                }
            }

            return await Task.FromResult(eval);
        }
    }
}
