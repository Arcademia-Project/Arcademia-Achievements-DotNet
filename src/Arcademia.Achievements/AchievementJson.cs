using System;
using System.Globalization;
using System.Text;

namespace Arcademia.Achievements
{
    internal static class AchievementJson
    {
        public static string Quote(string value)
        {
            value = value ?? "";
            var sb = new StringBuilder(value.Length + 2);
            sb.Append('"');
            foreach (var c in value)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20)
                            sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else
                            sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
            return sb.ToString();
        }

        public static AchievementScope ParseScope(string value)
        {
            switch (value)
            {
                case "Sessional": return AchievementScope.Sessional;
                case "Local": return AchievementScope.Local;
                case "National": return AchievementScope.National;
                default: return AchievementScope.Institutional;
            }
        }
    }

    internal class AchievementDto
    {
        public string apiName;
        public string name;
        public string description;
        public string iconUrl;
        public string iconPath;
        public bool hidden;
        public bool allowPersonal;
        public int sortOrder;
    }

    internal class TeamHoldDto
    {
        public string apiName;
        public string firstUnlockedAt;
        public string claimedBy;
        public string claimedAt;
    }

    internal class AchievementSetDto
    {
        public int gameId;
        public string gameName;
        public string scope;
        public string teamKey;
        public string teamLabel;
        public AchievementDto[] achievements;
        public TeamHoldDto[] teamHolds;
        public string[] unlockedThisSession;
    }

    internal class UnlockResponseDto
    {
        public string status;
        public AchievementDto achievement;
        public bool teamHadIt;
        public string teamLabel;
        public string teamClaimedBy;
        public string scope;
        public string achievedAt;
    }

    internal class LauncherResponseDto
    {
        public string id;
        public bool ok;
        public string status;
        public string message;
        public string error;
        public string mode;
        public string sessionId;
        public bool offline;
        public string render;
        public UnlockResponseDto unlock;
        public AchievementSetDto set;
    }

    internal class PingResponseDto
    {
        public int gameId;
        public string gameName;
        public string scope;
        public string environment;
    }

    internal class SandboxClaimDto
    {
        public string claimUrl;
        public string code;
        public string expiresAt;
    }

    internal class SandboxClaimStatusDto
    {
        public string status;
        public string claimedBy;
    }
}
