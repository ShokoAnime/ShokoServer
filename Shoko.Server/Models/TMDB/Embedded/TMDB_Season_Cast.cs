
#nullable enable
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Server.Repositories;

namespace Shoko.Server.Models.TMDB;

/// <summary>
/// Cast member within a season.
/// </summary>
public class TMDB_Season_Cast : TMDB_Cast
{
    /// <summary>
    /// TMDB Show ID for the show this role belongs to.
    /// </summary>
    public int TmdbShowID { get; set; }

    /// <summary>
    /// TMDB Show ID for the season this role belongs to.
    /// </summary>
    public int TmdbSeasonID { get; set; }

    /// <inheritdoc/>
    public override int TmdbParentID => TmdbSeasonID;

    /// <summary>
    /// Indicates the role is not a recurring role within the season.
    /// </summary>
    public bool IsGuestRole { get; set; }

    /// <summary>
    /// Number of episodes within this season the cast member have worked on.
    /// </summary>
    public int EpisodeCount { get; set; }

    public TMDB_Season? GetTmdbSeason() => RepoFactory.TMDB_Season.GetByTmdbSeasonID(TmdbSeasonID);

    public override IMetadata<int>? GetTmdbParent() => GetTmdbSeason();
}
