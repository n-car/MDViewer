using System;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Interop;
using Microsoft.Win32;

namespace MDViewer
{
    public partial class MainWindow : Window
    {
        private string currentPath = null;

        public MainWindow()
        {
            try
            {
                InitializeComponent();
                _ = InitializeAsync();
            }
            catch (Exception ex)
            {
             MessageBox.Show(string.Format(Properties.Resources.InitializationError, ex.Message), 
                                 Properties.Resources.Error, MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.Shutdown();
            }
        }

        private async Task InitializeAsync()
        {
            await Viewer.EnsureCoreWebView2Async();
            UpdateButtonStates();
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
                StatusIcon.Text = "&#xE783;";
                StatusIcon.Foreground = System.Windows.Media.Brushes.DarkRed;
                StatusMessage.Foreground = System.Windows.Media.Brushes.DarkRed;
            }
            else
            {
                StatusPanel.Background = System.Windows.Media.Brushes.LightCyan;
                StatusPanel.BorderBrush = System.Windows.Media.Brushes.SkyBlue;
                StatusIcon.Text = "&#xE946;";
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
                              "<h3>" + Properties.Resources.RenderingErrorTitle + "</h3>" +
                              "<p><strong>" + Properties.Resources.UnableToContactGitHub + "</strong></p>" +
                              "<p>" + Properties.Resources.Error + ": " + System.Net.WebUtility.HtmlEncode(ex.Message) + "</p>" +
                              "<p>" + Properties.Resources.FileWillBeShownAsText + "</p>" +
                              "<hr/><pre style='background: white; padding: 10px; border: 1px solid #ddd;'>" + System.Net.WebUtility.HtmlEncode(md) + "</pre>" +
                              "</div>";
                }
                catch (Exception ex)
                {
                    ShowStatus(string.Format(Properties.Resources.RenderingError, ex.Message), true);
                    htmlBody = "<div style='padding: 20px; background: #f8d7da; border: 1px solid #f5c6cb; border-radius: 4px; color: #721c24;'>" +
                              "<h3>" + Properties.Resources.RenderingErrorTitle + "</h3>" +
                              "<p>" + Properties.Resources.Error + ": " + System.Net.WebUtility.HtmlEncode(ex.Message) + "</p>" +
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
                            sb.Append("\\u");
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

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var args = Environment.GetCommandLineArgs();
            if (args.Length > 1)
            {
                var p = args[1];
                if (File.Exists(p))
                    _ = LoadFileAsync(p);
            }
        }
    }
}