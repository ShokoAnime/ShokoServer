using Shoko.Plugin.Abstractions.DataModels;

#nullable enable
namespace Shoko.Server.Models.TMDB;

/// <summary>
/// Crew member for an episode.
/// </summary>
public class TMDB_Episode_Crew : TMDB_Crew, ICrew<IEpisode>
{
    #region Properties

    /// <summary>
    ///  Local ID.
    /// </summary>
    public int TMDB_Episode_CrewID { get; set; }

    /// <summary>
    /// TMDB Show ID for the show this job belongs to.
    /// </summary>
    public int TmdbShowID { get; set; }

    /// <summary>
    /// TMDB Show ID for the season this job belongs to.
    /// </summary>
    public int TmdbSeasonID { get; set; }

    /// <summary>
    /// TMDB Episode ID for the episode this job belongs to.
    /// </summary>
    public int TmdbEpisodeID { get; set; }

    /// <inheritdoc />
    public override int TmdbParentID => TmdbEpisodeID;

    #endregion

    #region Methods

    public virtual TMDB_Episode? Episode { get; set; }

    public override IMetadata<int>? GetTmdbParent() => Episode;

    #endregion

    #region ICrew Implementation

    IEpisode? ICrew<IEpisode>.ParentOfType => Episode;

    #endregion
}
