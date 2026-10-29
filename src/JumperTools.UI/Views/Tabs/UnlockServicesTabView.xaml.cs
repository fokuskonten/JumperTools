using System.Windows.Controls;

namespace JumperTools.UI.Views.Tabs
{
    public partial class UnlockServicesTabView : UserControl
    {
        public UnlockServicesTabView()
        {
            InitializeComponent();
        }

        private void ConsoleTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox tb)
            {
                tb.CaretIndex = tb.Text.Length;
                tb.ScrollToEnd();
            }
        }
    }
}
