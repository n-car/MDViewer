using System;
using System.Windows;
using System.Windows.Controls;

namespace it.carpanese.utilities.MDViewer
{
    /// <summary>
    /// Finestra delle impostazioni dell'applicazione.
    /// </summary>
    public partial class SettingsWindow : Window
    {
        private bool _settingsChanged = false;
        private MarkdownProvider _selectedDefaultProvider = MarkdownProvider.Markdig;

        public SettingsWindow()
        {
            InitializeComponent();
            LoadSettings();
        }

        /// <summary>
        /// Carica le impostazioni correnti nei controlli.
        /// </summary>
        private void LoadSettings()
        {
            var settings = AppSettings.Instance;

            // Aggiornamenti
            SelectComboBoxByTag(CmbUpdateMode, settings.UpdateCheckMode.ToString());

            // Rendering
            _selectedDefaultProvider = settings.DefaultProvider;
            UpdateDefaultProviderDisplay();
            ChkSyntaxHighlighting.IsChecked = settings.EnableSyntaxHighlighting;

            // Tema
            SelectComboBoxByTag(CmbTheme, settings.ThemePreference.ToString());

            // File Recenti
            SelectComboBoxByTag(CmbMaxRecentFiles, settings.MaxRecentFiles.ToString());

            // Cache
            SelectComboBoxByTag(CmbCacheSize, settings.MaxCacheSizeMB.ToString());
            SelectComboBoxByTag(CmbCacheDuration, settings.CacheDurationDays.ToString());
            UpdateCacheInfo();

            // Comportamento
            ChkAutoReload.IsChecked = settings.EnableAutoReload;
            ChkRememberPosition.IsChecked = settings.RememberWindowPosition;

            // Info
            TxtVersion.Text = Localizer.Format("SettingsVersionFormat", UpdateManager.Instance.CurrentVersionString);
        }

        /// <summary>
        /// Salva le impostazioni dai controlli.
        /// </summary>
        private void SaveSettings()
        {
            var settings = AppSettings.Instance;

            // Aggiornamenti
            if (CmbUpdateMode.SelectedItem is ComboBoxItem ucmItem && ucmItem.Tag is string ucmTag)
            {
                if (Enum.TryParse<UpdateCheckMode>(ucmTag, out var ucm))
                    settings.UpdateCheckMode = ucm;
            }

            // Rendering
            settings.DefaultProvider = _selectedDefaultProvider;
            settings.EnableSyntaxHighlighting = ChkSyntaxHighlighting.IsChecked == true;

            // Tema
            if (CmbTheme.SelectedItem is ComboBoxItem thItem && thItem.Tag is string thTag)
            {
                if (Enum.TryParse<ThemePreference>(thTag, out var tp))
                {
                    settings.ThemePreference = tp;
                    ApplyTheme(tp);
                }
            }

            // File Recenti
            if (CmbMaxRecentFiles.SelectedItem is ComboBoxItem mrfItem && mrfItem.Tag is string mrfTag)
            {
                if (int.TryParse(mrfTag, out var mrf))
                    settings.MaxRecentFiles = mrf;
            }

            // Cache
            if (CmbCacheSize.SelectedItem is ComboBoxItem csItem && csItem.Tag is string csTag)
            {
                if (int.TryParse(csTag, out var cs))
                    settings.MaxCacheSizeMB = cs;
            }
            if (CmbCacheDuration.SelectedItem is ComboBoxItem cdItem && cdItem.Tag is string cdTag)
            {
                if (int.TryParse(cdTag, out var cd))
                    settings.CacheDurationDays = cd;
            }

            // Comportamento
            settings.EnableAutoReload = ChkAutoReload.IsChecked == true;
            settings.RememberWindowPosition = ChkRememberPosition.IsChecked == true;

            settings.Save();
            _settingsChanged = true;
        }

        /// <summary>
        /// Applica il tema selezionato.
        /// </summary>
        private void ApplyTheme(ThemePreference preference)
        {
            switch (preference)
            {
                case ThemePreference.System:
                    ThemeManager.Instance.SetTheme(AppTheme.System);
                    break;
                case ThemePreference.Light:
                    ThemeManager.Instance.SetTheme(AppTheme.Light);
                    break;
                case ThemePreference.Dark:
                    ThemeManager.Instance.SetTheme(AppTheme.Dark);
                    break;
            }
        }

        /// <summary>
        /// Aggiorna testo e tooltip del provider predefinito.
        /// </summary>
        private void UpdateDefaultProviderDisplay()
        {
            TxtDefaultProvider.Text = MarkdownProviderInfo.GetDisplayName(_selectedDefaultProvider);
            BtnChooseDefaultProvider.ToolTip = Localizer.Format(
                "ProviderTooltipFormat",
                MarkdownProviderInfo.GetShortDescription(_selectedDefaultProvider),
                Localizer.Get("MainClickToChangeProvider"));
        }

        /// <summary>
        /// Seleziona un item nella ComboBox in base al Tag.
        /// </summary>
        private void SelectComboBoxByTag(ComboBox comboBox, string tag)
        {
            foreach (ComboBoxItem item in comboBox.Items)
            {
                if (item.Tag is string itemTag && itemTag == tag)
                {
                    comboBox.SelectedItem = item;
                    return;
                }
            }
            // Default: seleziona il primo
            if (comboBox.Items.Count > 0)
                comboBox.SelectedIndex = 0;
        }

        /// <summary>
        /// Aggiorna le informazioni sulla cache.
        /// </summary>
        private void UpdateCacheInfo()
        {
            try
            {
                var cacheFolder = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "MDViewer", "Cache");

                if (System.IO.Directory.Exists(cacheFolder))
                {
                    long totalSize = 0;
                    int fileCount = 0;
                    foreach (var file in System.IO.Directory.GetFiles(cacheFolder))
                    {
                        var fi = new System.IO.FileInfo(file);
                        totalSize += fi.Length;
                        fileCount++;
                    }

                    double sizeMB = totalSize / (1024.0 * 1024.0);
                    TxtCacheInfo.Text = Localizer.Format("SettingsCacheInfoFormat", fileCount, sizeMB);
                }
                else
                {
                    TxtCacheInfo.Text = Localizer.Get("SettingsCacheEmpty");
                }
            }
            catch
            {
                TxtCacheInfo.Text = Localizer.Get("SettingsNotAvailableShort");
            }
        }

        // ========== EVENT HANDLERS ==========

        private void BtnChooseDefaultProvider_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ProviderSelectionWindow(_selectedDefaultProvider)
            {
                Owner = this
            };

            if (dialog.ShowDialog() != true)
                return;

            _selectedDefaultProvider = dialog.SelectedProvider;
            UpdateDefaultProviderDisplay();
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            SaveSettings();
            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void BtnClearRecentFiles_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                Localizer.Get("SettingsClearRecentFilesConfirm"),
                Localizer.Get("ConfirmTitle"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                RecentFilesManager.Instance.Clear();
                MessageBox.Show(Localizer.Get("SettingsRecentFilesCleared"), Localizer.Get("CompletedTitle"), 
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnClearCache_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                Localizer.Get("SettingsClearCacheConfirm"),
                Localizer.Get("ConfirmTitle"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    var cacheFolder = System.IO.Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "MDViewer", "Cache");

                    if (System.IO.Directory.Exists(cacheFolder))
                    {
                        foreach (var file in System.IO.Directory.GetFiles(cacheFolder))
                        {
                            try { System.IO.File.Delete(file); } catch { }
                        }
                    }

                    UpdateCacheInfo();
                    MessageBox.Show(Localizer.Get("SettingsCacheCleared"), Localizer.Get("CompletedTitle"), 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(Localizer.Format("SettingsClearCacheErrorFormat", ex.Message), 
                        Properties.Resources.Error, MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnGitHub_Click(object sender, RoutedEventArgs e)
        {
            UpdateManager.Instance.OpenProjectPage();
        }

        private void BtnResetDefaults_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                Localizer.Get("SettingsResetDefaultsConfirm"),
                Localizer.Get("ConfirmTitle"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                AppSettings.Instance.ResetToDefaults();
                LoadSettings();
                MessageBox.Show(Localizer.Get("SettingsDefaultsRestored"), Localizer.Get("CompletedTitle"), 
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        /// <summary>
        /// True se le impostazioni sono state modificate e salvate.
        /// </summary>
        public bool SettingsChanged => _settingsChanged;
    }
}
