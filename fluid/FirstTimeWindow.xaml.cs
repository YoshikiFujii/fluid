using System.Windows;

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
