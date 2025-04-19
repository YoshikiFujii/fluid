using ClosedXML.Excel;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Windows;
using System.Xml.Linq;
using PdfSharp;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using DocumentFormat.OpenXml.Spreadsheet;
using System.Runtime.InteropServices;
using Spire.Doc;
using Spire.Doc.Documents;
using PdfSharp.Pdf.Content.Objects;

namespace fluid.Pages
{
    /// <summary>
    /// ExportListWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class ExportPDFWindow : Window
    {
        private string CurrentEvent;
        private string eventFilePath;
        private RosterInfo rosterInfo;
        public ExportPDFWindow(string EventFilePath, string currentEvent)
        {
            eventFilePath = EventFilePath;
            CurrentEvent = currentEvent;

            InitializeComponent();
            GetDate();
            
        }
        private void GetDate()
        {
            DateTime dt = DateTime.Now;
            MonthTextBox.Text = dt.Month.ToString();
            DayTextBox.Text = dt.Day.ToString();

        }
        private void ExportPDF_click(object sender, RoutedEventArgs e)
        {
            exportPDF();
        }

        private void exportPDF()
        {
            string wordFilePath = System.IO.Path.Combine("Resources", "減点通知書base.docx");

            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = "PDFファイル(*.pdf)|*.pdf",
                Title = "PDFファイルの保存先を選択してください",
                FileName = $"{CurrentEvent}_減点通知書.pdf"
            };
            if (saveFileDialog.ShowDialog() == true)
            {
                string selectedPath = saveFileDialog.FileName;
                // 一時ファイルのパスを生成
                string tempFilePath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"Temp_{Guid.NewGuid()}.docx");

                try
                {
                    // 元のファイルを一時ファイルにコピー
                    File.Copy(wordFilePath, tempFilePath, true);

                    // Spire.Doc を使用して Word ドキュメントをロード
                    using (Spire.Doc.Document document = new Spire.Doc.Document())
                    {
                        document.LoadFromFile(tempFilePath);

                        // UI要素の存在確認
                        if (HeadTextBlock == null || ChairPersonTextBlock == null || YearTextBox == null ||
                            MonthTextBox == null || DayTextBox == null || PointTextBox == null || ReasonTextBox == null)
                        {
                            MessageBox.Show("必要な入力フィールドが不足しています。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }

                        // プレースホルダーを置き換え
                        document.Replace("{dormitoryhead}", HeadTextBlock.Text, true, true);
                        document.Replace("{chairperson}", ChairPersonTextBlock.Text, true, true);
                        document.Replace("{year}", YearTextBox.Text, true, true);
                        document.Replace("{month}", MonthTextBox.Text, true, true);
                        document.Replace("{day}", DayTextBox.Text, true, true);
                        document.Replace("{point}", PointTextBox.Text, true, true);
                        document.Replace("{reason}", ReasonTextBox.Text, true, true);

                        document.SaveToFile(tempFilePath, FileFormat.Docx);
                    }
                    // XML ファイルを読み込み
                    XDocument eventDoc = XDocument.Load(eventFilePath);
                    var entries = eventDoc.Descendants("Entry").Where(x => x.Element("Status")?.Value == "未参加").ToList();

                    if (entries.Count == 0)
                    {
                        MessageBox.Show("対象となるエントリが見つかりませんでした。", "情報", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }

                    // 個別のPDFを生成して後で結合する方法
                    string tempDir = Path.Combine(Path.GetTempPath(), $"PdfExport_{Guid.NewGuid()}");
                    Directory.CreateDirectory(tempDir);

                    try
                    {
                        List<string> pdfFiles = new List<string>();

                        // 各エントリに対して個別のPDFファイルを生成
                        foreach (var entry in entries)
                        {
                            string name = entry.Element("Name")?.Value ?? "";
                            string roomNumber = entry.Element("RoomNumber")?.Value ?? "";
                            string tempPdfPath = Path.Combine(tempDir, $"temp_{pdfFiles.Count}.pdf");

                            using (Spire.Doc.Document document = new Spire.Doc.Document())
                            {
                                document.LoadFromFile(tempFilePath);

                                document.Replace("{roomnumber}", roomNumber, true, true);
                                document.Replace("{name}", name, true, true);

                                document.SaveToFile(tempPdfPath, FileFormat.PDF);
                            }

                            pdfFiles.Add(tempPdfPath);
                        }

                        // PDFSharpを使用して複数のPDFを結合
                        if (pdfFiles.Count > 0)
                        {
                            using (PdfDocument outputDocument = new PdfDocument())
                            {
                                foreach (string file in pdfFiles)
                                {
                                    using (PdfDocument inputDocument = PdfReader.Open(file, PdfDocumentOpenMode.Import))
                                    {
                                        for (int i = 0; i < inputDocument.PageCount; i++)
                                        {
                                            outputDocument.AddPage(inputDocument.Pages[i]);
                                        }
                                    }
                                }

                                outputDocument.Save(selectedPath);
                            }

                            MessageBox.Show($"PDFが正常に保存されました: {selectedPath}", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"PDFの結合中にエラーが発生しました: {ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    finally
                    {
                        // 一時ファイルとディレクトリの削除
                        try
                        {
                            if (Directory.Exists(tempDir))
                            {
                                Directory.Delete(tempDir, true);
                            }
                        }
                        catch
                        {
                            // 削除に失敗しても処理を継続
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"エラーが発生しました: {ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}
