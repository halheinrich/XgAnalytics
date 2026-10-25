namespace XgAnalytics;

/// <summary>
/// One report the command entry point can run: its command-line name, the
/// input directory it scans when none is given, and the CSV-writing analysis.
/// </summary>
/// <param name="Name">The command-line name, which is the analysis' method name.</param>
/// <param name="DefaultDirectory">The input directory scanned when the command names none.</param>
/// <param name="Run">The analysis: scans a directory, logs, and writes its CSV.</param>
internal sealed record Report(string Name, string DefaultDirectory, Action<string, Action<string>> Run);

/// <summary>
/// The reports the command entry point runs, one per <c>void</c> wrapper on
/// <see cref="Analyses"/>, with the input directories they scan by default.
/// </summary>
internal static class Reports
{
    /// <summary>Hal's match database, scanned by the three header analyses.</summary>
    public const string XgDir = @"D:\Users\Hal\Documents\eXtremeGammon\BatchAnalyze\Matches\hhDb\Xg";

    // DuplicateProblems' natural input is a BatchAnalyze *Positions* folder
    // rather than the match database above — that is the folder-cleanup use
    // case it exists for (halheinrich/backgammon#117). A knob: repoint it at
    // whichever folder is being cleaned, or pass the folder on the command line.
    /// <summary>The BatchAnalyze positions folder <c>DuplicateProblems</c> scans.</summary>
    public const string PositionsDir =
        @"D:\Users\Hal\Documents\eXtremeGammon\BatchAnalyze\Positions\Move2\3a3a";

    /// <summary>Every report, in the order the usage text lists them.</summary>
    public static IReadOnlyList<Report> All { get; } =
    [
        new(nameof(Analyses.PlayerMatchCount), XgDir, Analyses.PlayerMatchCount),
        new(nameof(Analyses.NonStandardStarts), XgDir, Analyses.NonStandardStarts),
        new(nameof(Analyses.MatchScoreDistribution), XgDir, Analyses.MatchScoreDistribution),
        new(nameof(Analyses.DuplicateProblems), PositionsDir, Analyses.DuplicateProblems),
    ];
}
