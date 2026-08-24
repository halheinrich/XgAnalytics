using BgDataTypes_Lib;
using ConvertXgToJson_Lib;
using AwesomeAssertions;
using Xunit.Abstractions;

namespace XgAnalytics.Tests;

public class AnalysesTests(ITestOutputHelper output)
{
    // -------------------------------------------------------------------------
    //  Ad-hoc analysis-runner facts (manual driver, not CI checks)
    //
    //  These point at Hal's local match database and clobber CSVs under a
    //  hard-coded output directory. They are the user's ad-hoc way of *running*
    //  an analysis from Test Explorer — kept, not deleted. Both the input and
    //  the output directories are guarded so the facts self-skip (green) on any
    //  machine that lacks them, rather than failing.
    // -------------------------------------------------------------------------

    private const string XgDir = @"D:\Users\Hal\Documents\eXtremeGammon\BatchAnalyze\Matches\hhDb\Xg";
    private const string CsvOutputDir = @"D:\Users\Hal\Documents\Excel\Backgammon";

    // DuplicateProblems' natural input is a BatchAnalyze *Positions* folder
    // rather than the match database above — that is the folder-cleanup use
    // case it exists for (halheinrich/backgammon#117). A knob: repoint it at
    // whichever folder is being cleaned. Absent folder => the fact self-skips.
    private const string PositionsDir =
        @"D:\Users\Hal\Documents\eXtremeGammon\BatchAnalyze\Positions\Move2\3a3a";

    private static bool AdHocDirsPresent =>
        Directory.Exists(XgDir) && Directory.Exists(CsvOutputDir);

    [Fact]
    public void PlayerMatchCount()
    {
        if (!AdHocDirsPresent) return;
        Analyses.PlayerMatchCount(XgDir, output.WriteLine);
    }

    [Fact]
    public void NonStandardStarts()
    {
        if (!AdHocDirsPresent) return;
        Analyses.NonStandardStarts(XgDir, output.WriteLine);
    }

    [Fact]
    public void MatchScoreDistribution()
    {
        if (!AdHocDirsPresent) return;
        Analyses.MatchScoreDistribution(XgDir, output.WriteLine);
    }

    [Fact]
    public void DuplicateProblems()
    {
        if (!Directory.Exists(PositionsDir) || !Directory.Exists(CsvOutputDir)) return;
        Analyses.DuplicateProblems(PositionsDir, output.WriteLine);
    }

    // -------------------------------------------------------------------------
    //  Layer 1 — fixture-agnostic shape invariants over TestData/xg
    //
    //  Exercise the pure aggregators over whatever corpus exists and assert only
    //  relational, vacuous-safe invariants: each must hold at any corpus size,
    //  including zero. The corpus churns (files added/removed over time) and is
    //  empty on a fresh checkout, so the absence guard keeps these green where
    //  there is nothing to scan and meaningful where there is. Never pin a
    //  filename, a count, or global result non-emptiness.
    // -------------------------------------------------------------------------

    private static bool CorpusPresent =>
        Directory.Exists(TestPaths.XgDir)
        && XgFileReader.EnumerateXgFormatFiles(TestPaths.XgDir).Any();

    [Fact]
    public void PlayerMatchCount_OverCorpus_HoldsShapeInvariants()
    {
        if (!CorpusPresent) return;

        int fileCount = XgFileReader.EnumerateXgFormatFiles(TestPaths.XgDir).Count();
        var result = Analyses.ComputePlayerMatchCount(TestPaths.XgDir, output.WriteLine);

        // .All(...).Should().BeTrue() rather than .Should().OnlyContain(...): the
        // latter also asserts non-emptiness, but these invariants must hold
        // vacuously — a corpus may legitimately yield no players.
        result.Players.All(p => !string.IsNullOrWhiteSpace(p.Player)).Should().BeTrue(
            "a registered player always has a non-blank name");
        result.Players.All(p => p.MatchCount >= 1).Should().BeTrue(
            "a player is only registered because they appear in at least one match");
        result.DistinctMatchCount.Should().BeLessThanOrEqualTo(fileCount,
            "each enumerated file registers at most one distinct match ID");
    }

    [Fact]
    public void NonStandardStarts_OverCorpus_HoldsShapeInvariants()
    {
        if (!CorpusPresent) return;

        int fileCount = XgFileReader.EnumerateXgFormatFiles(TestPaths.XgDir).Count();
        var result = Analyses.ComputeNonStandardStarts(TestPaths.XgDir, output.WriteLine);

        result.NonStandard.Count.Should().BeLessThanOrEqualTo(result.GameCount,
            "flagged games are a subset of all games seen");
        // Vacuous-safe: a populated corpus may still contain zero non-standard
        // games, so these per-element checks must pass over an empty list.
        result.NonStandard.All(s => s.Game >= 1).Should().BeTrue(
            "game numbers are 1-based");
        result.NonStandard.All(s => !string.IsNullOrWhiteSpace(s.Match)).Should().BeTrue(
            "every flagged game comes from a real match file");
        result.MatchCount.Should().BeLessThanOrEqualTo(fileCount,
            "at most one match is scanned per enumerated file");
    }

    [Fact]
    public void MatchScoreDistribution_OverCorpus_HoldsShapeInvariants()
    {
        if (!CorpusPresent) return;

        int fileCount = XgFileReader.EnumerateXgFormatFiles(TestPaths.XgDir).Count();
        var result = Analyses.ComputeMatchScoreDistribution(TestPaths.XgDir, output.WriteLine);

        result.Counts.Values.Sum().Should().Be(result.GameCount,
            "every scanned game lands in exactly one bucket");
        // Vacuous-safe per-element checks (see PlayerMatchCount above).
        result.Counts.Keys.All(k => k.Away1 <= k.Away2).Should().BeTrue(
            "score keys are normalized so the lower away score is first");
        result.Counts.Values.All(v => v >= 1).Should().BeTrue(
            "a bucket exists only because at least one game fell into it");
        result.MatchCount.Should().BeLessThanOrEqualTo(fileCount,
            "at most one match is scanned per enumerated file");
    }

    // -------------------------------------------------------------------------
    //  Layer 2 — pinned-fixture discrimination over TestData/FixtureFiles
    //
    //  Layer 1 proves plumbing + invariants but not that the analyses actually
    //  discriminate: an empty result satisfies every Layer-1 invariant. This
    //  pins an append-only fixture whose *filename* independently encodes its
    //  facts — two named players, a 23-point match — and asserts the analyses
    //  recover them. Contents are gitignored, so presence is guarded. The file
    //  is copied into an isolated temp directory so the exact-count assertions
    //  are unaffected by other fixtures.
    // -------------------------------------------------------------------------

    [Fact]
    public void Analyses_OverPinnedFixture_RecoverKnownMatchFacts()
    {
        if (!File.Exists(TestPaths.AchimMuellerSeqXg)) return;

        string tempDir = Path.Combine(
            Path.GetTempPath(), "XgAnalytics.Tests_" + Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        try
        {
            File.Copy(
                TestPaths.AchimMuellerSeqXg,
                Path.Combine(tempDir, Path.GetFileName(TestPaths.AchimMuellerSeqXg)));

            // PlayerMatchCount: exactly the two participants, one match each.
            var players = Analyses.ComputePlayerMatchCount(tempDir, output.WriteLine);
            output.WriteLine("Players recovered: "
                + string.Join(", ", players.Players.Select(p => $"'{p.Player}'")));
            players.DistinctMatchCount.Should().Be(1, "the directory holds one match file");
            players.Players.Should().HaveCount(2, "a match has exactly two named players");
            players.Players.Should().OnlyContain(p => p.MatchCount == 1,
                "each player appears in the single match");

            // NonStandardStarts: one match scanned, with games.
            var nss = Analyses.ComputeNonStandardStarts(tempDir, output.WriteLine);
            nss.MatchCount.Should().Be(1, "exactly one match file was scanned");
            nss.GameCount.Should().BeGreaterThan(0, "a real match contains games");

            // MatchScoreDistribution: every bucket carries the known 23pt length,
            // and the buckets account for every scanned game.
            var dist = Analyses.ComputeMatchScoreDistribution(tempDir, output.WriteLine);
            dist.MatchCount.Should().Be(1, "exactly one match file was scanned");
            dist.GameCount.Should().BeGreaterThan(0, "a real match contains games");
            dist.Counts.Values.Sum().Should().Be(dist.GameCount,
                "every game lands in exactly one bucket");
            dist.Counts.Keys.Should().OnlyContain(k => k.MatchLength == 23,
                "the fixture's filename pins a 23-point match");
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { /* best-effort cleanup */ }
        }
    }

    // -------------------------------------------------------------------------
    //  DuplicateProblems (halheinrich/backgammon#117)
    //
    //  Layer 1 corpus invariants + Layer 2 discrimination, as above, plus a
    //  synthesized-record pin for the fail-open rule. The fail-open case is
    //  deliberately not sourced from `TestData/xg`: the corpus is
    //  fixture-agnostic and may be empty, and the in-tree producer stamps the
    //  Jacoby fact on every money record it emits — so the no-key rung would
    //  never fire from real data here even though it is a live population for
    //  records from laxer producers (halheinrich/backgammon#120).
    // -------------------------------------------------------------------------

    [Fact]
    public void DuplicateProblems_OverCorpus_HoldsShapeInvariants()
    {
        if (!CorpusPresent) return;

        int fileCount = XgFileReader.EnumerateXgFormatFiles(TestPaths.XgDir).Count();
        var result = Analyses.ComputeDuplicateProblems(TestPaths.XgDir, output.WriteLine);

        result.FileCount.Should().BeLessThanOrEqualTo(fileCount,
            "only enumerated files can contribute decisions");
        result.NoKeyCount.Should().BeLessThanOrEqualTo(result.ProblemCount,
            "no-key items are a subset of the decisions scanned");
        result.DistinctProblemCount.Should().BeLessThanOrEqualTo(result.ProblemCount,
            "collapsing copies can only reduce the distinct count");
        result.RedundantProblemCount.Should().Be(
            result.Groups.Sum(g => g.RedundantCount),
            "every redundant occurrence is a non-keeper member of exactly one class");

        // Vacuous-safe per-element checks: a corpus may legitimately contain no
        // duplicates at all, so these must pass over an empty group list.
        result.Groups.All(g => g.Occurrences.Count >= 2).Should().BeTrue(
            "a class exists only because a second copy contested the key");
        result.Groups.All(g => g.Keeper == g.Occurrences[0]).Should().BeTrue(
            "the keeper is the class's first occurrence");
        static bool OrdinalSortedByFilename(DuplicateProblemGroup group)
        {
            var names = group.Occurrences.Select(id => id.Filename).ToList();
            return names.SequenceEqual(names.Order(StringComparer.Ordinal));
        }
        result.Groups.All(OrdinalSortedByFilename).Should().BeTrue(
            "occurrences are ordered so the ordinal-first filename keeps");
        result.Groups.Select(g => g.Key).Should().OnlyHaveUniqueItems(
            "one class per content key");
        result.Groups.Select(g => g.Key).Should().BeInAscendingOrder(
            "classes are reported in key order");

        result.RedundantFiles.Should().OnlyHaveUniqueItems(
            "a file is listed at most once");
        result.RedundantFiles.Should().BeInAscendingOrder(StringComparer.Ordinal,
            "the redundant-file list is ordinal-sorted");
        var keeperFiles = result.Groups.Select(g => g.Keeper.Filename).ToHashSet(StringComparer.Ordinal);
        result.RedundantFiles.Any(keeperFiles.Contains).Should().BeFalse(
            "a file that keeps any problem is never wholly redundant");
    }

    [Fact]
    public void DuplicateProblems_OverDuplicatedFixture_ReportsTheOrdinalLaterCopyRedundant()
    {
        if (!File.Exists(TestPaths.AchimMuellerSeqXg)) return;

        string tempDir = Path.Combine(
            Path.GetTempPath(), "XgAnalytics.Tests_" + Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        try
        {
            // Two byte-identical copies under different names: every problem in
            // the match now has exactly one duplicate, and the ordinal-later
            // name keeps nothing. This is the discrimination check — an
            // always-empty result would satisfy Layer 1 but fails here.
            File.Copy(TestPaths.AchimMuellerSeqXg, Path.Combine(tempDir, "a.xg"));
            File.Copy(TestPaths.AchimMuellerSeqXg, Path.Combine(tempDir, "b.xg"));

            var result = Analyses.ComputeDuplicateProblems(tempDir, output.WriteLine);

            result.FileCount.Should().Be(2, "both copies carry decisions");
            result.NoKeyCount.Should().Be(0,
                "every decision in this pinned fixture derives a key — the exact "
                + "redundant-file claim below assumes the fail-open rung never fires");
            result.Groups.Should().NotBeEmpty("identical copies duplicate every problem");
            result.Groups.Should().OnlyContain(g => g.Keeper.Filename == "a.xg",
                "the ordinal-first filename keeps");
            result.RedundantFiles.Should().Equal(["b.xg"],
                "the second copy contributes no problem the first does not");
            result.DistinctProblemCount.Should().Be(result.ProblemCount / 2,
                "each problem appears exactly twice");
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { /* best-effort cleanup */ }
        }
    }

    [Fact]
    public void GroupDuplicateProblems_MoneyRecordWithoutJacoby_FailsOpen_AndIsNeverRedundant()
    {
        // Control — the pair is otherwise identical, so with the Jacoby fact
        // present it derives one key, collapses, and "b.xgp" is redundant.
        // Without this, the fail-open assertions below could pass for the wrong
        // reason (records that simply never grouped).
        var stamped = Analyses.GroupDuplicateProblems(
            [MoneyCubeRecord("a.xgp", isJacoby: true), MoneyCubeRecord("b.xgp", isJacoby: true)]);

        stamped.NoKeyCount.Should().Be(0, "a stamped money record derives a key");
        stamped.Groups.Should().ContainSingle("the two records are the same problem");
        stamped.DistinctProblemCount.Should().Be(1);
        stamped.RedundantFiles.Should().Equal(["b.xgp"], "the ordinal-first filename keeps");

        // Fail open — the same pair with the money grammar's Jacoby fact absent
        // has no derivable key. Underivability is not equality: neither copy
        // forms a class, and neither is ever reported redundant.
        var unstamped = Analyses.GroupDuplicateProblems(
            [MoneyCubeRecord("a.xgp", isJacoby: null), MoneyCubeRecord("b.xgp", isJacoby: null)]);

        unstamped.NoKeyCount.Should().Be(2, "a money record without Jacoby is the no-key rung");
        unstamped.Groups.Should().BeEmpty("no-key items never form a class");
        unstamped.RedundantFiles.Should().BeEmpty(
            "an item with no derivable key is never reported redundant");
        unstamped.DistinctProblemCount.Should().Be(2,
            "each no-key item counts as its own distinct problem");
        unstamped.RedundantProblemCount.Should().Be(0);
    }

    /// <summary>
    /// A money cube decision from the standard opening position, differing only
    /// in whether the Jacoby fact is stamped. Centered cube, because that is
    /// exactly where Jacoby is answer-changing (halheinrich/backgammon#120).
    /// </summary>
    private static BgDecisionData MoneyCubeRecord(string filename, bool? isJacoby) => new()
    {
        Id = new XgpDecisionId(filename),
        Position = new PositionData
        {
            Mop = StandardStartBoard,
            OnRollNeeds = 0,          // 0/0 away = money
            OpponentNeeds = 0,
            CubeSize = 1,
            CubeOwner = CubeOwner.Centered,
            IsJacoby = isJacoby,
        },
        Decision = new DecisionData { IsCube = true },
    };

    /// <summary>
    /// On-roll-relative standard opening position: index 0 is the opponent's
    /// bar, 1–24 the points, 25 the on-roll bar. Fifteen checkers a side.
    /// </summary>
    private static int[] StandardStartBoard =>
        [0, -2, 0, 0, 0, 0, 5, 0, 3, 0, 0, 0, -5, 5, 0, 0, 0, -3, 0, -5, 0, 0, 0, 0, 2, 0];
}
