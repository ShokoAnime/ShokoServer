using BenchmarkDotNet.Running;
using Benchmarks;
using Benchmarks.Text;

// "--fixture" prints the text fixture's row counts and queries instead of running anything.
if (args is ["--fixture"])
{
    Console.WriteLine(TextFixture.Instance.Describe());
    return;
}

var switcher = BenchmarkSwitcher.FromTypes(
[
    typeof(AniDB_AnimeBenchmarks),
    typeof(TagFilterBenchmarks),
    typeof(TextCacheLoadBenchmarks),
    typeof(ChooseTitleBenchmarks),
    typeof(FilterNamesBenchmarks),
    typeof(SeriesSearchBenchmarks),
    typeof(TmdbSearchBenchmarks),
    typeof(SetTitlesBenchmarks),
]);

// With no arguments everything runs; with arguments (e.g. "--filter *Text*") the switcher picks.
if (args.Length is 0)
    switcher.RunAll();
else
    switcher.Run(args);
