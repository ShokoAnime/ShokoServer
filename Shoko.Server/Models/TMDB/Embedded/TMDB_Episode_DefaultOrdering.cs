using Shoko.Abstractions.Metadata.Tmdb;
using Shoko.Server.Models.Metadata.Embedded;
using Shoko.Server.Services;

namespace Shoko.Server.Models.TMDB;

/// <summary>
///   A TMDB episode's place in the default ordering of its show: its own
///   season and number.
/// </summary>
/// <param name="episode">The episode.</param>
/// <param name="show">The episode's show, if it is available.</param>
/// <param name="service">The ordering service, which knows the choice, if there is one.</param>
public sealed class TMDB_Episode_DefaultOrdering(TMDB_Episode episode, TMDB_Show? show, MetadataOrderingService? service)
    : DefaultEpisodeOrdering(episode, show, service), ITmdbEpisodeOrderingInformation
{
    private readonly TMDB_Episode _episode = episode;

    private readonly TMDB_Show? _show = show;

    #region ITmdbEpisodeOrderingInformation Implementation

    /// <inheritdoc />
    public int TmdbShowID => _episode.TmdbShowID;

    /// <inheritdoc />
    public int TmdbEpisodeID => _episode.TmdbEpisodeID;

    ITmdbShow? ITmdbEpisodeOrderingInformation.Series => _show;

    ITmdbSeason? ITmdbEpisodeOrderingInformation.Season => _episode.TmdbSeason;

    ITmdbEpisode ITmdbEpisodeOrderingInformation.Episode => _episode;

    #endregion
}
