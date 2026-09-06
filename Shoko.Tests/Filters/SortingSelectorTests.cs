using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Filtering.Sorting;
using Shoko.Abstractions.Filtering.Sorting.Selectors;
using Shoko.Abstractions.Metadata;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Filters;

/// <summary>
/// Covers the sorting selectors, which had no tests of any kind. Each one projects a filterable to
/// the value the collection is ordered by, so a selector that throws, returns null, or quietly
/// disagrees with its own <c>TimeDependent</c>/<c>UserDependent</c> flags produces a wrong or
/// broken sort order for the user.
/// </summary>
public class SortingSelectorTests
{
    private static readonly Type[] s_selectorTypes =
    [
        .. typeof(SortingExpression).Assembly
            .GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false, IsPublic: true })
            .Where(typeof(SortingExpression).IsAssignableFrom)
            .OrderBy(t => t.FullName, StringComparer.Ordinal),
    ];

    private static readonly TestFilterable s_filterable = FilterableFactory.CreatePopulated<TestFilterable>();

    private static readonly TestFilterableUserInfo s_userInfo = FilterableFactory.CreatePopulated<TestFilterableUserInfo>();

    private static SortingExpression Create(string fullName)
        => (SortingExpression)Activator.CreateInstance(s_selectorTypes.Single(t => t.FullName == fullName))!;

    public static TheoryData<string> AllSelectors()
    {
        var data = new TheoryData<string>();
        foreach (var type in s_selectorTypes.Where(t => t.GetConstructor(Type.EmptyTypes) is not null))
            data.Add(type.FullName!);

        return data;
    }

    #region Discovery

    [Fact]
    public void TheSelectorsAreDiscovered()
    {
        // Guards the theories below from silently emptying out.
        Assert.True(s_selectorTypes.Length > 50, $"Only found {s_selectorTypes.Length} sorting selectors.");
    }

    [Fact]
    public void EverySelectorCanBeConstructedWithoutArguments()
    {
        // Stored sort orders are rebuilt by the JSON deserialiser, which needs a parameterless ctor.
        var missing = s_selectorTypes.Where(t => t.GetConstructor(Type.EmptyTypes) is null).Select(t => t.FullName).ToArray();

        Assert.Empty(missing);
    }

    #endregion

    #region Contract held by every selector

    [Theory]
    [MemberData(nameof(AllSelectors))]
    public void EverySelectorProducesSomethingComparable(string fullName)
    {
        // The result is what the collection is ordered by, so it has to be comparable.
        Assert.IsAssignableFrom<IComparable>(Create(fullName).Evaluate(s_filterable, s_userInfo, s_date));
    }

    /// <summary>
    /// Every selector paired with the property it reads. Generated from the selector sources, then
    /// held here so a selector that starts reading a different field fails.
    /// </summary>
    public static TheoryData<string, string, string, string> SelectorProperties() => new()
    {
        { "AddedDateSortingSelector", "filterable", "AddedDate", "" },
        { "AirDateSortingSelector", "filterable", "AirDate", "ToDateTime" },
        { "AudioLanguageCountSortingSelector", "filterable", "AudioLanguages.Count", "" },
        { "AverageAniDBRatingSortingSelector", "filterable", "AverageAniDBRating", "" },
        { "BluRaySourceCountSortingSelector", "filterable", "FileSourceCounts.BluRay", "" },
        { "CameraSourceCountSortingSelector", "filterable", "FileSourceCounts.Camera", "" },
        { "CreditsEpisodesCountSortingSelector", "filterable", "EpisodeCounts.Credits", "" },
        { "CustomTagCountSortingSelector", "filterable", "CustomTags.Count", "" },
        { "DescriptionSortingSelector", "filterable", "Description", "" },
        { "DvdSourceCountSortingSelector", "filterable", "FileSourceCounts.DVD", "" },
        { "EpisodeCountSortingSelector", "filterable", "EpisodeCount", "" },
        { "FilmSourceCountSortingSelector", "filterable", "FileSourceCounts.Film", "" },
        { "GroupIDSortingSelector", "filterable", "GroupID", "" },
        { "HiddenEpisodesSortingSelector", "filterable", "HiddenEpisodes", "" },
        { "HighestAniDBRatingSortingSelector", "filterable", "HighestAniDBRating", "" },
        { "HighestUserRatingSortingSelector", "userInfo", "HighestUserRating", "" },
        { "LaserDiscSourceCountSortingSelector", "filterable", "FileSourceCounts.LaserDisc", "" },
        { "LastAddedDateSortingSelector", "filterable", "LastAddedDate", "" },
        { "LastAirDateSortingSelector", "filterable", "LastAirDate", "ToDateTime" },
        { "LastWatchedDateSortingSelector", "userInfo", "LastWatchedDate", "" },
        { "LocalCreditsEpisodesCountSortingSelector", "filterable", "LocalEpisodeCounts.Credits", "" },
        { "LocalEpisodesCountSortingSelector", "filterable", "LocalEpisodeCounts.Episodes", "" },
        { "LocalOthersEpisodesCountSortingSelector", "filterable", "LocalEpisodeCounts.Others", "" },
        { "LocalParodiesEpisodesCountSortingSelector", "filterable", "LocalEpisodeCounts.Parodies", "" },
        { "LocalSpecialEpisodesCountSortingSelector", "filterable", "LocalEpisodeCounts.Specials", "" },
        { "LocalTrailersEpisodesCountSortingSelector", "filterable", "LocalEpisodeCounts.Trailers", "" },
        { "LowestAniDBRatingSortingSelector", "filterable", "LowestAniDBRating", "" },
        { "LowestUserRatingSortingSelector", "userInfo", "LowestUserRating", "" },
        { "MainNameSortingSelector", "filterable", "MainName", "" },
        { "MissingCreditsEpisodesCountSortingSelector", "filterable", "MissingEpisodeCounts.Credits", "" },
        { "MissingEpisodeCollectingCountSortingSelector", "filterable", "MissingEpisodesCollecting", "" },
        { "MissingEpisodeCountSortingSelector", "filterable", "MissingEpisodes", "" },
        { "MissingEpisodesCountSortingSelector", "filterable", "MissingEpisodeCounts.Episodes", "" },
        { "MissingOthersEpisodesCountSortingSelector", "filterable", "MissingEpisodeCounts.Others", "" },
        { "MissingParodiesEpisodesCountSortingSelector", "filterable", "MissingEpisodeCounts.Parodies", "" },
        { "MissingSpecialEpisodesCountSortingSelector", "filterable", "MissingEpisodeCounts.Specials", "" },
        { "MissingTrailersEpisodesCountSortingSelector", "filterable", "MissingEpisodeCounts.Trailers", "" },
        { "NameSortingSelector", "filterable", "Name", "" },
        { "OriginalNameSortingSelector", "filterable", "OriginalName", "" },
        { "OtherSourceCountSortingSelector", "filterable", "FileSourceCounts.Other", "" },
        { "OthersEpisodesCountSortingSelector", "filterable", "EpisodeCounts.Others", "" },
        { "ParodiesEpisodesCountSortingSelector", "filterable", "EpisodeCounts.Parodies", "" },
        { "SeriesCountSortingSelector", "filterable", "SeriesCount", "" },
        { "SeriesPermanentVoteCountSortingSelector", "userInfo", "SeriesPermanentVoteCount", "" },
        { "SeriesTemporaryVoteCountSortingSelector", "userInfo", "SeriesTemporaryVoteCount", "" },
        { "SeriesVoteCountSortingSelector", "userInfo", "SeriesVoteCount", "" },
        { "SortNameSortingSelector", "filterable", "SortName", "" },
        { "SortingNameSortingSelector", "filterable", "SortName", "" },
        { "SpecialEpisodesCountSortingSelector", "filterable", "EpisodeCounts.Specials", "" },
        { "SubtitleLanguageCountSortingSelector", "filterable", "SubtitleLanguages.Count", "" },
        { "TopLevelGroupIDSortingSelector", "filterable", "TopLevelGroupID", "" },
        { "TotalEpisodeCountSortingSelector", "filterable", "TotalEpisodeCount", "" },
        { "TrailersEpisodesCountSortingSelector", "filterable", "EpisodeCounts.Trailers", "" },
        { "TvSourceCountSortingSelector", "filterable", "FileSourceCounts.TV", "" },
        { "UnairedCreditsEpisodesCountSortingSelector", "filterable", "UnairedEpisodeCounts.Credits", "" },
        { "UnairedEpisodesCountSortingSelector", "filterable", "UnairedEpisodeCounts.Episodes", "" },
        { "UnairedOthersEpisodesCountSortingSelector", "filterable", "UnairedEpisodeCounts.Others", "" },
        { "UnairedParodiesEpisodesCountSortingSelector", "filterable", "UnairedEpisodeCounts.Parodies", "" },
        { "UnairedSpecialEpisodesCountSortingSelector", "filterable", "UnairedEpisodeCounts.Specials", "" },
        { "UnairedTrailersEpisodesCountSortingSelector", "filterable", "UnairedEpisodeCounts.Trailers", "" },
        { "UnknownSourceCountSortingSelector", "filterable", "FileSourceCounts.Unknown", "" },
        { "UnwatchedEpisodeCountSortingSelector", "userInfo", "UnwatchedEpisodes", "" },
        { "UserTagCountSortingSelector", "userInfo", "UserTags.Count", "" },
        { "VcdSourceCountSortingSelector", "filterable", "FileSourceCounts.VCD", "" },
        { "VhsSourceCountSortingSelector", "filterable", "FileSourceCounts.VHS", "" },
        { "WatchedCreditsEpisodesCountSortingSelector", "userInfo", "WatchedEpisodeCounts.Credits", "" },
        { "WatchedDateSortingSelector", "userInfo", "WatchedDate", "" },
        { "WatchedEpisodeCountSortingSelector", "userInfo", "WatchedEpisodes", "" },
        { "WatchedEpisodesCountSortingSelector", "userInfo", "WatchedEpisodeCounts.Episodes", "" },
        { "WatchedOthersEpisodesCountSortingSelector", "userInfo", "WatchedEpisodeCounts.Others", "" },
        { "WatchedParodiesEpisodesCountSortingSelector", "userInfo", "WatchedEpisodeCounts.Parodies", "" },
        { "WatchedSpecialEpisodesCountSortingSelector", "userInfo", "WatchedEpisodeCounts.Specials", "" },
        { "WatchedTrailersEpisodesCountSortingSelector", "userInfo", "WatchedEpisodeCounts.Trailers", "" },
        { "WebSourceCountSortingSelector", "filterable", "FileSourceCounts.Web", "" },
    };

    [Fact]
    public void EverySelectorIsInThePropertyTable()
    {
        // Without this a selector added tomorrow would be covered only by "returns something
        // comparable", which any non-null return satisfies.
        var tabled = SelectorProperties().Select(row => row.Data.Item1).ToHashSet(StringComparer.Ordinal);
        var missing = s_selectorTypes.Select(t => t.Name)
            // Not a plain property read; its scoring is exercised separately.
            .Except(["FuzzyNameRelevanceSortingSelector"], StringComparer.Ordinal)
            .Except(tabled, StringComparer.Ordinal)
            .Order(StringComparer.Ordinal);

        Assert.Equal(string.Empty, string.Join(", ", missing));
    }

    [Fact]
    public void TheTestDataTellsEverySelectorApart()
    {
        // If two selectors reading different fields resolve to the same value on the double, the
        // theory below cannot tell one from the other and both assertions become decoration.
        // Selectors that genuinely read the same field are expected to agree.
        var byValue = new Dictionary<string, HashSet<string>>();
        foreach (var row in SelectorProperties())
        {
            var (_, source, path, _) = row.Data;
            var expected = Resolve(source == "userInfo" ? s_userInfo : s_filterable, path);
            byValue.TryAdd($"{expected}", []);
            byValue[$"{expected}"].Add($"{source}.{path}");
        }

        var collisions = byValue.Where(entry => entry.Value.Count > 1)
            .Select(entry => $"{entry.Key}: {string.Join(", ", entry.Value.Order())}");

        Assert.Equal(string.Empty, string.Join(" | ", collisions));
    }

    [Theory]
    [MemberData(nameof(SelectorProperties))]
    public void EverySelectorReadsTheFieldItIsNamedFor(string selector, string source, string path, string transform)
    {
        var type = s_selectorTypes.Single(t => t.Name == selector);
        var instance = (SortingExpression)Activator.CreateInstance(type)!;
        object root = source == "userInfo" ? s_userInfo : s_filterable;

        var expected = Resolve(root, path);
        if (transform == "ToDateTime")
            expected = ((PartialDateOnly)expected!).ToDateTime();

        Assert.Equal(expected, instance.Evaluate(s_filterable, s_userInfo, s_date));
    }

    /// <summary>Walks a dotted property path off the populated double.</summary>
    private static object? Resolve(object root, string path)
    {
        var current = root;
        foreach (var part in path.Split('.'))
        {
            var property = current!.GetType().GetProperty(part)
                ?? throw new InvalidOperationException($"No property '{part}' on {current.GetType().Name}.");
            current = property.GetValue(current);
        }

        return current;
    }

    #endregion

    #region Defaults

    private static readonly DateTime s_date = new(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc);

    [Fact]
    public void Descending_DefaultsToAscending()
        => Assert.False(new AddedDateSortingSelector().Descending);

    [Fact]
    public void Next_DefaultsToNoFurtherSort()
        => Assert.Null(new AddedDateSortingSelector().Next);

    #endregion
}
