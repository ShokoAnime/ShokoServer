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
using Shoko.Server.AniDB_API.Titles;
using Shoko.Server.API.Converters;
using Shoko.Server.API.v3.Helpers;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.Commands;
using Shoko.Server.Commands.AniDB;
using Shoko.Server.Models;
using Shoko.Server.Repositories;
using Shoko.Server.Server;
using AniDBEpisodeType = Shoko.Models.Enums.EpisodeType;
using RelationType = Shoko.Plugin.Abstractions.DataModels.RelationType;

// ReSharper disable UnusedMember.Local
// ReSharper disable UnusedAutoPropertyAccessor.Global

using SSS = Shoko.Server.Server;

namespace Shoko.Server.API.v3.Models.Shoko
{
    /// <summary>
    /// Series object, stores all of the series info
    /// </summary>
    public class Series : BaseModel
    {
        /// <summary>
        /// The relevant IDs for the series, Shoko Internal, AniDB, etc
        /// </summary>
        public SeriesIDs IDs { get; set; }

        /// <summary>
        /// The default or random pictures for a series. This allows the client to not need to get all images and pick one.
        /// There should always be a poster, but no promises on the rest.
        /// </summary>
        public Images Images { get; set; }

        /// <summary>
        /// the user's rating
        /// </summary>
        public Rating UserRating { get; set; }

        /// <summary>
        /// links to series pages on various sites
        /// </summary>
        public List<Resource> Links { get; set; }

        /// <summary>
        /// Sizes object, has totals
        /// </summary>
        public SeriesSizes Sizes { get; set; }

        /// <summary>
        /// The time when the series was created, during the process of the first file being added
        /// </summary>
        [JsonConverter(typeof(IsoDateTimeConverter))]
        public DateTime Created { get; set; }

        /// <summary>
        /// The time when the series was last updated
        /// </summary>
        [JsonConverter(typeof(IsoDateTimeConverter))]
        public DateTime Updated { get; set; }

        #region Constructors and Helper Methods

        public Series() {}

        public Series(HttpContext ctx, SVR_AnimeSeries ser, bool randomiseImages = false)
        {
            int uid = ctx.GetUser()?.JMMUserID ?? 0;

            AddBasicAniDBInfo(ctx, ser);

            List<SVR_AnimeEpisode> ael = ser.GetAnimeEpisodes();
            var contract = ser.Contract;
            if (contract == null) ser.UpdateContract();

            IDs = GetIDs(ser);
            Images = GetDefaultImages(ctx, ser, randomiseImages);

            Name = ser.GetSeriesName();
            Sizes = ModelHelper.GenerateSeriesSizes(ael, uid);
            Size = Sizes.Local.Credits + Sizes.Local.Episodes + Sizes.Local.Others + Sizes.Local.Parodies +
                   Sizes.Local.Specials + Sizes.Local.Trailers;

            Created = ser.DateTimeCreated;
            Updated = ser.DateTimeUpdated;
        }

        private void AddBasicAniDBInfo(HttpContext ctx, SVR_AnimeSeries ser)
        {
            var anime = ser.GetAnime();
            if (anime == null) return;
            AniDB_Vote vote = RepoFactory.AniDB_Vote.GetByEntityAndType(anime.AnimeID, AniDBVoteType.Anime) ??
                              RepoFactory.AniDB_Vote.GetByEntityAndType(anime.AnimeID, AniDBVoteType.AnimeTemp);
            if (vote != null)
            {
                string voteType = (AniDBVoteType) vote.VoteType == AniDBVoteType.Anime ? "Permanent" : "Temporary";
                UserRating = new Rating
                {
                    Value = ((decimal) Math.Round(vote.VoteValue / 100D, 1)),
                    MaxValue = 10,
                    Type = voteType,
                    Source = "User"
                };
            }
        }

        public static bool RefreshAniDBFromCachedXML(HttpContext ctx, SVR_AniDB_Anime anime)
        {
            return SSS.ShokoService.AniDBProcessor.UpdateCachedAnimeInfoHTTP(anime);
        }

        public static bool QueueAniDBRefresh(int animeID, bool force, bool downloadRelations, bool createSeriesEntry, bool immediate = false)
        {
            if (immediate && !ShokoService.AniDBProcessor.IsHttpBanned) {
                var anime = ShokoService.AniDBProcessor.GetAnimeInfoHTTP(animeID, force, downloadRelations, 0, createSeriesEntry);
                if (anime != null) {
                    return true;
                }
            }

            var command = new CommandRequest_GetAnimeHTTP(animeID, force, downloadRelations, createSeriesEntry);
            command.Save();
            return false;
        }

        public static SeriesIDs GetIDs(SVR_AnimeSeries ser)
        {
            // Shoko
            var ids = new SeriesIDs
            {
                ID = ser.AnimeSeriesID,
                ParentGroup = ser.AnimeGroupID,
                TopLevelGroup = ser.TopLevelAnimeGroup.AnimeGroupID,
            };
            
            // AniDB
            var anidbId = ser.GetAnime()?.AnimeID;
            if (anidbId.HasValue) ids.AniDB = anidbId.Value;
            
            // TvDB
            var tvdbIds = RepoFactory.CrossRef_AniDB_TvDB.GetByAnimeID(ser.AniDB_ID);
            if (tvdbIds.Any()) ids.TvDB.AddRange(tvdbIds.Select(a => a.TvDBID).Distinct());
            
            // TODO: Cache the rest of these, so that they don't severely slow down the API

            // TMDB
            // TODO: make this able to support more than one, in fact move it to its own and remove CrossRef_Other
            var tmdbId = RepoFactory.CrossRef_AniDB_Other.GetByAnimeIDAndType(ser.AniDB_ID, CrossRefType.MovieDB);
            if (tmdbId != null && int.TryParse(tmdbId.CrossRefID, out int movieID)) ids.TMDB.Add(movieID);
            
            // Trakt
            // var traktIds = RepoFactory.CrossRef_AniDB_TraktV2.GetByAnimeID(ser.AniDB_ID).Select(a => a.TraktID)
            //     .Distinct().ToList();
            // if (traktIds.Any()) ids.TraktTv.AddRange(traktIds);
            
            // MAL
            var malIds = RepoFactory.CrossRef_AniDB_MAL.GetByAnimeID(ser.AniDB_ID).Select(a => a.MALID).Distinct()
                .ToList();
            if (malIds.Any()) ids.MAL.AddRange(malIds);
            
            // TODO: AniList later
            return ids;
        }

        public static Image GetDefaultImage(int anidbId, ImageSizeType imageSizeType, ImageEntityType? imageEntityType = null)
        {
            var defaultImage = imageEntityType.HasValue ?
                RepoFactory.AniDB_Anime_DefaultImage.GetByAnimeIDAndImagezSizeTypeAndImageEntityType(anidbId, imageSizeType, imageEntityType.Value) :
                RepoFactory.AniDB_Anime_DefaultImage.GetByAnimeIDAndImagezSizeType(anidbId, imageSizeType);
            return defaultImage != null ? new Image(defaultImage.ImageParentID, (ImageEntityType) defaultImage.ImageParentType, true) : null;       
        }

        public static Images GetDefaultImages(HttpContext ctx, SVR_AnimeSeries ser, bool randomiseImages = false)
        {
            var images = new Images();
            var random = ctx.Items["Random"] as Random;
            var allImages = GetArt(ctx, ser.AniDB_ID);

            var poster = randomiseImages ? allImages.Posters.GetRandomElement(random) : GetDefaultImage(ser.AniDB_ID, ImageSizeType.Poster) ?? allImages.Posters.FirstOrDefault();
            if (poster != null) images.Posters.Add(poster);

            var fanart = randomiseImages ? allImages.Fanarts.GetRandomElement(random) : GetDefaultImage(ser.AniDB_ID, ImageSizeType.Fanart) ?? allImages.Fanarts.FirstOrDefault();
            if (fanart != null) images.Fanarts.Add(fanart);

            var banner = randomiseImages ? allImages.Banners.GetRandomElement(random) : GetDefaultImage(ser.AniDB_ID, ImageSizeType.WideBanner) ?? allImages.Banners.FirstOrDefault();
            if (banner != null) images.Banners.Add(banner);

            return images;
        }

        /// <summary>
        /// Cast is aggregated, and therefore not in each provider
        /// </summary>
        /// <param name="ctx"></param>
        /// <param name="animeID"></param>
        /// <param name="roleType"></param>
        /// <returns></returns>
        public static List<Role> GetCast(HttpContext ctx, int animeID, Role.CreatorRoleType? roleType = null)
        {
            List<Role> roles = new List<Role>();
            var xrefAnimeStaff = roleType.HasValue ? RepoFactory.CrossRef_Anime_Staff.GetByAnimeIDAndRoleType(animeID,
                (StaffRoleType) roleType.Value) : RepoFactory.CrossRef_Anime_Staff.GetByAnimeID(animeID);
            foreach (var xref in xrefAnimeStaff)
            {
                AnimeCharacter character = xref.RoleID.HasValue ? RepoFactory.AnimeCharacter.GetByID(xref.RoleID.Value) : null;
                AnimeStaff staff = RepoFactory.AnimeStaff.GetByID(xref.StaffID);
                if (staff == null) continue;
                Role role = new Role
                {
                    Character = character != null ? new Role.Person
                    {
                        Name = character.Name,
                        AlternateName = character.AlternateName,
                        Image = new Image(character.CharacterID, ImageEntityType.Character),
                        Description = character.Description
                    } : null,
                    Staff = new Role.Person
                    {
                        Name = staff.Name,
                        AlternateName = staff.AlternateName,
                        Description = staff.Description,
                        Image = staff.ImagePath != null ? new Image(staff.StaffID, ImageEntityType.Staff) : null,
                    },
                    RoleName = (Role.CreatorRoleType) xref.RoleType,
                    RoleDetails = xref.Role
                };
                roles.Add(role);
            }

            return roles;
        }

        public static List<Tag> GetTags(HttpContext ctx, SVR_AniDB_Anime anime, TagFilter.Filter filter, bool excludeDescriptions = false)
        {
            var tags = new List<Tag>();

            var allTags = anime.GetAniDBTags().DistinctBy(a => a.TagName).ToList();
            var filteredTags = new TagFilter<AniDB_Tag>(name => RepoFactory.AniDB_Tag.GetByName(name).FirstOrDefault(), tag => tag.TagName).ProcessTags(filter, allTags);
            foreach (AniDB_Tag tag in filteredTags)
            {
                var toAPI = new Tag
                {
                    Name = tag.TagName
                };
                var animeXRef = RepoFactory.AniDB_Anime_Tag.GetByTagID(tag.TagID).FirstOrDefault();
                if (animeXRef != null) toAPI.Weight = animeXRef.Weight;
                if (!excludeDescriptions) toAPI.Description = tag.TagDescription;

                tags.Add(toAPI);
            }

            return tags;
        }

        public static SeriesType GetAniDBSeriesType(int? animeType)
            => animeType.HasValue ? GetAniDBSeriesType((AnimeType)animeType.Value) : SeriesType.Unknown;

        public static SeriesType GetAniDBSeriesType(AnimeType animeType)
        {
            switch (animeType)
            {
                default:
                case AnimeType.None:
                    return SeriesType.Unknown;
                case AnimeType.TVSeries:
                    return SeriesType.TV;
                case AnimeType.Movie:
                    return SeriesType.Movie;
                case AnimeType.OVA:
                    return SeriesType.OVA;
                case AnimeType.TVSpecial:
                    return SeriesType.TVSpecial;
                case AnimeType.Web:
                    return SeriesType.Web;
                case AnimeType.Other:
                    return SeriesType.Other;
            }
        }

        public static List<TvDB> GetTvDBInfo(HttpContext ctx, SVR_AnimeSeries ser)
        {
            var ael = ser.GetAnimeEpisodes();
            return RepoFactory.CrossRef_AniDB_TvDB.GetByAnimeID(ser.AniDB_ID)
                .Select(xref => RepoFactory.TvDB_Series.GetByTvDBID(xref.TvDBID))
                .Select(tvdbSer => new TvDB(ctx, tvdbSer, ser, ael))
                .ToList();
        }

        public static void AddSeriesVote(HttpContext context, SVR_AnimeSeries ser, int userID, Vote vote)
        {
            int voteType = (vote.Type?.ToLowerInvariant() ?? "") switch
            {
                "temporary" => (int) AniDBVoteType.AnimeTemp,
                "permanent" => (int) AniDBVoteType.Anime,
                _ => ser.GetAnime()?.GetFinishedAiring() ?? false ? (int) AniDBVoteType.Anime : (int) AniDBVoteType.AnimeTemp,
            };

            AniDB_Vote dbVote = RepoFactory.AniDB_Vote.GetByEntityAndType(ser.AniDB_ID, AniDBVoteType.AnimeTemp) ?? RepoFactory.AniDB_Vote.GetByEntityAndType(ser.AniDB_ID, AniDBVoteType.Anime);

            if (dbVote == null)
            {
                dbVote = new AniDB_Vote
                {
                    EntityID = ser.AniDB_ID,
                };
            }

            dbVote.VoteValue = (int) Math.Floor(vote.GetRating(1000));
            dbVote.VoteType = voteType;

            RepoFactory.AniDB_Vote.Save(dbVote);

            var cmdVote = new CommandRequest_VoteAnime(ser.AniDB_ID, voteType, vote.GetRating());
            cmdVote.Save();
        }

        public static Images GetArt(HttpContext ctx, int animeID, bool includeDisabled = false)
        {
            var images = new Images();
            AddAniDBPoster(ctx, images, animeID);
            AddTvDBImages(ctx, images, animeID, includeDisabled);
            // AddMovieDBImages(ctx, images, animeID, includeDisabled);
            return images;
        }

        private static void AddAniDBPoster(HttpContext ctx, Images images, int animeID)
        {
            images.Posters.Add(GetAniDBPoster(animeID));
        }
        
        public static Image GetAniDBPoster(int animeID)
        {
            var defaultImage = RepoFactory.AniDB_Anime_DefaultImage.GetByAnimeIDAndImagezSizeType(animeID,
                ImageSizeType.Poster);
            bool preferred = defaultImage != null && defaultImage.ImageParentType == (int) ImageEntityType.AniDB_Cover;
            return new Image(animeID, ImageEntityType.AniDB_Cover, preferred);
        }

        private static void AddTvDBImages(HttpContext ctx, Images images, int animeID, bool includeDisabled = false)
        {
            var tvdbIDs = RepoFactory.CrossRef_AniDB_TvDB.GetByAnimeID(animeID).ToList();

            var defaultFanart = RepoFactory.AniDB_Anime_DefaultImage.GetByAnimeIDAndImagezSizeTypeAndImageEntityType(animeID, ImageSizeType.Fanart, ImageEntityType.TvDB_FanArt);
            var fanarts = tvdbIDs.SelectMany(a => RepoFactory.TvDB_ImageFanart.GetBySeriesID(a.TvDBID)).ToList();
            images.Fanarts.AddRange(fanarts.Where(a => includeDisabled || a.Enabled != 0).Select(a =>
            {
                bool preferred = defaultFanart != null && defaultFanart.ImageParentID == a.TvDB_ImageFanartID;
                return new Image(a.TvDB_ImageFanartID, ImageEntityType.TvDB_FanArt, preferred, a.Enabled == 0);
            }));

            var defaultBanner = RepoFactory.AniDB_Anime_DefaultImage.GetByAnimeIDAndImagezSizeTypeAndImageEntityType(animeID, ImageSizeType.WideBanner, ImageEntityType.TvDB_Banner);
            var banners = tvdbIDs.SelectMany(a => RepoFactory.TvDB_ImageWideBanner.GetBySeriesID(a.TvDBID)).ToList();
            images.Banners.AddRange(banners.Where(a => includeDisabled || a.Enabled != 0).Select(a =>
            {
                bool preferred = defaultBanner != null && defaultBanner.ImageParentID == a.TvDB_ImageWideBannerID;
                return new Image(a.TvDB_ImageWideBannerID, ImageEntityType.TvDB_Banner, preferred, a.Enabled == 0);
            }));

            var defaultPoster = RepoFactory.AniDB_Anime_DefaultImage.GetByAnimeIDAndImagezSizeTypeAndImageEntityType(animeID, ImageSizeType.Poster, ImageEntityType.TvDB_Cover);
            var posters = tvdbIDs.SelectMany(a => RepoFactory.TvDB_ImagePoster.GetBySeriesID(a.TvDBID)).ToList();
            images.Posters.AddRange(posters.Where(a => includeDisabled || a.Enabled != 0).Select(a =>
            {
                bool preferred = defaultPoster != null && defaultPoster.ImageParentID == a.TvDB_ImagePosterID;
                return new Image(a.TvDB_ImagePosterID, ImageEntityType.TvDB_Cover, preferred, a.Enabled == 0);
            }));
        }

        private static void AddMovieDBImages(HttpContext ctx, Images images, int animeID, bool includeDisabled = false)
        {
            var moviedb = RepoFactory.CrossRef_AniDB_Other.GetByAnimeIDAndType(animeID, CrossRefType.MovieDB);

            var moviedbPosters = moviedb == null ? new() : RepoFactory.MovieDB_Poster.GetByMovieID(int.Parse(moviedb.CrossRefID));
            var defaultPoster = RepoFactory.AniDB_Anime_DefaultImage.GetByAnimeIDAndImagezSizeTypeAndImageEntityType(animeID, ImageSizeType.Poster, ImageEntityType.MovieDB_Poster);
            images.Posters.AddRange(moviedbPosters.Where(a => includeDisabled || a.Enabled != 0).Select(a =>
            {
                bool preferred = defaultPoster != null && defaultPoster.ImageParentID == a.MovieDB_PosterID;
                return new Image(a.MovieDB_PosterID, ImageEntityType.MovieDB_Poster, preferred, a.Enabled == 1);
            }));

            var moviedbFanarts = moviedb == null ? new() : RepoFactory.MovieDB_Fanart.GetByMovieID(int.Parse(moviedb.CrossRefID));
            var defaultFanart = RepoFactory.AniDB_Anime_DefaultImage.GetByAnimeIDAndImagezSizeTypeAndImageEntityType(animeID, ImageSizeType.Fanart, ImageEntityType.MovieDB_FanArt);
            images.Fanarts.AddRange(moviedbFanarts.Where(a => includeDisabled || a.Enabled != 0).Select(a =>
            {
                bool preferred = defaultFanart != null && defaultFanart.ImageParentID == a.MovieDB_FanartID;
                return new Image(a.MovieDB_FanartID, ImageEntityType.MovieDB_FanArt, preferred, a.Enabled == 1);
            }));
        }

        #endregion

        /// <summary>
        /// Basic anidb data across all anidb types.
        /// </summary>
        public class AniDB
        {
            public AniDB() { }

            public AniDB(SVR_AniDB_Anime anime, bool includeTitles) : this(anime, null, includeTitles) { }

            public AniDB(SVR_AniDB_Anime anime, SVR_AnimeSeries series = null, bool includeTitles = true)
            {
                if (series == null)
                    series = RepoFactory.AnimeSeries.GetByAnimeID(anime.AnimeID);
                ID = anime.AnimeID;
                ShokoID = series?.AnimeSeriesID;
                Type = GetAniDBSeriesType(anime.AnimeType);
                Title = series?.GetSeriesName() ?? anime.PreferredTitle;
                Titles = includeTitles ? anime.GetTitles().Select(title => new Title
                    {
                        Name = title.Title,
                        Language = title.Language,
                        Type = title.TitleType,
                        Default = string.Equals(title.Title, Title),
                        Source = "AniDB",
                    }
                ).ToList() : null;
                Description = anime.Description;
                Restricted = anime.Restricted == 1;
                Poster = GetAniDBPoster(anime.AnimeID);
                EpisodeCount = anime.EpisodeCountNormal;
                Rating = new Rating
                {
                    Source = "AniDB",
                    Value = anime.Rating,
                    MaxValue = 1000,
                    Votes = anime.VoteCount,
                };
                UserApproval = null;
                Relation = null;
            }

            public AniDB(AniDBRaw_AnimeTitle_Anime result,bool includeTitles) : this(result, null, includeTitles) { }

            public AniDB(AniDBRaw_AnimeTitle_Anime result, SVR_AnimeSeries series = null, bool includeTitles = false)
            {
                if (series == null)
                    series = RepoFactory.AnimeSeries.GetByAnimeID(result.AnimeID);
                var anime = series != null ? series.GetAnime() : RepoFactory.AniDB_Anime.GetByAnimeID(result.AnimeID);

                ID = result.AnimeID;
                ShokoID = series?.AnimeSeriesID;
                Type = Series.GetAniDBSeriesType(anime?.AnimeType);
                Title = series?.GetSeriesName() ?? anime?.PreferredTitle ?? result.MainTitle;
                Titles = includeTitles ? result.Titles.Select(title => new Title
                    {
                        Language = title.TitleLanguage,
                        Name = title.Title,
                        Type = title.TitleType,
                        Default = string.Equals(title.Title, Title),
                        Source = "AniDB",
                    }
                ).ToList() : null;
                Description = anime?.Description;
                Restricted = anime != null ? anime.Restricted == 1 : false;
                EpisodeCount = anime?.EpisodeCount;
                Poster = GetAniDBPoster(result.AnimeID);
            }

            public AniDB(SVR_AniDB_Anime_Relation relation, bool includeTitles) : this(relation, null, includeTitles) { }

            public AniDB(SVR_AniDB_Anime_Relation relation, SVR_AnimeSeries series = null, bool includeTitles = true)
            {
                if (series == null)
                    series = RepoFactory.AnimeSeries.GetByAnimeID(relation.RelatedAnimeID);
                ID = relation.RelatedAnimeID;
                ShokoID = series?.AnimeSeriesID;
                if (RepoFactory.AniDB_Anime.TryGetByAnimeID(relation.RelatedAnimeID, out var anime))
                {
                    Type = GetAniDBSeriesType(anime.AnimeType);
                    Title = series?.GetSeriesName() ?? anime.PreferredTitle;
                    Titles = includeTitles ? anime.GetTitles().Select(title => new Title
                        {
                            Name = title.Title,
                            Language = title.Language,
                            Type = title.TitleType,
                            Default = string.Equals(title.Title, Title),
                            Source = "AniDB",
                        }
                    ).ToList() : null;
                    Description = anime.Description;
                    Restricted = anime.Restricted == 1;
                    EpisodeCount = anime.EpisodeCountNormal;
                }
                else if (AniDB_TitleHelper.Instance.TrySearchAnimeID(relation.RelatedAnimeID, out var result))
                {
                    Type = SeriesType.Unknown;
                    Title = result.MainTitle;
                    Titles = includeTitles ? result.Titles.Select(title => new Title
                        {
                            Language = title.TitleLanguage,
                            Name = title.Title,
                            Type = title.TitleType,
                            Default = string.Equals(title.Title, Title),
                            Source = "AniDB",
                        }
                    ).ToList() : null;
                    Description = null;
                    // If the other anime is present we assume they're of the same kind. Be it restricted or unrestricted.
                    if (RepoFactory.AniDB_Anime.TryGetByAnimeID(relation.AnimeID, out anime))
                        Restricted = anime.Restricted == 1;
                    else
                        Restricted = false;
                }
                else 
                {
                    Type = SeriesType.Unknown;
                    Titles = includeTitles ? new() : null;
                    Restricted = false;
                }
                Poster = GetAniDBPoster(relation.RelatedAnimeID);
                Rating = null;
                UserApproval = null;
                Relation = SeriesRelation.GetRelationTypeFromAnidbRelationType(relation.RelationType);
            }

            public AniDB(AniDB_Anime_Similar similar, bool includeTitles) : this(similar, null, includeTitles) { }

            public AniDB(AniDB_Anime_Similar similar, SVR_AnimeSeries series = null, bool includeTitles = true)
            {
                if (series == null)
                    series = RepoFactory.AnimeSeries.GetByAnimeID(similar.SimilarAnimeID);
                ID = similar.SimilarAnimeID;
                ShokoID = series?.AnimeSeriesID;
                if (RepoFactory.AniDB_Anime.TryGetByAnimeID(similar.SimilarAnimeID, out var anime))
                {
                    Type = GetAniDBSeriesType(anime.AnimeType);
                    Title = series?.GetSeriesName() ?? anime.PreferredTitle;
                    Titles = includeTitles ? anime.GetTitles().Select(title => new Title
                        {
                            Name = title.Title,
                            Language = title.Language,
                            Type = title.TitleType,
                            Default = string.Equals(title.Title, Title),
                            Source = "AniDB",
                        }
                    ).ToList() : null;
                    Description = anime.Description;
                    Restricted = anime.Restricted == 1;
                }
                else if (AniDB_TitleHelper.Instance.TrySearchAnimeID(similar.SimilarAnimeID, out var result))
                {
                    Type = SeriesType.Unknown;
                    Title = result.MainTitle;
                    Titles = includeTitles ? result.Titles.Select(title => new Title
                        {
                            Language = title.TitleLanguage,
                            Name = title.Title,
                            Type = title.TitleType,
                            Default = string.Equals(title.Title, Title),
                            Source = "AniDB",
                        }
                    ).ToList() : null;
                    Description = null;
                    // If the other anime is present we assume they're of the same kind. Be it restricted or unrestricted.
                    if (RepoFactory.AniDB_Anime.TryGetByAnimeID(similar.AnimeID, out anime))
                        Restricted = anime.Restricted == 1;
                    else
                        Restricted = false;
                }
                else
                {
                    Type = SeriesType.Unknown;
                    Title = null;
                    Titles = includeTitles ? new() : null;
                    Description = null;
                    Restricted = false;
                }
                Poster = GetAniDBPoster(similar.AnimeID);
                Rating = null;
                UserApproval = new Rating
                {
                    Value = new Vote(similar.Approval, similar.Total).GetRating(),
                    MaxValue = 10,
                    Votes = similar.Total,
                    Source = "AniDB",
                    Type = "User Approval",
                };
                Relation = null;
                Restricted = false;
            }

            /// <summary>
            /// AniDB ID
            /// </summary>
            [Required]
            public int ID { get; set; }

            /// <summary>
            /// <see cref="Series"/> ID if the series is available locally.
            /// </summary>
            /// <value></value>
            public int? ShokoID { get; set; }

            /// <summary>
            /// Series type. Series, OVA, Movie, etc
            /// </summary>
            [Required]
            [JsonConverter(typeof(StringEnumConverter))]
            public SeriesType Type { get; set; }

            /// <summary>
            /// Main Title, usually matches x-jat
            /// </summary>
            [Required]
            public string Title { get; set; }

            /// <summary>
            /// There should always be at least one of these, the <see cref="Title"/>.
            /// </summary>
            [Required]
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public List<Title> Titles { get; set; }

            /// <summary>
            /// Description.
            /// </summary>
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public string Description { get; set; }

            /// <summary>
            /// Restricted content. Mainly porn.
            /// </summary>
            public bool Restricted { get; set; }

            /// <summary>
            /// The main or default poster.
            /// </summary>
            [Required]
            public Image Poster { get; set; }

            /// <summary>
            /// Number of <see cref="EpisodeType.Normal"/> episodes contained within the series if it's known.
            /// </summary>
            public int? EpisodeCount { get; set; }

            /// <summary>
            /// The average rating for the anime.
            /// </summary>
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public Rating Rating { get; set; }

            /// <summary>
            /// User approval rate for the similar submission. Only available for similar.
            /// </summary>
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public Rating UserApproval { get; set; }

            /// <summary>
            /// Relation type. Only available for relations.
            /// </summary>
            [JsonConverter(typeof(StringEnumConverter))]
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public RelationType? Relation { get; set; }
        }

        /// <summary>
        /// The AniDB Data model for series
        /// </summary>
        public class AniDBWithDate : AniDB
        {
            public AniDBWithDate(HttpContext ctx, SVR_AniDB_Anime anime, SVR_AnimeSeries series = null) : base(anime, series)
            {
                if (anime.AirDate != null)
                {
                    var airdate = anime.AirDate.Value;
                    if (airdate != DateTime.MinValue)
                        AirDate = airdate;
                }
                if (anime.EndDate != null)
                {
                    var enddate = anime.EndDate.Value;
                    if (enddate != DateTime.MinValue)
                        EndDate = enddate;
                }
            }

            /// <summary>
            /// Air date (2013-02-27, shut up avael). Anything without an air date is going to be missing a lot of info.
            /// </summary>
            [Required]
            [JsonConverter(typeof(DateFormatConverter), "yyyy-MM-dd")]
            public DateTime? AirDate { get; set; }

            /// <summary>
            /// End date, can be omitted. Omitted means that it's still airing (2013-02-27)
            /// </summary>
            [JsonConverter(typeof(DateFormatConverter), "yyyy-MM-dd")]
            public DateTime? EndDate { get; set; }
        }

        /// <summary>
        /// The result entries for the "Recommended For You" algorithm.
        /// </summary>
        public class AniDBRecommendedForYou
        {
            /// <summary>
            /// The recommended AniDB entry.
            /// </summary>
            public AniDBWithDate Anime;

            /// <summary>
            /// Number of similar anime that resulted in this recommendation.
            /// </summary>
            public int SimilarTo;
        }

        /// <summary>
        /// The TvDB Data model for series
        /// </summary>
        public class TvDB
        {
            public TvDB(HttpContext ctx, TvDB_Series tbdbSeries, SVR_AnimeSeries series, List<SVR_AnimeEpisode> episodeList = null)
            {
                if (episodeList == null) {
                    episodeList = series.GetAnimeEpisodes();
                }
                ID = tbdbSeries.SeriesID;
                Description = tbdbSeries.Overview;
                Title = tbdbSeries.SeriesName;
                if (tbdbSeries.Rating != null)
                    Rating = new Rating {Source = "TvDB", Value = tbdbSeries.Rating.Value, MaxValue = 10};

                Images images = new Images();
                AddTvDBImages(ctx, images, series.AniDB_ID);
                Posters = images.Posters;
                Fanarts = images.Fanarts;
                Banners = images.Banners;

                // Aggregate stuff
                var firstEp = episodeList.FirstOrDefault(a =>
                        a.AniDB_Episode != null && (a.AniDB_Episode.EpisodeType == (int) AniDBEpisodeType.Episode &&
                                                    a.AniDB_Episode.EpisodeNumber == 1))
                    ?.TvDBEpisode;

                var lastEp = episodeList
                    .Where(a => a.AniDB_Episode != null && (a.AniDB_Episode.EpisodeType == (int) AniDBEpisodeType.Episode))
                    .OrderBy(a => a.AniDB_Episode.EpisodeType)
                    .ThenBy(a => a.AniDB_Episode.EpisodeNumber).LastOrDefault()
                    ?.TvDBEpisode;

                Season = firstEp?.SeasonNumber;
                AirDate = firstEp?.AirDate;
                EndDate = lastEp?.AirDate;
            }
            
            /// <summary>
            /// TvDB ID
            /// </summary>
            [Required]
            public int ID { get; set; }

            /// <summary>
            /// Air date (2013-02-27, shut up avael)
            /// </summary>
            [JsonConverter(typeof(DateFormatConverter), "yyyy-MM-dd")]
            public DateTime? AirDate { get; set; }

            /// <summary>
            /// End date, can be null. Null means that it's still airing (2013-02-27)
            /// </summary>
            [JsonConverter(typeof(DateFormatConverter), "yyyy-MM-dd")]
            public DateTime? EndDate { get; set; }

            /// <summary>
            /// TvDB only supports one title
            /// </summary>
            [Required]
            public string Title { get; set; }

            /// <summary>
            /// Description
            /// </summary>
            public string Description { get; set; }

            /// <summary>
            /// TvDB Season. This value is not guaranteed to be even kind of accurate
            /// TvDB matchings and links affect this. Null means no match. 0 means specials
            /// </summary>
            public int? Season { get; set; }

            /// <summary>
            /// Posters
            /// </summary>
            public List<Image> Posters { get; set; }

            /// <summary>
            /// Fanarts
            /// </summary>
            public List<Image> Fanarts { get; set; }

            /// <summary>
            /// Banners
            /// </summary>
            public List<Image> Banners { get; set; }

            /// <summary>
            /// The rating object
            /// </summary>
            public Rating Rating { get; set; }

        }

        /// <summary>
        /// A site link, as in hyperlink.
        /// </summary>
        public class Resource
        {
            /// <summary>
            /// site name
            /// </summary>
            [Required]
            public string name { get; set; }

            /// <summary>
            /// the url to the series page
            /// </summary>
            [Required]
            public string url { get; set; }

            /// <summary>
            /// favicon or something. A logo
            /// </summary>
            public Image image { get; set; }
        }
    }

    public class SeriesIDs : IDs
    {
        #region Groups

        /// <summary>
        /// The ID of the direct parent group, if it has one.
        /// </summary>
        public int ParentGroup { get; set; }

        /// <summary>
        /// The ID of the top-level (ancestor) group this series belongs to.
        /// </summary>
        public int TopLevelGroup { get; set; }

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
        public List<int> TvDB { get; set; } = new List<int>();

        // TODO Support for TvDB string IDs (like in the new URLs) one day maybe

        /// <summary>
        /// The Movie DB IDs
        /// </summary>
        public List<int> TMDB { get; set; } = new List<int>();

        /// <summary>
        /// The MyAnimeList IDs
        /// </summary>
        public List<int> MAL { get; set; } = new List<int>();

        /// <summary>
        /// The TraktTv IDs
        /// </summary>
        public List<string> TraktTv { get; set; } = new List<string>();

        /// <summary>
        /// The AniList IDs
        /// </summary>
        public List<int> AniList { get; set; } = new List<int>();
        #endregion
    }

    /// <summary>
    /// An Extended Series Model with Values for Search Results
    /// </summary>
    public class SeriesSearchResult : Series
    {
        /// <summary>
        /// What is the string we matched on
        /// </summary>
        public string Match { get; set; }

        /// <summary>
        /// The Sorensen-Dice Distance of the match
        /// </summary>
        public double Distance { get; set; }
        public SeriesSearchResult(HttpContext ctx, SVR_AnimeSeries ser, string match, double dist) : base(ctx, ser)
        {
            Match = match;
            Distance = dist;
        }
    }

    public enum SeriesType
    {
        /// <summary>
        /// The series type is unknown.
        /// </summary>
        Unknown = 0,
        /// <summary>
        /// A catch-all type for future extensions when a provider can't use a current episode type, but knows what the future type should be.
        /// </summary>
        Other = 1,
        /// <summary>
        /// Standard TV series.
        /// </summary>
        TV = 2,
        /// <summary>
        /// TV special.
        /// </summary>
        TVSpecial = 3,
        /// <summary>
        /// Web series.
        /// </summary>
        Web = 4,
        /// <summary>
        /// All movies, regardless of source (e.g. web or theater)
        /// </summary>
        Movie = 5,
        /// <summary>
        /// Original Video Animations, AKA standalone releases that don't air on TV or the web.
        /// </summary>
        OVA = 6,
    }

    /// <summary>
    /// Downloaded, Watched, Total, etc
    /// </summary>
    public class SeriesSizes
    {
        
        public SeriesSizes() : base()
        {
            FileSources = new();
            Total = new();
            Local = new();
            Watched = new();
        }

        /// <summary>
        /// Counts of each file source type available within the local colleciton
        /// </summary>
        [Required]
        public FileSourceCounts FileSources { get; set; }

        /// <summary>
        /// What is downloaded and available
        /// </summary>
        [Required]
        public EpisodeTypeCounts Local { get; set; }

        /// <summary>
        /// What is local and watched.
        /// </summary>
        public EpisodeTypeCounts Watched { get; set; }

        /// <summary>
        /// Total count of each type
        /// </summary>
        [Required]
        public EpisodeTypeCounts Total { get; set; }

        /// <summary>
        /// Lists the count of each type of episode.
        /// </summary>
        public class EpisodeTypeCounts
        {
            public int Unknown { get; set; }
            public int Episodes { get; set; }
            public int Specials { get; set; }
            public int Credits { get; set; }
            public int Trailers { get; set; }
            public int Parodies { get; set; }
            public int Others { get; set; }
        }

        public class FileSourceCounts
        {
            public int Unknown;
            public int Other;
            public int TV;
            public int DVD;
            public int BluRay;
            public int Web;
            public int VHS;
            public int VCD;
            public int LaserDisc;
            public int Camera;
        }
    }
}
