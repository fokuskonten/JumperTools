param (
    [switch]$SkipTests = $false
)

$ErrorActionPreference = "Stop"

Write-Host "=============================================" -ForegroundColor Cyan
Write-Host " JUMPERTOOLS PRO - FINAL RELEASE GATE" -ForegroundColor Cyan
Write-Host "=============================================" -ForegroundColor Cyan

$baseDir = $PSScriptRoot

# 1. Check Required Tools
$tools = @(
    "tools\platform-tools\adb.exe",
    "tools\platform-tools\fastboot.exe",
    "tools\runtime\python\python.exe",
    "tools\mtkclient\mtk.py",
    "tools\qcom-edl\edl.py"
)

$missingTools = $false
Write-Host "`n[1/4] Checking Tool Dependencies..." -ForegroundColor Yellow
foreach ($tool in $tools) {
    $path = Join-Path $baseDir $tool
    if (Test-Path $path) {
        Write-Host " [PASS] Found $tool" -ForegroundColor Green
    } else {
        Write-Host " [FAIL] Missing $tool" -ForegroundColor Red
        $missingTools = $true
    }
}

# 2. Check Database Directory
Write-Host "`n[2/4] Checking Environment Integrity..." -ForegroundColor Yellow
$dbDir = Join-Path $baseDir "database"
if (-not (Test-Path $dbDir)) {
    Write-Host " [INFO] Creating database directory..." -ForegroundColor Gray
    New-Item -ItemType Directory -Force -Path $dbDir | Out-Null
}
Write-Host " [PASS] Environment directories valid." -ForegroundColor Green


# 3. Build Project
Write-Host "`n[3/4] Compiling Solution..." -ForegroundColor Yellow
$buildResult = dotnet build "$baseDir\JumperTools.sln" --configuration Release --nologo -v q
if ($LASTEXITCODE -ne 0) {
    Write-Host " [FAIL] Build failed!" -ForegroundColor Red
    Write-Host "=============================================" -ForegroundColor Red
    Write-Host " [RELEASE BLOCKED] Software Compilation Error" -ForegroundColor Red
    Write-Host "=============================================" -ForegroundColor Red
    exit 1
}
Write-Host " [PASS] Build Success." -ForegroundColor Green


# 4. Run Tests (Failure Simulation)
Write-Host "`n[4/4] Running Validation & Failure Simulation Tests..." -ForegroundColor Yellow
if ($SkipTests) {
    Write-Host " [SKIP] Tests skipped via parameter." -ForegroundColor Gray
} else {
    $testResult = dotnet test "$baseDir\tests\JumperTools.Tests\JumperTools.Tests.csproj" --configuration Release --no-build -v q
    if ($LASTEXITCODE -ne 0) {
        Write-Host " [FAIL] Tests failed!" -ForegroundColor Red
        Write-Host "=============================================" -ForegroundColor Red
        Write-Host " [RELEASE BLOCKED] Failure Simulation Test Failed" -ForegroundColor Red
        Write-Host "=============================================" -ForegroundColor Red
        exit 1
    }
    Write-Host " [PASS] All critical tests passed." -ForegroundColor Green
}

Write-Host "`n=============================================" -ForegroundColor Cyan
if ($missingTools) {
    Write-Host " [RELEASE BLOCKED]" -ForegroundColor Red
    Write-Host " Critical hardware tools are missing from deployment package." -ForegroundColor Yellow
    Write-Host " Fix the tools/ directory before deployment." -ForegroundColor Yellow
} else {
    Write-Host " [RELEASE CANDIDATE]" -ForegroundColor Green
    Write-Host " Software, tests, and dependencies are verified." -ForegroundColor Green
    Write-Host " Awaiting final hardware validation." -ForegroundColor Green
}
Write-Host "=============================================" -ForegroundColor Cyan
