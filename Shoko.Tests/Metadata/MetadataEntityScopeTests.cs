using System;
using System.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Metadata;

/// <summary>
/// Covers <see cref="MetadataEntityScope"/>: its factories, reading it back,
/// its set operations and its equality.
/// </summary>
public class MetadataEntityScopeTests
{
    [Fact]
    public void TheFactoriesBuildTheirPairs()
    {
        var single = MetadataEntityScope.Single(MetadataSource.TMDB, MetadataEntityType.Series);
        var forSource = MetadataEntityScope.ForSource(MetadataSource.TMDB, MetadataEntityType.Series, MetadataEntityType.Movie, MetadataEntityType.Series);
        var forSources = MetadataEntityScope.ForSources([MetadataSource.TMDB, MetadataSource.AniDB], [MetadataEntityType.Series, MetadataEntityType.Episode]);
        var fromPairs = MetadataEntityScope.FromPairs([(MetadataSource.AniDB, MetadataEntityType.Series), (TestSources.Plugin, TestEntityTypes.Library)]);

        Assert.Equal([(MetadataSource.TMDB, MetadataEntityType.Series)], single.ToList());
        Assert.Equal(2, forSource.Count);
        Assert.Equal(4, forSources.Count);
        Assert.True(forSources.Contains(MetadataSource.AniDB, MetadataEntityType.Episode));
        Assert.True(fromPairs.Contains(new MetadataGuid(TestSources.Plugin, TestEntityTypes.Library, "1")));
        Assert.False(fromPairs.Contains(new MetadataGuid(TestSources.Plugin, MetadataEntityType.Series, "1")));
        Assert.Same(MetadataEntityScope.Empty, MetadataEntityScope.FromPairs([]));
        Assert.True(MetadataEntityScope.Empty.IsEmpty);
    }

    [Fact]
    public void ItReadsBackItsSourcesAndKinds()
    {
        var scope = MetadataEntityScope.FromPairs([
            (MetadataSource.TMDB, MetadataEntityType.Movie),
            (MetadataSource.TMDB, MetadataEntityType.Series),
            (MetadataSource.AniDB, MetadataEntityType.Series),
        ]);

        Assert.True(scope.Sources.SetEquals([MetadataSource.AniDB, MetadataSource.TMDB]));
        Assert.Equal([MetadataEntityType.Series, MetadataEntityType.Movie], scope.GetEntityTypes(MetadataSource.TMDB).Order().ToList());
        Assert.Empty(scope.GetEntityTypes(TestSources.Plugin));

        Assert.Equal(scope.OrderBy(pair => pair.Source).ThenBy(pair => pair.EntityType).ToList(), scope.ToList());
    }

    [Fact]
    public void ItCombinesAndComparesAsASet()
    {
        var series = MetadataEntityScope.ForSources([MetadataSource.TMDB, MetadataSource.AniDB], [MetadataEntityType.Series]);
        var tmdb = MetadataEntityScope.ForSource(MetadataSource.TMDB, MetadataEntityType.Series, MetadataEntityType.Movie);

        Assert.Equal(3, series.Union(tmdb).Count);
        Assert.Equal(MetadataEntityScope.Single(MetadataSource.TMDB, MetadataEntityType.Series), series.Intersect(tmdb));
        Assert.Equal(MetadataEntityScope.Single(MetadataSource.AniDB, MetadataEntityType.Series), series.Except(tmdb));
        Assert.Equal(series, MetadataEntityScope.FromPairs(series.Reverse()));
        Assert.Equal(series.GetHashCode(), MetadataEntityScope.FromPairs(series.Reverse()).GetHashCode());
        Assert.NotEqual(series, tmdb);
    }
}
