# Folder Watcher — Music-only mode

A silent Windows background application that tracks additions, deletions, and renames of audio files.

## Configuration

Edit `appsettings.json` next to the published executable:

```json
{
  "watchPath": "D:\\SoundSpace",
  "includeSubdirectories": true
}
```

The log is always created as `MusicChanges.txt` directly inside the monitored root directory. It is a UTF-8 plain-text file with readable messages and timestamps.

Example:

```text
[2026-11-22 03:15:20] Music file 'song.flac' was added to folder 'D:\\SoundSpace\\Album'.
[2026-11-22 03:18:02] Music file 'old-name.mp3' was deleted from folder 'D:\\SoundSpace\\Album'.
```

Only supported audio extensions are tracked. The application does not inspect, download, or modify music file contents. Renaming an audio file to another supported audio extension is logged as a rename. Renaming an audio file to a non-audio extension is logged as a deletion; the reverse is logged as an addition.

## Publish and start with Windows

Run this command from the project directory:

```powershell
dotnet publish ".\\Folder Watcher.csproj" -c Release -r win-x64 --self-contained true
```

Copy the entire publish output directory. Keep `appsettings.json` next to `FolderWatcher.exe`. Create a shortcut to the executable in the Windows Startup folder by opening `shell:startup` from the Run dialog.

The application runs without a console window.

## Notes

- Monitoring begins when the application starts. No initial scan or historical inventory is performed.
- The log file is created inside the monitored directory. It is not an audio file and is ignored by the extension filter.
- Windows may drop notifications during very heavy file activity. The application records an error if Windows reports a watcher failure.
- File content edits and metadata changes are not monitored in this version.
