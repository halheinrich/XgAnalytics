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

    /// <summary>
    /// The directory enumeration helper used by all three analyses must yield
    /// both <c>.xg</c> match files and <c>.xgp</c> position files. The prior
    /// <c>*.xg</c>-only filter silently skipped <c>.xgp</c>.
    ///
    /// <para>Self-contained: empty fixture files in a fresh temp dir — the
    /// helper enumerates names only, so file contents don't matter.</para>
    /// </summary>
    [Fact]
    public void EnumerateXgFormatFiles_IncludesBothXgAndXgpFiles()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "XgAnalytics.Tests_" + Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "match.xg"), "");
            File.WriteAllText(Path.Combine(tempDir, "position.xgp"), "");

            var names = Analyses.EnumerateXgFormatFiles(tempDir)
                .Select(Path.GetFileName)
                .ToList();

            Assert.Contains("match.xg", names);
            Assert.Contains("position.xgp", names);
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { /* best-effort cleanup */ }
        }
    }
}