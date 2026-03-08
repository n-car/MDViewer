using System;
using System.IO;

namespace it.carpanese.utilities.MDViewer
{
    /// <summary>
    /// Modalità di controllo aggiornamenti.
    /// </summary>
    public enum UpdateCheckMode
    {
        /// <summary>
        /// Solo manuale - l'utente deve cliccare il pulsante.
        /// </summary>
        Manual,

        /// <summary>
        /// Automatico una volta al giorno alla prima apertura.
        /// </summary>
        Daily,

        /// <summary>
        /// Automatico una volta a settimana alla prima apertura.
        /// </summary>
        Weekly
    }

    /// <summary>
    /// Preferenza tema dell'applicazione.
    /// </summary>
    public enum ThemePreference
    {
        /// <summary>
        /// Segue il tema di Windows.
        /// </summary>
        System,

        /// <summary>
        /// Sempre tema chiaro.
        /// </summary>
        Light,

        /// <summary>
        /// Sempre tema scuro.
        /// </summary>
        Dark
    }

    /// <summary>
    /// Gestisce le impostazioni dell'applicazione con persistenza su file.
    /// </summary>
    public class AppSettings
    {
        private static AppSettings _instance;
        public static AppSettings Instance => _instance ?? (_instance = new AppSettings());

        private readonly string _settingsFilePath;

        // ========== AGGIORNAMENTI ==========
        
        /// <summary>
        /// Modalità controllo aggiornamenti.
        /// </summary>
        public UpdateCheckMode UpdateCheckMode { get; set; } = UpdateCheckMode.Manual;

        /// <summary>
        /// Data ultimo controllo aggiornamenti.
        /// </summary>
        public DateTime LastUpdateCheck { get; set; } = DateTime.MinValue;

        // ========== RENDERING ==========
        
        /// <summary>
        /// Provider di rendering predefinito.
        /// </summary>
        public MarkdownProvider DefaultProvider { get; set; } = MarkdownProvider.Markdig;

        /// <summary>
        /// Abilita syntax highlighting per i blocchi di codice.
        /// </summary>
        public bool EnableSyntaxHighlighting { get; set; } = true;

        // ========== TEMA ==========
        
        /// <summary>
        /// Preferenza tema.
        /// </summary>
        public ThemePreference ThemePreference { get; set; } = ThemePreference.System;

        // ========== FILE RECENTI ==========
        
        /// <summary>
        /// Numero massimo di file recenti da memorizzare.
        /// </summary>
        public int MaxRecentFiles { get; set; } = 10;

        // ========== CACHE ==========
        
        /// <summary>
        /// Dimensione massima cache in MB.
        /// </summary>
        public int MaxCacheSizeMB { get; set; } = 50;

        /// <summary>
        /// Durata cache in giorni.
        /// </summary>
        public int CacheDurationDays { get; set; } = 7;

        // ========== COMPORTAMENTO ==========
        
        /// <summary>
        /// Abilita auto-reload quando il file viene modificato.
        /// </summary>
        public bool EnableAutoReload { get; set; } = true;

        /// <summary>
        /// Ricorda posizione e dimensione finestra.
        /// </summary>
        public bool RememberWindowPosition { get; set; } = true;

        /// <summary>
        /// Posizione X della finestra.
        /// </summary>
        public double WindowLeft { get; set; } = double.NaN;

        /// <summary>
        /// Posizione Y della finestra.
        /// </summary>
        public double WindowTop { get; set; } = double.NaN;

        /// <summary>
        /// Larghezza finestra.
        /// </summary>
        public double WindowWidth { get; set; } = 800;

        /// <summary>
        /// Altezza finestra.
        /// </summary>
        public double WindowHeight { get; set; } = 600;

        /// <summary>
        /// Finestra massimizzata.
        /// </summary>
        public bool WindowMaximized { get; set; } = false;

        // ========== COSTRUTTORE ==========

        private AppSettings()
        {
            string appDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MDViewer");

            Directory.CreateDirectory(appDataFolder);
            _settingsFilePath = Path.Combine(appDataFolder, "settings.ini");

            Load();
        }

        // ========== METODI ==========

        /// <summary>
        /// Carica le impostazioni dal file.
        /// </summary>
        public void Load()
        {
            try
            {
                if (!File.Exists(_settingsFilePath))
                {
                    System.Diagnostics.Debug.WriteLine("File impostazioni non trovato, uso default");
                    return;
                }

                var lines = File.ReadAllLines(_settingsFilePath);
                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#") || !line.Contains("="))
                        continue;

                    var parts = line.Split(new[] { '=' }, 2);
                    if (parts.Length != 2)
                        continue;

                    var key = parts[0].Trim();
                    var value = parts[1].Trim();

                    SetProperty(key, value);
                }

                System.Diagnostics.Debug.WriteLine("Impostazioni caricate");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Errore caricamento impostazioni: {ex.Message}");
            }
        }

        /// <summary>
        /// Salva le impostazioni su file.
        /// </summary>
        public void Save()
        {
            try
            {
                var lines = new[]
                {
                    "# MDViewer Settings",
                    $"# Salvato: {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                    "",
                    "# Aggiornamenti",
                    $"UpdateCheckMode={UpdateCheckMode}",
                    $"LastUpdateCheck={LastUpdateCheck:o}",
                    "",
                    "# Rendering",
                    $"DefaultProvider={DefaultProvider}",
                    $"EnableSyntaxHighlighting={EnableSyntaxHighlighting}",
                    "",
                    "# Tema",
                    $"ThemePreference={ThemePreference}",
                    "",
                    "# File Recenti",
                    $"MaxRecentFiles={MaxRecentFiles}",
                    "",
                    "# Cache",
                    $"MaxCacheSizeMB={MaxCacheSizeMB}",
                    $"CacheDurationDays={CacheDurationDays}",
                    "",
                    "# Comportamento",
                    $"EnableAutoReload={EnableAutoReload}",
                    $"RememberWindowPosition={RememberWindowPosition}",
                    $"WindowLeft={WindowLeft}",
                    $"WindowTop={WindowTop}",
                    $"WindowWidth={WindowWidth}",
                    $"WindowHeight={WindowHeight}",
                    $"WindowMaximized={WindowMaximized}"
                };

                File.WriteAllLines(_settingsFilePath, lines);
                System.Diagnostics.Debug.WriteLine("Impostazioni salvate");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Errore salvataggio impostazioni: {ex.Message}");
            }
        }

        /// <summary>
        /// Imposta una proprietà dal valore stringa.
        /// </summary>
        private void SetProperty(string key, string value)
        {
            try
            {
                switch (key)
                {
                    case "UpdateCheckMode":
                        if (Enum.TryParse<UpdateCheckMode>(value, out var ucm))
                            UpdateCheckMode = ucm;
                        break;

                    case "LastUpdateCheck":
                        if (DateTime.TryParse(value, out var luc))
                            LastUpdateCheck = luc;
                        break;

                    case "DefaultProvider":
                        if (Enum.TryParse<MarkdownProvider>(value, out var dp))
                            DefaultProvider = dp;
                        break;

                    case "EnableSyntaxHighlighting":
                        if (bool.TryParse(value, out var esh))
                            EnableSyntaxHighlighting = esh;
                        break;

                    case "ThemePreference":
                        if (Enum.TryParse<ThemePreference>(value, out var tp))
                            ThemePreference = tp;
                        break;

                    case "MaxRecentFiles":
                        if (int.TryParse(value, out var mrf))
                            MaxRecentFiles = mrf;
                        break;

                    case "MaxCacheSizeMB":
                        if (int.TryParse(value, out var mcs))
                            MaxCacheSizeMB = mcs;
                        break;

                    case "CacheDurationDays":
                        if (int.TryParse(value, out var cdd))
                            CacheDurationDays = cdd;
                        break;

                    case "EnableAutoReload":
                        if (bool.TryParse(value, out var ear))
                            EnableAutoReload = ear;
                        break;

                    case "RememberWindowPosition":
                        if (bool.TryParse(value, out var rwp))
                            RememberWindowPosition = rwp;
                        break;

                    case "WindowLeft":
                        if (double.TryParse(value, out var wl))
                            WindowLeft = wl;
                        break;

                    case "WindowTop":
                        if (double.TryParse(value, out var wt))
                            WindowTop = wt;
                        break;

                    case "WindowWidth":
                        if (double.TryParse(value, out var ww))
                            WindowWidth = ww;
                        break;

                    case "WindowHeight":
                        if (double.TryParse(value, out var wh))
                            WindowHeight = wh;
                        break;

                    case "WindowMaximized":
                        if (bool.TryParse(value, out var wm))
                            WindowMaximized = wm;
                        break;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Errore parsing {key}: {ex.Message}");
            }
        }

        /// <summary>
        /// Verifica se è il momento di controllare aggiornamenti in base alle impostazioni.
        /// </summary>
        public bool ShouldCheckForUpdates()
        {
            switch (UpdateCheckMode)
            {
                case UpdateCheckMode.Manual:
                    return false;

                case UpdateCheckMode.Daily:
                    return (DateTime.Now - LastUpdateCheck).TotalDays >= 1;

                case UpdateCheckMode.Weekly:
                    return (DateTime.Now - LastUpdateCheck).TotalDays >= 7;

                default:
                    return false;
            }
        }

        /// <summary>
        /// Aggiorna la data dell'ultimo controllo aggiornamenti.
        /// </summary>
        public void MarkUpdateChecked()
        {
            LastUpdateCheck = DateTime.Now;
            Save();
        }

        /// <summary>
        /// Ripristina le impostazioni predefinite.
        /// </summary>
        public void ResetToDefaults()
        {
            UpdateCheckMode = UpdateCheckMode.Manual;
            LastUpdateCheck = DateTime.MinValue;
            DefaultProvider = MarkdownProvider.Markdig;
            EnableSyntaxHighlighting = true;
            ThemePreference = ThemePreference.System;
            MaxRecentFiles = 10;
            MaxCacheSizeMB = 50;
            CacheDurationDays = 7;
            EnableAutoReload = true;
            RememberWindowPosition = true;
            WindowLeft = double.NaN;
            WindowTop = double.NaN;
            WindowWidth = 800;
            WindowHeight = 600;
            WindowMaximized = false;

            Save();
        }
    }
}
