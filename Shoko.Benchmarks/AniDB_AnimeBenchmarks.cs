using BenchmarkDotNet.Attributes;
using Shoko.Abstractions.Enums;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Repositories.Cached;
using Shoko.TestData;

namespace Benchmarks;

/// <summary>
/// This exists to see how necessary it is to cache various data
/// </summary>
[BenchmarkCategory("AniDB_Anime")]
public class AniDB_AnimeBenchmarks
{
    private readonly AniDB_Anime[] _anime = TestData.AniDB_Anime.Value.Where(a => a.AirDate != null).ToArray();

    [Benchmark]
    public List<(int Year, YearlySeason Season)> GetAllSeasons()
    {
        return AnimeSeriesRepository.GetAllSeasons(_anime).ToList();
    }

    [Benchmark]
    public List<string> GetAllTags()
    {
        return _anime.SelectMany(a => a.AllTags.Split('|').Select(b => b.Trim().Replace('`', '\''))).ToList();
    }

    [Benchmark]
    public Dictionary<int, string[]> GetAllTitles()
    {
        return _anime.Select(a => (a.AnimeID, a.AllTags.Split('|').Select(b => b.Trim().Replace('`', '\'')).ToArray()))
            .ToDictionary(a => a.AnimeID, a => a.Item2);
    }
}
