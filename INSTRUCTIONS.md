# XgAnalytics — Subproject Instructions

> Collaboration contract: [`../AGENTS.md`](../AGENTS.md)
> Umbrella status & dependency graph: [`../INSTRUCTIONS.md`](../INSTRUCTIONS.md)
> Mission & principles: [`../VISION.md`](../VISION.md)

## Stack

C# / .NET 10 console app (one command per report) + xUnit test project. Visual Studio 2026, Windows.

## Solution

`D:\Users\Hal\Documents\Visual Studio 2026\Projects\backgammon\XgAnalytics\XgAnalytics.slnx`

## Repo

https://github.com/halheinrich/XgAnalytics — branch `main`.

## Depends on

- **ConvertXgToJson_Lib** — file discovery (`XgFileReader.EnumerateXgFormatFiles`) plus **two parsing surfaces, chosen per analysis**: the fast-path metadata readers (`XgFileReader.ReadMatchInfo` / `ReadGameHeaders`, `XgMatchInfo`, `XgIteratorState`) for analyses whose facts live in the file headers, and `XgFileReader.ReadFile` + `XgDecisionIterator.IterateDiagramRequests` for analyses that need per-decision content the headers cannot carry (`DuplicateProblems`). *Amended 2026-08-24 (halheinrich/backgammon#117): this section previously stated that the fast-path metadata readers were the sole parsing surface the analyses use. They are not — they are the surface the header-level analyses use, and the default for a new analysis that can be answered from headers.*
- **BgDataTypes_Lib** — transitive through ConvertXgToJson_Lib, consumed directly: `BgDecisionData`, `DecisionId`, and `ProblemKey`, the ecosystem's single content-identity derivation. `DuplicateProblems` groups on it and defines no dedup rule of its own. `MatchScoreDistribution` reads money versus match by the session's kind: the header types' `XgMatchInfo.Terms` and `XgGameInfo.Standing`, paired by `GameSession.Create` (see Architecture).
- **BgDataTypes_Lib.TestSupport** — test-only, referenced by `XgAnalytics.Tests`: the producer's record builders (`TestRecords`), the one way the tests build a decision record.
- **XgFilter_Lib** — project-referenced but not yet consumed by any analysis. Left in place for future filter-driven analyses.

## Layout

Two projects in one solution: the analyses with the command that runs them,
and their tests (see Architecture).

- **`XgAnalytics/`** — the analyses and their command entry point. Nothing in
  it is `public`. Three areas: the `Analyses` static class, which holds every
  analysis as a `Compute*` aggregator plus a `void` CSV-writing sibling; the
  immutable result records those aggregators return; and the entry point —
  `Program.cs`, the `ReportCommand` dispatch, the `Reports` table of names and
  default input directories, and one launch profile per report in
  `Properties/launchSettings.json`.
- **`XgAnalytics.Tests/`** — xUnit + AwesomeAssertions, reaching the library
  through `InternalsVisibleTo`: the fixture-agnostic corpus tests over the
  shared `TestData/xg`; the pinned-fixture discrimination tests over
  `TestData/FixtureFiles`; the pins of the pure grouping core over records
  built with `TestRecords`, of the result records' immutable collections
  (`AnalysisResultsTests`), and of the entry point's dispatch
  (`ReportCommandTests`). Also carries the shared-`TestData` path helper,
  mirroring ConvertXgToJson_Lib.Tests'.

## Architecture

**Run a report as a command, not a test.** `XgAnalytics` is an exe whose
`Program` runs one named report — `PlayerMatchCount`, `NonStandardStarts`,
`MatchScoreDistribution` or `DuplicateProblems`, the `void` wrappers' names —
over an optional input directory, logging progress to the console:

- **Visual Studio:** set `XgAnalytics` as the startup project, pick the report
  in the toolbar's start-profile dropdown (`Properties/launchSettings.json`),
  and press Ctrl+F5. Use the Release configuration for a full-corpus run. The
  first profile, `Usage`, passes no arguments, so a start with no report
  picked prints the list of reports and runs nothing; one profile per report
  follows it.
- **Command line:** `dotnet run --project XgAnalytics -c Release -- <analysis> [dir]`.
  The arguments after `--` replace the launch profile's; a bare `dotnet run`
  with none falls back to the first profile, `Usage`. Pass
  `--no-launch-profile` anyway when only the usage is wanted: it does not
  depend on the profiles' order.

Without `[dir]`, the three header analyses scan Hal's match database
(`Reports.XgDir`) and `DuplicateProblems` a BatchAnalyze positions folder
(`Reports.PositionsDir`). An unknown or missing name prints usage and exits 2;
an absent input directory says so and exits 1. The reports used to be xUnit
facts, so every test run ran three or four full analyses on Hal's machine —
most of the suite's time — and overwrote their CSVs
(halheinrich/backgammon#438). A command keeps the test run to tests, and the
entry point lives inside this assembly so it reaches the `internal` `Analyses`
without widening anything. `ReportCommand.Run` takes the directory check and
the run as arguments, which is what lets `ReportCommandTests` pin the dispatch
without running an analysis.

**Analysis method shape — compute / persist split.** Each analysis is two
methods on `Analyses`. `Compute*(string xgDir, Action<string> log)` does the
streaming scan, writes progress + summary to the `log` callback, and **returns**
the aggregated data as an immutable result record (`AnalysisResults.cs`) — no
file output. The matching `public static void` wrapper `(string xgDir,
Action<string> log)` calls the aggregator, then writes the result to a CSV. The
split keeps the machine-dependent file write out of the computation path so the
aggregation is testable without touching disk; the `log` callback is the only
side effect the aggregator has, and it is dependency-injected.

**File iteration.** Each analysis calls `XgFileReader.EnumerateXgFormatFiles` (the shared public helper in `ConvertXgToJson_Lib`), which yields `*.xg` match files concatenated with `*.xgp` position files. Each file is then parsed at the level the analysis needs: `XgFileReader.ReadMatchInfo` (match-level), `XgFileReader.ReadGameHeaders` with a shared `XgIteratorState` (game-level), or `XgFileReader.ReadFile` + `XgDecisionIterator.IterateDiagramRequests` (decision-level — `DuplicateProblems` only; see Depends on). `try { ... } catch { continue; }` silently skips unreadable files (see Pitfalls) — except in `DuplicateProblems`, which counts the skips and logs the total. Enumeration and its tests now single-source from the producer (`XgFileReaderDiscoveryTests`); the formerly-duplicated private helper here has been removed.

**Progress reporting.** Each analysis prints a status line after scanned-item counts 1, 2, 4, 8, 16, … via an exponential-backoff `nextReport` counter (matches for the header analyses, files for `DuplicateProblems`). Rate is computed from a `Stopwatch`.

**CSV output.** Each analysis writes its result CSV to a hard-coded path under `D:\Users\Hal\Documents\Excel\Backgammon\`. No prompt, no overwrite guard — running twice overwrites.

**Money and match by kind (MatchScoreDistribution).** Each game's session is
paired from the header's terms and the game's standing by BgDataTypes_Lib's
`GameSession.Create`, the producer's one rule for pairing the two (it also
holds them to one kind), and read through the game session's exhaustive
`Switch`: a money game is counted as such (`MoneyGameCount`), and never as a
score; a match game lands in a score bucket, from its terms' length and its
standing's away scores and Crawford flag. The CSV leads with a `Session` column (`Money` / `Match`, the kind's name)
and leaves a money row's match columns empty, as the ecosystem's flat
decision row does for the other kind's columns.

**Score normalization (MatchScoreDistribution).** Keys are a match game's `(MatchLength, Away1, Away2, IsCrawford)` with `Away1 <= Away2` — the pair is swapped so both player perspectives collapse onto one bucket.

**Duplicate-problem identity (`DuplicateProblems`).** Grouping is over
`ProblemKey` — the ecosystem's single content-identity derivation, the same key
BgGame_Lib's `DistinctPositionProblemSetSource` applies for the quiz. This
analysis invents no identity of its own. Every record has a key
(`ProblemKey.From` is total; `../SPEC-stats-identity.md` §2, amended
2026-09-27, halheinrich/backgammon#273), so every decision joins exactly one
content class; the analysis relies on that rather than guarding against it.
Two rules complete it, both ratified in halheinrich/backgammon#117:

- **Report-only.** The library never deletes. `RedundantFiles` is a
  recommendation the caller acts on.
- **Keeper = ordinal-first filename** within each content-equivalence class.

**File-level redundancy is a roll-up, not an identity of its own.** A file is
redundant iff it contributed at least one decision and *none* of those
decisions is essential — essential meaning a class keeper or a problem seen
only there. Deleting exactly that set loses no problem. Over a
one-decision-per-file `.xgp` folder (the halheinrich/backgammon#117 use case)
this degenerates to "every non-keeper file"; over `.xg` match files it is what
stops the report recommending the deletion of a match that also carries
positions found nowhere else.

**Pure grouping seam.** `ComputeDuplicateProblems` owns the scan — enumeration,
parse, progress, skip counting — and `GroupDuplicateProblems` owns the grouping
over an `IEnumerable<BgDecisionData>`, with no file access and no logging. The
split is what lets the grouping and the file roll-up be pinned over records
built with `TestRecords` — one problem under two Jacoby rules, a match file
holding both a redundant copy and a problem found nowhere else — because the
fixture-agnostic `TestData/xg` must never be depended on for a shape.

**Immutable results.** Every collection a result record holds is an immutable
copy made where the record is built (`ImmutableCopy`), by construction and by
`with` alike: the read-only view it hands out is neither the caller's
collection nor writable through a cast.

## Public API

**There is no public API.** This is a personal analysis tool run as its own
command, not a library: nothing in this assembly is `public`. Its callers are
its own entry point (`Program` → `ReportCommand`, see Architecture) and the
test assembly, which reaches everything below through the
`InternalsVisibleTo` in `XgAnalytics.csproj`.

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
    public static DuplicateProblemsResult      ComputeDuplicateProblems      (string xgDir, Action<string> log);

    // Pure grouping core of ComputeDuplicateProblems — no file access, no log.
    public static DuplicateProblemsResult GroupDuplicateProblems(IEnumerable<BgDecisionData> decisions);

    // Persistence wrappers — call the aggregator, then write a CSV.
    public static void PlayerMatchCount       (string xgDir, Action<string> log);
    public static void NonStandardStarts      (string xgDir, Action<string> log);
    public static void MatchScoreDistribution (string xgDir, Action<string> log);
    public static void DuplicateProblems      (string xgDir, Action<string> log);
}
```

The `Compute*` methods log progress incrementally via the `log` callback and
return their aggregated result (`PlayerMatchCountResult`,
`NonStandardStartsResult`, `MatchScoreDistributionResult`,
`DuplicateProblemsResult` in `AnalysisResults.cs` — immutable records whose
collections are immutable copies, handed out as read-only views; the score
distribution keys match games by the normalized `MatchScoreKey` and counts
money games apart, by kind). Those records, and the `PlayerMatchTally` /
`NonStandardStart` / `DuplicateProblemGroup` elements they carry, are
`internal` for the same reason as `Analyses` — they are these methods' return
types. `GroupDuplicateProblems` is the one aggregator whose pure core is
exposed separately, so the grouping and the file roll-up can be pinned over
synthesized records rather than the churning corpus (see Architecture). The
`void` wrappers add the CSV write to a hard-coded path; their observable
output is the `log` stream plus the CSV file.

## Pitfalls

- **Hard-coded CSV output paths** under `D:\Users\Hal\Documents\Excel\Backgammon\`, baked into the `void` wrappers. The directory must already exist — no `Directory.CreateDirectory` call. The wrappers won't run on a non-Hal machine; the `Compute*` aggregators carry no such dependency.
- **Two test layers, both green-on-any-machine; neither runs a report.** Running a report is the command's job (see Architecture), and the reports do not all share one input: the three header analyses scan `...\hhDb\Xg`, while `DuplicateProblems` scans a BatchAnalyze `Positions\` folder, the folder-cleanup case it exists for. The tests are corpus shape-invariant tests over the shared `TestData/xg` (guarded for an empty/absent corpus) plus pinned-fixture discrimination tests over `TestData/FixtureFiles` (the 23-point match, and `MoneyTest.xg` for money read by kind); they call the `Compute*` aggregators and write no CSV. Never make the corpus tests pin a filename or count — that corpus churns (see `../AGENTS.md` TestData convention).
- **Silent parse-failure swallow.** `catch { continue; }` hides corrupted-file exceptions entirely — neither logged nor counted. A batch can appear to "complete" while skipping a meaningful fraction of input. `DuplicateProblems` is the exception: it still swallows the exception, but counts the file and logs the total.
- **CSV overwrite with no guard.** Re-running an analysis clobbers its previous CSV without warning.
- **Unfiltered iteration.** Despite the `XgFilter_Lib` project reference, no analysis filters — every `.xg`/`.xgp` in `xgDir` is processed.
- **`DuplicateProblems` is materially slower per file** than the header analyses — it runs the full decision parse, not the header fast path (~110 files/sec over `TestData/xg`: 343 files, 46,327 decisions, ~3 s). Expected, and the price of per-decision content; don't "optimize" it back onto `ReadGameHeaders`, which cannot see a decision's board, cube or dice.
- **`DuplicateProblems` reports bare filenames**, not paths: `EnumerateXgFormatFiles(string)` is non-recursive, so the scan is one flat directory and names are unique within it. A caller acting on `RedundantFiles` must re-join `xgDir`.

## Subproject-internal next steps

- **`NonStandardStarts` positive-detection coverage gap.** The clean-up-pass test session added Layer-2 fixture-discrimination tests that confirm `PlayerMatchCount` and `MatchScoreDistribution` recover real values from the Achim Mueller fixture — but **not `NonStandardStarts`**, the analysis Layer-2 was named for: the reused fixture has standard starts and the live corpus has zero non-standard, so an empty result still passes every invariant even if detection were broken. No existing `FixtureFiles` entry has a known non-standard start. Best-practice fix: when a `.xg` with a known non-standard start is available, add it (append-only) and a positive `NonStandardStarts` detection test asserting it's flagged with the right game/players. Deferred by decision (sourcing the fixture was out of the clean-up pass's scope).
- **Parameterize the analysis CSV destination.** The clean-up-pass session split each analysis into a `Compute*` aggregator (testable, no file write) + a thin `void` wrapper that still writes to the hard-coded `D:\…\Excel\Backgammon\*.csv` path. Now that compute/persist are decoupled, the natural follow-up is to parameterize the wrapper's output (path / `TextWriter` arg, or at minimum a `Directory.CreateDirectory` guard) so it's not machine-pinned. Mentioned-not-folded by that session per scope discipline.
- **`DuplicateProblems` scans one flat directory; the target tree is now nested.** halheinrich/backgammon#117's motivating population — a flat `Positions\Move2` of 1393 `.xgp` files, of which 308 measured redundant — no longer exists in that form: as of 2026-08-24 that folder holds per-score subfolders (`3a3a`, `2a3a`, `DMP`, …), so cleaning the tree means one run per subfolder. That 308-of-1393 figure is also **pre-v3**: it was measured under the v2 `ProblemKey`, before halheinrich/backgammon#120 put Jacoby in the money grammar. Under v3 money keys split on Jacoby (`0a0j` / `0a0nj`), so the true count for that population was lower — it is a historical order-of-magnitude marker, not a number to reproduce. `XgFileReader` already offers an `EnumerateXgFormatFiles(string, SearchOption)` overload; a recursive option on the analysis would be the fix. Deliberately out of halheinrich/backgammon#117's scope (non-goal: no CLI, no new discovery behaviour), and note that going recursive breaks the bare-filename assumption in Pitfalls — `RedundantFiles` would have to carry relative paths.
