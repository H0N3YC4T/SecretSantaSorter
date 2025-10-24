// App.xaml.cs
using System.Windows;
// Aliases to clear ambiguity
using WpfMessageBox = System.Windows.MessageBox;
using WpfMessageApplication = System.Windows.Application;

namespace SecretSantaSorter
{
    // Fully-qualify the base type so there's no confusion with WinForms.Application
    public partial class App : WpfMessageApplication
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // Global UI exception handler (optional, nice to have)
            this.DispatcherUnhandledException += (_, args) =>
            {
                WpfMessageBox.Show(
                    args.Exception.ToString(),
                    Globals.Dialog.ErrorTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                args.Handled = true;
            };

            base.OnStartup(e);
        }
    }
}