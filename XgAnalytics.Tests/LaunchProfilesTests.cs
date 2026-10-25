using System.Text.Json;
using AwesomeAssertions;

namespace XgAnalytics.Tests;

/// <summary>
/// Pins that the launch profiles Visual Studio offers agree with
/// <see cref="Reports.All"/>: <c>Usage</c> first, with no arguments; exactly
/// one profile under each report's name, whose first argument is that report;
/// and every other profile, under any name, starting a real report. A report
/// added without its profile, a profile left after its report is gone, or a
/// profile naming a report that does not exist fails here.
/// </summary>
/// <remarks>
/// <para>
/// Any profile may carry a folder after the report name, so Hal can rescan
/// another folder from Visual Studio, on the report's own profile or on an
/// extra one. Only the first whitespace-separated token of the arguments is
/// checked, as the report name <see cref="ReportCommand"/> receives first.
/// The rest is the optional folder; a quoted folder with spaces is one
/// argument to the program and lies wholly after that token, so it is not
/// checked either.
/// </para>
/// <para>
/// Reads <c>XgAnalytics/Properties/launchSettings.json</c> itself, which the
/// test project copies to its output as <c>XgAnalytics.launchSettings.json</c>.
/// </para>
/// </remarks>
public class LaunchProfilesTests
{
    private const string UsageProfile = "Usage";

    private static IReadOnlyList<(string Name, string Args)> ReadProfiles()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "XgAnalytics.launchSettings.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        return doc.RootElement.GetProperty("profiles").EnumerateObject()
            .Select(p => (p.Name, p.Value.TryGetProperty("commandLineArgs", out var a) ? a.GetString() ?? "" : ""))
            .ToList();
    }

    private static string FirstToken(string args) =>
        args.Split((char[]?)null, 2, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";

    [Fact]
    public void LaunchProfiles_StartOnUsage_KeepOneProfilePerReport_AndNameOnlyRealReports()
    {
        var profiles = ReadProfiles();
        var reportNames = Reports.All.Select(r => r.Name).ToList();

        profiles.Should().NotBeEmpty();
        profiles[0].Name.Should().Be(UsageProfile);
        profiles[0].Args.Should().BeEmpty("the Usage profile must start no report");

        foreach (var report in Reports.All)
            FirstToken(profiles.Where(p => p.Name == report.Name).Should().ContainSingle(
                    "report {0} needs exactly one launch profile under its name", report.Name)
                .Which.Args).Should().Be(report.Name,
                    "the profile named {0} must start that report", report.Name);

        foreach (var profile in profiles.Skip(1))
            reportNames.Should().Contain(FirstToken(profile.Args),
                "profile '{0}' must start a report in Reports.All", profile.Name);
    }
}
