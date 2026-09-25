# Changelog

All notable changes to the Arcademia Achievements SDK for .NET are
documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [1.0.0] - 2026-09-25

### Added
- `UnlockAsync`, `GetAchievementsAsync`, `OpenOverlayAsync` and `PingAsync`, working the same way on a cabinet (through the launcher) and on your own PC (sandbox).
- `ToastRequested` event for drawing toasts in game when the launcher can't, plus `Unlocked`, `OverlayOpened` and `OverlayClosed`.
- `RequestSandboxClaimAsync` and `StartNewSandboxSession` for trying the claim flow while testing.
- `samples/QuickStart` console app.
