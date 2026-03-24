# XgAnalytics — Project Instructions

Part of the Backgammon tools ecosystem: https://github.com/halheinrich/backgammon
**After committing here, return to the Backgammon Umbrella project to update hashes and instructions doc.**

## Purpose
Ad-hoc analysis tools and queries against .xg/.xgp files. One-off scripts and exploratory code that doesn't belong in the core libraries or the Blazor app.

## Stack
C# / .NET 10 / Visual Studio 2026 / Windows

## Local root
`D:\Users\Hal\Documents\Visual Studio 2026\Projects\backgammon\`

## Shared infrastructure

### TestData
- Location: `D:\Users\Hal\Documents\Visual Studio 2026\Projects\backgammon\TestData`
- `BothWays/` subfolder: `ThisWay.xg` and `ThatWay.xg` — same match, perspectives reversed
- Shared across all projects — do NOT put TestData inside individual project directories

## Dependencies

### ConvertXgToJson_Lib (commit `8fe6069`)
Reads .xg/.xgp files; produces DecisionRow records.

Key files:
- DecisionRow.cs: https://raw.githubusercontent.com/halheinrich/ConvertXgToJson_Lib/8fe6069/ConvertXgToJson_Lib/Models/DecisionRow.cs
- XgDecisionIterator.cs: https://raw.githubusercontent.com/halheinrich/ConvertXgToJson_Lib/8fe6069/ConvertXgToJson_Lib/XgDecisionIterator.cs
- XgMatchInfo.cs: https://raw.githubusercontent.com/halheinrich/ConvertXgToJson_Lib/8fe6069/ConvertXgToJson_Lib/XgMatchInfo.cs
- XgGameInfo.cs: https://raw.githubusercontent.com/halheinrich/ConvertXgToJson_Lib/8fe6069/ConvertXgToJson_Lib/XgGameInfo.cs
- ConvertXgToJson_Lib.csproj: https://raw.githubusercontent.com/halheinrich/ConvertXgToJson_Lib/8fe6069/ConvertXgToJson_Lib/ConvertXgToJson_Lib.csproj

### XgFilter_Lib (commit `9384c3b`)
Filtering and classification of DecisionRow records.

Key files:
- Filtering/IDecisionFilter.cs: https://raw.githubusercontent.com/halheinrich/XgFilter_Lib/9384c3b/XgFilter_Lib/Filtering/IDecisionFilter.cs
- Filtering/IMatchFilter.cs: https://raw.githubusercontent.com/halheinrich/XgFilter_Lib/9384c3b/XgFilter_Lib/Filtering/IMatchFilter.cs
- Filtering/DecisionFilterSet.cs: https://raw.githubusercontent.com/halheinrich/XgFilter_Lib/9384c3b/XgFilter_Lib/Filtering/DecisionFilterSet.cs
- FilteredDecisionIterator.cs: https://raw.githubusercontent.com/halheinrich/XgFilter_Lib/9384c3b/XgFilter_Lib/FilteredDecisionIterator.cs
- XgFilter_Lib.csproj: https://raw.githubusercontent.com/halheinrich/XgFilter_Lib/9384c3b/XgFilter_Lib/XgFilter_Lib.csproj

## Key facts

### DecisionRow.Board
- `int[]` 26 elements
- `board[0]` = opponent bar (never positive)
- `board[1–24]` = points 1–24 from player on roll's perspective
- `board[25]` = player bar (never negative)
- Positive = player on roll; negative = opponent

### XGID
- Always normalized to bottom-player perspective

### Player names
- Available on DecisionRow — use to group/count matches

## Current analyses
- Player match count — list of players and how many matches they've played

## Shared rules

See `AGENTS.md` in the umbrella repo — applies to all sub-projects.
`https://raw.githubusercontent.com/halheinrich/backgammon/main/AGENTS.md`

## Session handoff
After any commit:
1. `git rev-parse HEAD` — note the short hash
2. Update commit hash and URLs in this doc
3. Return to Backgammon Umbrella project — update umbrella instructions doc
