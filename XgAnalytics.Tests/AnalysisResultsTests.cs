using BgDataTypes_Lib;
using BgDataTypes_Lib.TestSupport;
using AwesomeAssertions;

namespace XgAnalytics.Tests;

/// <summary>
/// Pins that every collection a result record holds is an immutable copy,
/// by construction and by <c>with</c> alike: the caller's collection no
/// longer reaches the record, and the read-only view handed out refuses a
/// write through a cast. A record holding the caller's <see cref="List{T}"/>,
/// <see cref="Dictionary{TKey, TValue}"/> or array fails both halves.
/// </summary>
public class AnalysisResultsTests
{
    [Fact]
    public void PlayerMatchTally_HoldsAnImmutableCopyOfItsMatchIds()
    {
        List<string> ids = ["m1", "m2"];
        var tally = new PlayerMatchTally("p", ids);
        ShouldHoldAnImmutableCopy(ids, tally.MatchIds, "MatchIds, constructed");

        List<string> replaced = ["m3"];
        ShouldHoldAnImmutableCopy(replaced, (tally with { MatchIds = replaced }).MatchIds, "MatchIds, with");
    }

    [Fact]
    public void PlayerMatchCountResult_HoldsAnImmutableCopyOfItsPlayers()
    {
        List<PlayerMatchTally> players = [new("p", ["m1"])];
        var result = new PlayerMatchCountResult(players, DistinctMatchCount: 1);
        ShouldHoldAnImmutableCopy(players, result.Players, "Players, constructed");

        List<PlayerMatchTally> replaced = [new("q", ["m2"])];
        ShouldHoldAnImmutableCopy(replaced, (result with { Players = replaced }).Players, "Players, with");
    }

    [Fact]
    public void NonStandardStartsResult_HoldsAnImmutableCopyOfItsGames()
    {
        List<NonStandardStart> games = [new("m", 1, "p1", "p2")];
        var result = new NonStandardStartsResult(games, GameCount: 1, MatchCount: 1);
        ShouldHoldAnImmutableCopy(games, result.NonStandard, "NonStandard, constructed");

        List<NonStandardStart> replaced = [new("m", 2, "p1", "p2")];
        ShouldHoldAnImmutableCopy(replaced, (result with { NonStandard = replaced }).NonStandard, "NonStandard, with");
    }

    [Fact]
    public void MatchScoreDistributionResult_HoldsAnImmutableCopyOfItsCounts()
    {
        var counts = new Dictionary<MatchScoreKey, int> { [new(7, 7, 7, false)] = 1 };
        var result = new MatchScoreDistributionResult(counts, MoneyGameCount: 0, GameCount: 1, MatchCount: 1);
        ShouldHoldAnImmutableCopy(counts, result.Counts, "Counts, constructed");

        var replaced = new Dictionary<MatchScoreKey, int> { [new(5, 3, 5, false)] = 2 };
        ShouldHoldAnImmutableCopy(replaced, (result with { Counts = replaced }).Counts, "Counts, with");
    }

    [Fact]
    public void DuplicateProblemGroup_HoldsAnImmutableCopyOfItsOccurrences()
    {
        var key = ProblemKey.From(TestRecords.CheckerPlay());
        List<DecisionId> occurrences = [new XgpDecisionId("a.xgp"), new XgpDecisionId("b.xgp")];
        var group = new DuplicateProblemGroup(key, occurrences);
        ShouldHoldAnImmutableCopy(occurrences, group.Occurrences, "Occurrences, constructed");

        List<DecisionId> replaced = [new XgpDecisionId("c.xgp"), new XgpDecisionId("d.xgp")];
        ShouldHoldAnImmutableCopy(replaced, (group with { Occurrences = replaced }).Occurrences, "Occurrences, with");
    }

    [Fact]
    public void DuplicateProblemsResult_HoldsImmutableCopiesOfItsGroupsAndRedundantFiles()
    {
        var key = ProblemKey.From(TestRecords.CheckerPlay());
        List<DuplicateProblemGroup> groups = [new(key, [new XgpDecisionId("a.xgp"), new XgpDecisionId("b.xgp")])];
        List<string> redundantFiles = ["b.xgp"];
        var result = new DuplicateProblemsResult(
            groups, redundantFiles, FileCount: 2, ProblemCount: 2, DistinctProblemCount: 1);
        ShouldHoldAnImmutableCopy(groups, result.Groups, "Groups, constructed");
        ShouldHoldAnImmutableCopy(redundantFiles, result.RedundantFiles, "RedundantFiles, constructed");

        List<DuplicateProblemGroup> replacedGroups = [new(key, [new XgpDecisionId("c.xgp"), new XgpDecisionId("d.xgp")])];
        List<string> replacedFiles = ["d.xgp"];
        var replaced = result with { Groups = replacedGroups, RedundantFiles = replacedFiles };
        ShouldHoldAnImmutableCopy(replacedGroups, replaced.Groups, "Groups, with");
        ShouldHoldAnImmutableCopy(replacedFiles, replaced.RedundantFiles, "RedundantFiles, with");
    }

    [Fact]
    public void ResultRecords_RefuseANullCollection()
    {
        var key = ProblemKey.From(TestRecords.CheckerPlay());

        FluentActions.Invoking(() => new PlayerMatchTally("p", null!))
            .Should().Throw<ArgumentNullException>().WithParameterName("MatchIds");
        FluentActions.Invoking(() => new PlayerMatchCountResult(null!, 0))
            .Should().Throw<ArgumentNullException>().WithParameterName("Players");
        FluentActions.Invoking(() => new NonStandardStartsResult(null!, 0, 0))
            .Should().Throw<ArgumentNullException>().WithParameterName("NonStandard");
        FluentActions.Invoking(() => new MatchScoreDistributionResult(null!, 0, 0, 0))
            .Should().Throw<ArgumentNullException>().WithParameterName("Counts");
        FluentActions.Invoking(() => new DuplicateProblemGroup(key, null!))
            .Should().Throw<ArgumentNullException>().WithParameterName("Occurrences");
        FluentActions.Invoking(() => new DuplicateProblemsResult(null!, [], 0, 0, 0))
            .Should().Throw<ArgumentNullException>().WithParameterName("Groups");
        FluentActions.Invoking(() => new DuplicateProblemsResult([], null!, 0, 0, 0))
            .Should().Throw<ArgumentNullException>().WithParameterName("RedundantFiles");
    }

    /// <summary>
    /// <paramref name="view"/>, built from <paramref name="source"/>, is a copy
    /// the source no longer reaches and a cast cannot write.
    /// </summary>
    private static void ShouldHoldAnImmutableCopy<T>(List<T> source, IEnumerable<T> view, string member)
    {
        var snapshot = source.ToList();
        source.Add(source[0]);

        view.Should().Equal(snapshot, $"{member} is a copy, so the caller's list no longer reaches it");
        var writable = view.Should().BeAssignableTo<IList<T>>().Subject;
        writable.Invoking(list => list[0] = snapshot[0]).Should().Throw<NotSupportedException>(
            $"{member} refuses a write through a cast");
    }

    /// <summary>
    /// <paramref name="view"/>, built from <paramref name="source"/>, is a copy
    /// the source no longer reaches and a cast cannot write.
    /// </summary>
    private static void ShouldHoldAnImmutableCopy<TKey, TValue>(
        Dictionary<TKey, TValue> source, IReadOnlyDictionary<TKey, TValue> view, string member)
        where TKey : notnull
    {
        var snapshot = source.ToDictionary();
        var (firstKey, firstValue) = snapshot.First();
        source.Remove(firstKey);

        view.Should().BeEquivalentTo(snapshot, $"{member} is a copy, so the caller's dictionary no longer reaches it");
        var writable = view.Should().BeAssignableTo<IDictionary<TKey, TValue>>().Subject;
        writable.Invoking(dictionary => dictionary[firstKey] = firstValue).Should().Throw<NotSupportedException>(
            $"{member} refuses a write through a cast");
    }
}
