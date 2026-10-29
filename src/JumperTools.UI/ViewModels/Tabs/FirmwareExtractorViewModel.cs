using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using JumperTools.Core;
using JumperTools.Models;
using JumperTools.UI.ViewModels.Shared;
using Microsoft.Win32;

namespace JumperTools.UI.ViewModels.Tabs
{
    public class FirmwareExtractorViewModel : TabViewModelBase
    {
        private readonly FirmwareExtractorEngine _extractorEngine;
        private readonly FastbootEngine _fastbootEngine;

        private string _firmwareFilePath = string.Empty;

        public ObservableCollection<ExtractedFileInfo> ExtractedFiles { get; }

        public ICommand BrowseFirmwareFileCommand { get; }
        public ICommand ExtractPayloadBinCommand { get; }
        public ICommand ExtractOfpCommand { get; }
        public ICommand ExtractSamsungTarCommand { get; }
        public ICommand ParseScatterCommand { get; }
        public ICommand ParseRawProgramCommand { get; }
        public ICommand FlashExtractedFilesCommand { get; }
        public ICommand OpenExtractedFolderCommand { get; }

        public FirmwareExtractorViewModel(ISharedWorkspaceContext context, FirmwareExtractorEngine extractorEngine, FastbootEngine fastbootEngine)
            : base(context)
        {
            _extractorEngine = extractorEngine;
            _fastbootEngine = fastbootEngine;
            ExtractedFiles = new ObservableCollection<ExtractedFileInfo>();

            BrowseFirmwareFileCommand = new RelayCommand(BrowseFirmwareFile);
            ExtractPayloadBinCommand = new AsyncRelayCommand(ExtractPayloadBinAsync);
            ExtractOfpCommand = new AsyncRelayCommand(ExtractOfpAsync);
            ExtractSamsungTarCommand = new AsyncRelayCommand(ExtractSamsungTarAsync);
            ParseScatterCommand = new AsyncRelayCommand(ParseScatterAsync);
            ParseRawProgramCommand = new AsyncRelayCommand(ParseRawProgramAsync);
            FlashExtractedFilesCommand = new AsyncRelayCommand(FlashExtractedFilesAsync);
            OpenExtractedFolderCommand = new RelayCommand(OpenExtractedFolder);
        }

        public string FirmwareFilePath
        {
            get => _firmwareFilePath;
            set
            {
                if (_firmwareFilePath != value)
                {
                    _firmwareFilePath = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool HasNoExtractedFiles => ExtractedFiles.Count == 0;

        public void BrowseFirmwareFile()
        {
            try
            {
                var ofd = new OpenFileDialog
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

        public async Task ExtractPayloadBinAsync()
        {
            if (string.IsNullOrWhiteSpace(FirmwareFilePath) || !File.Exists(FirmwareFilePath))
            {
                AddLog("[Ekstraktor] Harap pilih berkas payload.bin terlebih dahulu menggunakan tombol [📂 Pilih File...].");
                return;
            }

            Context.CurrentOperation = "Mengekstrak Android OTA payload.bin...";
            Context.OperationProgress = 15;

            try
            {
                var outDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "extracted", "payload");
                var list = await _extractorEngine.ExtractPayloadBinAsync(FirmwareFilePath, outDir);
                ExtractedFiles.Clear();
                foreach (var f in list) ExtractedFiles.Add(f);
                OnPropertyChanged(nameof(HasNoExtractedFiles));
                Context.OperationProgress = 100;
                Context.CurrentOperation = $"Ekstraksi payload.bin Selesai ({list.Count} partisi)";
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

            Context.CurrentOperation = "Mendekripsi & Mengekstrak Oppo/Realme OFP...";
            Context.OperationProgress = 15;

            try
            {
                var outDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "extracted", "ofp");
                var list = await _extractorEngine.ExtractOppoOfpAsync(FirmwareFilePath, outDir);
                ExtractedFiles.Clear();
                foreach (var f in list) ExtractedFiles.Add(f);
                OnPropertyChanged(nameof(HasNoExtractedFiles));
                Context.OperationProgress = 100;
                Context.CurrentOperation = "Ekstraksi OFP Selesai";
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

            Context.CurrentOperation = "Mengekstrak Samsung .tar.md5 Archive...";
            Context.OperationProgress = 15;

            try
            {
                var outDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "extracted", "samsung_tar");
                var list = await _extractorEngine.ExtractSamsungTarMd5Async(FirmwareFilePath, outDir);
                ExtractedFiles.Clear();
                foreach (var f in list) ExtractedFiles.Add(f);
                OnPropertyChanged(nameof(HasNoExtractedFiles));
                Context.OperationProgress = 100;
                Context.CurrentOperation = "Ekstraksi Samsung TAR Selesai";
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

            Context.CurrentOperation = "Membaca MediaTek Scatter File...";
            Context.OperationProgress = 20;

            try
            {
                var list = await _extractorEngine.ParseMediaTekScatterAsync(FirmwareFilePath);
                ExtractedFiles.Clear();
                foreach (var f in list) ExtractedFiles.Add(f);
                OnPropertyChanged(nameof(HasNoExtractedFiles));
                Context.OperationProgress = 100;
                Context.CurrentOperation = $"Berhasil memetakan {list.Count} partisi scatter";
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

            Context.CurrentOperation = "Membaca Qualcomm rawprogram XML...";
            Context.OperationProgress = 20;

            try
            {
                var list = await _extractorEngine.ParseQualcommRawProgramAsync(FirmwareFilePath);
                ExtractedFiles.Clear();
                foreach (var f in list) ExtractedFiles.Add(f);
                OnPropertyChanged(nameof(HasNoExtractedFiles));
                Context.OperationProgress = 100;
                Context.CurrentOperation = $"Berhasil memetakan {list.Count} partisi Qualcomm";
            }
            catch (Exception ex)
            {
                AddLog($"[RawProgram Error] {ex.Message}");
            }
        }

        public async Task FlashExtractedFilesAsync()
        {
            if (ExtractedFiles.Count == 0)
            {
                AddLog("[Flasher Warning] Tidak ada partisi yang dimuat untuk di-flash. Harap ekstrak / parse firmware terlebih dahulu.");
                return;
            }

            Context.CurrentOperation = "Flashing Berurutan Partisi Hasil Ekstraksi...";
            Context.OperationProgress = 5;

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
                    Context.OperationProgress = pct;
                    Context.CurrentOperation = $"Flashing [{part.PartitionName}] ({currentStep}/{total})...";

                    if (!File.Exists(part.FilePath))
                    {
                        AddLog($"[Lewati] Berkas tidak ditemukan fisik di disk: {part.FilePath}");
                        continue;
                    }

                    AddLog($"[{currentStep}/{total}] Menulis partisi [{part.PartitionName}] dari '{part.FileName}'...");
                    var ok = await _fastbootEngine.FlashPartitionAsync(part.PartitionName, part.FilePath, Context.SelectedDevice?.DeviceId);
                    if (ok) successCount++;
                }

                Context.OperationProgress = 100;
                Context.CurrentOperation = $"Flashing Selesai ({successCount}/{total} Sukses)";
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
                Process.Start(new ProcessStartInfo
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
