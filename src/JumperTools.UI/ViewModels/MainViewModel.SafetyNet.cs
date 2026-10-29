using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using JumperTools.Core;
using JumperTools.Models;

namespace JumperTools.UI.ViewModels
{
    public partial class MainViewModel
    {
        // SafetyNetEngine disambungkan ke FastbootEngine melalui SetFastbootEngine() di MainViewModel constructor
        private SafetyNetEngine _safetyNetEngine = new SafetyNetEngine();


        public ObservableCollection<SafetySnapshotInfo> SafetySnapshots { get; set; } = new ObservableCollection<SafetySnapshotInfo>();

        private SafetySnapshotInfo? _selectedSafetySnapshot;
        public SafetySnapshotInfo? SelectedSafetySnapshot
        {
            get => _selectedSafetySnapshot;
            set { _selectedSafetySnapshot = value; OnPropertyChanged(); }
        }

        private bool _isSafetyNetAutoBackupEnabled = true;
        public bool IsSafetyNetAutoBackupEnabled
        {
            get => _isSafetyNetAutoBackupEnabled;
            set { _isSafetyNetAutoBackupEnabled = value; OnPropertyChanged(); }
        }

        public ICommand RefreshSnapshotsCommand => new AsyncRelayCommand(RefreshSnapshotsAsync);
        public ICommand RollbackSelectedSnapshotCommand => new AsyncRelayCommand(RollbackSelectedSnapshotAsync);
        public ICommand CreateManualSnapshotCommand => new AsyncRelayCommand(CreateManualSnapshotAsync);

        public async Task RefreshSnapshotsAsync()
        {
            await Task.Run(() =>
            {
                var list = _safetyNetEngine.GetAllSnapshots();
                App.Current?.Dispatcher?.Invoke(() =>
                {
                    SafetySnapshots.Clear();
                    foreach (var s in list) SafetySnapshots.Add(s);
                    if (SafetySnapshots.Count > 0 && SelectedSafetySnapshot == null)
                        SelectedSafetySnapshot = SafetySnapshots[0];
                });
            });
            AddLog($"[Safety Net] Berhasil memuat {SafetySnapshots.Count} riwayat snapshot cadangan.");
        }

        public async Task RollbackSelectedSnapshotAsync()
        {
            if (SelectedSafetySnapshot == null)
            {
                AddLog("[Safety Net] Pilih snapshot yang ingin dipulihkan dari tabel.");
                return;
            }

            var port = SelectedDevice?.DeviceId ?? "DEVICE_PORT";
            AddLog($"[Safety Net] ⚡ Memulai 1-Click Rollback untuk '{SelectedSafetySnapshot.Brand} {SelectedSafetySnapshot.ModelName}'...");
            CurrentOperation = $"Memulihkan Snapshot {SelectedSafetySnapshot.SnapshotId}...";
            OperationProgress = 20;

            try
            {
                var ok = await _safetyNetEngine.RollbackSnapshotAsync(SelectedSafetySnapshot, port);
                OperationProgress = 100;
                if (ok)
                {
                    CurrentOperation = "Rollback Snapshot Selesai 100%";
                    AddLog($"[Safety Net] ✅ Sukses memulihkan snapshot '{SelectedSafetySnapshot.SnapshotId}'!");
                }
            }
            catch (Exception ex)
            {
                AddLog($"[Safety Net Error] {ex.Message}");
            }
        }

        public async Task CreateManualSnapshotAsync()
        {
            var brand = SelectedPhoneModel?.Brand ?? SelectedDevice?.Brand ?? "Generic";
            var model = SelectedPhoneModel?.ModelName ?? SelectedDevice?.Model ?? "Android";
            var chip = SelectedPhoneModel?.Chipset ?? "MTK/QCOM";
            var arch = SelectedPhoneModel?.Architecture ?? "MediaTek";

            AddLog($"[Safety Net] Membuat snapshot manual untuk {brand} {model}...");
            CurrentOperation = "Membuat Pre-Flight Snapshot...";
            OperationProgress = 30;

            try
            {
                var snap = await _safetyNetEngine.CreatePreFlightSnapshotAsync(brand, model, chip, "MANUAL_SERIAL", arch);
                OperationProgress = 100;
                CurrentOperation = "Snapshot Berhasil Dibuat";
                await RefreshSnapshotsAsync();
                AddLog($"[Safety Net] ✅ Snapshot manual berhasil dibuat ({snap.TotalSizeFormatted}).");
            }
            catch (Exception ex)
            {
                AddLog($"[Safety Net Error] {ex.Message}");
            }
        }
    }
}
