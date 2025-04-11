using DocumentFormat.OpenXml.Wordprocessing;
using ModernWpf.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
using Windows.UI.Xaml.Controls;

namespace fluid.Pages
{ 
    public partial class RosterDialog : ModernWpf.Controls.ContentDialog
    {
        public string rostername { get; private set; }
        public int namenum { get; private set; }
        public int snnum { get; private set; }
        public int rnnum { get; private set; }
        public int kananum { get; private set; }
        public int gendernum { get; private set; }
        public int departnum { get; private set; }
        public int yearnum { get; private set; }
        public RosterDialog(string rostername, int nameCol, int snCol, int rnCol, int kanaCol, int genderCol, int departCol, int yearCol)
        {
            InitializeComponent();

            RosterNameTextBox.Text = rostername;
            NameNum.Value = nameCol;
            SNNum.Value = snCol;
            RNNum.Value = rnCol;
            KanaNum.Value = kanaCol;
            GenderNum.Value = genderCol;
            DepartNum.Value = departCol;
            YearNum.Value = yearCol;
        }
        private void ContentDialog_PrimaryButtonClick(ModernWpf.Controls.ContentDialog sender, ModernWpf.Controls.ContentDialogButtonClickEventArgs args)
        {
            // バリデーション: イベント名と開催日が入力されているか確認
            if (string.IsNullOrWhiteSpace(RosterNameTextBox.Text) || NameNum.Value <= 0 || SNNum.Value <= 0 || RNNum.Value <= 0 || KanaNum.Value <= 0 || GenderNum.Value <= 0 || DepartNum.Value <= 0 || YearNum.Value <= 0)
            {
                // 入力が不完全な場合はアラートを表示
                MessageBox.Show("すべてのフィールドに記入してください。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);

                // イベントをキャンセルしてダイアログを閉じないようにする
                args.Cancel = true;
            }
            else
            {
                // 入力されたイベント名と開催日を取得
                rostername = RosterNameTextBox.Text;
                namenum = (int)NameNum.Value;
                snnum = (int)SNNum.Value;
                rnnum = (int)RNNum.Value;
                kananum = (int)KanaNum.Value;
                gendernum = (int)GenderNum.Value;
                departnum = (int)DepartNum.Value;
                yearnum = (int)YearNum.Value;

            }

        }
    }
}
