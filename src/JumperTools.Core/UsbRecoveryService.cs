using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using JumperTools.Models;

namespace JumperTools.Core
{
    /// <summary>
    /// Enterprise hardware recovery and USB session resilience service
    /// Prevents blind retries on failed flash writes and provides deterministic recovery guidance
    /// </summary>
    public class UsbRecoveryService
    {
        /// <summary>
        /// Analyzes error message and process exit context to determine precise FailureClassification
        /// </summary>
        public static FailureClassification ClassifyFailure(string? errorMessage, int exitCode, bool wasTimedOut, bool wasCancelled)
        {
            if (wasCancelled) return FailureClassification.Cancelled;
            if (wasTimedOut) return FailureClassification.Timeout;

            if (string.IsNullOrEmpty(errorMessage))
            {
                return exitCode == 0 ? FailureClassification.None : FailureClassification.UnknownFailure;
            }

            var lower = errorMessage.ToLowerInvariant();

            // 1. Disconnected patterns
            if (lower.Contains("device not found") || 
                lower.Contains("no device") || 
                lower.Contains("device disconnected") || 
                lower.Contains("device gone") || 
                lower.Contains("port closed") ||
                lower.Contains("serial port closed") ||
                lower.Contains("io error") ||
                lower.Contains("error 31") ||
                lower.Contains("cannot open port") ||
                lower.Contains("usb device not recognized") ||
                lower.Contains("failed to open device"))
            {
                return FailureClassification.Disconnected;
            }

            // 2. Mode change patterns
            if (lower.Contains("handshake failed") ||
                lower.Contains("preloader") ||
                lower.Contains("mode changed") ||
                lower.Contains("rebooted") ||
                lower.Contains("sahara hello error") ||
                lower.Contains("device in wrong state"))
            {
                return FailureClassification.DeviceChanged;
            }

            // 3. Timeout patterns
            if (lower.Contains("timed out") || lower.Contains("timeout") || lower.Contains("wait response failed"))
            {
                return FailureClassification.Timeout;
            }

            // 4. Permission / Driver error
            if (lower.Contains("access denied") || lower.Contains("permission") || lower.Contains("unauthorized") || lower.Contains("libusb_error_access"))
            {
                return FailureClassification.PermissionDenied;
            }

            // 5. Incompatible firmware / verification
            if (lower.Contains("mismatch") || lower.Contains("incompatible") || lower.Contains("signature verification failed") || lower.Contains("anti-rollback"))
            {
                return FailureClassification.IncompatibleFirmware;
            }

            if (lower.Contains("verification failed") || lower.Contains("hash mismatch") || lower.Contains("checksum error"))
            {
                return FailureClassification.VerificationFailed;
            }

            return FailureClassification.UnknownFailure;
        }

        /// <summary>
        /// Determines if an operation can safely be retried automatically without physical intervention
        /// Destructive write/flash operations that failed midway MUST NOT be blindly retried!
        /// </summary>
        public static bool CanSafelyRetry(OperationType operation, OperationState failedState, FailureClassification failure)
        {
            // If it failed before writing began (Preflight, DeviceDetected, Backup, Preparing), it is safe to retry
            if (failedState == OperationState.Preflight || 
                failedState == OperationState.DeviceDetected || 
                failedState == OperationState.Backup || 
                failedState == OperationState.Preparing)
            {
                return failure != FailureClassification.PermissionDenied && failure != FailureClassification.IncompatibleFirmware;
            }

            // If it failed DURING execution or verification of a destructive operation -> STRICTLY FORBID BLIND RETRY
            if (operation.IsDestructive() && (failedState == OperationState.Executing || failedState == OperationState.Verifying))
            {
                return false;
            }

            return false;
        }

        /// <summary>
        /// Provides deterministic hardware recovery procedure for field technicians based on device architecture and failed stage
        /// </summary>
        public static (string Summary, List<string> ActionSteps) GetSafeRecoveryGuide(
            string brand, 
            string chipset, 
            OperationType operation, 
            OperationState failedState, 
            string? partitionName = null)
        {
            var brandLower = brand.ToLowerInvariant();
            var chipLower = chipset.ToLowerInvariant();
            var steps = new List<string>();

            // Case 1: Interrupted or Failed during Pre-Execution
            if (failedState == OperationState.Preflight || failedState == OperationState.Backup || failedState == OperationState.Preparing)
            {
                steps.Add("Perangkat masih dalam keadaan aman karena memori flash belum dimodifikasi.");
                steps.Add("Periksa koneksi fisik kabel USB dan pastikan port COM / ADB stabil.");
                steps.Add("Pastikan ruang disk mencukupi dan alat pendukung telah terpasang.");
                return ("Aman untuk diulang kembali.", steps);
            }

            // Case 2: Qualcomm Emergency Download (EDL 9008)
            if (chipLower.Contains("snapdragon") || chipLower.Contains("qualcomm") || chipLower.Contains("9008") || chipLower.Contains("edl"))
            {
                steps.Add("Cabut kabel data USB dan lepas konektor baterai HP selama 5 detik (atau tahan tombol Power 15 detik).");
                steps.Add("Hubungkan kembali kabel data sambil menghubungkan Titik Test Point (TP) ke Ground (GND) untuk masuk mode EDL 9008.");
                steps.Add("Verifikasi di Device Manager bahwa port 'Qualcomm HS-USB QDLoader 9008' telah muncul kembali.");
                steps.Add($"Gunakan file Programmer / Firehose (.mbn) resmi untuk mem-flash ulang partisi [{partitionName ?? "kritis"}].");
                return ("Prosedur Pemulihan Qualcomm EDL 9008 (Test Point Mode)", steps);
            }

            // Case 3: MediaTek (MTK BROM)
            if (chipLower.Contains("mtk") || chipLower.Contains("mediatek") || chipLower.Contains("helio") || chipLower.Contains("dimensity"))
            {
                steps.Add("Cabut kabel USB dan matikan total perangkat.");
                steps.Add("Tahan tombol VOLUME ATAS + VOLUME BAWAH (Boot Key) secara bersamaan, lalu colokkan kabel USB ke PC.");
                steps.Add("Pastikan filter UsbDk aktif agar Windows tidak memutus koneksi port MediaTek USB VCOM.");
                steps.Add($"Jika partisi bootloader/NVRAM terganggu, lakukan restore dari backup manifest atau flash firmware scatter resmi.");
                return ("Prosedur Pemulihan MediaTek BootROM (BROM Safe Recovery)", steps);
            }

            // Case 4: Samsung (Odin / Download Mode)
            if (brandLower.Contains("samsung") || chipLower.Contains("exynos"))
            {
                steps.Add("Cabut kabel USB. Tahan tombol VOLUME BAWAH + POWER selama 7 detik untuk keluar dari loop.");
                steps.Add("Segera tahan VOLUME ATAS + VOLUME BAWAH dan colokkan kabel USB untuk masuk ke Layar Hijau Download Mode.");
                steps.Add("Tekan VOLUME ATAS 1x untuk melanjutkan ke mode Odin Download.");
                steps.Add("Flash ulang firmware resmi 4-File (BL, AP, CP, CSC) via Odin engine.");
                return ("Prosedur Pemulihan Samsung Download Mode", steps);
            }

            // Case 5: Unisoc / Spreadtrum
            if (chipLower.Contains("spd") || chipLower.Contains("unisoc") || chipLower.Contains("spreadtrum"))
            {
                steps.Add("Cabut kabel USB dan lepas baterai selama beberapa detik.");
                steps.Add("Tahan tombol VOLUME BAWAH (Boot Key Unisoc) dan colokkan kabel USB.");
                steps.Add("Gunakan file firmware resmi PAC untuk menulis ulang partisi yang gagal.");
                return ("Prosedur Pemulihan Unisoc / Spreadtrum BSL Download Mode", steps);
            }

            // Default Android Generic / Fastboot
            steps.Add("Cabut kabel USB dan coba nyalakan ke mode Fastboot / Bootloader (Tahan Power + Vol Bawah).");
            steps.Add("Jika perangkat terdeteksi di Fastboot, flash partisi pemulihan (boot, recovery, vbmeta).");
            steps.Add("Jika tidak ada respons sama sekali (Hard-brick), gunakan metode Test Point perangkat keras.");
            return ("Prosedur Pemulihan Standar Perangkat Android", steps);
        }
    }
}
