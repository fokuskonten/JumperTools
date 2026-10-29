using System;
using System.Collections.Generic;
using JumperTools.Models;

namespace JumperTools.Core
{
    /// <summary>
    /// Master Registry containing exhaustive technician guidance, risk levels, prerequisites, 
    /// and step-by-step physical action tutorials for every UI container, dropdown, and CTA button.
    /// </summary>
    public static class FeatureGuideRegistry
    {
        private static readonly Dictionary<string, FeatureGuideInfo> _guides = new(StringComparer.OrdinalIgnoreCase);

        static FeatureGuideRegistry()
        {
            InitializeGuides();
        }

        public static FeatureGuideInfo GetGuide(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return GetDefaultGuide();
            if (_guides.TryGetValue(key, out var guide)) return guide;

            return new FeatureGuideInfo
            {
                Key = key,
                Title = $"Panduan: {key}",
                Icon = "ℹ️",
                Category = "Operasi Hardware & Servis",
                RiskLevel = GuideRiskLevel.Safe,
                RiskDescription = "Informasi umum tata cara penggunaan fitur.",
                Description = $"Petunjuk teknis mendalam untuk fitur '{key}'. Gunakan sesuai prosedur standar operasional teknisi ponsel.",
                WhenToUse = "Digunakan saat melakukan servis atau konfigurasi perangkat yang terhubung.",
                Prerequisites = new List<string> { "Kabel data USB berkualitas baik terhubung ke PC.", "Driver chipset terkait telah terpasang dengan benar." },
                StepByStepGuide = new List<string> { "Pastikan model HP dan protokol yang dipilih sudah sesuai.", "Ikuti instruksi yang tertera pada terminal output saat proses berjalan." },
                Troubleshooting = "Jika terjadi kegagalan koneksi, periksa Device Manager Windows dan gunakan tombol [Reset Port] atau [Scan USB]."
            };
        }

        private static FeatureGuideInfo GetDefaultGuide()
        {
            return new FeatureGuideInfo
            {
                Key = "default",
                Title = "Pusat Panduan & Informasi Fitur JumperTools Pro",
                Icon = "💡",
                Category = "Bantuan Antarmuka",
                RiskLevel = GuideRiskLevel.Safe,
                RiskDescription = "Sistem panduan terpadu untuk teknisi hardware dan firmware profesional.",
                Description = "Setiap tombol dan kontrol pada toolkit ini memiliki fungsi presisi tingkat hardware. Klik ikon informasi (ℹ️) pada tombol manapun untuk melihat panduan langkah fisik, risiko, dan solusi jika terjadi kendala.",
                WhenToUse = "Kapan saja Anda membutuhkan panduan kombinasi tombol, syarat pengerjaan, atau diagnosis kegagalan servis.",
                Prerequisites = new List<string> { "Perangkat ponsel terhubung ke PC.", "Driver terpasang lengkap di tab [Driver USB]." },
                StepByStepGuide = new List<string> { "1. Pilih merek HP di tombol filter atas.", "2. Pilih Model HP atau biarkan sistem mendeteksi otomatis.", "3. Pilih tab protokol yang sesuai (MediaTek, Qualcomm, Odin, dll).", "4. Klik tombol aksi yang diinginkan." },
                Troubleshooting = "Gunakan tombol merah [⏹️ Stop / Batal] di pojok kanan terminal jika Anda ingin membatalkan listener port."
            };
        }

        private static void InitializeGuides()
        {
            // ==================== UI CONTAINERS & HEADER CONTROLS ====================
            Register(new FeatureGuideInfo
            {
                Key = "Container_UsbDevices",
                Title = "Panel Perangkat USB & Detektor Port Realtime",
                Icon = "🔌",
                Category = "Hardware & Koneksi",
                RiskLevel = GuideRiskLevel.Safe,
                RiskDescription = "Aman. Hanya memindai port COM, ADB, Fastboot, dan MTP yang terhubung.",
                Description = "Panel ini memantau koneksi USB secara realtime menggunakan WMI PnP Listener Windows. Mendeteksi secara instan pergantian mode HP dari Normal (MTP), BROM (MediaTek), EDL 9008 (Qualcomm), Fastboot, hingga Odin Download Mode.",
                WhenToUse = "Digunakan setiap saat untuk memverifikasi apakah kabel data dan port ponsel telah dikenali oleh Windows sebelum menekan tombol eksekusi.",
                Prerequisites = new List<string> { "Kabel data Micro USB / Type-C yang mendukung jalur Data (bukan hanya charging).", "Port USB 2.0 / 3.0 langsung ke Motherboard PC (hindari USB Hub pasif jika sering disconnect)." },
                StepByStepGuide = new List<string>
                {
                    "1. Colokkan HP ke PC.",
                    "2. Periksa apakah nama perangkat atau nomor Port COM (misal: COM5) muncul di daftar.",
                    "3. Jika port tidak muncul, klik tombol [Scan USB] atau [Reset Port]."
                },
                Troubleshooting = "Jika HP hanya terisi daya (charging) tapi tidak terdeteksi, bersihkan konektor charger HP, gunakan kabel original, atau cek status port di Device Manager."
            });

            Register(new FeatureGuideInfo
            {
                Key = "Container_BrandFilter",
                Title = "Filter Merek HP Cerdas (Brand Selector)",
                Icon = "🏷️",
                Category = "Navigasi & Profil",
                RiskLevel = GuideRiskLevel.Safe,
                RiskDescription = "Aman. Menyaring daftar model HP di dropdown.",
                Description = "Tombol-tombol merek (Semua, Xiaomi, Oppo, Vivo, Samsung, Realme, Infinix) berfungsi mempercepat pencarian seri HP. Memilih salah satu merek akan menyaring dropdown 'Model HP:' hanya untuk seri merek tersebut dan secara cerdas mengarahkan protokol ke arsitektur default merek.",
                WhenToUse = "Digunakan saat memilih model HP secara manual sebelum pengerjaan bypass atau flashing.",
                StepByStepGuide = new List<string>
                {
                    "1. Klik tombol merek yang diinginkan (contoh: Oppo).",
                    "2. Buka dropdown 'Model HP:' untuk memilih seri ponsel.",
                    "3. Klik tombol [Semua] jika ingin melihat gabungan seluruh database model."
                }
            });

            Register(new FeatureGuideInfo
            {
                Key = "Control_ModelSelector",
                Title = "Pemilih Model HP & Database Profil Forensik",
                Icon = "📱",
                Category = "Basis Data Model",
                RiskLevel = GuideRiskLevel.Safe,
                RiskDescription = "Aman. Memuat spesifikasi chip, partisi, dan loader rekomendasi.",
                Description = "Dropdown ini terhubung ke database profil master berisi ratusan seri ponsel. Saat model dipilih, software secara otomatis memuat nama chipset, jalur arsitektur, berkas loader biner (DA / Firehose / FDL), serta parameter partisi yang aman.",
                WhenToUse = "Wajib dipilih sebelum melakukan operasi yang memerlukan loader khusus (misal: Oppo SLA, Vivo Auth, Xiaomi EDL).",
                StepByStepGuide = new List<string>
                {
                    "1. Ketik atau pilih nama seri HP (contoh: Oppo A57 2022).",
                    "2. Perhatikan label loader biner di sebelah kanan (misal: DA_PL_CRYPTO20.bin).",
                    "3. Sistem akan otomatis menyesuaikan tab protokol yang tepat."
                }
            });

            Register(new FeatureGuideInfo
            {
                Key = "Control_ProtocolSelector",
                Title = "Selektor Protokol & Arsitektur Chipset",
                Icon = "⚡",
                Category = "Arsitektur Flashing",
                RiskLevel = GuideRiskLevel.Safe,
                RiskDescription = "Aman. Menentukan modul eksekusi tingkat rendah yang aktif.",
                Description = "Dropdown ini mengendalikan protokol komunikasi hardware tingkat rendah yang digunakan:",
                WhenToUse = "Disesuaikan dengan chipset fisik yang tertanam pada ponsel (MediaTek BROM, Qualcomm EDL 9008, Samsung Odin, Xiaomi Sideload, Unisoc SPD, Huawei Kirin, Fastboot, Apple iOS).",
                StepByStepGuide = new List<string>
                {
                    "• MediaTek (BROM / Preloader) : Untuk chip Helio / Dimensity.",
                    "• Qualcomm (EDL 9008 / Firehose) : Untuk chip Snapdragon.",
                    "• Samsung (Odin / AT Mode) : Untuk HP Samsung Exynos / Snapdragon.",
                    "• Xiaomi Sideload : Untuk Recovery Sideload Mi Assistant.",
                    "• Unisoc (SPD / BSL) : Untuk chip SC9863 / T606 / T612 / T616.",
                    "• Huawei Kirin : Untuk chip HiSilicon Kirin (USB COM 1.0)."
                }
            });

            // ==================== TAB 1: MEDIATEK PROTOCOL ====================
            Register(new FeatureGuideInfo
            {
                Key = "Mtk_FormatUserdata",
                Title = "Format Userdata / Safe Factory Reset (MediaTek BROM)",
                Icon = "🗑️",
                Category = "MediaTek BROM",
                RiskLevel = GuideRiskLevel.Critical,
                RiskDescription = "🔴 KRITIS: Menghapus seluruh pola/PIN kunci layar dan data pengguna di partisi 'userdata' & 'metadata'. Sistem OS utama tetap utuh.",
                Description = "Melakukan penghapusan sektor partisi userdata dan metadata secara langsung di level BootROM menggunakan mtkclient native. Sangat efektif untuk mengatasi lupa pola, PIN, sandi, serta bootloop ringan tanpa perlu download firmware berukuran besar.",
                WhenToUse = "Ponsel terkunci pola/sandi/PIN, penyimpanan internal penuh hingga crash, atau ingin reset pabrik bersih pada HP Oppo, Vivo, Xiaomi, Realme, Infinix, Tecno berchipset MediaTek.",
                Prerequisites = new List<string>
                {
                    "HP dalam kondisi MATI TOTAL.",
                    "Baterai minimal 25% (untuk mencegah mati mendadak saat format).",
                    "Driver LibUSB / UsbDk dan MTK VCOM telah terpasang."
                },
                StepByStepGuide = new List<string>
                {
                    "1. Klik tombol [🗑️ Format Userdata] di software (terminal akan masuk mode Standby).",
                    "2. Matikan HP sepenuhnya hingga layar benar-benar padam.",
                    "3. Tahan tombol VOLUME ATAS (+) dan VOLUME BAWAH (-) secara bersamaan.",
                    "4. Sambil tetap menahan kedua tombol, colokkan kabel USB dari PC ke HP.",
                    "5. Begitu terminal mendeteksi chip (Handshake BROM OK), lepaskan kedua tombol volume.",
                    "6. Tunggu proses format sektor selesai (sekitar 3-5 detik), lalu HP akan restart bersih."
                },
                Troubleshooting = "Jika HP langsung menyala masuk ke Android normal tanpa terformat: waktu colok kabel terlalu lambat atau tombol volume dilepas sebelum USB terpasang. Klik tombol merah [⏹️ Stop / Batal], klik ulang [Format Userdata], dan ulangi langkah colok kabel."
            });

            Register(new FeatureGuideInfo
            {
                Key = "Mtk_EraseFrp",
                Title = "Erase FRP / Bypass Akun Google (MediaTek BROM)",
                Icon = "⚡",
                Category = "MediaTek BROM",
                RiskLevel = GuideRiskLevel.Warning,
                RiskDescription = "🟡 PERHATIAN: Menghapus data akun Google yang tersimpan di partisi 'frp' / 'persistent'. Data lain tidak terpengaruh.",
                Description = "Menghapus sektor proteksi Factory Reset Protection (FRP) pada partisi hardware MediaTek. Ponsel yang meminta verifikasi akun Google lama setelah di-reset akan langsung dapat masuk ke menu beranda tanpa meminta akun email sebelumnya.",
                WhenToUse = "Ponsel terkunci di wizard setup awal ('Verifikasi Akun Anda') setelah dilakukan hard reset manual via Recovery.",
                StepByStepGuide = new List<string>
                {
                    "1. Klik tombol [⚡ Erase FRP].",
                    "2. Matikan HP -> Tahan Vol Atas + Vol Bawah -> Colokkan kabel USB.",
                    "3. Lepaskan tombol saat baris deteksi BROM muncul di terminal.",
                    "4. Tunggu pesan [SUKSES FRP ERASED]. Nyalakan HP dan lewati langkah login Google."
                }
            });

            Register(new FeatureGuideInfo
            {
                Key = "Mtk_ForceBrom",
                Title = "Force BROM / Auto-Crash Preloader (MediaTek)",
                Icon = "⚡",
                Category = "MediaTek BROM",
                RiskLevel = GuideRiskLevel.Warning,
                RiskDescription = "🟡 PERHATIAN: Mengirimkan instruksi glitch hardware untuk memaksa chip masuk mode BootROM murni.",
                Description = "Pada ponsel MediaTek modern (Android 11, 12, 13, 14), produsen sering kali mengunci port BROM sehingga HP hanya terdeteksi sebagai 'Preloader VCOM' yang menolak perintah flash pihak ketiga. Fitur ini melakukan glitch watchdog / software UART break untuk memaksa port Preloader (0E8D:2000) jatuh kembali ke mode BROM murni (0E8D:0003).",
                WhenToUse = "Wajib digunakan jika saat format atau erase FRP muncul error 'Preloader security locked' atau 'Handshake failed'.",
                StepByStepGuide = new List<string>
                {
                    "1. Klik tombol [⚡ Force BROM].",
                    "2. Matikan HP -> Tahan Vol Atas + Bawah -> Colok USB.",
                    "3. Software akan melakukan exploit crash preloader dalam 1 detik.",
                    "4. Setelah sukses, Anda dapat langsung mengeksekusi tombol Format Userdata atau Erase FRP."
                }
            });

            Register(new FeatureGuideInfo
            {
                Key = "Mtk_UnlockBootloader",
                Title = "Instant Unlock Bootloader / UBL Instan (MediaTek)",
                Icon = "🔓",
                Category = "MediaTek BROM",
                RiskLevel = GuideRiskLevel.Critical,
                RiskDescription = "🔴 KRITIS: Membuka kunci bootloader (seccfg). Ponsel akan otomatis melakukan factory reset setelah UBL.",
                Description = "Memodifikasi flag seccfg pada partisi keamanan secara langsung di level BROM untuk membuka status Bootloader secara instan tanpa perlu akun auth atau menunggu masa tunggu 7 hari.",
                WhenToUse = "Diperlukan sebelum memasang Custom Recovery (TWRP), Root Magisk, atau flashing custom ROM.",
                StepByStepGuide = new List<string>
                {
                    "1. Backup data penting pelanggan terlebih dahulu.",
                    "2. Klik [🔓 Unlock Bootloader].",
                    "3. Matikan HP -> Tahan Vol Atas + Bawah -> Colok USB.",
                    "4. Setelah sukses, nyalakan HP. Layar akan menampilkan peringatan 'Orange State / Unlocked'."
                }
            });

            Register(new FeatureGuideInfo
            {
                Key = "Mtk_BackupNvram",
                Title = "Backup NVRAM & NVDATA (MediaTek Sinyal & IMEI)",
                Icon = "💾",
                Category = "MediaTek Sinyal",
                RiskLevel = GuideRiskLevel.Safe,
                RiskDescription = "🟢 AMAN: Membaca partisi kalibrasi radio tanpa mengubah data apapun di dalam ponsel.",
                Description = "Mengekstrak dan menyimpan cadangan partisi 'nvram', 'nvdata', 'nvcfg', dan 'protect1/protect2' ke folder database lokal PC. Berkas ini menyimpan nomor IMEI asli, kalibrasi baseband, dan sertifikat sinyal seluler ponsel.",
                WhenToUse = "SANGAT DISARANKAN dilakukan sebelum melakukan flashing ROM total atau partisi ulang untuk mencegah IMEI Null / No Service.",
                StepByStepGuide = new List<string>
                {
                    "1. Klik tombol [💾 Backup NVRAM].",
                    "2. Matikan HP -> Tahan Vol Atas + Bawah -> Colok USB.",
                    "3. Berkas backup akan otomatis disimpan di folder database/nvram/<Brand>/<Model>/."
                }
            });

            // ==================== TAB 1: SAMSUNG PROTOCOL ====================
            Register(new FeatureGuideInfo
            {
                Key = "Samsung_BypassFrp",
                Title = "Bypass FRP Samsung via Test Mode (*#0*#)",
                Icon = "⚡",
                Category = "Samsung Service",
                RiskLevel = GuideRiskLevel.Warning,
                RiskDescription = "🟡 PERHATIAN: Mengaktifkan USB Debugging (ADB) via panggilan darurat untuk menghapus kunci akun Google.",
                Description = "Metode 1-klik bypass FRP Samsung paling populer dan cepat di dunia. Memanfaatkan menu diagnostik pabrik (*#0*# atau *#*#8888#*#*) untuk mengirimkan instruksi aktivasi ADB Shell secara instan.",
                WhenToUse = "Samsung Galaxy (seri A, M, S, Z, Tab) yang terkunci akun Google setelah factory reset.",
                Prerequisites = new List<string>
                {
                    "HP dalam kondisi menyala di layar Selamat Datang (Welcome Screen).",
                    "Kabel USB terhubung ke PC dalam mode normal (bukan Download mode).",
                    "Driver Samsung USB terpasang."
                },
                StepByStepGuide = new List<string>
                {
                    "1. Pada layar Selamat Datang HP Samsung, klik tombol [Panggilan Darurat / Emergency Call].",
                    "2. Ketik kode: *#0*# hingga layar HP berubah menjadi menu kotak-kotak tes layar putih.",
                    "   (Jika *#0*# tidak merespons, coba: *#*#8888#*#* atau *#06#).",
                    "3. Hubungkan HP ke PC via kabel USB.",
                    "4. Klik tombol [⚡ Bypass FRP (*#0*#)] di software.",
                    "5. Perhatikan layar HP: Saat muncul kotak popup 'Izinkan USB Debugging', centang 'Selalu izinkan' lalu klik OK.",
                    "6. Software akan otomatis menghapus akun Google dan me-restart HP ke menu utama."
                },
                Troubleshooting = "Jika kode *#0*# dinonaktifkan oleh security patch terbaru (2024/2025): Gunakan metode Odin Flashing dengan firmware CSC kombinasi atau mode EDL Test Point."
            });

            Register(new FeatureGuideInfo
            {
                Key = "Samsung_FlashOdin",
                Title = "Flashing Firmware Resmi Samsung 4-File (Odin Engine)",
                Icon = "⚡",
                Category = "Samsung Flashing",
                RiskLevel = GuideRiskLevel.Critical,
                RiskDescription = "🔴 KRITIS: Menulis ulang partisi firmware sistem. Pastikan Binary / Bit (SW REV) firmware sama atau lebih tinggi dari yang ada di HP.",
                Description = "Engine flashing resmi Samsung berbasis protokol Odin Loke. Mendukung 4-File lengkap (BL = Bootloader, AP = System/PDA, CP = Modem/Phone, CSC = Regional Configuration) untuk pemulihan total bootloop, unbrick, downgrade, atau upgrade OS resmi.",
                WhenToUse = "HP Samsung mengalami mati suri, bootloop mentok logo, terkena virus sistem, atau ingin instal ulang OS bersih.",
                StepByStepGuide = new List<string>
                {
                    "1. Masukkan HP Samsung ke Download Mode: Matikan HP -> Tahan Vol Atas + Vol Bawah -> Colokkan kabel USB -> Tekan Vol Atas sekali untuk Continue.",
                    "2. Di software, klik tombol [📂 Pilih] untuk memasukkan file BL, AP, CP, dan CSC (.tar.md5).",
                    "   (Pilih file CSC standar untuk format bersih, atau HOME_CSC jika ingin mempertahankan data pengguna).",
                    "3. Klik tombol [⚡ Flash Odin] dan tunggu proses flashing hingga selesai (bar progress 100%)."
                }
            });

            // ==================== TAB 1: QUALCOMM PROTOCOL ====================
            Register(new FeatureGuideInfo
            {
                Key = "Qualcomm_BypassFrp",
                Title = "Bypass FRP Qualcomm via Emergency Download (EDL 9008)",
                Icon = "⚡",
                Category = "Qualcomm Firehose",
                RiskLevel = GuideRiskLevel.Warning,
                RiskDescription = "🟡 PERHATIAN: Menghapus partisi 'config' / 'frp' langsung pada chip Snapdragon.",
                Description = "Berkomunikasi langsung dengan prosesor Snapdragon via protokol Sahara & Firehose programmer (.mbn / .elf). Menghapus partisi pengunci akun Google tanpa perlu menyalakan sistem Android.",
                WhenToUse = "Ponsel Qualcomm (Xiaomi, Oppo, Vivo, Realme, OnePlus, Samsung Snapdragon) yang terkunci FRP.",
                StepByStepGuide = new List<string>
                {
                    "1. Masuk ke mode EDL 9008: Matikan HP -> Buka tutup belakang & hubungkan titik Test Point ke Ground -> Colokkan kabel USB.",
                    "   (Atau gunakan perintah 'adb reboot edl' / 'fastboot oem edl' jika bootloader mengizinkan).",
                    "2. Pastikan port 'Qualcomm HS-USB QDLoader 9008' muncul di daftar perangkat USB.",
                    "3. Klik tombol [⚡ Bypass FRP (EDL)]."
                }
            });

            Register(new FeatureGuideInfo
            {
                Key = "Qualcomm_FormatUserdata",
                Title = "Format Userdata / Factory Reset (Qualcomm EDL 9008)",
                Icon = "🗑️",
                Category = "Qualcomm Firehose",
                RiskLevel = GuideRiskLevel.Critical,
                RiskDescription = "🔴 KRITIS: Menghapus seluruh sektor partisi userdata via Firehose programmer.",
                Description = "Mengirimkan perintah XML rawprogram/erase ke chip flash penyimpanan (eMMC/UFS) pada mode EDL 9008 untuk menghapus kunci layar, pola, sandi, dan data akun.",
                WhenToUse = "Lupa pola pada ponsel Snapdragon yang recovery modenya terkunci sandi.",
                StepByStepGuide = new List<string>
                {
                    "1. Hubungkan HP ke mode EDL 9008 via Test Point.",
                    "2. Pilih file Firehose (.mbn) yang sesuai di tab Firehose jika tipe HP belum ada di database auto.",
                    "3. Klik [🗑️ Format Userdata]."
                }
            });

            // ==================== TAB 1: XIAOMI SIDELOAD ====================
            Register(new FeatureGuideInfo
            {
                Key = "Xiaomi_SideloadFormat",
                Title = "Wipe Userdata via Xiaomi Mi Assistant (Sideload Mode)",
                Icon = "🗑️",
                Category = "Xiaomi Sideload",
                RiskLevel = GuideRiskLevel.Critical,
                RiskDescription = "🔴 KRITIS: Menghapus data pengguna resmi via perintah recovery Xiaomi Mi Assistant.",
                Description = "Melakukan factory reset pada HP Xiaomi / Redmi / Poco tanpa perlu membuka casing belakang (No Test Point) dan tanpa perlu Unlock Bootloader. Menggunakan protokol Sideload Mi Recovery resmi.",
                WhenToUse = "Lupa pola pada Xiaomi/Redmi yang tidak bisa di-wipe via menu Recovery bawaan (terkunci PIN).",
                StepByStepGuide = new List<string>
                {
                    "1. Matikan HP Xiaomi.",
                    "2. Tahan tombol VOLUME ATAS (+) dan POWER secara bersamaan hingga masuk ke 'Main Menu (Mi-Recovery 5.0 / 3.0)'.",
                    "3. Gunakan tombol volume untuk memilih menu [Connect with MIAssistant], lalu tekan tombol Power untuk memilih.",
                    "4. Layar HP akan menampilkan gambar HP dengan kabel USB terhubung.",
                    "5. Hubungkan HP ke PC via kabel USB.",
                    "6. Klik tombol [🗑️ Wipe Userdata] di tab Xiaomi Sideload."
                }
            });

            // ==================== TAB 2: PARTISI GPT ====================
            Register(new FeatureGuideInfo
            {
                Key = "Tab_PartitionGpt",
                Title = "Manajer Partisi GPT (GUID Partition Table Engine)",
                Icon = "📑",
                Category = "Tingkat Rendah Memori",
                RiskLevel = GuideRiskLevel.Warning,
                RiskDescription = "🟡 PERHATIAN: Akses langsung ke struktur partisi chip penyimpanan eMMC / UFS.",
                Description = "Membaca, membackup, menulis, atau menghapus partisi individual (seperti boot, recovery, super, nvram, fsg, persist, modem, seccfg) secara langsung dari tabel GPT ponsel tanpa perlu mendownload seluruh firmware.",
                WhenToUse = "Melakukan backup IMEI/modem spesifik, memperbaiki partisi rusak (misal: persist corrupt yang menyebabkan sensor gyro mati), atau root via patch boot.img.",
                StepByStepGuide = new List<string>
                {
                    "1. Hubungkan HP dalam mode BROM (MediaTek) atau EDL 9008 (Qualcomm).",
                    "2. Klik tombol [📑 Read GPT Live] untuk memuat seluruh daftar partisi.",
                    "3. Centang partisi yang ingin di-backup, lalu klik [💾 Backup Selected].",
                    "4. Gunakan tombol [📥 Write Selected] untuk menyuntikkan file biner (.img) ke partisi target."
                }
            });

            // ==================== TAB 3: EKSTRAK FIRMWARE ====================
            Register(new FeatureGuideInfo
            {
                Key = "Tab_FirmwareExtractor",
                Title = "Ekstraktor & Dekriptor Firmware Multi-Format",
                Icon = "📦",
                Category = "Olah Firmware",
                RiskLevel = GuideRiskLevel.Safe,
                RiskDescription = "🟢 AMAN: Operasi offline pada file di PC. Tidak menyentuh hardware ponsel.",
                Description = "Toolkit terpadu untuk mengekstrak dan mendekripsi berbagai format ROM pabrikan:",
                WhenToUse = "Mengekstrak file boot.img untuk root, mengambil file modem/NVRAM, atau membongkar ROM resmi sebelum flashing.",
                StepByStepGuide = new List<string>
                {
                    "• Dekripsi .OFP Oppo/Realme : Membuka enkripsi firmware resmi Oppo Qcom & MTK.",
                    "• Ekstrak Payload.bin : Membongkar ROM Android modern (OTA Update.zip).",
                    "• Splitter .TAR.MD5 Samsung : Mengekstrak lz4 & lz4c dari file AP/BL Samsung.",
                    "• Generator Scatter / Rawprogram : Membuat file mapping partisi otomatis."
                }
            });

            // ==================== TAB 4: FIREHOSE FINDER ====================
            Register(new FeatureGuideInfo
            {
                Key = "Tab_FirehoseFinder",
                Title = "Firehose Finder & Pemindai HWID Qualcomm EDL",
                Icon = "🔍",
                Category = "Qualcomm Forensic",
                RiskLevel = GuideRiskLevel.Safe,
                RiskDescription = "🟢 AMAN: Memindai identitas chip hardware tanpa menulis partisi.",
                Description = "Membaca Chip HWID, OEM ID, Model ID, dan PK Hash autentikasi langsung dari prosesor Snapdragon via protokol Sahara. Secara otomatis mencocokkan hash tersebut dengan ribuan koleksi berkas programmer firehose (.mbn/.elf) di database lokal.",
                WhenToUse = "Saat menghadapi HP Qualcomm tipe langka atau tipe baru yang membutuhkan programmer no-auth yang cocok.",
                StepByStepGuide = new List<string>
                {
                    "1. Hubungkan HP ke mode EDL 9008 via Test Point.",
                    "2. Klik tombol [🔍 Scan HWID & Match Loader].",
                    "3. Software akan menampilkan persentase kecocokan loader biner.",
                    "4. Klik tombol [Gunakan Loader Terpilih] untuk langsung mengunci loader di tab Flash."
                }
            });

            // ==================== TAB 5: TEST POINT HUB ====================
            Register(new FeatureGuideInfo
            {
                Key = "Tab_TestPointHub",
                Title = "Hub Diagram Skematik & Titik Jumper Test Point (ISP / EDL)",
                Icon = "📍",
                Category = "Hardware Skematik",
                RiskLevel = GuideRiskLevel.Safe,
                RiskDescription = "🟢 AMAN: Panduan visual titik jumper fisik pada motherboard.",
                Description = "Koleksi diagram resolusi tinggi lokasi titik Test Point (EDL 9008 / BROM CLK / ISP Direct eMMC). Dilengkapi petunjuk komponen mana yang harus di-grounding (dihubungkan ke plat seng/ground motherboard) menggunakan pinset.",
                WhenToUse = "Saat HP mati total (hardbrick) atau ponsel tipe baru yang mematikan kombinasi tombol volume sehingga wajib jumper hardware.",
                StepByStepGuide = new List<string>
                {
                    "1. Pilih merek dan model HP di kolom pencarian sebelah kiri.",
                    "2. Perhatikan lingkaran merah / titik kuning pada gambar motherboard.",
                    "3. Lepaskan soket baterai HP.",
                    "4. Gunakan pinset runcing untuk menyentuhkan titik Test Point ke plat Ground.",
                    "5. Sambil tetap menahan pinset, colokkan kabel USB ke PC hingga port EDL 9008 terdeteksi di Windows.",
                    "6. Lepaskan pinset, pasang kembali soket baterai, dan lanjutkan flashing di software."
                }
            });

            // ==================== TAB 6: NOTA SERVIS (CRM) ====================
            Register(new FeatureGuideInfo
            {
                Key = "Tab_ServiceCrm",
                Title = "Sistem CRM & Generator Nota Servis Kasir (Thermal 58/80mm)",
                Icon = "🧾",
                Category = "Manajemen Konter",
                RiskLevel = GuideRiskLevel.Safe,
                RiskDescription = "🟢 AMAN: Manajemen pelanggan, pencatatan biaya servis, dan cetak struk.",
                Description = "Sistem kasir profesional khusus konter perbaikan HP. Mencatat nama pelanggan, nomor WhatsApp, IMEI, kerusakan fisik, uang muka (DP), total biaya servis, serta masa garansi. Mendukung cetak langsung ke printer thermal struk (58mm / 80mm ESC/POS) dan ekspor nota PDF ber-barcode.",
                WhenToUse = "Saat menerima ponsel masuk dari pelanggan atau saat penyerahan unit servis yang telah selesai diperbaiki.",
                StepByStepGuide = new List<string>
                {
                    "1. Data IMEI & Tipe HP akan otomatis terisi jika HP tercolok ke USB.",
                    "2. Masukkan nama pemilik, keluhan kerusakan, dan biaya jasa.",
                    "3. Klik tombol [🖨️ Cetak Struk 58mm / 80mm] untuk mencetak nota kasir.",
                    "4. Klik [Export PDF] untuk mengirimkan bukti tanda terima via WhatsApp pelanggan."
                }
            });

            // ==================== TAB 7: DRIVER USB DOCTOR ====================
            Register(new FeatureGuideInfo
            {
                Key = "Tab_UsbDrivers",
                Title = "Dokter Diagnostik & Penginstal Driver USB Otomatis",
                Icon = "🔧",
                Category = "Sistem & Driver",
                RiskLevel = GuideRiskLevel.Safe,
                RiskDescription = "🟢 AMAN: Memeriksa dan menginstal driver USB resmi di Windows.",
                Description = "Memverifikasi integritas driver USB krusial pada sistem Windows Anda:",
                WhenToUse = "Wajib dibuka saat pertama kali menginstal software atau saat HP sering disconnect / muncul tanda seru kuning di Device Manager.",
                StepByStepGuide = new List<string>
                {
                    "• UsbDk (USB Development Kit) : Mengambil alih port USB level kernel tanpa konflik.",
                    "• LibUSB-Win32 Filter : Driver utama untuk protokol mtkclient BROM.",
                    "• MTK VCOM Driver : Driver resmi MediaTek Preloader (CDC).",
                    "• Qualcomm HS-USB QDLoader 9008 : Driver resmi Snapdragon EDL.",
                    "• Samsung Mobile USB Driver : Driver resmi Samsung Odin & ADB."
                }
            });

            // ==================== TAB 8: SINYAL & IMEI REPAIR ====================
            Register(new FeatureGuideInfo
            {
                Key = "Tab_SignalRepair",
                Title = "Pusat Kalibrasi Sinyal & Pemulihan IMEI (QCN & NVRAM)",
                Icon = "📶",
                Category = "Perbaikan Jaringan",
                RiskLevel = GuideRiskLevel.Warning,
                RiskDescription = "🟡 PERHATIAN: Menulis ulang sertifikat jaringan radio RF. Gunakan hanya untuk memulihkan IMEI resmi bawaan HP yang hilang/corrupt.",
                Description = "Menangani kasus No Service, Sinyal Silang (Bulat), Baseband Unknown, atau IMEI 0/Null. Mendukung aktivasi Qualcomm Diagnostic (Diag) COM Port, penulisan file kalibrasi QCN, dan pemulihan database NVRAM/NVDATA MediaTek.",
                WhenToUse = "Ponsel tidak ada sinyal setelah ganti IC CPU/eMMC, salah flash modem, atau setelah reset pabrik.",
                StepByStepGuide = new List<string>
                {
                    "1. Untuk Qualcomm: Klik [Aktivasi Diag Port via ADB] -> Pilih file .QCN yang sesuai dari database -> Klik [Tulis File QCN].",
                    "2. Untuk MediaTek: Masuk BROM mode -> Pilih file backup NVRAM -> Klik [Restore NVRAM]."
                }
            });

            // ==================== TAB 9: CUSTOM RECOVERY HUB ====================
            Register(new FeatureGuideInfo
            {
                Key = "Tab_RecoveryHub",
                Title = "Pusat Pemasangan Custom Recovery (TWRP / OrangeFox / PBRP)",
                Icon = "⚡",
                Category = "Modifikasi & Custom Recovery",
                RiskLevel = GuideRiskLevel.Warning,
                RiskDescription = "🟡 PERHATIAN: Menulis image recovery ke partisi 'recovery' atau 'boot' ponsel.",
                Description = "Memasang custom recovery populer (TWRP, OrangeFox, PitchBlack, SkyHawk) secara 1-klik via Fastboot atau BROM. Mendukung boot sementara (twrp boot) tanpa menimpa recovery bawaan pabrik.",
                WhenToUse = "Untuk instalasi Magisk Root, backup Nandroid partisi total, atau flashing Custom ROM.",
                StepByStepGuide = new List<string>
                {
                    "1. Pastikan Bootloader sudah Terbuka (Status: Unlocked).",
                    "2. Masukkan HP ke mode Fastboot.",
                    "3. Pilih file recovery (.img) dari database lokal atau klik [Pilih File .img].",
                    "4. Klik tombol [⚡ Flash Recovery Image]."
                }
            });

            // ==================== TAB 10: READ INFO FORENSIK ====================
            Register(new FeatureGuideInfo
            {
                Key = "Tab_ReadInfoForensic",
                Title = "Inspektur Forensik Hardware & Audit Keaslian Ponsel",
                Icon = "ℹ️",
                Category = "Forensik Perangkat",
                RiskLevel = GuideRiskLevel.Safe,
                RiskDescription = "🟢 AMAN: Hanya membaca register internal chip prosesor dan memori eMMC/UFS.",
                Description = "Melakukan audit mendalam terhadap identitas asli perangkat keras ponsel:",
                WhenToUse = "Memverifikasi apakah HP yang diservis merupakan barang refurbish / rekondisi / fake sebelum mulai pengerjaan.",
                StepByStepGuide = new List<string>
                {
                    "• SoC ID & Hardware Code : Membaca nama silikon prosesor langsung dari CPU.",
                    "• eMMC / UFS Life Time Estimation : Memeriksa kesehatan chip memori (0-10% normal, >90% mendekati rusak/rusak fisik).",
                    "• Factory SKU & Anti-Rollback : Membaca batas minimum versi Android yang aman untuk di-flash tanpa risiko brick."
                }
            });
        }

        private static void Register(FeatureGuideInfo guide)
        {
            _guides[guide.Key] = guide;
        }
    }
}
