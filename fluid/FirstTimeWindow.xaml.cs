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
using System.Windows.Shapes;
using ModernWpf.Controls;
using ui = ModernWpf.Controls;

namespace fluid
{
    /// <summary>
    /// FirstTimeWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class FirstTimeWindow : Window
    {
        public FirstTimeWindow()
        {
            InitializeComponent();
        }
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
    
}
