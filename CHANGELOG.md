# Changelog

## 0.6.1 (unreleased)

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
