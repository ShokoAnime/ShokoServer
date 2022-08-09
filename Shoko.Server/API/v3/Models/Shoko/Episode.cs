using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Commons.Extensions;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Server.API.Converters;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.Models;
using Shoko.Server.Repositories;
using Shoko.Server.Utilities;
using AniDBEpisodeType = Shoko.Models.Enums.EpisodeType;

namespace Shoko.Server.API.v3.Models.Shoko
{
    public class Episode : BaseModel
    {
        /// <summary>
        /// The relevant IDs for the Episode: Shoko, AniDB, TvDB
        /// </summary>
        public EpisodeIDs IDs { get; set; }

        /// <summary>
        /// The duration of the episode.
        /// </summary>
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// Where to resume the next playback.
        /// </summary>
        public TimeSpan? ResumePosition { get; set; }

        /// <summary>
        /// The Last Watched Date for the current user. If null, it is unwatched
        /// </summary>
        [Required]
        public DateTime? Watched { get; set; }

        public Episode() {}

        public Episode(HttpContext context, SVR_AnimeEpisode episode)
        {
            var userID = context.GetUser()?.JMMUserID ?? 0;
            var anidbEpisode = episode.AniDB_Episode;
            var tvdbEpisodes = episode.TvDBEpisodes;
            var files = episode.GetVideoLocals();
            var (file, userRecord) = files
                .Select(file =>
                {
                    var userRecord = file.GetUserRecord(userID);
                    if (userRecord == null)
                        return (file, null);

                    return (file, userRecord);
                })
                .Where(tuple => tuple.Item1 != null)
                .OrderByDescending(tuple => tuple.Item2?.LastUpdated)
                .FirstOrDefault();
            IDs = new EpisodeIDs
            {
                ID = episode.AnimeEpisodeID,
                AniDB = episode.AniDB_EpisodeID,
                TvDB = tvdbEpisodes.Select(a => a.Id).ToList(),
            };
            Duration = file?.DurationTimeSpan ?? new TimeSpan(0, 0, anidbEpisode.LengthSeconds);
            ResumePosition = userRecord?.ResumePositionTimeSpan;
            Watched = userRecord?.WatchedDate;
            Name = GetEpisodeTitle(episode.AniDB_EpisodeID);
            Size = files.Count;
        }
        
        
        internal static string GetEpisodeTitle(int anidbEpisodeID)
        {
            // Try finding one of the preferred languages.
            foreach (var language in Languages.PreferredEpisodeNamingLanguages)
            {
                var title = RepoFactory.AniDB_Episode_Title.GetByEpisodeIDAndLanguage(anidbEpisodeID, language.Language)
                    .FirstOrDefault()?.Title;
                if (!string.IsNullOrEmpty(title))
                    return title;
            }

            // Fallback to English if available.
            return RepoFactory.AniDB_Episode_Title.GetByEpisodeIDAndLanguage(anidbEpisodeID, TitleLanguage.English)
                .FirstOrDefault()
                ?.Title;
        }

        internal static EpisodeType MapAniDBEpisodeType(AniDBEpisodeType episodeType)
        {
            switch (episodeType)
            {
                case AniDBEpisodeType.Episode:
                    return EpisodeType.Normal;
                case AniDBEpisodeType.Special:
                    return EpisodeType.Special;
                case AniDBEpisodeType.Parody:
                    return EpisodeType.Parody;
                case AniDBEpisodeType.Credits:
                    return EpisodeType.ThemeSong;
                case AniDBEpisodeType.Trailer:
                    return EpisodeType.Trailer;
                default:
                case AniDBEpisodeType.Other:
                    return EpisodeType.Unknown;
            }
        }

        public static void AddEpisodeVote(HttpContext context, SVR_AnimeEpisode ep, int userID, Vote vote)
        {
            AniDB_Vote dbVote = RepoFactory.AniDB_Vote.GetByEntityAndType(ep.AnimeEpisodeID, AniDBVoteType.Episode);

            if (dbVote == null)
            {
                dbVote = new AniDB_Vote
                {
                    EntityID = ep.AnimeEpisodeID,
                    VoteType = (int) AniDBVoteType.Episode,
                };
            }

            dbVote.VoteValue = (int) Math.Floor(vote.GetRating(1000));

            RepoFactory.AniDB_Vote.Save(dbVote);

            //var cmdVote = new CommandRequest_VoteAnimeEpisode(ep.AniDB_EpisodeID, voteType, vote.GetRating());
            //cmdVote.Save();
        }

        /// <summary>
        /// AniDB specific data for an Episode
        /// </summary>
        public class AniDB
        {
            public AniDB(AniDB_Episode ep)
            {
                if (!decimal.TryParse(ep.Rating, out var rating))
                    rating = 0;
                if (!int.TryParse(ep.Votes, out var votes))
                    votes = 0;
                var titles = RepoFactory.AniDB_Episode_Title.GetByEpisodeID(ep.EpisodeID);

                ID = ep.EpisodeID;
                Type = MapAniDBEpisodeType(ep.GetEpisodeTypeEnum());
                EpisodeNumber = ep.EpisodeNumber;
                AirDate = ep.GetAirDateAsDate();
                Description = ep.Description;
                Rating = new Rating
                {
                    MaxValue = 10,
                    Value = rating,
                    Votes = votes,
                    Source = "AniDB",
                };
                Titles = titles.Select(a => new Title
                    {
                        Name = a.Title,
                        Language = a.LanguageCode,
                        Default = false,
                        Source = "AniDB",
                    }
                ).ToList();
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
            /// Titles for the Episode
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

        public class TvDB
        {
            public TvDB(TvDB_Episode tvDBEpisode)
            {
                Rating rating = tvDBEpisode.Rating == null ? null : new Rating
                {
                    MaxValue = 10,
                    Value = tvDBEpisode.Rating.Value,
                    Source = "TvDB",
                };
                ID = tvDBEpisode.Id;
                Season = tvDBEpisode.SeasonNumber;
                Number = tvDBEpisode.EpisodeNumber;
                AbsoluteNumber = tvDBEpisode.AbsoluteNumber ?? 0;
                Title = tvDBEpisode.EpisodeName;
                Description = tvDBEpisode.Overview;
                AirDate = tvDBEpisode.AirDate;
                Rating = rating;
                AirsAfterSeason = tvDBEpisode.AirsAfterSeason ?? 0;
                AirsBeforeSeason = tvDBEpisode.AirsBeforeSeason ?? 0;
                AirsBeforeEpisode = tvDBEpisode.AirsBeforeEpisode ?? 0;
                Thumbnail = (new Image(tvDBEpisode.Id, ImageEntityType.TvDB_Episode, true));
            }
            
            /// <summary>
            /// TvDB Episode ID
            /// </summary>
            public int ID { get; set; }

            /// <summary>
            /// Season Number, 0 is Specials. TvDB's Season system doesn't always make sense for anime, so don't count on it
            /// </summary>
            public int Season { get; set; }

            /// <summary>
            /// Episode Number in the Season. This is not Absolute Number
            /// </summary>
            public int Number { get; set; }

            /// <summary>
            /// Absolute Episode Number. Keep in mind that due to reordering, this may not be accurate.
            /// </summary>
            public int AbsoluteNumber { get; set; }

            /// <summary>
            /// Episode Title, in the language selected for TvDB. TvDB doesn't allow pulling more than one language at a time, so this isn't a list.
            /// </summary>
            public string Title { get; set; }

            /// <summary>
            /// Episode Description, in the language selected for TvDB. See Title for more info on Language.
            /// </summary>
            public string Description { get; set; }

            /// <summary>
            /// Air Date. Unfortunately, the TvDB air date doesn't necessarily conform to a specific timezone, so it can be a day off. If you see one that's wrong, please fix it on TvDB. You have the ID here in this model for easy lookup.
            /// </summary>
            [JsonConverter(typeof(DateFormatConverter), "yyyy-MM-dd")]
            public DateTime? AirDate { get; set; }

            /// <summary>
            /// Mostly for specials. It shows when in the timeline the episode aired. I wouldn't count on it, as it's often blank.
            /// </summary>
            public int AirsAfterSeason { get; set; }

            /// <summary>
            /// Mostly for specials. It shows when in the timeline the episode aired. I wouldn't count on it, as it's often blank.
            /// </summary>
            public int AirsBeforeSeason { get; set; }

            /// <summary>
            /// Like AirsAfterSeason, it is for determining where in the timeline an episode airs. Also often blank.
            /// </summary>
            public int AirsBeforeEpisode { get; set; }

            /// <summary>
            /// Rating of the episode
            /// </summary>
            public Rating Rating { get; set; }

            /// <summary>
            /// The TvDB Thumbnail. Later, we'll have more thumbnail support, and episodes will have an Images endpoint like series, but for now, this will do.
            /// </summary>
            public Image Thumbnail { get; set; }
        }

        public class EpisodeIDs : IDs
        {
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
            public List<int> TvDB { get; set; } = new List<int>();

            // TODO Support for TvDB string IDs (like in the new URLs) one day maybe
            #endregion
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
        Extra = 10,
    }
}
