using Microsoft.Win32;
using System;
using System.Windows;
using System.Windows.Media;

namespace it.carpanese.utilities.MDViewer
{
    /// <summary>
    /// Tipi di tema disponibili.
    /// </summary>
    public enum AppTheme
    {
        Light,
        Dark,
        System // Segue il tema di Windows
    }

    /// <summary>
    /// Gestisce i temi dell'applicazione con supporto per il rilevamento automatico del tema Windows.
    /// </summary>
    public class ThemeManager
    {
        private static ThemeManager _instance;
        public static ThemeManager Instance => _instance ?? (_instance = new ThemeManager());

        /// <summary>
        /// Evento fired quando il tema cambia.
        /// </summary>
        public event EventHandler<AppTheme> ThemeChanged;

        /// <summary>
        /// Tema corrente effettivo (Light o Dark).
        /// </summary>
        public AppTheme CurrentTheme { get; private set; } = AppTheme.Light;

        /// <summary>
        /// Preferenza utente (può essere System).
        /// </summary>
        public AppTheme UserPreference { get; private set; } = AppTheme.System;

        /// <summary>
        /// True se il tema corrente è scuro.
        /// </summary>
        public bool IsDarkTheme => CurrentTheme == AppTheme.Dark;

        private ThemeManager()
        {
            // Registra listener per cambio tema Windows
            SystemEvents.UserPreferenceChanged += OnSystemThemeChanged;
        }

        /// <summary>
        /// Inizializza il tema basandosi sulle preferenze di sistema.
        /// </summary>
        public void Initialize()
        {
            SetTheme(AppTheme.System);
        }

        /// <summary>
        /// Imposta il tema dell'applicazione.
        /// </summary>
        public void SetTheme(AppTheme theme)
        {
            UserPreference = theme;

            AppTheme effectiveTheme;
            if (theme == AppTheme.System)
            {
                effectiveTheme = IsWindowsDarkTheme() ? AppTheme.Dark : AppTheme.Light;
            }
            else
            {
                effectiveTheme = theme;
            }

            if (CurrentTheme != effectiveTheme)
            {
                CurrentTheme = effectiveTheme;
                ApplyTheme(effectiveTheme);
                ThemeChanged?.Invoke(this, effectiveTheme);
            }
        }

        /// <summary>
        /// Alterna tra tema chiaro e scuro.
        /// </summary>
        public void ToggleTheme()
        {
            SetTheme(CurrentTheme == AppTheme.Light ? AppTheme.Dark : AppTheme.Light);
        }

        /// <summary>
        /// Rileva se Windows è in modalità tema scuro.
        /// </summary>
        public bool IsWindowsDarkTheme()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    if (key != null)
                    {
                        var value = key.GetValue("AppsUseLightTheme");
                        if (value != null)
                        {
                            return (int)value == 0; // 0 = Dark, 1 = Light
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Errore lettura tema Windows: {ex.Message}");
            }

            return false; // Default: tema chiaro
        }

        /// <summary>
        /// Applica il tema alle risorse dell'applicazione.
        /// </summary>
        private void ApplyTheme(AppTheme theme)
        {
            var app = Application.Current;
            if (app == null) return;

            // Rimuovi tema precedente
            var toRemove = new System.Collections.Generic.List<ResourceDictionary>();
            foreach (var dict in app.Resources.MergedDictionaries)
            {
                if (dict.Source != null && 
                    (dict.Source.ToString().Contains("LightTheme") || 
                     dict.Source.ToString().Contains("DarkTheme")))
                {
                    toRemove.Add(dict);
                }
            }
            foreach (var dict in toRemove)
            {
                app.Resources.MergedDictionaries.Remove(dict);
            }

            // Aggiungi nuovo tema
            var themePath = theme == AppTheme.Dark 
                ? "Themes/DarkTheme.xaml" 
                : "Themes/LightTheme.xaml";

            try
            {
                var themeUri = new Uri(themePath, UriKind.Relative);
                var themeDict = new ResourceDictionary { Source = themeUri };
                app.Resources.MergedDictionaries.Add(themeDict);
                System.Diagnostics.Debug.WriteLine($"Tema applicato: {theme}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Errore caricamento tema: {ex.Message}");
                // Applica colori di fallback direttamente
                ApplyFallbackColors(theme);
            }
        }

        /// <summary>
        /// Applica colori di fallback se il file XAML non è disponibile.
        /// </summary>
        private void ApplyFallbackColors(AppTheme theme)
        {
            var app = Application.Current;
            if (app == null) return;

            if (theme == AppTheme.Dark)
            {
                app.Resources["WindowBackground"] = new SolidColorBrush(Color.FromRgb(30, 30, 30));
                app.Resources["ToolbarBackground"] = new SolidColorBrush(Color.FromRgb(45, 45, 45));
                app.Resources["TextForeground"] = new SolidColorBrush(Color.FromRgb(224, 224, 224));
                app.Resources["TextBoxBackground"] = new SolidColorBrush(Color.FromRgb(60, 60, 60));
                app.Resources["BorderColor"] = new SolidColorBrush(Color.FromRgb(64, 64, 64));
                app.Resources["AccentColor"] = new SolidColorBrush(Color.FromRgb(33, 150, 243));
            }
            else
            {
                app.Resources["WindowBackground"] = new SolidColorBrush(Colors.White);
                app.Resources["ToolbarBackground"] = new SolidColorBrush(Color.FromRgb(245, 245, 245));
                app.Resources["TextForeground"] = new SolidColorBrush(Color.FromRgb(66, 66, 66));
                app.Resources["TextBoxBackground"] = new SolidColorBrush(Colors.White);
                app.Resources["BorderColor"] = new SolidColorBrush(Color.FromRgb(224, 224, 224));
                app.Resources["AccentColor"] = new SolidColorBrush(Color.FromRgb(33, 150, 243));
            }
        }

        /// <summary>
        /// Gestisce il cambio tema di sistema Windows.
        /// </summary>
        private void OnSystemThemeChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            if (e.Category == UserPreferenceCategory.General && UserPreference == AppTheme.System)
            {
                // Esegui sul thread UI
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    SetTheme(AppTheme.System);
                });
            }
        }

        /// <summary>
        /// Restituisce il CSS per il tema corrente (per il rendering HTML).
        /// </summary>
        public string GetHtmlThemeCss()
        {
            if (IsDarkTheme)
            {
                return @"
                    body {
                        background: #1e1e1e !important;
                        color: #e0e0e0 !important;
                    }
                    pre {
                        background: #2d2d2d !important;
                        border-color: #404040 !important;
                    }
                    code {
                        background: rgba(255,255,255,.1) !important;
                    }
                    h1, h2, h3, h4 {
                        border-bottom-color: #404040 !important;
                        color: #ffffff !important;
                    }
                    blockquote {
                        color: #9e9e9e !important;
                        border-left-color: #505050 !important;
                    }
                    a {
                        color: #64b5f6 !important;
                    }
                    table {
                        border-color: #404040 !important;
                    }
                    th, td {
                        border-color: #404040 !important;
                    }
                    th {
                        background: #2d2d2d !important;
                    }
                    hr {
                        background-color: #404040 !important;
                    }
                ";
            }
            else
            {
                return @"
                    body {
                        background: white;
                        color: #24292f;
                    }
                ";
            }
        }

        /// <summary>
        /// Cleanup quando l'app viene chiusa.
        /// </summary>
        public void Dispose()
        {
            SystemEvents.UserPreferenceChanged -= OnSystemThemeChanged;
        }
    }
}
