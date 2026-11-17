# Auto Loop user guide

This guide walks through each part of Auto Loop. For download and install steps, see the [README](README.md).

## Click or repeat a key

| Click | Keys |
|---|---|
| ![Click tab](click.png) | ![Keys tab](keys.png) |

1. Choose **Click** and pick a mouse button, or choose **Keys**, click the key field and press the key to repeat.
2. Set the **Interval**. `100 ms` is about ten presses per second; the shortest interval is 10 ms.
3. Choose **Stop after**: a number of repeats, a number of seconds, or **Until stopped**. Time spent paused doesn't count toward the seconds.
4. Choose **Any app**, or **Selected app** and the window to use.
5. Press **Start**. Auto Loop minimizes; switch to your app during the three-second countdown.
6. Press **F9** to stop.

By default a click happens wherever the pointer is. To always click the same spot, open **Settings**, turn on **Always click at the same screen position**, and type the X and Y coordinates or use **Pick in 3s** and hold the pointer over the spot.

**Press duration** in Settings is how long each click or key is held down (30 ms by default). It must not be longer than the interval.

## Record and replay

![Record tab with an example recording](record.png)

1. Choose **Record**, then press **F7**.
2. Switch to the app you want to record during the three-second countdown.
3. Do your actions, then press **F7** again (or **F9**) to finish.
4. Press **Test once** to play one pass. It ignores your Stop after setting without changing it.
5. Press **Start** to play using your Stop after setting.
6. Press **Save** to keep the recording, or **Open** to load one.

What gets recorded:

- Mouse moves, button presses and releases (so holds and drags replay correctly), and scrolling.
- Keys, as separate press and release steps, so key combinations replay correctly.
- The number on the right of each step is the delay **before** that step.

What doesn't get recorded:

- Anything you do in Auto Loop's own window, including clicking its Record or Stop button.
- Your Start, Record, Pause and Stop shortcuts.
- In **Selected app** mode, presses and scrolls outside the selected app.
- The keys or click that switched you away from the app (for example Alt+Tab, the Windows key, or a taskbar click). Replaying them would only make playback pause itself.

Right-click a step to change its delay or remove it. If removing a step leaves a press without its release, Auto Loop explains the problem before playback. **Clear** empties the recording after asking you to confirm, and starting a new recording asks before replacing the current one.

Recordings replay **screen positions**. Auto Loop doesn't recognize buttons or wait for windows to load, so keep the window in the same place and add longer delays where the app needs time.

## Shortcuts and settings

| Default | Action |
|---|---|
| F6 | Start |
| F7 | Record / finish recording |
| F8 | Pause / resume |
| F9 | Stop, including during the countdown |

![Settings](settings.png)

Click a shortcut in **Settings** and press the new key or key combination. If another app already uses the default function keys, Auto Loop switches to Ctrl+Alt+F6–F9 and shows the active keys on its buttons. Playback can't start until a global Stop shortcut works.

## Why playback pauses

Hover over the status line at the bottom for details.

- **Paused: selected app is not active**: return to that window. With **Resume when I return** off, press Pause to continue.
- **Paused: Auto Loop is active**: minimize Auto Loop and go back to the app you are automating.
- **Paused: pointer is outside the selected app**: the next click would have landed in a different window. Move the window or pointer back and press Pause to continue.
- **Paused: the active app runs as administrator**: Windows blocks input to it from a normal app. Switch to another app, or run Auto Loop as administrator.
- **Paused by you**: press Pause again to continue.

Every held key and mouse button is released when playback pauses or stops. A repetition that was interrupted starts again from its first step. Closing the selected window stops playback.

## Files

Auto Loop keeps `settings.xml` and the `Routines` folder next to `Auto Loop.exe`. If Windows doesn't let it write there, the status line says **Settings not saved**. Move Auto Loop to a folder you own, such as one in Documents. Copy those files along if you move Auto Loop.
