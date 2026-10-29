# JumperTools

**Universal Smartphone Hardware Protocol & Firmware Suite**  
*A standalone, high-performance desktop suite for smartphone repair technicians built with C# .NET 8 and WPF (Windows x64).*

---

## Overview

**JumperTools** is a specialized native desktop utility engineered for professional smartphone hardware and firmware servicing. Designed with an MVVM architecture on top of modern **.NET 8 WPF**, JumperTools unifies low-level communication protocols across four major chipset architectures:
- **Qualcomm Snapdragon** (EDL 9008 via Sahara & Firehose XML)
- **MediaTek** (BROM & Preloader DA via mtkclient / scatter)
- **Samsung Exynos & Qualcomm** (Odin Download Mode & PIT Parser)
- **UNISOC / Spreadtrum** (FDL1/FDL2 Bootloader & PAC Flasher)

The application operates **100% offline**, eliminating reliance on expensive hardware dongles, remote cloud servers, or recurring credit subscriptions.

---

## 11 Core Service Modules

| No | Modul | Deskripsi Fungsional |
|---|---|---|
| **01** | **Unlock & Servis Khusus** | Bypass FRP Google Account, hapus kunci pola/PIN/password, reset Mi Account Xiaomi, serta factory reset aman tanpa risiko bootloop. |
| **02** | **Flasher Firmware Multi-Chipset** | Flasher firmware multi-protokol resmi: Qualcomm RawProgram/Patch XML, MediaTek Scatter, Samsung 4-File Odin (BL/AP/CP/CSC/PIT), dan UNISOC PAC. |
| **03** | **Manajer Partisi GPT (eMMC & UFS)** | Akses langsung ke tabel partisi chip memori fisik. Mendukung Dump .img, Write/Flash sektor tunggal, serta Erase dengan proteksi otomatis sektor radio/IMEI (*SafetyNet Guard*). |
| **04** | **Ekstraktor Firmware & Unpacker ROM** | Membongkar paket firmware pabrikan terenkripsi: OFP Oppo/Realme, Payload.bin Google/OnePlus, Sparse Super.img, serta ekstraksi rawprogram XML Qualcomm. |
| **05** | **Firehose EDL Finder & Sahara Handshake** | Pangkalan data offline 1.416 programmer Firehose (.elf/.mbn) resmi dengan filter HWID otomatis, deteksi chipset, dan protokol jabat tangan Sahara langsung ke bus USB. |
| **06** | **Katalog Test Point & Jalur ISP Pinout** | Diagram visual titik jumper darurat (Test Point BROM/EDL 9008) untuk 3.078+ model ponsel serta 1.565+ jalur solder langsung ISP eMMC/UFS (CLK, CMD, DAT0, VCC). |
| **07** | **Manajemen Nota Servis & Kasir CRM** | Sistem CRM meja servis terintegrasi dengan database SQLite lokal. Pencatatan nota servis masuk/keluar, status perbaikan, garansi, estimasi biaya, dan cetak struk thermal 58mm/80mm. |
| **08** | **Asisten Driver USB & Filter Windows** | Monitoring real-time status driver port USB chipset smartphone via Windows PnP. Pemasangan filter UsbDk, LibUSB WinUSB, dan paket installer resmi sekali klik. |
| **09** | **Perbaikan Sinyal, Baseband & Nomor IMEI** | Pembuka port diagnostik Qualcomm Diag COM, pencadangan dan pemulihan partisi QCN (.qcn), serta backup/restore partisi vital NVRAM dan NVDATA MediaTek anti-hilang sinyal. |
| **10** | **Pemasang Custom Recovery Fastboot** | Instalasi recovery kustom TWRP, OrangeFox, dan PBRP secara permanen via Fastboot, ditambah fitur Boot RAM sementara untuk eksplorasi tanpa mengubah partisi recovery bawaan. |
| **11** | **Inspeksi Forensik Hardware & Read Info** | Membaca rincian identitas mendalam ponsel: arsitektur SoC, model komersial, versi pita basis, patch keamanan Android, estimasi kesehatan baterai, dan sisa umur memori eMMC/UFS Health. |

---

## Arsitektur Sistem

```
JumperTools.sln
├── src/
│   ├── JumperTools.Core/         # Hardware communication engines & protocol handlers
│   │   ├── QualcommEdlEngine.cs     # Sahara Protocol & Firehose XML communication
│   │   ├── MediaTekBromEngine.cs    # BROM / Preloader DA bypass & scatter execution
│   │   ├── SamsungAtEngine.cs       # Samsung Modem AT & Download Mode protocols
│   │   ├── UnisocSpdEngine.cs       # UNISOC FDL bootloader & PAC extraction
│   │   ├── FastbootEngine.cs        # Android Fastboot protocol & partition I/O
│   │   ├── PartitionManagerEngine.cs# Physical GPT parser & sector read/write
│   │   ├── SafetyNetEngine.cs       # Critical radio/baseband protection guard
│   │   ├── SignalRepairEngine.cs    # Diag port switcher & QCN/NVRAM calibration
│   │   ├── DriverAssistantEngine.cs # Windows PnP hardware device monitor
│   │   └── JobOrderEngine.cs        # CRM logic & thermal receipt layout engine
│   │
│   ├── JumperTools.UI/           # Presentation layer (WPF .NET 8 x64)
│   │   ├── Views/Tabs/              # 11 Modular XAML Tab Views
│   │   ├── ViewModels/Tabs/         # Clean MVVM ViewModels with CommunityToolkit
│   │   └── Styles/                  # Modern enterprise dark/light theme resources
│   │
│   ├── JumperTools.Models/       # Domain entities, DTOs & protocol contracts
│   ├── JumperTools.Data/         # SQLite database context & repository layer
│   ├── JumperTools.Services/     # Background hardware services & state coordinators
│   ├── JumperTools.Utilities/    # Low-level helpers, path resolvers & logger
│   └── JumperTools.DriverInstaller/# Driver setup utility
│
├── tests/
│   └── JumperTools.Tests/        # 13 Automated unit test suites (xUnit)
│
└── ss/                           # 11 High-Definition authentic interface screenshots
```

---

## Screenshot Antarmuka Desktop

| 01. Unlock & Servis | 02. Flasher Firmware |
|:---:|:---:|
| ![Unlock](ss/Screenshot%202026-10-30%20044113.png) | ![Flasher](ss/Screenshot%202026-10-30%20044131.png) |

| 03. Partisi GPT | 05. Firehose EDL |
|:---:|:---:|
| ![Partisi](ss/Screenshot%202026-10-30%20044136.png) | ![Firehose](ss/Screenshot%202026-10-30%20044143.png) |

| 06. Test Point | 07. Nota Kasir CRM |
|:---:|:---:|
| ![TestPoint](ss/Screenshot%202026-10-30%20044213.png) | ![Nota](ss/Screenshot%202026-10-30%20044216.png) |

---

## Persyaratan Sistem

- **Sistem Operasi**: Windows 10 (64-bit) / Windows 11 (64-bit)
- **Runtime**: [.NET 8.0 Desktop Runtime (x64)](https://dotnet.microsoft.com/download/dotnet/8.0)
- **Konektivitas**: Port USB 2.0 / 3.0 & kabel data berkualitas tinggi
- **Driver USB**: Driver pabrikan (Qualcomm QDLoader, MediaTek VCOM, Samsung Mobile USB, UNISOC SPD)

---

## Kompilasi dari Source Code

Pastikan [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) telah terpasang di sistem Windows Anda.

```bash
# 1. Clone repositori
git clone https://github.com/fokuskonten/JumperTools.git
cd JumperTools

# 2. Restore dependensi NuGet
dotnet restore JumperTools.sln

# 3. Jalankan pengujian unit test
dotnet test tests/JumperTools.Tests/JumperTools.Tests.csproj

# 4. Build aplikasi biner rilis mandiri (Single-File x64)
dotnet publish src/JumperTools.UI/JumperTools.UI.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o build/Release
```

---

## Pengembang & Lisensi

- **Arsitek & Pengembang**: **Muhari** ([FokusKonten](https://fokuskonten.my.id))
- **Hak Cipta**: &copy; 2026 FokusKonten. Seluruh hak cipta dilindungi undang-undang.
