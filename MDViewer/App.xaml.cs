using System;
using System.Globalization;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using Res = it.carpanese.utilities.MDViewer.Properties.Resources;

namespace it.carpanese.utilities.MDViewer
{
    public partial class App : Application
    {
        public App()
        {
            // registro subito gli handler globali
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        }

        private void App_Startup(object sender, StartupEventArgs e)
        {
            ConfigureUiCulture();
            System.Diagnostics.Debug.WriteLine("Startup args: " + string.Join(" | ", e.Args));
            try
            {
                // Inizializza il ThemeManager con la preferenza tema salvata
                var preference = AppSettings.Instance.ThemePreference;
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

                var main = new MainWindow(e.Args);
                main.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    Localizer.Format("AppMainWindowOpenError", ex),
                    Localizer.Get("AppFatalErrorTitle"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Shutdown(-1);
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // Cleanup del ThemeManager
            ThemeManager.Instance.Dispose();
            base.OnExit(e);
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
                MessageBox.Show(
                    Localizer.Format("AppUnhandledUiException", e.Exception),
                    Res.Error,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            e.Handled = true;
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var ex = e.ExceptionObject as Exception;
            MessageBox.Show(
                Localizer.Format("AppUnhandledDomainException", ex),
                Res.Error,
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }

        private static void ConfigureUiCulture()
        {
            var systemUiCulture = CultureInfo.InstalledUICulture;
            var targetCulture = string.Equals(
                systemUiCulture.TwoLetterISOLanguageName,
                "it",
                StringComparison.OrdinalIgnoreCase)
                ? CultureInfo.GetCultureInfo("it-IT")
                : CultureInfo.GetCultureInfo("en");

            Res.Culture = targetCulture;
            CultureInfo.DefaultThreadCurrentUICulture = targetCulture;
            Thread.CurrentThread.CurrentUICulture = targetCulture;
        }
    }
}
