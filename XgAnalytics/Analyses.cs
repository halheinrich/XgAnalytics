using ConvertXgToJson_Lib;
using ConvertXgToJson_Lib.Models;
using System.Diagnostics;

namespace XgAnalytics;

/// <remarks>
/// Kept <c>internal</c>: this library has no external consumer and no exe — its
/// test project is both the sole caller and the ad-hoc runner, and reaches these
/// members through the <c>InternalsVisibleTo</c> in <c>XgAnalytics.csproj</c>.
/// The result records in <c>AnalysisResults.cs</c> are likewise <c>internal</c>,
/// since they appear in these signatures. Should a real consumer ever appear,
/// widen deliberately as a designed public surface rather than by default.
/// </remarks>
internal static class Analyses
{
    // -------------------------------------------------------------------------
    //  Analysis: Player match count
    //
    //  Compute* aggregates the corpus and returns the data (with progress +
    //  summary logging via `log`); the void wrapper persists it to CSV. The
    //  split keeps the machine-dependent file write out of the computation path
    //  so the aggregation is testable without writing to disk.
    // -------------------------------------------------------------------------

    public static PlayerMatchCountResult ComputePlayerMatchCount(string xgDir, Action<string> log)
    {
        var playerMatches = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var sw = Stopwatch.StartNew();

        int matchCount = 0;
        int nextReport = 1;

        foreach (var path in XgFileReader.EnumerateXgFormatFiles(xgDir))
        {
            XgMatchInfo? info;
            try { info = XgFileReader.ReadMatchInfo(path); }
            catch { continue; }
            if (info is null) continue;

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
            .Select(kv => new PlayerMatchTally(kv.Key, kv.Value.ToArray()))
            .ToList();

        log("");
        log($"{"Player",-30} {"Matches",7}");
        log(new string('-', 39));
        foreach (var p in sorted)
            log($"{p.Player,-30} {p.MatchCount,7}");
        log(new string('-', 39));
        log($"{"Total players:",-30} {sorted.Count,7}");
        log("");
        log($"Total matches : {matchCount}");
        log($"Total time    : {totalSecs:F1}s");
        log($"Avg rate      : {finalRate:F2} matches/sec");

        int distinctMatchCount = playerMatches.Values
            .SelectMany(s => s)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        return new PlayerMatchCountResult(sorted, distinctMatchCount);
    }

    public static void PlayerMatchCount(string xgDir, Action<string> log)
    {
        var result = ComputePlayerMatchCount(xgDir, log);

        string csvPath = @"D:\Users\Hal\Documents\Excel\Backgammon\PlayerMatchCount.csv";
        using var writer = new StreamWriter(csvPath);
        writer.WriteLine("Player,Matches");
        foreach (var p in result.Players)
            writer.WriteLine($"{CsvEscape(p.Player)},{p.MatchCount}");

        log($"CSV written to: {csvPath}");
    }

    // -------------------------------------------------------------------------
    //  Analysis: Non-standard game starts
    // -------------------------------------------------------------------------

    public static NonStandardStartsResult ComputeNonStandardStarts(string xgDir, Action<string> log)
    {
        var nonStandard = new List<NonStandardStart>();
        var sw = Stopwatch.StartNew();
        var state = new XgIteratorState();

        int gameCount = 0;
        int matchCount = 0;
        int nextReport = 1;

        foreach (var path in XgFileReader.EnumerateXgFormatFiles(xgDir))
        {
            int gameNum = 0;
            try
            {
                foreach (var game in XgFileReader.ReadGameHeaders(path, state))
                {
                    gameNum++;
                    gameCount++;
                    if (!game.IsStandardStart)
                        nonStandard.Add(new NonStandardStart(
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

        return new NonStandardStartsResult(nonStandard, gameCount, matchCount);
    }

    public static void NonStandardStarts(string xgDir, Action<string> log)
    {
        var result = ComputeNonStandardStarts(xgDir, log);

        string csvPath = @"D:\Users\Hal\Documents\Excel\Backgammon\NonStandardStarts.csv";
        using var writer = new StreamWriter(csvPath);
        writer.WriteLine("Match,Game,Player1,Player2");
        foreach (var (match, game, p1, p2) in result.NonStandard)
            writer.WriteLine($"{CsvEscape(match)},{game},{CsvEscape(p1)},{CsvEscape(p2)}");

        log($"CSV written to: {csvPath}");
    }

    // -------------------------------------------------------------------------
    //  Analysis: Match score distribution
    // -------------------------------------------------------------------------

    public static MatchScoreDistributionResult ComputeMatchScoreDistribution(string xgDir, Action<string> log)
    {
        // Key: (MatchLength, Away1, Away2, IsCrawford) where Away1 <= Away2 (normalized)
        var counts = new Dictionary<MatchScoreKey, int>();
        var sw = Stopwatch.StartNew();
        var state = new XgIteratorState();

        int gameCount = 0;
        int matchCount = 0;
        int nextReport = 1;

        foreach (var path in XgFileReader.EnumerateXgFormatFiles(xgDir))
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

                    var key = new MatchScoreKey(ml, a1, a2, game.IsCrawfordGame);
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

        return new MatchScoreDistributionResult(counts, gameCount, matchCount);
    }

    public static void MatchScoreDistribution(string xgDir, Action<string> log)
    {
        var result = ComputeMatchScoreDistribution(xgDir, log);

        string csvPath = @"D:\Users\Hal\Documents\Excel\Backgammon\MatchScoreDistribution.csv";
        using var writer = new StreamWriter(csvPath);
        writer.WriteLine("MatchLength,Away1,Away2,IsCrawford,Occurs");
        foreach (var (key, occurs) in result.Counts
            .OrderBy(kv => kv.Key.MatchLength)
            .ThenBy(kv => kv.Key.Away1)
            .ThenBy(kv => kv.Key.Away2)
            .ThenBy(kv => kv.Key.IsCrawford))
        {
            writer.WriteLine($"{key.MatchLength},{key.Away1},{key.Away2},{(key.IsCrawford ? 1 : 0)},{occurs}");
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
