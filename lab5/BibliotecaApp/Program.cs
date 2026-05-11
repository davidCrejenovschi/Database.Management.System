using System.Windows;
using Microsoft.EntityFrameworkCore;


namespace BibliotecaApp
{
    public class Program
    {
        [STAThread] // Necesar pentru aplicații WPF
        public static void Main(string[] args)
        {
            // 1. Logica de pregătire a bazei de date (Consolă)
            Console.WriteLine("Checking database and applying migrations...");
            using (var context = new LibraryContext())
            {
                try
                {
                    context.Database.Migrate();
                    Console.WriteLine("Database is up to date.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Database error: {ex.Message}");
                }
            }

            // 2. Pornirea aplicației WPF
            Console.WriteLine("Launching UI...");
            var app = new Application();
            var window = new Lab5Window();
            app.Run(window);
        }
    }
}