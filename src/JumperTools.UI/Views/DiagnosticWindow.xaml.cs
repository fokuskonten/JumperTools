using System.Windows;
using JumperTools.UI.ViewModels;

namespace JumperTools.UI.Views
{
    public partial class DiagnosticWindow : Window
    {
        public DiagnosticWindow(DiagnosticViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
