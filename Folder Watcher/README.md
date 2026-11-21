# Folder Watcher — Music-only mode

A small Windows background process that records additions, deletions and renames of audio files. It does not download, open, read, or modify music files.

## Configure

Edit `appsettings.json` beside the published executable:

```json
{
  "watchPath": "D:\\SoundSpace",
  "logFilePath": "music-changes.jsonl",
  "includeSubdirectories": true
}
```

- `watchPath`: directory to monitor.
- `logFilePath`: JSON Lines log file. Relative paths are resolved beside the executable; absolute paths are also supported.
- `includeSubdirectories`: include nested folders.

The application reads this file at startup. If another application generates the configuration, write the complete JSON file before launching FolderWatcher.

## Publish

From the project directory:

```powershell
dotnet publish ".\\Folder Watcher.csproj" -c Release -r win-x64 --self-contained true
```

Copy the published output folder as a whole. Keep `appsettings.json` beside `FolderWatcher.exe`. Put a shortcut to the published executable in the Windows Startup folder (`shell:startup`). The app runs without a console window.

## Log format

Each event is one JSON object on one line, for example:

```json
{"timestamp":"2026-11-22T03:15:20+03:30","action":"Added","path":"D:\\SoundSpace\\album\\track.flac","newPath":null,"details":null}
```

Actions are `Added`, `Deleted`, `Renamed`, `WatcherStarted`, and `WatcherError`. The log is append-only and flushed after each event so another process can read it while monitoring continues.

Only files with recognized audio extensions are logged. File edits/metadata changes are intentionally ignored; the requested tracking is additions, deletions and renames. A rename between audio and non-audio extensions is represented as an add or delete.

## Notes

- Monitoring begins when the process starts; there is no initial library scan and no historical inventory.
- Windows file notifications can overflow during very heavy activity. The app records a `WatcherError` event when Windows reports a buffer overflow, but cannot reconstruct events that Windows did not deliver.
- Keep the log outside the watched folder where practical.
