using ConvertXgToJson_Lib;
using ConvertXgToJson_Lib.Models;
using System.Diagnostics;

namespace XgAnalytics;

internal class Program
{
    static void Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Usage: XgAnalytics <path-to-directory-of-.xg-files>");
            return;
        }

        string dir = args[0];

        if (!Directory.Exists(dir))
        {
            Console.WriteLine($"Directory not found: {dir}");
            return;
        }

        PlayerMatchCount(dir);
    }

    // -------------------------------------------------------------------------
    //  Analysis: Player match count
    // -------------------------------------------------------------------------

    static void PlayerMatchCount(string xgDir)
    {
        var playerMatches = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var sw = Stopwatch.StartNew();

        int matchCount = 0;
        int nextReport = 1;

        foreach (var path in Directory.EnumerateFiles(xgDir, "*.xg"))
        {
            XgMatchInfo info;
            try { info = XgFileReader.ReadMatchInfo(path); }
            catch { continue; }

            string matchId = Path.GetFileNameWithoutExtension(path);

            RegisterPlayer(info.Player1, matchId, playerMatches);
            RegisterPlayer(info.Player2, matchId, playerMatches);

            matchCount++;

            if (matchCount >= nextReport)
            {
                double secs = sw.Elapsed.TotalSeconds;
                double rate = secs > 0 ? matchCount / secs : 0;
                Console.WriteLine($"  {matchCount,6} matches  {secs,7:F1}s  {rate:F2} matches/sec");
                while (nextReport <= matchCount) nextReport *= 2;
            }
        }

        sw.Stop();
        double totalSecs = sw.Elapsed.TotalSeconds;
        double finalRate = totalSecs > 0 ? matchCount / totalSecs : 0;

        var sorted = playerMatches
            .OrderByDescending(kv => kv.Value.Count)
            .ThenBy(kv => kv.Key)
            .ToList();

        // Console output
        Console.WriteLine();
        Console.WriteLine($"{"Player",-30} {"Matches",7}");
        Console.WriteLine(new string('-', 39));
        foreach (var (player, matches) in sorted)
            Console.WriteLine($"{player,-30} {matches.Count,7}");
        Console.WriteLine(new string('-', 39));
        Console.WriteLine($"{"Total players:",-30} {sorted.Count,7}");
        Console.WriteLine();
        Console.WriteLine($"Total matches : {matchCount}");
        Console.WriteLine($"Total time    : {totalSecs:F1}s");
        Console.WriteLine($"Avg rate      : {finalRate:F2} matches/sec");

        // CSV output
        string csvPath = @"D:\Users\Hal\Documents\Excel\Backgammon\PlayerMatchCount.csv";
        using var writer = new StreamWriter(csvPath);
        writer.WriteLine("Player,Matches");
        foreach (var (player, matches) in sorted)
            writer.WriteLine($"{CsvEscape(player)},{matches.Count}");

        Console.WriteLine($"\nCSV written to: {csvPath}");
    }
    // -------------------------------------------------------------------------
    //  Helpers
    // -------------------------------------------------------------------------

    static void RegisterPlayer(string player, string match,
        Dictionary<string, HashSet<string>> playerMatches)
    {
        if (string.IsNullOrWhiteSpace(player)) return;
        if (!playerMatches.TryGetValue(player, out var matches))
        {
            matches = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            playerMatches[player] = matches;
        }
        matches.Add(match);
    }

    static string CsvEscape(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}