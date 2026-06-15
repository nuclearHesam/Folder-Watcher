using System;
using System.IO;

namespace FolderWatcher;

class Program
{
    private static FileSystemWatcher? watcher;

    static void Main()
    {
        try
        {
            string folderPath = File.ReadAllText("appsettings.txt");
            string logFilePath = Path.Combine(folderPath, "LogChanges.txt");

            watcher = new FileSystemWatcher(folderPath)
            {
                IncludeSubdirectories = true,
                EnableRaisingEvents = true
            };

            watcher.Created += (sender, e) => LogChange("Created", e.FullPath, logFilePath);
            watcher.Deleted += (sender, e) => LogChange("Deleted", e.FullPath, logFilePath);

            Console.WriteLine("Watching for changes. Press Ctrl+C to exit.");
            
            Console.CancelKeyPress += (sender, e) =>
            {
                Console.WriteLine("Stopping watcher...");
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
                Environment.Exit(0);
            };

            while (true) Thread.Sleep(1000);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }

    private static void LogChange(string action, string path, string logFilePath)
    {
        try
        {
            string[] parts = path.Split('\\');
            string secondLast = parts.Length > 1 ? parts[^2] : "Unknown";
            string last = parts[^1];

            string logEntry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {action} ==> '{secondLast}'  {last}\n";
            File.AppendAllText(logFilePath, logEntry);
            Console.WriteLine(logEntry);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Logging Error: {ex.Message}");
        }
    }
}
