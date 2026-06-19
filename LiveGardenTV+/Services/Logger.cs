using System.IO;

namespace LiveGardenTVPlus.Services
{
    public static class Logger
    {
        private static readonly string LogDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LiveGardenTVPlus",
            "Logs");

        public static string LogPath => LogDirectory;

        public static void Write(string message, string stackTrace = null)
        {
            try
            {
                Directory.CreateDirectory(LogDirectory);
                string logFile = Path.Combine(LogDirectory, $"log_{DateTime.Now:yyyy-MM-dd}.log");
                string entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
                if (!string.IsNullOrEmpty(stackTrace))
                    entry += $"\nStackTrace: {stackTrace}";
                File.AppendAllText(logFile, entry + "\n\n");
            }
            catch { }
        }

        public static void WriteException(Exception ex, string context = null)
        {
            string msg = $"Exception: {ex.GetType().FullName}";
            if (!string.IsNullOrEmpty(context))
                msg += $" - {context}";
            Write(msg, ex.ToString());
        }

        public static void Info(string message) => Write($"INFO: {message}");
        public static void Error(string message) => Write($"ERROR: {message}");
        public static void Success(string message) => Write($"SUCCESS: {message}");
    }
}