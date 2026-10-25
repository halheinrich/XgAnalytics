namespace XgAnalytics;

/// <summary>
/// The command entry point's dispatch: <c>&lt;analysis&gt; [dir]</c> picks one
/// of <see cref="Reports.All"/> by name and runs it over <c>dir</c>, or over the
/// report's default directory when none is given.
/// </summary>
/// <remarks>
/// The directory check and the run itself are passed in, so a test can drive
/// this production path end to end without touching the disk or running an
/// analysis (which would overwrite Hal's CSVs).
/// </remarks>
internal static class ReportCommand
{
    /// <summary>The report ran.</summary>
    public const int Success = 0;

    /// <summary>The input directory does not exist.</summary>
    public const int DirectoryNotFound = 1;

    /// <summary>The arguments name no report, or there are too many of them.</summary>
    public const int UsageError = 2;

    /// <summary>Parses <paramref name="args"/>, checks the input directory, and runs the named report.</summary>
    /// <param name="args">The command-line arguments: a report name, optionally followed by an input directory.</param>
    /// <param name="error">Where usage and the missing-directory message go.</param>
    /// <param name="directoryExists">The input-directory check; <see cref="Directory.Exists(string?)"/> in production.</param>
    /// <param name="run">Runs the chosen report over the chosen directory.</param>
    /// <returns>The process exit code: <see cref="Success"/>, <see cref="DirectoryNotFound"/> or <see cref="UsageError"/>.</returns>
    public static int Run(
        IReadOnlyList<string> args,
        TextWriter error,
        Func<string, bool> directoryExists,
        Action<Report, string> run)
    {
        var report = args.Count is 1 or 2
            ? Reports.All.FirstOrDefault(r => r.Name == args[0])
            : null;
        if (report is null)
        {
            WriteUsage(error);
            return UsageError;
        }

        var dir = args.Count == 2 ? args[1] : report.DefaultDirectory;
        if (!directoryExists(dir))
        {
            error.WriteLine($"Input directory not found: {dir}");
            return DirectoryNotFound;
        }

        run(report, dir);
        return Success;
    }

    private static void WriteUsage(TextWriter error)
    {
        error.WriteLine("Usage: XgAnalytics <analysis> [dir]");
        error.WriteLine();
        error.WriteLine("Analyses, with the directory each scans when [dir] is omitted:");
        foreach (var r in Reports.All)
            error.WriteLine($"  {r.Name,-24}{r.DefaultDirectory}");
    }
}
