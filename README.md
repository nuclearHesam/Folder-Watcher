# Folder Watcher 🎵

Folder Watcher is a lightweight Windows background application that monitors a directory and optionally all of its subdirectories for music file changes.

It records music file additions, deletions, and renames in a readable text log. It does not download, open, or modify music files.

## Features

- Detects newly added audio files.
- Detects deleted audio files.
- Detects renamed audio files.
- Optionally monitors all subdirectories.
- Filters events by audio file extension.
- Writes timestamped, human-readable entries to `MusicChanges.txt`.
- Appends to the existing log instead of replacing it.
- Runs without a console window.
- Can start automatically when the user signs in to Windows.

## Log file location

The log is always created directly inside the monitored root directory, not beside the application executable.

For example, when monitoring `D:\SoundSpace`, the log is:

```text
D:\SoundSpace\MusicChanges.txt
```

Changes in nested directories include the actual directory path in the log.

### Example log

```text
[2026-11-22 03:15:20] Music monitoring started. Folder: D:\SoundSpace
[2026-11-22 03:16:05] Music file 'song.flac' was added to folder 'D:\SoundSpace\Album'.
[2026-11-22 03:18:02] Music file 'old-name.mp3' was deleted from folder 'D:\SoundSpace\Album'.
[2026-11-22 03:20:10] Music file 'track.mp3' in folder 'D:\SoundSpace\Album' was renamed to 'new-track.mp3'.
```

The log is written as UTF-8 plain text. New events are appended, so previous entries remain available after restarting the application.

## Configuration

The `appsettings.json` file must be placed next to `FolderWatcher.exe`.

Default configuration:

```json
{
  "watchPath": "D:\\SoundSpace",
  "includeSubdirectories": true
}
```

| Setting | Type | Description |
|---|---|---|
| `watchPath` | String | Absolute or relative path to the directory to monitor. |
| `includeSubdirectories` | Boolean | Set to `true` to monitor nested directories, or `false` to monitor only the root directory. |

The monitored directory must already exist. After changing the configuration, restart the application for the new settings to take effect.

The log filename is fixed as `MusicChanges.txt` and is always placed in the monitored root directory.

## Supported audio extensions

File extensions are matched case-insensitively.

| Category | Extensions |
|---|---|
| Common audio | `.mp3`, `.wav`, `.wave`, `.flac`, `.aac`, `.m4a`, `.ogg`, `.oga`, `.opus`, `.wma` |
| Lossless and high-resolution audio | `.aiff`, `.aif`, `.aifc`, `.alac`, `.ape`, `.wv`, `.tta`, `.dsf`, `.dff` |
| MIDI and other audio | `.mid`, `.midi`, `.amr`, `.3ga`, `.ac3`, `.dts`, `.mpc`, `.mp+`, `.mpp`, `.ra`, `.ram`, `.voc`, `.au`, `.snd`, `.caf`, `.spx` |
| Audiobooks and other formats | `.m4b`, `.m4p`, `.aa`, `.aax`, `.awb` |

The application identifies audio files by extension, not by inspecting file contents. A non-audio file with a supported audio extension will therefore be treated as an audio file.

## Requirements

To run a published build:

- Windows 10 or Windows 11.
- Permission to monitor the selected directory.
- Permission to create and append to `MusicChanges.txt` in the monitored directory.

To build from source:

- .NET 10 SDK.
- The project source code.

The self-contained publish command below includes the .NET runtime, so a separate runtime installation is not required on the target PC.

## Build and publish

Open PowerShell or a terminal in the project directory and run:

```powershell
dotnet publish ".\Folder Watcher.csproj" -c Release -r win-x64 --self-contained true
```

This publishes a self-contained Release build for 64-bit Windows.

The output directory is:

```text
Folder Watcher\bin\Release\net10.0-windows\win-x64\publish\
```

Copy the entire contents of the `publish` directory to your preferred installation location. Keep `appsettings.json` next to `FolderWatcher.exe`.

Example deployment layout:

```text
FolderWatcher/
├── FolderWatcher.exe
├── appsettings.json
└── Other published runtime files
```

Do not remove `appsettings.json`. The application reads this file from the executable's directory.

## Start automatically with Windows

To launch Folder Watcher whenever you sign in:

1. Move the published application files to a permanent location, for example `C:\Apps\FolderWatcher`.
2. Confirm that `appsettings.json` is next to `FolderWatcher.exe` and that `watchPath` points to the intended directory.
3. Press `Win + R`.
4. Enter the following command and press Enter:

   ```text
   shell:startup
   ```

5. Create a shortcut to `FolderWatcher.exe` inside the Startup folder.

The application will start automatically at the next sign-in and will run without displaying a console window.

The `shell:startup` folder applies to the current Windows user. Use the common Startup folder if the application should launch for all users.

## Stop the application

Because the application runs without a console window:

1. Press `Ctrl + Shift + Esc` to open Task Manager.
2. Find `FolderWatcher.exe` in the Processes or Details list.
3. Select **End task**.

When launched again, monitoring resumes and new events are appended to the existing `MusicChanges.txt` file.

## Behavior and limitations

- **No initial scan:** Monitoring starts when the application launches. Existing files are not logged as newly added.
- **No content-change tracking:** Editing tags, changing file size, or modifying the contents of an existing audio file is not tracked in the current version. Only additions, deletions, and renames are monitored.
- **Extension changes:** Renaming an audio file to a non-audio extension is logged as a deletion. Renaming a non-audio file to a supported audio extension is logged as an addition.
- **Audio-to-audio renames:** When both the old and new extensions are supported audio extensions, one rename entry is recorded.
- **Log exclusion:** `MusicChanges.txt` is not an audio file and is ignored by the audio extension filter.
- **Windows notification limits:** Windows may overflow its file-system notification buffer during very heavy activity. The application attempts to record an error when Windows reports one, but missed notifications cannot be reconstructed.
- **Unexpected shutdown:** If Windows or the application stops unexpectedly, the most recent events may not have been written to disk.
- **Directory permissions:** The application must have permission to create and append to the log file inside the monitored directory.

## Troubleshooting

### The log file is not created

Check the following:

1. Confirm that `watchPath` in `appsettings.json` is correct.
2. Confirm that the monitored directory exists.
3. Confirm that the current Windows user can write to that directory.
4. Confirm that `appsettings.json` is next to `FolderWatcher.exe`.

### The application does not start

If startup fails, the application attempts to write details to:

```text
FolderWatcher-errors.log
```

This file is placed next to the executable and may contain information about invalid paths, missing configuration, or access errors.

### Some changes are not logged

- Confirm that the file extension is included in the supported extension list.
- Confirm that `includeSubdirectories` is set to `true` if nested folders should be monitored.
- Confirm that the application was running when the file operation occurred.
- Consider Windows file-system notification limits if many files are changing at the same time.

## Project structure

```text
Folder-Watcher/
├── Folder Watcher/
│   ├── Program.cs
│   ├── Folder Watcher.csproj
│   └── appsettings.json
├── Folder Watcher.sln
└── README.md
```

## Contributing

For bug reports, feature requests, or contributions, use the Issues and Pull Requests sections of this GitHub repository.

## License

Usage and redistribution are governed by the license included in this repository. If no license file is present, contact the repository owner before redistributing the project.
