using BenchmarkDotNet.Attributes;
using Shoko.Server;

namespace Benchmarks;

/// <summary>
/// Tag filtering runs over every tag of every anime, so its throughput is worth watching.
/// </summary>
/// <remarks>
/// Was a <c>[Fact]</c> in <c>Shoko.Tests</c> asserting an average under 2000ms. That is a benchmark,
/// not a test, and on a shared CI runner it measured 2911ms and failed the suite.
/// </remarks>
[BenchmarkCategory("TagFilter")]
public class TagFilterBenchmarks
{
    private const TagFilter.Filter Filters =
        TagFilter.Filter.Genre | TagFilter.Filter.AnidbInternal | TagFilter.Filter.Programming | TagFilter.Filter.Misc;

    private static readonly string[] _tags =
    [
        "comedy", "Comedy", "horror", "18 restricted", "large breasts", "japan", "violence", "action", "romance",
        "school life", "seinen", "shounen", "asia", "contemporary fantasy", "earth", "afterlife", "alien",
        "angst", "ecchi", "gore", "themes", "elements", "origin", "setting", "manga", "new", "ugly",
    ];

    [Benchmark]
    public List<string> ProcessTags() => TagFilter.String.ProcessTags(Filters, _tags);
}
