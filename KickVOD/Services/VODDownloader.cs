using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace KickVOD.Services
{
    public class DownloadProgressInfo
    {
        public double Percentage { get; set; }
        public string Speed { get; set; }
        public string ETA { get; set; }
    }

    public class VideoMetadata
    {
        public string Title { get; set; }
        public string ChannelName { get; set; }
        public string ThumbnailUrl { get; set; }
        public string VideoUrl { get; set; }
        public string OriginalUrl { get; set; }
        public bool IsClip { get; set; }
        public TimeSpan Duration { get; set; } = TimeSpan.Zero;
        public System.Collections.Generic.List<string> AvailableQualities { get; set; } = new System.Collections.Generic.List<string>();
    }

    public class VODDownloader
    {
        public event EventHandler<DownloadProgressInfo> OnProgressChanged;
        public event EventHandler<string> OnStatusMessageChanged;
        public event EventHandler<Exception> OnError;
        public event EventHandler OnDownloadCompleted;

        private Process _downloadProcess;
        private CancellationTokenSource _cancellationTokenSource;
        private TimeSpan _totalDuration = TimeSpan.Zero;
        private readonly HttpClient _httpClient;
        private DateTime _lastUpdate = DateTime.MinValue;
        private double _lastSizeKb = 0;
        private string _lastSpeedText = "Hesaplanıyor...";

        public VODDownloader()
        {
            var handler = new SocketsHttpHandler
            {
                AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate | System.Net.DecompressionMethods.Brotli,
                UseCookies = true,
                PooledConnectionLifetime = TimeSpan.FromMinutes(2)
            };

            _httpClient = new HttpClient(handler)
            {
                DefaultRequestVersion = new Version(2, 0)
            };

            // Kick (Cloudflare) engellemelerini aşmak için daha güncel ve detaylı tarayıcı başlıkları
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36 OPR/108.0.0.0");
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json, text/plain, */*");
            _httpClient.DefaultRequestHeaders.Add("Accept-Language", "tr-TR,tr;q=0.9,en-US;q=0.8,en;q=0.7");
            _httpClient.DefaultRequestHeaders.Add("Sec-Ch-Ua", "\"Chromium\";v=\"122\", \"Not(A:Brand\";v=\"24\", \"Opera GX\";v=\"108\"");
            _httpClient.DefaultRequestHeaders.Add("Sec-Ch-Ua-Mobile", "?0");
            _httpClient.DefaultRequestHeaders.Add("Sec-Ch-Ua-Platform", "\"Windows\"");
            _httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Dest", "empty");
            _httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Mode", "cors");
            _httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Site", "same-origin");
            _httpClient.DefaultRequestHeaders.Add("Origin", "https://kick.com");
            _httpClient.DefaultRequestHeaders.Add("Referer", "https://kick.com/");
        }

        public async Task EnsureDependenciesExistAsync()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string ffmpegPath = Path.Combine(baseDir, "ffmpeg.exe");
            string ytDlpPath = Path.Combine(baseDir, "yt-dlp.exe");

            bool missing = !File.Exists(ytDlpPath) || !File.Exists(ffmpegPath);
            if (missing)
            {
                OnStatusMessageChanged?.Invoke(this, "Eksik araçlar (yt-dlp ve ffmpeg) indiriliyor, bu işlem internet hızınıza bağlı olarak birkaç dakika sürebilir. Lütfen bekleyin...");
                try
                {
                    if (!File.Exists(ytDlpPath))
                    {
                        OnStatusMessageChanged?.Invoke(this, "Aşama 1/2: yt-dlp indiriliyor...");
                        var ytDlpBytes = await _httpClient.GetByteArrayAsync("https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe");
                        await File.WriteAllBytesAsync(ytDlpPath, ytDlpBytes);
                    }

                    if (!File.Exists(ffmpegPath))
                    {
                        OnStatusMessageChanged?.Invoke(this, "Aşama 2/2: ffmpeg indiriliyor ve çıkarılıyor (boyutu biraz büyük olabilir)...");
                        string zipPath = Path.Combine(baseDir, "ffmpeg.zip");
                        var ffmpegZipBytes = await _httpClient.GetByteArrayAsync("https://github.com/yt-dlp/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip");
                        await File.WriteAllBytesAsync(zipPath, ffmpegZipBytes);

                        using (var archive = System.IO.Compression.ZipFile.OpenRead(zipPath))
                        {
                            var entry = archive.Entries.FirstOrDefault(e => e.FullName.EndsWith("ffmpeg.exe", StringComparison.OrdinalIgnoreCase));
                            if (entry != null)
                            {
                                entry.ExtractToFile(ffmpegPath, true);
                            }
                        }
                        File.Delete(zipPath);
                    }

                    OnStatusMessageChanged?.Invoke(this, "Gerekli araçlar başarıyla indirildi. Kullanıma hazır!");
                }
                catch (Exception ex)
                {
                    OnStatusMessageChanged?.Invoke(this, "Araçlar indirilirken hata oluştu!");
                    throw new Exception("Araçlar indirilirken hata oluştu: " + ex.Message + "\nLütfen programı yönetici olarak çalıştırın veya internet bağlantınızı kontrol edin.");
                }
            }

            if (!File.Exists(ytDlpPath))
                throw new Exception("yt-dlp.exe bulunamadı!");

            if (!File.Exists(ffmpegPath))
                throw new Exception("ffmpeg.exe bulunamadı!");
        }

        public async Task<VideoMetadata> FetchMetadataAsync(string url)
        {
            var metadata = new VideoMetadata();

            try
            {
                await EnsureDependenciesExistAsync();

                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string ytDlpPath = Path.Combine(baseDir, "yt-dlp.exe");

                OnStatusMessageChanged?.Invoke(this, "yt-dlp aracıyla video bilgileri çekiliyor...");

                var processInfo = new ProcessStartInfo
                {
                    FileName = ytDlpPath,
                    Arguments = $"--dump-json --no-warnings --no-playlist \"{url}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = System.Text.Encoding.UTF8 // Türkçe karakterler için önemli
                };

                using var process = Process.Start(processInfo);

                string jsonOutput = await process.StandardOutput.ReadToEndAsync();
                string error = await process.StandardError.ReadToEndAsync();

                await process.WaitForExitAsync();

                if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(jsonOutput))
                {
                    throw new Exception($"Veri çekilemedi. Bağlantıyı kontrol edin.\nDetay: {error}");
                }

                // Eger birden fazla JSON nesnesi (playlist vs) veya hata mesaji varsa sadece JSON'u alalim
                string[] lines = jsonOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                string validJson = System.Linq.Enumerable.FirstOrDefault(lines, l => l.TrimStart().StartsWith("{"));

                if (string.IsNullOrEmpty(validJson))
                {
                    throw new Exception("Geçerli bir video bilgisi bulunamadı.\nDetay: " + (string.IsNullOrWhiteSpace(error) ? jsonOutput : error));
                }

                using var doc = JsonDocument.Parse(validJson);
                var root = doc.RootElement;

                metadata.Title = root.TryGetProperty("title", out var t) ? t.GetString() : "VOD";
                metadata.ChannelName = root.TryGetProperty("uploader", out var u) ? u.GetString() : "Bilinmeyen Yayıncı";
                metadata.ThumbnailUrl = root.TryGetProperty("thumbnail", out var th) ? th.GetString() : "";

                string foundUrl = "";
                if (root.TryGetProperty("url", out var vUrl) && !string.IsNullOrEmpty(vUrl.GetString()))
                {
                    foundUrl = vUrl.GetString();
                }
                else if (root.TryGetProperty("requested_formats", out var reqFormats) && reqFormats.ValueKind == JsonValueKind.Array && reqFormats.GetArrayLength() > 0)
                {
                    if (reqFormats[0].TryGetProperty("url", out var rfUrl))
                        foundUrl = rfUrl.GetString();
                }
                else if (root.TryGetProperty("requested_downloads", out var reqDownloads) && reqDownloads.ValueKind == JsonValueKind.Array && reqDownloads.GetArrayLength() > 0)
                {
                    if (reqDownloads[0].TryGetProperty("url", out var rdUrl))
                        foundUrl = rdUrl.GetString();
                }

                metadata.VideoUrl = foundUrl;
                metadata.OriginalUrl = url;

                if (root.TryGetProperty("duration", out var d) && d.ValueKind == JsonValueKind.Number)
                {
                    metadata.Duration = TimeSpan.FromSeconds(d.GetDouble());
                }

                metadata.AvailableQualities.Add("En İyi");
                var qualities = new System.Collections.Generic.List<int>();
                if (root.TryGetProperty("formats", out var formats))
                {
                    foreach (var format in formats.EnumerateArray())
                    {
                        if (format.TryGetProperty("height", out var h) && h.ValueKind == JsonValueKind.Number)
                        {
                            int height = h.GetInt32();
                            if (height > 0 && !qualities.Contains(height))
                            {
                                qualities.Add(height);
                            }
                        }
                    }
                }
                qualities.Sort((a, b) => b.CompareTo(a));
                foreach(var q in qualities) metadata.AvailableQualities.Add($"{q}p");

                // Klip mi VOD mu basit kontrol
                metadata.IsClip = url.Contains("/clip");

                if (string.IsNullOrEmpty(metadata.VideoUrl))
                {
                    throw new Exception("Video m3u8 kaynak linki bulunamadı.");
                }

                return metadata;
            }
            catch (Exception ex)
            {
                throw new Exception("Medya bilgileri alınamadı: " + ex.Message);
            }
        }

        public async Task StartDownloadAsync(VideoMetadata metadata, string outputFolder, string fileName, string startTime = "", string endTime = "", string quality = "En İyi")
        {
            _cancellationTokenSource = new CancellationTokenSource();
            _totalDuration = metadata.Duration;
            _lastUpdate = DateTime.MinValue;
            _lastSizeKb = 0;
            _lastSpeedText = "Hesaplanıyor...";

            try
            {
                OnStatusMessageChanged?.Invoke(this, "Sistem kontrol ediliyor...");

                if (!Directory.Exists(outputFolder))
                    Directory.CreateDirectory(outputFolder);

                string outputPath = Path.Combine(outputFolder, $"{fileName}.mp4");

                await EnsureDependenciesExistAsync();

                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string ffmpegPath = Path.Combine(baseDir, "ffmpeg.exe");

                string streamUrl = metadata.VideoUrl;
                if (quality != "En İyi" && !string.IsNullOrEmpty(metadata.OriginalUrl))
                {
                    OnStatusMessageChanged?.Invoke(this, $"{quality} kalitesi için yayın linki bulunuyor...");
                    string height = quality.Replace("p", "");
                    string ytDlpPath = Path.Combine(baseDir, "yt-dlp.exe");

                    var getUrlInfo = new ProcessStartInfo
                    {
                        FileName = ytDlpPath,
                        Arguments = $"--no-warnings --no-playlist -f \"best[height<={height}]\" --get-url \"{metadata.OriginalUrl}\"",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    using var process = Process.Start(getUrlInfo);
                    string newUrl = await process.StandardOutput.ReadLineAsync();
                    await process.WaitForExitAsync();

                    if (!string.IsNullOrWhiteSpace(newUrl))
                    {
                        streamUrl = newUrl.Trim();
                    }
                }

                string FormatTime(string t)
                {
                    if (string.IsNullOrWhiteSpace(t) || t == "00:00:00") return "";
                    t = t.Replace(".", ":");
                    if (t.Split(':').Length == 2) t = "00:" + t; // 19:20 -> 00:19:20
                    return t;
                }

                startTime = FormatTime(startTime);
                endTime = FormatTime(endTime);

                string timeArgs = "";
                // Hızlı seek için -ss öncesine yazılır, ancak m3u8 playlistlerde stabilite için sonraya da yazılabilir.
                if (!string.IsNullOrWhiteSpace(startTime)) timeArgs += $"-ss {startTime} ";
                if (!string.IsNullOrWhiteSpace(endTime)) timeArgs += $"-to {endTime} ";

                string arguments = $"-y {timeArgs}-i \"{streamUrl}\" -c copy ";

                // VOD'lar için bsf:a gerekir (eğer m3u8 playlist ise). MP4 formatlılarda hata verebilir.
                if (streamUrl.Contains(".m3u8")) 
                    arguments += "-bsf:a aac_adtstoasc ";

                arguments += $"\"{outputPath}\"";

                _downloadProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = ffmpegPath,
                        Arguments = arguments,
                        UseShellExecute = false,
                        RedirectStandardError = true, 
                        CreateNoWindow = true
                    }
                };

                _downloadProcess.ErrorDataReceived += Process_ErrorDataReceived;
                _downloadProcess.Start();
                _downloadProcess.BeginErrorReadLine();

                OnStatusMessageChanged?.Invoke(this, "İndirme ve işleme başladı (0 Kayıp - En Hızlı Yöntem)...");

                await _downloadProcess.WaitForExitAsync(_cancellationTokenSource.Token);

                if (_downloadProcess.ExitCode == 0)
                {
                    OnStatusMessageChanged?.Invoke(this, "İşlem başarıyla tamamlandı.");
                    OnDownloadCompleted?.Invoke(this, EventArgs.Empty);
                }
                else if (!_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    throw new Exception($"FFmpeg bir hata ile sonlandı (Hata Kodu: {_downloadProcess.ExitCode}).");
                }
            }
            catch (OperationCanceledException)
            {
                OnStatusMessageChanged?.Invoke(this, "İşlem kullanıcı tarafından iptal edildi.");
                CleanupProcess();
                DeleteIncompleteFile(Path.Combine(outputFolder, $"{fileName}.mp4"));
            }
            catch (Exception ex)
            {
                OnError?.Invoke(this, ex);
            }
            finally
            {
                _downloadProcess?.Dispose();
                _downloadProcess = null;
            }
        }

        public void CancelDownload()
        {
            if (_cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
            {
                _cancellationTokenSource.Cancel();
                CleanupProcess();
            }
        }

        private void CleanupProcess()
        {
            try
            {
                if (_downloadProcess != null && !_downloadProcess.HasExited)
                    _downloadProcess.Kill(entireProcessTree: true); 
            }
            catch { }
        }

        private void DeleteIncompleteFile(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch { }
        }

        private void Process_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (string.IsNullOrEmpty(e.Data)) return;

            // Duration: 03:45:12.50
            if (_totalDuration == TimeSpan.Zero)
            {
                var durationMatch = Regex.Match(e.Data, @"Duration:\s(?<time>\d{2}:\d{2}:\d{2}\.\d{2})");
                if (durationMatch.Success && TimeSpan.TryParse(durationMatch.Groups["time"].Value, out TimeSpan duration))
                {
                    _totalDuration = duration;
                }
            }

            // time=00:15:30.20 bitrate=... speed= 5.5x
            var timeMatch = Regex.Match(e.Data, @"time=(?<time>\d{2}:\d{2}:\d{2}\.\d{2})");
            var speedMatch = Regex.Match(e.Data, @"speed=\s*(?<speed>\d+\.?\d*)x");
            var sizeMatch = Regex.Match(e.Data, @"size=\s*(?<size>\d+)kB");

            if (timeMatch.Success && _totalDuration.TotalSeconds > 0)
            {
                if (TimeSpan.TryParse(timeMatch.Groups["time"].Value, out TimeSpan currentTime))
                {
                    double percent = (currentTime.TotalSeconds / _totalDuration.TotalSeconds) * 100.0;
                    if (percent > 100) percent = 100;

                    string speedText = speedMatch.Success ? $"{speedMatch.Groups["speed"].Value}x" : "Hesaplanıyor...";

                    if (sizeMatch.Success && double.TryParse(sizeMatch.Groups["size"].Value, out double currentSizeKb))
                    {
                        var now = DateTime.Now;
                        if (_lastUpdate == DateTime.MinValue)
                        {
                            _lastUpdate = now;
                            _lastSizeKb = currentSizeKb;
                        }
                        else
                        {
                            var timeDiff = (now - _lastUpdate).TotalSeconds;
                            // Update speed every second for smoother reading
                            if (timeDiff >= 1.0)
                            {
                                double deltaKb = currentSizeKb - _lastSizeKb;
                                double speedMBps = (deltaKb / 1024.0) / timeDiff;
                                _lastSpeedText = $"{speedMBps:F1} MB/s"; // e.g., 24.5 MB/s
                                _lastUpdate = now;
                                _lastSizeKb = currentSizeKb;
                            }
                        }
                        // Combine logic: "24.5 MB/s (5.5x)"
                        speedText = $"{_lastSpeedText} ({speedText})";
                    }

                    string etaText = "Bilinmiyor";
                    if (speedMatch.Success && double.TryParse(speedMatch.Groups["speed"].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double speed))
                    {
                        if (speed > 0)
                        {
                            double remainingSeconds = (_totalDuration.TotalSeconds - currentTime.TotalSeconds) / speed;
                            etaText = TimeSpan.FromSeconds(remainingSeconds).ToString(@"hh\:mm\:ss");
                        }
                    }

                    OnProgressChanged?.Invoke(this, new DownloadProgressInfo
                    {
                        Percentage = percent,
                        Speed = speedText,
                        ETA = etaText
                    });
                }
            }
        }
    }
}