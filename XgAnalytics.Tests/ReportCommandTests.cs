using AwesomeAssertions;

namespace XgAnalytics.Tests;

/// <summary>
/// Pins the command entry point's dispatch through <see cref="ReportCommand.Run"/>,
/// the path <c>Program</c> takes, with the directory check and the run passed in:
/// nothing here touches the disk, runs an analysis or writes a CSV.
/// </summary>
public class ReportCommandTests
{
    private const string OtherDir = @"X:\Elsewhere\Positions";

    private sealed class Recorder
    {
        public StringWriter Error { get; } = new();
        public List<(Report Report, string Dir)> Runs { get; } = [];

        public int Run(Func<string, bool> directoryExists, params string[] args) =>
            ReportCommand.Run(args, Error, directoryExists, (report, dir) => Runs.Add((report, dir)));

        public int Run(params string[] args) => Run(_ => true, args);
    }

    private static Action<string, Action<string>> AnalysisNamed(string name) => name switch
    {
        "PlayerMatchCount" => Analyses.PlayerMatchCount,
        "NonStandardStarts" => Analyses.NonStandardStarts,
        "MatchScoreDistribution" => Analyses.MatchScoreDistribution,
        "DuplicateProblems" => Analyses.DuplicateProblems,
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, null),
    };

    [Theory]
    [InlineData("PlayerMatchCount")]
    [InlineData("NonStandardStarts")]
    [InlineData("MatchScoreDistribution")]
    [InlineData("DuplicateProblems")]
    public void EachName_RunsItsOwnAnalysis(string name)
    {
        var recorder = new Recorder();

        recorder.Run(name).Should().Be(ReportCommand.Success);

        recorder.Runs.Should().ContainSingle()
            .Which.Report.Run.Should().Be(AnalysisNamed(name));
        recorder.Error.ToString().Should().BeEmpty();
    }

    [Fact]
    public void NoName_PrintsUsage_AndRunsNothing()
    {
        var recorder = new Recorder();

        recorder.Run().Should().Be(ReportCommand.UsageError);

        ShouldHavePrintedUsage(recorder);
    }

    [Fact]
    public void UnknownName_PrintsUsage_AndRunsNothing()
    {
        var recorder = new Recorder();

        recorder.Run("PlayerMatchCounts").Should().Be(ReportCommand.UsageError);

        ShouldHavePrintedUsage(recorder);
    }

    [Fact]
    public void TooManyArguments_PrintsUsage_AndRunsNothing()
    {
        var recorder = new Recorder();

        recorder.Run("PlayerMatchCount", OtherDir, "extra").Should().Be(ReportCommand.UsageError);

        ShouldHavePrintedUsage(recorder);
    }

    [Fact]
    public void AbsentInputDirectory_SaysSo_AndRunsNothing()
    {
        var recorder = new Recorder();

        recorder.Run(_ => false, "PlayerMatchCount", OtherDir)
            .Should().Be(ReportCommand.DirectoryNotFound);

        recorder.Error.ToString().Should().Contain("not found").And.Contain(OtherDir);
        recorder.Runs.Should().BeEmpty();
    }

    [Theory]
    [InlineData("PlayerMatchCount", Reports.XgDir)]
    [InlineData("NonStandardStarts", Reports.XgDir)]
    [InlineData("MatchScoreDistribution", Reports.XgDir)]
    [InlineData("DuplicateProblems", Reports.PositionsDir)]
    public void NoDirectory_ScansTheReportsDefault(string name, string expectedDir)
    {
        var recorder = new Recorder();
        var checkedDirs = new List<string>();

        recorder.Run(d => { checkedDirs.Add(d); return true; }, name).Should().Be(ReportCommand.Success);

        checkedDirs.Should().Equal(expectedDir);
        recorder.Runs.Should().ContainSingle().Which.Dir.Should().Be(expectedDir);
    }

    [Fact]
    public void GivenDirectory_OverridesTheDefault()
    {
        var recorder = new Recorder();

        recorder.Run(d => d == OtherDir, "DuplicateProblems", OtherDir).Should().Be(ReportCommand.Success);

        recorder.Runs.Should().ContainSingle().Which.Dir.Should().Be(OtherDir);
    }

    private static void ShouldHavePrintedUsage(Recorder recorder)
    {
        var usage = recorder.Error.ToString();
        usage.Should().Contain("Usage:");
        foreach (var report in Reports.All)
            usage.Should().Contain(report.Name);
        recorder.Runs.Should().BeEmpty();
    }
}
