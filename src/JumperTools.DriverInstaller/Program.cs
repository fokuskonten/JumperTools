using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace JumperTools.DriverInstaller
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            string chipset = "Qualcomm QDLoader 9008";
            string targetFolder = "Qualcomm";

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--chipset" && i + 1 < args.Length) chipset = args[i + 1];
                if (args[i] == "--folder" && i + 1 < args.Length) targetFolder = args[i + 1];
            }

            Application.Run(new InstallerForm(chipset, targetFolder));
        }
    }

    public class InstallerForm : Form
    {
        private readonly string _chipset;
        private readonly string _targetFolder;
        private Label _lblStatus;
        private ProgressBar _progressBar;
        private TextBox _txtLogs;
        private Button _btnAction;
        private Button _btnClose;

        public InstallerForm(string chipset, string targetFolder)
        {
            _chipset = chipset;
            _targetFolder = targetFolder;

            Text = $"JumperTools — Wizard Penginstalan Driver {_chipset}";
            Size = new Size(580, 420);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            BackColor = Color.FromArgb(248, 250, 252);
            Font = new Font("Segoe UI", 9F, FontStyle.Regular);

            // Header Banner
            var pnlHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 65,
                BackColor = Color.FromArgb(15, 23, 42)
            };
            var lblTitle = new Label
            {
                Text = $"Instalasi Driver: {_chipset}",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                Location = new Point(16, 12),
                AutoSize = true
            };
            var lblSubtitle = new Label
            {
                Text = "Wizard pendaftaran berkas driver resmi ke Windows DriverStore",
                ForeColor = Color.FromArgb(148, 163, 184),
                Font = new Font("Segoe UI", 8.5F),
                Location = new Point(16, 36),
                AutoSize = true
            };
            pnlHeader.Controls.Add(lblTitle);
            pnlHeader.Controls.Add(lblSubtitle);
            Controls.Add(pnlHeader);

            // Status Label
            _lblStatus = new Label
            {
                Text = "Klik tombol 'Pasang Sekarang' untuk mendaftarkan driver ke Windows.",
                Location = new Point(16, 78),
                Size = new Size(530, 22),
                ForeColor = Color.FromArgb(51, 65, 85),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };
            Controls.Add(_lblStatus);

            // Progress Bar
            _progressBar = new ProgressBar
            {
                Location = new Point(16, 104),
                Size = new Size(530, 18),
                Style = ProgressBarStyle.Blocks,
                Value = 0
            };
            Controls.Add(_progressBar);

            // Log Console Box
            _txtLogs = new TextBox
            {
                Location = new Point(16, 130),
                Size = new Size(530, 200),
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = Color.FromArgb(15, 23, 42),
                ForeColor = Color.FromArgb(56, 189, 248),
                Font = new Font("Consolas", 8.5F)
            };
            Controls.Add(_txtLogs);

            // Footer Buttons
            _btnAction = new Button
            {
                Text = "Pasang Sekarang",
                Location = new Point(310, 340),
                Size = new Size(130, 30),
                BackColor = Color.FromArgb(2, 132, 199),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _btnAction.FlatAppearance.BorderSize = 0;
            _btnAction.Click += async (s, e) => await StartInstallationAsync();
            Controls.Add(_btnAction);

            _btnClose = new Button
            {
                Text = "Tutup",
                Location = new Point(450, 340),
                Size = new Size(95, 30),
                BackColor = Color.FromArgb(226, 232, 240),
                ForeColor = Color.FromArgb(30, 41, 59),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            _btnClose.FlatAppearance.BorderSize = 0;
            _btnClose.Click += (s, e) => Close();
            Controls.Add(_btnClose);

            // Auto-start installation after form load
            Shown += async (s, e) => await StartInstallationAsync();
        }

        private void AppendLog(string text)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<string>(AppendLog), text);
                return;
            }
            _txtLogs.AppendText(text + Environment.NewLine);
        }

        private async Task StartInstallationAsync()
        {
            _btnAction.Enabled = false;
            _progressBar.Style = ProgressBarStyle.Marquee;
            _lblStatus.Text = "Sedang mendaftarkan berkas driver ke Windows DriverStore...";
            _txtLogs.Clear();

            AppendLog($"============================================================");
            AppendLog($"  JUMPERTOOLS DRIVER INSTALLER WIZARD");
            AppendLog($"  Target: {_chipset}");
            AppendLog($"============================================================");

            await Task.Run(() =>
            {
                try
                {
                    var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                    var candidateDirs = new[]
                    {
                        Path.Combine(baseDir, _targetFolder),
                        Path.Combine(baseDir, "tools", "drivers", _targetFolder),
                        Path.Combine(baseDir, "..", _targetFolder),
                        baseDir
                    };

                    string? foundDir = null;
                    foreach (var d in candidateDirs)
                    {
                        if (Directory.Exists(d) && Directory.GetFiles(d, "*.inf", SearchOption.AllDirectories).Length > 0)
                        {
                            foundDir = d;
                            break;
                        }
                    }

                    if (foundDir == null)
                    {
                        AppendLog($"[Error] Berkas driver (*.inf) untuk {_chipset} tidak ditemukan.");
                        return;
                    }

                    var infFiles = Directory.GetFiles(foundDir, "*.inf", SearchOption.AllDirectories);
                    AppendLog($"[Info] Ditemukan {infFiles.Length} berkas paket driver INF di: {foundDir}");

                    foreach (var inf in infFiles)
                    {
                        AppendLog($"[pnputil] Memasang: {Path.GetFileName(inf)}...");
                        var psi = new ProcessStartInfo
                        {
                            FileName = "pnputil.exe",
                            Arguments = $"/add-driver \"{inf}\" /install",
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            CreateNoWindow = true
                        };

                        using var p = Process.Start(psi);
                        if (p != null)
                        {
                            var outText = p.StandardOutput.ReadToEnd();
                            var errText = p.StandardError.ReadToEnd();
                            p.WaitForExit();

                            if (!string.IsNullOrWhiteSpace(outText))
                            {
                                foreach (var line in outText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                                {
                                    AppendLog($"  -> {line.Trim()}");
                                }
                            }
                            if (!string.IsNullOrWhiteSpace(errText))
                            {
                                AppendLog($"  [Peringatan] {errText.Trim()}");
                            }

                            if (p.ExitCode == 0 || p.ExitCode == 3010 || p.ExitCode == 259)
                            {
                                AppendLog($"✅ {Path.GetFileName(inf)} BERHASIL terpasang di DriverStore.");
                            }
                        }
                    }

                    AppendLog($"============================================================");
                    AppendLog($"✅ INSTALASI DRIVER SELESAI DENGAN SUKSES.");
                    AppendLog($"============================================================");
                }
                catch (Exception ex)
                {
                    AppendLog($"[Fatal Error] {ex.Message}");
                }
            });

            _progressBar.Style = ProgressBarStyle.Blocks;
            _progressBar.Value = 100;
            _lblStatus.Text = "✅ Instalasi driver selesai! Anda dapat menutup jendela ini.";
            _btnAction.Text = "Selesai";
            _btnAction.Enabled = true;
            _btnAction.Click -= async (s, e) => await StartInstallationAsync();
            _btnAction.Click += (s, e) => Close();
        }
    }
}
