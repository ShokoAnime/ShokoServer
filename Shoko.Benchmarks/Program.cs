using BenchmarkDotNet.Running;
using Benchmarks;

BenchmarkSwitcher.FromTypes([typeof(AniDB_AnimeBenchmarks), typeof(TagFilterBenchmarks)]).RunAll();
