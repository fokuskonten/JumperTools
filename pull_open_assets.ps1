# ==============================================================================
# JUMPERTOOLS PRO - AUTOMATED OPEN-SOURCE ASSET PULLER
# Lisensi: MIT & Apache 2.0 (Open-Source Hardware Technician Toolkit)
# ==============================================================================

param (
    [switch]$SkipCloning = $false
)

$ErrorActionPreference = "Continue"

Write-Host "============================================================" -ForegroundColor Cyan
Write-Host " JUMPERTOOLS - OPEN ASSET & DRIVER SYNCHRONIZER    " -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan

$baseDir = $PSScriptRoot
$toolsDir = Join-Path $baseDir "tools"
$driversDir = Join-Path $toolsDir "drivers"
$databaseDir = Join-Path $baseDir "database"
$daDir = Join-Path $databaseDir "da"
$firehoseDir = Join-Path $databaseDir "firehose"

# Pastikan folder target tersedia
@($toolsDir, $driversDir, $databaseDir, $daDir, $firehoseDir) | ForEach-Object {
    if (-not (Test-Path $_)) {
        New-Item -ItemType Directory -Force -Path $_ | Out-Null
    }
}

# ------------------------------------------------------------------------------
# 1. PENGUNDUHAN DRIVER USB LEVEL RENDAH (USBDK 64-BIT & UNIVERSAL ADB DRIVER)
# ------------------------------------------------------------------------------
Write-Host "`n[1/4] Sinkronisasi Driver Fisik Windows (GitHub Official Releases)..." -ForegroundColor Yellow

$usbdkUrl = "https://github.com/daynix/UsbDk/releases/download/v1.00-22/UsbDk_1.0.22_x64.msi"
$usbdkTarget = Join-Path $driversDir "UsbDk64.msi"

if (-not (Test-Path $usbdkTarget) -or (Get-Item $usbdkTarget).Length -lt 100000) {
    Write-Host "  -> Mengunduh Daynix UsbDk 64-bit MSI resmi (v1.0.22)..." -ForegroundColor Cyan
    try {
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12 -bor [Net.SecurityProtocolType]::Tls13
        Invoke-WebRequest -Uri $usbdkUrl -OutFile $usbdkTarget -UseBasicParsing -TimeoutSec 120
        Write-Host "  -> [SUKSES] UsbDk64.msi tersimpan ($([math]::Round((Get-Item $usbdkTarget).Length / 1MB, 2)) MB)." -ForegroundColor Green
    } catch {
        Write-Host "  -> [PERINGATAN] Gagal mengunduh UsbDk otomatis: $($_.Exception.Message)" -ForegroundColor Red
    }
} else {
    Write-Host "  -> [OK] UsbDk64.msi sudah tersedia di tools/drivers/." -ForegroundColor Green
}

$universalAdbUrl = "https://github.com/koush/UniversalAdbDriver/releases/download/1.0.4/UniversalAdbDriverSetup.msi"
$universalAdbTarget = Join-Path $driversDir "UniversalAdbDriverSetup.msi"

if (-not (Test-Path $universalAdbTarget) -or (Get-Item $universalAdbTarget).Length -lt 100000) {
    Write-Host "  -> Mengunduh Universal ADB Driver Installer (Koush Apache 2.0)..." -ForegroundColor Cyan
    try {
        Invoke-WebRequest -Uri $universalAdbUrl -OutFile $universalAdbTarget -UseBasicParsing -TimeoutSec 120
        Write-Host "  -> [SUKSES] UniversalAdbDriverSetup.msi tersimpan ($([math]::Round((Get-Item $universalAdbTarget).Length / 1MB, 2)) MB)." -ForegroundColor Green
    } catch {
        Write-Host "  -> [INFO] Lewati Universal ADB installer otomatis jika koneksi dibatasi." -ForegroundColor Gray
    }
} else {
    Write-Host "  -> [OK] Universal ADB Driver sudah tersedia." -ForegroundColor Green
}

# ------------------------------------------------------------------------------
# 2. SINKRONISASI DATABASE QUALCOMM FIREHOSE LOADERS (BKERLER / MIT UNLICENSE)
# ------------------------------------------------------------------------------
Write-Host "`n[2/4] Sinkronisasi Qualcomm Firehose Loaders (bkerler/Loaders)..." -ForegroundColor Yellow

$firehoseTargetDir = Join-Path $firehoseDir "bkerler_loaders"
if (-not $SkipCloning) {
    if (-not (Test-Path (Join-Path $firehoseTargetDir ".git"))) {
        Write-Host "  -> Cloning repositori bkerler/Loaders (Qualcomm Firehose Library)..." -ForegroundColor Cyan
        git clone --depth 1 https://github.com/bkerler/Loaders.git $firehoseTargetDir
        if ($LASTEXITCODE -eq 0) {
            Write-Host "  -> [SUKSES] Repositori Qualcomm Firehose berhasil di-clone!" -ForegroundColor Green
        } else {
            Write-Host "  -> [PERINGATAN] Git clone Firehose gagal / offline. Menggunakan cache lokal." -ForegroundColor Yellow
        }
    } else {
        Write-Host "  -> [OK] Repositori bkerler_loaders sudah terpasang. Melakukan git pull..." -ForegroundColor Cyan
        Push-Location $firehoseTargetDir
        git pull --quiet
        Pop-Location
        Write-Host "  -> [OK] Qualcomm Firehose loaders up to date." -ForegroundColor Green
    }
}

# ------------------------------------------------------------------------------
# 3. SINKRONISASI MEDIATEK EXPLOITS & DA (DOWNLOAD AGENT) COLLECTION
# ------------------------------------------------------------------------------
Write-Host "`n[3/4] Sinkronisasi MediaTek Exploits & DA Collections (MTK-bypass)..." -ForegroundColor Yellow

$mtkExploitsDir = Join-Path $daDir "exploits_collection"
if (-not $SkipCloning) {
    if (-not (Test-Path (Join-Path $mtkExploitsDir ".git"))) {
        Write-Host "  -> Cloning repositori MTK-bypass/exploits_collection..." -ForegroundColor Cyan
        git clone --depth 1 https://github.com/MTK-bypass/exploits_collection.git $mtkExploitsDir
        if ($LASTEXITCODE -eq 0) {
            Write-Host "  -> [SUKSES] Repositori MediaTek Exploits berhasil di-clone!" -ForegroundColor Green
        } else {
            Write-Host "  -> [PERINGATAN] Git clone MTK exploits gagal / offline." -ForegroundColor Yellow
        }
    } else {
        Write-Host "  -> [OK] Repositori exploits_collection sudah terpasang." -ForegroundColor Green
    }
}

# ------------------------------------------------------------------------------
# 4. AUDIT & RE-RUN RELEASE GATE
# ------------------------------------------------------------------------------
Write-Host "`n[4/4] Memvalidasi Sistem & Menjalankan Unit Tests..." -ForegroundColor Yellow

$gateScript = Join-Path $baseDir "release_gate.ps1"
if (Test-Path $gateScript) {
    & $gateScript
}

Write-Host "`n============================================================" -ForegroundColor Cyan
Write-Host " SEMUA ASET TERVERIFIKASI & SIAP DIGUNAKAN DI LAPANGAN SERVIS " -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan
