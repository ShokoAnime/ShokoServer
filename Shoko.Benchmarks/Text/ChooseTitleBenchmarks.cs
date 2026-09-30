using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Core.Services;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Server.Models;

#pragma warning disable CS0618
namespace Benchmarks.Text;

/// <summary>
///   Choosing the preferred title and description of every series, episode, group and TMDB entity in the library.
/// </summary>
/// <remarks>
///   "Cold" drops the entry's memos first, as a text write or an import does; "Memoized" and the benchmarks without a
///   variant read them back once the first iteration has worked them out.
/// </remarks>
[BenchmarkCategory("Text")]
[MemoryDiagnoser]
public class ChooseTitleBenchmarks
{
    private TextFixture _fixture = null!;

    private IMetadataTextManager _textManager = null!;

    [GlobalSetup]
    public void Setup()
    {
        _fixture = TextFixture.Instance;
        _textManager = ISystemService.StaticServices.GetRequiredService<IMetadataTextManager>();
        _fixture.WarmSeries();
    }

    #region Series

    [Benchmark]
    public int SeriesPreferredTitleCold()
    {
        var count = 0;
        foreach (var series in _fixture.Series)
        {
            TextAccess.Forget(((IMetadata)series).ID);
            count += series.PreferredTitle?.Value.Length ?? 0;
        }

        return count;
    }

    [Benchmark]
    public int SeriesPreferredTitleMemoized()
        => _fixture.Series.Sum(series => series.PreferredTitle?.Value.Length ?? 0);

    [Benchmark]
    public int SeriesTitlesCold()
    {
        var count = 0;
        foreach (var series in _fixture.Series)
        {
            TextAccess.Forget(((IMetadata)series).ID);
            count += series.Titles.Count;
        }

        return count;
    }

    [Benchmark]
    public int SeriesPreferredDescriptionCold()
    {
        var count = 0;
        foreach (var series in _fixture.Series)
        {
            TextAccess.Forget(((IMetadata)series).ID);
            count += series.PreferredOverview?.Value.Length ?? 0;
        }

        return count;
    }

    [Benchmark]
    public int SeriesPreferredDescriptionMemoized()
        => _fixture.Series.Sum(series => series.PreferredOverview?.Value.Length ?? 0);

    /// <summary>
    ///   The generic chooser plugins call, over each series' full title list.
    /// </summary>
    [Benchmark]
    public int ChoosePreferredTitleFromList()
        => _fixture.Series.Sum(series => _textManager.ChoosePreferredTitle(series.Titles)?.Value.Length ?? 0);

    #endregion

    #region Episodes

    [Benchmark]
    public int EpisodePreferredTitle()
        => _fixture.Episodes.Sum(episode => ((IWithTitles)episode).PreferredTitle?.Value.Length ?? 0);

    [Benchmark]
    public int EpisodePreferredTitleCold()
    {
        var count = 0;
        foreach (var episode in _fixture.Episodes)
        {
            TextAccess.Forget(((IMetadata)episode).ID);
            count += ((IWithTitles)episode).PreferredTitle?.Value.Length ?? 0;
        }

        return count;
    }

    [Benchmark]
    public int EpisodePreferredDescription()
        => _fixture.Episodes.Sum(episode => ((IWithOverviews)episode).PreferredOverview?.Value.Length ?? 0);

    [Benchmark]
    public int EpisodePreferredDescriptionCold()
    {
        var count = 0;
        foreach (var episode in _fixture.Episodes)
        {
            TextAccess.Forget(((IMetadata)episode).ID);
            count += ((IWithOverviews)episode).PreferredOverview?.Value.Length ?? 0;
        }

        return count;
    }

    [Benchmark]
    public int EpisodeTitles()
        => _fixture.Episodes.Sum(episode => ((IWithTitles)episode).Titles.Count);

    #endregion

    #region Groups

    [Benchmark]
    public int GroupPreferredTitle()
        => _fixture.Groups.Sum(group => ((IWithTitles)group).PreferredTitle?.Value.Length ?? 0);

    [Benchmark]
    public int GroupTitles()
        => _fixture.Groups.Sum(group => ((IWithTitles)group).Titles.Count);

    [Benchmark]
    public int GroupPreferredDescription()
        => _fixture.Groups.Sum(group => ((IWithOverviews)group).PreferredOverview?.Value.Length ?? 0);

    #endregion

    #region TMDB

    [Benchmark]
    public int TmdbShowPreferredTitleAndOverview()
        => _fixture.TmdbShows.Sum(Chosen);

    [Benchmark]
    public int TmdbEpisodePreferredTitleAndOverview()
        => _fixture.TmdbEpisodes.Sum(Chosen);

    [Benchmark]
    public int TmdbMoviePreferredTitleAndOverview()
        => _fixture.TmdbMovies.Sum(Chosen);

    private static int Chosen<T>(T entity) where T : IWithTitles, IWithOverviews
        => (entity.PreferredTitle?.Value.Length ?? 0) + (entity.PreferredOverview?.Value.Length ?? 0);

    #endregion
}
