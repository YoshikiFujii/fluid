using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using ModernWpf.Controls;
using ui = ModernWpf.Controls;

namespace fluid.Pages
{
    /// <summary>
    /// ExportDialog.xaml の相互作用ロジック
    /// </summary>
    public partial class ExportDialog : ModernWpf.Controls.ContentDialog
    {
        private string CurrentEvent;
        private string eventFilePath;
        public ExportDialog(string currentEvent)
        {
            CurrentEvent = currentEvent;
            eventFilePath = System.IO.Path.Combine("data", $"{currentEvent}.xml");
            InitializeComponent();
        }
        private void OutputListClick(object sender, RoutedEventArgs e)
        {
            ExportListWindow exportListWindow = new ExportListWindow(eventFilePath,CurrentEvent);
            exportListWindow.Show();
        }
        private void OutputPDFClick(object sender, RoutedEventArgs e)
        {

        }
    }
}
