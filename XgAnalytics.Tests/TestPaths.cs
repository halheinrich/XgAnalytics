namespace XgAnalytics.Tests;

/// <summary>
/// Paths into the shared <c>backgammon/TestData/</c> tree, resolved relative to
/// the test assembly. Mirrors <c>ConvertXgToJson_Lib.Tests.TestPaths</c> — the
/// XgAnalytics.Tests output directory sits at the same depth, so the same
/// five-<c>..</c> walk lands on the umbrella <c>TestData</c>.
/// </summary>
internal static class TestPaths
{
    private static readonly string _root =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\TestData"));

    /// <summary>
    /// Fixture-agnostic corpus: iterate whatever <c>.xg</c>/<c>.xgp</c> files
    /// exist and assert shape invariants. Contents churn and may be empty on a
    /// fresh checkout — never pin a filename or count here.
    /// </summary>
    public static string XgDir => Path.Combine(_root, "xg");

    /// <summary>
    /// Append-only home for fixtures pinned by an exact filename. A file added
    /// here is never removed, so a test may safely name it. Contents are
    /// gitignored, so presence must still be guarded.
    /// </summary>
    public static string FixtureFilesDir => Path.Combine(_root, "FixtureFiles");

    /// <summary>
    /// Tournament match whose filename independently encodes its participants
    /// ("Achim Mueller", "Mario Sequeira") and match length (23pt) — pins the
    /// Layer-2 discrimination test for the analyses. Append-only fixture.
    /// </summary>
    public static string AchimMuellerSeqXg =>
        Path.Combine(FixtureFilesDir,
            "Achim Mueller (8.36) - Mario Sequeira (7.13) 23pt Monte Carlo WC 2008 SF.xg");
}
