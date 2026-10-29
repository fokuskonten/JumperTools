using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using JumperTools.Core;
using JumperTools.Models;
using JumperTools.Services;

namespace JumperTools.UI.ViewModels.Shared
{
    public interface ISharedWorkspaceContext : INotifyPropertyChanged
    {
        // Device State
        Device? SelectedDevice { get; set; }
        ObservableCollection<Device> Devices { get; }
        Task TriggerInstantUsbScanAsync();
        void ForceResetUsbPorts();
        void OpenDeviceManager();

        // Logging
        string TerminalLogText { get; }
        void AddLog(string message);
        void ClearLog();
        void CopyLog();

        // Global Progress & Async Cancellation
        int OperationProgress { get; set; }
        string CurrentOperation { get; set; }
        bool IsBusy { get; }
        void CancelCurrentOperation();
        CancellationToken CancellationToken { get; }

        // Feature Guide Modal
        void ShowFeatureGuide(string guideKey);
        void CloseFeatureGuide();

        // Core Infrastructure & Engines
        string ToolsBasePath { get; }
        string PlatformToolsPath { get; }
        string TempWorkspacePath { get; }
        ToolRegistry ToolRegistry { get; }
        OperationOrchestrator Orchestrator { get; }
        JobJournalService JobJournal { get; }
        ToolExecutionEngine ToolEngine { get; }
        DeviceDetectionEngine DeviceEngine { get; }
    }
}
