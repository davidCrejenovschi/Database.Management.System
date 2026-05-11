using System.Windows;
using BibliotecaApp.DataBase; // Asigură-te că namespace-ul coincide cu cel din DemoSimulator.cs

namespace BibliotecaApp
{
    public partial class Lab5Window : Window
    {
        public Lab5Window()
        {
            InitializeComponent();
        }

        private void StartDemo_Click(object sender, RoutedEventArgs e)
        {
            // Dezactivăm butonul temporar pentru a nu rula de mai multe ori simultan
            if (sender is System.Windows.Controls.Button btn) btn.IsEnabled = false;

            // Rulăm simularea creată anterior
            DemoSimulator.RunFullDemonstration();

            MessageBox.Show("Demonstration completed! Check the console window for logs.", "Success");

            if (sender is System.Windows.Controls.Button btnEnable) btnEnable.IsEnabled = true;
        }
    }
}