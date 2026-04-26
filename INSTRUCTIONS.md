# XgAnalytics — Subproject Instructions

> See [`../CLAUDE.md`](../CLAUDE.md) for session conventions.
> See [`../INSTRUCTIONS.md`](../INSTRUCTIONS.md) for cross-cutting status and the dependency graph.
> See [`../VISION.md`](../VISION.md) for mission and principles.

## Stack

C# / .NET 10 class library + xUnit test project. Visual Studio 2026, Windows.

## Solution

`D:\Users\Hal\Documents\Visual Studio 2026\Projects\backgammon\XgAnalytics\XgAnalytics.slnx`

## Repo

https://github.com/halheinrich/XgAnalytics — branch `main`.

## Depends on

- **ConvertXgToJson_Lib** — `XgFileReader.ReadMatchInfo` / `ReadGameHeaders`, `XgMatchInfo`, `XgIteratorState`. The fast-path metadata readers are the sole parsing surface the analyses use.
- **XgFilter_Lib** — project-referenced but not yet consumed by any analysis. Left in place for future filter-driven analyses.

## Directory tree

```
XgAnalytics/
├── XgAnalytics.slnx
├── INSTRUCTIONS.md
├── XgAnalytics/
│   ├── XgAnalytics.csproj          class library
│   └── Analyses.cs                 static Analyses — all analyses live here
└── XgAnalytics.Tests/
    ├── XgAnalytics.Tests.csproj    xUnit
    └── AnalysesTests.cs            one [Fact] per analysis
```

## Architecture

**Class library, not a console app.** There is no `Program.cs` and no `Main`. The entry points are xUnit `[Fact]` methods in `XgAnalytics.Tests`, which call static methods on `Analyses` and pass `ITestOutputHelper.WriteLine` as the log sink. Running an analysis means running its test. This is intentional — it gives progress output in the Test Explorer, lets Visual Studio be the runner, and avoids a separate console host.

**Analysis method shape.** Every analysis is a `public static void` on `Analyses` with the signature `(string xgDir, Action<string> log)`. The `log` callback is written to as the analysis streams; structured results are written to a CSV at the end.

**File iteration.** Each analysis loops the private `EnumerateXgFormatFiles` helper, which yields `*.xg` match files concatenated with `*.xgp` position files (both formats — `XgFileReader` handles both). Each file is parsed via `XgFileReader.ReadMatchInfo` (for match-level analyses) or `XgFileReader.ReadGameHeaders` with a shared `XgIteratorState` (for game-level analyses), and `try { ... } catch { continue; }` skips unreadable files. The exceptions are silently swallowed. The helper mirrors the same-named private helper in `ConvertXgToJson_Lib.XgDecisionIterator` — cross-subproject duplication accepted until a third consumer warrants promoting it to a shared public utility on `XgFileReader`.

**Progress reporting.** Each analysis prints a status line after match counts 1, 2, 4, 8, 16, … via an exponential-backoff `nextReport` counter. Rate (`matches/sec`) is computed from a `Stopwatch`.

**CSV output.** Each analysis writes its result CSV to a hard-coded path under `D:\Users\Hal\Documents\Excel\Backgammon\`. No prompt, no overwrite guard — running twice overwrites.

**Score normalization (MatchScoreDistribution).** Keys are `(MatchLength, Away1, Away2, IsCrawford)` with `Away1 <= Away2` — the pair is swapped so both player perspectives collapse onto one bucket.

## Public API

```csharp
public static class Analyses
{
    public static void PlayerMatchCount       (string xgDir, Action<string> log);
    public static void NonStandardStarts      (string xgDir, Action<string> log);
    public static void MatchScoreDistribution (string xgDir, Action<string> log);
}
```

All three methods log progress incrementally via the `log` callback and write a CSV at a hard-coded path when done. None return a value; observable output is the `log` stream plus the CSV file.

## Pitfalls

- **Hard-coded CSV output paths** under `D:\Users\Hal\Documents\Excel\Backgammon\`. The directory must already exist — no `Directory.CreateDirectory` call. Won't run on a non-Hal machine without editing the source.
- **Hard-coded test input path** in `AnalysesTests.cs` (`...\hhDb\Xg`). A commented-out line points at `TestData/xg`. Any test run on a fresh machine enumerates an empty directory or throws.
- **Silent parse-failure swallow.** `catch { continue; }` hides corrupted-file exceptions entirely — neither logged nor counted. A batch can appear to "complete" while skipping a meaningful fraction of input.
- **CSV overwrite with no guard.** Re-running an analysis clobbers its previous CSV without warning.
- **XgFilter_Lib reference is load-bearing-looking but unused.** Don't assume analyses filter anything today — they iterate every `.xg` in `xgDir`.

## Subproject-internal next steps

None pending.
