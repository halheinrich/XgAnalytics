# XgAnalytics

> Collaboration contract: [`../AGENTS.md`](../AGENTS.md)
> Umbrella status & dependency graph: [`../INSTRUCTIONS.md`](../INSTRUCTIONS.md)
> Mission & principles: [`../VISION.md`](../VISION.md)
> Open work: the [tracker](https://github.com/halheinrich/backgammon/issues), label `XgAnalytics`.

## What it is for

Hal's personal analysis tool over his match database and his BatchAnalyze
positions: a .NET 10 console command that runs one report over a folder of XG
files, logs its progress and writes a CSV. Nothing consumes it. There are four
reports:

| Report | What it answers | Scans by default | Parses |
|---|---|---|---|
| `PlayerMatchCount` | how many matches each player appears in | `Reports.XgDir` | match header |
| `NonStandardStarts` | which games did not start from the standard position | `Reports.XgDir` | game headers |
| `MatchScoreDistribution` | how many match games began at each score, and how many games were money | `Reports.XgDir` | game headers |
| `DuplicateProblems` | which decisions are the same problem, and which files hold nothing but copies | `Reports.PositionsDir` | every decision |

## Build and test

The solution is `XgAnalytics.slnx`: this repo's two projects and four of the
umbrella's (see Layout). It builds only inside an umbrella checkout, with those
members beside it. From this directory:

```bash
dotnet build XgAnalytics.slnx
```

```bash
dotnet test XgAnalytics.slnx
```

No test runs a report or writes a CSV; running a report is the command's job
(see Run a report).

**What the tests read.** `TestData` is the umbrella's, shared at
`backgammon\TestData` and gitignored (`../AGENTS.md`, Tests); `TestPaths` reaches
it from the test assembly's directory. The suite reads it at two layers, both
in `AnalysesTests`, and passes without either; everything else needs nothing:

- **Layer 1, `TestData/xg`:** shape invariants over whatever files are there.
  Each holds at any corpus size including zero, and an empty or absent corpus
  returns early. The corpus churns, so never make a Layer 1 test pin a filename,
  a count or a non-empty result.
- **Layer 2, `TestData/FixtureFiles`:** discrimination tests over the 23-point
  Achim Mueller match and `MoneyTest.xg`, which is what shows an analysis
  recovers real values, since an empty result satisfies Layer 1. A missing
  fixture returns early, so these pass vacuously without it
  (halheinrich/backgammon#500).

## Run a report

`Program` runs one named report over an optional input directory. The steps
live in the comment at the top of `XgAnalytics/Program.cs`, the one home of
them: choosing a report in Visual Studio's start-profile dropdown, scanning
another folder, the command-line forms, the exit codes, and what adding a report
takes. The usual form, from this directory:

```bash
dotnet run --project XgAnalytics -c Release -- <analysis> [dir]
```

Without `[dir]`, a report scans its default folder (the table above). Both
defaults and the CSV destination are paths on Hal's machine; see Pitfalls.

## Depends on

- **ConvertXgToJson_Lib** — file discovery (`XgFileReader.EnumerateXgFormatFiles`)
  plus **two parsing surfaces, chosen per analysis**: the fast-path metadata
  readers (`XgFileReader.ReadMatchInfo` / `ReadGameHeaders`, `XgMatchInfo`,
  `XgIteratorState`), the default for an analysis whose facts live in the file
  headers, and `XgFileReader.ReadFile` +
  `XgDecisionIterator.IterateDiagramRequests` for one that needs per-decision
  content the headers cannot carry (`DuplicateProblems`). What each does is that
  library's: its "Which surface for which task" and "XgFileReader"
  ([`../ConvertXgToJson_Lib/INSTRUCTIONS.md`](../ConvertXgToJson_Lib/INSTRUCTIONS.md)).
- **BgDataTypes_Lib** — transitive through ConvertXgToJson_Lib, consumed
  directly: `BgDecisionData`, `DecisionId`, `ProblemKey`, and `GameSession`,
  which `MatchScoreDistribution` builds from `XgMatchInfo.Terms` and
  `XgGameInfo.Standing`. Both rules are its own: "ProblemKey" and "Shared
  consumer contracts: IMatchInfo and IGameInfo"
  ([`../BgDataTypes_Lib/INSTRUCTIONS.md`](../BgDataTypes_Lib/INSTRUCTIONS.md)).
  What this tool does with them is under Architecture.
- **BgDataTypes_Lib.TestSupport** — test-only, referenced by `XgAnalytics.Tests`:
  the producer's record builders (`TestRecords`), the one way the tests build a
  decision record.
- **XgFilter_Lib** — referenced by `XgAnalytics.csproj` and consumed by nothing.
  Left in place for future filter-driven analyses.

## Layout

Six projects in one solution: the analyses with the command that runs them, and
their tests, are this repo's; BgDataTypes_Lib, BgDataTypes_Lib.TestSupport,
ConvertXgToJson_Lib and XgFilter_Lib are the outside four (see Depends on).

- **`XgAnalytics/`** — the analyses and their command entry point. No type in
  it is `public`. Three areas: the `Analyses` static class, which holds every
  analysis as a `Compute*` aggregator plus a `void` CSV-writing sibling; the
  immutable result records those aggregators return; and the entry point —
  `Program.cs`, the `ReportCommand` dispatch, the `Reports` table of names and
  default input directories, and one launch profile per report in
  `Properties/launchSettings.json`.
- **`XgAnalytics.Tests/`** — xUnit + AwesomeAssertions, reaching the app's
  internals through `InternalsVisibleTo`: `AnalysesTests` holds the two layers
  above and the pins of the pure grouping core over records built with
  `TestRecords`; `AnalysisResultsTests` pins the result records' immutable
  collections, `ReportCommandTests` the entry point's dispatch, and
  `LaunchProfilesTests` the launch profiles against `Reports.All`. Also carries
  `TestPaths`, the shared-`TestData` path helper, mirroring
  ConvertXgToJson_Lib.Tests'.

## Architecture

**A report is a command, not a test.** Running the reports as tests would put
full analyses and overwritten CSVs into every test run on Hal's machine; a
command keeps the test run to tests (halheinrich/backgammon#438). The entry
point lives inside this assembly so it reaches the `internal` `Analyses` without
widening anything. `ReportCommand.Run` takes the directory check and the run as
arguments, which is what lets `ReportCommandTests` pin the dispatch without
running an analysis.

**No public API.** This is a personal analysis tool run as its own command, not
a library: no type in this assembly is `public`, so the `public` members of
`Analyses` and the result records reach only its own entry point and the test
assembly, through the `InternalsVisibleTo` in `XgAnalytics.csproj`. Should a
real consumer ever appear, the answer is to design a public surface deliberately
(extracting an `XgAnalytics_Lib` if the analyses and the CSV wrappers want
separating), not to widen these to `public` by default.

**Analysis method shape — compute / persist split.** Each analysis is two
methods on `Analyses`. `Compute*(string xgDir, Action<string> log)` does the
streaming scan, writes progress + summary to the `log` callback, and **returns**
the aggregated data as an immutable result record (`AnalysisResults.cs`) — no
file output. The matching `void` wrapper `(string xgDir, Action<string> log)`
calls the aggregator, then writes the result to a CSV. The split keeps the
machine-dependent file write out of the computation path so the aggregation is
testable without touching disk; the `log` callback is the only side effect the
aggregator has, and it is dependency-injected. Progress lines thin out as a scan
runs: one after scanned-item counts 1, 2, 4, 8, 16, … (matches for the header
analyses, files for `DuplicateProblems`).

**Money and match by kind (`MatchScoreDistribution`).** Each game's session comes
from `GameSession.Create` and is read through its exhaustive `Switch`: a money
game is counted as such (`MoneyGameCount`), and never as a score; a match game
lands in a score bucket keyed `(MatchLength, Away1, Away2, IsCrawford)` with
`Away1 <= Away2`, the pair swapped so both player perspectives collapse onto one
bucket. The CSV leads with a `Session` column (`Money` / `Match`, the kind's
name) and leaves a money row's match columns empty, as the ecosystem's flat
decision row does for the other kind's columns.

**Duplicate-problem identity (`DuplicateProblems`).** Grouping is over
`ProblemKey`, the same key BgGame_Lib's `DistinctProblems` applies for the quiz.
This analysis invents no identity of its own. Every record has a key
(`ProblemKey.From` is total; `../SPEC-stats-identity.md` §2, amended
2026-09-27, halheinrich/backgammon#273), so every decision joins exactly one
content class; the analysis relies on that rather than guarding against it.
Two rules complete it, both ratified in halheinrich/backgammon#117:

- **Report-only.** Nothing here deletes. `RedundantFiles` is a recommendation
  the caller acts on.
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
over an `IEnumerable<BgDecisionData>`, with no file access and no logging. It is
the one aggregator whose pure core stands apart, which is what lets the grouping
and the file roll-up be pinned over records built with `TestRecords` — one
problem under two Jacoby rules, a match file holding both a redundant copy and a
problem found nowhere else — because the fixture-agnostic `TestData/xg` must
never be depended on for a shape.

**Immutable results.** Every collection a result record holds is an immutable
copy made where the record is built (`ImmutableCopy`, one of four such helpers
across the ecosystem, halheinrich/backgammon#316), by construction and by `with`
alike: the read-only view it hands out is neither the caller's collection nor
writable through a cast.

## Pitfalls

- **Reports run only on Hal's machine, and a rerun overwrites.** Each `void`
  wrapper writes `<report>.csv` to a hard-coded path under
  `D:\Users\Hal\Documents\Excel\Backgammon\`, with no prompt and no overwrite
  guard, so running a report twice clobbers its previous CSV. That directory
  must already exist (no `Directory.CreateDirectory`), and the write comes after
  the scan, so a missing one fails only once the scan is done. The `Compute*`
  aggregators carry no such dependency (halheinrich/backgammon#502).
- **The header analyses skip an unreadable file silently.** Their
  `catch { continue; }` swallows every exception and logs nothing, so a batch can
  appear to complete while skipping a meaningful fraction of its input, and a
  file that fails partway keeps the games already counted from it though the file
  itself is not counted. `DuplicateProblems` still swallows the exception, but
  counts the file and logs the total. halheinrich/backgammon#308 holds this with
  what else the records leave open: their `MatchCount` counts files, money
  sessions among them, and the `MatchScoreKey` pair's order and
  `DuplicateProblemGroup`'s occurrences are documented on the records but
  enforced only by how `Analyses` builds them.
- **An empty `NonStandardStarts` result proves nothing.** No `FixtureFiles` entry
  is known to hold a non-standard start, so no test asserts that the analysis
  flags a game, and an empty result passes every invariant even if detection were
  broken (halheinrich/backgammon#501).
- **Unfiltered iteration.** Despite the `XgFilter_Lib` reference (see Depends
  on), no analysis filters: every `.xg`/`.xgp` in `xgDir` is processed.
- **`DuplicateProblems` does more work per file** than the header analyses: it
  runs the full decision parse, not the header fast path. Expected, and the
  price of per-decision content; don't "optimize" it back onto `ReadGameHeaders`,
  which cannot see a decision's board, cube or dice.
- **`DuplicateProblems` scans one flat directory and reports bare filenames**, not
  paths, because `EnumerateXgFormatFiles(string)` is non-recursive and names are
  unique within one directory. A caller acting on `RedundantFiles` must re-join
  `xgDir`. A nested tree takes one run per subfolder, as `Positions\Move2` did
  on 2026-08-24, when it held per-score subfolders. A recursive scan would leave
  the names ambiguous, so `RedundantFiles` would have to carry relative paths
  (halheinrich/backgammon#503).
