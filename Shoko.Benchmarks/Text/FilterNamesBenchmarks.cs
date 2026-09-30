using BenchmarkDotNet.Attributes;
using Shoko.Abstractions.Filtering.Expressions.Info;
using Shoko.Server.Filters;

namespace Benchmarks.Text;

/// <summary>
///   The name and description sets filters read, and a fuzzy name filter over every series and group.
/// </summary>
/// <remarks>
///   The filterables are created per evaluation, as <c>FilteringEngine</c> does per request. Today the name sets are
///   rebuilt on every access, so the fuzzy score cache (keyed by the set instance) never hits across evaluations.
/// </remarks>
[BenchmarkCategory("Text")]
[MemoryDiagnoser]
public class FilterNamesBenchmarks
{
    private TextFixture _fixture = null!;

    private DateTime _now;

    private HasFuzzyNameExpression _fuzzy = null!;

    [GlobalSetup]
    public void Setup()
    {
        _fixture = TextFixture.Instance;
        _now = DateTime.Now;
        _fuzzy = new(_fixture.Queries[3]);
        _fixture.WarmSeries();
    }

    [Benchmark]
    public int SeriesNames()
        => _fixture.Series.Sum(series => new FilterableAnimeSeries(series, _now).Names.Count);

    [Benchmark]
    public int SeriesPreferredNames()
        => _fixture.Series.Sum(series => new FilterableAnimeSeries(series, _now).PreferredNames.Count);

    [Benchmark]
    public int SeriesDescriptions()
        => _fixture.Series.Sum(series => new FilterableAnimeSeries(series, _now).Descriptions.Count);

    [Benchmark]
    public int GroupNames()
        => _fixture.Groups.Sum(group => new FilterableAnimeGroup(group, _now).Names.Count);

    [Benchmark]
    public int SeriesFuzzyNameFilter()
        => _fixture.Series.Count(series => _fuzzy.Evaluate(new FilterableAnimeSeries(series, _now), null, _now));

    [Benchmark]
    public int GroupFuzzyNameFilter()
        => _fixture.Groups.Count(group => _fuzzy.Evaluate(new FilterableAnimeGroup(group, _now), null, _now));
}
