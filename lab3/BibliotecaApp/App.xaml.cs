using System.Windows;

namespace BibliotecaApp
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            this.DispatcherUnhandledException += (s, args) =>
            {
                MessageBox.Show(args.Exception.ToString(), "Eroare Fatala");
                args.Handled = true;
            };

            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                MessageBox.Show(args.ExceptionObject.ToString(), "Eroare Fatala AppDomain");
            };
        }
    }
}