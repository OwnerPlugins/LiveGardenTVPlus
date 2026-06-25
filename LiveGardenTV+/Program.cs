using System.IO;
using System.Windows;

namespace LiveGardenTVPlus
{
    public static class Program
    {
        [STAThread]
        public static void Main()
        {
            // Usa C:\temp se possibile, altrimenti la cartella dell'app
            string logDir = @"C:\temp";
            string logFile = Path.Combine(logDir, "LiveGarden_startup.log");

            try
            {
                Directory.CreateDirectory(logDir);
            }
            catch
            {
                // Fallback: usa la cartella dell'app
                logDir = AppDomain.CurrentDomain.BaseDirectory;
                logFile = Path.Combine(logDir, "startup.log");
                Directory.CreateDirectory(logDir);
            }

            try
            {
                File.WriteAllText(logFile, $"{DateTime.Now}: Avvio\n");

                var app = new App();
                app.Run(new MainWindow());

                File.AppendAllText(logFile, $"{DateTime.Now}: Uscita normale\n");
            }
            catch (Exception ex)
            {
                File.AppendAllText(logFile, $"{DateTime.Now}: CRASH - {ex}\n");
                MessageBox.Show($"Errore grave:\n{ex.Message}\n\nLog: {logFile}", "Errore", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}