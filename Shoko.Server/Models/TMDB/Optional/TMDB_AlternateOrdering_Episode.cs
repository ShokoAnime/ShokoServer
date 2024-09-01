using System;
using System.Linq;
using Shoko.Server.Repositories;

#nullable enable
namespace Shoko.Server.Models.TMDB;

public class TMDB_AlternateOrdering_Episode : TMDB_Base<string>
{
    #region Properties

    public override string Id => $"{TmdbEpisodeGroupID}:{TmdbEpisodeID}";

    /// <summary>
    /// Local ID.
    /// </summary>
    public int TMDB_AlternateOrdering_EpisodeID { get; set; }

    /// <summary>
    /// TMDB Show ID.
    /// </summary>
    public int TmdbShowID { get; set; }

    /// <summary>
    /// TMDB Episode Group Collection ID.
    /// </summary>
    public string TmdbEpisodeGroupCollectionID { get; set; } = string.Empty;

    /// <summary>
    /// TMDB Episode Group ID.
    /// </summary>
    public string TmdbEpisodeGroupID { get; set; } = string.Empty;

    /// <summary>
    /// TMDB Episode ID.
    /// </summary>
    public int TmdbEpisodeID { get; set; }

    /// <summary>
    /// Overridden season number for alternate ordering.
    /// </summary>
    public int SeasonNumber { get; set; }

    /// <summary>
    /// Overridden episode number for alternate ordering.
    /// </summary>
    /// <value></value>
    public int EpisodeNumber { get; set; }

    /// <summary>
    /// When the metadata was first downloaded.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When the metadata was last synchronized with the remote.
    /// </summary>
    public DateTime LastUpdatedAt { get; set; }

    #endregion
    #region Constructors

    public TMDB_AlternateOrdering_Episode() { }

    public TMDB_AlternateOrdering_Episode(string episodeGroupId, int episodeId)
    {
        TmdbEpisodeGroupID = episodeGroupId;
        TmdbEpisodeID = episodeId;
        CreatedAt = DateTime.Now;
        LastUpdatedAt = CreatedAt;
    }

    #endregion
    #region Methods

    public bool Populate(string collectionId, int showId, int seasonNumber, int episodeNumber)
    {
        var updates = new[]
        {
            UpdateProperty(TmdbShowID, showId, v => TmdbShowID = v),
            UpdateProperty(TmdbEpisodeGroupCollectionID, collectionId, v => TmdbEpisodeGroupCollectionID = v),
            UpdateProperty(SeasonNumber, seasonNumber, v => SeasonNumber = v),
            UpdateProperty(EpisodeNumber, episodeNumber, v => EpisodeNumber = v),
        };

        return updates.Any(updated => updated);
    }

    public TMDB_Show? TmdbShow =>
        RepoFactory.TMDB_Show.GetByTmdbShowID(TmdbShowID);

    public TMDB_AlternateOrdering? TmdbAlternateOrdering =>
        RepoFactory.TMDB_AlternateOrdering.GetByTmdbEpisodeGroupCollectionID(TmdbEpisodeGroupCollectionID);

    public TMDB_AlternateOrdering_Season? TmdbAlternateOrderingSeason =>
        RepoFactory.TMDB_AlternateOrdering_Season.GetByTmdbEpisodeGroupID(TmdbEpisodeGroupID);

    public TMDB_Episode? TmdbEpisode =>
        RepoFactory.TMDB_Episode.GetByTmdbEpisodeID(TmdbEpisodeID);

    #endregion
}
