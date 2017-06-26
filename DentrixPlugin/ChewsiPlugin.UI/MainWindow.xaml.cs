using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ChewsiPlugin.UI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void PaymentsDataGrid_OnPreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!(e.OriginalSource is Button))
            {
                e.Handled = true;
            }
        }
    }
}
