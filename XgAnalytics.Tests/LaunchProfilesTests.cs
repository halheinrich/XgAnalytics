using System.Text.Json;
using AwesomeAssertions;

namespace XgAnalytics.Tests;

/// <summary>
/// Pins that the launch profiles Visual Studio offers agree with
/// <see cref="Reports.All"/>: <c>Usage</c> first, with no arguments, then one
/// profile per report, named after it and passing its name. A report added
/// without its profile, or a profile left after its report is gone, fails here.
/// </summary>
/// <remarks>
/// Reads <c>XgAnalytics/Properties/launchSettings.json</c> itself, which the
/// test project copies to its output as <c>XgAnalytics.launchSettings.json</c>.
/// </remarks>
public class LaunchProfilesTests
{
    private const string UsageProfile = "Usage";

    private static IReadOnlyList<(string Name, string? Args)> ReadProfiles()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "XgAnalytics.launchSettings.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        return doc.RootElement.GetProperty("profiles").EnumerateObject()
            .Select(p => (p.Name, p.Value.TryGetProperty("commandLineArgs", out var a) ? a.GetString() : null))
            .ToList();
    }

    [Fact]
    public void LaunchProfiles_StartOnUsage_ThenOneProfilePerReport()
    {
        var profiles = ReadProfiles();

        profiles.Should().NotBeEmpty();
        profiles[0].Name.Should().Be(UsageProfile);
        profiles[0].Args.Should().BeNullOrEmpty("the Usage profile must start no report");

        foreach (var report in Reports.All)
            profiles.Where(p => p.Name == report.Name).Should().ContainSingle(
                    "report {0} needs exactly one launch profile", report.Name)
                .Which.Args.Should().Be(report.Name);

        profiles.Skip(1).Select(p => p.Name).Should().BeSubsetOf(
            Reports.All.Select(r => r.Name), "every profile after Usage must name a report");
    }
}
