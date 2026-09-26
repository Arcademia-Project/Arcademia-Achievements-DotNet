# Arcademia Achievements SDK (.NET)

Unlock achievements from your game and let players collect them on their
Arcademia account. You set the achievements up on the Arcademia manager, and
your game only ever sends the achievement's API name.

This is the plain .NET build of the SDK, for anything that isn't Unity:
any engine or framework you write in C#, your own engine, or a .NET tool.
If you're building in Unity, use the
[Unity package](https://github.com/Arcademia-Project/ac.arcademia.achievements)
instead. It has the same API and also draws toasts for you.

Full platform docs: **https://manager.arcademia.ac/docs**

## Install

```
dotnet add package Arcademia.Achievements
```

Targets `netstandard2.0`, so it works on .NET Framework 4.6.1+, .NET
6/8/9+, and Mono.

This package is completely separate from the leaderboards package. Use one,
the other, or both.

## Setting up

1. On the manager, open your game and click the trophy icon to get to
   **Achievements**.
2. Request access. Once it's approved, generate an achievements API key.
   This is a different key from the leaderboards one.
3. Pick who competes (see "Scopes" below) and add your achievements. The API
   name is what your code uses, for example `FIRST_BLOOD`.
4. Put the key in an `arcademia.json` file next to your built executable:

```json
{
  "apiBase": "https://manager.arcademia.ac",
  "achievementsKey": "arc_xxxxxxxx_..."
}
```

If you also use the leaderboards SDK, keep its `apiKey` in the same file.
Each SDK only reads its own key. You can also set the key in code with
`ArcademiaAchievements.Configure(...)`.

## Quick start

```csharp
using Arcademia.Achievements;

void OnLevelTenCleared()
{
    _ = ArcademiaAchievements.UnlockAsync("FIRST_BLOOD");
}
```

On a cabinet that's all you need. The launcher shows the toast over your
game and offers the player a QR code to claim it when the game closes.
On your own PC nothing appears on screen until you handle `ToastRequested`
(see [Drawing toasts yourself](#drawing-toasts-yourself)).
Calling `UnlockAsync` again for the same achievement in the same playthrough
does nothing, so you don't need to track it yourself.

## Two modes, one API

| | Launcher (Live) | Sandbox |
|---|---|---|
| When | The game was started by the Arcademia launcher on a cabinet | Anywhere else: your dev machine, a build you're testing |
| Auth | Nothing you set. The launcher and the cabinet handle it | Your achievements key |
| Unlocks land on | The live achievements for that cabinet's team | A private test area only you can see |
| Toasts | Drawn by the launcher on top of your game | Raised as a `ToastRequested` event for you to draw |
| Claiming | A QR code on the cabinet after the game closes | `RequestSandboxClaimAsync` gives you a link |

The SDK picks the mode itself by checking for environment variables the
launcher sets, so the same code works in both places.

## Scopes

You choose one scope per game on the manager:

- **Sessional**: every playthrough starts fresh. Players always claim
  achievements for themselves.
- **Local**: each arcade machine competes on its own.
- **Institutional**: each site competes as a team.
- **National**: each country competes as a team.

For the team scopes, the first time a team unlocks an achievement it's
recorded against the team straight away, even if nobody claims it. A
player who claims it afterwards is named as the team's claimer. Each
achievement also has a "personal claims" switch. When it's on, players can
add the achievement to their own collection too (once per team, so they can
collect it again at another site).

## API

```csharp
Task<UnlockResult> ArcademiaAchievements.UnlockAsync(string apiName)
Task<AchievementsResult> ArcademiaAchievements.GetAchievementsAsync()
Task<OverlayResult> ArcademiaAchievements.OpenOverlayAsync()
Task<AchievementsPingResult> ArcademiaAchievements.PingAsync()
Task<SandboxClaimResult> ArcademiaAchievements.RequestSandboxClaimAsync(Action<string> onClaimLink = null, CancellationToken ct = default)
bool ArcademiaAchievements.TryGetToast(out AchievementToast toast)
Task<byte[]> ArcademiaAchievements.GetIconBytesAsync(Achievement achievement)
void ArcademiaAchievements.StartNewSandboxSession()
void ArcademiaAchievements.Configure(ArcademiaAchievementSettings settings)
void ArcademiaAchievements.Shutdown()

ArcademiaMode ArcademiaAchievements.Mode
string ArcademiaAchievements.SessionId
bool ArcademiaAchievements.IsOverlayOpen
event Action<UnlockResult> ArcademiaAchievements.Unlocked
event Action<AchievementToast> ArcademiaAchievements.ToastRequested
event Action ArcademiaAchievements.OverlayOpened
event Action ArcademiaAchievements.OverlayClosed
```

`UnlockResult.Status` is one of:

- `unlocked`: first time this playthrough.
- `alreadyUnlocked`: already unlocked this playthrough. Nothing happens.
- `unknown`: no achievement with that API name exists for your game.
- `rejected` or `error`: something went wrong, see `Message`.

`TeamHadIt`, `TeamLabel` and `TeamClaimedBy` tell you whether the player's
team already had the achievement.

Events are raised from the code that awaited the call. If your game has
no synchronisation context, that can be a
thread-pool thread, so hand the work back to your main loop before touching
engine objects.

### Drawing toasts yourself

The launcher draws toasts on a cabinet, so you usually don't need to.
`ToastRequested` fires when your game should draw one instead:

- in sandbox mode, so you can see unlocks while testing, and
- on a cabinet when the launcher can't draw over your game (true exclusive
  fullscreen, see below).

The simplest way is to ask for toasts once a frame from your game loop.
You get each toast on the thread that calls it, so you can draw it straight
away:

```csharp
while (ArcademiaAchievements.TryGetToast(out var toast))
    MyHud.ShowToast(toast.Title, toast.Name, toast.Subtitle, toast.IconBytes);
```

Or subscribe to the event, which runs on the thread that awaited the unlock.
Once something is subscribed, toasts go to the event instead of
`TryGetToast`, so using both never shows a toast twice:

```csharp
ArcademiaAchievements.ToastRequested += toast =>
{
    MyHud.ShowToast(toast.Title, toast.Name, toast.Subtitle, toast.IconBytes);
};
```

If neither is set up, the SDK writes a warning to the console and the
debugger output each time a toast is dropped.

`Title` and `Subtitle` are ready-made text. `TeamHadIt` is `true` when the
player's team already held the achievement, which is worth styling
differently (the launcher greys that toast out). `IconBytes` is the icon,
already loaded, and `IconExtension` says whether it's a `.png` or `.jpg`.
`GetIconBytesAsync(achievement)` does the same for your own achievements
screen.

### Achievements screen

Call `OpenOverlayAsync` from a menu button to show a Steam-style list
of every achievement over your game:

```csharp
public async void OnAchievementsButton()
{
    var result = await ArcademiaAchievements.OpenOverlayAsync();
    if (result.DrawInGame)
        ShowMyOwnAchievementsScreen(await ArcademiaAchievements.GetAchievementsAsync());
}
```

On a cabinet the launcher draws the list and the call finishes when the
player closes it with Exit. Pause your game between `OverlayOpened` and
`OverlayClosed`. Your game still receives controller input while the list is
open, so make sure a paused game ignores it.

`DrawInGame` is `true` in sandbox mode (there's no launcher) and when the
launcher can't draw over your game. `GetAchievementsAsync` gives you
everything the launcher's list uses: names, descriptions, icons, whether
each one was unlocked this playthrough and whether the team holds it.

### Secret achievements

Achievements marked as secret come back with `Hidden = true`. Hide their
name and description in your own screens until `Unlocked` is `true`.

## Fullscreen

On a cabinet the launcher draws toasts and the list in a window on top of
your game. That works with windowed and borderless fullscreen, and with
normal fullscreen on Windows 10 and 11. It can't draw over a game in true
exclusive fullscreen, so prefer borderless. If the launcher detects
exclusive fullscreen anyway, `UnlockResult.ShowToastInGame` is `true`,
`ToastRequested` fires, and `OpenOverlayAsync` returns `DrawInGame`.

## Testing

Everything works on your own PC in sandbox mode. Unlocks go to a test area
that only you can see.

- `StartNewSandboxSession()` simulates a new playthrough.
- `RequestSandboxClaimAsync()` creates a claim link for the current sandbox
  playthrough, so you can try the claim page yourself. The link is written
  to the console and the debugger output, and passed to `onClaimLink`. The
  call finishes once the link is used or expires after 5 minutes. Only the
  game's owners can claim sandbox playthroughs.
- The manager's Achievements page has a Sandbox tab showing your test
  unlocks, with a Reset button.

## Sample

`samples/QuickStart` is a console app with a menu that exercises every
call. Put an `arcademia.json` next to it, or type the key when it asks.

```
dotnet run --project samples/QuickStart
```

## License

MIT, see [LICENSE.md](LICENSE.md).
