using ConvertXgToJson_Lib;
using ConvertXgToJson_Lib.Models;
using System.Diagnostics;

namespace XgAnalytics;

public static class Analyses
{
    // -------------------------------------------------------------------------
    //  Analysis: Player match count
    // -------------------------------------------------------------------------

    public static void PlayerMatchCount(string xgDir, Action<string> log)
    {
        var playerMatches = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var sw = Stopwatch.StartNew();

        int matchCount = 0;
        int nextReport = 1;

        foreach (var path in Directory.EnumerateFiles(xgDir, "*.xg"))
        {
            XgMatchInfo info;
            try { info = XgFileReader.ReadMatchInfo(path); }
            catch { continue; }

            string matchId = Path.GetFileNameWithoutExtension(path);
            RegisterPlayer(info.Player1, matchId, playerMatches);
            RegisterPlayer(info.Player2, matchId, playerMatches);

            matchCount++;

            if (matchCount >= nextReport)
            {
                double secs = sw.Elapsed.TotalSeconds;
                double rate = secs > 0 ? matchCount / secs : 0;
                log($"  {matchCount,6} matches  {secs,7:F1}s  {rate:F2} matches/sec");
                while (nextReport <= matchCount) nextReport *= 2;
            }
        }

        sw.Stop();
        double totalSecs = sw.Elapsed.TotalSeconds;
        double finalRate = totalSecs > 0 ? matchCount / totalSecs : 0;

        var sorted = playerMatches
            .OrderByDescending(kv => kv.Value.Count)
            .ThenBy(kv => kv.Key)
            .ToList();

        log("");
        log($"{"Player",-30} {"Matches",7}");
        log(new string('-', 39));
        foreach (var (player, matches) in sorted)
            log($"{player,-30} {matches.Count,7}");
        log(new string('-', 39));
        log($"{"Total players:",-30} {sorted.Count,7}");
        log("");
        log($"Total matches : {matchCount}");
        log($"Total time    : {totalSecs:F1}s");
        log($"Avg rate      : {finalRate:F2} matches/sec");

        string csvPath = @"D:\Users\Hal\Documents\Excel\Backgammon\PlayerMatchCount.csv";
        using var writer = new StreamWriter(csvPath);
        writer.WriteLine("Player,Matches");
        foreach (var (player, matches) in sorted)
            writer.WriteLine($"{CsvEscape(player)},{matches.Count}");

        log($"CSV written to: {csvPath}");
    }

    // -------------------------------------------------------------------------
    //  Analysis: Non-standard game starts
    // -------------------------------------------------------------------------

    public static void NonStandardStarts(string xgDir, Action<string> log)
    {
        var nonStandard = new List<(string Match, int Game, string Player1, string Player2)>();
        var sw = Stopwatch.StartNew();
        var state = new XgIteratorState();

        int gameCount = 0;
        int matchCount = 0;
        int nextReport = 1;

        foreach (var path in Directory.EnumerateFiles(xgDir, "*.xg"))
        {
            int gameNum = 0;
            try
            {
                foreach (var game in XgFileReader.ReadGameHeaders(path, state))
                {
                    gameNum++;
                    gameCount++;
                    if (!game.IsStandardStart)
                        nonStandard.Add((
                            Path.GetFileNameWithoutExtension(path),
                            gameNum,
                            state.MatchInfo?.Player1 ?? "",
                            state.MatchInfo?.Player2 ?? ""));
                }
            }
            catch { continue; }

            matchCount++;
            if (matchCount >= nextReport)
            {
                double secs = sw.Elapsed.TotalSeconds;
                double rate = secs > 0 ? matchCount / secs : 0;
                double pct = gameCount > 0 ? 100.0 * nonStandard.Count / gameCount : 0;
                log($"  {matchCount,6} matches  {gameCount,8} games  {secs,7:F1}s  {rate:F2} matches/sec  {pct:F1}% non-standard");
                while (nextReport <= matchCount) nextReport *= 2;
            }
        }

        sw.Stop();
        double totalSecs = sw.Elapsed.TotalSeconds;
        double finalRate = totalSecs > 0 ? matchCount / totalSecs : 0;
        double finalPct = gameCount > 0 ? 100.0 * nonStandard.Count / gameCount : 0;

        log("");
        log($"Total matches     : {matchCount}");
        log($"Total games       : {gameCount}");
        log($"Non-standard      : {nonStandard.Count} ({finalPct:F2}%)");
        log($"Total time        : {totalSecs:F1}s");
        log($"Avg rate          : {finalRate:F2} matches/sec");

        string csvPath = @"D:\Users\Hal\Documents\Excel\Backgammon\NonStandardStarts.csv";
        using var writer = new StreamWriter(csvPath);
        writer.WriteLine("Match,Game,Player1,Player2");
        foreach (var (match, game, p1, p2) in nonStandard)
            writer.WriteLine($"{CsvEscape(match)},{game},{CsvEscape(p1)},{CsvEscape(p2)}");

        log($"CSV written to: {csvPath}");
    }

    // -------------------------------------------------------------------------
    //  Analysis: Match score distribution
    // -------------------------------------------------------------------------

    public static void MatchScoreDistribution(string xgDir, Action<string> log)
    {
        // Key: (MatchLength, Away1, Away2, IsCrawford) where Away1 <= Away2 (normalized)
        var counts = new Dictionary<(int MatchLength, int Away1, int Away2, bool IsCrawford), int>();
        var sw = Stopwatch.StartNew();
        var state = new XgIteratorState();

        int gameCount = 0;
        int matchCount = 0;
        int nextReport = 1;

        foreach (var path in Directory.EnumerateFiles(xgDir, "*.xg"))
        {
            try
            {
                foreach (var game in XgFileReader.ReadGameHeaders(path, state))
                {
                    gameCount++;

                    int ml = state.MatchInfo?.MatchLength ?? 0;
                    int a1 = game.Away1;
                    int a2 = game.Away2;

                    // Normalize: lower away score first
                    if (a1 > a2) (a1, a2) = (a2, a1);

                    var key = (ml, a1, a2, game.IsCrawfordGame);
                    counts.TryGetValue(key, out int existing);
                    counts[key] = existing + 1;
                }
            }
            catch { continue; }

            matchCount++;
            if (matchCount >= nextReport)
            {
                double secs = sw.Elapsed.TotalSeconds;
                double rate = secs > 0 ? matchCount / secs : 0;
                log($"  {matchCount,6} matches  {gameCount,8} games  {secs,7:F1}s  {rate:F2} matches/sec");
                while (nextReport <= matchCount) nextReport *= 2;
            }
        }

        sw.Stop();
        double totalSecs = sw.Elapsed.TotalSeconds;
        double finalRate = totalSecs > 0 ? matchCount / totalSecs : 0;

        log("");
        log($"Total matches : {matchCount}");
        log($"Total games   : {gameCount}");
        log($"Total time    : {totalSecs:F1}s");
        log($"Avg rate      : {finalRate:F2} matches/sec");

        string csvPath = @"D:\Users\Hal\Documents\Excel\Backgammon\MatchScoreDistribution.csv";
        using var writer = new StreamWriter(csvPath);
        writer.WriteLine("MatchLength,Away1,Away2,IsCrawford,Occurs");
        foreach (var ((ml, a1, a2, isCrawford), occurs) in counts
            .OrderBy(kv => kv.Key.MatchLength)
            .ThenBy(kv => kv.Key.Away1)
            .ThenBy(kv => kv.Key.Away2)
            .ThenBy(kv => kv.Key.IsCrawford))
        {
            writer.WriteLine($"{ml},{a1},{a2},{(isCrawford ? 1 : 0)},{occurs}");
        }
        log($"CSV written to: {csvPath}");
    }

    // -------------------------------------------------------------------------
    //  Helpers
    // -------------------------------------------------------------------------

    static void RegisterPlayer(string player, string match,
        Dictionary<string, HashSet<string>> playerMatches)
    {
        if (string.IsNullOrWhiteSpace(player)) return;
        if (!playerMatches.TryGetValue(player, out var matches))
        {
            matches = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            playerMatches[player] = matches;
        }
        matches.Add(match);
    }

    static string CsvEscape(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}