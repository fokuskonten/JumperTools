using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using JumperTools.Core;
using JumperTools.Models;
using JumperTools.UI.ViewModels.Shared;
using Microsoft.Win32;

namespace JumperTools.UI.ViewModels.Tabs
{
    public class PartitionManagerViewModel : TabViewModelBase
    {
        private readonly PartitionManagerEngine _partitionEngine;
        private PartitionTableItem? _selectedPartition;

        public ObservableCollection<PartitionTableItem> Partitions { get; }

        public ICommand RefreshLivePartitionTableCommand { get; }
        public ICommand ReadPartitionCommand { get; }
        public ICommand WritePartitionCommand { get; }
        public ICommand ErasePartitionCommand { get; }

        public PartitionManagerViewModel(ISharedWorkspaceContext context, PartitionManagerEngine partitionEngine)
            : base(context)
        {
            _partitionEngine = partitionEngine;
            Partitions = new ObservableCollection<PartitionTableItem>();

            RefreshLivePartitionTableCommand = new AsyncRelayCommand(RefreshLivePartitionTableAsync);
            ReadPartitionCommand = new AsyncRelayCommand(ReadPartitionAsync);
            WritePartitionCommand = new AsyncRelayCommand(WritePartitionAsync);
            ErasePartitionCommand = new AsyncRelayCommand(ErasePartitionAsync);
        }

        public PartitionTableItem? SelectedPartition
        {
            get => _selectedPartition;
            set
            {
                if (_selectedPartition != value)
                {
                    _selectedPartition = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool HasNoPartitions => Partitions.Count == 0;

        private DeviceConnectionProtocol DetermineDeviceProtocol(out string? deviceParam)
        {
            deviceParam = Context.SelectedDevice?.DeviceId;
            if (Context.SelectedDevice == null)
            {
                return DeviceConnectionProtocol.AutoDetect;
            }

            if (Context.SelectedDevice.Mode == DeviceMode.Fastboot)
            {
                return DeviceConnectionProtocol.Fastboot;
            }
            if (Context.SelectedDevice.Mode == DeviceMode.Odin || (Context.SelectedDevice.Mode == DeviceMode.Download && Context.SelectedDevice.Brand == "Samsung"))
            {
                return DeviceConnectionProtocol.SamsungOdin;
            }
            if (Context.SelectedDevice.Mode == DeviceMode.BROM || Context.SelectedDevice.Brand == "MediaTek" || Context.SelectedDevice.Chipset?.Contains("MediaTek") == true)
            {
                return DeviceConnectionProtocol.MediaTekBrom;
            }
            if (Context.SelectedDevice.Mode == DeviceMode.EDL || Context.SelectedDevice.Brand == "Qualcomm" || Context.SelectedDevice.Chipset?.Contains("Qualcomm") == true)
            {
                return DeviceConnectionProtocol.QualcommEdl;
            }
            if (Context.SelectedDevice.Brand == "Unisoc" || Context.SelectedDevice.Chipset?.Contains("Unisoc") == true || Context.SelectedDevice.Chipset?.Contains("Spreadtrum") == true)
            {
                return DeviceConnectionProtocol.UnisocSpd;
            }
            if (Context.SelectedDevice.Mode == DeviceMode.Normal || Context.SelectedDevice.Mode == DeviceMode.Recovery)
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
            var device = Context.SelectedDevice ?? new Device
            {
                DeviceId = deviceParam ?? "DEVICE",
                Brand = "Android",
                Model = "Generic",
                Mode = Context.SelectedDevice?.Mode ?? DeviceMode.Fastboot
            };

            var dumpDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "backups", "partitions");

            var result = await Context.Orchestrator.RunOperationAsync(
                device,
                OperationType.BackupPartition,
                $"Dump Partisi [{SelectedPartition.Name}]",
                executeAction: async (jobId, ct) =>
                {
                    var path = await _partitionEngine.ReadPartitionAsync(SelectedPartition, dumpDir, protocol, deviceParam);
                    return !string.IsNullOrEmpty(path);
                },
                targetPartition: SelectedPartition.Name);

            Context.CurrentOperation = result.State == OperationState.Completed ? $"Dump {SelectedPartition.Name} Sukses" : $"Dump Gagal: {result.FailureReason}";
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
                var openFileDialog = new OpenFileDialog
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
            var device = Context.SelectedDevice ?? new Device
            {
                DeviceId = deviceParam ?? "DEVICE",
                Brand = "Android",
                Model = "Generic",
                Mode = Context.SelectedDevice?.Mode ?? DeviceMode.Fastboot
            };

            var dumpDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "backups", "partitions");

            var result = await Context.Orchestrator.RunOperationAsync(
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

            Context.CurrentOperation = result.State == OperationState.Completed ? $"Flash {SelectedPartition.Name} Selesai" : $"Flash Gagal: {result.FailureReason}";
        }

        public async Task ErasePartitionAsync()
        {
            if (SelectedPartition == null)
            {
                AddLog("[Peringatan] Pilih partisi yang ingin dihapus (Wipe).");
                return;
            }

            var protocol = DetermineDeviceProtocol(out var deviceParam);
            var device = Context.SelectedDevice ?? new Device
            {
                DeviceId = deviceParam ?? "DEVICE",
                Brand = "Android",
                Model = "Generic",
                Mode = Context.SelectedDevice?.Mode ?? DeviceMode.Fastboot
            };

            var dumpDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "backups", "partitions");

            var result = await Context.Orchestrator.RunOperationAsync(
                device,
                OperationType.ErasePartition,
                $"Wipe Partisi [{SelectedPartition.Name}]",
                executeAction: async (jobId, ct) =>
                {
                    return await _partitionEngine.ErasePartitionAsync(SelectedPartition, protocol, deviceParam);
                },
                backupAction: async (ct) =>
                {
                    return await _partitionEngine.ReadPartitionAsync(SelectedPartition, dumpDir, protocol, deviceParam);
                },
                targetPartition: SelectedPartition.Name);

            Context.CurrentOperation = result.State == OperationState.Completed ? $"Wipe {SelectedPartition.Name} Selesai" : $"Wipe Gagal: {result.FailureReason}";
        }

        public async Task RefreshLivePartitionTableAsync()
        {
            Context.CurrentOperation = "Membaca Tabel Partisi Hardware...";
            Context.OperationProgress = 25;

            var protocol = DetermineDeviceProtocol(out var deviceParam);
            var serial = Context.SelectedDevice?.DeviceId ?? deviceParam;
            AddLog($"[Partition Manager] Mengidentifikasi tabel partisi fisik untuk perangkat ({protocol})...");

            var realPartitions = await _partitionEngine.ReadLivePartitionTableAsync(
                serial,
                protocol.ToString(),
                Context.SelectedDevice?.PortName,
                CancellationToken.None);

            System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
            {
                Partitions.Clear();
                foreach (var p in realPartitions) Partitions.Add(p);
            });

            OnPropertyChanged(nameof(HasNoPartitions));
            Context.OperationProgress = 100;
            if (Partitions.Count > 0)
            {
                Context.CurrentOperation = $"Tabel Partisi Nyata Dimuat ({Partitions.Count} Partisi)";
                AddLog($"[Partition Manager] Sukses memuat {Partitions.Count} partisi fisik dari perangkat.");
            }
            else
            {
                Context.CurrentOperation = "Gagal Membaca Partisi (Perangkat Offline / Tidak Merespons)";
                AddLog("[Partition Manager] Tidak ada partisi yang terbaca. Pastikan mode koneksi hardware sesuai dan izin otorisasi aktif.");
            }
        }
    }
}
