using BgDataTypes_Lib;

namespace XgAnalytics;

// -----------------------------------------------------------------------------
//  Computed analysis results
//
//  These immutable records are the return values of the `Analyses.Compute*`
//  aggregators — the pure computation extracted out of the side-effecting
//  `void` analysis methods. They expose the aggregated data as read-only views
//  so callers (the CSV-writing wrappers, and tests asserting shape invariants)
//  can consume the result without re-running the scan or touching the corpus.
//
//  All `internal`, matching `Analyses` — they are its return types, and this
//  library has no consumer outside its own test project (which sees them via
//  the `InternalsVisibleTo` in XgAnalytics.csproj).
// -----------------------------------------------------------------------------

/// <summary>
/// One player's match tally: the player name and the distinct match IDs
/// (file names, without extension) in which the player appears.
/// </summary>
/// <param name="Player">Player name; never blank (blank names are not registered).</param>
/// <param name="MatchIds">Distinct match IDs this player appears in; always non-empty.</param>
internal sealed record PlayerMatchTally(string Player, IReadOnlyCollection<string> MatchIds)
{
    /// <summary>Number of distinct matches this player appears in (≥ 1).</summary>
    public int MatchCount => MatchIds.Count;
}

/// <summary>
/// Result of <see cref="Analyses.ComputePlayerMatchCount"/>: every player seen
/// across the corpus with the matches they appear in.
/// </summary>
/// <param name="Players">
/// Players ordered by descending match count, then name — the same order the
/// CSV and summary table use.
/// </param>
/// <param name="DistinctMatchCount">
/// Count of distinct match IDs across all players. Bounded above by the number
/// of XG-format files enumerated (a file registers at most one match ID).
/// </param>
internal sealed record PlayerMatchCountResult(
    IReadOnlyList<PlayerMatchTally> Players,
    int DistinctMatchCount);

/// <summary>
/// One game that did not start from the standard backgammon opening position.
/// </summary>
/// <param name="Match">Match ID (file name without extension); never blank.</param>
/// <param name="Game">1-based game number within its match (≥ 1).</param>
/// <param name="Player1">Player 1 name, or empty if unavailable.</param>
/// <param name="Player2">Player 2 name, or empty if unavailable.</param>
internal sealed record NonStandardStart(string Match, int Game, string Player1, string Player2);

/// <summary>
/// Result of <see cref="Analyses.ComputeNonStandardStarts"/>: the games whose
/// opening position was non-standard, plus the totals scanned.
/// </summary>
/// <param name="NonStandard">
/// Flagged games — a subset of all games seen, so
/// <c>NonStandard.Count ≤ GameCount</c>.
/// </param>
/// <param name="GameCount">Total games scanned across all matches.</param>
/// <param name="MatchCount">Total matches (files) scanned.</param>
internal sealed record NonStandardStartsResult(
    IReadOnlyList<NonStandardStart> NonStandard,
    int GameCount,
    int MatchCount);

/// <summary>
/// Normalized match-score bucket: the away-point scores at the start of a game,
/// with <see cref="Away1"/> ≤ <see cref="Away2"/> so both player perspectives
/// collapse onto one key.
/// </summary>
/// <param name="MatchLength">Match length in points (0 = money session).</param>
/// <param name="Away1">Lower away score (points still needed); ≤ <see cref="Away2"/>.</param>
/// <param name="Away2">Higher away score (points still needed).</param>
/// <param name="IsCrawford">Whether the Crawford rule applied to the game.</param>
internal readonly record struct MatchScoreKey(int MatchLength, int Away1, int Away2, bool IsCrawford);

/// <summary>
/// Result of <see cref="Analyses.ComputeMatchScoreDistribution"/>: how many
/// games fell into each normalized score bucket, plus the totals scanned.
/// </summary>
/// <param name="Counts">
/// Occurrence count per <see cref="MatchScoreKey"/>. Every value is ≥ 1 and the
/// values sum to <see cref="GameCount"/>.
/// </param>
/// <param name="GameCount">Total games scanned across all matches.</param>
/// <param name="MatchCount">Total matches (files) scanned.</param>
internal sealed record MatchScoreDistributionResult(
    IReadOnlyDictionary<MatchScoreKey, int> Counts,
    int GameCount,
    int MatchCount);


/// <summary>
/// One content-equivalence class of duplicate problems: a
/// <see cref="ProblemKey"/> that two or more scanned decisions derived, and
/// the occurrences that derived it.
/// </summary>
/// <param name="Key">The shared content identity (see <see cref="ProblemKey"/>).</param>
/// <param name="Occurrences">
/// The decisions that derived <paramref name="Key"/>, ordered by
/// <see cref="DecisionId.Filename"/> ordinal-ascending (ties — several
/// occurrences inside one file — keep scan order). Always holds two or more
/// entries; a key with a single occurrence is not a duplicate and forms no
/// class.
/// </param>
internal sealed record DuplicateProblemGroup(ProblemKey Key, IReadOnlyList<DecisionId> Occurrences)
{
    /// <summary>
    /// The surviving occurrence — the ordinal-first filename in the class, per
    /// the ratified keeper rule. Report-only: nothing here deletes anything.
    /// </summary>
    public DecisionId Keeper => Occurrences[0];

    /// <summary>How many occurrences in this class are redundant copies (≥ 1).</summary>
    public int RedundantCount => Occurrences.Count - 1;
}

/// <summary>
/// Result of <see cref="Analyses.ComputeDuplicateProblems"/>: the corpus's
/// content-duplicate problems, grouped by <see cref="ProblemKey"/>, plus the
/// files that carry nothing but redundant copies.
///
/// <para>
/// <b>Report-only.</b> Nothing here deletes; <see cref="RedundantFiles"/> is a
/// recommendation the caller acts on (halheinrich/backgammon#117).
/// </para>
///
/// <para>
/// <b>Fail-open on an underivable key.</b> A decision with no derivable
/// <see cref="ProblemKey"/> (the no-key rung — see
/// <see cref="ProblemKey.TryDerive"/>) forms no class and is never reported
/// redundant, even when several such decisions are otherwise identical:
/// treating underivability as equality would collapse unrelated problems.
/// It counts toward both <see cref="ProblemCount"/> and
/// <see cref="NoKeyCount"/>, and as its own entry in
/// <see cref="DistinctProblemCount"/>. Consequently a file holding one keeps
/// that file out of <see cref="RedundantFiles"/>.
/// </para>
/// </summary>
/// <param name="Groups">
/// The duplicate classes, ordered by <see cref="ProblemKey"/>. Empty when
/// nothing collapsed.
/// </param>
/// <param name="RedundantFiles">
/// Bare filenames (relative to the scanned directory — the scan is one flat
/// directory, so names are unique within it), ordinal-ascending, of files
/// every one of whose problems survives elsewhere: the file contributed at
/// least one decision, and not one of its decisions is either a class keeper,
/// a problem seen only there, or a no-key item. Deleting exactly this set
/// loses no problem.
/// </param>
/// <param name="FileCount">
/// Distinct files that contributed at least one decision. Bounded above by the
/// number of XG-format files enumerated; files that were unreadable or carried
/// no analysed decision are excluded (the scan logs those totals).
/// </param>
/// <param name="ProblemCount">Total decisions scanned, no-key items included.</param>
/// <param name="DistinctProblemCount">
/// Distinct problems: one per derived <see cref="ProblemKey"/>, plus one per
/// no-key item (fail-open — no-key items never merge, with each other or with
/// anything else). Equals <see cref="ProblemCount"/> when nothing collapsed.
/// </param>
/// <param name="NoKeyCount">
/// Decisions with no derivable key; ≤ <see cref="ProblemCount"/>.
/// </param>
internal sealed record DuplicateProblemsResult(
    IReadOnlyList<DuplicateProblemGroup> Groups,
    IReadOnlyList<string> RedundantFiles,
    int FileCount,
    int ProblemCount,
    int DistinctProblemCount,
    int NoKeyCount)
{
    /// <summary>
    /// Redundant problem occurrences — the copies beyond the first in every
    /// class. Equals <see cref="ProblemCount"/> − <see cref="DistinctProblemCount"/>
    /// and the sum of every group's <see cref="DuplicateProblemGroup.RedundantCount"/>.
    /// </summary>
    public int RedundantProblemCount => ProblemCount - DistinctProblemCount;
}
