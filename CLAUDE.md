# Auto Loop

## Project
- Portable Windows auto-clicker, key repeater and macro recorder (Click / Keys / Record tabs). Simple by design, no scripting.
- C# on .NET Framework 4.x, WinForms, x64. Built with the in-box csc (C# 5: no `?.`, string interpolation or `=>` members).
- GitHub: https://github.com/RikkYS/Auto-Loop (MIT, owner RikkYS). Solo project: commit straight to `main`.
- Build: `./build.ps1` → `Auto Loop.exe`. Self-test: `& ".\Auto Loop.exe" --self-test <empty dir>` (82 checks, writes internal-tests.txt). Screenshots: `--preview <dir> <scale>`.
- Git is not on PowerShell's PATH: use `& "$env:ProgramFiles\Git\cmd\git.exe"`. Push and GitHub API auth come from the saved Git Credential Manager login (`git credential fill`).
- Release: bump both Assembly versions in AutoLoop.cs; update CHANGELOG.md, TESTING.txt, README; regenerate screenshots; commit and push; build from the clean commit; self-test; ZIP = `Auto Loop.exe` + QUICKSTART.txt + LICENSE.txt; write SHA256SUMS.txt; publish via GitHub API (draft, upload the 3 assets, publish with make_latest); then download the assets, check sums and self-test the downloaded EXE.
- Asset names: `Auto-Loop-<ver>-Portable.zip`, `Auto-Loop-<ver>.exe`, `SHA256SUMS.txt`.

## Key files
- `AutoLoop.cs`: engine, main window, settings, input hooks, routines, self-tests (very long lines; search, don't scan).
- `Theme.cs`: DPI scaling (UiScale), themed controls, brand gradient, header logo.
- `Overlays.cs`: floating status bar, countdown ring, spot dot (never take focus; ring and dot are click-through).
- `build.ps1`, `README.md`, `GUIDE.md`, `CHANGELOG.md`, `TESTING.txt`, `AUTOMATED-TEST-RESULTS.txt`.
- `Logo.png` (embedded real logo), `AutoLoop.ico`, screenshots from `--preview`, `demo.png` (made with `promo/tools/DemoMaker.cs`).
- `promo/` (local only, excluded in `.git/info/exclude`): `social-preview.png` banner, `release-notes/`, `phase2/` drafts, `tools/`.

## Decisions and don'ts
- Never add `Co-Authored-By: Claude` or any Claude attribution to commits or PRs; RikkYS stays the only contributor.
- Keep it simple. Main window stays 400×512 (587 with Selected app) at 100%; never make it bigger or crowd the layout.
- No automatic update check or other network calls; Check for updates only opens the releases page.
- Not wanted: random delay, replay speed, recent-routines menu. Also skip image recognition, scripting, scheduling, background-window clicking and a light theme unless users ask.
- Look is "Polished": real logo tile in the header; logo mint-to-teal gradient on the selected tab, the selected Run-in choice and Start. Countdown is the smooth progress ring.
- Selected app: Pick app (click the window) is primary, the window list is the backup.
- Esc cancels the shortcut dialog but stays selectable as a key to repeat.
- Input hooks run only while recording or picking; the PC is kept awake only while running.
- Versions: patch = fixes, minor = features; 1.0 only after real-world testing and code signing. Replacing a release in place is OK only if it has 0 downloads.
- Self-tests and previews must never read or change the user's settings.xml (Isolated mode).
- Claude may fill in and submit listings/posts in the user's signed-in browser, but asks before every final submit and never pays (decline paid "priority" options). Reddit is blocked for Claude's browsers: the user posts there.

## Status
- Current release: v0.7.3 (latest, 2026-11-19): engine hardening, version line + Check for updates link in Settings (Settings window 490×478). VirusTotal: 2/70 generic flags (Bkav Pro, SecureAge), linked in README and release notes.
- Phase 1 (page polish: demo, pitch, VirusTotal, banner): done.
- Phase 2 (get found): all drafts written in `promo/phase2/` (2026-11-19); the user posts them. Status per site:
  - [x] AlternativeTo submitted 2026-11-19, free queue, awaiting review → `alternativeto.md`
  - [-] Softpedia skipped (requires "No AI-written code"); MajorGeeks has no submission form → `download-sites.md`
  - [ ] Reddit (r/software, r/windows, r/opensource, r/SideProject; rules unverified, check each sidebar first) → `reddit.md`
  - [ ] Show HN: user only, in their own words (HN bans generated text and automated posting); draft is notes → `show-hn.md`
  - [ ] Product Hunt → `product-hunt.md`
  - [ ] awesome-windows (now 0PandaDEV/awesome-windows; needs 50 stars, wait) → `awesome-windows.md`
- Later: Phase 3 winget/Scoop/Chocolatey packages and SignPath code signing; Phase 4 feedback-driven 0.8.
- Next step: the user submits the Phase 2 drafts; update links and checkboxes as they go live.

## How to work
- Read only the files the task needs; search instead of scanning the whole project.
- Don't cut corners on quality. Test what you changed before saying it works.
- Writing tasks (listings, posts): give final paste-ready text tailored to each site, not generic copy.
- Ask before long or full test passes; quick checks of the change are fine.
- Be concise; no summary tables unless I ask.
- When a task is finished or a new decision is made, update the status/rules in this CLAUDE.md.
