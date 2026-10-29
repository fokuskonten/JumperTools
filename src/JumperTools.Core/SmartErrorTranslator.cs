using System;
using System.Collections.Generic;

namespace JumperTools.Core
{
    public class ErrorDiagnosis
    {
        public string RawCode { get; set; } = string.Empty;
        public string Protocol { get; set; } = string.Empty;
        public string Meaning { get; set; } = string.Empty;
        public string ActionableSolution { get; set; } = string.Empty;
    }

    /// <summary>
    /// Smart Error Translator converting cryptic hexadecimal & protocol errors into technician-friendly actionable solutions in Bahasa Indonesia
    /// </summary>
    public static class SmartErrorTranslator
    {
        private static readonly Dictionary<string, ErrorDiagnosis> ErrorDictionary = new(StringComparer.OrdinalIgnoreCase)
        {
            // ========== MEDIATEK BROM / DA ERRORS ==========
            ["0xC0060001"] = new ErrorDiagnosis
            {
                RawCode = "0xC0060001 (STATUS_BROM_CMD_FAIL)",
                Protocol = "MediaTek BROM",
                Meaning = "Perintah BootROM gagal direspon oleh prosesor HP (Handshake Timeout / USB Drop).",
                ActionableSolution = "1. Pastikan baterai terisi minimal 30%.\n2. Cabut kabel USB, lepas pasang baterai (atau tunggu 5 detik).\n3. Tahan tombol Vol Atas + Vol Bawah lebih kuat, lalu colok kabel USB kembali."
            },
            ["0xC0020004"] = new ErrorDiagnosis
            {
                RawCode = "0xC0020004 (STATUS_ERR)",
                Protocol = "MediaTek BROM",
                Meaning = "Koneksi serial komunikasi USB terputus tiba-tiba di tengah proses transfer.",
                ActionableSolution = "1. Ganti kabel data USB dengan kabel original yang berkualitas.\n2. Pindahkan ke port USB belakang (Direct Motherboard PC).\n3. Hindari penggunaan USB Hub non-power."
            },
            ["0xC0040003"] = new ErrorDiagnosis
            {
                RawCode = "0xC0040003 (STATUS_DA_SELECTION_ERR)",
                Protocol = "MediaTek DA",
                Meaning = "File Download Agent (DA) ditolak oleh sekuriti bootloader HP karena tidak cocok dengan tipe SoC/eMMC.",
                ActionableSolution = "Pilih loader DA khusus model atau gunakan 'DA_PL_CRYPTO20.bin' / 'DA_SWSEC.bin' untuk perangkat dengan proteksi SLA/DAA."
            },
            ["0xC0070002"] = new ErrorDiagnosis
            {
                RawCode = "0xC0070002 (STATUS_SEC_AUTH_FILE_NEEDED)",
                Protocol = "MediaTek SLA",
                Meaning = "Perangkat membutuhkan file autentikasi (*.auth) atau token server resmi pabrik (SLA Secure Boot Aktif).",
                ActionableSolution = "Gunakan fitur 'BROM SLA Bypass' terlebih dahulu di tab MediaTek untuk melumpuhkan proteksi auth sebelum eksekusi partisi."
            },
            ["0xC0030005"] = new ErrorDiagnosis
            {
                RawCode = "0xC0030005 (STATUS_SCAT_FILE_INVALID)",
                Protocol = "MediaTek Scatter",
                Meaning = "Format file scatter rusak, tidak lengkap, atau tidak cocok dengan arsitektur partisi HP target.",
                ActionableSolution = "Ekstrak ulang file firmware stock resmi, dan pastikan file 'MT67xx_Android_scatter.txt' berada dalam folder yang sama dengan file .bin/.img."
            },

            // ========== QUALCOMM EDL 9008 / SAHARA PROTOCOL ERRORS ==========
            ["NAK 0x09"] = new ErrorDiagnosis
            {
                RawCode = "Sahara NAK: 0x09 (HASH_VERIFICATION_FAIL)",
                Protocol = "Qualcomm EDL",
                Meaning = "Loader Firehose (.mbn/.elf) ditolak oleh chip Qualcomm karena sertifikat Public Key Hash (PKHash) tidak cocok dengan vendor HP.",
                ActionableSolution = "Loader yang dipilih tidak sesuai OEM (contoh: loader Xiaomi dicoba ke HP Vivo). Pilih file loader Firehose khusus merek dan tipe HP pelanggan."
            },
            ["NAK 0x02"] = new ErrorDiagnosis
            {
                RawCode = "Sahara NAK: 0x02 (TARGET_BUSY_OR_INVALID_CMD)",
                Protocol = "Qualcomm Sahara",
                Meaning = "Chipset Qualcomm sedang sibuk atau menolak inisialisasi handshake awal.",
                ActionableSolution = "Lepas baterai HP sejenak, pasang kembali, hubungkan titik Test Point EDL dengan pinset secara stabil, lalu colok kabel USB."
            },
            ["FIREHOSE_XML_FAIL"] = new ErrorDiagnosis
            {
                RawCode = "Firehose FirehosePacket / XML NAK",
                Protocol = "Qualcomm Firehose",
                Meaning = "Prosesor menolak perintah penulisan sektor partisi (Kemungkinan chip eMMC/UFS Read-Only / Terkunci).",
                ActionableSolution = "Periksa kesehatan chip eMMC pelanggan. Jika eMMC sudah Write-Protect / Rusak Fisik, partisi tidak dapat ditimpa via software."
            },

            // ========== ANDROID FASTBOOT & SIDELOAD ERRORS ==========
            ["FAILED (remote: partition table doesn't exist)"] = new ErrorDiagnosis
            {
                RawCode = "Fastboot: partition table doesn't exist",
                Protocol = "Android Fastboot",
                Meaning = "Tabel partisi GPT pada memori internal kosong atau korup parah (Hardbrick / Unallocated).",
                ActionableSolution = "Perangkat harus di-flash ulang secara menyeluruh (Full Rawprogram/Scatter) via mode darurat EDL 9008 atau MTK BROM."
            },
            ["FAILED (remote: Device not unlocked)"] = new ErrorDiagnosis
            {
                RawCode = "Fastboot: Device not unlocked / Flashing is not allowed",
                Protocol = "Android Fastboot",
                Meaning = "Bootloader perangkat masih terkunci (LOCKED / UBL Belum Aktif).",
                ActionableSolution = "Lakukan Unlock Bootloader (UBL) terlebih dahulu, atau gunakan jalur Sideload / BROM untuk bypass tanpa perlu UBL."
            },
            ["FAILED (remote: check antirollback failed)"] = new ErrorDiagnosis
            {
                RawCode = "Fastboot: check antirollback failed (ARB Error)",
                Protocol = "Xiaomi Anti-Rollback",
                Meaning = "Firmware yang dipilih memiliki versi ARB (Anti-Rollback) lebih rendah dari versi yang terpasang di HP (Ditolak untuk mencegah hardbrick).",
                ActionableSolution = "Gunakan file ROM versi terbaru yang sama atau lebih tinggi dari versi MIUI / HyperOS yang saat ini ada di HP."
            }
        };

        /// <summary>
        /// Translate a raw error line from CLI/Engine to human technician diagnosis
        /// </summary>
        public static ErrorDiagnosis? TranslateError(string logLine)
        {
            if (string.IsNullOrWhiteSpace(logLine)) return null;

            foreach (var kvp in ErrorDictionary)
            {
                if (logLine.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase))
                {
                    return kvp.Value;
                }
            }

            return null;
        }
    }
}
