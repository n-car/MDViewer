using System;
using System.Windows;
using System.Windows.Threading;

namespace MDViewer
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
            System.Diagnostics.Debug.WriteLine("Startup args: " + string.Join(" | ", e.Args));
            try
            {
                var main = new MainWindow(e.Args);
                main.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Errore durante l'apertura della finestra principale:\n" + ex, "Errore fatale", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown(-1);
            }
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            MessageBox.Show("Unhandled UI exception:\n" + e.Exception, "Errore", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var ex = e.ExceptionObject as Exception;
            MessageBox.Show("Unhandled domain exception:\n" + ex, "Errore", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
