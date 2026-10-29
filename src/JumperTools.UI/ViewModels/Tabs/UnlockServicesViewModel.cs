using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using JumperTools.Core;
using JumperTools.Models;
using JumperTools.UI.ViewModels.Shared;

namespace JumperTools.UI.ViewModels.Tabs
{
    public class UnlockServicesViewModel : TabViewModelBase
    {
        public ObservableCollection<Device> Devices => Context.Devices;
        public Device? SelectedDevice
        {
            get => Context.SelectedDevice;
            set => Context.SelectedDevice = value;
        }

        public string TerminalLogText => Context.TerminalLogText;
        public int OperationProgress => Context.OperationProgress;
        public string CurrentOperation => Context.CurrentOperation;

        public ICommand RefreshDevicesCommand { get; }
        public ICommand ForceResetUsbPortsCommand { get; }
        public ICommand ShowFeatureGuideCommand { get; }

        public UnlockServicesViewModel(ISharedWorkspaceContext context)
            : base(context)
        {
            RefreshDevicesCommand = new AsyncRelayCommand(Context.TriggerInstantUsbScanAsync);
            ForceResetUsbPortsCommand = new RelayCommand(Context.ForceResetUsbPorts);
            ShowFeatureGuideCommand = new RelayCommand<string>(key => Context.ShowFeatureGuide(key ?? "Tab_UnlockServices"));
        }
    }
}
