using ConvertXgToJson_Lib;
using FluentAssertions;
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
}
