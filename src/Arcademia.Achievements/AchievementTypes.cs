using System;

namespace Arcademia.Achievements
{
    public enum ArcademiaMode
    {
        Sandbox,
        Launcher,
    }

    public enum AchievementScope
    {
        Sessional,
        Local,
        Institutional,
        National,
    }

    public class ArcademiaAchievementSettings
    {
        public string apiBase = "https://manager.arcademia.ac";
        public string achievementsKey = "";
    }

    public class Achievement
    {
        public string ApiName;
        public string Name;
        public string Description;
        public string IconUrl;
        public string IconPath;
        public bool Hidden;
        public bool AllowPersonal;
        public int SortOrder;
        public bool UnlockedThisSession;
        public bool HeldByTeam;
        public string TeamUnlockedAt;
        public string TeamClaimedBy;

        public bool Unlocked => UnlockedThisSession || HeldByTeam;

        public override string ToString() =>
            $"{ApiName} \"{Name}\""
            + (UnlockedThisSession ? " [this session]" : "")
            + (HeldByTeam ? " [team" + (string.IsNullOrEmpty(TeamClaimedBy) ? ", anonymous" : ", claimed by " + TeamClaimedBy) + "]" : "")
            + (Hidden ? " [secret]" : "")
            + (AllowPersonal ? "" : " [team only]");
    }

    public class AchievementsResult
    {
        public bool Success;
        public string Message;
        public ArcademiaMode Mode;
        public bool Offline;
        public string GameName;
        public AchievementScope Scope;
        public string TeamLabel;
        public Achievement[] Achievements = new Achievement[0];

        public Achievement Find(string apiName)
        {
            foreach (var a in Achievements)
                if (string.Equals(a.ApiName, apiName, StringComparison.Ordinal))
                    return a;
            return null;
        }

        public override string ToString() =>
            Success
                ? $"[{Mode}] {GameName} ({Scope}{(string.IsNullOrEmpty(TeamLabel) ? "" : ", team " + TeamLabel)}) - {Achievements.Length} achievement(s)" + (Offline ? " (offline cache)" : "")
                : $"[{Mode}] failed - {Message}";
    }

    public class UnlockResult
    {
        public bool Success;
        public string Status;
        public string ApiName;
        public string Name;
        public string Description;
        public string IconUrl;
        public string IconPath;
        public bool AllowPersonal;
        public bool TeamHadIt;
        public string TeamLabel;
        public string TeamClaimedBy;
        public AchievementScope Scope;
        public bool Offline;
        public bool ShowToastInGame;
        public string Message;
        public ArcademiaMode Mode;

        public bool IsNewThisSession => Status == "unlocked";

        public override string ToString() =>
            $"[{Mode}] {ApiName}: {Status}"
            + (Success ? "" : " (failed)")
            + (TeamHadIt ? $" - {TeamLabel} already had it" + (string.IsNullOrEmpty(TeamClaimedBy) ? "" : $" (claimed by {TeamClaimedBy})") : "")
            + (Offline ? " - queued offline" : "")
            + (ShowToastInGame ? " - draw the toast in game" : "")
            + (string.IsNullOrEmpty(Message) ? "" : $" - {Message}");
    }

    public class AchievementToast
    {
        public string ApiName;
        public string Name;
        public string Description;
        public string IconUrl;
        public string IconPath;
        public bool TeamHadIt;
        public string TeamLabel;
        public bool AllowPersonal;
        public byte[] IconBytes;

        public string IconExtension => ArcademiaAchievements.SniffImageExtension(IconBytes);

        public string Title => TeamHadIt
            ? "Already held by " + (string.IsNullOrEmpty(TeamLabel) ? "your team" : TeamLabel)
            : "Achievement Unlocked";

        public string Subtitle => TeamHadIt
            ? (AllowPersonal ? "Claim it for yourself after the game" : "Your team already has this one")
            : (string.IsNullOrEmpty(Description) ? "Claim it after the game" : Description);
    }

    public class AchievementsPingResult
    {
        public bool Success;
        public ArcademiaMode Mode;
        public int GameId;
        public string GameName;
        public AchievementScope Scope;
        public string Environment;
        public string Message;

        public override string ToString() =>
            Success
                ? $"[{Mode}] OK - {Environment}" + (string.IsNullOrEmpty(GameName) ? "" : $" - game \"{GameName}\" (#{GameId}), scope {Scope}")
                : $"[{Mode}] failed - {Message}";
    }

    public class OverlayResult
    {
        public bool Success;
        public string Status;
        public string Message;
        public ArcademiaMode Mode;

        public bool DrawInGame => Status == "renderInGame";

        public override string ToString() =>
            $"[{Mode}] overlay {Status}" + (string.IsNullOrEmpty(Message) ? "" : $" - {Message}");
    }

    public class SandboxClaimResult
    {
        public bool Success;
        public string Status;
        public string ClaimUrl;
        public string ClaimedBy;
        public string Message;

        public override string ToString() =>
            $"[Sandbox] claim {Status}"
            + (string.IsNullOrEmpty(ClaimedBy) ? "" : $" by {ClaimedBy}")
            + (string.IsNullOrEmpty(Message) ? "" : $" - {Message}");
    }
}
