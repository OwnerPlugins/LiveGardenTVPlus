using System.IO;
using System.Windows;

namespace LiveGardenTVPlus
{
    public static class Program
    {
        [STAThread]
        public static void Main()
        {
            string logDir = @"C:\temp";
            string logFile = Path.Combine(logDir, "LiveGarden_startup.log");

            try
            {
                Directory.CreateDirectory(logDir);
            }
            catch
            {
                logDir = AppDomain.CurrentDomain.BaseDirectory;
                logFile = Path.Combine(logDir, "startup.log");
                Directory.CreateDirectory(logDir);
            }

            try
            {
                File.WriteAllText(logFile, $"{DateTime.Now}: Application started\n");

                var app = new App();
                app.Run(new MainWindow());

                File.AppendAllText(logFile, $"{DateTime.Now}: Application exited normally\n");
            }
            catch (Exception ex)
            {
                File.AppendAllText(logFile, $"{DateTime.Now}: CRASH - {ex}\n");
                MessageBox.Show($"Fatal error:\n{ex.Message}\n\nLog: {logFile}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}