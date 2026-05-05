using System;
using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Server.API.v3.Helpers;
using Shoko.Server.API.v3.Models.AniDB;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.Extensions;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories;

#nullable enable
namespace Shoko.Server.API.v3.Models.Shoko;

public static class Dashboard
{
    public class CollectionStats
    {
        /// <summary>
        /// Number of Files in the collection (visible to the current user)
        /// </summary>
        [Required]
        public int FileCount { get; set; }

        /// <summary>
        /// Number of Series in the Collection (visible to the current user)
        /// </summary>
        [Required]
        public int SeriesCount { get; set; }

        /// <summary>
        /// The number of Groups in the Collection (visible to the current user)
        /// </summary>
        [Required]
        public int GroupCount { get; set; }

        /// <summary>
        /// Total amount of space the collection takes (of what's visible to the current user)
        /// </summary>
        [Required]
        public long FileSize { get; set; }

        /// <summary>
        /// Number of Series Completely Watched
        /// </summary>
        [Required]
        public int FinishedSeries { get; set; }

        /// <summary>
        /// Number of Episodes Watched
        /// </summary>
        [Required]
        public int WatchedEpisodes { get; set; }

        /// <summary>
        /// Watched Hours, rounded to one place
        /// </summary>
        [Required]
        public decimal WatchedHours { get; set; }

        /// <summary>
        /// The percentage of files that are either duplicates or belong to the same episode
        /// </summary>
        [Required]
        public decimal PercentDuplicate { get; set; }

        /// <summary>
        /// The Number of missing episodes, regardless of where they are from or available
        /// </summary>
        [Required]
        public int MissingEpisodes { get; set; }

        /// <summary>
        /// The number of missing episodes from groups we are collecting. This should not be used as a rule, as it's not very reliable
        /// </summary>
        [Required]
        public int MissingEpisodesCollecting { get; set; }

        /// <summary>
        /// Number of Unrecognized Files
        /// </summary>
        [Required]
        public int UnrecognizedFiles { get; set; }

        /// <summary>
        /// The number of series missing TMDB Links
        /// </summary>
        [Required]
        public int SeriesWithMissingLinks { get; set; }

        /// <summary>
        /// The number of Episodes with more than one File (not marked as a variation)
        /// </summary>
        [Required]
        public int EpisodesWithMultipleFiles { get; set; }

        /// <summary>
        /// The number of files that exist in more than one location
        /// </summary>
        [Required]
        public int FilesWithDuplicateLocations { get; set; }
    }

    public class SeriesSummary
    {
        /// <summary>
        /// The number of normal Series
        /// </summary>
        [Required]
        public int Series { get; set; }

        /// <summary>
        /// The Number of OVAs
        /// </summary>
        [Required]
        public int OVA { get; set; }

        /// <summary>
        /// The Number of Movies
        /// </summary>
        [Required]
        public int Movie { get; set; }

        /// <summary>
        /// The Number of TV Specials
        /// </summary>
        [Required]
        public int Special { get; set; }

        /// <summary>
        /// ONAs and the like, it's more of a new concept
        /// </summary>
        [Required]
        public int Web { get; set; }

        /// <summary>
        /// Things marked on AniDB as Other, different from None
        /// </summary>
        [Required]
        public int Other { get; set; }

        /// <summary>
        /// The Number of Music Videos
        /// </summary>
        [Required]
        public int MusicVideo { get; set; }

        /// <summary>
        /// The entry have not yet been assigned a type.
        /// </summary>
        [Required]
        public int Unknown { get; set; }

        /// <summary>
        /// Series that don't have AniDB Records. This is very bad, and usually means there was an error in the import process. It can also happen if the API is hit at just the right time.
        /// </summary>
        [Required]
        public int None { get; set; }
    }

    /// <summary>
    /// Episode details for displaying on the dashboard.
    /// </summary>
    public class Episode
    {
        public Episode(AniDB_Episode episode, AniDB_Anime anime, AnimeSeries? series = null,
            VideoLocal? file = null, VideoLocal_User? userRecord = null)
        {
            IDs = new EpisodeDetailsIDs()
            {
                ID = episode.EpisodeID,
                Series = anime.AnimeID,
                ShokoFile = file?.VideoLocalID,
                ShokoSeries = series?.AnimeSeriesID,
                ShokoEpisode = series != null
                    ? RepoFactory.AnimeEpisode.GetByAniDBEpisodeID(episode.EpisodeID)?.AnimeEpisodeID
                    : null
            };
            Title = episode.Title;
            Number = episode.EpisodeNumber;
            Type = episode.EpisodeType.ToV3Dto();
            AirDate = episode.GetAirDateAsDate()?.ToDateOnly();
            Duration = file?.DurationTimeSpan ?? new TimeSpan(0, 0, episode.LengthSeconds);
            ResumePosition = userRecord?.ProgressPosition;
            Watched = userRecord?.WatchedDate?.ToUniversalTime();
            SeriesTitle = series?.Title ?? anime.Title;
            SeriesPoster = new Image(anime.PreferredOrDefaultPoster);
            Thumbnail = episode.PreferredOrDefaultThumbnail is { } image ? new Image(image) : null;
        }

        /// <summary>
        /// All ids that may be useful for navigating away from the dashboard.
        /// </summary>
        [Required]
        public EpisodeDetailsIDs IDs { get; set; }

        /// <summary>
        /// Episode title.
        /// </summary>
        [Required]
        public string Title { get; set; }

        /// <summary>
        /// Episode number.
        /// </summary>
        [Required]
        public int Number { get; set; }

        /// <summary>
        /// Episode type.
        /// </summary>
        [Required, JsonConverter(typeof(StringEnumConverter))]
        public EpisodeType Type { get; set; }

        /// <summary>
        /// Air Date.
        /// </summary>
        /// <value></value>
        public DateOnly? AirDate { get; set; }

        /// <summary>
        /// The duration of the episode.
        /// </summary>
        [Required]
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// Where to resume the next playback.
        /// </summary>
        public TimeSpan? ResumePosition { get; set; }

        /// <summary>
        /// If the file/episode is considered watched.
        /// </summary>
        [JsonConverter(typeof(IsoDateTimeConverter))]
        public DateTime? Watched { get; set; }

        /// <summary>
        /// Series title.
        /// </summary>
        [Required]
        public string SeriesTitle { get; set; }

        /// <summary>
        /// Series poster.
        /// </summary>
        [Required]
        public Image SeriesPoster { get; set; }

        /// <summary>
        /// Episode thumbnail.
        /// </summary>
        public Image? Thumbnail { get; set; }
    }

    /// <summary>
    /// Object holding ids related to the episode.
    /// </summary>
    public class EpisodeDetailsIDs : IDs
    {
        /// <summary>
        /// The related <see cref="AnidbEpisode"/> id for the entry.
        /// </summary>
        public new int ID { get; set; }

        /// <summary>
        /// The related <see cref="Series.AniDB"/> id for the entry.
        /// </summary>
        [Required]
        public int Series { get; set; }

        /// <summary>
        /// The related Shoko <see cref="File"/> id if a file is available
        /// and/or appropriate.
        /// </summary>
        public int? ShokoFile { get; set; }

        /// <summary>
        /// The related Shoko <see cref="Shoko.Episode"/> id if the episode is
        /// available locally.
        /// </summary>
        public int? ShokoEpisode { get; set; }

        /// <summary>
        /// The related Shoko <see cref="Shoko.Series"/> id if the series is
        /// available locally.
        /// </summary>
        public int? ShokoSeries { get; set; }
    }
}
