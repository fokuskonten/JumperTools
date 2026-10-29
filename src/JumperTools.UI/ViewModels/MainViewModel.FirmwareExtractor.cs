using System;
using System.IO;
using System.Threading.Tasks;

namespace JumperTools.UI.ViewModels
{
    public partial class MainViewModel
    {
        // ==================== FIRMWARE EXTRACTOR OPERATIONS ====================

        public async Task ExtractPayloadBinAsync()
        {
            if (string.IsNullOrWhiteSpace(FirmwareFilePath) || !File.Exists(FirmwareFilePath))
            {
                AddLog("[Ekstraktor] Harap pilih berkas payload.bin terlebih dahulu menggunakan tombol [📂 Pilih File...].");
                return;
            }

            CurrentOperation = "Mengekstrak Android OTA payload.bin...";
            OperationProgress = 15;

            try
            {
                var outDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "extracted", "payload");
                var list = await _extractorEngine.ExtractPayloadBinAsync(FirmwareFilePath, outDir);
                ExtractedFiles.Clear();
                foreach (var f in list) ExtractedFiles.Add(f);
                OperationProgress = 100;
                CurrentOperation = $"Ekstraksi payload.bin Selesai ({list.Count} partisi)";
            }
            catch (Exception ex)
            {
                AddLog($"[Extractor Error] {ex.Message}");
            }
        }

        public async Task ExtractOfpAsync()
        {
            if (string.IsNullOrWhiteSpace(FirmwareFilePath) || !File.Exists(FirmwareFilePath))
            {
                AddLog("[Ekstraktor] Harap pilih berkas OFP Oppo/Realme terlebih dahulu menggunakan tombol [📂 Pilih File...].");
                return;
            }

            CurrentOperation = "Mendekripsi & Mengekstrak Oppo/Realme OFP...";
            OperationProgress = 15;

            try
            {
                var outDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "extracted", "ofp");
                var list = await _extractorEngine.ExtractOppoOfpAsync(FirmwareFilePath, outDir);
                ExtractedFiles.Clear();
                foreach (var f in list) ExtractedFiles.Add(f);
                OperationProgress = 100;
                CurrentOperation = "Ekstraksi OFP Selesai";
            }
            catch (Exception ex)
            {
                AddLog($"[Extractor Error] {ex.Message}");
            }
        }

        public async Task ExtractSamsungTarAsync()
        {
            if (string.IsNullOrWhiteSpace(FirmwareFilePath) || !File.Exists(FirmwareFilePath))
            {
                AddLog("[Ekstraktor] Harap pilih berkas .tar / .tar.md5 Samsung terlebih dahulu menggunakan tombol [📂 Pilih File...].");
                return;
            }

            CurrentOperation = "Mengekstrak Samsung .tar.md5 Archive...";
            OperationProgress = 15;

            try
            {
                var outDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "extracted", "samsung_tar");
                var list = await _extractorEngine.ExtractSamsungTarMd5Async(FirmwareFilePath, outDir);
                ExtractedFiles.Clear();
                foreach (var f in list) ExtractedFiles.Add(f);
                OperationProgress = 100;
                CurrentOperation = "Ekstraksi Samsung TAR Selesai";
            }
            catch (Exception ex)
            {
                AddLog($"[Extractor Error] {ex.Message}");
            }
        }

        public async Task ParseScatterAsync()
        {
            if (string.IsNullOrWhiteSpace(FirmwareFilePath) || !File.Exists(FirmwareFilePath))
            {
                AddLog("[Ekstraktor] Harap pilih berkas Scatter MTK (*_Android_scatter.txt) terlebih dahulu menggunakan tombol [📂 Pilih File...].");
                return;
            }

            CurrentOperation = "Membaca MediaTek Scatter File...";
            OperationProgress = 20;

            try
            {
                var list = await _extractorEngine.ParseMediaTekScatterAsync(FirmwareFilePath);
                ExtractedFiles.Clear();
                foreach (var f in list) ExtractedFiles.Add(f);
                OperationProgress = 100;
                CurrentOperation = $"Berhasil memetakan {list.Count} partisi scatter";
            }
            catch (Exception ex)
            {
                AddLog($"[Scatter Error] {ex.Message}");
            }
        }

        public async Task ParseRawProgramAsync()
        {
            if (string.IsNullOrWhiteSpace(FirmwareFilePath) || !File.Exists(FirmwareFilePath))
            {
                AddLog("[Ekstraktor] Harap pilih berkas rawprogram XML Qualcomm (rawprogram0.xml) terlebih dahulu menggunakan tombol [📂 Pilih File...].");
                return;
            }

            CurrentOperation = "Membaca Qualcomm rawprogram XML...";
            OperationProgress = 20;

            try
            {
                var list = await _extractorEngine.ParseQualcommRawProgramAsync(FirmwareFilePath);
                ExtractedFiles.Clear();
                foreach (var f in list) ExtractedFiles.Add(f);
                OperationProgress = 100;
                CurrentOperation = $"Berhasil memetakan {list.Count} partisi Qualcomm";
            }
            catch (Exception ex)
            {
                AddLog($"[RawProgram Error] {ex.Message}");
            }
        }

        public void BrowseFirmwareFile()
        {
            try
            {
                var ofd = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "Pilih Berkas Firmware / ROM / Scatter / XML",
                    Filter = "Firmware Files (*.bin;*.ofp;*.tar;*.md5;*.txt;*.xml;*.zip)|*.bin;*.ofp;*.tar;*.md5;*.txt;*.xml;*.zip|All Files (*.*)|*.*"
                };

                if (ofd.ShowDialog() == true)
                {
                    FirmwareFilePath = ofd.FileName;
                    AddLog($"[Firmware File] Dipilih: {ofd.FileName}");
                }
            }
            catch (Exception ex)
            {
                AddLog($"[File Picker Error] {ex.Message}");
            }
        }

        public async Task FlashExtractedFilesAsync()
        {
            if (ExtractedFiles.Count == 0)
            {
                AddLog("[Flasher Warning] Tidak ada partisi yang dimuat untuk di-flash. Harap ekstrak / parse firmware terlebih dahulu.");
                return;
            }

            CurrentOperation = "Flashing Berurutan Partisi Hasil Ekstraksi...";
            OperationProgress = 5;

            try
            {
                var total = ExtractedFiles.Count;
                var successCount = 0;

                AddLog("╔════════════════════════════════════════════════════════════════╗");
                AddLog($"║      MEMULAI FLASHING MULTI-PARTISI ({total} PARTISI)              ║");
                AddLog("╚════════════════════════════════════════════════════════════════╝");

                for (int i = 0; i < total; i++)
                {
                    var part = ExtractedFiles[i];
                    var currentStep = i + 1;
                    var pct = (int)((double)currentStep / total * 100.0);
                    OperationProgress = pct;
                    CurrentOperation = $"Flashing [{part.PartitionName}] ({currentStep}/{total})...";

                    if (!File.Exists(part.FilePath))
                    {
                        AddLog($"[Lewati] Berkas tidak ditemukan fisik di disk: {part.FilePath}");
                        continue;
                    }

                    // PERBAIKAN P4-3: Validasi ukuran file > 0 sebelum flash.
                    // Partisi dari payload parser yang gagal (CrAU valid tapi tool tidak ada)
                    // bisa menghasilkan entri dengan FilePath ke file 0-byte atau non-existent.
                    var imgInfo = new FileInfo(part.FilePath);
                    if (imgInfo.Length == 0)
                    {
                        AddLog($"[Lewati] Berkas [{part.PartitionName}] berukuran 0-byte — kemungkinan ekstraksi payload belum selesai.");
                        continue;
                    }

                    AddLog($"[{currentStep}/{total}] Menulis partisi [{part.PartitionName}] dari '{part.FileName}' ({imgInfo.Length / (1024 * 1024):N1} MB)...");
                    var ok = await _fastbootEngine.FlashPartitionAsync(part.PartitionName, part.FilePath, SelectedDevice?.DeviceId);
                    if (ok) successCount++;
                }

                OperationProgress = 100;
                CurrentOperation = $"Flashing Selesai ({successCount}/{total} Sukses)";
                AddLog($"\n[SUKSES] Flashing multi-partisi selesai. Total sukses: {successCount}/{total} partisi.");
            }
            catch (Exception ex)
            {
                AddLog($"[Flash Error] {ex.Message}");
            }
        }

        public void OpenExtractedFolder()
        {
            try
            {
                var baseDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "extracted");
                if (!Directory.Exists(baseDir)) Directory.CreateDirectory(baseDir);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = baseDir,
                    UseShellExecute = true
                });
                AddLog($"[Folder] Membuka direktori penyimpanan hasil ekstraksi: {baseDir}");
            }
            catch (Exception ex)
            {
                AddLog($"[Folder Error] {ex.Message}");
            }
        }
    }
}
