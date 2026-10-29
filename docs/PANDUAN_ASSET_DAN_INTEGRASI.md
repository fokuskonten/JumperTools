# PANDUAN PENYIAPAN ASSET & INTEGRASI TOOLS (FOKUSKONTEN HP TOOLS PRO)

Dokumen ini adalah panduan inventaris dan checklist resmi untuk melengkapi seluruh file fisik, driver Windows, binary flasher khusus, database Download Agent (DA), loader Firehose Qualcomm, dan diagram Test Point agar **FokusKonten HP Tools** siap beroperasi secara maksimal di lapangan servis.

---

## 📋 DAFTAR CHECKLIST KEBUTUHAN UTAMA

```
FokusKonten-HP-Tools/
├── tools/
│   ├── drivers/                         # [PRIORITAS 1] Paket Installer Driver USB
│   ├── samsung/                         # [PRIORITAS 2] Samsung 4-File Odin
│   ├── spd/                             # [PRIORITAS 2] Unisoc PAC Flasher
│   └── xiaomi/                          # [PRIORITAS 2] Xiaomi MiFlash
├── database/
│   ├── da/                              # [PRIORITAS 3] Download Agent (DA) MediaTek
│   ├── firehose/                        # [PRIORITAS 3] Programmer MBN Qualcomm
│   └── recovery/                        # [PRIORITAS 4] File TWRP/OrangeFox Recovery
└── resources/
    └── images/
        └── testpoints/                  # [PRIORITAS 4] Foto Titik Pinout Test Point
```

---

## 1. PAKET DRIVER FISIK WINDOWS (`tools/drivers/`)

Driver ini wajib dipasang di komputer teknisi agar protokol level rendah (BROM & EDL 9008) tidak terputus di tengah jalan.

| Nama File Target | Sumber / Repositori Resmi | Fungsi Teknis | Cara Integrasi |
|---|---|---|---|
| `tools/drivers/UsbDk64.msi` | [Daynix UsbDk GitHub Releases](https://github.com/daynix/UsbDk/releases) | Driver akses langsung USB filter (mengizinkan Python berkomunikasi dengan chipset tanpa bentrok driver Windows). | Unduh installer `UsbDk_1.0.22_x64.msi`, ganti nama menjadi `UsbDk64.msi`, letakkan di folder `tools/drivers/`. |
| `tools/drivers/libusb-win32-devel-filter-1.2.6.0.exe` | [SourceForge libusb-win32](https://sourceforge.net/projects/libusb-win32/) | Filter driver LibUSB untuk handshake MediaTek BROM. | Unduh installer resmi dan letakkan di `tools/drivers/`. |
| `tools/drivers/qualcomm/` | Qualcomm QDLoader Driver 2.1.2.x (x64/x86) | Driver port serial COM 9008 (`QUSB_BULK` $\rightarrow$ `Qualcomm HS-USB QDLoader 9008`). | Masukkan file `.inf`, `.cat`, `.sys` ke dalam folder `tools/drivers/qualcomm/`. |
| `tools/drivers/mtk/` | MediaTek USB VCOM Driver Pack v1.0.84 | Driver port `MediaTek USB Port (PID 0003)` & Preloader COM Port. | Masukkan file `.inf` MTK ke `tools/drivers/mtk/`. |
| `tools/drivers/samsung/` | SAMSUNG Mobile USB Driver v1.7.59 | Driver MTP, Modem AT Port, dan Odin Download Mode. | Letakkan installer `SAMSUNG_USB_Driver_for_Mobile_Phones.exe` di `tools/drivers/samsung/`. |

---

## 2. BINARY FLASHER KHUSUS BRAND (`tools/`)

Untuk mendukung proses flashing firmware lengkap (Full Stock ROM), letakkan executable berikut:

### A. Samsung Odin Flasher
- **Target Path**: `tools/samsung/odin.exe` (atau `tools/odin/Odin3.exe`)
- **Versi yang Direkomendasikan**: Odin3 v3.14.4 (Support Android 10, 11, 12, 13, 14, 15)
- **Fungsi**: Flashing 4-file (`BL`, `AP`, `CP`, `CSC`) pada mode Samsung Download.

### B. Unisoc / Spreadtrum Flasher
- **Target Path**: `tools/spd/ResearchDownload.exe` atau `tools/spd/UpgradeDownload.exe`
- **Versi yang Direkomendasikan**: ResearchDownload R24.0.0003 / R26.21.2801
- **Fungsi**: Flashing file single `.pac` pada chipset Unisoc Tiger T606, T610, T612, T616, SC9863A.

### C. Xiaomi MiFlash
- **Target Path**: `tools/xiaomi/MiFlash.exe` (bersama folder `Source/ThirdParty/Google/Android`)
- **Versi yang Direkomendasikan**: MiFlash 20200314 / 20210228 Portable
- **Fungsi**: Flashing Fastboot ROM Xiaomi berbasis `flash_all.bat`.

---

## 3. DATABASE DOWNLOAD AGENT (DA) MEDIATEK (`database/da/`)

File Download Agent bertindak sebagai instruktur RAM untuk chipset Helio dan Dimensity. Letakkan file-file `.bin` berikut di folder `database/da/`:

| Nama File DA | Chipset / Model Target | Keterangan |
|---|---|---|
| `MTK_AllInOne_DA.bin` | Universal MT6580, MT6735, MT6737, MT6739 | DA standar multi-chipset legacy. |
| `MTK_AllInOne_DA_6765.bin` | Helio P35 / G25 / G35 (MT6765) | Digunakan pada Redmi 9A/9C, Oppo A5s/A15, Vivo Y12. |
| `MTK_AllInOne_DA_6768.bin` | Helio G70 / G80 / G85 (MT6768/MT6769) | Digunakan pada Redmi 9, Note 9, Realme C25. |
| `MTK_AllInOne_DA_Oppo_6765.bin` | Oppo A5s / A12 / A15 / A54 | DA dengan signature khusus Oppo SLA. |
| `MTK_AllInOne_DA_Vivo_6765.bin` | Vivo Y12 / Y15 / Y17 / Y1s | DA dengan signature bypass demo Vivo. |
| `MTK_AllInOne_DA_Infinix.bin` | Infinix Hot 9 / Hot 10 / Smart 5 | DA Transsion MediaTek series. |

---

## 4. DATABASE FIREHOSE PROGRAMMER QUALCOMM (`database/firehose/`)

Project ini sudah memiliki repositori ribuan Firehose di subfolder:
`database/firehose/bkerler_loaders/`

Untuk model-model favorit teknisi di Indonesia, pastikan file MBN/ELF berikut ter-copy ke `database/firehose/qualcomm/`:

| Nama File Firehose | SoC / Chipset | Model Populer |
|---|---|---|
| `prog_emmc_firehose_sm6125_ddr.mbn` | Snapdragon 665 (SM6125) | Xiaomi Redmi Note 8, Realme 5 |
| `prog_emmc_firehose_sdm660_ddr.mbn` | Snapdragon 660 (SDM660) | Xiaomi Redmi Note 7, Mi A2 |
| `prog_ufs_firehose_sm8150_ddr.elf` | Snapdragon 855 / 860 (SM8150) | Poco X3 Pro, Xiaomi Mi 9 |
| `prog_ufs_firehose_sm7150_ddr.elf` | Snapdragon 730 / 732G (SM7150) | Redmi Note 10 Pro, Poco X3 NFC |
| `prog_emmc_firehose_sm4250_ddr.mbn` | Snapdragon 460 / 662 (SM4250/SM6115) | Oppo A53, Vivo Y20, Redmi 9T |
| `prog_emmc_firehose_sdm439_ddr.mbn` | Snapdragon 439 (SDM439) | Vivo Y91 / Y93 / Y95, Redmi 8A |
| `prog_emmc_firehose_sdm450_samsung.mbn` | Snapdragon 450 (SDM450) | Samsung Galaxy A02s, A11, M11 |

---

## 5. DIAGRAM FOTO TEST POINT (`resources/images/testpoints/`)

Agar teknisi bisa melihat gambar fisik titik jumper Test Point di layar UI, siapkan file foto motherboard beresolusi jelas (JPG/PNG rasio 16:9 atau 4:3) dengan format penamaan berikut:

```
resources/images/testpoints/
├── xiaomi_redmi_note_8_tp.jpg
├── xiaomi_redmi_note_7_tp.jpg
├── xiaomi_poco_x3_pro_tp.jpg
├── xiaomi_redmi_9a_tp.jpg
├── oppo_a5s_tp.jpg
├── oppo_a53_tp.jpg
├── vivo_y12_tp.jpg
├── vivo_y20_tp.jpg
├── samsung_a02s_tp.jpg
└── realme_c11_tp.jpg
```

---

## 6. CARA VERIFIKASI SETELAH SEMUA FILE DIMASUKKAN

Setelah Anda meletakkan file-file di atas ke folder masing-masing, jalankan script audit otomatis:

1. **Jalankan Verifikasi Release Gate**:
   ```powershell
   powershell -ExecutionPolicy Bypass -File .\release_gate.ps1
   ```
2. **Jalankan Audit Unit Test Suite**:
   ```bash
   dotnet test tests\FokusKonten-HP-Tools.Tests\FokusKonten-HP-Tools.Tests.csproj --nologo
   ```
3. **Hasil yang Diharapkan**:
   - Seluruh dependensi berstatus `[PASS]`.
   - 71/71 Unit Test lolos.
   - Status: **`[RELEASE CANDIDATE - READY FOR PHYSICAL BENCH TESTING]`**.
