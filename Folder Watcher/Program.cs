using System.Globalization;
using System.Text.Json;

namespace FolderWatcher
{
    class Program
    {
        static void Main()
        {
             string folderPath = File.ReadAllText("appsettings.txt");


            string logFilePath = @$"{folderPath}\LogChanges.txt";

            FileSystemWatcher watcher = new(folderPath)
            {
                IncludeSubdirectories = true,
                EnableRaisingEvents = true
            };
            watcher.Created += (sender, e) => LogChange("Created", e.FullPath, logFilePath);
            watcher.Deleted += (sender, e) => LogChange("Deleted", e.FullPath, logFilePath);

            Console.WriteLine("Press ENTER to exit.");

            // still runnig to press key
            while (true)
            {
                string userInput = Console.ReadLine() ?? "";
                if (string.IsNullOrEmpty(userInput))
                    break;
            }
        }

        private static void LogChange(string action, string path, string logFilePath)
        {
            string[] parts = path.Split('\\');
            string secondLast = parts[parts.Length - 2];
            string last = parts[parts.Length - 1];

            string logEntry = $"{action}==> '{secondLast}'  {last}\n";
            File.AppendAllText(logFilePath, logEntry);
            Console.WriteLine(logEntry);
        }
    }


}
