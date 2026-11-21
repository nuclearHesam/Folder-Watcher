using System.Text.Json;
using System.Threading.Channels;

namespace FolderWatcher;

internal static class Program
{
    private static readonly HashSet<string> AudioExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp3", ".wav", ".wave", ".flac", ".aac", ".m4a", ".mp4", ".ogg", ".oga",
        ".opus", ".wma", ".aiff", ".aif", ".aifc", ".alac", ".ape", ".wv", ".tta",
        ".dsf", ".dff", ".mid", ".midi", ".amr", ".3ga", ".ac3", ".dts", ".mka",
        ".mpc", ".mp+", ".mpp", ".ra", ".ram", ".voc", ".au", ".snd", ".caf",
        ".spx", ".m4b", ".m4p", ".aa", ".aax", ".awb"
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static async Task<int> Main()
    {
        var baseDirectory = AppContext.BaseDirectory;
        var configPath = Path.Combine(baseDirectory, "appsettings.json");

        try
        {
            var config = await LoadConfigurationAsync(configPath);
            var watchPath = ResolvePath(baseDirectory, config.WatchPath);
            var logPath = ResolvePath(baseDirectory, config.LogFilePath);

            if (!Directory.Exists(watchPath))
                throw new DirectoryNotFoundException($"Watch folder does not exist: {watchPath}");

            var logDirectory = Path.GetDirectoryName(logPath);
            if (!string.IsNullOrWhiteSpace(logDirectory))
                Directory.CreateDirectory(logDirectory);

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
                var message = e.GetException()?.Message ?? "Unknown FileSystemWatcher error";
                channel.Writer.TryWrite(new MusicChange(DateTimeOffset.Now, "WatcherError", null, null, message));
            };

            var writerTask = WriteEventsAsync(channel.Reader, logPath);
            watcher.EnableRaisingEvents = true;
            channel.Writer.TryWrite(new MusicChange(
                DateTimeOffset.Now, "WatcherStarted", watchPath, null,
                $"Recursive={config.IncludeSubdirectories}; audio extensions only"));

            var runTask = Task.Delay(Timeout.Infinite, shutdown.Token);
            var completedTask = await Task.WhenAny(runTask, writerTask);

            // If the log writer fails (for example, the destination becomes unavailable),
            // surface that failure instead of leaving a watcher running without a log.
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
                    $"{DateTimeOffset.Now:O} {ex}{Environment.NewLine}");
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
            throw new FileNotFoundException("appsettings.json was not found beside the application.", path);

        await using var stream = File.OpenRead(path);
        var config = await JsonSerializer.DeserializeAsync<WatcherConfiguration>(stream,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (config is null || string.IsNullOrWhiteSpace(config.WatchPath))
            throw new InvalidDataException("Set a valid 'watchPath' in appsettings.json.");

        if (string.IsNullOrWhiteSpace(config.LogFilePath))
            throw new InvalidDataException("Set a valid 'logFilePath' in appsettings.json.");

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
            writer.TryWrite(new MusicChange(DateTimeOffset.Now, "Deleted", oldPath, null, "Renamed to a non-audio file"));
        else if (newIsAudio)
            writer.TryWrite(new MusicChange(DateTimeOffset.Now, "Added", newPath, null, "Renamed from a non-audio file"));
    }

    private static async Task WriteEventsAsync(ChannelReader<MusicChange> reader, string logPath)
    {
        await using var stream = new FileStream(logPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite,
            bufferSize: 16 * 1024, options: FileOptions.Asynchronous | FileOptions.SequentialScan);
        await using var writer = new StreamWriter(stream);

        await foreach (var change in reader.ReadAllAsync())
        {
            await writer.WriteLineAsync(JsonSerializer.Serialize(change, JsonOptions));
            await writer.FlushAsync();
        }
    }

    private sealed class WatcherConfiguration
    {
        public string WatchPath { get; set; } = "";
        public string LogFilePath { get; set; } = "music-changes.jsonl";
        public bool IncludeSubdirectories { get; set; } = true;
    }

    private sealed record MusicChange(
        DateTimeOffset Timestamp,
        string Action,
        string? Path,
        string? NewPath,
        string? Details);
}
