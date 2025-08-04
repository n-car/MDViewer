using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace MDViewer
{
    public partial class MainWindow : Window
    {
        private string currentPath = null;
        private string[] startupArgs;

        public MainWindow(string[] args)
        {
            startupArgs = args;
            InitializeComponent();
            this.Loaded += MainWindow_Loaded;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                await InitializeWebView2Async(); // invece del precedente EnsureCoreWebView2Async
                UpdateButtonStates();

                if (startupArgs != null && startupArgs.Length > 0)
                {
                    if (File.Exists(startupArgs[0]))
                    {
                        currentPath = startupArgs[0];
                        FilePathBox.Text = currentPath;
                        await LoadFileAsync(currentPath);
                    }
                    else
                    {
                        ShowStatus(string.Format(Properties.Resources.FileNotFound, startupArgs[0]), true);
                    }
                }
            }
            catch (Exception ex)
            {
                ShowStatus($"Errore durante l'inizializzazione: {ex.Message}", true);
                MessageBox.Show(ex.ToString(), "Errore dettagliato", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task InitializeWebView2Async()
        {
            // Assicuriamoci che WebView2 runtime sia disponibile
            await EnsureWebView2RuntimeAsync();

            // Usa una cartella user-data che sicuramente è scrivibile dall'utente
            string userDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MDViewer",
                "WebView2");

            try
            {
                Directory.CreateDirectory(userDataFolder);
            }
            catch (Exception ex)
            {
                ShowStatus("Impossibile creare la cartella dati di WebView2: " + ex.Message, true);
                throw;
            }

            // Crea l'ambiente esplicitamente con quella cartella
            var env = await Microsoft.Web.WebView2.Core.CoreWebView2Environment.CreateAsync(null, userDataFolder);
            await Viewer.EnsureCoreWebView2Async(env);
        }

        private static readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

        private async Task EnsureWebView2RuntimeAsync()
        {
            if (IsWebView2RuntimeInstalled())
                return;

            ShowStatus("Sto installando WebView2 Runtime...", false);

            string downloadUrl = "https://go.microsoft.com/fwlink/p/?LinkId=2124703";
            string tempBootstrapper = Path.Combine(Path.GetTempPath(), "MicrosoftEdgeWebView2Setup.exe");

            if (!File.Exists(tempBootstrapper) ||
                (DateTime.UtcNow - File.GetLastWriteTimeUtc(tempBootstrapper)) > TimeSpan.FromDays(1))
            {
                try
                {
                    using (HttpResponseMessage response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead))
                    {
                        response.EnsureSuccessStatusCode();
                        using (Stream responseStream = await response.Content.ReadAsStreamAsync())
                        using (var fs = new FileStream(tempBootstrapper, FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            await responseStream.CopyToAsync(fs);
                        }
                    }
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException("Impossibile scaricare il bootstrapper WebView2: " + ex.Message, ex);
                }
            }

            bool installed = await RunBootstrapperAsync(tempBootstrapper, silent: true);

            if (!installed)
            {
                installed = await RunBootstrapperAsync(tempBootstrapper, silent: false, requireElevation: true);
            }

            if (!installed || !IsWebView2RuntimeInstalled())
                throw new InvalidOperationException("WebView2 Runtime non è stato installato correttamente.");
        }

        private Task<bool> WaitForExitAsync(Process process, int timeoutMilliseconds = -1)
        {
            var tcs = new TaskCompletionSource<bool>();

            void Handler(object s, EventArgs e)
            {
                process.Exited -= Handler;
                tcs.TrySetResult(process.ExitCode == 0);
            }

            process.EnableRaisingEvents = true;
            process.Exited += Handler;

            if (process.HasExited)
            {
                process.Exited -= Handler;
                return Task.FromResult(process.ExitCode == 0);
            }

            if (timeoutMilliseconds >= 0)
            {
                var ct = new System.Threading.CancellationTokenSource(timeoutMilliseconds);
                ct.Token.Register(() =>
                {
                    process.Exited -= Handler;
                    tcs.TrySetResult(false);
                });
            }

            return tcs.Task;
        }

        private async Task<bool> RunBootstrapperAsync(string path, bool silent, bool requireElevation = false)
        {
            var psi = new ProcessStartInfo(path)
            {
                Arguments = silent ? "/silent" : string.Empty,
                UseShellExecute = true
            };

            if (requireElevation)
                psi.Verb = "runas";

            try
            {
                var proc = Process.Start(psi);
                if (proc == null)
                    return false;

                return await WaitForExitAsync(proc, timeoutMilliseconds: 120_000);
            }
            catch (System.ComponentModel.Win32Exception ex) when (requireElevation && ex.ErrorCode == -2147467259)
            {
                // L'utente ha annullato la richiesta UAC
                ShowStatus("Installazione WebView2 annullata dall'utente.", true);
                return false;
            }
            catch
            {
                return false;
            }
        }

        private bool IsWebView2RuntimeInstalled()
        {
            try
            {
                // null usa il default user data folder
                string version = Microsoft.Web.WebView2.Core.CoreWebView2Environment.GetAvailableBrowserVersionString(null);
                return !string.IsNullOrEmpty(version);
            }
            catch
            {
                return false;
            }
        }


        private void UpdateButtonStates()
        {
            bool hasFile = !string.IsNullOrEmpty(currentPath);
            BtnStampa.IsEnabled = hasFile;
            BtnRicarica.IsEnabled = hasFile;
        }

        private void ShowLoading(bool show)
        {
            LoadingSpinner.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            BtnApri.IsEnabled = !show;
            BtnRicarica.IsEnabled = !show && !string.IsNullOrEmpty(currentPath);
        }

        private void ShowStatus(string message, bool isError = false)
        {
            StatusMessage.Text = message;
            if (isError)
            {
                StatusPanel.Background = System.Windows.Media.Brushes.MistyRose;
                StatusPanel.BorderBrush = System.Windows.Media.Brushes.LightCoral;
                StatusIcon.Text = "\uE783";
                StatusIcon.Foreground = System.Windows.Media.Brushes.DarkRed;
                StatusMessage.Foreground = System.Windows.Media.Brushes.DarkRed;
            }
            else
            {
                StatusPanel.Background = System.Windows.Media.Brushes.LightCyan;
                StatusPanel.BorderBrush = System.Windows.Media.Brushes.SkyBlue;
                StatusIcon.Text = "\uE946";
                StatusIcon.Foreground = System.Windows.Media.Brushes.DarkBlue;
                StatusMessage.Foreground = System.Windows.Media.Brushes.DarkBlue;
            }
            StatusPanel.Visibility = Visibility.Visible;
        }

        private void HideStatus()
        {
            StatusPanel.Visibility = Visibility.Collapsed;
        }

        private void OpenFile_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFileDialog()
            {
                Filter = Properties.Resources.MarkdownFilter
            };
            if (ofd.ShowDialog() == true)
            {
                _ = LoadFileAsync(ofd.FileName);
            }
        }

        private async void Reload_Click(object sender, RoutedEventArgs e)
        {
            if (currentPath != null)
                await LoadFileAsync(currentPath);
        }

        private async void Print_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (Viewer.CoreWebView2 != null && !string.IsNullOrEmpty(currentPath))
                {
                    await Viewer.CoreWebView2.ExecuteScriptAsync("window.print();");
                }
                else
                {
                    MessageBox.Show(Properties.Resources.NoPrintDocument, Properties.Resources.Warning,
                                  MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format(Properties.Resources.PrintError, ex.Message), Properties.Resources.Error,
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LoadFileAsync(string path)
        {
            if (!File.Exists(path)) return;

            try
            {
                ShowLoading(true);
                HideStatus();

                currentPath = path;
                FilePathBox.Text = path;

                string md = File.ReadAllText(path);
                ShowStatus(Properties.Resources.LoadingMessage);

                string htmlBody;
                try
                {
                    htmlBody = await RenderGitHubMarkdownAsync(md);
                    HideStatus();
                }
                catch (HttpRequestException ex)
                {
                    ShowStatus(string.Format(Properties.Resources.ConnectionError, ex.Message), true);
                    htmlBody = "<div style='padding: 20px; background: #f8d7da; border: 1px solid #f5c6cb; border-radius: 4px; color: #721c24;'>" +
                              "<h3>🔌 " + Properties.Resources.RenderingErrorTitle + "</h3>" +
                              "<p><strong>🌐 " + Properties.Resources.UnableToContactGitHub + "</strong></p>" +
                              "<p>❌ " + Properties.Resources.Error + ": " + System.Net.WebUtility.HtmlEncode(ex.Message) + "</p>" +
                              "<p>" + Properties.Resources.FileWillBeShownAsText + "</p>" +
                              "<hr/><pre style='background: white; padding: 10px; border: 1px solid #ddd;'>" + System.Net.WebUtility.HtmlEncode(md) + "</pre>" +
                              "</div>";
                }
                catch (Exception ex)
                {
                    ShowStatus(string.Format(Properties.Resources.RenderingError, ex.Message), true);
                    htmlBody = "<div style='padding: 20px; background: #f8d7da; border: 1px solid #f5c6cb; border-radius: 4px; color: #721c24;'>" +
                              "<h3>💥 " + Properties.Resources.RenderingErrorTitle + "</h3>" +
                              "<p>⛔ " + Properties.Resources.Error + ": " + System.Net.WebUtility.HtmlEncode(ex.Message) + "</p>" +
                              "<p>" + Properties.Resources.FileWillBeShownAsText + "</p>" +
                              "<hr/><pre style='background: white; padding: 10px; border: 1px solid #ddd;'>" + System.Net.WebUtility.HtmlEncode(md) + "</pre>" +
                              "</div>";
                }

                string fileName = System.IO.Path.GetFileName(path);
                string full = @"<!doctype html>
<html>
<head>
<meta charset='utf-8'>
<title>" + fileName + @"</title>
<style>
body {
    font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Helvetica, Arial, sans-serif;
    padding: 1rem;
    background: white;
    color: #24292f;
}
.markdown-body {
    max-width: 900px;
    margin: auto;
}
pre {
    background: #f6f8fa;
    padding: 10px;
    overflow: auto;
    border-radius: 6px;
    border: 1px solid #e1e4e8;
}
code {
    background: rgba(27,31,35,.05);
    padding: .2em .4em;
    border-radius: 6px;
    font-size: 85%;
}
h1, h2, h3, h4 {
    border-bottom: 1px solid #e1e4e8;
    padding-bottom: .3em;
}
blockquote {
    color: #6a737d;
    border-left: .25em solid #dfe2e5;
    padding: 0 1em;
}
@media print {
    body {
        background: white !important;
        color: black !important;
    }
    pre {
        background: white !important;
        border: 1px solid #ccc !important;
    }
}
</style>
</head>
<body class='markdown-body'>
" + htmlBody + @"
</body>
</html>";

                Viewer.CoreWebView2.NavigateToString(full);
                UpdateButtonStates();
            }
            catch (Exception ex)
            {
                ShowStatus(string.Format(Properties.Resources.LoadingFileError, ex.Message), true);
            }
            finally
            {
                ShowLoading(false);
            }
        }

        private async Task<string> RenderGitHubMarkdownAsync(string markdown)
        {
            using (var client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromSeconds(10);
                client.DefaultRequestHeaders.UserAgent.ParseAdd("MarkdownViewerApp");
                var payload = "{\"text\":" + JsonEscape(markdown) + ",\"mode\":\"gfm\"}";
                var content = new StringContent(payload, Encoding.UTF8, "application/json");
                var resp = await client.PostAsync("https://api.github.com/markdown", content);
                resp.EnsureSuccessStatusCode();
                return await resp.Content.ReadAsStringAsync();
            }
        }

        private string JsonEscape(string s)
        {
            var sb = new StringBuilder();
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '"': sb.Append("\\\""); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (char.IsControl(c))
                        {
                            sb.Append("&#x");
                            sb.Append(((int)c).ToString("x4"));
                        }
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
            return sb.ToString();
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var arr = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (arr.Length > 0)
                    _ = LoadFileAsync(arr[0]);
            }
        }
    }
}