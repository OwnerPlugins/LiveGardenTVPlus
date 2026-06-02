using System.Diagnostics;
using System.IO;
using System.IO.Compression;

namespace LiveGardenTVPlus.Updater;

class Program
{
    static void Main(string[] args)
    {
        if (args.Length < 3)
        {
            Console.WriteLine("ERROR: Missing arguments. Usage: updater.exe <zipPath> <extractPath> <mainExePath>");
            return;
        }

        string zipPath = args[0];
        string extractPath = args[1];
        string mainExePath = args[2];

        string processName = Path.GetFileNameWithoutExtension(mainExePath);

        // Wait for main process to exit
        while (Process.GetProcessesByName(processName).Length > 0)
            System.Threading.Thread.Sleep(500);

        // Give the OS a moment to fully release file handles
        System.Threading.Thread.Sleep(500);

        // Extract update zip (overwrite all files)
        ZipFile.ExtractToDirectory(zipPath, extractPath, true);

        // Wait for extraction to complete (flush buffers)
        System.Threading.Thread.Sleep(500);

        // Clean up
        File.Delete(zipPath);

        // Verify main exe exists before launching
        if (!File.Exists(mainExePath))
        {
            Console.WriteLine($"ERROR: Main exe not found: {mainExePath}");
            return;
        }

        // Launch main app
        Process.Start(new ProcessStartInfo(mainExePath) { UseShellExecute = true });
    }
}