using Microsoft.Win32;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace it.carpanese.utilities.MDViewer
{
    public partial class MainWindow : Window
    {
        private string currentPath = null;
        private string[] startupArgs;
        private readonly MarkdownCache _cache;
        private readonly MarkdownRenderer _renderer;
        private FileSystemWatcher _fileWatcher;
        private DateTime _lastFileChangeTime = DateTime.MinValue;
        private readonly SemaphoreSlim _loadFileSemaphore = new SemaphoreSlim(1, 1);
        private bool _enableSyntaxHighlighting = true;
        private bool _enableAutoReload = true;

        public MainWindow(string[] args)
        {
            startupArgs = args;
            var settings = AppSettings.Instance;

            // Inizializza il renderer con la cache (usata solo per GitHub API)
            _cache = new MarkdownCache(
                TimeSpan.FromDays(Math.Max(1, settings.CacheDurationDays)),
                Math.Max(1, settings.MaxCacheSizeMB));
            _renderer = new MarkdownRenderer(_cache);
            _renderer.CurrentProvider = settings.DefaultProvider;

            InitializeComponent();
            RestoreWindowBounds();
            UpdateProviderButtonLabel();

            // Registra listener per cambio tema
            ThemeManager.Instance.ThemeChanged += OnThemeChanged;

            // Registra listener per file recenti
            RecentFilesManager.Instance.RecentFilesChanged += OnRecentFilesChanged;

            this.Loaded += MainWindow_Loaded;
            this.Closing += MainWindow_Closing;
        }

        /// <summary>
        /// Gestisce il cambio tema in tempo reale.
        /// </summary>
        private async void OnThemeChanged(object sender, AppTheme theme)
        {
            System.Diagnostics.Debug.WriteLine($"Tema cambiato: {theme}");

            // Se c'è un file aperto, ricaricalo con il nuovo tema HTML
            if (!string.IsNullOrEmpty(currentPath) && File.Exists(currentPath))
            {
                await LoadFileAsync(currentPath);
            }
        }

        /// <summary>
        /// Inizializza il menu dei file recenti.
        /// </summary>
        private void InitializeRecentFilesMenu()
        {
            UpdateRecentFilesMenu();
        }

        /// <summary>
        /// Aggiorna il menu dei file recenti.
        /// </summary>
        private void UpdateRecentFilesMenu()
        {
            RecentFilesMenu.Items.Clear();

            var recentFiles = RecentFilesManager.Instance.GetRecentFilesInfo().ToList();

            if (recentFiles.Count == 0)
            {
                var emptyItem = new MenuItem
                {
                    Header = Localizer.Get("MainNoRecentFiles"),
                    IsEnabled = false
                };
                RecentFilesMenu.Items.Add(emptyItem);
            }
            else
            {
                int index = 1;
                foreach (var fileInfo in recentFiles)
                {
                    var menuItem = new MenuItem
                    {
                        Header = Localizer.Format("MainRecentFileEntryFormat", index, fileInfo.FileName),
                        ToolTip = fileInfo.FullPath,
                        Tag = fileInfo.FullPath,
                        IsEnabled = fileInfo.Exists
                    };

                    if (!fileInfo.Exists)
                    {
                        menuItem.Header = Localizer.Format("MainRecentFileEntryMissingFormat", index, fileInfo.FileName);
                    }

                    menuItem.Click += RecentFileMenuItem_Click;
                    RecentFilesMenu.Items.Add(menuItem);
                    index++;
                }

                // Separatore
                RecentFilesMenu.Items.Add(new Separator());

                // Opzione per pulire la lista
                var clearItem = new MenuItem
                {
                    Header = Localizer.Get("MainClearRecentFilesMenuItem"),
                    Icon = new System.Windows.Controls.TextBlock 
                    { 
                        Text = "\uE74D", 
                        FontFamily = new System.Windows.Media.FontFamily("Segoe MDL2 Assets"),
                        FontSize = 12
                    }
                };
                clearItem.Click += ClearRecentFiles_Click;
                RecentFilesMenu.Items.Add(clearItem);
            }
        }

        /// <summary>
        /// Gestisce il click su un file recente.
        /// </summary>
        private async void RecentFileMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Tag is string filePath)
            {
                if (File.Exists(filePath))
                {
                    await LoadFileAsync(filePath);
                }
                else
                {
                    MessageBox.Show(
                        Localizer.Format("MainRecentFileMissingMessage", filePath),
                        Localizer.Get("MainRecentFileMissingTitle"),
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    // Rimuovi il file dalla lista
                    RecentFilesManager.Instance.RemoveFile(filePath);
                }
            }
        }

        /// <summary>
        /// Pulisce la lista dei file recenti.
        /// </summary>
        private void ClearRecentFiles_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                Localizer.Get("MainClearRecentFilesConfirm"),
                Localizer.Get("ConfirmTitle"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                RecentFilesManager.Instance.Clear();
            }
        }

        /// <summary>
        /// Mostra il menu dei file recenti.
        /// </summary>
        private void RecentFiles_Click(object sender, RoutedEventArgs e)
        {
            UpdateRecentFilesMenu();
            RecentFilesMenu.IsOpen = true;
        }

        /// <summary>
        /// Gestisce il cambio nella lista dei file recenti.
        /// </summary>
        private void OnRecentFilesChanged(object sender, EventArgs e)
        {
            Dispatcher.Invoke(() => UpdateRecentFilesMenu());
        }

        /// <summary>
        /// Aggiorna il testo del pulsante provider in toolbar.
        /// </summary>
        private void UpdateProviderButtonLabel()
        {
            var provider = _renderer.CurrentProvider;
            TxtSelectedProvider.Text = Localizer.Format(
                "MainProviderButtonFormat",
                MarkdownProviderInfo.GetDisplayName(provider));
            BtnSelectProvider.ToolTip = Localizer.Format(
                "ProviderTooltipFormat",
                MarkdownProviderInfo.GetShortDescription(provider),
                Localizer.Get("MainClickToChangeProvider"));
        }

        /// <summary>
        /// Imposta il provider attivo e aggiorna la UI associata.
        /// </summary>
        private void SetCurrentProvider(MarkdownProvider provider)
        {
            _renderer.CurrentProvider = provider;
            UpdateProviderButtonLabel();
            System.Diagnostics.Debug.WriteLine($"Provider attivo: {provider}");
        }

        /// <summary>
        /// Apre il dialog di scelta provider con confronto caratteristiche.
        /// </summary>
        private async void SelectProvider_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ProviderSelectionWindow(_renderer.CurrentProvider)
            {
                Owner = this
            };

            var result = dialog.ShowDialog();
            if (result != true)
                return;

            var previousProvider = _renderer.CurrentProvider;
            SetCurrentProvider(dialog.SelectedProvider);

            if (previousProvider != dialog.SelectedProvider &&
                !string.IsNullOrEmpty(currentPath) &&
                File.Exists(currentPath))
            {
                await LoadFileAsync(currentPath);
            }
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                await InitializeWebView2Async(); // invece del precedente EnsureCoreWebView2Async
                UpdateButtonStates();

                // Inizializza il menu dei file recenti
                InitializeRecentFilesMenu();

                // Pulisci file non più esistenti
                RecentFilesManager.Instance.CleanupInvalidFiles();

                // Mostra versione corrente nella status bar
                VersionLabel.Text = Localizer.Format("MainVersionLabelFormat", UpdateManager.Instance.CurrentVersionString);

                // Applica le impostazioni salvate
                ApplySettings();

                // Controlla aggiornamenti automatici se configurato
                await CheckAutoUpdateAsync();

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
                ShowStatus(string.Format(Properties.Resources.InitializationError, ex.Message), true);
                MessageBox.Show(ex.ToString(), Localizer.Get("MainDetailedErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Controlla automaticamente gli aggiornamenti in base alle impostazioni.
        /// </summary>
        private async Task CheckAutoUpdateAsync()
        {
            try
            {
                if (!AppSettings.Instance.ShouldCheckForUpdates())
                    return;

                System.Diagnostics.Debug.WriteLine("Controllo automatico aggiornamenti...");

                var updateInfo = await UpdateManager.Instance.CheckForUpdatesAsync();

                // Aggiorna timestamp ultimo controllo
                AppSettings.Instance.MarkUpdateChecked();

                if (updateInfo != null && updateInfo.IsUpdateAvailable)
                {
                    // Mostra notifica discreta nella status bar
                    VersionLabel.Text = Localizer.Format(
                        "MainUpdateAvailableVersionLabelFormat",
                        UpdateManager.Instance.CurrentVersionString,
                        updateInfo.LatestVersionString);
                    VersionLabel.Cursor = System.Windows.Input.Cursors.Hand;
                    VersionLabel.MouseLeftButtonUp += (s, e) => 
                    {
                        UpdateManager.Instance.OpenDownloadPage(updateInfo);
                    };
                    VersionLabel.ToolTip = Localizer.Get("MainClickToDownloadNewVersion");
                }
            }
            catch (Exception ex)
            {
                // Non mostrare errori per il controllo automatico
                System.Diagnostics.Debug.WriteLine($"Controllo automatico aggiornamenti fallito: {ex.Message}");
            }
        }

        /// <summary>
        /// Controlla se sono disponibili aggiornamenti.
        /// </summary>
        private async void CheckUpdates_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                BtnCheckUpdates.IsEnabled = false;
                ShowStatus(Localizer.Get("MainCheckingUpdatesStatus"));

                var updateInfo = await UpdateManager.Instance.CheckForUpdatesAsync();

                HideStatus();

                if (updateInfo == null)
                {
                    MessageBox.Show(
                        Localizer.Get("MainUpdateInfoUnavailableMessage"),
                        Localizer.Get("MainCheckUpdatesTitle"),
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                if (updateInfo.IsUpdateAvailable)
                {
                    // Nuova versione disponibile
                    var message = Localizer.Get("MainUpdateAvailableHeader") + Environment.NewLine + Environment.NewLine +
                                  Localizer.Format("MainCurrentVersionFormat", updateInfo.CurrentVersion) + Environment.NewLine +
                                  Localizer.Format("MainNewVersionFormat", updateInfo.LatestVersionString) + Environment.NewLine + Environment.NewLine;

                    if (!string.IsNullOrEmpty(updateInfo.ReleaseNotes))
                    {
                        // Limita le note a 500 caratteri
                        var notes = updateInfo.ReleaseNotes;
                        if (notes.Length > 500)
                            notes = notes.Substring(0, 500) + "...";
                        message += Localizer.Get("MainReleaseNotesHeader") + Environment.NewLine + notes + Environment.NewLine + Environment.NewLine;
                    }

                    message += Localizer.Get("MainOpenDownloadPageQuestion");

                    var result = MessageBox.Show(
                        message,
                        Localizer.Get("MainUpdateAvailableTitle"),
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Information);

                    if (result == MessageBoxResult.Yes)
                    {
                        UpdateManager.Instance.OpenDownloadPage(updateInfo);
                    }
                }
                else
                {
                    // Nessun aggiornamento
                    MessageBox.Show(
                        Localizer.Format("MainNoUpdateMessageFormat", UpdateManager.Instance.CurrentVersionString),
                        Localizer.Get("MainNoUpdateTitle"),
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            catch (UpdateCheckException ex)
            {
                HideStatus();
                MessageBox.Show(
                    ex.Message,
                    Localizer.Get("MainUpdateCheckErrorTitle"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                HideStatus();
                MessageBox.Show(
                    Localizer.Format("MainUpdateCheckUnexpectedErrorFormat", ex.Message),
                    Properties.Resources.Error,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                BtnCheckUpdates.IsEnabled = true;
            }
        }

        /// <summary>
        /// Apre la finestra delle impostazioni.
        /// </summary>
        private async void Settings_Click(object sender, RoutedEventArgs e)
        {
            var settingsWindow = new SettingsWindow();
            settingsWindow.Owner = this;

            var result = settingsWindow.ShowDialog();

            if (result == true && settingsWindow.SettingsChanged)
            {
                // Applica le impostazioni modificate
                ApplySettings();

                // Ricarica il file corrente se necessario
                if (!string.IsNullOrEmpty(currentPath) && File.Exists(currentPath))
                {
                    await LoadFileAsync(currentPath);
                }
            }
        }

        /// <summary>
        /// Applica le impostazioni correnti all'applicazione.
        /// </summary>
        private void ApplySettings()
        {
            var settings = AppSettings.Instance;

            // Applica provider predefinito
            SetCurrentProvider(settings.DefaultProvider);
            _enableSyntaxHighlighting = settings.EnableSyntaxHighlighting;
            _enableAutoReload = settings.EnableAutoReload;

            _cache.UpdateSettings(
                TimeSpan.FromDays(Math.Max(1, settings.CacheDurationDays)),
                Math.Max(1, settings.MaxCacheSizeMB));

            RecentFilesManager.Instance.SetMaxRecentFiles(settings.MaxRecentFiles);
            ApplyThemePreference(settings.ThemePreference);

            if (_enableAutoReload)
            {
                if (!string.IsNullOrEmpty(currentPath) && File.Exists(currentPath))
                {
                    SetupFileWatcher(currentPath);
                }
            }
            else
            {
                DisposeFileWatcher();
            }

            System.Diagnostics.Debug.WriteLine("Impostazioni applicate");
        }

        private void ApplyThemePreference(ThemePreference preference)
        {
            switch (preference)
            {
                case ThemePreference.Light:
                    ThemeManager.Instance.SetTheme(AppTheme.Light);
                    break;
                case ThemePreference.Dark:
                    ThemeManager.Instance.SetTheme(AppTheme.Dark);
                    break;
                default:
                    ThemeManager.Instance.SetTheme(AppTheme.System);
                    break;
            }
        }

        private void RestoreWindowBounds()
        {
            var settings = AppSettings.Instance;
            if (!settings.RememberWindowPosition)
                return;

            if (settings.WindowWidth > 0 && settings.WindowHeight > 0)
            {
                Width = settings.WindowWidth;
                Height = settings.WindowHeight;
            }

            if (!double.IsNaN(settings.WindowLeft) &&
                !double.IsNaN(settings.WindowTop) &&
                AreBoundsVisible(settings.WindowLeft, settings.WindowTop, Width, Height))
            {
                WindowStartupLocation = WindowStartupLocation.Manual;
                Left = settings.WindowLeft;
                Top = settings.WindowTop;
            }
            else if (!double.IsNaN(settings.WindowLeft) || !double.IsNaN(settings.WindowTop))
            {
                // Se i monitor sono cambiati rispetto all'ultima sessione,
                // evita aperture fuori schermo tornando al posizionamento automatico.
                WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }

            if (settings.WindowMaximized)
            {
                WindowState = WindowState.Maximized;
            }
        }

        private static bool AreBoundsVisible(double left, double top, double width, double height)
        {
            if (width <= 0 || height <= 0)
                return false;

            var windowRect = new Rect(left, top, width, height);
            var virtualScreen = new Rect(
                SystemParameters.VirtualScreenLeft,
                SystemParameters.VirtualScreenTop,
                SystemParameters.VirtualScreenWidth,
                SystemParameters.VirtualScreenHeight);

            return windowRect.IntersectsWith(virtualScreen);
        }

        private void SaveWindowBounds()
        {
            var settings = AppSettings.Instance;
            if (!settings.RememberWindowPosition)
                return;

            var bounds = WindowState == WindowState.Normal ? this : null;
            settings.WindowLeft = bounds != null ? Left : RestoreBounds.Left;
            settings.WindowTop = bounds != null ? Top : RestoreBounds.Top;
            settings.WindowWidth = bounds != null ? Width : RestoreBounds.Width;
            settings.WindowHeight = bounds != null ? Height : RestoreBounds.Height;
            settings.WindowMaximized = WindowState == WindowState.Maximized;
            settings.Save();
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
                ShowStatus(Localizer.Format("MainWebViewDataFolderCreateError", ex.Message), true);
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

            ShowStatus(Localizer.Get("MainInstallingWebViewRuntimeStatus"), false);

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
                    throw new InvalidOperationException(Localizer.Format("MainWebViewBootstrapperDownloadErrorFormat", ex.Message), ex);
                }
            }

            bool installed = await RunBootstrapperAsync(tempBootstrapper, silent: true);

            if (!installed)
            {
                installed = await RunBootstrapperAsync(tempBootstrapper, silent: false, requireElevation: true);
            }

            if (!installed || !IsWebView2RuntimeInstalled())
                throw new InvalidOperationException(Localizer.Get("MainWebViewRuntimeInstallFailed"));
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
                ShowStatus(Localizer.Get("MainWebViewInstallCanceled"), true);
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
            BtnExportPdf.IsEnabled = hasFile;
            BtnRicarica.IsEnabled = hasFile;
        }

        private void ShowLoading(bool show)
        {
            LoadingSpinner.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            BtnApri.IsEnabled = !show;
            BtnRicarica.IsEnabled = !show && !string.IsNullOrEmpty(currentPath);
            BtnExportPdf.IsEnabled = !show && !string.IsNullOrEmpty(currentPath);
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

        /// <summary>
        /// Esporta il documento corrente in formato PDF.
        /// </summary>
        private async void ExportPdf_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (Viewer.CoreWebView2 == null || string.IsNullOrEmpty(currentPath))
                {
                    MessageBox.Show(Localizer.Get("MainNoDocumentToExport"), Properties.Resources.Warning,
                                  MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // Proponi un nome file basato sul file Markdown
                string defaultFileName = Path.GetFileNameWithoutExtension(currentPath) + ".pdf";
                string defaultFolder = Path.GetDirectoryName(currentPath);

                var saveDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Title = Localizer.Get("MainExportPdfDialogTitle"),
                    Filter = Localizer.Get("MainPdfFilter"),
                    FileName = defaultFileName,
                    InitialDirectory = defaultFolder,
                    DefaultExt = ".pdf"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    ShowLoading(true);
                    ShowStatus(Localizer.Get("MainExportPdfInProgressStatus"));

                    try
                    {
                        // Usa PrintToPdfAsync di WebView2
                        await Viewer.CoreWebView2.PrintToPdfAsync(saveDialog.FileName);

                        HideStatus();

                        // Chiedi se aprire il file
                        var result = MessageBox.Show(
                            Localizer.Format("MainExportPdfSuccessMessageFormat", saveDialog.FileName),
                            Localizer.Get("MainExportPdfCompletedTitle"),
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Information);

                        if (result == MessageBoxResult.Yes)
                        {
                            // Apri il PDF con l'applicazione predefinita
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = saveDialog.FileName,
                                UseShellExecute = true
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        ShowStatus(Localizer.Format("MainExportErrorStatusFormat", ex.Message), true);
                        MessageBox.Show(Localizer.Format("MainExportPdfErrorFormat", ex.Message), 
                                      Properties.Resources.Error,
                                      MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    finally
                    {
                        ShowLoading(false);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(Localizer.Format("MainGenericErrorFormat", ex.Message), Properties.Resources.Error,
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LoadFileAsync(string path)
        {
            if (!File.Exists(path)) return;

            await _loadFileSemaphore.WaitAsync();
            try
            {
                if (!File.Exists(path))
                    return;

                ShowLoading(true);
                HideStatus();

                currentPath = path;
                FilePathBox.Text = path;

                // Setup file watcher per auto-reload quando il file cambia
                SetupFileWatcher(path);

                string md = File.ReadAllText(path);

                // Mostra messaggio solo per GitHub API (Markdig è istantaneo)
                if (_renderer.CurrentProvider == MarkdownProvider.GitHubApi)
                {
                    ShowStatus(Properties.Resources.LoadingMessage);
                }

                // Usa il renderer selezionato
                var result = await _renderer.RenderAsync(md);

                string htmlBody;

                if (result.Success)
                {
                    htmlBody = result.Html;

                    // Mostra indicazione se dalla cache
                    if (result.FromCache)
                    {
                        System.Diagnostics.Debug.WriteLine("Rendering dalla cache");
                    }

                    HideStatus();
                }
                else if (result.IsNetworkError)
                {
                    // Errore di rete - mostra errore con fallback
                    ShowStatus(string.Format(Properties.Resources.ConnectionError, result.Error), true);
                    htmlBody = "<div style='padding: 20px; background: #f8d7da; border: 1px solid #f5c6cb; border-radius: 4px; color: #721c24;'>" +
                              "<h3>🔌 " + Properties.Resources.RenderingErrorTitle + "</h3>" +
                              "<p><strong>🌐 " + Properties.Resources.UnableToContactGitHub + "</strong></p>" +
                              "<p>❌ " + Properties.Resources.Error + ": " + System.Net.WebUtility.HtmlEncode(result.Error) + "</p>" +
                              "<p>" + Properties.Resources.FileWillBeShownAsText + "</p>" +
                              "<hr/><pre style='background: white; padding: 10px; border: 1px solid #ddd;'>" + System.Net.WebUtility.HtmlEncode(md) + "</pre>" +
                              "</div>";
                }
                else
                {
                    // Altro errore
                    ShowStatus(string.Format(Properties.Resources.RenderingError, result.Error), true);
                    htmlBody = "<div style='padding: 20px; background: #f8d7da; border: 1px solid #f5c6cb; border-radius: 4px; color: #721c24;'>" +
                              "<h3>💥 " + Properties.Resources.RenderingErrorTitle + "</h3>" +
                              "<p>⛔ " + Properties.Resources.Error + ": " + System.Net.WebUtility.HtmlEncode(result.Error ?? Localizer.Get("MainUnknownError")) + "</p>" +
                              "<p>" + Properties.Resources.FileWillBeShownAsText + "</p>" +
                              "<hr/><pre style='background: white; padding: 10px; border: 1px solid #ddd;'>" + System.Net.WebUtility.HtmlEncode(md) + "</pre>" +
                              "</div>";
                }

                string fileName = System.IO.Path.GetFileName(path);
                string safeTitle = System.Net.WebUtility.HtmlEncode(fileName);

                // Ottieni il CSS per il tema corrente
                string themeCss = ThemeManager.Instance.GetHtmlThemeCss();

                // Seleziona il tema highlight.js in base al tema corrente
                string highlightTheme = ThemeManager.Instance.IsDarkTheme 
                    ? "github-dark" 
                    : "github";

                string highlightHeader = string.Empty;
                string highlightScript = string.Empty;

                if (_enableSyntaxHighlighting)
                {
                    var scriptNonce = Guid.NewGuid().ToString("N");
                    var csp = "default-src 'none'; " +
                              "img-src data: http: https: file:; " +
                              "style-src 'unsafe-inline' https://cdnjs.cloudflare.com; " +
                              "script-src 'nonce-" + scriptNonce + "' https://cdnjs.cloudflare.com; " +
                              "font-src data: https://cdnjs.cloudflare.com; " +
                              "object-src 'none'; frame-src 'none'; base-uri 'none'; form-action 'none';";

                    highlightHeader =
                        "<meta http-equiv='Content-Security-Policy' content=\"" + csp + "\">\n" +
                        "<!-- Highlight.js per syntax highlighting -->\n" +
                        "<link rel='stylesheet' href='https://cdnjs.cloudflare.com/ajax/libs/highlight.js/11.9.0/styles/" + highlightTheme + ".min.css'>\n" +
                        "<script nonce='" + scriptNonce + "' src='https://cdnjs.cloudflare.com/ajax/libs/highlight.js/11.9.0/highlight.min.js'></script>";

                    highlightScript = @"
<script nonce='" + scriptNonce + @"'>
// Applica syntax highlighting a tutti i blocchi di codice
document.addEventListener('DOMContentLoaded', function() {
    document.querySelectorAll('pre code').forEach(function(block) {
        hljs.highlightElement(block);
    });
});
// Fallback se DOMContentLoaded è già passato
if (document.readyState !== 'loading') {
    document.querySelectorAll('pre code').forEach(function(block) {
        hljs.highlightElement(block);
    });
}
</script>";
                }
                else
                {
                    var csp = "default-src 'none'; " +
                              "img-src data: http: https: file:; " +
                              "style-src 'unsafe-inline'; " +
                              "script-src 'none'; " +
                              "object-src 'none'; frame-src 'none'; base-uri 'none'; form-action 'none';";
                    highlightHeader = "<meta http-equiv='Content-Security-Policy' content=\"" + csp + "\">";
                }

                string full = @"<!doctype html>
<html>
<head>
<meta charset='utf-8'>
<title>" + safeTitle + @"</title>
" + highlightHeader + @"
<style>
body {
    font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Helvetica, Arial, sans-serif;
    padding: 1rem;
    background: white;
    color: #24292f;
    transition: background-color 0.3s, color 0.3s;
}
.markdown-body {
    max-width: 900px;
    margin: auto;
}
pre {
    background: #f6f8fa;
    padding: 0;
    overflow: auto;
    border-radius: 6px;
    border: 1px solid #e1e4e8;
}
pre code {
    display: block;
    padding: 16px;
    overflow-x: auto;
    background: transparent;
}
code {
    background: rgba(27,31,35,.05);
    padding: .2em .4em;
    border-radius: 6px;
    font-size: 85%;
    font-family: 'SFMono-Regular', Consolas, 'Liberation Mono', Menlo, monospace;
}
pre code.hljs {
    padding: 16px;
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
table {
    border-collapse: collapse;
    width: 100%;
    margin: 1em 0;
}
th, td {
    border: 1px solid #e1e4e8;
    padding: 8px 12px;
    text-align: left;
}
th {
    background: #f6f8fa;
    font-weight: 600;
}
a {
    color: #0366d6;
    text-decoration: none;
}
a:hover {
    text-decoration: underline;
}
img {
    max-width: 100%;
    height: auto;
}
hr {
    border: none;
    height: 1px;
    background-color: #e1e4e8;
    margin: 1.5em 0;
}
/* Tema dinamico */
" + themeCss + @"
/* Stile per stampa */
@media print {
    body {
        background: white !important;
        color: black !important;
    }
    pre {
        background: #f6f8fa !important;
        border: 1px solid #ccc !important;
    }
    pre code {
        white-space: pre-wrap !important;
        word-break: break-word !important;
    }
}
</style>
</head>
<body class='markdown-body'>
" + htmlBody + @"
" + highlightScript + @"
</body>
</html>";

                Viewer.CoreWebView2.NavigateToString(full);
                UpdateButtonStates();

                // Aggiungi ai file recenti
                RecentFilesManager.Instance.AddFile(path);
            }
            catch (Exception ex)
            {
                ShowStatus(string.Format(Properties.Resources.LoadingFileError, ex.Message), true);
            }
            finally
            {
                ShowLoading(false);
                _loadFileSemaphore.Release();
            }
        }

        /// <summary>
        /// Configura il FileSystemWatcher per rilevare modifiche al file aperto.
        /// </summary>
        private void SetupFileWatcher(string filePath)
        {
            // Rimuovi watcher precedente
            DisposeFileWatcher();

            if (!_enableAutoReload)
            {
                System.Diagnostics.Debug.WriteLine("Auto-reload disabilitato: watcher non attivato");
                return;
            }

            try
            {
                var directory = Path.GetDirectoryName(filePath);
                var fileName = Path.GetFileName(filePath);

                if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(fileName))
                    return;

                _fileWatcher = new FileSystemWatcher
                {
                    Path = directory,
                    Filter = fileName,
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size
                };

                _fileWatcher.Changed += OnFileChanged;
                _fileWatcher.EnableRaisingEvents = true;

                System.Diagnostics.Debug.WriteLine($"FileWatcher attivato per: {filePath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Errore setup FileWatcher: {ex.Message}");
            }
        }

        /// <summary>
        /// Gestisce l'evento di modifica del file.
        /// </summary>
        private async void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            if (!_enableAutoReload)
                return;

            // Debounce: ignora eventi troppo ravvicinati (il sistema può generarne multipli)
            var now = DateTime.Now;
            if ((now - _lastFileChangeTime).TotalMilliseconds < 500)
                return;

            _lastFileChangeTime = now;

            // Attendi un attimo per evitare conflitti di scrittura
            await Task.Delay(200);

            // Ricarica il file sul thread UI
            await Dispatcher.InvokeAsync(() =>
            {
                if (!string.IsNullOrEmpty(currentPath) && File.Exists(currentPath))
                {
                    System.Diagnostics.Debug.WriteLine($"Auto-reload: {currentPath}");
                    return LoadFileAsync(currentPath);
                }
                return Task.CompletedTask;
            }).Task.Unwrap();
        }

        private void DisposeFileWatcher()
        {
            if (_fileWatcher == null)
                return;

            _fileWatcher.EnableRaisingEvents = false;
            _fileWatcher.Changed -= OnFileChanged;
            _fileWatcher.Dispose();
            _fileWatcher = null;
        }

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            SaveWindowBounds();
            DisposeFileWatcher();

            ThemeManager.Instance.ThemeChanged -= OnThemeChanged;
            RecentFilesManager.Instance.RecentFilesChanged -= OnRecentFilesChanged;
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
