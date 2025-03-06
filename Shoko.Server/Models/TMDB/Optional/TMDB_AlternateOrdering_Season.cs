using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using Shoko.Server.Extensions;
using TMDbLib.Objects.TvShows;

#nullable enable
namespace Shoko.Server.Models.TMDB;

// TODO Navigation properties
public class TMDB_AlternateOrdering_Season : TMDB_Base<string>
{
    public override string Id => TmdbEpisodeGroupID;

    /// <summary>
    /// Local ID.
    /// </summary>
    public int TMDB_AlternateOrdering_SeasonID { get; set; }

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
    /// Episode Group Season name.
    /// </summary>
    public string EnglishTitle { get; set; } = string.Empty;

    /// <summary>
    /// Overridden season number for alternate ordering.
    /// </summary>
    public int SeasonNumber { get; set; }

    /// <summary>
    /// Number of episodes within the alternate ordering season.
    /// </summary>
    public int EpisodeCount { get; set; }

    /// <summary>
    /// Number of episodes within the season that are hidden.
    /// </summary>
    public int HiddenEpisodeCount { get; set; }

    /// <summary>
    /// Indicates the alternate ordering season is locked.
    /// </summary>
    /// <remarks>
    /// Exactly what this 'locked' status indicates is yet to be determined.
    /// </remarks>
    public bool IsLocked { get; set; } = true;

    /// <summary>
    /// When the metadata was first downloaded.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When the metadata was last synchronized with the remote.
    /// </summary>
    public DateTime LastUpdatedAt { get; set; }

    
    public virtual TMDB_Show? TmdbShow { get; set; }
    public virtual TMDB_AlternateOrdering? TmdbAlternateOrdering { get; set; }
    public virtual ICollection<TMDB_AlternateOrdering_Episode> TmdbAlternateOrderingEpisodes { get; set; }

    public bool Populate(TvGroup episodeGroup, string collectionId, int showId, int seasonNumber)
    {
        var updates = new[]
        {
            UpdateProperty(TmdbShowID, showId, v => TmdbShowID = v),
            UpdateProperty(TmdbEpisodeGroupCollectionID, collectionId, v => TmdbEpisodeGroupCollectionID = v),
            UpdateProperty(EnglishTitle, episodeGroup.Name, v => EnglishTitle = v),
            UpdateProperty(SeasonNumber, seasonNumber, v => SeasonNumber = v),
            UpdateProperty(IsLocked, episodeGroup.Locked, v => IsLocked = v),
        };

        return updates.Any(updated => updated);
    }

    /// <summary>
    /// Get all cast members that have worked on this season.
    /// </summary>
    /// <returns>All cast members that have worked on this season.</returns>
    [NotMapped]
    public IReadOnlyList<TMDB_Season_Cast> Cast =>
        TmdbAlternateOrderingEpisodes
            .SelectMany(episode => episode.TmdbEpisode?.Cast ?? [])
            .WhereNotNull()
            .GroupBy(cast => new { cast.TmdbPersonID, cast.CharacterName, cast.IsGuestRole })
            .Select(group =>
            {
                var episodes = group.ToList();
                var firstEpisode = episodes.First();
                return new TMDB_Season_Cast()
                {
                    TmdbPersonID = firstEpisode.TmdbPersonID,
                    TmdbShowID = firstEpisode.TmdbShowID,
                    TmdbSeasonID = firstEpisode.TmdbSeasonID,
                    IsGuestRole = firstEpisode.IsGuestRole,
                    CharacterName = firstEpisode.CharacterName,
                    Ordering = firstEpisode.Ordering,
                    EpisodeCount = episodes.Count,
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
    public IReadOnlyList<TMDB_Season_Crew> Crew =>
        TmdbAlternateOrderingEpisodes
            .SelectMany(episode => episode.TmdbEpisode?.Crew ?? [])
            .WhereNotNull()
            .GroupBy(cast => new { cast.TmdbPersonID, cast.Department, cast.Job })
            .Select(group =>
            {
                var episodes = group.ToList();
                var firstEpisode = episodes.First();
                return new TMDB_Season_Crew()
                {
                    TmdbPersonID = firstEpisode.TmdbPersonID,
                    TmdbShowID = firstEpisode.TmdbShowID,
                    TmdbSeasonID = firstEpisode.TmdbSeasonID,
                    Department = firstEpisode.Department,
                    Job = firstEpisode.Job,
                    EpisodeCount = episodes.Count,
                };
            })
            .OrderBy(crew => crew.Department)
            .ThenBy(crew => crew.Job)
            .ThenBy(crew => crew.TmdbPersonID)
            .ToList();
}
