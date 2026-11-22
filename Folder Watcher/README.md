# Folder Watcher — Music-only mode

A silent Windows background app that tracks additions, deletions and renames of audio files only.

## Configuration

Edit `appsettings.json` beside the published executable:

```json
{
  "watchPath": "D:\\SoundSpace",
  "includeSubdirectories": true
}
```

The log is always created as `MusicChanges.txt` directly inside the monitored root folder. It is plain UTF-8 text and uses readable Persian messages with timestamps. Example:

```text
[2026-11-22 03:15:20] آهنگ «song.flac» به پوشه «D:\\SoundSpace\\Album» اضافه شد.
[2026-11-22 03:18:02] آهنگ «old-name.mp3» از داخل پوشه «D:\\SoundSpace\\Album» حذف شد.
```

Only recognized audio extensions are tracked. The app does not inspect or download music contents. Rename between two audio filenames is logged as a rename; renaming an audio file to a non-audio extension is logged as a deletion, and the reverse is logged as an addition.

## Publish and start with Windows

Run from the project directory:

```powershell
dotnet publish ".\\Folder Watcher.csproj" -c Release -r win-x64 --self-contained true
```

Copy the published output folder as a whole. Keep `appsettings.json` beside `FolderWatcher.exe`. Create a shortcut to the executable in the Windows Startup folder (`shell:startup`). It runs without a console window.

## Notes

- Monitoring begins at process startup. No initial scan or historical inventory is performed.
- The log file is inside the monitored folder; it is not an audio file, so it is ignored by the audio-extension filter.
- Windows may drop notifications during very heavy activity. A watcher error is written to the log when Windows reports such a condition.
