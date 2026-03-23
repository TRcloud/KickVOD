using System.Configuration;
using System.Data;
using System.Windows;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace KickVOD
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Arayüz (UI) thread'indeki hataları yakala
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;

            // Arka plan thread'lerindeki hataları yakala
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            // Beklenmeyen Task hatalarını yakala
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            ShowAndLogError("UI Thread Hatası", e.Exception);
            e.Handled = true; // Uygulamanın aniden kapanmasını engellemeye çalış
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            ShowAndLogError("Arka Plan Thread Hatası", e.ExceptionObject as Exception);
        }

        private void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            ShowAndLogError("Task Hatası", e.Exception);
            e.SetObserved();
        }

        private void ShowAndLogError(string source, Exception? ex)
        {
            string exMessage = ex?.Message ?? "Bilinmeyen Hata";
            string exTrace = ex?.StackTrace ?? "";

            string errorMsg = $"Hata Kaynağı: {source}\n\n";
            errorMsg += $"Mesaj: {exMessage}\n\n";
            errorMsg += $"Detaylar: {exTrace}\n";

            // Eğer varsa iç hatayı da (InnerException) ekle
            if (ex?.InnerException != null)
            {
                errorMsg += $"\nİç Hata: {ex.InnerException.Message}\n{ex.InnerException.StackTrace}";
            }

            try
            {
                // Hataları uygulama dizininde bir txt dosyasına kaydet
                string logFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash_log.txt");
                File.AppendAllText(logFile, $"[{DateTime.Now}] {errorMsg}\n\n--------------------------------------------------\n\n");
            }
            catch { }

            MessageBox.Show(errorMsg, "Beklenmeyen Bir Hata Oluştu!", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
