using BgDataTypes_Lib;
using BgDataTypes_Lib.TestSupport;
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

        (result.Counts.Values.Sum() + result.MoneyGameCount).Should().Be(result.GameCount,
            "every scanned game is a money game or lands in exactly one match bucket");
        // Vacuous-safe per-element checks (see PlayerMatchCount above).
        result.Counts.Keys.All(k => k.Away1 <= k.Away2).Should().BeTrue(
            "score keys are normalized so the lower away score is first");
        result.Counts.Keys.All(k => k.MatchLength >= 1 && k.Away1 >= 1).Should().BeTrue(
            "a bucket is a match's score — money is read by its kind, never as a zero score");
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
            dist.MoneyGameCount.Should().Be(0, "a match has no money game");
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { /* best-effort cleanup */ }
        }
    }

    [Fact]
    public void MatchScoreDistribution_OverMoneyFixture_CountsEveryGameAsMoney()
    {
        if (!File.Exists(TestPaths.MoneyTestXg)) return;

        string tempDir = Path.Combine(
            Path.GetTempPath(), "XgAnalytics.Tests_" + Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        try
        {
            File.Copy(TestPaths.MoneyTestXg, Path.Combine(tempDir, Path.GetFileName(TestPaths.MoneyTestXg)));

            // The discrimination check for reading money by kind: a money
            // session's games have no match score, so none may reach a match
            // bucket — reading a zero length as money would put them all in one.
            var dist = Analyses.ComputeMatchScoreDistribution(tempDir, output.WriteLine);

            dist.MatchCount.Should().Be(1, "exactly one file was scanned");
            dist.GameCount.Should().BeGreaterThan(0, "a real money session contains games");
            dist.MoneyGameCount.Should().Be(dist.GameCount, "every game of a money session is a money game");
            dist.Counts.Should().BeEmpty("a money game has no match score to bucket");
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { /* best-effort cleanup */ }
        }
    }

    // -------------------------------------------------------------------------
    //  DuplicateProblems (halheinrich/backgammon#117)
    //
    //  Layer 1 corpus invariants + Layer 2 discrimination, as above, plus
    //  pins of the pure grouping core over records built with the producer's
    //  TestRecords. Those are deliberately not sourced from `TestData/xg`: the
    //  corpus is fixture-agnostic and may be empty, and the shapes they pin —
    //  one problem under two session rules, a multi-decision file holding both
    //  a redundant copy and a problem found nowhere else — are stated, not
    //  hoped for.
    // -------------------------------------------------------------------------

    [Fact]
    public void DuplicateProblems_OverCorpus_HoldsShapeInvariants()
    {
        if (!CorpusPresent) return;

        int fileCount = XgFileReader.EnumerateXgFormatFiles(TestPaths.XgDir).Count();
        var result = Analyses.ComputeDuplicateProblems(TestPaths.XgDir, output.WriteLine);

        result.FileCount.Should().BeLessThanOrEqualTo(fileCount,
            "only enumerated files can contribute decisions");
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
    public void GroupDuplicateProblems_GroupsByProblemKey_SoOneBoardUnderTwoJacobyRulesIsTwoProblems()
    {
        // Control — the pair is identical, so it derives one key, collapses,
        // and "b.xgp" is redundant. Without this, the split below could pass
        // for the wrong reason (records that simply never grouped).
        var same = Analyses.GroupDuplicateProblems(
            [MoneyCube(new XgpDecisionId("a.xgp"), isJacoby: true),
             MoneyCube(new XgpDecisionId("b.xgp"), isJacoby: true)]);

        same.Groups.Should().ContainSingle("the two records are the same problem");
        same.DistinctProblemCount.Should().Be(1);
        same.RedundantFiles.Should().Equal(["b.xgp"], "the ordinal-first filename keeps");

        // The same board and cube under the other Jacoby rule is another
        // problem: the rule is part of a money key, so the grouping — which
        // is the key's, with no rule of its own — keeps the two apart.
        var split = Analyses.GroupDuplicateProblems(
            [MoneyCube(new XgpDecisionId("a.xgp"), isJacoby: true),
             MoneyCube(new XgpDecisionId("b.xgp"), isJacoby: false)]);

        split.Groups.Should().BeEmpty("the Jacoby rule separates the two problems");
        split.DistinctProblemCount.Should().Be(2);
        split.RedundantFiles.Should().BeEmpty("each file holds a problem found nowhere else");
        split.RedundantProblemCount.Should().Be(0);
    }

    [Fact]
    public void GroupDuplicateProblems_MultiDecisionFile_IsRedundantOnlyWhenEveryProblemSurvivesElsewhere()
    {
        // Three match files share one cube problem; "b.xg" also holds a
        // checker play found nowhere else. Scanned in reverse ordinal order,
        // since the core assumes no input order.
        var shared = (string file) => MoneyCube(
            new XgDecisionId(file, Game: 1, MoveNumber: 1, IsCube: true), isJacoby: true);
        var onlyInB = TestRecords.CheckerPlay(
            id: new XgDecisionId("b.xg", Game: 1, MoveNumber: 2, IsCube: false));

        var result = Analyses.GroupDuplicateProblems(
            [shared("c.xg"), shared("b.xg"), onlyInB, shared("a.xg")]);

        result.ProblemCount.Should().Be(4);
        result.DistinctProblemCount.Should().Be(2, "one shared problem and one found only in b.xg");
        result.RedundantProblemCount.Should().Be(2, "the shared problem's copies in b.xg and c.xg");
        result.Groups.Should().ContainSingle("only the cube problem is duplicated")
            .Which.Occurrences.Select(id => id.Filename).Should().Equal(
                ["a.xg", "b.xg", "c.xg"], "occurrences run ordinal-first, so a.xg keeps");
        result.RedundantFiles.Should().Equal(["c.xg"],
            "b.xg's cube copy is redundant, but its checker play survives nowhere else");
    }

    /// <summary>
    /// A money cube decision at the standard start, the cube centred — where
    /// the Jacoby rule is answer-changing (halheinrich/backgammon#120) —
    /// differing only in its identifier and the session's Jacoby rule.
    /// </summary>
    private static CubeDecision MoneyCube(DecisionId id, bool isJacoby) => TestRecords.Cube(
        id: id,
        position: TestRecords.Position(session: TestRecords.MoneySession(isJacoby: isJacoby)));
}
