using System;
using System.Collections.Generic;
using System.Reflection;
using System.Windows;
using System.Windows.Navigation;
using ui = ModernWpf.Controls;


namespace fluid
{
    public enum NaviIcon
    {
        Event,
        Roster,
        Settings,
        Library,
        About,
        None,
    }
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        //NaviIcon 列挙体の値をキーとし、それに対応するページの型 (Type) を値とする、
        //読み取り専用の辞書フィールド _pages を定義し、初期化している
        private static IReadOnlyDictionary<NaviIcon, Type> _pages = new Dictionary<NaviIcon, Type>()
        {
            // 新しいページが増えたら追加
            {NaviIcon.Event, typeof(Pages.EventPage)},
            {NaviIcon.Roster, typeof(Pages.RosterPage)},
            {NaviIcon.Settings, typeof(Pages.BlankPage)},
            {NaviIcon.Library, typeof(Pages.BlankPage)},
            {NaviIcon.About, typeof(Pages.aboutPage)},
            {NaviIcon.None, typeof(Pages.BlankPage)},
        };
        public MainWindow()
        {
            InitializeComponent();

            // バージョン情報を取得
            string version = Assembly.GetExecutingAssembly().GetName().Version.ToString();

            // ウィンドウタイトルに設定
            this.Title = $"fluid - {version}";
            ShowFirstTimeWindow();
        }
        //naviview選択時の関数
        //引数：senderにはNavigationView コントロールが渡され、args 引数にはその選択変更に関する情報が渡されます
        private void NaviView_SelectionChanged(ui.NavigationView sender, ui.NavigationViewSelectionChangedEventArgs args)
        {
            try
            {
                //選択項目をselectedItemに格納。型を定義してる
                var selectedItem = (ui.NavigationViewItem)args.SelectedItem;
                // Tag取得し文字列としてiconNameに代入TagがNULLでないならTagを文字列にして代入
                string iconName = selectedItem.Tag?.ToString();
                string header = selectedItem.Content?.ToString();
                // ヘッダー設定
                sender.Header = header;

                if (Enum.TryParse(iconName, out NaviIcon icon))
                {
                    // 対応するページ表示
                    ContentFrame.Navigate(_pages[icon]);
                }
                else
                {
                    // 空ページ表示
                    ContentFrame.Navigate(_pages[NaviIcon.None]);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }

        }

        private void ContentFrame_Navigated(object sender, NavigationEventArgs e)
        {

        }
        private void ShowFirstTimeWindow()
        {
            if (Properties.Settings.Default.IsFirstTime)
            {
                FirstTimeWindow firstTimeWindow = new FirstTimeWindow();
                firstTimeWindow.Show();

                Properties.Settings.Default.IsFirstTime = false;
                Properties.Settings.Default.Save();
            }
        }
    }
}
