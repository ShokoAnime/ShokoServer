using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Server.Repositories;

#nullable enable
namespace Shoko.Server.Models.TMDB;

/// <summary>
/// Cast member for an episode.
/// </summary>
public class TMDB_Episode_Cast : TMDB_Cast, ICast<IEpisode>
{
    #region Properties

    /// <summary>
    /// Local ID.
    /// </summary>
    public int TMDB_Episode_CastID { get; set; }

    /// <summary>
    /// TMDB Show ID for the show this role belongs to.
    /// </summary>
    public int TmdbShowID { get; set; }

    /// <summary>
    /// TMDB Show ID for the season this role belongs to.
    /// </summary>
    public int TmdbSeasonID { get; set; }

    /// <summary>
    /// TMDB Episode ID for the episode this role belongs to.
    /// </summary>
    public int TmdbEpisodeID { get; set; }

    /// <inheritdoc />
    public override int TmdbParentID => TmdbEpisodeID;

    /// <summary>
    /// Indicates the role is not a recurring role within the season.
    /// </summary>
    public bool IsGuestRole { get; set; }

    #endregion

    #region Methods

    public virtual TMDB_Episode? Episode { get; set; }

    public override IMetadata<int>? GetTmdbParent() =>
        Episode;

    #endregion

    #region ICast Implementation

    IEpisode? ICast<IEpisode>.ParentOfType => Episode;

    #endregion
}
