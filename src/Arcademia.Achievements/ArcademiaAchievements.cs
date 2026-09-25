using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Arcademia.Achievements
{
    public static class ArcademiaAchievements
    {
        private const string SettingsFileName = "arcademia.json";
        private const int OverlayResponseTimeoutMs = 60 * 60 * 1000;
        private const int SandboxClaimPollMs = 2000;

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            IncludeFields = true,
        };

        private static readonly object Gate = new object();
        private static ArcademiaAchievementSettings _settings;
        private static LauncherTransport _launcher;
        private static bool _initialised;
        private static Guid _sandboxSession = Guid.NewGuid();
        private static readonly HashSet<string> SessionUnlocks = new HashSet<string>(StringComparer.Ordinal);

        public static event Action<UnlockResult> Unlocked;
        public static event Action<AchievementToast> ToastRequested;
        public static event Action OverlayOpened;
        public static event Action OverlayClosed;

        public static bool IsOverlayOpen { get; private set; }

        public static ArcademiaMode Mode
        {
            get
            {
                EnsureInitialised();
                return _launcher != null ? ArcademiaMode.Launcher : ArcademiaMode.Sandbox;
            }
        }

        public static string SessionId
        {
            get
            {
                EnsureInitialised();
                return _launcher != null ? _launcher.SessionId : _sandboxSession.ToString();
            }
        }

        public static ArcademiaAchievementSettings Settings
        {
            get
            {
                EnsureInitialised();
                return _settings;
            }
        }

        public static void Configure(ArcademiaAchievementSettings settings)
        {
            EnsureInitialised();
            _settings = settings ?? new ArcademiaAchievementSettings();
        }

        public static void Shutdown()
        {
            _launcher?.Dispose();
            _launcher = null;
            _settings = null;
            _initialised = false;
            lock (Gate)
                SessionUnlocks.Clear();
            _sandboxSession = Guid.NewGuid();
        }

        public static void StartNewSandboxSession()
        {
            EnsureInitialised();
            _sandboxSession = Guid.NewGuid();
            lock (Gate)
                SessionUnlocks.Clear();
        }

        private static void EnsureInitialised()
        {
            if (_initialised)
                return;

            _initialised = true;
            _launcher = LauncherTransport.TryCreateFromEnvironment();
            _settings = LoadSettingsFile() ?? new ArcademiaAchievementSettings();
        }

        private static ArcademiaAchievementSettings LoadSettingsFile()
        {
            try
            {
                var path = Path.Combine(AppContext.BaseDirectory, SettingsFileName);
                if (!File.Exists(path))
                    return null;

                return JsonSerializer.Deserialize<ArcademiaAchievementSettings>(File.ReadAllText(path), JsonOptions);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[Arcademia] Could not read " + SettingsFileName + ": " + ex.Message);
                return null;
            }
        }

        private static T Parse<T>(string json) => JsonSerializer.Deserialize<T>(json, JsonOptions);

        private static string KeyField => "\"apiKey\":" + AchievementJson.Quote(_settings.achievementsKey ?? "");

        public static async Task<AchievementsPingResult> PingAsync()
        {
            EnsureInitialised();

            try
            {
                if (_launcher != null)
                {
                    var dto = Parse<LauncherResponseDto>(await _launcher.SendAsync("achievementsHello", KeyField));
                    return new AchievementsPingResult
                    {
                        Success = dto.ok,
                        Mode = ArcademiaMode.Launcher,
                        GameName = dto.set?.gameName,
                        GameId = dto.set?.gameId ?? 0,
                        Scope = AchievementJson.ParseScope(dto.set?.scope),
                        Environment = "Live (via launcher)" + (dto.offline ? ", offline" : ""),
                        Message = dto.ok ? null : dto.message ?? dto.error,
                    };
                }

                var response = await SandboxTransport.SendAsync(
                    HttpMethod.Get, _settings.apiBase, "/api/Sdk/Achievements/Ping", _settings.achievementsKey, null);

                if (!response.IsSuccess)
                    return new AchievementsPingResult { Success = false, Mode = ArcademiaMode.Sandbox, Message = Describe(response) };

                var ping = Parse<PingResponseDto>(response.Body);
                return new AchievementsPingResult
                {
                    Success = true,
                    Mode = ArcademiaMode.Sandbox,
                    GameId = ping.gameId,
                    GameName = ping.gameName,
                    Scope = AchievementJson.ParseScope(ping.scope),
                    Environment = ping.environment + " (sandbox)",
                };
            }
            catch (Exception ex)
            {
                return new AchievementsPingResult { Success = false, Mode = Mode, Message = ex.Message };
            }
        }

        public static async Task<AchievementsResult> GetAchievementsAsync()
        {
            EnsureInitialised();

            try
            {
                if (_launcher != null)
                {
                    var dto = Parse<LauncherResponseDto>(await _launcher.SendAsync("getAchievements", KeyField));
                    if (!dto.ok || dto.set == null)
                        return new AchievementsResult { Success = false, Mode = ArcademiaMode.Launcher, Message = dto.message ?? dto.error ?? "No achievements are available." };
                    return ToResult(dto.set, ArcademiaMode.Launcher, dto.offline);
                }

                var path = "/api/Sdk/Achievements?sessionId=" + Uri.EscapeDataString(_sandboxSession.ToString());
                var response = await SandboxTransport.SendAsync(HttpMethod.Get, _settings.apiBase, path, _settings.achievementsKey, null);
                if (!response.IsSuccess)
                    return new AchievementsResult { Success = false, Mode = ArcademiaMode.Sandbox, Message = Describe(response) };

                return ToResult(Parse<AchievementSetDto>(response.Body), ArcademiaMode.Sandbox, false);
            }
            catch (Exception ex)
            {
                return new AchievementsResult { Success = false, Mode = Mode, Message = ex.Message };
            }
        }

        private static AchievementsResult ToResult(AchievementSetDto set, ArcademiaMode mode, bool offline)
        {
            var holds = new Dictionary<string, TeamHoldDto>(StringComparer.Ordinal);
            if (set.teamHolds != null)
                foreach (var h in set.teamHolds)
                    holds[h.apiName] = h;

            var session = new HashSet<string>(set.unlockedThisSession ?? new string[0], StringComparer.Ordinal);
            lock (Gate)
                foreach (var name in session)
                    SessionUnlocks.Add(name);

            var list = new List<Achievement>();
            if (set.achievements != null)
                foreach (var a in set.achievements)
                {
                    holds.TryGetValue(a.apiName, out var hold);
                    bool thisSession;
                    lock (Gate)
                        thisSession = session.Contains(a.apiName) || SessionUnlocks.Contains(a.apiName);
                    list.Add(new Achievement
                    {
                        ApiName = a.apiName,
                        Name = a.name,
                        Description = a.description,
                        IconUrl = AbsoluteIcon(a.iconUrl),
                        IconPath = a.iconPath,
                        Hidden = a.hidden,
                        AllowPersonal = a.allowPersonal,
                        SortOrder = a.sortOrder,
                        UnlockedThisSession = thisSession,
                        HeldByTeam = hold != null,
                        TeamUnlockedAt = hold?.firstUnlockedAt,
                        TeamClaimedBy = string.IsNullOrEmpty(hold?.claimedBy) ? null : hold.claimedBy,
                    });
                }

            return new AchievementsResult
            {
                Success = true,
                Mode = mode,
                Offline = offline,
                GameName = set.gameName,
                Scope = AchievementJson.ParseScope(set.scope),
                TeamLabel = string.IsNullOrEmpty(set.teamLabel) ? null : set.teamLabel,
                Achievements = list.ToArray(),
            };
        }

        private static string AbsoluteIcon(string iconUrl) =>
            string.IsNullOrEmpty(iconUrl) || iconUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? iconUrl
                : (_settings.apiBase ?? "").TrimEnd('/') + iconUrl;

        public static async Task<UnlockResult> UnlockAsync(string apiName)
        {
            EnsureInitialised();

            if (string.IsNullOrWhiteSpace(apiName))
                return new UnlockResult { Success = false, Status = "error", Message = "apiName is required.", Mode = Mode };

            apiName = apiName.Trim();
            lock (Gate)
                if (SessionUnlocks.Contains(apiName))
                    return new UnlockResult { Success = true, Status = "alreadyUnlocked", ApiName = apiName, Mode = Mode };

            UnlockResult result;
            try
            {
                result = _launcher != null
                    ? await UnlockViaLauncherAsync(apiName)
                    : await UnlockViaSandboxAsync(apiName);
            }
            catch (Exception ex)
            {
                result = new UnlockResult { Success = false, Status = "error", ApiName = apiName, Message = ex.Message, Mode = Mode };
            }

            if (result.Success)
                lock (Gate)
                    SessionUnlocks.Add(apiName);

            if (result.Success && result.IsNewThisSession)
            {
                Raise(Unlocked, result);
                if (result.ShowToastInGame)
                    Raise(ToastRequested, new AchievementToast
                    {
                        ApiName = result.ApiName,
                        Name = result.Name,
                        Description = result.Description,
                        IconUrl = result.IconUrl,
                        IconPath = result.IconPath,
                        TeamHadIt = result.TeamHadIt,
                        TeamLabel = result.TeamLabel,
                        AllowPersonal = result.AllowPersonal,
                    });
            }

            return result;
        }

        private static async Task<UnlockResult> UnlockViaLauncherAsync(string apiName)
        {
            var fields = KeyField
                + ",\"apiName\":" + AchievementJson.Quote(apiName)
                + ",\"unlockId\":" + AchievementJson.Quote(Guid.NewGuid().ToString())
                + ",\"achievedAt\":" + AchievementJson.Quote(DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));

            var dto = Parse<LauncherResponseDto>(await _launcher.SendAsync("unlockAchievement", fields));
            if (!dto.ok || dto.unlock == null)
                return new UnlockResult
                {
                    Success = false,
                    Status = dto.status ?? "error",
                    ApiName = apiName,
                    Message = dto.message ?? dto.error,
                    Mode = ArcademiaMode.Launcher,
                };

            var result = ToUnlockResult(dto.unlock, apiName, ArcademiaMode.Launcher, dto.offline);
            result.ShowToastInGame = dto.render == "game";
            return result;
        }

        private static async Task<UnlockResult> UnlockViaSandboxAsync(string apiName)
        {
            var body = "{\"sessionId\":" + AchievementJson.Quote(_sandboxSession.ToString())
                + ",\"unlockId\":" + AchievementJson.Quote(Guid.NewGuid().ToString()) + "}";
            var path = "/api/Sdk/Achievements/" + Uri.EscapeDataString(apiName) + "/Unlock";
            var response = await SandboxTransport.SendAsync(HttpMethod.Post, _settings.apiBase, path, _settings.achievementsKey, body);

            if (!response.IsSuccess)
                return new UnlockResult
                {
                    Success = false,
                    Status = response.StatusCode == 404 ? "unknown" : "rejected",
                    ApiName = apiName,
                    Message = Describe(response),
                    Mode = ArcademiaMode.Sandbox,
                };

            var result = ToUnlockResult(Parse<UnlockResponseDto>(response.Body), apiName, ArcademiaMode.Sandbox, false);
            result.ShowToastInGame = true;
            return result;
        }

        private static UnlockResult ToUnlockResult(UnlockResponseDto dto, string apiName, ArcademiaMode mode, bool offline)
        {
            var a = dto.achievement;
            return new UnlockResult
            {
                Success = dto.status == "unlocked" || dto.status == "alreadyUnlocked",
                Status = dto.status,
                ApiName = !string.IsNullOrEmpty(a?.apiName) ? a.apiName : apiName,
                Name = a?.name ?? apiName,
                Description = a?.description,
                IconUrl = AbsoluteIcon(a?.iconUrl),
                IconPath = a?.iconPath,
                AllowPersonal = a != null && a.allowPersonal,
                TeamHadIt = dto.teamHadIt,
                TeamLabel = string.IsNullOrEmpty(dto.teamLabel) ? null : dto.teamLabel,
                TeamClaimedBy = string.IsNullOrEmpty(dto.teamClaimedBy) ? null : dto.teamClaimedBy,
                Scope = AchievementJson.ParseScope(dto.scope),
                Offline = offline,
                Mode = mode,
            };
        }

        public static async Task<OverlayResult> OpenOverlayAsync()
        {
            EnsureInitialised();

            if (_launcher == null)
                return new OverlayResult
                {
                    Success = true,
                    Status = "renderInGame",
                    Message = "There is no launcher in sandbox mode. Draw your own list with GetAchievementsAsync.",
                    Mode = ArcademiaMode.Sandbox,
                };

            if (IsOverlayOpen)
                return new OverlayResult { Success = false, Status = "alreadyOpen", Mode = ArcademiaMode.Launcher };

            IsOverlayOpen = true;
            Raise(OverlayOpened);
            try
            {
                using (var channel = _launcher.CreateSibling())
                {
                    var dto = Parse<LauncherResponseDto>(await channel.SendAsync("openAchievements", KeyField, OverlayResponseTimeoutMs));
                    if (dto.ok && dto.render == "game")
                        return new OverlayResult { Success = true, Status = "renderInGame", Mode = ArcademiaMode.Launcher };
                    return new OverlayResult
                    {
                        Success = dto.ok,
                        Status = dto.ok ? "closed" : dto.status ?? dto.error ?? "error",
                        Message = dto.ok ? null : dto.message ?? dto.error,
                        Mode = ArcademiaMode.Launcher,
                    };
                }
            }
            catch (Exception ex)
            {
                return new OverlayResult { Success = false, Status = "error", Message = ex.Message, Mode = ArcademiaMode.Launcher };
            }
            finally
            {
                IsOverlayOpen = false;
                Raise(OverlayClosed);
            }
        }

        public static async Task<SandboxClaimResult> RequestSandboxClaimAsync(
            Action<string> onClaimLink = null,
            CancellationToken cancellationToken = default)
        {
            EnsureInitialised();

            if (_launcher != null)
                return new SandboxClaimResult
                {
                    Success = false,
                    Status = "notSandbox",
                    Message = "On an arcade cabinet the launcher shows the claim QR code when the game closes.",
                };

            string code = null;
            string claimUrl = null;
            try
            {
                var body = "{\"sessionId\":" + AchievementJson.Quote(_sandboxSession.ToString()) + "}";
                var response = await SandboxTransport.SendAsync(
                    HttpMethod.Post, _settings.apiBase, "/api/Sdk/Achievements/Claims", _settings.achievementsKey, body);

                if (!response.IsSuccess)
                    return new SandboxClaimResult
                    {
                        Success = false,
                        Status = response.StatusCode == 409 ? "nothingToClaim" : "rejected",
                        Message = Describe(response),
                    };

                var created = Parse<SandboxClaimDto>(response.Body);
                code = created.code;
                claimUrl = created.claimUrl;
                var expiresAt = DateTime.TryParse(
                    created.expiresAt, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var parsed)
                    ? parsed
                    : DateTime.UtcNow.AddMinutes(5);

                Console.Error.WriteLine("[Arcademia] Open this link to claim the sandbox playthrough: " + claimUrl);
                System.Diagnostics.Debug.WriteLine("[Arcademia] Open this link to claim the sandbox playthrough: " + claimUrl);
                onClaimLink?.Invoke(claimUrl);

                var statusBody = "{\"code\":" + AchievementJson.Quote(code) + "}";
                while (true)
                {
                    await Task.Delay(SandboxClaimPollMs, cancellationToken);

                    var poll = await SandboxTransport.SendAsync(
                        HttpMethod.Post, _settings.apiBase, "/api/Sdk/Achievements/Claims/Status", _settings.achievementsKey, statusBody);

                    if (poll.IsSuccess)
                    {
                        var state = Parse<SandboxClaimStatusDto>(poll.Body);
                        if (state.status != "pending")
                            return new SandboxClaimResult
                            {
                                Success = state.status == "saved",
                                Status = state.status,
                                ClaimUrl = claimUrl,
                                ClaimedBy = string.IsNullOrEmpty(state.claimedBy) ? null : state.claimedBy,
                            };
                    }
                    else if (DateTime.UtcNow > expiresAt.AddSeconds(30))
                        return new SandboxClaimResult { Success = false, Status = "expired", ClaimUrl = claimUrl, Message = Describe(poll) };
                }
            }
            catch (OperationCanceledException)
            {
                if (!string.IsNullOrEmpty(code))
                {
                    try
                    {
                        await SandboxTransport.SendAsync(
                            HttpMethod.Delete, _settings.apiBase, "/api/Sdk/Achievements/Claims", _settings.achievementsKey,
                            "{\"code\":" + AchievementJson.Quote(code) + "}");
                    }
                    catch (Exception)
                    {
                    }
                }

                return new SandboxClaimResult { Success = false, Status = "cancelled", ClaimUrl = claimUrl };
            }
            catch (Exception ex)
            {
                return new SandboxClaimResult { Success = false, Status = "error", ClaimUrl = claimUrl, Message = ex.Message };
            }
        }

        private static void Raise(Action handler)
        {
            try { handler?.Invoke(); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[Arcademia] Event handler failed: " + ex); }
        }

        private static void Raise<T>(Action<T> handler, T value)
        {
            try { handler?.Invoke(value); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[Arcademia] Event handler failed: " + ex); }
        }

        private static string Describe(SandboxResponse response)
        {
            var body = string.IsNullOrWhiteSpace(response.Body) ? "" : response.Body.Trim('"');
            return "HTTP " + response.StatusCode + (body.Length > 0 ? ": " + body : "");
        }
    }
}
