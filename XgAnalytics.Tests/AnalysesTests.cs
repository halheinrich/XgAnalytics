using Xunit.Abstractions;

namespace XgAnalytics.Tests;

public class AnalysesTests(ITestOutputHelper output)
{
    private const string XgDir = @"D:\Users\Hal\Documents\eXtremeGammon\BatchAnalyze\Matches\hhDb\Xg";
    //private const string XgDir = @"D:\Users\Hal\Documents\Visual Studio 2026\Projects\backgammon\TestData\xg";

    [Fact]
    public void PlayerMatchCount() =>
        Analyses.PlayerMatchCount(XgDir, output.WriteLine);

    [Fact]
    public void NonStandardStarts() =>
        Analyses.NonStandardStarts(XgDir, output.WriteLine);
    [Fact]
    public void MatchScoreDistribution() =>
    Analyses.MatchScoreDistribution(XgDir, output.WriteLine);
}