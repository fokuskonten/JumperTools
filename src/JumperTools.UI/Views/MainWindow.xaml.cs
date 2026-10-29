using System;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using JumperTools.UI.ViewModels;

namespace JumperTools.UI.Views
{
    public partial class MainWindow : Window
    {
        private const int WM_DEVICECHANGE = 0x0219;
        private const int DBT_DEVICEARRIVAL = 0x8000;
        private const int DBT_DEVICEREMOVECOMPLETE = 0x8004;

        private static readonly string PlacementFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config", "window_placement.json");
        private bool _isLoaded = false;
        private bool _isRestoring = true;

        public MainWindow()
        {
            _isRestoring = true;
            InitializeComponent();
            RestoreWindowPlacement();
            DataContext = new MainViewModel();

            Loaded += (s, e) =>
            {
                RestoreWindowPlacement();
                _isLoaded = true;
                _isRestoring = false;
            };

            LocationChanged += (s, e) => { if (_isLoaded && !_isRestoring && WindowState == WindowState.Normal) SaveWindowPlacement(); };
            SizeChanged += (s, e) => { if (_isLoaded && !_isRestoring && WindowState == WindowState.Normal) SaveWindowPlacement(); };
            StateChanged += (s, e) => { if (_isLoaded && !_isRestoring) SaveWindowPlacement(); };
            Closing += (s, e) => 
            { 
                if (_isLoaded) SaveWindowPlacement(); 
                if (DataContext is MainViewModel vm)
                {
                    vm.CancelCurrentOperation();
                }
                KillAllOrphanProcesses();
            };
        }

        private static void KillAllOrphanProcesses()
        {
            try
            {
                var processes = new[] { "python", "mtk", "fastboot", "edl" };
                foreach (var p in processes)
                {
                    foreach (var proc in System.Diagnostics.Process.GetProcessesByName(p))
                    {
                        try { proc.Kill(true); } catch { }
                    }
                }
            }
            catch { }
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            RestoreWindowPlacement();
            var hwndSource = PresentationSource.FromVisual(this) as HwndSource;
            hwndSource?.AddHook(WndProc);
        }

        private void RestoreWindowPlacement()
        {
            try
            {
                _isRestoring = true;
                if (File.Exists(PlacementFilePath))
                {
                    var json = File.ReadAllText(PlacementFilePath);
                    var settings = JsonSerializer.Deserialize<WindowPlacementSettings>(json);
                    if (settings != null && settings.Width > 0 && settings.Height > 0)
                    {
                        WindowStartupLocation = WindowStartupLocation.Manual;
                        Left = settings.Left;
                        Top = settings.Top;
                        Width = settings.Width;
                        Height = settings.Height;

                        if (settings.WindowState == WindowState.Maximized)
                        {
                            WindowState = WindowState.Maximized;
                        }
                    }
                }
            }
            catch { }
            finally
            {
                _isRestoring = false;
            }
        }

        private void SaveWindowPlacement()
        {
            try
            {
                var dir = Path.GetDirectoryName(PlacementFilePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                double curWidth = (WindowState == WindowState.Normal) ? ActualWidth : RestoreBounds.Width;
                double curHeight = (WindowState == WindowState.Normal) ? ActualHeight : RestoreBounds.Height;
                double curLeft = (WindowState == WindowState.Normal) ? Left : RestoreBounds.Left;
                double curTop = (WindowState == WindowState.Normal) ? Top : RestoreBounds.Top;

                if (double.IsNaN(curWidth) || curWidth <= 0) curWidth = ActualWidth > 0 ? ActualWidth : Width;
                if (double.IsNaN(curHeight) || curHeight <= 0) curHeight = ActualHeight > 0 ? ActualHeight : Height;
                if (double.IsNaN(curLeft)) curLeft = Left;
                if (double.IsNaN(curTop)) curTop = Top;

                var settings = new WindowPlacementSettings
                {
                    Left = curLeft,
                    Top = curTop,
                    Width = curWidth,
                    Height = curHeight,
                    WindowState = WindowState
                };

                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(PlacementFilePath, json);
            }
            catch { }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_DEVICECHANGE)
            {
                var eventType = wParam.ToInt32();
                if (eventType == DBT_DEVICEARRIVAL || eventType == DBT_DEVICEREMOVECOMPLETE)
                {
                    // Trigger instant < 50ms USB scan without waiting for background polling timer
                    if (DataContext is MainViewModel vm)
                    {
                        _ = vm.TriggerInstantUsbScanAsync();
                    }
                }
            }
            return IntPtr.Zero;
        }

        private void ConsoleTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (sender is System.Windows.Controls.TextBox tb)
            {
                tb.CaretIndex = tb.Text.Length;
                tb.ScrollToEnd();
            }
        }
    }

    public class WindowPlacementSettings
    {
        public double Left { get; set; }
        public double Top { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public WindowState WindowState { get; set; }
    }
}
