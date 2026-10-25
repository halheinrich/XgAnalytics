using XgAnalytics;

// How to run one job
//
// Arguments: <job> [folder]   (the usage text calls them <analysis> [dir])
//   <job>     one of: PlayerMatchCount, NonStandardStarts,
//             MatchScoreDistribution, DuplicateProblems
//   [folder]  optional; the folder to scan. When omitted, the job scans its
//             default folder (Reports.XgDir for the first three,
//             Reports.PositionsDir for DuplicateProblems). Quote a folder
//             that contains spaces, with no backslash before the closing
//             quote ("D:\a b\c", not "D:\a b\c\").
//   No job, an unknown job or extra arguments print this list and exit 2;
//   a folder that does not exist says so and exits 1.
//
// From Visual Studio
// 1. In Solution Explorer, right-click the XgAnalytics project and choose
//    "Set as Startup Project" (once; Visual Studio remembers it).
// 2. Set the configuration dropdown on the toolbar to Release (faster for a
//    full-corpus run).
// 3. On the toolbar, the green Start button is labelled with the current job
//    (it starts on "Usage", then remembers your last pick). Click the small
//    arrow beside the label and choose a job. Each job entry runs on its
//    default folder unless a folder follows the job name.
// 4. Press Ctrl+F5 (Debug > Start Without Debugging). A console window shows
//    the job's progress; its last line names the CSV it wrote. Rerunning a
//    job overwrites its CSV.
//
// To scan a different folder from Visual Studio, open Debug > XgAnalytics
// Debug Properties, select the job's profile (or create a new profile), and
// put the folder after the job name in "Command line arguments", e.g.:
//   DuplicateProblems "D:\Users\Hal\Documents\eXtremeGammon\BatchAnalyze\Positions\Move2\4a4a"
// That is saved in Properties/launchSettings.json. LaunchProfilesTests
// accepts it, as long as every profile's first argument names a real job and
// each job keeps exactly one profile under its own name.
//
// From a terminal
//   dotnet run --project XgAnalytics -c Release -- <job> [folder]
//   dotnet run --project XgAnalytics -c Release -- DuplicateProblems "D:\path\to\folder"
//   dotnet run --project XgAnalytics --no-launch-profile     (prints the list, exits 2)
//
// "Usage" runs nothing; it only lists the jobs and their default folders.
// To add a job: add its method to Analyses, a line to Reports.All, and a
// profile under its name to Properties/launchSettings.json
// (LaunchProfilesTests checks the last two agree).
return ReportCommand.Run(args, Console.Error, Directory.Exists, (report, dir) => report.Run(dir, Console.WriteLine));
