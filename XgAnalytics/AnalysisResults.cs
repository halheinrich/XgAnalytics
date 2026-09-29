using BgDataTypes_Lib;

namespace XgAnalytics;

// -----------------------------------------------------------------------------
//  Computed analysis results
//
//  These immutable records are the return values of the `Analyses.Compute*`
//  aggregators — the pure computation extracted out of the side-effecting
//  `void` analysis methods. Callers (the CSV-writing wrappers, and tests
//  asserting shape invariants) consume the result without re-running the scan
//  or touching the corpus.
//
//  Every collection a record holds is an immutable copy made where the record
//  is built (`ImmutableCopy`), by construction and by `with` alike: neither
//  the collection the caller passed nor a cast of the read-only view handed
//  out can change a record afterwards.
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
    private readonly IReadOnlyCollection<string> _matchIds = ImmutableCopy.Of(MatchIds, nameof(MatchIds));

    /// <summary>
    /// Distinct match IDs this player appears in; an immutable copy, so neither
    /// the caller's collection nor a cast can change it.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown on init when the value is <see langword="null"/>.</exception>
    public IReadOnlyCollection<string> MatchIds
    {
        get => _matchIds;
        init => _matchIds = ImmutableCopy.Of(value, nameof(MatchIds));
    }

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
    int DistinctMatchCount)
{
    private readonly IReadOnlyList<PlayerMatchTally> _players = ImmutableCopy.Of(Players, nameof(Players));

    /// <summary>
    /// Players ordered by descending match count, then name; an immutable copy,
    /// so neither the caller's list nor a cast can change it.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown on init when the value is <see langword="null"/>.</exception>
    public IReadOnlyList<PlayerMatchTally> Players
    {
        get => _players;
        init => _players = ImmutableCopy.Of(value, nameof(Players));
    }
}

/// <summary>
/// One game that did not start from the standard backgammon opening position.
/// </summary>
/// <param name="Match">Match ID (file name without extension); never blank.</param>
/// <param name="Game">1-based game number within its match (≥ 1).</param>
/// <param name="Player1">Player 1 name, as the match header records it (possibly empty).</param>
/// <param name="Player2">Player 2 name, as the match header records it (possibly empty).</param>
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
    int MatchCount)
{
    private readonly IReadOnlyList<NonStandardStart> _nonStandard = ImmutableCopy.Of(NonStandard, nameof(NonStandard));

    /// <summary>
    /// Flagged games; an immutable copy, so neither the caller's list nor a
    /// cast can change it.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown on init when the value is <see langword="null"/>.</exception>
    public IReadOnlyList<NonStandardStart> NonStandard
    {
        get => _nonStandard;
        init => _nonStandard = ImmutableCopy.Of(value, nameof(NonStandard));
    }
}

/// <summary>
/// Normalized match-score bucket: a match game's away-point scores as it
/// begins, with <see cref="Away1"/> ≤ <see cref="Away2"/> so both player
/// perspectives collapse onto one key. A match's alone — a money game has no
/// match score, and is counted by its kind instead
/// (<see cref="MatchScoreDistributionResult.MoneyGameCount"/>).
/// </summary>
/// <param name="MatchLength">The match's length in points, its terms' (≥ 1).</param>
/// <param name="Away1">Lower away score (points still needed, ≥ 1); ≤ <see cref="Away2"/>.</param>
/// <param name="Away2">Higher away score (points still needed); ≤ <see cref="MatchLength"/>.</param>
/// <param name="IsCrawford">Whether the game is the Crawford game.</param>
internal readonly record struct MatchScoreKey(int MatchLength, int Away1, int Away2, bool IsCrawford);

/// <summary>
/// Result of <see cref="Analyses.ComputeMatchScoreDistribution"/>: how many
/// match games fell into each normalized score bucket, how many games were
/// played for money, plus the totals scanned. Money and match are told apart
/// by the session's kind, never by a stand-in score.
/// </summary>
/// <param name="Counts">
/// Occurrence count per <see cref="MatchScoreKey"/>, over match games only.
/// Every value is ≥ 1, and the values sum to <see cref="GameCount"/> less
/// <see cref="MoneyGameCount"/>.
/// </param>
/// <param name="MoneyGameCount">Games of money sessions, which have no match score.</param>
/// <param name="GameCount">Total games scanned across all files, money and match.</param>
/// <param name="MatchCount">Total files scanned — each a match or a money session.</param>
internal sealed record MatchScoreDistributionResult(
    IReadOnlyDictionary<MatchScoreKey, int> Counts,
    int MoneyGameCount,
    int GameCount,
    int MatchCount)
{
    private readonly IReadOnlyDictionary<MatchScoreKey, int> _counts = ImmutableCopy.OfDictionary(Counts, nameof(Counts));

    /// <summary>
    /// Occurrence count per match-score bucket; an immutable copy, so neither
    /// the caller's dictionary nor a cast can change it.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown on init when the value is <see langword="null"/>.</exception>
    public IReadOnlyDictionary<MatchScoreKey, int> Counts
    {
        get => _counts;
        init => _counts = ImmutableCopy.OfDictionary(value, nameof(Counts));
    }
}


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
    private readonly IReadOnlyList<DecisionId> _occurrences = ImmutableCopy.Of(Occurrences, nameof(Occurrences));

    /// <summary>
    /// The decisions that derived <see cref="Key"/>, keeper first; an immutable
    /// copy, so neither the caller's list nor a cast can change it.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown on init when the value is <see langword="null"/>.</exception>
    public IReadOnlyList<DecisionId> Occurrences
    {
        get => _occurrences;
        init => _occurrences = ImmutableCopy.Of(value, nameof(Occurrences));
    }

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
/// </summary>
/// <param name="Groups">
/// The duplicate classes, ordered by <see cref="ProblemKey"/>. Empty when
/// nothing collapsed.
/// </param>
/// <param name="RedundantFiles">
/// Bare filenames (relative to the scanned directory — the scan is one flat
/// directory, so names are unique within it), ordinal-ascending, of files
/// every one of whose problems survives elsewhere: the file contributed at
/// least one decision, and not one of its decisions is either a class keeper
/// or a problem seen only there. Deleting exactly this set loses no problem.
/// </param>
/// <param name="FileCount">
/// Distinct files that contributed at least one decision. Bounded above by the
/// number of XG-format files enumerated; files that were unreadable or carried
/// no analysed decision are excluded (the scan logs those totals).
/// </param>
/// <param name="ProblemCount">Total decisions scanned.</param>
/// <param name="DistinctProblemCount">
/// Distinct problems: one per <see cref="ProblemKey"/> the scanned decisions
/// derived. Equals <see cref="ProblemCount"/> when nothing collapsed.
/// </param>
internal sealed record DuplicateProblemsResult(
    IReadOnlyList<DuplicateProblemGroup> Groups,
    IReadOnlyList<string> RedundantFiles,
    int FileCount,
    int ProblemCount,
    int DistinctProblemCount)
{
    private readonly IReadOnlyList<DuplicateProblemGroup> _groups = ImmutableCopy.Of(Groups, nameof(Groups));
    private readonly IReadOnlyList<string> _redundantFiles = ImmutableCopy.Of(RedundantFiles, nameof(RedundantFiles));

    /// <summary>
    /// The duplicate classes, ordered by <see cref="ProblemKey"/>; an immutable
    /// copy, so neither the caller's list nor a cast can change it.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown on init when the value is <see langword="null"/>.</exception>
    public IReadOnlyList<DuplicateProblemGroup> Groups
    {
        get => _groups;
        init => _groups = ImmutableCopy.Of(value, nameof(Groups));
    }

    /// <summary>
    /// The wholly redundant files, ordinal-ascending; an immutable copy, so
    /// neither the caller's list nor a cast can change it.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown on init when the value is <see langword="null"/>.</exception>
    public IReadOnlyList<string> RedundantFiles
    {
        get => _redundantFiles;
        init => _redundantFiles = ImmutableCopy.Of(value, nameof(RedundantFiles));
    }

    /// <summary>
    /// Redundant problem occurrences — the copies beyond the first in every
    /// class. Equals <see cref="ProblemCount"/> − <see cref="DistinctProblemCount"/>
    /// and the sum of every group's <see cref="DuplicateProblemGroup.RedundantCount"/>.
    /// </summary>
    public int RedundantProblemCount => ProblemCount - DistinctProblemCount;
}
