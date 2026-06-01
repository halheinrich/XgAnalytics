# XgAnalytics — Subproject Instructions

> Collaboration contract: [`../AGENTS.md`](../AGENTS.md)
> Umbrella status & dependency graph: [`../INSTRUCTIONS.md`](../INSTRUCTIONS.md)
> Mission & principles: [`../VISION.md`](../VISION.md)

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
    └── AnalysesTests.cs            one [Fact] per analysis plus a self-contained helper test for EnumerateXgFormatFiles
```

## Architecture

**Class library, not a console app.** There is no `Program.cs` and no `Main`. The entry points are xUnit `[Fact]` methods in `XgAnalytics.Tests`, which call static methods on `Analyses` and pass `ITestOutputHelper.WriteLine` as the log sink. Running an analysis means running its test. This is intentional — it gives progress output in the Test Explorer, lets Visual Studio be the runner, and avoids a separate console host.

**Analysis method shape.** Every analysis is a `public static void` on `Analyses` with the signature `(string xgDir, Action<string> log)`. Each analysis writes progress to the `log` callback as it streams; structured results land in a CSV at the end.

**File iteration.** Each analysis loops the `internal` `EnumerateXgFormatFiles` helper (visible to `XgAnalytics.Tests` via `InternalsVisibleTo`, declared in the csproj), which yields `*.xg` match files concatenated with `*.xgp` position files. Each file is parsed via `XgFileReader.ReadMatchInfo` (for match-level analyses) or `XgFileReader.ReadGameHeaders` with a shared `XgIteratorState` (for game-level analyses), and `try { ... } catch { continue; }` silently skips unreadable files (see Pitfalls). The helper mirrors the same-named private helper in `ConvertXgToJson_Lib.XgDecisionIterator` — cross-subproject duplication accepted until a third consumer warrants promoting it to a shared public utility on `XgFileReader`.

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
- **Unfiltered iteration.** Despite the `XgFilter_Lib` project reference, no analysis filters — every `.xg`/`.xgp` in `xgDir` is processed.

## Subproject-internal next steps

None pending.
