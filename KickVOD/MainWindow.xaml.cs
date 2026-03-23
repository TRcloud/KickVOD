using System;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using Microsoft.Win32; // OpenFolderDialog (available in .NET 8 WPF)
using KickVOD.Services;
using System.Threading.Tasks;
using System.Diagnostics;

namespace KickVOD
{
    public partial class MainWindow : Window
    {
        private readonly VODDownloader _downloader;
        private VideoMetadata _currentMetadata;
        private int _downloadCounter = 1;

        public MainWindow()
        {
            InitializeComponent();
            _downloader = new VODDownloader();
            _downloader.OnProgressChanged += Downloader_OnProgressChanged;
            _downloader.OnStatusMessageChanged += Downloader_OnStatusMessageChanged;
            _downloader.OnError += Downloader_OnError;
            _downloader.OnDownloadCompleted += Downloader_OnDownloadCompleted;
            UrlTextBox.GotFocus += UrlTextBox_GotFocus;
            QualityComboBox.Items.Add("En İyi");
            QualityComboBox.SelectedIndex = 0;
            this.Loaded += MainWindow_Loaded;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                DownloadButton.IsEnabled = false;
                UrlTextBox.IsEnabled = false;
                StatusTextBlock.Text = "Araçlar kontrol ediliyor...";
                await _downloader.EnsureDependenciesExistAsync();
                StatusTextBlock.Text = "Kullanıma hazır.";
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Başlangıç Hatası", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusTextBlock.Text = "Araçlar eksik veya hatalı.";
            }
            finally
            {
                DownloadButton.IsEnabled = true;
                UrlTextBox.IsEnabled = true;
            }
        }

        private void UrlTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(UrlTextBox.Text) && Clipboard.ContainsText())
            {
                string clipText = Clipboard.GetText();
                if (clipText.Contains("kick.com/"))
                {
                    UrlTextBox.Text = clipText;
                    UrlTextBox.Select(UrlTextBox.Text.Length, 0);
                }
            }
        }

        private void PasteButton_Click(object sender, RoutedEventArgs e)
        {
            if (Clipboard.ContainsText())
            {
                UrlTextBox.Text = Clipboard.GetText();
                UrlTextBox.Focus();
                UrlTextBox.Select(UrlTextBox.Text.Length, 0);
            }
        }

        private void UrlTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            // Placeholder (Watermark) gizle/göster mekanizması
            if (UrlPlaceholder != null)
            {
                UrlPlaceholder.Visibility = string.IsNullOrEmpty(UrlTextBox.Text) 
                    ? Visibility.Visible 
                    : Visibility.Collapsed;
            }

            string text = UrlTextBox.Text.Trim();
            if (!string.IsNullOrEmpty(text))
            {
                if (!text.Contains("kick.com/"))
                {
                    UrlTextBox.Foreground = System.Windows.Media.Brushes.Red;
                    if (StatusTextBlock != null)
                    {
                        StatusTextBlock.Text = "Lütfen geçerli bir Kick linki girin!";
                        StatusTextBlock.Foreground = System.Windows.Media.Brushes.Red;
                    }
                }
                else
                {
                    UrlTextBox.Foreground = System.Windows.Media.Brushes.White;
                    if (StatusTextBlock != null && StatusTextBlock.Text == "Lütfen geçerli bir Kick linki girin!")
                    {
                        StatusTextBlock.Text = "Bekleniyor...";
                        StatusTextBlock.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#A0A0A0"));
                    }
                }
            }
            else
            {
                UrlTextBox.Foreground = System.Windows.Media.Brushes.White;
                if (StatusTextBlock != null)
                {
                    StatusTextBlock.Text = "Bekleniyor...";
                    StatusTextBlock.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#A0A0A0"));
                }
            }
        }

        private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            _downloader?.CancelDownload();
            Application.Current.Shutdown();
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private async void PreviewButton_Click(object sender, RoutedEventArgs e)
        {
            await LoadPreviewAsync();
        }

        private async Task LoadPreviewAsync()
        {
            string url = UrlTextBox.Text.Trim();
            if (string.IsNullOrEmpty(url)) return;

            try
            {
                StatusTextBlock.Text = "Video bilgileri alınıyor...";

                _currentMetadata = await _downloader.FetchMetadataAsync(url);

                PreviewTitle.Text = _currentMetadata.Title;
                PreviewChannel.Text = _currentMetadata.ChannelName;

                if (!string.IsNullOrEmpty(_currentMetadata.ThumbnailUrl))
                {
                    BitmapImage bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(_currentMetadata.ThumbnailUrl);
                    bitmap.EndInit();
                    PreviewImage.Source = bitmap;
                }

                StatusTextBlock.Text = _currentMetadata.IsClip ? "Klip kopyalanmaya hazır." : "VOD kopyalanmaya hazır.";

                string baseName = _currentMetadata.IsClip ? "KLİP" : "YAYIN";
                FileNameTextBox.Text = $"{baseName}_{_downloadCounter++}";

                if (_currentMetadata.Duration > TimeSpan.Zero)
                {
                    EndTimeTextBox.Text = _currentMetadata.Duration.ToString(@"hh\:mm\:ss");
                }
                else
                {
                     EndTimeTextBox.Text = "00:00:00";
                }
                StartTimeTextBox.Text = "00:00:00";

                QualityComboBox.Items.Clear();
                foreach (var q in _currentMetadata.AvailableQualities)
                {
                    QualityComboBox.Items.Add(q);
                }
                if (QualityComboBox.Items.Count > 0)
                {
                    QualityComboBox.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Önizleme Hatası", MessageBoxButton.OK, MessageBoxImage.Warning);
                StatusTextBlock.Text = "Bekleniyor...";
            }
        }

        private void StartTimeUp_Click(object sender, RoutedEventArgs e)
        {
            AdjustTime(StartTimeTextBox, 1);
        }

        private void StartTimeDown_Click(object sender, RoutedEventArgs e)
        {
            AdjustTime(StartTimeTextBox, -1);
        }

        private void EndTimeUp_Click(object sender, RoutedEventArgs e)
        {
            AdjustTime(EndTimeTextBox, 1);
        }

        private void EndTimeDown_Click(object sender, RoutedEventArgs e)
        {
            AdjustTime(EndTimeTextBox, -1);
        }

        private void AdjustTime(System.Windows.Controls.TextBox textBox, int incrementMinutes)
        {
            if (string.IsNullOrWhiteSpace(textBox.Text)) textBox.Text = "00:00:00";

            if (TimeSpan.TryParse(textBox.Text, out TimeSpan currentTime))
            {
                TimeSpan newTime = currentTime.Add(TimeSpan.FromMinutes(incrementMinutes));
                if (newTime < TimeSpan.Zero) newTime = TimeSpan.Zero;

                if (textBox == EndTimeTextBox && _currentMetadata != null && _currentMetadata.Duration > TimeSpan.Zero)
                {
                    if (newTime > _currentMetadata.Duration) newTime = _currentMetadata.Duration;
                }
                else if (textBox == StartTimeTextBox && _currentMetadata != null && _currentMetadata.Duration > TimeSpan.Zero)
                {
                    if (newTime >= _currentMetadata.Duration) newTime = _currentMetadata.Duration.Subtract(TimeSpan.FromSeconds(1));
                    if (newTime < TimeSpan.Zero) newTime = TimeSpan.Zero;
                }

                textBox.Text = newTime.ToString(@"hh\:mm\:ss");
            }
        }

        private void BrowseFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog();
            if (dialog.ShowDialog() == true)
            {
                FolderTextBox.Text = dialog.FolderName;
            }
        }

        private async void DownloadButton_Click(object sender, RoutedEventArgs e)
        {
            string url = UrlTextBox.Text.Trim();
            string folder = FolderTextBox.Text.Trim();
            string filename = FileNameTextBox.Text.Trim();
            string st = StartTimeTextBox.Text.Trim();
            string et = EndTimeTextBox.Text.Trim();

            if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(folder) || string.IsNullOrEmpty(filename))
            {
                MessageBox.Show("Lütfen tüm alanları doldurun (Link, Klasör, Ad).", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Load metadata before downloading if not already loaded
            if (_currentMetadata == null)
            {
                await LoadPreviewAsync();
                if (_currentMetadata == null) return; // If fetch failed
            }

            DownloadButton.IsEnabled = false;
            CancelButton.IsEnabled = true;
            ProgressValueTextBlock.Text = "0%";
            SpeedTextBlock.Text = "HZ: -";
            EtaTextBlock.Text = "Kalan: -";

            string quality = QualityComboBox.SelectedItem?.ToString() ?? "En İyi";

            await _downloader.StartDownloadAsync(_currentMetadata, folder, filename, st, et, quality);
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            _downloader.CancelDownload();
            ResetUI();
        }

        private void Downloader_OnProgressChanged(object sender, DownloadProgressInfo info)
        {
            Dispatcher.Invoke(() =>
            {
                ProgressValueTextBlock.Text = $"{info.Percentage:F1}%";
                SpeedTextBlock.Text = $"HZ: {info.Speed}";
                EtaTextBlock.Text = $"⏱: {info.ETA}";
            });
        }

        private void Downloader_OnStatusMessageChanged(object sender, string message)
        {
            Dispatcher.Invoke(() => { StatusTextBlock.Text = message; });
        }

        private void Downloader_OnError(object sender, Exception ex)
        {
            Dispatcher.Invoke(() =>
            {
                MessageBox.Show(ex.Message, "Hata Oluştu", MessageBoxButton.OK, MessageBoxImage.Error);
                ResetUI();
                StatusTextBlock.Text = "Hata oluştu/İptal edildi.";
                SpeedTextBlock.Text = "-";
                EtaTextBlock.Text = "-";
            });
        }

        private void Downloader_OnDownloadCompleted(object sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                // Save history
                try
                {
                    if (_currentMetadata != null)
                    {
                        string historyFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "history.json");
                        System.Collections.Generic.List<DownloadHistoryItem> history = new System.Collections.Generic.List<DownloadHistoryItem>();

                        if (File.Exists(historyFile))
                        {
                            string existingJson = File.ReadAllText(historyFile);
                            if (!string.IsNullOrWhiteSpace(existingJson))
                            {
                                history = System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.List<DownloadHistoryItem>>(existingJson) ?? new System.Collections.Generic.List<DownloadHistoryItem>();
                            }
                        }

                        history.Add(new DownloadHistoryItem {
                            Title = _currentMetadata.Title,
                            Date = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                            Url = _currentMetadata.OriginalUrl
                        });

                        File.WriteAllText(historyFile, System.Text.Json.JsonSerializer.Serialize(history, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
                    }
                }
                catch { } // Ignore history save errors

                var result = MessageBox.Show("İndirme başarıyla tamamlandı! İndirilen klasörü açmak ister misiniz?", "Başarılı", MessageBoxButton.YesNo, MessageBoxImage.Information);
                if (result == MessageBoxResult.Yes)
                {
                    try { Process.Start("explorer.exe", FolderTextBox.Text); } catch { }
                }

                ResetUI();
                ProgressValueTextBlock.Text = "100%";
                StatusTextBlock.Text = "Tamamlandı.";
                SpeedTextBlock.Text = "-";
                EtaTextBlock.Text = "-";
            });
        }

        private void ResetUI()
        {
            DownloadButton.IsEnabled = true;
            CancelButton.IsEnabled = false;
            _currentMetadata = null;
        }

        private void DiscordButton_Click(object sender, RoutedEventArgs e)
        {
            OpenUrl("https://discord.gg/eEwShe3dqf");
        }

        private void KickButton_Click(object sender, RoutedEventArgs e)
        {
            OpenUrl("https://kick.com/wailfy");
        }

        private void GithubButton_Click(object sender, RoutedEventArgs e)
        {
            OpenUrl("https://github.com/TRcloud/KickVOD");
        }

        private void OpenUrl(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Web sayfası açılamadı: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void TimeTextBox_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            var tb = sender as System.Windows.Controls.TextBox;
            e.Handled = true;

            if (!char.IsDigit(e.Text, 0)) return;

            string text = tb.Text;
            int caret = tb.CaretIndex;

            if (caret == 2 || caret == 5) {
                caret++;
                tb.CaretIndex = caret;
            }

            if (caret < 8) {
                char[] chars = text.ToCharArray();
                chars[caret] = e.Text[0];
                tb.Text = new string(chars);
                tb.CaretIndex = caret + 1;
            }
        }

        private void TimeTextBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            var tb = sender as System.Windows.Controls.TextBox;
            if (e.Key == System.Windows.Input.Key.Back)
            {
                e.Handled = true;
                int caret = tb.CaretIndex;
                if (caret > 0)
                {
                    caret--;
                    if (caret == 2 || caret == 5) caret--;

                    char[] chars = tb.Text.ToCharArray();
                    chars[caret] = '0';
                    tb.Text = new string(chars);
                    tb.CaretIndex = caret;
                }
            }
            else if (e.Key == System.Windows.Input.Key.Delete)
            {
                e.Handled = true;
                int caret = tb.CaretIndex;
                if (caret < 8)
                {
                    if (caret == 2 || caret == 5) caret++;
                    if (caret < 8)
                    {
                        char[] chars = tb.Text.ToCharArray();
                        chars[caret] = '0';
                        tb.Text = new string(chars);
                        tb.CaretIndex = caret + 1;
                    }
                }
            }
            else if (e.Key == System.Windows.Input.Key.Space)
            {
                e.Handled = true;
            }
        }
            private async void UpdateToolsButton_Click(object sender, RoutedEventArgs e)
            {
                try
                {
                    StatusTextBlock.Text = "Araçlar güncelleniyor, lütfen bekleyin...";
                    SpeedTextBlock.Text = "";
                    EtaTextBlock.Text = "";

                    await Task.Run(async () =>
                    {
                        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                        string ytDlpPath = Path.Combine(baseDir, "yt-dlp.exe");

                        if (File.Exists(ytDlpPath))
                        {
                            var processInfo = new ProcessStartInfo
                            {
                                FileName = ytDlpPath,
                                Arguments = "-U",
                                UseShellExecute = false,
                                CreateNoWindow = true,
                                RedirectStandardOutput = true,
                                RedirectStandardError = true
                            };

                            using var process = Process.Start(processInfo);
                            if (process != null)
                            {
                                string output = await process.StandardOutput.ReadToEndAsync();
                                await process.WaitForExitAsync();
                            }
                        }
                    });

                    MessageBox.Show("Araçlar (yt-dlp) başarıyla güncellendi.", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
                    StatusTextBlock.Text = "Araçlar güncellendi.";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Araçlar güncellenirken hata oluştu: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                    StatusTextBlock.Text = "Kullanıma hazır.";
                }
            }

            private void LoadHistory()
            {
                try
                {
                    string historyFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "history.json");
                    bool hasItems = false;

                    if (File.Exists(historyFile))
                    {
                        string existingJson = File.ReadAllText(historyFile);
                        var history = System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.List<DownloadHistoryItem>>(existingJson);
                        if (history != null && history.Count > 0) {
                            history.Reverse(); // En yeni indirilen en üstte
                            HistoryListBox.ItemsSource = history;
                            hasItems = true;
                        }
                    }

                    if (!hasItems)
                    {
                        HistoryListBox.ItemsSource = null;
                        if (EmptyHistoryText != null) EmptyHistoryText.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        if (EmptyHistoryText != null) EmptyHistoryText.Visibility = Visibility.Collapsed;
                    }
                }
                catch { }
            }

            private void ClearHistoryButton_Click(object sender, RoutedEventArgs e)
            {
                try
                {
                    string historyFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "history.json");
                    if (File.Exists(historyFile))
                    {
                        File.Delete(historyFile);
                    }
                    HistoryListBox.ItemsSource = null;
                    if (EmptyHistoryText != null) EmptyHistoryText.Visibility = Visibility.Visible;
                    MessageBox.Show("Geçmiş temizlendi.", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch { }
            }

            private void TabDownloader_Checked(object sender, RoutedEventArgs e)
            {
                if (DownloaderView != null) DownloaderView.Visibility = Visibility.Visible;
                if (HistoryView != null) HistoryView.Visibility = Visibility.Collapsed;
                if (GuideView != null) GuideView.Visibility = Visibility.Collapsed;
            }

            private void TabHistory_Checked(object sender, RoutedEventArgs e)
            {
                if (DownloaderView != null) DownloaderView.Visibility = Visibility.Collapsed;
                if (GuideView != null) GuideView.Visibility = Visibility.Collapsed;
                if (HistoryView != null)
                {
                    HistoryView.Visibility = Visibility.Visible;
                    LoadHistory();
                }
            }

            private void TabGuide_Checked(object sender, RoutedEventArgs e)
            {
                if (DownloaderView != null) DownloaderView.Visibility = Visibility.Collapsed;
                if (HistoryView != null) HistoryView.Visibility = Visibility.Collapsed;
                if (GuideView != null) GuideView.Visibility = Visibility.Visible;
            }
        }
    }

    public class DownloadHistoryItem
    {
        public string Title { get; set; }
        public string Date { get; set; }
        public string Url { get; set; }
    }