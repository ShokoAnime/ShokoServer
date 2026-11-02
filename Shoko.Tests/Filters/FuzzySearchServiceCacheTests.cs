using System.Collections.Generic;
using Shoko.Server.Filters;
using Xunit;

namespace Shoko.Tests.Filters;

/// <summary>
///   The fuzzy score cache keeps the most recently used queries only, so
///   searching for ever new text does not grow it without end.
/// </summary>
public class FuzzySearchServiceCacheTests
{
    private static readonly IReadOnlySet<string> _names = new HashSet<string> { "Shingeki no Kyojin", "Attack on Titan" };

    [Fact]
    public void OnlyTheMostRecentQueriesAreKept()
    {
        var service = new FuzzySearchService();
        for (var i = 0; i < FuzzySearchService.MaxCachedQueries * 3; i++)
            service.FuzzyScoreAnyName($"query {i}", _names);

        Assert.Equal(FuzzySearchService.MaxCachedQueries, service.CachedQueryCount);
    }

    [Fact]
    public void AScoreIsTheSameFromTheCacheAndAfterItWasDropped()
    {
        var service = new FuzzySearchService();
        var first = service.FuzzyScoreAnyName("attack", _names);
        Assert.NotNull(first);
        Assert.Equal(first, service.FuzzyScoreAnyName("attack", _names));

        for (var i = 0; i < FuzzySearchService.MaxCachedQueries; i++)
            service.FuzzyScoreAnyName($"other {i}", _names);

        Assert.Equal(first, service.FuzzyScoreAnyName("attack", _names));
        Assert.True(service.FuzzyMatchesAnyName("kyojin", _names));
        Assert.False(service.FuzzyMatchesAnyName("zzzzzz", _names));
    }

    [Fact]
    public void InvalidatingEmptiesTheCache()
    {
        var service = new FuzzySearchService();
        service.FuzzyScoreAnyName("attack", _names);

        service.InvalidateCache();

        Assert.Equal(0, service.CachedQueryCount);
    }
}
