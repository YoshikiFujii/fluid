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

namespace fluid.Pages
{
    /// <summary>
    /// Window1.xaml の相互作用ロジック
    /// </summary>
    public partial class EventSettingsDialog : ModernWpf.Controls.ContentDialog
    {
        public string SelectedSoundSetting { get; private set; }
        public bool SameStudentErrorEnabled { get; set; }
        public EventSettingsDialog(string eventFilePath, SettingItem currentSettings)
        {
            InitializeComponent();

            TouchSoundComboBox.Items.Add("JR");
            TouchSoundComboBox.Items.Add("JUGGLER");

            // 初期値を設定
            if (currentSettings != null) //currentSettingsがnullの場合の対策
            {
                TouchSoundComboBox.SelectedItem = currentSettings.SoundSetting;
                SameStudentErrorEnabled = currentSettings.SameStudentErrorSoundSetting;
                SameStudentErrorCheckBox.IsChecked = SameStudentErrorEnabled; //チェックボックスの状態も設定
            }
            else
            {
                //currentSettingsがnullの場合の初期値
                TouchSoundComboBox.SelectedItem = "JR"; // デフォルト値
                SameStudentErrorEnabled = false;
                SameStudentErrorCheckBox.IsChecked = false;

            }


        }
        private void EventSettingsPrimaryButtonClick(ModernWpf.Controls.ContentDialog sender, ModernWpf.Controls.ContentDialogButtonClickEventArgs args)
        {
            SelectedSoundSetting = TouchSoundComboBox.SelectedItem as string;
            SameStudentErrorEnabled = SameStudentErrorCheckBox.IsChecked == true; //チェックボックスの状態を反映

        }
    }
}
