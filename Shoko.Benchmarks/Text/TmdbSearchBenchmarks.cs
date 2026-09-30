using BenchmarkDotNet.Attributes;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Models.TMDB;
using Shoko.Server.Providers.TMDB;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

namespace Benchmarks.Text;

/// <summary>
///   The local TMDB show and movie search (<c>/api/v3/TMDB/Show?search=</c> and <c>/api/v3/TMDB/Movie?search=</c>),
///   with the title selector the controller uses, over the fixture's ten queries.
/// </summary>
/// <remarks>
///   The entities' titles come from memos the fixture seeds. On a cold server each entity's first read is a database
///   query, which only the live measurement covers.
/// </remarks>
[BenchmarkCategory("Text")]
[MemoryDiagnoser]
public class TmdbSearchBenchmarks
{
    private TextFixture _fixture = null!;

    private HashSet<TitleLanguage> _languages = null!;

    [GlobalSetup]
    public void Setup()
    {
        _fixture = TextFixture.Instance;
        _languages = ISettingsProvider.Instance.GetSettings().Language.DescriptionLanguageOrder
            .Select(language => language.GetTitleLanguage())
            .Concat([TitleLanguage.English])
            .ToHashSet();
    }

    [Benchmark]
    public int ShowTitleReads()
        => _fixture.TmdbShows.Sum(show => ShowTitles(show).Count);

    [Benchmark]
    public int ShowSearch()
        => _fixture.Queries.Sum(query => _fixture.TmdbShows.Search(query, ShowTitles, fuzzy: true).Count());

    [Benchmark]
    public int MovieSearch()
        => _fixture.Queries.Sum(query => _fixture.TmdbMovies.Search(query, MovieTitles, fuzzy: true).Count());

    private List<string> ShowTitles(TMDB_Show show)
        => show.GetAllTitles()
            .WhereInLanguages(_languages)
            .Select(title => title.Value)
            .Append(show.EnglishTitle)
            .Append(show.OriginalTitle)
            .Distinct()
            .ToList();

    private List<string> MovieTitles(TMDB_Movie movie)
        => movie.GetAllTitles()
            .WhereInLanguages(_languages)
            .Select(title => title.Value)
            .Append(movie.EnglishTitle)
            .Append(movie.OriginalTitle)
            .Distinct()
            .ToList();
}
