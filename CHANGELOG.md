# Changelog

All notable changes to the Arcademia Achievements SDK for .NET are
documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [1.1.0] - 2026-09-26

### Added
- `TryGetToast(out toast)` for engines with a main loop (raylib, MonoGame, FNA). Call it once a frame and draw whatever it returns, with no threading to worry about. Toasts only go to this queue when nothing is subscribed to `ToastRequested`, so using both never shows a toast twice.
- `AchievementToast.IconBytes` and `IconExtension`: the toast's icon, already loaded, ready to turn into a texture.
- `GetIconBytesAsync(achievement)` for drawing your own achievements screen.
- A warning on the console and debugger output when a toast has nowhere to go, instead of it silently not appearing.

## [1.0.0] - 2026-09-25

### Added
- `UnlockAsync`, `GetAchievementsAsync`, `OpenOverlayAsync` and `PingAsync`, working the same way on a cabinet (through the launcher) and on your own PC (sandbox).
- `ToastRequested` event for drawing toasts in game when the launcher can't, plus `Unlocked`, `OverlayOpened` and `OverlayClosed`.
- `RequestSandboxClaimAsync` and `StartNewSandboxSession` for trying the claim flow while testing.
- `samples/QuickStart` console app.
