using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Commons.Extensions;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Server.API.Converters;
using Shoko.Server.API.v3.Helpers;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.Models;
using Shoko.Server.Repositories;

using AniDBEpisodeType = Shoko.Models.Enums.EpisodeType;
using DataSource = Shoko.Server.API.v3.Models.Common.DataSource;
using TmdbEpisode = Shoko.Server.API.v3.Models.TMDB.Episode;
using TmdbMovie = Shoko.Server.API.v3.Models.TMDB.Movie;

#nullable enable
namespace Shoko.Server.API.v3.Models.Shoko;

public class Episode : BaseModel
{
    /// <summary>
    /// The relevant IDs for the Episode: Shoko, AniDB, TMDB.
    /// </summary>
    public EpisodeIDs IDs { get; set; }

    /// <summary>
    /// Indicates that the episode have a custom name set.
    /// </summary>
    public bool HasCustomName { get; set; }

    /// <summary>
    /// Preferred episode description based on the language preference.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// The preferred images for the episode.
    /// </summary>
    public Images Images { get; set; }

    /// <summary>
    /// The duration of the episode.
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Where to resume the next playback for the most recently watched file, if
    /// any. Otherwise `null` if no files for the episode have any resume
    /// positions.
    /// </summary>
    public TimeSpan? ResumePosition { get; set; }

    /// <summary>
    /// The last watched date and time for the current user for the most
    /// recently watched file, if any. Or `null` if it is considered
    /// "unwatched."
    /// </summary>
    [JsonConverter(typeof(IsoDateTimeConverter))]
    public DateTime? Watched { get; set; }

    /// <summary>
    /// Total number of times the episode have been watched (till completion) by
    /// the user across all files.
    /// </summary>
    public int WatchCount { get; set; }

    /// <summary>
    /// Episode is marked as "ignored." Which means it won't be show up in the
    /// api unless explicitly requested, and will not count against the unwatched
    /// counts and missing counts for the series.
    /// </summary>
    public bool IsHidden { get; set; }

    /// <summary>
    /// The user's rating
    /// </summary>
    public Rating? UserRating { get; set; }

#pragma warning disable IDE1006
    /// <summary>
    /// The <see cref="Episode.AniDB"/>, if <see cref="DataSource.AniDB"/> is
    /// included in the data to add.
    /// </summary>
    [JsonProperty("AniDB", NullValueHandling = NullValueHandling.Ignore)]
    public AniDB? _AniDB { get; set; }
#pragma warning restore IDE1006

    /// <summary>
    /// The <see cref="TmdbData"/> entries, if <see cref="DataSource.TMDB"/>
    /// is included in the data to add.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public TmdbData? TMDB { get; set; }

    /// <summary>
    /// Files associated with the episode, if included with the metadata.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public IEnumerable<File>? Files { get; set; }

    /// <summary>
    /// File/episode cross-references linked to the episode.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public IEnumerable<FileCrossReference.EpisodeCrossReferenceIDs>? CrossReferences { get; set; }

    public Episode(HttpContext context, SVR_AnimeEpisode episode, HashSet<DataSource>? includeDataFrom = null, bool includeFiles = false, bool includeMediaInfo = false, bool includeAbsolutePaths = false, bool withXRefs = false)
    {
        includeDataFrom ??= [];
        var userID = context.GetUser()?.JMMUserID ?? 0;
        var episodeUserRecord = episode.GetUserRecord(userID);
        var anidbEpisode = episode.AniDB_Episode ??
            throw new NullReferenceException($"Unable to get AniDB Episode {episode.AniDB_EpisodeID} for Anime Episode {episode.AnimeEpisodeID}");
        var tmdbMovieXRefs = episode.TmdbMovieCrossReferences;
        var tmdbEpisodeXRefs = episode.TmdbEpisodeCrossReferences;
        var files = episode.VideoLocals;
        var (file, fileUserRecord) = files
            .Select(file => (file, userRecord: RepoFactory.VideoLocalUser.GetByUserIDAndVideoLocalID(userID, file.VideoLocalID)))
            .OrderByDescending(tuple => tuple.userRecord?.LastUpdated)
            .FirstOrDefault();
        var vote = RepoFactory.AniDB_Vote.GetByEntityAndType(episode.AniDB_EpisodeID, AniDBVoteType.Episode);
        IDs = new EpisodeIDs
        {
            ID = episode.AnimeEpisodeID,
            ParentSeries = episode.AnimeSeriesID,
            AniDB = episode.AniDB_EpisodeID,
            TvDB = tmdbEpisodeXRefs.Select(xref => xref.TmdbEpisode?.TvdbEpisodeID).WhereNotNull().Distinct().ToList(),
            IMDB = tmdbMovieXRefs
                .Select(xref => xref.TmdbMovie?.ImdbMovieID)
                .WhereNotNull()
                .Distinct()
                .ToList(),
            TMDB = new()
            {
                Episode = tmdbEpisodeXRefs
                    .Where(xref => xref.TmdbEpisodeID != 0)
                    .Select(xref => xref.TmdbEpisodeID)
                    .ToList(),
                Movie = tmdbMovieXRefs
                    .Select(xref => xref.TmdbMovieID)
                    .ToList(),
                Show = tmdbEpisodeXRefs
                    .Where(xref => xref.TmdbShowID != 0)
                    .Select(xref => xref.TmdbShowID)
                    .Distinct()
                    .ToList(),
            },
        };
        HasCustomName = !string.IsNullOrEmpty(episode.EpisodeNameOverride);
        Images = episode.GetImages().ToDto(includeThumbnails: true, preferredImages: true);
        Duration = file?.DurationTimeSpan ?? new TimeSpan(0, 0, anidbEpisode.LengthSeconds);
        ResumePosition = fileUserRecord?.ResumePositionTimeSpan;
        Watched = fileUserRecord?.WatchedDate?.ToUniversalTime();
        WatchCount = episodeUserRecord?.WatchedCount ?? 0;
        IsHidden = episode.IsHidden;
        Name = episode.PreferredTitle;
        Description = episode.PreferredOverview;
        Size = files.Count;

        if (vote is not null)
        {
            UserRating = new()
            {
                Value = (decimal)Math.Round(vote.VoteValue / 100D, 1),
                MaxValue = 10,
                Type = AniDBVoteType.Episode.ToString(),
                Source = "User"
            };
        }

        if (includeDataFrom.Contains(DataSource.AniDB))
            _AniDB = new AniDB(anidbEpisode);
        if (includeDataFrom.Contains(DataSource.TMDB))
            TMDB = new()
            {
                Episodes = tmdbEpisodeXRefs
                    .Select(tmdbEpisodeXref => tmdbEpisodeXref.TmdbEpisode)
                    .WhereNotNull()
                    .GroupBy(tmdbEpisode => tmdbEpisode.TmdbShowID)
                    .Select(groupBy => (TmdbShow: groupBy.First().TmdbShow!, TmdbEpisodes: groupBy.ToList()))
                    .Where(tuple => tuple.TmdbShow is not null)
                    .SelectMany(tuple0 =>
                        string.IsNullOrEmpty(tuple0.TmdbShow.PreferredAlternateOrderingID)
                            ? tuple0.TmdbEpisodes.Select(tmdbEpisode => new TmdbEpisode(tuple0.TmdbShow, tmdbEpisode))
                            : tuple0.TmdbEpisodes
                                .Select(tmdbEpisode => (TmdbEpisode: tmdbEpisode, TmdbAlternateOrdering: tmdbEpisode.GetTmdbAlternateOrderingEpisodeById(tuple0.TmdbShow.PreferredAlternateOrderingID)))
                                .Where(tuple1 => tuple1.TmdbAlternateOrdering is not null)
                                .Select(tuple1 => new TmdbEpisode(tuple0.TmdbShow, tuple1.TmdbEpisode, tuple1.TmdbAlternateOrdering)
                    ))
                    .ToList(),
                Movies = tmdbMovieXRefs
                    .Select(tmdbMovieXref => tmdbMovieXref.TmdbMovie)
                    .WhereNotNull()
                    .Select(tmdbMovie => new TmdbMovie(tmdbMovie))
                    .ToList(),
            };
        if (includeFiles)
            Files = files
                .Select(f => new File(context, f, false, includeDataFrom, includeMediaInfo, includeAbsolutePaths))
                .ToList();
        if (withXRefs)
            CrossReferences = FileCrossReference.From(episode.FileCrossReferences).FirstOrDefault()?.EpisodeIDs ?? [];
    }

    internal static EpisodeType MapAniDBEpisodeType(AniDBEpisodeType episodeType)
        => episodeType switch
        {
            AniDBEpisodeType.Episode => EpisodeType.Normal,
            AniDBEpisodeType.Special => EpisodeType.Special,
            AniDBEpisodeType.Parody => EpisodeType.Parody,
            AniDBEpisodeType.Credits => EpisodeType.ThemeSong,
            AniDBEpisodeType.Trailer => EpisodeType.Trailer,
            AniDBEpisodeType.Other => EpisodeType.Other,
            _ => EpisodeType.Unknown,
        };

    /// <summary>
    /// AniDB specific data for an Episode
    /// </summary>
    public class AniDB
    {
        public AniDB(SVR_AniDB_Episode ep)
        {
            if (!decimal.TryParse(ep.Rating, out var rating))
            {
                rating = 0;
            }

            if (!int.TryParse(ep.Votes, out var votes))
            {
                votes = 0;
            }

            var defaultTitle = ep.DefaultTitle;
            var mainTitle = ep.PreferredTitle;
            var titles = ep.GetTitles();
            ID = ep.EpisodeID;
            Type = MapAniDBEpisodeType(ep.GetEpisodeTypeEnum());
            EpisodeNumber = ep.EpisodeNumber;
            AirDate = ep.GetAirDateAsDate();
            Description = ep.Description;
            Rating = new Rating { MaxValue = 10, Value = rating, Votes = votes, Source = "AniDB" };
            Title = mainTitle.Title;
            Titles = titles
                .Select(a => new Title(a, defaultTitle.Title, mainTitle))
                .ToList();
        }

        /// <summary>
        /// AniDB Episode ID
        /// </summary>
        public int ID { get; set; }

        /// <summary>
        /// Episode Type
        /// </summary>
        [JsonConverter(typeof(StringEnumConverter))]
        public EpisodeType Type { get; set; }

        /// <summary>
        /// Episode Number
        /// </summary>
        public int EpisodeNumber { get; set; }

        /// <summary>
        /// First Listed Air Date. This may not be when it aired, but an early release date
        /// </summary>
        [JsonConverter(typeof(DateFormatConverter), "yyyy-MM-dd")]
        public DateTime? AirDate { get; set; }

        /// <summary>
        /// Preferred title for the episode.
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// All titles for the episode.
        /// </summary>
        public List<Title> Titles { get; set; }

        /// <summary>
        /// AniDB Episode Summary
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Episode Rating
        /// </summary>
        public Rating Rating { get; set; }
    }

    public class EpisodeIDs : IDs
    {
        #region Series

        /// <summary>
        /// The id of the parent <see cref="Series"/>.
        /// </summary>
        public int ParentSeries { get; set; }

        #endregion

        #region XRefs

        // These are useful for many things, but for clients, it is mostly auxiliary

        /// <summary>
        /// The AniDB ID
        /// </summary>
        [Required]
        public int AniDB { get; set; }

        /// <summary>
        /// The TvDB IDs
        /// </summary>
        public List<int> TvDB { get; set; } = [];

        /// <summary>
        /// The IMDB Movie IDs.
        /// </summary>
        public List<string> IMDB { get; set; } = [];

        #endregion
        /// <summary>
        /// The Movie DataBase (TMDB) Cross-Reference IDs.
        /// </summary>
        public TmdbEpisodeIDs TMDB { get; init; } = new();

        public class TmdbEpisodeIDs
        {
            public List<int> Episode { get; init; } = [];

            public List<int> Movie { get; init; } = [];

            public List<int> Show { get; init; } = [];
        }
    }

    public class TmdbData
    {
        public IEnumerable<TmdbEpisode> Episodes { get; init; } = [];

        public IEnumerable<TmdbMovie> Movies { get; init; } = [];
    }

    public static class Input
    {
        public class EpisodeTitleOverrideBody
        {
            /// <summary>
            /// New title to be set as override for the series
            /// </summary>
            [Required(AllowEmptyStrings = true)]
            public string Title { get; set; } = string.Empty;
        }
    }
}

public enum EpisodeType
{
    /// <summary>
    /// The episode type is unknown.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// A catch-all type for future extensions when a provider can't use a current episode type, but knows what the future type should be.
    /// </summary>
    Other = 1,

    /// <summary>
    /// A normal episode.
    /// </summary>
    Normal = 2,

    /// <summary>
    /// A special episode.
    /// </summary>
    Special = 3,

    /// <summary>
    /// A trailer.
    /// </summary>
    Trailer = 4,

    /// <summary>
    /// Either an opening-song, or an ending-song.
    /// </summary>
    ThemeSong = 5,

    /// <summary>
    /// Intro, and/or opening-song.
    /// </summary>
    OpeningSong = 6,

    /// <summary>
    /// Outro, end-roll, credits, and/or ending-song.
    /// </summary>
    EndingSong = 7,

    /// <summary>
    /// AniDB parody type. Where else would this be useful?
    /// </summary>
    Parody = 8,

    /// <summary>
    /// A interview tied to the series.
    /// </summary>
    Interview = 9,

    /// <summary>
    /// A DVD or BD extra, e.g. BD-menu or deleted scenes.
    /// </summary>
    Extra = 10
}
