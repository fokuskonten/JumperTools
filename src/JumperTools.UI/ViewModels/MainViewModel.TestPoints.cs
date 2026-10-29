using System;
using System.Linq;
using System.Threading.Tasks;
using JumperTools.Models;

namespace JumperTools.UI.ViewModels
{
    public partial class MainViewModel
    {
        // ==================== TEST POINT & PINOUT OPERATIONS ====================

        public void SearchTestPoint(string? query)
        {
            var results = _testPointRepo.Search(query ?? "");
            TestPoints.Clear();
            foreach (var item in results)
            {
                TestPoints.Add(item);
            }

            if (TestPoints.Count > 0)
            {
                if (SelectedTestPoint == null || !TestPoints.Contains(SelectedTestPoint))
                {
                    SelectedTestPoint = TestPoints[0];
                }
            }
            else
            {
                SelectedTestPoint = null;
            }
        }

        public async Task SyncTestPointsAsync()
        {
            AddLog("[Cloud Sync] Memeriksa pembaruan diagram Test Point dari JumperTools Server...");
            try
            {
                var cloudItems = await _apiClient.GetTestPointsAsync();
                if (cloudItems.Count > 0)
                {
                    AddLog($"[Cloud Sync] Mengunduh {cloudItems.Count} diagram baru dari server.");
                    SearchTestPoint("");
                }
                else
                {
                    AddLog("[Cloud Sync] Database lokal sudah mutakhir (Up to Date).");
                }
                LastSyncTime = $"{DateTime.Now:HH:mm:ss}";
            }
            catch (Exception ex)
            {
                AddLog($"[Cloud Sync] Info: {ex.Message}");
                LastSyncTime = $"{DateTime.Now:HH:mm:ss}";
            }
        }

        public void OpenTestPointFolder()
        {
            try
            {
                var baseDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "database", "testpoint");
                if (!System.IO.Directory.Exists(baseDir)) System.IO.Directory.CreateDirectory(baseDir);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = baseDir,
                    UseShellExecute = true
                });
                AddLog($"[Test Point Hub] Membuka direktori penyimpanan skematik: {baseDir}");
            }
            catch (Exception ex)
            {
                AddLog($"[Folder Error] {ex.Message}");
            }
        }
    }
}
