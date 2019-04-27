
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Shoko.Commons.Extensions;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Server.Models;
using Shoko.Server.Repositories;
// ReSharper disable UnusedMember.Local
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace Shoko.Server.API.v3
{
    /// <summary>
    /// Series object, stores all of the series info
    /// </summary>
    public class Series : BaseModel
    {
        /// <summary>
        /// The relevant IDs for the series, Shoko Internal, AniDB, etc
        /// </summary>
        public IDs IDs { get; set; }

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

        #region Constructors and Helper Methods

        public Series(HttpContext ctx, int id)
        {
            SVR_AnimeSeries series = RepoFactory.AnimeSeries.GetByID(id);
            GenerateFromAnimeSeries(ctx, series);
        }
        
        public void GenerateFromAnimeSeries(HttpContext ctx, SVR_AnimeSeries ser)
        {
            int uid = ctx.GetUser()?.JMMUserID ?? 0;

            AddBasicAniDBInfo(ctx, ser);

            List<SVR_AnimeEpisode> ael = ser.GetAnimeEpisodes();
            var contract = ser.Contract;
            if (contract == null) ser.UpdateContract();

            IDs = GetIDs(ser);
            Images = GetRandomImages(ctx, ser);

            Name = ser.GetSeriesName();
            Sizes = ModelHelper.GenerateSizes(ael, uid);
            Size = Sizes.Local.Credits + Sizes.Local.Episodes + Sizes.Local.Others + Sizes.Local.Parodies +
                   Sizes.Local.Specials + Sizes.Local.Trailers;
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
                    Value = (decimal) Math.Round(vote.VoteValue / 100D, 1), MaxValue = 10, Type = voteType,
                    Source = "User"
                };
            }
        }
        
        public static IDs GetIDs(SVR_AnimeSeries ser)
        {
            // Shoko
            var ids = new IDs {ID = ser.AnimeSeriesID};
            // AniDB
            var anime = ser.GetAnime();
            if (anime != null) ids.AniDB = anime.AnimeID;
            // TvDB
            var tvdbs = RepoFactory.CrossRef_AniDB_TvDB.GetByAnimeID(ser.AniDB_ID);
            if (tvdbs.Any()) ids.TvDB.AddRange(tvdbs.Select(a => a.TvDBID));
            // MovieDB
            // TODO make this able to support more than one, in fact move it to its own and remove CrossRef_Other
            var moviedb = RepoFactory.CrossRef_AniDB_Other.GetByAnimeIDAndType(ser.AniDB_ID, CrossRefType.MovieDB);
            if (moviedb != null && int.TryParse(moviedb.CrossRefID, out int movieID)) ids.MovieDB.Add(movieID);
            // Trakt
            var traktids = RepoFactory.CrossRef_AniDB_TraktV2.GetByAnimeID(ser.AniDB_ID).Select(a => a.TraktID)
                .Distinct().ToList();
            if (traktids.Any()) ids.TraktTv.AddRange(traktids);
            // MAL
            var malids = RepoFactory.CrossRef_AniDB_MAL.GetByAnimeID(ser.AniDB_ID).Select(a => a.MALID).Distinct()
                .ToList();
            if (malids.Any()) ids.MAL.AddRange(malids);
            // TODO AniList later
            return ids;
        }

        private static Images GetRandomImages(HttpContext ctx, SVR_AnimeSeries ser, bool randomizePoster = false)
        {
            Images images = new Images();
            Random rand = ctx.Items["Random"] as Random;
            
            var allImages = GetArt(ctx, ser.AniDB_ID, false);
            var defaultPoster = RepoFactory.AniDB_Anime_DefaultImage.GetByAnimeIDAndImagezSizeType(ser.AniDB_ID,
                (int) ImageSizeType.Poster);
            
            if (defaultPoster != null)
            {
                images.Posters.Add(new Image(ctx, defaultPoster.ImageParentID,
                    (ImageEntityType) defaultPoster.ImageParentType, true));
            }
            else
            {
                var poster = allImages.Posters.GetRandomElement(rand);
                // This should never be null, since AniDB should have a poster
                if (poster != null)
                    images.Posters.Add(poster);
            }

            var fanart = allImages.Fanarts.GetRandomElement(rand);
            if (fanart != null) images.Fanarts.Add(fanart);
            
            var banner = allImages.Banners.GetRandomElement(rand);
            if (banner != null) images.Banners.Add(banner);

            return images;
        }

        /// <summary>
        /// Cast is aggregated, and therefore not in each provider
        /// </summary>
        /// <param name="ctx"></param>
        /// <param name="animeID"></param>
        /// <returns></returns>
        public static List<Role> GetCast(HttpContext ctx, int animeID)
        {
            List<Role> roles = new List<Role>();
            var xrefAnimeStaff = RepoFactory.CrossRef_Anime_Staff.GetByAnimeIDAndRoleType(animeID,
                StaffRoleType.Seiyuu);
            foreach (var xref in xrefAnimeStaff)
            {
                if (xref.RoleID == null) continue;
                AnimeCharacter character = RepoFactory.AnimeCharacter.GetByID(xref.RoleID.Value);
                if (character == null) continue;
                AnimeStaff staff = RepoFactory.AnimeStaff.GetByID(xref.StaffID);
                if (staff == null) continue;
                Role role = new Role
                {
                    Character = new Role.Person
                    {
                        Name = character.Name,
                        AlternateName = character.AlternateName,
                        Image = new Image(ctx, xref.RoleID.Value, ImageEntityType.Character),
                        Description = character.Description
                    },
                    Staff = new Role.Person
                    {
                        Name = staff.Name,
                        AlternateName = staff.AlternateName,
                        Description = staff.Description,
                        Image = new Image(ctx, xref.StaffID, ImageEntityType.Staff),
                    },
                    RoleName = ((StaffRoleType) xref.RoleType).ToString(),
                    RoleDetails = xref.Role
                };
                roles.Add(role);
            }

            return roles;
        }

        public static List<Tag> GetTags(HttpContext ctx, SVR_AniDB_Anime anime, TagFilter.Filter filter, bool excludeDescriptions = false)
        {
            // TODO This is probably slow. Make it faster.
            var tags = new List<Tag>();
            
            var allTags = anime.GetAniDBTags().DistinctBy(a => a.TagName).ToDictionary(a => a.TagName, a => a);
            var filteredTags = TagFilter.ProcessTags(filter, allTags.Keys.ToList());
            foreach (string filteredTag in filteredTags)
            {
                AniDB_Tag tag;
                tag = allTags.ContainsKey(filteredTag)
                    ? allTags[filteredTag]
                    : RepoFactory.AniDB_Tag.GetByName(filteredTag).FirstOrDefault();
                if (tag == null) continue;
                var toAPI = new Tag
                {
                    Name = tag.TagName
                };
                if (!excludeDescriptions) toAPI.Description = tag.TagDescription;
                tags.Add(toAPI);
            }
            
            return tags;
        }
        
        public static AniDB GetAniDBInfo(HttpContext ctx, SVR_AniDB_Anime anime)
        {
            var aniDB = new AniDB
            {
                ID = anime.AnimeID,
                SeriesType = anime.AnimeType.ToString(),
                Restricted = anime.Restricted == 1,
                Description = anime.Description,
                Rating = new Rating
                {
                    Source = "AniDB",
                    Value = anime.Rating,
                    MaxValue = 1000,
                    Votes = anime.VoteCount
                },
                Title = anime.MainTitle,
                Titles = anime.GetTitles().Select(title => new Title
                    {Language = title.Language, Name = title.Title, Type = title.TitleType}).ToList(),
            };

            if (anime.AirDate != null)
            {
                var airdate = anime.AirDate.Value;
                if (airdate != DateTime.MinValue)
                    aniDB.AirDate = airdate.ToString("yyyy-MM-dd");
            }
            if (anime.EndDate != null)
            {
                var enddate = anime.EndDate.Value;
                if (enddate != DateTime.MinValue)
                    aniDB.EndDate = enddate.ToString("yyyy-MM-dd");
            }
            
            // Add Poster
            v3.Images images = new Images();
            AddAniDBPoster(ctx, images, anime.AnimeID);
            aniDB.Poster = images.Posters.FirstOrDefault();

            return aniDB;
        }

        public static List<TvDB> GetTvDBInfo(HttpContext ctx, SVR_AnimeSeries ser)
        {
            List<SVR_AnimeEpisode> ael = ser.GetAnimeEpisodes();
            List<TvDB> models = new List<TvDB>();
            var tvdbs = RepoFactory.CrossRef_AniDB_TvDB.GetByAnimeID(ser.AniDB_ID);
            foreach (var tvdb in tvdbs)
            {
                var tvdbSer = RepoFactory.TvDB_Series.GetByTvDBID(tvdb.TvDBID);
                TvDB model = new TvDB
                {
                    ID = tvdb.TvDBID, Description = tvdbSer.Overview, Title = tvdbSer.SeriesName
                };
                if (tvdbSer.Rating != null)
                    model.Rating = new Rating {Source = "TvDB", Value = tvdbSer.Rating.Value, MaxValue = 10};
                
                v3.Images images = new Images();
                AddTvDBImages(ctx, images, ser.AniDB_ID);
                model.Posters = images.Posters;
                model.Fanarts = images.Fanarts;
                model.Banners = images.Banners;
                
                // Aggregate stuff
                var firstEp = ael.FirstOrDefault(a =>
                        a.AniDB_Episode != null && (a.AniDB_Episode.EpisodeType == (int) EpisodeType.Episode &&
                                                    a.AniDB_Episode.EpisodeNumber == 1))
                    ?.TvDBEpisode;

                var lastEp = ael
                    .Where(a => a.AniDB_Episode != null && (a.AniDB_Episode.EpisodeType == (int) EpisodeType.Episode))
                    .OrderBy(a => a.AniDB_Episode.EpisodeType)
                    .ThenBy(a => a.AniDB_Episode.EpisodeNumber).LastOrDefault()
                    ?.TvDBEpisode;

                model.Season = firstEp?.SeasonNumber;
                model.AirDate = firstEp?.AirDate?.ToString("yyyy-MM-dd");
                model.EndDate = lastEp?.AirDate?.ToString("yyyy-MM-dd");
                
                models.Add(model);
            }
            
            return models;
        }

        public static Images GetArt(HttpContext ctx, int animeID, bool includeDisabled = false)
        {
            var images = new Images();
            AddAniDBPoster(ctx, images, animeID);
            AddTvDBImages(ctx, images, animeID, includeDisabled);
            AddMovieDBImages(ctx, images, animeID, includeDisabled);

            return images;
        }

        private static void AddAniDBPoster(HttpContext ctx, Images images, int animeID)
        {
            var defaultImage = RepoFactory.AniDB_Anime_DefaultImage.GetByAnimeIDAndImagezSizeType(animeID,
                (int) ImageEntityType.AniDB_Cover);
            bool preferred = defaultImage != null;
            images.Posters.Add(new Image(ctx, animeID, ImageEntityType.AniDB_Cover, preferred));
        }

        private static void AddTvDBImages(HttpContext ctx, Images images, int animeID, bool includeDisabled = false)
        {
            var tvdbIDs = RepoFactory.CrossRef_AniDB_TvDB.GetByAnimeID(animeID).ToList();
            var fanarts = tvdbIDs.SelectMany(a => RepoFactory.TvDB_ImageFanart.GetBySeriesID(a.TvDBID)).ToList();
            var banners = tvdbIDs.SelectMany(a => RepoFactory.TvDB_ImageWideBanner.GetBySeriesID(a.TvDBID)).ToList();
            images.Fanarts.AddRange(fanarts.Where(a => includeDisabled || a.Enabled != 0).Select(a =>
            {
                var defaultImage = RepoFactory.AniDB_Anime_DefaultImage.GetByAnimeIDAndImagezSizeType(animeID,
                    (int) ImageEntityType.TvDB_FanArt);
                bool preferred = defaultImage != null;
                return new Image(ctx, a.TvDB_ImageFanartID, ImageEntityType.TvDB_FanArt, preferred, a.Enabled == 0);
            }));

            images.Banners.AddRange(banners.Where(a => includeDisabled || a.Enabled != 0).Select(a =>
            {
                var defaultImage = RepoFactory.AniDB_Anime_DefaultImage.GetByAnimeIDAndImagezSizeType(animeID,
                    (int) ImageEntityType.TvDB_Banner);
                bool preferred = defaultImage != null;
                return new Image(ctx, a.TvDB_ImageWideBannerID, ImageEntityType.TvDB_Banner, preferred, a.Enabled == 0);
            }));
        }

        private static void AddMovieDBImages(HttpContext ctx, Images images, int animeID, bool includeDisabled = false)
        {
            var moviedb = RepoFactory.CrossRef_AniDB_Other.GetByAnimeIDAndType(animeID, CrossRefType.MovieDB);
            
            List<MovieDB_Poster> moviedbPosters = moviedb == null
                ? new List<MovieDB_Poster>()
                : RepoFactory.MovieDB_Poster.GetByMovieID(int.Parse(moviedb.CrossRefID));
            
            List<MovieDB_Fanart> moviedbFanarts = moviedb == null
                ? new List<MovieDB_Fanart>()
                : RepoFactory.MovieDB_Fanart.GetByMovieID(int.Parse(moviedb.CrossRefID));
            
            images.Posters.AddRange(moviedbPosters.Where(a => includeDisabled || a.Enabled != 0).Select(a =>
            {
                var defaultImage = RepoFactory.AniDB_Anime_DefaultImage.GetByAnimeIDAndImagezSizeType(animeID,
                    (int) ImageEntityType.MovieDB_FanArt);
                bool preferred = defaultImage != null;
                return new Image(ctx, a.MovieDB_PosterID, ImageEntityType.MovieDB_Poster, preferred, a.Enabled == 1);
            }));

            images.Fanarts.AddRange(moviedbFanarts.Where(a => includeDisabled || a.Enabled != 0).Select(a =>
            {
                var defaultImage = RepoFactory.AniDB_Anime_DefaultImage.GetByAnimeIDAndImagezSizeType(animeID,
                    (int) ImageEntityType.MovieDB_FanArt);
                bool preferred = defaultImage != null;
                return new Image(ctx, a.MovieDB_FanartID, ImageEntityType.MovieDB_FanArt, preferred, a.Enabled == 1);
            }));
        }

        #endregion

        /// <summary>
        /// The AniDB Data model for series
        /// </summary>
        public class AniDB
        {
            /// <summary>
            /// AniDB ID
            /// </summary>
            [Required]
            public int ID { get; set; }
            
            /// <summary>
            /// Series type. Series, OVA, Movie, etc
            /// </summary>
            [Required]
            public string SeriesType { get; set; }

            /// <summary>
            /// Main Title, usually matches x-jat
            /// </summary>
            [Required]
            public string Title { get; set; }
            
            /// <summary>
            /// Is it porn...or close enough
            /// If not provided, assume no
            /// </summary>
            public bool Restricted { get; set; }

            /// <summary>
            /// Air date (2013-02-27, shut up avael)
            /// </summary>
            [Required]
            public string AirDate { get; set; }

            /// <summary>
            /// End date, can be null. Null means that it's still airing (2013-02-27)
            /// </summary>
            public string EndDate { get; set; }

            /// <summary>
            /// There should always be at least one of these, since name will be valid
            /// </summary>
            public List<Title> Titles { get; set; }

            /// <summary>
            /// Description
            /// </summary>
            public string Description { get; set; }

            /// <summary>
            /// The default poster
            /// </summary>
            public Image Poster { get; set; }

            /// <summary>
            /// The rating object
            /// </summary>
            public Rating Rating { get; set; }

        }
        
        /// <summary>
        /// The TvDB Data model for series
        /// </summary>
        public class TvDB
        {
            /// <summary>
            /// TvDB ID
            /// </summary>
            [Required]
            public int ID { get; set; }

            /// <summary>
            /// Air date (2013-02-27, shut up avael)
            /// </summary>
            public string AirDate { get; set; }

            /// <summary>
            /// End date, can be null. Null means that it's still airing (2013-02-27)
            /// </summary>
            public string EndDate { get; set; }

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
}
