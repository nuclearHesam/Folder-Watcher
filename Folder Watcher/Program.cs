using System.Text;
using System.Threading.Channels;

namespace FolderWatcher;

internal static class Program
{
    private static readonly HashSet<string> AudioExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp3", ".wav", ".wave", ".flac", ".aac", ".m4a", ".ogg", ".oga",
        ".opus", ".wma", ".aiff", ".aif", ".aifc", ".alac", ".ape", ".wv", ".tta",
        ".dsf", ".dff", ".mid", ".midi", ".amr", ".3ga", ".ac3", ".dts",
        ".mpc", ".mp+", ".mpp", ".ra", ".ram", ".voc", ".au", ".snd", ".caf",
        ".spx", ".m4b", ".m4p", ".aa", ".aax", ".awb"
    };

    private const string LogFileName = "MusicChanges.txt";

    private static async Task<int> Main()
    {
        var baseDirectory = AppContext.BaseDirectory;
        var configPath = Path.Combine(baseDirectory, "appsettings.json");

        try
        {
            var config = await LoadConfigurationAsync(configPath);
            var watchPath = ResolvePath(baseDirectory, config.WatchPath);
            var logPath = Path.Combine(watchPath, LogFileName);

            if (!Directory.Exists(watchPath))
                throw new DirectoryNotFoundException($"Watch folder does not exist: {watchPath}");

            var channel = Channel.CreateUnbounded<MusicChange>(
                new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

            using var shutdown = new CancellationTokenSource();
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                shutdown.Cancel();
            };

            using var watcher = new FileSystemWatcher(watchPath)
            {
                IncludeSubdirectories = config.IncludeSubdirectories,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName,
                InternalBufferSize = 32 * 1024,
                Filter = "*",
                EnableRaisingEvents = false
            };

            watcher.Created += (_, e) => QueueIfAudio(channel.Writer, "Added", e.FullPath);
            watcher.Deleted += (_, e) => QueueIfAudio(channel.Writer, "Deleted", e.FullPath);
            watcher.Renamed += (_, e) => HandleRename(channel.Writer, e.OldFullPath, e.FullPath);
            watcher.Error += (_, e) =>
            {
                var message = e.GetException()?.Message ?? "Unknown folder monitoring error";
                channel.Writer.TryWrite(new MusicChange(DateTimeOffset.Now, "Error", null, null, message));
            };

            var writerTask = WriteEventsAsync(channel.Reader, logPath);
            watcher.EnableRaisingEvents = true;
            channel.Writer.TryWrite(new MusicChange(
                DateTimeOffset.Now, "Started", watchPath, null, null));

            var runTask = Task.Delay(Timeout.Infinite, shutdown.Token);
            var completedTask = await Task.WhenAny(runTask, writerTask);
            if (completedTask == writerTask)
                await writerTask;

            try
            {
                await runTask;
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown.
            }

            watcher.EnableRaisingEvents = false;
            channel.Writer.TryComplete();
            await writerTask;
            return 0;
        }
        catch (Exception ex)
        {
            try
            {
                var errorPath = Path.Combine(baseDirectory, "FolderWatcher-errors.log");
                await File.AppendAllTextAsync(errorPath,
                    $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss} Error: {ex}{Environment.NewLine}",
                    Encoding.UTF8);
            }
            catch
            {
                // Startup errors are recorded when possible.
            }

            return 1;
        }
    }

    private static async Task<WatcherConfiguration> LoadConfigurationAsync(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("appsettings.json was not found next to the executable.", path);

        await using var stream = File.OpenRead(path);
        var config = await System.Text.Json.JsonSerializer.DeserializeAsync<WatcherConfiguration>(stream,
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (config is null || string.IsNullOrWhiteSpace(config.WatchPath))
            throw new InvalidDataException("Set a valid 'watchPath' in appsettings.json.");

        return config;
    }

    private static string ResolvePath(string baseDirectory, string path) =>
        Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(baseDirectory, path));

    private static bool IsAudioFile(string path) =>
        AudioExtensions.Contains(Path.GetExtension(path));

    private static void QueueIfAudio(ChannelWriter<MusicChange> writer, string action, string path)
    {
        if (IsAudioFile(path))
            writer.TryWrite(new MusicChange(DateTimeOffset.Now, action, path, null, null));
    }

    private static void HandleRename(ChannelWriter<MusicChange> writer, string oldPath, string newPath)
    {
        var oldIsAudio = IsAudioFile(oldPath);
        var newIsAudio = IsAudioFile(newPath);

        if (oldIsAudio && newIsAudio)
            writer.TryWrite(new MusicChange(DateTimeOffset.Now, "Renamed", oldPath, newPath, null));
        else if (oldIsAudio)
            writer.TryWrite(new MusicChange(DateTimeOffset.Now, "Deleted", oldPath, null, null));
        else if (newIsAudio)
            writer.TryWrite(new MusicChange(DateTimeOffset.Now, "Added", newPath, null, null));
    }

    private static string Describe(MusicChange change)
    {
        var time = change.Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
        if (change.Action == "Started")
            return $"[{time}] Music monitoring started. Folder: {change.Path}";

        if (change.Action == "Error")
            return $"[{time}] Folder monitoring error: {change.Details}";

        var fileName = Path.GetFileName(change.Path);
        var folder = Path.GetDirectoryName(change.Path) ?? change.Path ?? "Unknown";

        return change.Action switch
        {
            "Added" => $"[{time}] Music file '{fileName}' was added to folder '{folder}'.",
            "Deleted" => $"[{time}] Music file '{fileName}' was deleted from folder '{folder}'.",
            "Renamed" => $"[{time}] Music file '{fileName}' in folder '{folder}' was renamed to '{Path.GetFileName(change.NewPath)}'.",
            _ => $"[{time}] Change: {change.Action} - {change.Path}"
        };
    }

    private static async Task WriteEventsAsync(ChannelReader<MusicChange> reader, string logPath)
    {
        await using var stream = new FileStream(logPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite,
            bufferSize: 16 * 1024, options: FileOptions.Asynchronous | FileOptions.SequentialScan);
        await using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        await foreach (var change in reader.ReadAllAsync())
        {
            await writer.WriteLineAsync(Describe(change));
            await writer.FlushAsync();
        }
    }

    private sealed class WatcherConfiguration
    {
        public string WatchPath { get; set; } = "";
        public bool IncludeSubdirectories { get; set; } = true;
    }

    private sealed record MusicChange(
        DateTimeOffset Timestamp,
        string Action,
        string? Path,
        string? NewPath,
        string? Details);
}
