using XgAnalytics;

// Runs one report: `XgAnalytics <analysis> [dir]`. From Visual Studio, pick the
// analysis in the start-profile dropdown (Properties/launchSettings.json).
return ReportCommand.Run(args, Console.Error, Directory.Exists, (report, dir) => report.Run(dir, Console.WriteLine));
