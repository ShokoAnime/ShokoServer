using BenchmarkDotNet.Attributes;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Providers.AniDB.HTTP;
using Shoko.Server.Providers.AniDB.HTTP.GetAnime;

namespace Benchmarks.Text;

/// <summary>
///   Writing an AniDB anime's titles on import: the diff <c>AnimeCreator</c> runs against the stored titles, and
///   the write through the text store, for every anime in the library, with the writes landing in the cache.
/// </summary>
/// <remarks>
///   The TMDB side (<c>TmdbMetadataUpdater.UpdateTitlesAndOverviewsWithTuple</c>) reads and writes its rows through
///   direct repositories, so it cannot run without a database and has no baseline here.
/// </remarks>
[BenchmarkCategory("Text")]
[MemoryDiagnoser]
public class SetTitlesBenchmarks
{
    private TextFixture _fixture = null!;

    private List<ResponseTitle>[] _current = [];

    private List<ResponseTitle>[] _renamed = [];

    private bool _flip;

    private bool CreateTitles(List<ResponseTitle> titles, AniDB_Anime anime)
        => AnimeCreator.StoreTitles(_fixture.TextStore, titles, anime, [], []);

    [GlobalSetup]
    public void Setup()
    {
        _fixture = TextFixture.Instance;
        _current = [.. _fixture.Anime.Select(anime => _fixture.ResponseTitlesFor(anime.AnimeID, variant: false))];
        _renamed = [.. _fixture.Anime.Select(anime => _fixture.ResponseTitlesFor(anime.AnimeID, variant: true))];
    }

    /// <summary>
    ///   A refresh where nothing changed, the common case.
    /// </summary>
    [Benchmark]
    public int AnidbAnimeDiffUnchanged()
    {
        var changed = 0;
        for (var i = 0; i < _fixture.Anime.Count; i++)
            changed += CreateTitles(_current[i], _fixture.Anime[i]) ? 1 : 0;

        return changed;
    }

    /// <summary>
    ///   A refresh where one title of every anime was renamed: one row removed and one added each, alternating
    ///   between the two spellings so every call changes something.
    /// </summary>
    [Benchmark]
    public int AnidbAnimeDiffOneRenamed()
    {
        _flip = !_flip;
        var titles = _flip ? _renamed : _current;
        var changed = 0;
        for (var i = 0; i < _fixture.Anime.Count; i++)
            changed += CreateTitles(titles[i], _fixture.Anime[i]) ? 1 : 0;

        return changed;
    }
}
