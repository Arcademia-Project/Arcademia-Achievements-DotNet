using System;
using System.Linq;
using System.Threading.Tasks;
using Arcademia.Achievements;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        ArcademiaAchievements.Unlocked += r => Console.WriteLine("  event Unlocked: " + r.Name + (r.TeamHadIt ? " (team already had it)" : ""));
        ArcademiaAchievements.ToastRequested += t => Console.WriteLine($"  event ToastRequested: [{t.Title}] {t.Name} - {t.Subtitle}");
        ArcademiaAchievements.OverlayOpened += () => Console.WriteLine("  event OverlayOpened (pause your game)");
        ArcademiaAchievements.OverlayClosed += () => Console.WriteLine("  event OverlayClosed (resume your game)");

        Console.WriteLine("Arcademia Achievements Quick Start");
        Console.WriteLine("Mode: " + ArcademiaAchievements.Mode + "  |  session " + ArcademiaAchievements.SessionId);

        if (args.Contains("--self-test"))
            return await SelfTest();

        var apiBase = Prompt("API base", ArcademiaAchievements.Settings.apiBase);
        var key = Prompt("Achievements key", ArcademiaAchievements.Settings.achievementsKey);
        ArcademiaAchievements.Configure(new ArcademiaAchievementSettings { apiBase = apiBase, achievementsKey = key });

        while (true)
        {
            Console.WriteLine();
            Console.WriteLine("1) Ping  2) List  3) Unlock  4) Open overlay  5) New sandbox playthrough  6) Claim playthrough (sandbox)  7) Quit");
            Console.Write("> ");
            switch (Console.ReadLine())
            {
                case "1":
                    Console.WriteLine(await ArcademiaAchievements.PingAsync());
                    break;
                case "2":
                    var list = await ArcademiaAchievements.GetAchievementsAsync();
                    Console.WriteLine(list);
                    foreach (var a in list.Achievements)
                        Console.WriteLine("  " + a);
                    break;
                case "3":
                    Console.WriteLine(await ArcademiaAchievements.UnlockAsync(Prompt("API name", "FIRST_BLOOD")));
                    break;
                case "4":
                    Console.WriteLine(await ArcademiaAchievements.OpenOverlayAsync());
                    break;
                case "5":
                    ArcademiaAchievements.StartNewSandboxSession();
                    Console.WriteLine("New sandbox playthrough " + ArcademiaAchievements.SessionId);
                    break;
                case "6":
                    Console.WriteLine(await ArcademiaAchievements.RequestSandboxClaimAsync(url => Console.WriteLine("Open: " + url)));
                    break;
                case "7":
                    return 0;
            }
        }
    }

    private static async Task<int> SelfTest()
    {
        Console.WriteLine(await ArcademiaAchievements.PingAsync());
        var list = await ArcademiaAchievements.GetAchievementsAsync();
        Console.WriteLine(list);
        if (!list.Success)
            return 1;
        foreach (var a in list.Achievements)
            Console.WriteLine("  " + a);

        foreach (var a in list.Achievements.Take(3))
            Console.WriteLine(await ArcademiaAchievements.UnlockAsync(a.ApiName));
        if (list.Achievements.Length > 0)
            Console.WriteLine("repeat: " + await ArcademiaAchievements.UnlockAsync(list.Achievements[0].ApiName));
        Console.WriteLine("unknown: " + await ArcademiaAchievements.UnlockAsync("NOT_A_REAL_ACHIEVEMENT"));
        Console.WriteLine(await ArcademiaAchievements.OpenOverlayAsync());

        var after = await ArcademiaAchievements.GetAchievementsAsync();
        Console.WriteLine("unlocked this session: " + string.Join(", ", after.Achievements.Where(a => a.UnlockedThisSession).Select(a => a.ApiName)));
        return 0;
    }

    private static string Prompt(string label, string fallback)
    {
        Console.Write($"{label} [{fallback}]: ");
        var input = Console.ReadLine();
        return string.IsNullOrWhiteSpace(input) ? fallback : input.Trim();
    }
}
