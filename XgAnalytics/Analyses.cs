using BgDataTypes_Lib;
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
    //  Analysis: Duplicate problems (halheinrich/backgammon#117)
    //
    //  The only analysis that needs the full decision parse rather than the
    //  fast-path metadata readers — content identity is a property of the
    //  decision, not of the file header. See INSTRUCTIONS.md "Depends on".
    // -------------------------------------------------------------------------

    /// <summary>
    /// Scans <paramref name="xgDir"/>'s XG-format files, derives every analysed
    /// decision's <see cref="ProblemKey"/>, and reports the content-duplicate
    /// problems plus the files that carry nothing but redundant copies.
    /// Report-only — nothing is deleted (halheinrich/backgammon#117).
    /// </summary>
    /// <param name="xgDir">Directory of <c>.xg</c> / <c>.xgp</c> files.</param>
    /// <param name="log">Progress and summary sink; the only side effect.</param>
    public static DuplicateProblemsResult ComputeDuplicateProblems(string xgDir, Action<string> log)
    {
        var sw = Stopwatch.StartNew();

        int fileCount = 0;
        int unreadableCount = 0;
        int decisionCount = 0;
        int nextReport = 1;

        // Local iterator: streams decisions into the pure grouping core while
        // keeping the scan's own counters (files, skips, rate) out of it. The
        // core never sees a path or a Stopwatch.
        IEnumerable<BgDecisionData> Scan()
        {
            foreach (var path in XgFileReader.EnumerateXgFormatFiles(xgDir))
            {
                fileCount++;

                // IterateDiagramRequests is lazy, so a content-level parse
                // failure surfaces during enumeration, not at the call — hence
                // the per-file materialization inside the try. Bounded by one
                // file's decision count. Same silent-skip policy as the other
                // analyses (see Pitfalls), but counted here rather than lost.
                List<BgDecisionData> decisions;
                try
                {
                    var file = XgFileReader.ReadFile(path);
                    decisions = XgDecisionIterator
                        .IterateDiagramRequests(file, Path.GetFileName(path))
                        .ToList();
                }
                catch { unreadableCount++; continue; }

                decisionCount += decisions.Count;
                foreach (var decision in decisions)
                    yield return decision;

                if (fileCount >= nextReport)
                {
                    double secs = sw.Elapsed.TotalSeconds;
                    double rate = secs > 0 ? fileCount / secs : 0;
                    log($"  {fileCount,6} files  {decisionCount,8} decisions  {secs,7:F1}s  {rate:F2} files/sec");
                    while (nextReport <= fileCount) nextReport *= 2;
                }
            }
        }

        var result = GroupDuplicateProblems(Scan());

        sw.Stop();
        double totalSecs = sw.Elapsed.TotalSeconds;
        double finalRate = totalSecs > 0 ? fileCount / totalSecs : 0;
        double redundantPct = result.ProblemCount > 0
            ? 100.0 * result.RedundantProblemCount / result.ProblemCount
            : 0;

        log("");
        log($"Files scanned       : {fileCount}");
        log($"Files unreadable    : {unreadableCount}");
        log($"Files with problems : {result.FileCount}");
        log($"Problems            : {result.ProblemCount}");
        log($"Distinct problems   : {result.DistinctProblemCount}");
        log($"Redundant problems  : {result.RedundantProblemCount} ({redundantPct:F2}%)");
        log($"Duplicate groups    : {result.Groups.Count}");
        log($"No-key (fail open)  : {result.NoKeyCount}");
        log($"Redundant files     : {result.RedundantFiles.Count}");
        log($"Total time          : {totalSecs:F1}s");
        log($"Avg rate            : {finalRate:F2} files/sec");

        return result;
    }

    /// <summary>
    /// The pure grouping core of <see cref="ComputeDuplicateProblems"/>: groups
    /// decisions by derived <see cref="ProblemKey"/> and settles which files are
    /// wholly redundant. No file access, no logging, no ordering assumption
    /// about the input — the seam that lets the fail-open rule be tested with a
    /// synthesized record no corpus is required to contain.
    ///
    /// <para>
    /// Streams: only a <see cref="DecisionId"/> per decision is retained, never
    /// the record itself.
    /// </para>
    ///
    /// <para>
    /// <b>Fail open.</b> A decision whose <see cref="ProblemKey"/> will not
    /// derive is never merged with anything and never reported redundant,
    /// matching <c>DistinctPositionProblemSetSource</c> (BgGame_Lib). Under the
    /// v3 key this is a live population, not a theoretical one: a money record
    /// that does not carry the Jacoby fact is underivable by design
    /// (halheinrich/backgammon#120) — guessing "off" is exactly what the no-key
    /// rung forbids.
    /// </para>
    /// </summary>
    /// <param name="decisions">The decisions to group; enumerated once.</param>
    /// <exception cref="ArgumentNullException"><paramref name="decisions"/> is null.</exception>
    public static DuplicateProblemsResult GroupDuplicateProblems(IEnumerable<BgDecisionData> decisions)
    {
        ArgumentNullException.ThrowIfNull(decisions);

        var occurrencesByKey = new Dictionary<ProblemKey, List<DecisionId>>();

        // Per-file tallies. `essentialByFile` counts the occurrences a file
        // cannot lose: no-key items (fail open) immediately, class keepers once
        // every class is closed — which occurrence keeps is unknown until the
        // whole scan is in, so keepers are settled below. A file is redundant
        // iff it contributed decisions and none of them is essential.
        var occurrencesByFile = new Dictionary<string, int>(StringComparer.Ordinal);
        var essentialByFile = new Dictionary<string, int>(StringComparer.Ordinal);

        int problemCount = 0;
        int noKeyCount = 0;

        foreach (var data in decisions)
        {
            problemCount++;
            string file = data.Id.Filename;
            occurrencesByFile[file] = occurrencesByFile.GetValueOrDefault(file) + 1;

            if (!ProblemKey.TryDerive(data, out var key))
            {
                noKeyCount++;
                essentialByFile[file] = essentialByFile.GetValueOrDefault(file) + 1;
                continue;
            }

            if (!occurrencesByKey.TryGetValue(key, out var occurrences))
                occurrencesByKey[key] = occurrences = [];
            occurrences.Add(data.Id);
        }

        var groups = new List<DuplicateProblemGroup>();
        foreach (var (key, occurrences) in occurrencesByKey)
        {
            // Keeper = ordinal-first filename (ratified). OrderBy is stable, so
            // several occurrences inside one file keep scan order.
            var ordered = occurrences
                .OrderBy(id => id.Filename, StringComparer.Ordinal)
                .ToList();

            string keeperFile = ordered[0].Filename;
            essentialByFile[keeperFile] = essentialByFile.GetValueOrDefault(keeperFile) + 1;

            if (ordered.Count > 1)
                groups.Add(new DuplicateProblemGroup(key, ordered));
        }

        var redundantFiles = occurrencesByFile.Keys
            .Where(file => essentialByFile.GetValueOrDefault(file) == 0)
            .Order(StringComparer.Ordinal)
            .ToList();

        return new DuplicateProblemsResult(
            Groups: groups.OrderBy(g => g.Key).ToList(),
            RedundantFiles: redundantFiles,
            FileCount: occurrencesByFile.Count,
            ProblemCount: problemCount,
            DistinctProblemCount: occurrencesByKey.Count + noKeyCount,
            NoKeyCount: noKeyCount);
    }

    public static void DuplicateProblems(string xgDir, Action<string> log)
    {
        var result = ComputeDuplicateProblems(xgDir, log);

        string csvPath = @"D:\Users\Hal\Documents\Excel\Backgammon\DuplicateProblems.csv";
        var redundantFiles = result.RedundantFiles.ToHashSet(StringComparer.Ordinal);

        using var writer = new StreamWriter(csvPath);
        writer.WriteLine("ProblemKey,File,DecisionId,IsGroupKeeper,IsFileRedundant");
        foreach (var group in result.Groups)
        {
            foreach (var id in group.Occurrences)
            {
                writer.WriteLine(string.Join(',',
                    CsvEscape(group.Key.ToString()),
                    CsvEscape(id.Filename),
                    CsvEscape(id.ToString()),
                    id == group.Keeper ? 1 : 0,
                    redundantFiles.Contains(id.Filename) ? 1 : 0));
            }
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
