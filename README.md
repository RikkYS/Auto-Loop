# Auto Loop

Auto Loop is a portable Windows app for repeating mouse clicks, repeating a key, and recording keyboard and mouse actions for playback.

**Windows x64 · .NET Framework 4.x · No installer · v0.6.0 preview**

[Download portable ZIP](https://github.com/RikkYS/Auto-Loop/releases/download/v0.6.0/Auto-Loop-0.6.0-Portable.zip) · [Download standalone EXE](https://github.com/RikkYS/Auto-Loop/releases/download/v0.6.0/Auto-Loop-0.6.0.exe) · [Release notes and checksums](https://github.com/RikkYS/Auto-Loop/releases/tag/v0.6.0)

![Auto Loop's Click tab showing the interval, target, and stop controls](click.png)

## Try five clicks

1. Extract the ZIP to a folder you can write to and open **Auto Loop.exe**.
2. Open a blank Notepad window. In Auto Loop, choose **Click**, set **Interval** to **500 ms**, and set **Stop after** to **5 repeats**.
3. Choose **Selected app** and pick the Notepad window.
4. Press **Start**. Switch to Notepad and put the pointer in the blank document during the three-second countdown.
5. Use **F9** to stop early. Check the Stop button for your active shortcut if the default is unavailable.

## Record and replay

Press **F7**, switch to the app you want to record, and perform the actions. Press **F7** again to finish, then use **Test once** before looping. Use **Save** to keep the recording.

| Default shortcut | Action |
| --- | --- |
| F6 | Start |
| F7 | Start or finish recording |
| F8 | Pause or resume |
| F9 | Stop or cancel the countdown |

Shortcuts can be changed in Settings. See the [illustrated guide](GUIDE.md) for key repeat, fixed click positions, and recording controls.

## Playback behavior and limits

- **Selected app** pauses when the chosen window loses focus. An interrupted repetition restarts from the first step on resume.
- **Any app** follows the active window and pauses when Auto Loop is in front. Keep the intended window in front while playback runs.
- Recordings use screen coordinates. Keep the same window position and display layout for playback. Review saved routines before sharing, as they can contain the keys you pressed.
- Builds from the current source require a working global Stop shortcut before playback. If playback is blocked, assign an available shortcut in Settings.
- The executable is unsigned. Both downloads can be verified against [SHA256SUMS.txt](https://github.com/RikkYS/Auto-Loop/releases/download/v0.6.0/SHA256SUMS.txt).
- The bundled checks cover simulated playback and recording events. Compatibility with elevated apps, games, and different display setups is unverified. See [testing and known limits](TESTING.txt).

## Build and report issues

On Windows with the 64-bit .NET Framework C# compiler installed, run `./build.ps1` in PowerShell. See the [guide](GUIDE.md#build-from-source) for the self-test command.

When [reporting a problem](https://github.com/RikkYS/Auto-Loop/issues), include your Windows version, Auto Loop version, mode, target setting, steps to reproduce, and what you expected to happen. If input was blocked, include the exact message and whether the target was running as administrator. Remove private input from recordings before attaching them.
