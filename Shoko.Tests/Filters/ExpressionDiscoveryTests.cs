using System;
using System.Linq;
using Shoko.Abstractions.Filtering;
using Shoko.Abstractions.Filtering.Expressions;
using Shoko.Abstractions.Filtering.Expressions.Info;
using Shoko.Abstractions.Filtering.Sorting;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Filters;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.Metadata;
using Shoko.Server.Models.Release;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Cached.AniDB;
using Shoko.Server.Repositories.Cached.Metadata;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Filters;

/// <summary>
/// Covers the values the filter help offers for the parameters of each expression.
/// </summary>
[Collection(nameof(RepoFactoryCollection))]
public class ExpressionDiscoveryTests
{
    [Fact]
    public void SourceTagPairs_GroupsBySourceInOrder_OnceEachIgnoringCase()
    {
        (string, string)[] tags =
        [
            ("tmdb", "Drama"),
            ("plugin", "action"),
            ("tmdb", "drama"),
            ("tmdb", "Comedy"),
            ("anidb", "Action"),
            ("plugin", ""),
        ];

        var pairs = ExpressionDiscovery.SourceTagPairs(tags, ["plugin", "tmdb"]);

        string[][] expected = [["plugin", "action"], ["tmdb", "Comedy"], ["tmdb", "Drama"]];
        Assert.Equal(expected, pairs);
    }

    #region One value list per parameter

    [Fact]
    public void EveryBuiltInExpression_OffersOneValueListPerParameter()
    {
        // One airing series and one tag, so the help built from stored data has pairs to clash with.
        using var scope = new RepoFactoryScope()
            .With<AniDB_AnimeRepository, int, AniDB_Anime>(a => a.AniDB_AnimeID,
                [new() { AniDB_AnimeID = 1, AnimeID = 1, AnimeType = AnimeType.TV, AirDate = new(2020, 4, 3), EndDate = new(2020, 6, 26) }])
            .With<AnimeSeriesRepository, int, AnimeSeries>(s => s.AnimeSeriesID, [new() { AnimeSeriesID = 1, AniDB_ID = 1 }])
            .With<AniDB_EpisodeRepository, int, AniDB_Episode>(e => e.AniDB_EpisodeID)
            .With<AniDB_Anime_TagRepository, int, AniDB_Anime_Tag>(t => t.AniDB_Anime_TagID)
            .With<AniDB_TagRepository, int, AniDB_Tag>(t => t.AniDB_TagID)
            .With<StoredReleaseInfoRepository, int, StoredReleaseInfo>(r => r.StoredReleaseInfoID)
            .With<Metadata_TagRepository, int, Metadata_Tag>(t => t.Metadata_TagID,
                [new() { Metadata_TagID = 1, Source = MetadataSource.TMDB, ProviderID = "genre/18", Name = "Drama", Kind = TagKind.Genre }])
            .With<Metadata_Tag_EntryRepository, int, Metadata_Tag_Entry>(e => e.Metadata_Tag_EntryID);
        var types = typeof(FilterExpression).Assembly.GetTypes()
            .Where(type => typeof(FilterExpression).IsAssignableFrom(type) && !typeof(SortingExpression).IsAssignableFrom(type));

        var help = types.Select(ExpressionDiscovery.GetExpressionHelp).OfType<IFilterExpressionHelp>().ToDictionary(h => h.InternalType);

        Assert.NotEmpty(help[typeof(InSeasonExpression)].PossibleParameterPairs!);
        Assert.NotEmpty(help[typeof(HasSourceGenreExpression)].PossibleParameterPairs!);
    }

    [Fact]
    public void AnExpressionOfferingPairsAndAList_Throws()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => ExpressionDiscovery.GetExpressionHelp(typeof(PairsAndListProbeExpression)));

        Assert.Contains(nameof(PairsAndListProbeExpression), exception.Message);
    }

    /// <summary>
    /// Offers pairs and a separate list of second parameters, which discovery refuses.
    /// </summary>
    private sealed class PairsAndListProbeExpression : FilterExpression<bool>
    {
        public override string[][] HelpPossibleParameterPairs => [["a", "b"]];

        public override string[] HelpPossibleSecondParameters => ["b"];

        public override bool Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time)
            => false;
    }

    #endregion
}
