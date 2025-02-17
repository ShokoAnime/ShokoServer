using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using Shoko.Server.Extensions;
using Shoko.Server.Providers.TMDB;
using TMDbLib.Objects.TvShows;

#nullable enable
namespace Shoko.Server.Models.TMDB;

/// <summary>
/// Alternate Season and Episode ordering using TMDB's "Episode Group" feature.
/// Note: don't ask me why they called it that.
/// </summary>
// TODO Navigation properties
public class TMDB_AlternateOrdering : TMDB_Base<string>
{
    #region Properties

    public override string Id => TmdbEpisodeGroupCollectionID;

    /// <summary>
    /// Local ID.
    /// </summary>
    public int TMDB_AlternateOrderingID { get; set; }

    /// <summary>
    /// TMDB Show ID.
    /// </summary>
    public int TmdbShowID { get; set; }

    /// <summary>
    /// TMDB Network ID.
    /// </summary>
    /// <remarks>
    /// It may be null if the group is not tied to a network.
    /// </remarks>
    public int? TmdbNetworkID { get; set; }

    /// <summary>
    /// TMDB Episode Group Collection ID.
    /// </summary>
    public string TmdbEpisodeGroupCollectionID { get; set; } = string.Empty;

    /// <summary>
    /// The name of the alternate ordering scheme.
    /// </summary>
    public string EnglishTitle { get; set; } = string.Empty;

    /// <summary>
    /// A short overview about what the scheme entails.
    /// </summary>
    public string EnglishOverview { get; set; } = string.Empty;

    /// <summary>
    /// Number of episodes within the episode group.
    /// </summary>
    public int EpisodeCount { get; set; }

    /// <summary>
    /// Number of episodes within the season that are hidden.
    /// </summary>
    public int HiddenEpisodeCount { get; set; }

    /// <summary>
    /// Number of seasons within the episode group.
    /// </summary>
    public int SeasonCount { get; set; }

    /// <summary>
    /// 
    /// </summary>
    public AlternateOrderingType Type { get; set; }

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

    public TMDB_AlternateOrdering() { }

    public TMDB_AlternateOrdering(string collectionId)
    {
        TmdbEpisodeGroupCollectionID = collectionId;
        CreatedAt = DateTime.Now;
        LastUpdatedAt = CreatedAt;
    }

    #endregion
    #region Methods

    public bool Populate(TvGroupCollection collection, int showId)
    {
        var updates = new[]
        {
            UpdateProperty(TmdbShowID, showId, v => TmdbShowID = v),
            UpdateProperty(TmdbNetworkID, collection.Network?.Id, v => TmdbNetworkID = v),
            UpdateProperty(EnglishTitle, collection.Name, v => EnglishTitle = v),
            UpdateProperty(EnglishOverview, collection.Description, v => EnglishOverview = v),
            UpdateProperty(SeasonCount, collection.GroupCount, v => SeasonCount = v),
            UpdateProperty(Type, Enum.Parse<AlternateOrderingType>(collection.Type.ToString()), v => Type = v),
        };

        return updates.Any(updated => updated);
    }

    /// <summary>
    /// Get all cast members that have worked on this season.
    /// </summary>
    /// <returns>All cast members that have worked on this season.</returns>
    [NotMapped]
    public IReadOnlyList<TMDB_Show_Cast> Cast =>
        TmdbAlternateOrderingEpisodes
            .SelectMany(episode => episode.TmdbEpisode?.Cast ?? [])
            .WhereNotNull()
            .GroupBy(cast => new { cast.TmdbPersonID, cast.CharacterName, cast.IsGuestRole })
            .Select(group =>
            {
                var episodes = group.ToList();
                var firstEpisode = episodes.First();
                var seasonCount = episodes.GroupBy(a => a.TmdbSeasonID).Count();
                return new TMDB_Show_Cast()
                {
                    TmdbPersonID = firstEpisode.TmdbPersonID,
                    TmdbShowID = firstEpisode.TmdbShowID,
                    CharacterName = firstEpisode.CharacterName,
                    Ordering = firstEpisode.Ordering,
                    EpisodeCount = episodes.Count,
                    SeasonCount = seasonCount,
                };
            })
            .OrderBy(crew => crew.Ordering)
            .ThenBy(crew => crew.TmdbPersonID)
            .ToList();

    /// <summary>
    /// Get all crew members that have worked on this season.
    /// </summary>
    /// <returns>All crew members that have worked on this season.</returns>
    [NotMapped]
    public IReadOnlyList<TMDB_Show_Crew> Crew =>
        TmdbAlternateOrderingEpisodes
            .Select(episode => episode.TmdbEpisode?.Crew)
            .WhereNotNull()
            .SelectMany(list => list)
            .GroupBy(cast => new { cast.TmdbPersonID, cast.Department, cast.Job })
            .Select(group =>
            {
                var episodes = group.ToList();
                var firstEpisode = episodes.First();
                var seasonCount = episodes.GroupBy(a => a.TmdbSeasonID).Count();
                return new TMDB_Show_Crew()
                {
                    TmdbPersonID = firstEpisode.TmdbPersonID,
                    TmdbShowID = firstEpisode.TmdbShowID,
                    Department = firstEpisode.Department,
                    Job = firstEpisode.Job,
                    EpisodeCount = episodes.Count,
                    SeasonCount = seasonCount,
                };
            })
            .OrderBy(crew => crew.Department)
            .ThenBy(crew => crew.Job)
            .ThenBy(crew => crew.TmdbPersonID)
            .ToList();

    public virtual TMDB_Show? TmdbShow { get; set; }
    public virtual IEnumerable<TMDB_AlternateOrdering_Season> TmdbAlternateOrderingSeasons { get; set; }
    public virtual IEnumerable<TMDB_AlternateOrdering_Episode> TmdbAlternateOrderingEpisodes { get; set; }

    #endregion
}
