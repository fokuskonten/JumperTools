using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace JumperTools.UI.ViewModels.Shared
{
    public abstract class TabViewModelBase : INotifyPropertyChanged
    {
        protected readonly ISharedWorkspaceContext Context;

        public event PropertyChangedEventHandler? PropertyChanged;

        protected TabViewModelBase(ISharedWorkspaceContext context)
        {
            Context = context;
        }

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected void AddLog(string message) => Context.AddLog(message);
    }
}
