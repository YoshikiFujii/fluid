using System;
using System.Collections.Generic;
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
using System.Windows.Shapes;
using System.Xml.Linq;
using ClosedXML.Excel;
using Microsoft.Win32;
using ModernWpf.Controls;
using Windows.UI.Xaml.Controls;

namespace fluid.Pages
{
    /// <summary>
    /// ImportDialog.xaml の相互作用ロジック
    /// </summary>
    public partial class ImportDialog : ModernWpf.Controls.ContentDialog
    {
        private string CurrentEvent;
        public bool IsImportSuccessful { get; private set; } // インポート結果を保持するプロパティ

        public ImportDialog(string currentEvent)
        {
            CurrentEvent = currentEvent;
            InitializeComponent();
        }
        private async void ImportAbsentClick(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Excel Files|*.xlsx",
                Title = "欠席リストファイルをインポート"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    string selectedFilePath = openFileDialog.FileName;

                    // 現在のイベントファイルパスを取得
                    string dataFolder = "data";
                    string currentEventFilePath = System.IO.Path.Combine(dataFolder, $"{CurrentEvent}.xml");

                    if (!Directory.Exists(dataFolder))
                    {
                        MessageBox.Show("データフォルダが見つかりません。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    if (!File.Exists(currentEventFilePath))
                    {
                        MessageBox.Show("現在のイベントファイルが見つかりません。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    // 欠席者リストを取得
                    var absentList = new HashSet<string>();
                    using (var workbook = new XLWorkbook(selectedFilePath))
                    {
                        var worksheet = workbook.Worksheets.First();
                        foreach (var cell in worksheet.Column(1).CellsUsed())
                        {
                            string roomNumber = cell.GetValue<string>().Trim();
                            absentList.Add(roomNumber);
                        }
                    }

                    XDocument xmlDoc = XDocument.Load(currentEventFilePath);
                    var entries = xmlDoc.Descendants("Entry").ToList();
                    var registeredList = new HashSet<string>(); // 不参加登録した生徒を格納

                    int totalEntries = entries.Count;
                    int processedEntries = 0;

                    // プログレスバーを表示
                    progressBar.Visibility = Visibility.Visible;
                    progressBar.Minimum = 0;
                    progressBar.Maximum = totalEntries;
                    progressBar.Value = 0;

                    await Task.Run(() =>
                    {
                        foreach (var entry in entries)
                        {
                            var roomNumberElement = entry.Element("RoomNumber");
                            if (roomNumberElement != null && absentList.Contains(roomNumberElement.Value.Trim()))
                            {
                                entry.Element("Status").Value = "不参加";
                                registeredList.Add(roomNumberElement.Value.Trim());
                            }

                            processedEntries++;

                            // UI スレッドでプログレスバーを更新
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                progressBar.Value = processedEntries;
                            });
                        }
                    });

                    // 名簿に存在しない欠席者リストを取得
                    var missingStudents = absentList.Except(registeredList).ToList();

                    if (missingStudents.Any())
                    {
                        string missingMessage = "以下の部屋番号は名簿に存在しません:\n" + string.Join("\n", missingStudents);
                        MessageBox.Show(missingMessage, "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                    else
                    {
                        xmlDoc.Save(currentEventFilePath);
                        MessageBox.Show("欠席リストの取り込みが完了しました。", "完了", MessageBoxButton.OK, MessageBoxImage.Information);
                    }

                    // インポート完了後にプログレスバーを非表示
                    progressBar.Visibility = Visibility.Collapsed;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"エラーが発生しました: {ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                    progressBar.Visibility = Visibility.Collapsed;
                }
            }
        }

        private async void ImportEventClick(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "XML Files (*.xml)|*.xml",
                Title = "イベントファイルをインポート"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    string selectedFilePath = openFileDialog.FileName;

                    // dataフォルダの確認
                    string dataFolder = "data";
                    if (!Directory.Exists(dataFolder))
                    {
                        MessageBox.Show("データフォルダが見つかりません。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    // 現在のイベントファイルパスを取得
                    string currentEventFilePath = System.IO.Path.Combine(dataFolder, $"{CurrentEvent}.xml");
                    if (!File.Exists(currentEventFilePath))
                    {
                        MessageBox.Show("現在のイベントファイルが見つかりません。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    // XMLファイルを読み込み
                    var sourceDoc = System.Xml.Linq.XDocument.Load(selectedFilePath);
                    var currentDoc = System.Xml.Linq.XDocument.Load(currentEventFilePath);

                    // 出席記録をマージ
                    var sourceEntries = sourceDoc.Descendants("Entry");
                    var currentEntries = currentDoc.Descendants("Entry").ToList();
                    var sameStudentList = new HashSet<string>(); //不参加登録した生徒を格納する

                    int totalEntries = sourceEntries.Count();
                    int processedEntries = 0;

                    // プログレスバーを表示
                    progressBar.Visibility = Visibility.Visible;
                    progressBar.Minimum = 0;
                    progressBar.Maximum = totalEntries;
                    progressBar.Value = 0;

                    await Task.Run(() =>
                    {
                        foreach (var sourceEntry in sourceEntries)
                        {
                            var studentNumberElement = sourceEntry.Element("StudentNumber");
                            var roomNumberElement = sourceEntry.Element("RoomNumber");
                            var sourceStatusElement = sourceEntry.Element("Status");

                            if (studentNumberElement == null || sourceStatusElement == null)
                                continue;

                            string studentNumber = studentNumberElement.Value;
                            string roomNumber = roomNumberElement?.Value?.Trim() ?? "";
                            string sourceStatus = sourceStatusElement.Value;

                            if (string.IsNullOrWhiteSpace(studentNumber) || sourceStatus != "参加済み")
                                continue;

                            // 現在のイベントに同じ学籍番号があるか確認
                            var matchingEntry = currentEntries.FirstOrDefault(entry =>
                                entry.Element("StudentNumber")?.Value == studentNumber);

                            if (matchingEntry != null)
                            {
                                var currentStatusElement = matchingEntry.Element("Status");

                                if (currentStatusElement != null && currentStatusElement.Value == "参加済み")
                                {
                                    // 両方で参加済みの場合、選択ウィンドウを表示
                                    sameStudentList.Add(roomNumber);
                                }

                                // 上書き処理: インポート元のデータを使用
                                currentStatusElement?.SetValue(sourceStatus);
                            }
                            else
                            {
                                // 新規追加
                                currentDoc.Root.Element("Entries")?.Add(sourceEntry);
                            }

                            processedEntries++;

                            // UI スレッドでプログレスバーを更新
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                progressBar.Value = processedEntries;
                            });
                        }
                    });

                    // 重複データの処理
                    if (sameStudentList.Count > 0)
                    {
                        string samestudentMessage = "以下のデータは両方のファイルで「参加済み」です。:\n" + string.Join("\n", sameStudentList);
                        var dialogResult = MessageBox.Show(
                            samestudentMessage,
                            "データの重複",
                            MessageBoxButton.OKCancel,
                            MessageBoxImage.Question
                        );

                        if (dialogResult == MessageBoxResult.Cancel)
                        {
                            MessageBox.Show("インポートをキャンセルしました。", "キャンセル", MessageBoxButton.OK, MessageBoxImage.Information);
                            progressBar.Visibility = Visibility.Collapsed;
                            return;
                        }
                    }

                    // 競合チェック
                    try
                    {
                        using (FileStream fs = new FileStream(currentEventFilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                        {
                            // ファイルが使用中でなければ処理を続行
                        }
                    }
                    catch (IOException)
                    {
                        MessageBox.Show("現在のイベントファイルが使用中です。アプリを閉じてから再試行してください。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                        progressBar.Visibility = Visibility.Collapsed;
                        return;
                    }

                    // マージ結果を保存
                    currentDoc.Save(currentEventFilePath);

                    // インポート完了後にプログレスバーを非表示
                    progressBar.Visibility = Visibility.Collapsed;

                    MessageBox.Show("インポートが完了しました。", "成功", MessageBoxButton.OK, MessageBoxImage.Information);

                    // ダイアログを閉じる
                    this.Hide();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"エラーが発生しました: {ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                    progressBar.Visibility = Visibility.Collapsed;
                }
            }
        }


    }
}
