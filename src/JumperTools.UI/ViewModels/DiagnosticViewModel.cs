using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using JumperTools.Core;
using JumperTools.Models;

namespace JumperTools.UI.ViewModels
{
    public class DiagnosticViewModel : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
    {
        private readonly HardwareDiagnosticEngine _diagnosticEngine;
        private string _diagnosticLog = string.Empty;
        private bool _isTesting;

        public DiagnosticViewModel(HardwareDiagnosticEngine diagnosticEngine)
        {
            _diagnosticEngine = diagnosticEngine;
            _diagnosticEngine.OnLog += msg =>
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    DiagnosticLog += msg + "\n";
                });
            };

            TestResults = new ObservableCollection<HardwareTestResult>();
            RunAllTestsCommand = new AsyncRelayCommand(RunAllTestsAsync);
        }

        public ObservableCollection<HardwareTestResult> TestResults { get; }

        public string DiagnosticLog
        {
            get => _diagnosticLog;
            set => SetProperty(ref _diagnosticLog, value);
        }

        public bool IsTesting
        {
            get => _isTesting;
            set => SetProperty(ref _isTesting, value);
        }

        public ICommand RunAllTestsCommand { get; }

        private async Task RunAllTestsAsync()
        {
            if (IsTesting) return;
            IsTesting = true;
            DiagnosticLog = "";
            TestResults.Clear();

            try
            {
                using var cts = new CancellationTokenSource(30000); // 30 seconds timeout
                var results = await _diagnosticEngine.RunAllSafeTestsAsync(cts.Token);
                
                foreach (var result in results)
                {
                    TestResults.Add(result);
                }
            }
            finally
            {
                IsTesting = false;
            }
        }
    }
}
