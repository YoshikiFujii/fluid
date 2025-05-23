﻿using fluid.Pages;
using Microsoft.VisualBasic; // StrConv を使用するため
using ModernWpf.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Media;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Xml.Linq;
using WpfAnimatedGif;

namespace fluid
{
    /// <summary>
    /// Window1.xaml の相互作用ロジック
    /// </summary>

    public class RosterItem : INotifyPropertyChanged
    {
        public string RoomNumber { get; set; }
        public string Name { get; set; }
        public string Kana { get; set; }
        public string StudentNumber { get; set; }
        public string Gender { get; set; }
        public string Department { get; set; }
        public string Year { get; set; }

        private bool isRegistered;
        public bool IsRegistered
        {
            get { return isRegistered; }
            set
            {
                isRegistered = value;
                OnPropertyChanged(nameof(IsRegistered));
            }
        }

        private bool isNotRegistered;
        public bool IsNotRegistered
        {
            get { return isNotRegistered; }
            set
            {
                isNotRegistered = value;
                OnPropertyChanged(nameof(IsNotRegistered));
            }
        }

        private bool isAbsent;
        public bool IsAbsent
        {
            get { return isAbsent; }
            set
            {
                isAbsent = value;
                OnPropertyChanged(nameof(IsAbsent));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
    public class SettingItem
    {
        public string SoundSetting { get; set; }
        public bool SameStudentErrorSoundSetting { get; set; }
    }
    public partial class EventWindow : Window
    {
        public ObservableCollection<RosterItem> RosterItems { get; set; } = new ObservableCollection<RosterItem>();
        private SettingItem Settings { get; set; } = new SettingItem();
        private string LogFolderPath = System.IO.Path.Combine(Environment.CurrentDirectory, "log");
        private string currentEvent;
        private string eventFilePath;

        private int TotalParticipants;//progressnumberのそうす
        private int DoneParticipants;//progressnumberの値
        private int FirstTotalParticipants;//1年の総数
        private int FirstParticipants;//progressnumberの1年の値
        private int SecondTotalParticipants;//2年の総数
        private int SecondParticipants;//progressnumberの2年の値

        private bool isDialogOpen = false;

        private int ConnectTryCount = 0;
        private const string QueryMessage = "cntfluid";
        private const string ExpectedResponse = "hithere!";
        private const int TimeoutMilliseconds = 2000; // タイムアウト時間（ミリ秒）
        private BitmapImage image_waiting;
        private BitmapImage image_serching;
        private Dictionary<string, BitmapImage> preloadGifs = new Dictionary<string, BitmapImage>();
        private int RandomSoundCount = 0;
        private System.Media.SoundPlayer player = null;
        SerialPort connectedPort = null;
        private bool AbsentErrorSettings = true;//欠席登録者が認証したときの設定 true:拒否 false:認証
        private SoundPlayer Sound1 = null;
        private SoundPlayer Sound2 = null;
        private SoundPlayer Sound3 = null;
        private SoundPlayer Sound4 = null;
        private SoundPlayer Sound5 = null;

        public EventWindow(Event selectedEvent)
        {
            InitializeComponent();

            // バージョン情報を取得
            string version = Assembly.GetExecutingAssembly().GetName().Version.ToString();

            // ウィンドウタイトルに設定
            this.Title = $"fluid - {version} - EventWindow";

            currentEvent = selectedEvent.EventName;
            eventFilePath = System.IO.Path.Combine("data", $"{currentEvent}.xml");
            EventHeader.Text = currentEvent;
            EventInfoHeader.Text = selectedEvent.EventDate.ToString();
            DataContext = this;

            LoadEvent();
            LoadGifAsync();
            if (!System.IO.Directory.Exists(LogFolderPath))
            {
                Directory.CreateDirectory(LogFolderPath);
            }
        }
        private async void LoadGifAsync()
        {
            await Task.Run(() =>
            {
                Dispatcher.Invoke(() =>
                {
                    var gifPaths = new Dictionary<string, string>
                    {
                        { "waiting", "Resources/waiting.gif" },
                        { "searching", "Resources/searching.gif" }
                    };
                    foreach (var gif in gifPaths)
                    {
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.UriSource = new Uri(gif.Value, UriKind.Relative);
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.EndInit();
                        bitmap.Freeze(); // スレッドセーフにするためにフリーズ
                        preloadGifs[gif.Key] = bitmap;
                    }
                });

            });
        }
        //音声ファイルの読み込み
        private void Window_ContentRendered(object sender, EventArgs e)
        {
            string sound1 = "j_1.wav";
            string sound2 = "j_2.wav";
            string sound3 = "j_3.wav";
            string sound4 = "Gate_BEEP.wav";
            string sound5 = "Gate_Alert.wav";

            string path1 = System.IO.Path.Combine("Resources", "Sound", sound1);
            string path2 = System.IO.Path.Combine("Resources", "Sound", sound2);
            string path3 = System.IO.Path.Combine("Resources", "Sound", sound3);
            string path4 = System.IO.Path.Combine("Resources", "Sound", sound4);
            string path5 = System.IO.Path.Combine("Resources", "Sound", sound5);

            Sound1 = new SoundPlayer(path1);
            Sound2 = new SoundPlayer(path2);
            Sound3 = new SoundPlayer(path3);
            Sound4 = new SoundPlayer(path4);
            Sound5 = new SoundPlayer(path5);
        }
        public async void AddTerminal(object sender, RoutedEventArgs e)
        {
            if (connectedPort != null && connectedPort.IsOpen)
            {
                UnconnectTerminal();
                return;
            }
            AddTerminalButton.IsEnabled = false; // ボタンを無効にする
            await GetCOMPort();     // 非同期でCOMポートの検索を行う
            AddTerminalButton.IsEnabled = true;  // 処理が終わったらボタンを再び有効にする
            if (connectedPort != null)
            {
                // 接続が完了したらデータを受信し続ける
                await ReceiveDataContinuously(connectedPort);
            }
        }
        public async void UnconnectTerminal()
        {
            if (isDialogOpen) return;
            if (connectedPort != null && connectedPort.IsOpen)
            {
                connectedPort.Write("111111111"); // 端末に終了コマンドを送信
                connectedPort.Close();
                connectedPort.Dispose();
                connectedPort = null;
                ShutdownButton.Visibility = Visibility.Collapsed;
                AddTerminalButtonIcon.Glyph = "\uE710";
                AddTerminalButton.ToolTip = "認証端末を接続する";
                StatusAnimation.Visibility = Visibility.Collapsed;
                SubStatusText.Text = "";
                CertificationLabel.Content = "";
                CertificationLabel2.Content = "";
                CertificationRectangle.Fill = new SolidColorBrush(Color.FromRgb(222, 222, 222));

                isDialogOpen = true;
                ContentDialog DisconnectDialog = new ContentDialog
                {
                    Title = "接続解除",
                    Content = "認証端末を接続解除しました。再接続するには接続ボタンを押してください。",
                    CloseButtonText = "閉じる"
                };

                await DisconnectDialog.ShowAsync();
            }
            else 
            {
                if (connectedPort != null && connectedPort.IsOpen)
                {
                    connectedPort.Close();
                    connectedPort.Dispose();
                    connectedPort = null;
                }
                ShutdownButton.Visibility = Visibility.Collapsed;
                AddTerminalButtonIcon.Glyph = "\uE710";
                AddTerminalButton.ToolTip = "認証端末を接続する";
                StatusAnimation.Visibility = Visibility.Collapsed;
                SubStatusText.Text = "";
                CertificationLabel.Content = "";
                CertificationLabel2.Content = "";
                CertificationRectangle.Fill = new SolidColorBrush(Color.FromRgb(222, 222, 222));

                isDialogOpen = true;
                ContentDialog ForceDisconnectDialog = new ContentDialog
                {
                    Title = "接続解除",
                    Content = "認証端末からの接続が切れました。再接続するには接続ボタンを押してください。",
                    CloseButtonText = "閉じる"
                };

                await ForceDisconnectDialog.ShowAsync();
            }
            isDialogOpen = false;
        }
        public async void ShutdownTerminal(object sender, RoutedEventArgs e)
        {
            if (connectedPort != null && connectedPort.IsOpen)
            {
                connectedPort.Write("222222222"); //shutdown signal
                connectedPort.Close();
                connectedPort.Dispose();
                connectedPort = null;
                ShutdownButton.Visibility = Visibility.Collapsed;
                AddTerminalButtonIcon.Glyph = "\uE710";
                AddTerminalButton.ToolTip = "認証端末を接続する";
                StatusAnimation.Visibility = Visibility.Collapsed;
                SubStatusText.Text = "";
                CertificationLabel.Content = "";
                CertificationLabel2.Content = "";
                CertificationRectangle.Fill = new SolidColorBrush(Color.FromRgb(222, 222, 222));
            }
            ContentDialog ResultDialog = new ContentDialog
            {
                Title = "シャットダウン",
                Content = "認証端末をシャットダウンしました。再接続する場合はUSBを刺しなおしてください。",
                CloseButtonText = "閉じる"
            };

            await ResultDialog.ShowAsync();
        }

        public async Task GetCOMPort()
        {
            ImageBehavior.SetAnimatedSource(StatusAnimation, null); // GIFをリセット
            await Task.Delay(100); // 短い遅延を入れて完全にリセット
            // アニメーションの設定
            Dispatcher.Invoke(() =>
            {
                if (preloadGifs.TryGetValue("searching", out var searchingGif))
                {
                    ImageBehavior.SetAnimatedSource(StatusAnimation, searchingGif);
                    StatusAnimation.Visibility = Visibility.Visible;
                }
            });
            MessageLabel.Content = "";
            SubStatusText.Text = "端末を検索中　USBタイプの認証端末を接続してください";
            StringBuilder responseBuilder = new StringBuilder();
            int retire = 0;

            await Task.Run(async () =>
            {
                while (retire < 5)
                {
                    string[] ports = SerialPort.GetPortNames();
                    foreach (string portName in ports)
                    {
                        try
                        {
                            // SerialPort オブジェクトを作成
                            connectedPort = new SerialPort(portName)
                            {
                                // ポートの設定を行う（ボーレート、パリティ、データビットなど）
                                BaudRate = 115200,
                                Parity = Parity.None,
                                DataBits = 8,
                                StopBits = StopBits.One,
                                Handshake = Handshake.None,
                                ReadTimeout = TimeoutMilliseconds,
                                WriteTimeout = TimeoutMilliseconds,
                            };
                            await Task.Delay(500);
                            // ポートをオープン
                            connectedPort.Open();

                            try
                            {
                                // メッセージを書き込む
                                connectedPort.Write(QueryMessage);

                                // バッファを作成
                                char[] buffer = new char[8];
                                int readCount = 0;
                                int attempts = 0;
                                int maxAttempts = 10; // 最大試行回数を設定（例: 10回）

                                // データが来るまで待機しながらループ
                                while (readCount < 8 && attempts < maxAttempts)
                                {
                                    if (connectedPort.BytesToRead > 0)
                                    {
                                        // 読み込む文字数を調整（最大8文字）
                                        int bytesToRead = Math.Min(connectedPort.BytesToRead, 8 - readCount);

                                        // 読み込んだ文字をバッファに追加
                                        readCount += connectedPort.Read(buffer, readCount, bytesToRead);
                                    }
                                    else
                                    {
                                        // データがまだ来ていない場合、短時間待機
                                        await Task.Delay(100); // 100ミリ秒待機
                                        attempts++; // 試行回数を増加
                                    }
                                }

                                // バッファの内容を文字列に変換
                                responseBuilder.Append(buffer);

                                if (responseBuilder.ToString().EndsWith(ExpectedResponse))
                                {
                                    retire = 5;

                                    // 成功時のアニメーション変更
                                    Dispatcher.Invoke(() =>
                                    {
                                        SubStatusText.Text = "端末接続完了";
                                        StatusAnimation.Visibility = Visibility.Visible;
                                        AddTerminalButtonIcon.Glyph = "\uE711";
                                        AddTerminalButton.ToolTip = "接続解除";
                                        ShutdownButton.Visibility = Visibility.Visible;
                                        Dispatcher.Invoke(() =>
                                        {
                                            if (preloadGifs.TryGetValue("waiting", out var waitingGif))
                                            {
                                                ImageBehavior.SetAnimatedSource(StatusAnimation, waitingGif);
                                            }
                                        });

                                    });

                                    return;
                                }
                                else
                                {
                                    Console.WriteLine("レスポンスが一致しません。");
                                }
                            }
                            catch (TimeoutException)
                            {
                                // タイムアウトが発生した場合は次のポートへ進む
                                Console.WriteLine($"ポート {portName} でタイムアウトが発生しました。次のポートに進みます。");
                            }

                        }
                        catch (Exception ex)
                        {
                            // 例外をキャッチしてログに出力
                            Console.WriteLine($"ポート {portName} でエラーが発生: {ex.Message}");
                            await Task.Delay(500);
                        }
                    }
                    retire++;
                }

                // 全てのポートを試した後、接続が失敗した場合の処理
                Dispatcher.Invoke(() =>
                {
                    FadeOutElement(StatusAnimation, 1.0); // 1秒でフェードアウト
                    MessageLabel.Content = "端末が見つかりませんでした。"; // メッセージを表示
                    SubStatusText.Text = "端末接続後起動に３０秒程度かかります。少し待ってから再度検索してください。";
                    ConnectTryCount++;
                    if (Properties.Settings.Default.IsFirstTimeConnectError == true & ConnectTryCount == 2)
                    {
                        MessageBox.Show("ドライバのインストールが完了していない可能性があります。端末を接続した状態でWindowsアップデートを実行してみてください。");
                        Properties.Settings.Default.IsFirstTimeConnectError = false;
                        Properties.Settings.Default.Save();
                    }
                });
            });
        }
        //#####################################サウンド#########################################
        //-------------タッチサウンド--------------------
        private void TouchSound()
        {
            int count = RandomSoundCount % 5;

            if (Settings.SoundSetting == "JR")
            {
                PlaySound(Sound4);
            }

            if (Settings.SoundSetting == "JUGGLER")
            {
                if (count == 0)
                {
                    PlaySound(Sound1);
                    RandomSoundCount++;
                }
                else if (count == 1 || count == 2 || count == 3)
                {
                    PlaySound(Sound2);
                    RandomSoundCount++;
                }
                else
                {   // Randomオブジェクトの作成
                    Random random = new Random();
                    int num = random.Next(1, 5);
                    Console.WriteLine($"RandomCount is {num}");
                    if (num == 1)
                    {
                        PlaySound(Sound3);
                        RandomSoundCount = 0;
                    }
                    else
                    {
                        PlaySound(Sound1);
                        RandomSoundCount = 1;
                    }
                }
            }

        }
        //---------------エラーサウンド---------------------
        private void ErrorSound()
        {
            PlaySound(Sound5);
        }
        //--------２回以上認証したときのサウンド------
        private void SameStudentErrorSound()
        {
            if (Settings.SameStudentErrorSoundSetting == true)
            {
                PlaySound(Sound5);
            }
            if (Settings.SameStudentErrorSoundSetting == false)
            {
                PlaySound(Sound4);
            }
        }
        private void AbsentErrorSound()
        {
            if (AbsentErrorSettings == true)
            {
                PlaySound(Sound5);
            }
            if (AbsentErrorSettings == false)
            {
                PlaySound(Sound4);
            }
        }
        private void StopSound()
        {
            if (player != null)
            {
                player.Stop();
                player.Dispose();
                player = null;
            }
        }
        private void PlaySound(SoundPlayer Sound)
        {
            Sound.Play();
        }
        //##########################################################################################


        public async Task ReceiveDataContinuously(SerialPort serialPort)
        {
            try
            {
                DateTime lastPingTime = DateTime.Now;
                connectedPort.Write("PING");
                System.Console.WriteLine("PINGを送信しました。");

                // データを受信し続けるループ
                while (serialPort.IsOpen)
                {
                    if (connectedPort.BytesToRead > 0)
                    {
                        int availableBytes = connectedPort.BytesToRead;
                        byte[] buffer = new byte[availableBytes];

                        // シリアルポートからデータを読み取る
                        int bytesRead = serialPort.Read(buffer, 0, buffer.Length);

                        // 読み取ったバイト列を文字列にデコード（UTF-8を例に使用）
                        string result = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                        Console.WriteLine("Received data: " + result);
                        if (result == "PONG")
                        {
                            Console.WriteLine("PONG を受信しました。");
                            lastPingTime = DateTime.Now;
                            // 3秒後に再度 "ping" を送信
                            await Task.Delay(3000);
                            serialPort.Write("PING");
                            Console.WriteLine("Sent: PING");
                        }
                        else if (result == "000000000")
                        {
                            CertificationLabel.Content = "認証エラー：学生証以外が認識されました";
                            CertificationLabel2.Content = "再度試してください";
                            Console.WriteLine("学生証以外の検出");
                            CertificationBackChange(255, 80, 80);
                            ErrorSound();
                        }
                        else if (result == "E0001")
                        {
                            ContentDialog ScannerErrorDialog = new ContentDialog
                            {
                                Title = "スキャナーエラー",
                                Content = "認証端末に接続されているNFCスキャナが正常に接続されていません。スキャナを接続しなおしてください。",
                                CloseButtonText = "閉じる"
                            };

                            await ScannerErrorDialog.ShowAsync();
                        }
                        else
                        {
                            AuthenticateByNFC(result);
                        }
                    }
                    if ((DateTime.Now - lastPingTime).TotalSeconds>7)
                    {
                        // PINGが5秒以上来ない場合、接続が切れたとみなす
                        Console.WriteLine("接続が切れました。");
                        UnconnectTerminal();
                        break;
                    }
                    // 適宜、短い待機を挟んで無駄なCPU使用率を防ぐ
                    await Task.Delay(100);
                }
                Console.WriteLine("シリアルポートが閉じられました。");
                UnconnectTerminal();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"データ受信中にエラーが発生しました: {ex.Message}");
            }
        }
        private void AuthenticateByNFC(string studentNumber)
        {
            // 末尾2桁を削除
            if (studentNumber.Length > 2)
            {
                studentNumber = studentNumber.Substring(0, studentNumber.Length - 2);
            }
            // 受け取った学籍番号に一致する生徒をRosterItemsから検索
            var student = RosterItems.FirstOrDefault(item => item.StudentNumber == studentNumber);

            if (student != null)
            {
                // すでに参加済みでない場合のみステータスを更新
                if (student.IsNotRegistered)
                {
                    student.IsRegistered = true;
                    student.IsNotRegistered = false;
                    student.IsAbsent = false;

                    // 状態が変更されたので、保存する
                    SaveStatus(student);
                    CertificationLabel.Content = $"{student.Name} さんが参加しました";
                    CertificationLabel2.Content = $"部屋番号:{student.RoomNumber} | 区分:{student.Year} | 学科:{student.Department}";
                    Console.WriteLine($"Student {studentNumber} marked as registered.");
                    TouchSound();
                    WriteLog("参加済み", student);
                    CertificationBackChange(151, 209, 255);
                    UpdateProgressBar(student, "参加済み");
                }
                else if (student.IsAbsent)
                {
                    AbsentErrorSound();
                    CertificationLabel.Content = $"{student.Name} さんは欠席登録者です";
                    CertificationLabel2.Content = $"部屋番号:{student.RoomNumber} | 区分:{student.Year} | 学科:{student.Department}";
                    Console.WriteLine($"Student {studentNumber} is Absent registered.");
                    CertificationBackChange(255, 255, 125);

                    if (AbsentErrorSettings == false)
                    {
                        student.IsRegistered = true;
                        student.IsNotRegistered = false;
                        student.IsAbsent = false;
                        UpdateProgressBar(student, "参加済み");
                    }
                }
                else
                {
                    CertificationLabel.Content = $"{student.Name} さんは参加済みです";
                    CertificationLabel2.Content = $"部屋番号:{student.RoomNumber} | 区分:{student.Year} | 学科:{student.Department}";
                    Console.WriteLine($"Student {studentNumber} is already registered.");
                    SameStudentErrorSound();
                    CertificationBackChange(255, 255, 125);
                }
            }
            else
            {
                // 該当する生徒が見つからない場合の処理
                CertificationLabel.Content = $"ERROR:{studentNumber} は名簿にありません";
                Console.WriteLine($"Student {studentNumber} not found in the roster.");
                CertificationBackChange(255, 80, 80);
            }
        }
        private async void CertificationBackChange(int goalR, int goalG, int goalB)
        {
            int defaultR = 222, defaultG = 222, defaultB = 222;
            double r, g, b;
            double R = goalR, G = goalG, B = goalB;

            SolidColorBrush brush = new SolidColorBrush(Color.FromRgb((byte)R, (byte)G, (byte)B));
            CertificationRectangle.Fill = brush;
            brush.Color = Color.FromRgb((byte)goalR, (byte)goalG, (byte)goalB);

            // 色の変化量を計算
            r = (defaultR - goalR) / 30.00;
            g = (defaultG - goalG) / 30.00;
            b = (defaultB - goalB) / 30.00;

            // 10ステップで色を変更
            for (int i = 0; i < 30; i++)
            {
                R += r;
                G += g;
                B += b;

                // 色を更新
                brush.Color = Color.FromRgb((byte)R, (byte)G, (byte)B);

                // 少し待ってから次のステップへ
                await Task.Delay(10);  // 100ミリ秒待つ
            }
        }

        private void LoadEvent()
        {

            if (!File.Exists(eventFilePath))
            {
                MessageBox.Show($"イベントファイルが見つかりません: {eventFilePath}");
                return;
            }
            // XMLを解析し、RosterItemsにデータを追加
            XDocument eventDoc = XDocument.Load(eventFilePath);
            Settings.SoundSetting = eventDoc.Root.Element("TouchSound").Value;
            // "SameStudentSetting" が存在する場合はその値を使い、存在しない場合は true を設定する
            string sameStudentSettingValue = eventDoc.Root.Element("SameStudentSetting")?.Value;
            Settings.SameStudentErrorSoundSetting = bool.TryParse(sameStudentSettingValue, out bool result) ? result : true;
            UpdateProgressBar();

            // RosterItems の更新
            RefreshRosterList(eventDoc);
            RosterListView.Visibility = Visibility.Visible;
        }
        public void UpdateProgressbarButtonCllick(object sender, RoutedEventArgs e)
        {
            UpdateProgressBar();
        }
        public async void StatusButtonClick(object sender, RoutedEventArgs e)
        {
            var dialog = new StatusDialog(eventFilePath);
             var result = await dialog.ShowAsync();

        }
        public void UpdateProgressBar()
        {
            XDocument eventDoc = XDocument.Load(eventFilePath);
            //ProgressBarの初期化--------------------------------------------------------------
            TotalParticipants = eventDoc.Descendants("Entry")
                                     .Count(e => (string)e.Element("Status") == "参加済み" |
                                                 (string)e.Element("Status") == "未参加");
            FirstTotalParticipants = eventDoc.Descendants("Entry").Count(e => (string)e.Element("Year") == "新");
            SecondTotalParticipants = TotalParticipants - FirstTotalParticipants;

            DoneParticipants = eventDoc.Descendants("Entry").Count(e => (string)e.Element("Status") == "参加済み");
            FirstParticipants = eventDoc.Descendants("Entry")
                                     .Count(e => (string)e.Element("Status") == "参加済み" &&
                                                 (string)e.Element("Year") == "新");
            SecondParticipants = DoneParticipants - FirstParticipants;

            WholeProgressBar.Minimum = 0;
            WholeProgressBar.Maximum = TotalParticipants;
            WholeProgressBar.Value = DoneParticipants;
            FirstProgressBar.Minimum = 0;
            FirstProgressBar.Maximum = FirstTotalParticipants;
            FirstProgressBar.Value = FirstParticipants;
            SecondProgressBar.Minimum = 0;
            SecondProgressBar.Maximum = SecondTotalParticipants;
            SecondProgressBar.Value = SecondParticipants;
            //----------------------------------------------------------------------------------
            
        }
        public void OpenEventFolder(object sender, RoutedEventArgs e)
        {
            try
            {

                // フォルダが存在するか確認
                if (Directory.Exists("data"))
                {
                    // エクスプローラーでログフォルダを開く
                    System.Diagnostics.Process.Start("explorer.exe", "data");
                }
                else
                {
                    // フォルダが存在しない場合はエラーメッセージを表示
                    MessageBox.Show($"ログフォルダが存在しません: data", "エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                // 例外が発生した場合、エラーメッセージを表示
                MessageBox.Show($"ログフォルダを開く際にエラーが発生しました: {ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        public async void aboutButtonClick(object sender, RoutedEventArgs e)
        {
            var dialog = new aboutDialog();
            var result = await dialog.ShowAsync();
        }
        public async void SettingsButtonClick(object sender, RoutedEventArgs e)
        {
            var dialog = new EventSettingsDialog(eventFilePath, Settings);
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
                Settings.SoundSetting = dialog.SelectedSoundSetting;
            Settings.SameStudentErrorSoundSetting = dialog.SameStudentErrorEnabled;
            SaveSettings(Settings);
        }
        public void WriteLog(string status, RosterItem rosterItem)
        {

            string logFile = $"{currentEvent}.txt"; // ログファイルのパス
            string logFilePath = System.IO.Path.Combine(LogFolderPath, logFile);
            string currentTime = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"); // 現在の時間
            string logEntry = $"{currentTime}, {rosterItem.RoomNumber}, {rosterItem.Name}, {status}\n";

            // ログファイルに追記
            LogList.AppendText($"[{currentTime}]    {status}    {rosterItem.RoomNumber}     {rosterItem.Name}  \r\n");
            LogList.ScrollToEnd();
            File.AppendAllText(logFilePath, logEntry);
        }

        // ラジオボタンが選択されたときの処理
        private void RadioButton_Checked(object sender, RoutedEventArgs e)
        {
            var radioButton = sender as RadioButton;
            var selectedRosterItem = radioButton.DataContext as RosterItem;

            if (selectedRosterItem == null) return;

            string NewStatus = "";

            // 状態に応じてステータスを更新
            if (radioButton.Content.ToString() == "参加済み")
            {
                selectedRosterItem.IsRegistered = true;
                selectedRosterItem.IsNotRegistered = false;
                selectedRosterItem.IsAbsent = false;
                NewStatus = "参加済み";
            }
            else if (radioButton.Content.ToString() == "未参加")
            {
                selectedRosterItem.IsRegistered = false;
                selectedRosterItem.IsNotRegistered = true;
                selectedRosterItem.IsAbsent = false;
                NewStatus = "未参加";
            }
            else if (radioButton.Content.ToString() == "不参加")
            {
                selectedRosterItem.IsRegistered = false;
                selectedRosterItem.IsNotRegistered = false;
                selectedRosterItem.IsAbsent = true;
                NewStatus = "不参加";
            }

            // 更新されたステータスを進捗バーに反映
            UpdateProgressBar(selectedRosterItem, NewStatus);

            // ログに書き込む
            WriteLog(NewStatus, selectedRosterItem);

            // 変更を保存するロジックをここに追加
            SaveStatus(selectedRosterItem);
        }
        private void UpdateProgressBar(RosterItem student, string currentStatus)
        {

            if (currentStatus == "不参加")
            {
                WholeProgressBar.Maximum--;
                if (student.Year == "新")
                    FirstProgressBar.Maximum--;
                if (student.Year == "在")
                    SecondProgressBar.Maximum--;
            }
            if (currentStatus == "参加済み")
            {
                WholeProgressBar.Value++;
                if (student.Year == "新")
                    FirstProgressBar.Value++;
                if (student.Year == "在")
                    SecondProgressBar.Value++;
            }
            else
            {
                WholeProgressBar.Value--;
                if (student.Year == "新")
                    FirstProgressBar.Value--;
                if (student.Year == "在")
                    SecondProgressBar.Value--;
            }
        }
        // 選択された状態を保存する
        //設定をイベントファイルに保存する
        private void SaveSettings(SettingItem settings)
        {
            XDocument eventDoc;

            if (File.Exists(eventFilePath))
            {
                eventDoc = XDocument.Load(eventFilePath);
            }
            else
            {
                MessageBox.Show("イベントファイルが存在しません。");
                return;
            }

            eventDoc.Root.Element("TouchSound").Value = settings.SoundSetting;
            eventDoc.Root.Element("SameStudentSetting").Value = settings.SameStudentErrorSoundSetting ? "true" : "false";
            eventDoc.Save(eventFilePath);
        }
        private void SaveStatus(RosterItem rosterItem)
        {
            XDocument eventDoc;

            if (File.Exists(eventFilePath))
            {
                eventDoc = XDocument.Load(eventFilePath);
            }
            else
            {
                MessageBox.Show("イベントファイルが存在しません。");
                return;
            }

            var entryElement = eventDoc.Descendants("Entry")
        .FirstOrDefault(x => x.Element("RoomNumber")?.Value == rosterItem.RoomNumber);

            if (entryElement != null)
            {
                // Statusを更新
                entryElement.Element("Status").Value = rosterItem.IsRegistered ? "参加済み" :
                                                       rosterItem.IsNotRegistered ? "未参加" : "不参加";
            }
            else
            {
                MessageBox.Show($"部屋番号 {rosterItem.RoomNumber} の参加者が見つかりません。");
                return;
            }
            // イベントファイルに保存
            eventDoc.Save(eventFilePath);
        }
        // テキストボックスでの入力に基づいてリストをフィルタリング
        private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            FilterRosterList();
        }
        private void FilterList(object sender, RoutedEventArgs e)
        {
            // 選択されているフィルター条件を取得
            bool showRegistered = Show_Registerd.IsChecked == true;
            bool showNotRegistered = Show_NotRegisterd.IsChecked == true;
            bool showAbsent = Show_Absent.IsChecked == true;

            // 絞り込んだ結果を作成
            var filteredItems = RosterItems.Where(item =>
                (showRegistered && item.IsRegistered) ||
                (showNotRegistered && item.IsNotRegistered) ||
                (showAbsent && item.IsAbsent)).ToList();

            // 絞り込んだリストを `ListView` に適用
            RosterListView.ItemsSource = filteredItems;

            // 結果が空の場合は非表示
            RosterListView.Visibility = filteredItems.Any() ? Visibility.Visible : Visibility.Collapsed;
        }


        // リストのフィルタリング処理
        private void FilterRosterList()
        {
            string searchRoomNumber = SearchByRNBox.Text.ToLower();
            string searchName = SearchByNameBox.Text.ToLower();

            var filteredList = RosterItems.Where(item =>
                (string.IsNullOrEmpty(searchRoomNumber) || item.RoomNumber.ToLower().Contains(searchRoomNumber)) &&
                (string.IsNullOrEmpty(searchName) || item.Name.ToLower().Contains(searchName) || ConvertToHiragana(item.Kana.ToLower()).Contains(searchName))
            ).ToList();

            RosterListView.ItemsSource = filteredList;
        }
        //半角カナを全角かなに変換する関数-----------------------------------------------------------
        static string ConvertToHiragana(string input)
        {
            // (1) 半角カタカナを全角カタカナに変換
            string fullWidthKatakana = Strings.StrConv(input, VbStrConv.Wide, 0x0411);

            // (2) 全角カタカナをひらがなに変換
            return KatakanaToHiragana(fullWidthKatakana);
        }

        static string KatakanaToHiragana(string input)
        {
            StringBuilder sb = new StringBuilder();
            foreach (char c in input)
            {
                if (c >= 0x30A0 && c <= 0x30FF) // カタカナ範囲
                {
                    sb.Append((char)(c - 0x60)); // ひらがなへ変換
                }
                else
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }
        //--------------------------------------------------------------------------------------------

        // フェードアウトの処理を関数化
        private void FadeOutElement(UIElement element, double durationInSeconds)
        {
            var fadeOutAnimation = new DoubleAnimation
            {
                From = 1.0, // 現在の不透明度（完全表示）
                To = 0.0, // 最終的な不透明度（完全に非表示）
                Duration = TimeSpan.FromSeconds(durationInSeconds), // フェードアウトにかかる時間
                FillBehavior = FillBehavior.Stop // アニメーション終了後に状態を保持しない
            };

            fadeOutAnimation.Completed += (s, e) =>
            {
                // フェードアウト完了後に要素を非表示
                element.Visibility = Visibility.Hidden;
            };

            // アニメーションを開始
            element.BeginAnimation(UIElement.OpacityProperty, fadeOutAnimation);
        }
        private void CloseWindowButtonClick(object sender, RoutedEventArgs e)
        {
            ClosingProcess();
        }
        private void ClosingProcess()
        {
            try
            {
                if (connectedPort != null && connectedPort.IsOpen)
                {
                    connectedPort.Write("11111111"); // 端末に終了コマンドを送信
                    connectedPort.Close(); // ポートを閉じる
                }
            }
            catch (Exception ex)
            {
                // エラーが発生した場合、ログを表示または処理
                MessageBox.Show($"エラーが発生しました: {ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                if (Application.Current.MainWindow != null)
                {
                    // メインウィンドウが非表示の場合は再表示
                    Application.Current.MainWindow.Show();
                }
                else
                {
                    // メインウィンドウが存在しない場合は新規作成
                    MainWindow mainWindow = new MainWindow();
                    Application.Current.MainWindow = mainWindow;
                    mainWindow.Show();

                }
                this.Close();
            }
        }
        void EventWindow_Closing(object sender, CancelEventArgs e)
        {
            try
            {
                if (connectedPort != null && connectedPort.IsOpen)
                {
                    connectedPort.Write("11111111"); // 端末に終了コマンドを送信
                    connectedPort.Close(); // ポートを閉じる
                }
            }
            catch (Exception ex)
            {
                // エラーが発生した場合、ログを表示または処理
                MessageBox.Show($"エラーが発生しました: {ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                if (Application.Current.MainWindow != null)
                {
                    // メインウィンドウが非表示の場合は再表示
                    Application.Current.MainWindow.Show();
                }
                else
                {
                    // メインウィンドウが存在しない場合は新規作成
                    MainWindow mainWindow = new MainWindow();
                    Application.Current.MainWindow = mainWindow;
                    mainWindow.Show();
                }
            }
        }
        private void ClearLog(object sender, RoutedEventArgs e)
        {
            LogList.Text = "";
        }
        private async void DeleteLogFile(object sender, RoutedEventArgs e)
        {
            ContentDialog deleteLogDialog = new ContentDialog
            {
                Title = "ログの削除",
                Content = "本当にログを削除しますか？この操作は取り消せません。",
                PrimaryButtonText = "削除",
                CloseButtonText = "キャンセル"
            };

            // ユーザーの選択を待つ
            ContentDialogResult result = await deleteLogDialog.ShowAsync();

            // ユーザーが「削除」を選択した場合にのみログをクリア
            if (result == ContentDialogResult.Primary)
            {
                try
                {
                    // ログファイルのパスを取得
                    string logFile = $"{currentEvent}.txt"; // ログファイルの名前
                    string logFilePath = System.IO.Path.Combine(LogFolderPath, logFile);

                    // ファイルが存在する場合、内容をクリア
                    if (File.Exists(logFilePath))
                    {
                        File.WriteAllText(logFilePath, string.Empty); // ファイルの中身を空にする
                    }
                    else
                    {
                        MessageBox.Show($"ログファイルが存在しません: {logFilePath}", "エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
                catch (Exception ex)
                {
                    // エラーが発生した場合、ユーザーに通知
                    MessageBox.Show($"ログのクリア中にエラーが発生しました: {ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        private void OpenLogFile(object sender, RoutedEventArgs e)
        {
            try
            {
                string logFile = $"{currentEvent}.txt";
                string logFilePath = System.IO.Path.Combine(LogFolderPath, logFile);

                if (File.Exists(logFilePath))
                {
                    System.Diagnostics.Process.Start("notepad.exe", logFilePath);
                }
                else
                {
                    MessageBox.Show($"ログファイルが存在しません: {logFilePath}", "エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"ログファイルの展開時にエラーが発生しました: {ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void OpenLogFolder(object sender, RoutedEventArgs e)
        {
            try
            {
                // ログフォルダのパスを取得
                string logFolderPath = LogFolderPath;

                // フォルダが存在するか確認
                if (Directory.Exists(logFolderPath))
                {
                    // エクスプローラーでログフォルダを開く
                    System.Diagnostics.Process.Start("explorer.exe", logFolderPath);
                }
                else
                {
                    // フォルダが存在しない場合はエラーメッセージを表示
                    MessageBox.Show($"ログフォルダが存在しません: {logFolderPath}", "エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                // 例外が発生した場合、エラーメッセージを表示
                MessageBox.Show($"ログフォルダを開く際にエラーが発生しました: {ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private async void ImportButtonClick(object sender, RoutedEventArgs e)
        {
            var dialog = new ImportDialog(currentEvent);
            var result = await dialog.ShowAsync();
            LoadEvent();
        }
        private async void ExportButtonClick(object sender, RoutedEventArgs e)
        {
            var dialog = new ExportDialog(currentEvent);
            var result = await dialog.ShowAsync();
        }
        public void RefreshRosterList(XDocument eventDoc)
        {
            try
            {
                RosterItems.Clear();
                foreach (var entryElement in eventDoc.Descendants("Entry").Skip(1))
                {
                    RosterItems.Add(CreateRosterItem(entryElement));
                }
                RosterListView.ItemsSource = null;
                RosterListView.ItemsSource = RosterItems;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"リストの更新中にエラーが発生しました: {ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private RosterItem CreateRosterItem(XElement entryElement)
        {
            string roomNumber = entryElement.Element("RoomNumber")?.Value ?? "部屋番号情報なし";
            string studentNumber = entryElement.Element("StudentNumber")?.Value ?? "学籍番号情報なし";
            string name = entryElement.Element("Name")?.Value ?? "名前情報なし";
            string gender = entryElement.Element("Gender")?.Value ?? "性別情報なし";
            string kana = entryElement.Element("Kana")?.Value ?? "よみがな情報なし";
            string depart = entryElement.Element("Department")?.Value ?? "学科情報なし";
            string year = entryElement.Element("Year")?.Value ?? "学年情報なし";
            string status = entryElement.Element("Status")?.Value ?? "未参加";

            return new RosterItem
            {
                RoomNumber = roomNumber,
                Name = name,
                Kana = kana,
                StudentNumber = studentNumber,
                Gender = gender,
                Department = depart,
                Year = year,
                IsRegistered = status == "参加済み",
                IsNotRegistered = status == "未参加",
                IsAbsent = status == "不参加"
            };
        }

    }
}