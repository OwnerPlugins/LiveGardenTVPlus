using System.IO;

namespace LiveGardenTVPlus.Services
{
    public static class Logger
    {
        private static readonly string _installDir = AppDomain.CurrentDomain.BaseDirectory;
        private static readonly string _logDirectory = Path.Combine(_installDir, "Logs");
        private static readonly string _fallbackDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LiveGardenTVPlus",
            "Logs");

        public static string LogPath => _logDirectory;

        public static void Write(string message, string stackTrace = null)
        {
            try
            {
                string directory = GetWritableDirectory();
                string logFile = Path.Combine(directory, $"log_{DateTime.Now:yyyy-MM-dd}.log");
                string entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
                if (!string.IsNullOrEmpty(stackTrace))
                    entry += $"\nStackTrace: {stackTrace}";
                File.AppendAllText(logFile, entry + "\n\n");
            }
            catch { /* Ignore logging errors */ }
        }

        private static string GetWritableDirectory()
        {
            // Try install directory first
            try
            {
                if (!Directory.Exists(_logDirectory))
                    Directory.CreateDirectory(_logDirectory);
                // Test write permissions
                string testFile = Path.Combine(_logDirectory, "write_test.tmp");
                File.WriteAllText(testFile, "test");
                File.Delete(testFile);
                return _logDirectory;
            }
            catch
            {
                // Fallback to AppData
                if (!Directory.Exists(_fallbackDirectory))
                    Directory.CreateDirectory(_fallbackDirectory);
                return _fallbackDirectory;
            }
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