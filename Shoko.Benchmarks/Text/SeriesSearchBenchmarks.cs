using BenchmarkDotNet.Attributes;
using Shoko.Server.Services;
using Shoko.Server.Utilities;

namespace Benchmarks.Text;

/// <summary>
///   <see cref="SeriesSearch"/>: building its index over the library and running the fixture's ten queries, plus the
///   main/official title match the file endpoints filter with.
/// </summary>
[BenchmarkCategory("Text")]
[MemoryDiagnoser]
public class SeriesSearchBenchmarks
{
    private const SeriesSearch.SearchFlags Fuzzy = SeriesSearch.SearchFlags.Titles | SeriesSearch.SearchFlags.Fuzzy;

    private TextFixture _fixture = null!;

    private AnidbTitleSearch _titleSearch = null!;

    [GlobalSetup]
    public void Setup()
    {
        _fixture = TextFixture.Instance;
        _titleSearch = new(_fixture.TextStore.Cache);
        _fixture.WarmSeries();
        SeriesSearch.MarkDirty();
        SeriesSearch.SearchSeries(_fixture.User, _fixture.Queries[0], 50, Fuzzy);
    }

    /// <summary>
    ///   A rebuild after any series save, measured through the first query that needs it.
    /// </summary>
    [Benchmark]
    public int BuildIndex()
    {
        SeriesSearch.MarkDirty();
        return SeriesSearch.SearchSeries(_fixture.User, _fixture.Queries[0], 1, Fuzzy).Count;
    }

    [Benchmark]
    public int TenFuzzyQueries()
        => _fixture.Queries.Sum(query => SeriesSearch.SearchSeries(_fixture.User, query, 50, Fuzzy).Count);

    [Benchmark]
    public int TenExactQueries()
        => _fixture.Queries.Sum(query => SeriesSearch.SearchSeries(_fixture.User, query, 50, SeriesSearch.SearchFlags.Titles).Count);

    [Benchmark]
    public int TenMainTitleMatches()
        => _fixture.Queries.Sum(query =>
        {
            var normalized = AnidbTitleSearch.NormalizeForSearch(query);
            return _fixture.Anime.Count(anime => _titleSearch.AnimeMatchesSearch(anime.AnimeID, normalized));
        });
}
