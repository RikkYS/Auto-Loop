# Changelog

## 0.7.2 (2026-11-18)

A polished look; same size, layout and behavior.

### Changed
- The window now shows the **real Auto Loop logo**, the same artwork as the EXE icon, tray icon and GitHub banner.
- The selected tab, the selected "Any app"/"Selected app" choice and the **Start** button use the logo's mint-to-teal gradient, so the main action stands out.
- **New countdown**: a smooth progress ring with a soft shadow that drains each second around the number, which pops in. Its edges are fully smooth and clicks still pass through it.
- 77 built-in checks (up from 76).

## 0.7.1 (2026-11-18)

Small improvements; the window looks exactly the same.

### Added
- **Double click** in the Click tab's mouse-button menu.
- **Key combinations** in the Keys tab, such as Ctrl+C: modifiers are held around the key.
- Your PC **stays awake** (no sleep, screen stays on) while Auto Loop counts down, plays or records.
- Opening a saved routine **reselects its app** when that window is open.

### Changed
- Keys are sent with hardware scan codes, so they work in more games and apps.
- The keyboard and mouse hooks only run while recording or picking, not while Auto Loop is idle.
- Esc cancels the "set shortcut" dialog. (When choosing the key to repeat, Esc can still be chosen.)
- Removed unused code.
- 76 built-in checks (up from 69).

## 0.7.0 (2026-11-17)

Easier to use, with the same window size and layout.

### Added
- **Pick app**: press it and click the window Auto Loop should use. The list of open windows is still next to it, and now refreshes by itself when opened.
- **Pick spot** (Settings) replaces "Pick in 3s": click the spot to click on, and a small dot marks it.
- **Floating status bar** while playing or recording: progress, Pause and Stop (Finish while recording). It never takes focus, can be dragged, and moves out of the way if a playback click would land on it.
- **Big 3-2-1 countdown** in the middle of the screen before playback or recording starts. Clicks pass through it.
- **Tray icon** while running, with Pause, Stop and Show Auto Loop, and a Windows notice when a run finishes on its own.
- **Plain-language hints**: "about 10 clicks per second" under the interval; the status line describes the Stop after rule, for example "about 600 clicks, then stops".
- **Remembers your settings** between launches, including the selected app if it's still open.

### Changed
- The refresh button next to the app list is gone; the list refreshes each time you open it.
- 69 built-in checks (up from 59), including one that shows the overlays on screen and confirms they never take focus.

## 0.6.1 (2026-11-17)

### Fixed
- Finishing a recording by clicking Auto Loop's **Record** or **Stop** button no longer records that click. Before, playback would click whatever sat under that spot.
- The keys or click that switch away from the recorded app (Alt+Tab, Windows key, a taskbar click) are no longer recorded. Replaying them made playback pause itself over and over.
- **Selected app** now really keeps clicks inside the selected app. If the next click or scroll would land in another window, playback pauses instead of clicking.
- **Selected app** now includes the app's own dialogs and menus, so recording and playback no longer pause when the app opens one.
- Apps running as administrator are detected. Before, Windows silently dropped all input to them and Auto Loop seemed to do nothing.
- The layout no longer clips at 125%, 150% or 200% display scaling.
- Recorded positions now replay correctly on monitors with different scaling (per-monitor DPI awareness).
- A failure to save settings (for example in a read-only folder) is now shown instead of being hidden.
- The EXE's file version showed 0.4.2; it now matches the app version.
- When playback stops because input could not be sent, Auto Loop restores its window so the message is visible.

### Changed
- Auto Loop is now released under the MIT License.
- Starting a new recording or pressing **Clear** asks before discarding the current steps.
- Playback requires a working global Stop shortcut. *(Already on `main` after 0.6.0; first released in 0.6.1.)*
- New `--preview <folder> <scale>` option renders the UI at a chosen display scale.
- 59 built-in checks (up from 46).

## 0.6.0 (2026-11-02)

First public preview: Click, Keys and Record & replay modes, Any app / Selected app, Stop after rules, saved routines, and customizable shortcuts.
