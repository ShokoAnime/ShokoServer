using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Server.API.v3.Helpers;
using Shoko.Server.API.v3.Models.AniDB;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.Extensions;
using Shoko.Server.Models;
using Shoko.Server.Repositories;

namespace Shoko.Server.API.v3.Models.Shoko;

public static class Dashboard
{
    public class CollectionStats
    {
        /// <summary>
        /// Number of Files in the collection (visible to the current user)
        /// </summary>
        public int FileCount { get; set; }

        /// <summary>
        /// Number of Series in the Collection (visible to the current user)
        /// </summary>
        public int SeriesCount { get; set; }

        /// <summary>
        /// The number of Groups in the Collection (visible to the current user)
        /// </summary>
        public int GroupCount { get; set; }

        /// <summary>
        /// Total amount of space the collection takes (of what's visible to the current user)
        /// </summary>
        public long FileSize { get; set; }

        /// <summary>
        /// Number of Series Completely Watched
        /// </summary>
        public int FinishedSeries { get; set; }

        /// <summary>
        /// Number of Episodes Watched
        /// </summary>
        public int WatchedEpisodes { get; set; }

        /// <summary>
        /// Watched Hours, rounded to one place
        /// </summary>
        public decimal WatchedHours { get; set; }

        /// <summary>
        /// The percentage of files that are either duplicates or belong to the same episode
        /// </summary>
        public decimal PercentDuplicate { get; set; }

        /// <summary>
        /// The Number of missing episodes, regardless of where they are from or available
        /// </summary>
        public int MissingEpisodes { get; set; }

        /// <summary>
        /// The number of missing episodes from groups we are collecting. This should not be used as a rule, as it's not very reliable
        /// </summary>
        public int MissingEpisodesCollecting { get; set; }

        /// <summary>
        /// Number of Unrecognized Files
        /// </summary>
        public int UnrecognizedFiles { get; set; }

        /// <summary>
        /// The number of series missing TMDB Links
        /// </summary>
        public int SeriesWithMissingLinks { get; set; }

        /// <summary>
        /// The number of Episodes with more than one File (not marked as a variation)
        /// </summary>
        public int EpisodesWithMultipleFiles { get; set; }

        /// <summary>
        /// The number of files that exist in more than one location
        /// </summary>
        public int FilesWithDuplicateLocations { get; set; }
    }

    public class SeriesSummary
    {
        /// <summary>
        /// The number of normal Series
        /// </summary>
        public int Series { get; set; }

        /// <summary>
        /// The Number of OVAs
        /// </summary>
        public int OVA { get; set; }

        /// <summary>
        /// The Number of Movies
        /// </summary>
        public int Movie { get; set; }

        /// <summary>
        /// The Number of TV Specials
        /// </summary>
        public int Special { get; set; }

        /// <summary>
        /// ONAs and the like, it's more of a new concept
        /// </summary>
        public int Web { get; set; }

        /// <summary>
        /// Things marked on AniDB as Other, different from None
        /// </summary>
        public int Other { get; set; }

        /// <summary>
        /// Series that don't have AniDB Records. This is very bad, and usually means there was an error in the import process. It can also happen if the API is hit at just the right time.
        /// </summary>
        public int None { get; set; }
    }

    /// <summary>
    /// Episode details for displaying on the dashboard.
    /// </summary>
    public class Episode
    {
        public Episode(SVR_AniDB_Episode episode, SVR_AniDB_Anime anime, SVR_AnimeSeries series = null,
            SVR_VideoLocal file = null, SVR_VideoLocal_User userRecord = null)
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
            Title = episode.PreferredTitle.Title;
            Number = episode.EpisodeNumber;
            Type = episode.AbstractEpisodeType.ToV3Dto();
            AirDate = episode.GetAirDateAsDate() is { } airDate ? DateOnly.FromDateTime(airDate) : null;
            Duration = file?.DurationTimeSpan ?? new TimeSpan(0, 0, episode.LengthSeconds);
            ResumePosition = userRecord?.ResumePositionTimeSpan;
            Watched = userRecord?.WatchedDate?.ToUniversalTime();
            SeriesTitle = series?.PreferredTitle ?? anime.PreferredTitle;
            SeriesPoster = new Image(anime.PreferredOrDefaultPoster);
        }

        /// <summary>
        /// All ids that may be useful for navigating away from the dashboard.
        /// </summary>
        public EpisodeDetailsIDs IDs { get; set; }

        /// <summary>
        /// Episode title.
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// Episode number.
        /// </summary>
        public int Number { get; set; }

        /// <summary>
        /// Episode type.
        /// </summary>
        [JsonConverter(typeof(StringEnumConverter))]
        public EpisodeType Type { get; set; }

        /// <summary>
        /// Air Date.
        /// </summary>
        /// <value></value>
        public DateOnly? AirDate { get; set; }

        /// <summary>
        /// The duration of the episode.
        /// </summary>
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
        public string SeriesTitle { get; set; }

        /// <summary>
        /// Series poster.
        /// </summary>
        public Image SeriesPoster { get; set; }
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
