using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using JumperTools.Core;
using JumperTools.Models;

namespace JumperTools.UI.ViewModels
{
    public partial class MainViewModel
    {
        // ==================== PARTITION MANAGER OPERATIONS ====================

        private DeviceConnectionProtocol DetermineDeviceProtocol(out string? deviceParam)
        {
            deviceParam = SelectedDevice?.DeviceId;
            if (SelectedDevice == null)
            {
                return DeviceConnectionProtocol.AutoDetect;
            }

            if (SelectedDevice.Mode == DeviceMode.Fastboot)
            {
                return DeviceConnectionProtocol.Fastboot;
            }
            if (SelectedDevice.Mode == DeviceMode.Odin || (SelectedDevice.Mode == DeviceMode.Download && SelectedDevice.Brand == "Samsung"))
            {
                return DeviceConnectionProtocol.SamsungOdin;
            }
            if (SelectedDevice.Mode == DeviceMode.BROM || SelectedDevice.Brand == "MediaTek" || SelectedDevice.Chipset?.Contains("MediaTek") == true)
            {
                return DeviceConnectionProtocol.MediaTekBrom;
            }
            if (SelectedDevice.Mode == DeviceMode.EDL || SelectedDevice.Brand == "Qualcomm" || SelectedDevice.Chipset?.Contains("Qualcomm") == true)
            {
                return DeviceConnectionProtocol.QualcommEdl;
            }
            if (SelectedDevice.Brand == "Unisoc" || SelectedDevice.Chipset?.Contains("Unisoc") == true || SelectedDevice.Chipset?.Contains("Spreadtrum") == true)
            {
                return DeviceConnectionProtocol.UnisocSpd;
            }
            if (SelectedDevice.Mode == DeviceMode.Normal || SelectedDevice.Mode == DeviceMode.Recovery)
            {
                return DeviceConnectionProtocol.AdbRoot;
            }

            return DeviceConnectionProtocol.AutoDetect;
        }

        public async Task ReadPartitionAsync()
        {
            if (SelectedPartition == null)
            {
                AddLog("[Peringatan] Pilih partisi yang ingin di-dump dari tabel partisi.");
                return;
            }

            var protocol = DetermineDeviceProtocol(out var deviceParam);
            var device = SelectedDevice ?? new Device
            {
                DeviceId = deviceParam ?? "DEVICE",
                Brand = SelectedPhoneModel?.Brand ?? "Android",
                Model = SelectedPhoneModel?.ModelName ?? "Generic",
                Mode = SelectedDevice?.Mode ?? DeviceMode.Fastboot
            };

            var dumpDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "backups", "partitions");

            var result = await _operationOrchestrator.RunOperationAsync(
                device,
                OperationType.BackupPartition,
                $"Dump Partisi [{SelectedPartition.Name}]",
                executeAction: async (jobId, ct) =>
                {
                    var path = await _partitionEngine.ReadPartitionAsync(SelectedPartition, dumpDir, protocol, deviceParam);
                    return !string.IsNullOrEmpty(path);
                },
                targetPartition: SelectedPartition.Name);

            OperationStatus = result.State == OperationState.Completed ? "Completed" : "Failed";
            CurrentOperation = result.State == OperationState.Completed ? $"Dump {SelectedPartition.Name} Sukses" : $"Dump Gagal: {result.FailureReason}";
        }

        public async Task WritePartitionAsync()
        {
            if (SelectedPartition == null)
            {
                AddLog("[Peringatan] Pilih partisi target sebelum melakukan flashing.");
                return;
            }

            var imagePath = SelectedPartition.SelectedFileToFlash;
            if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath))
            {
                var openFileDialog = new Microsoft.Win32.OpenFileDialog
                {
                    Title = $"Pilih File Image untuk Partisi [{SelectedPartition.Name}]",
                    Filter = "Image / Binary Files (*.img;*.bin;*.elf;*.mbn)|*.img;*.bin;*.elf;*.mbn|All Files (*.*)|*.*",
                    CheckFileExists = true
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    imagePath = openFileDialog.FileName;
                    SelectedPartition.SelectedFileToFlash = imagePath;
                }
                else
                {
                    AddLog("[Batal] Pemilihan file flashing partisi dibatalkan.");
                    return;
                }
            }

            var protocol = DetermineDeviceProtocol(out var deviceParam);
            var device = SelectedDevice ?? new Device
            {
                DeviceId = deviceParam ?? "DEVICE",
                Brand = SelectedPhoneModel?.Brand ?? "Android",
                Model = SelectedPhoneModel?.ModelName ?? "Generic",
                Mode = SelectedDevice?.Mode ?? DeviceMode.Fastboot
            };

            var dumpDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "backups", "partitions");

            var result = await _operationOrchestrator.RunOperationAsync(
                device,
                OperationType.FlashPartition,
                $"Flash Partisi [{SelectedPartition.Name}]",
                executeAction: async (jobId, ct) =>
                {
                    return await _partitionEngine.WritePartitionAsync(SelectedPartition, imagePath, protocol, deviceParam);
                },
                backupAction: async (ct) =>
                {
                    return await _partitionEngine.ReadPartitionAsync(SelectedPartition, dumpDir, protocol, deviceParam);
                },
                targetPartition: SelectedPartition.Name,
                firmwareFilePath: imagePath);

            OperationStatus = result.State == OperationState.Completed ? "Completed" : "Failed";
            CurrentOperation = result.State == OperationState.Completed ? $"Flash {SelectedPartition.Name} Selesai" : $"Flash Gagal: {result.FailureReason}";
        }

        public async Task ErasePartitionAsync()
        {
            if (SelectedPartition == null)
            {
                AddLog("[Peringatan] Pilih partisi yang ingin dihapus (Wipe).");
                return;
            }

            // PERBAIKAN Cacat X1: Dialog konfirmasi berlapis sebelum erase partisi kritis.
            // Engine sudah hard-block di layer bawah — tapi user WAJIB tahu alasannya
            // dan harus memberi konfirmasi sadar sebelum lanjut.
            var risk = PartitionManagerEngine.ClassifyPartitionRisk(SelectedPartition.PartitionName);
            bool forceCritical = false;

            if (risk == PartitionRiskLevel.CriticalRadio)
            {
                // Langkah 1: Peringatan merah eksplisit
                var step1 = System.Windows.MessageBox.Show(
                    $"⛔  PERINGATAN KRITIS — PARTISI RADIO / BASEBAND\n\n" +
                    $"Anda akan menghapus partisi:\n" +
                    $"  Nama  : {SelectedPartition.PartitionName}\n" +
                    $"  Risiko : IMEI Hilang Permanen / HP Mati Total\n\n" +
                    $"Partisi ini menyimpan kalibrasi sinyal, IMEI, dan lisensi baseband.\n" +
                    $"Menghapusnya tanpa backup dapat membuat HP tidak bisa diperbaiki!\n\n" +
                    $"Pastikan Anda sudah memiliki file backup (.bin) dari partisi ini!\n\n" +
                    $"Klik YES untuk konfirmasi pertama, atau NO untuk batal.",
                    $"⛔ Konfirmasi Erase Partisi Kritis [{SelectedPartition.PartitionName}]",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Warning,
                    System.Windows.MessageBoxResult.No);

                if (step1 != System.Windows.MessageBoxResult.Yes)
                {
                    AddLog($"[Dibatalkan] Erase partisi kritis '{SelectedPartition.PartitionName}' dibatalkan pada konfirmasi pertama.");
                    return;
                }

                // Langkah 2: Konfirmasi terakhir — stop default adalah No
                var step2 = System.Windows.MessageBox.Show(
                    $"KONFIRMASI TERAKHIR\n\n" +
                    $"Apakah Anda benar-benar yakin ingin menghapus partisi '{SelectedPartition.PartitionName}'?\n\n" +
                    $"Operasi ini TIDAK DAPAT DIBATALKAN setelah dimulai.\n" +
                    $"HP harus dalam mode Fastboot / Fastbootd untuk operasi ini.",
                    $"Konfirmasi Terakhir — Erase [{SelectedPartition.PartitionName}]",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Stop,
                    System.Windows.MessageBoxResult.No);

                if (step2 != System.Windows.MessageBoxResult.Yes)
                {
                    AddLog($"[Dibatalkan] Erase partisi kritis '{SelectedPartition.PartitionName}' dibatalkan pada konfirmasi kedua.");
                    return;
                }

                forceCritical = true;
                AddLog($"[Konfirmasi Teknisi ✓] Erase paksa pada '{SelectedPartition.PartitionName}' dilanjutkan atas konfirmasi 2 langkah.");
            }
            else if (risk == PartitionRiskLevel.Warning)
            {
                var warningResult = System.Windows.MessageBox.Show(
                    $"⚠️ Peringatan Erase Partisi Sistem\n\n" +
                    $"Anda akan menghapus partisi: {SelectedPartition.PartitionName}\n" +
                    $"Partisi ini adalah bagian dari OS Android (Boot / Recovery / Super / Vendor).\n\n" +
                    $"HP mungkin tidak bisa booting setelah erase tanpa flash ulang firmware.\n\n" +
                    $"Lanjutkan?",
                    $"Peringatan Erase [{SelectedPartition.PartitionName}]",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Warning,
                    System.Windows.MessageBoxResult.No);

                if (warningResult != System.Windows.MessageBoxResult.Yes)
                {
                    AddLog($"[Dibatalkan] Erase partisi sistem '{SelectedPartition.PartitionName}' dibatalkan.");
                    return;
                }
            }

            var protocol = DetermineDeviceProtocol(out var deviceParam);
            var device = SelectedDevice ?? new Device
            {
                DeviceId = deviceParam ?? "DEVICE",
                Brand = SelectedPhoneModel?.Brand ?? "Android",
                Model = SelectedPhoneModel?.ModelName ?? "Generic",
                Mode = SelectedDevice?.Mode ?? DeviceMode.Fastboot
            };

            var dumpDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "backups", "partitions");

            var result = await _operationOrchestrator.RunOperationAsync(
                device,
                OperationType.ErasePartition,
                $"Wipe Partisi [{SelectedPartition.Name}]",
                executeAction: async (jobId, ct) =>
                {
                    return await _partitionEngine.ErasePartitionAsync(SelectedPartition, protocol, deviceParam, ct, forceCritical);
                },
                backupAction: async (ct) =>
                {
                    return await _partitionEngine.ReadPartitionAsync(SelectedPartition, dumpDir, protocol, deviceParam);
                },
                targetPartition: SelectedPartition.Name);

            OperationStatus = result.State == OperationState.Completed ? "Completed" : "Failed";
            CurrentOperation = result.State == OperationState.Completed ? $"Wipe {SelectedPartition.Name} Selesai" : $"Wipe Gagal: {result.FailureReason}";
        }

        public async Task RefreshLivePartitionTableAsync()
        {
            CurrentOperation = "Membaca Tabel Partisi Hardware...";
            OperationProgress = 25;

            var protocol = DetermineDeviceProtocol(out var deviceParam);
            var serial = SelectedDevice?.DeviceId ?? deviceParam;
            AddLog($"[Partition Manager] Mengidentifikasi tabel partisi fisik untuk perangkat ({protocol})...");

            var realPartitions = await _partitionEngine.ReadLivePartitionTableAsync(
                serial,
                protocol.ToString(),
                SelectedDevice?.PortName,
                CancellationToken.None);

            App.Current?.Dispatcher?.Invoke(() =>
            {
                Partitions.Clear();
                foreach (var p in realPartitions) Partitions.Add(p);
            });

            OperationProgress = 100;
            if (Partitions.Count > 0)
            {
                CurrentOperation = $"Tabel Partisi Nyata Dimuat ({Partitions.Count} Partisi)";
                AddLog($"[Partition Manager] Sukses memuat {Partitions.Count} partisi fisik dari perangkat.");
            }
            else
            {
                CurrentOperation = "Gagal Membaca Partisi (Perangkat Offline / Tidak Merespons)";
                AddLog("[Partition Manager] Tidak ada partisi yang terbaca. Pastikan mode koneksi hardware sesuai dan izin otorisasi aktif.");
            }
        }
    }
}
