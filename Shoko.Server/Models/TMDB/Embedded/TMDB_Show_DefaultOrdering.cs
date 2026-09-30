using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata.Tmdb;
using Shoko.Server.Models.Metadata.Embedded;
using Shoko.Server.Services;

namespace Shoko.Server.Models.TMDB;

/// <summary>
///   A TMDB show's default ordering, made from its own seasons and never
///   stored. Its ID is <c>tmdb://ordering/&lt;show id&gt;</c>.
/// </summary>
/// <param name="show">The show.</param>
/// <param name="service">The ordering service, which knows the choice and the hidden episodes, if there is one.</param>
public sealed class TMDB_Show_DefaultOrdering(TMDB_Show show, MetadataOrderingService? service) : DefaultOrdering(show, service), ITmdbShowOrderingInformation
{
    private readonly TMDB_Show _show = show;

    #region ITmdbShowOrderingInformation Implementation

    /// <inheritdoc />
    public int TmdbShowID => _show.TmdbShowID;

    ITmdbShow? ITmdbShowOrderingInformation.Series => _show;

    IReadOnlyList<ITmdbSeason> ITmdbShowOrderingInformation.Seasons => _show.TmdbSeasons;

    IReadOnlyList<ITmdbEpisode> ITmdbShowOrderingInformation.Episodes => [.. Episodes.OfType<ITmdbEpisode>()];

    #endregion
}
