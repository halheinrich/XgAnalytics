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
│   ├── Analyses.cs                 static Analyses — all analyses live here
│   └── AnalysisResults.cs          immutable result records the Compute* methods return
└── XgAnalytics.Tests/
    ├── XgAnalytics.Tests.csproj    xUnit + FluentAssertions
    ├── TestPaths.cs                shared-TestData path helper (mirrors CXJ)
    └── AnalysesTests.cs            ad-hoc runner facts + corpus/fixture tests
```

## Architecture

**Class library, not a console app.** There is no `Program.cs` and no `Main`. The entry points are xUnit `[Fact]` methods in `XgAnalytics.Tests`, which call static methods on `Analyses` and pass `ITestOutputHelper.WriteLine` as the log sink. Running an analysis means running its test. This is intentional — it gives progress output in the Test Explorer, lets Visual Studio be the runner, and avoids a separate console host.

**Analysis method shape — compute / persist split.** Each analysis is two
methods on `Analyses`. `Compute*(string xgDir, Action<string> log)` does the
streaming scan, writes progress + summary to the `log` callback, and **returns**
the aggregated data as an immutable result record (`AnalysisResults.cs`) — no
file output. The matching `public static void` wrapper `(string xgDir,
Action<string> log)` calls the aggregator, then writes the result to a CSV. The
split keeps the machine-dependent file write out of the computation path so the
aggregation is testable without touching disk; the `log` callback is the only
side effect the aggregator has, and it is dependency-injected.

**File iteration.** Each analysis calls `XgFileReader.EnumerateXgFormatFiles` (the shared public helper in `ConvertXgToJson_Lib`), which yields `*.xg` match files concatenated with `*.xgp` position files. Each file is parsed via `XgFileReader.ReadMatchInfo` (for match-level analyses) or `XgFileReader.ReadGameHeaders` with a shared `XgIteratorState` (for game-level analyses), and `try { ... } catch { continue; }` silently skips unreadable files (see Pitfalls). Enumeration and its tests now single-source from the producer (`XgFileReaderDiscoveryTests`); the formerly-duplicated private helper here has been removed.

**Progress reporting.** Each analysis prints a status line after match counts 1, 2, 4, 8, 16, … via an exponential-backoff `nextReport` counter. Rate (`matches/sec`) is computed from a `Stopwatch`.

**CSV output.** Each analysis writes its result CSV to a hard-coded path under `D:\Users\Hal\Documents\Excel\Backgammon\`. No prompt, no overwrite guard — running twice overwrites.

**Score normalization (MatchScoreDistribution).** Keys are `(MatchLength, Away1, Away2, IsCrawford)` with `Away1 <= Away2` — the pair is swapped so both player perspectives collapse onto one bucket.

## Public API

**There is no public API.** This is a personal analysis tool driven by its own
test project, not a library: nothing in this assembly is `public`. There is no
exe either — the `[Fact]` methods in `XgAnalytics.Tests` are the runner (see
Architecture), so the test assembly is the *only* caller, and it reaches
everything below through the `InternalsVisibleTo` in `XgAnalytics.csproj`.

The internal testable seams are documented here because they are what a
maintainer tests and modifies against — not because anything outside may call
them. Should a real consumer ever appear, the answer is to design a public
surface deliberately (extracting an `XgAnalytics_Lib` if the analyses and the
CSV wrappers want separating), not to widen these back to `public` by default.

```csharp
internal static class Analyses
{
    // Aggregators — scan + log, return the data, no file output.
    public static PlayerMatchCountResult       ComputePlayerMatchCount       (string xgDir, Action<string> log);
    public static NonStandardStartsResult      ComputeNonStandardStarts      (string xgDir, Action<string> log);
    public static MatchScoreDistributionResult ComputeMatchScoreDistribution (string xgDir, Action<string> log);

    // Persistence wrappers — call the aggregator, then write a CSV.
    public static void PlayerMatchCount       (string xgDir, Action<string> log);
    public static void NonStandardStarts      (string xgDir, Action<string> log);
    public static void MatchScoreDistribution (string xgDir, Action<string> log);
}
```

The `Compute*` methods log progress incrementally via the `log` callback and
return their aggregated result (`PlayerMatchCountResult`,
`NonStandardStartsResult`, `MatchScoreDistributionResult` in
`AnalysisResults.cs` — immutable records exposing read-only views; the score
distribution is keyed by the normalized `MatchScoreKey`). Those records, and the
`PlayerMatchTally` / `NonStandardStart` elements they carry, are `internal` for
the same reason as `Analyses` — they are these methods' return types. The `void` wrappers add
the CSV write to a hard-coded path; their observable output is the `log` stream
plus the CSV file.

## Pitfalls

- **Hard-coded CSV output paths** under `D:\Users\Hal\Documents\Excel\Backgammon\`, baked into the `void` wrappers. The directory must already exist — no `Directory.CreateDirectory` call. The wrappers won't run on a non-Hal machine; the `Compute*` aggregators carry no such dependency.
- **Two test layers, both green-on-any-machine.** The three `[Fact]`s named after the analyses are the *ad-hoc runner* — they point at Hal's local `...\hhDb\Xg` and write CSVs, and self-skip (early return) when either that input dir or the CSV output dir is absent. The deterministic CI coverage is separate: corpus shape-invariant tests over the shared `TestData/xg` (guarded for an empty/absent corpus) plus a pinned-fixture discrimination test over `TestData/FixtureFiles`. Never make the corpus tests pin a filename or count — that corpus churns (see `../AGENTS.md` TestData convention).
- **Silent parse-failure swallow.** `catch { continue; }` hides corrupted-file exceptions entirely — neither logged nor counted. A batch can appear to "complete" while skipping a meaningful fraction of input.
- **CSV overwrite with no guard.** Re-running an analysis clobbers its previous CSV without warning.
- **Unfiltered iteration.** Despite the `XgFilter_Lib` project reference, no analysis filters — every `.xg`/`.xgp` in `xgDir` is processed.

## Subproject-internal next steps

None pending.
