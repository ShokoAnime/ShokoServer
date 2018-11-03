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

namespace Shoko.Server.API.v3
{
    /// <summary>
    /// Series object, stores all of the series info
    /// </summary>
    public class Series : BaseModel
    {
        public override string type => "series";

        /// <summary>
        /// AniDB_ID
        /// </summary>
        [Required]
        public int anidb_id { get; set; }

        /// <summary>
        /// The group ID, for easy lookup
        /// While required, it is possible (a problem but possible) for a series to not have a group
        /// In this case, the id will be 0
        /// </summary>
        [Required]
        public int group_id { get; set; }

        /// <summary>
        /// Series type. Series, OVA, Movie, etc
        /// </summary>
        [Required]
        public string series_type { get; set; }

        /// <summary>
        /// Is it porn...or close enough
        /// If not provided, assume no
        /// </summary>
        public bool restricted { get; set; }

        /// <summary>
        /// Air date (2013-02-27, shut up avael)
        /// </summary>
        [Required]
        public string air_date { get; set; }

        /// <summary>
        /// End date, can be null. Null means that it's still airing (2013-02-27)
        /// </summary>
        public string end_date { get; set; }
        
        /// <summary>
        /// TvDB Season. This value is not guaranteed to be even kind of accurate
        /// TvDB matchings and links affect this. Null means no match. 0 means specials
        /// </summary>
        public int? season { get; set; }

        /// <summary>
        /// There should always be at least one of these, since name will be valid
        /// </summary>
        public List<Title> titles { get; set; }

        /// <summary>
        /// Description, probably only 2 or 3
        /// </summary>
        public List<Description> description { get; set; }

        /// <summary>
        /// The default poster, with override
        /// </summary>
        public Image preferred_poster { get; set; }
        
        /// <summary>
        /// The rest of the images, including posters, fanarts, and banners
        /// They have the fields for the client to filter on
        /// </summary>
        public List<Image> images { get; set; }

        /// <summary>
        /// The ratings object, with info of ratings from various sources
        /// </summary>
        public List<Rating> ratings { get; set; }
        
        /// <summary>
        /// the user's rating
        /// </summary>
        public Rating user_rating { get; set; }
        
        /// <summary>
        /// tags
        /// </summary>
        public List<string> tags { get; set; }
        
        /// <summary>
        /// series cast and staff
        /// </summary>
        public List<Role> cast { get; set; }

        /// <summary>
        /// links to series pages on various sites
        /// </summary>
        public List<Resource> links { get; set; }

        #region Constructors and Helper Methods

        public Series(HttpContext ctx, int id)
        {
            SVR_AnimeSeries series = Repo.Instance.AnimeSeries.GetByID(id);
            GenerateFromAnimeSeries(ctx, series);
        }
        
        public void GenerateFromAniDB_Anime(HttpContext ctx, SVR_AniDB_Anime anime)
        {
            anidb_id = anime.AnimeID;
            restricted = anime.Restricted == 1;
            if (ctx.Items.ContainsKey("description"))
            {
                description = new List<Description>
                {
                    new Description {source = "AniDB", language = "en", description = anime.Description}
                };
            }

            ratings = new List<Rating>
            {
                new Rating
                {
                    source = "AniDB",
                    rating = (decimal) anime.Rating / 10,
                    max_rating = 100,
                    votes = anime.VoteCount
                }
            };
            name = anime.MainTitle;
            series_type = anime.AnimeType.ToString();
            
            if (anime.AirDate != null)
            {
                var airdate = anime.AirDate.Value;
                if (airdate != DateTime.MinValue)
                    air_date = airdate.ToString("yyyy-MM-dd");
            }
            if (anime.EndDate != null)
            {
                var enddate = anime.EndDate.Value;
                if (enddate != DateTime.MinValue)
                    end_date = enddate.ToString("yyyy-MM-dd");
            }

            AniDB_Vote vote = Repo.Instance.AniDB_Vote.GetByEntityAndType(anime.AnimeID, AniDBVoteType.Anime) ??
                              Repo.Instance.AniDB_Vote.GetByEntityAndType(anime.AnimeID, AniDBVoteType.AnimeTemp);
            if (vote != null)
            {
                string voteType = (AniDBVoteType) vote.VoteType == AniDBVoteType.Anime ? "Permanent" : "Temporary";
                user_rating = new Rating
                {
                    rating = (decimal) Math.Round(vote.VoteValue / 100D, 1), max_rating = 10, type = voteType,
                    source = "User"
                };
            }

            if (ctx.Items.ContainsKey("titles"))
                titles = anime.GetTitles().Select(title => new Title
                    {language = title.Language, title = title.Title, type = title.TitleType}).ToList();

            PopulateArtFromAniDBAnime(ctx, anime);

            if (ctx.Items.ContainsKey("cast"))
            {
                var xref_animestaff = Repo.Instance.CrossRef_Anime_Staff.GetByAnimeIDAndRoleType(anime.AnimeID,
                    StaffRoleType.Seiyuu);
                foreach (var xref in xref_animestaff)
                {
                    if (xref.RoleID == null) continue;
                    AnimeCharacter character = Repo.Instance.AnimeCharacter.GetByID(xref.RoleID.Value);
                    if (character == null) continue;
                    AnimeStaff staff = Repo.Instance.AnimeStaff.GetByID(xref.StaffID);
                    if (staff == null) continue;
                    Role role = new Role
                    {
                        character =  new Role.Person
                        {
                            name = character.Name, alternate_name = character.AlternateName,
                            image = new Image(ctx, xref.RoleID.Value, ImageEntityType.Character),
                            description = character.Description
                        },
                        staff = new Role.Person
                        {
                            name = staff.Name,
                            alternate_name = staff.AlternateName,
                            description = staff.Description,
                            image = new Image(ctx, xref.StaffID, ImageEntityType.Staff),
                        },
                        role = ((StaffRoleType) xref.RoleType).ToString(),
                        role_details = xref.Role
                    };
                    if (cast == null) cast = new List<Role>();
                    cast.Add(role);
                }
            }

            if (ctx.Items.ContainsKey("tags"))
            {
                var animeTags = anime.GetAllTags();
                if (animeTags != null)
                {
                    if (!ctx.Items.TryGetValue("tagfilter", out object tagfilter)) tagfilter = 0;
                    tags = TagFilter.ProcessTags((TagFilter.Filter) tagfilter, animeTags.ToList());
                }
            }
        }

        public void GenerateFromAnimeSeries(HttpContext ctx, SVR_AnimeSeries ser)
        {
            // TODO Add Links
            int uid = ctx.GetUser()?.JMMUserID ?? 0;
            GenerateFromAniDB_Anime(ctx, ser.GetAnime());

            List<SVR_AnimeEpisode> ael = ser.GetAnimeEpisodes();
            var contract = ser.Contract;
            if (contract == null) ser.UpdateContract();

            id = ser.AnimeSeriesID;
            group_id = ser.AnimeGroupID;
            if (Repo.Instance.AnimeGroup.GetByID(group_id) == null) group_id = 0;

            name = ser.GetSeriesName();
            GenerateSizes(ael, uid);

            season = ael.FirstOrDefault(a =>
                    a.AniDB_Episode != null && (a.AniDB_Episode.EpisodeType == (int) EpisodeType.Episode && a.AniDB_Episode.EpisodeNumber == 1))
                ?.TvDBEpisode?.SeasonNumber;

            var tvdbseriesID = ael.Select(a => a.TvDBEpisode).Where(a => a != null).GroupBy(a => a.SeriesID)
                .MaxBy(a => a.Count()).FirstOrDefault()?.Key;
            if (tvdbseriesID != null)
            {
                var tvdbseries = Repo.Instance.TvDB_Series.GetByTvDBID(tvdbseriesID.Value);
                if (tvdbseries != null)
                {
                    if (ctx.Items.ContainsKey("titles"))
                        titles.Add(new Title
                            {language = "en", type = "official", title = tvdbseries.SeriesName, source = "TvDB"});

                    if (ctx.Items.ContainsKey("description"))
                    {
                        description.Add(new Description
                        {
                            source = "TvDB", language = ServerSettings.Instance.TvDB.Language,
                            description = tvdbseries.Overview
                        });
                    }

                    if (tvdbseries.Rating != null)
                        ratings.Add(new Rating {source = "TvDB", rating = tvdbseries.Rating.Value, max_rating = 100});
                }
            }
        }

        private void GenerateSizes(List<SVR_AnimeEpisode> ael, int uid)
        {
            int eps = 0;
            int credits = 0;
            int specials = 0;
            int trailers = 0;
            int parodies = 0;
            int others = 0;

            int local_eps = 0;
            int local_credits = 0;
            int local_specials = 0;
            int local_trailers = 0;
            int local_parodies = 0;
            int local_others = 0;

            int watched_eps = 0;
            int watched_credits = 0;
            int watched_specials = 0;
            int watched_trailers = 0;
            int watched_parodies = 0;
            int watched_others = 0;

            // single loop. Will help on long shows
            foreach (SVR_AnimeEpisode ep in ael)
            {
                if (ep?.AniDB_Episode == null) continue;
                var local = ep.GetVideoLocals().Any();
                bool watched = (ep.GetUserRecord(uid)?.WatchedCount ?? 0) > 0;
                switch (ep.EpisodeTypeEnum)
                {
                    case EpisodeType.Episode:
                    {
                        eps++;
                        if (local) local_eps++;
                        if (watched) watched_eps++;
                        break;
                    }
                    case EpisodeType.Credits:
                    {
                        credits++;
                        if (local) local_credits++;
                        if (watched) watched_credits++;
                        break;
                    }
                    case EpisodeType.Special:
                    {
                        specials++;
                        if (local) local_specials++;
                        if (watched) watched_specials++;
                        break;
                    }
                    case EpisodeType.Trailer:
                    {
                        trailers++;
                        if (local) local_trailers++;
                        if (watched) watched_trailers++;
                        break;
                    }
                    case EpisodeType.Parody:
                    {
                        parodies++;
                        if (local) local_parodies++;
                        if (watched) watched_parodies++;
                        break;
                    }
                    case EpisodeType.Other:
                    {
                        others++;
                        if (local) local_others++;
                        if (watched) watched_others++;
                        break;
                    }
                }
            }

            size = local_eps + local_credits + local_specials + local_trailers + local_parodies + local_others;

            sizes = new Sizes
            {
                total =
                    new Sizes.EpisodeCounts()
                    {
                        episodes = eps,
                        credits = credits,
                        specials = specials,
                        trailers = trailers,
                        parodies = parodies,
                        others = others
                    },
                local = new Sizes.EpisodeCounts()
                {
                    episodes = local_eps,
                    credits = local_credits,
                    specials = local_specials,
                    trailers = local_trailers,
                    parodies = local_parodies,
                    others = local_others
                },
                watched = new Sizes.EpisodeCounts()
                {
                    episodes = watched_eps,
                    credits = watched_credits,
                    specials = watched_specials,
                    trailers = watched_trailers,
                    parodies = watched_parodies,
                    others = watched_others
                }
            };


        }

        private void PopulateArtFromAniDBAnime(HttpContext ctx, SVR_AniDB_Anime anime)
        {
            Random rand = (Random) ctx.Items["Random"];
            // TODO Makes this more safe
            preferred_poster = new Image(ctx, anidb_id, ImageEntityType.AniDB_Cover);
            var defaultPoster = Repo.Instance.AniDB_Anime_DefaultImage.GetByAnimeIDAndImagezSizeType(anime.AnimeID,
                (int) ImageSizeType.Poster);
            if (defaultPoster != null)
                preferred_poster = new Image(ctx, defaultPoster.ImageParentID,
                    (ImageEntityType) defaultPoster.ImageParentType);

            var tvdbIDs = Repo.Instance.CrossRef_AniDB_TvDB.GetByAnimeID(anime.AnimeID).ToList();
            var fanarts = tvdbIDs.SelectMany(a => Repo.Instance.TvDB_ImageFanart.GetBySeriesID(a.TvDBID)).ToList();
            var banners = tvdbIDs.SelectMany(a => Repo.Instance.TvDB_ImageWideBanner.GetBySeriesID(a.TvDBID)).ToList();

            var moviedb = Repo.Instance.CrossRef_AniDB_Other.GetByAnimeIDAndType(anime.AnimeID, CrossRefType.MovieDB);
            List<MovieDB_Fanart> moviedbFanarts = moviedb == null
                ? new List<MovieDB_Fanart>()
                : Repo.Instance.MovieDB_Fanart.GetByMovieID(int.Parse(moviedb.CrossRefID));
            
            if (ctx.Items.ContainsKey("images"))
            {
                var posters = anime.AllPosters;
                images.AddRange(posters.Select(a =>
                    new Image(ctx, a.AniDB_Anime_DefaultImageID, (ImageEntityType) a.ImageType)));

                images.AddRange(fanarts.Select(a => new Image(ctx, a.TvDB_ImageFanartID, ImageEntityType.TvDB_FanArt)));

                images.AddRange(banners.Select(a =>
                    new Image(ctx, a.TvDB_ImageWideBannerID, ImageEntityType.TvDB_Banner)));

                images.AddRange(moviedbFanarts.Select(a =>
                    new Image(ctx, a.MovieDB_FanartID, ImageEntityType.MovieDB_FanArt)));
            }
            else
            {
                object fanart_object = tvdbIDs.SelectMany(a => Repo.Instance.TvDB_ImageFanart.GetBySeriesID(a.TvDBID))
                    .Cast<object>().Concat(moviedbFanarts).GetRandomElement(rand);

                if (fanart_object is TvDB_ImageFanart tvdb_fanart)
                    images.Add(new Image(ctx, tvdb_fanart.TvDB_ImageFanartID, ImageEntityType.TvDB_FanArt));
                else if (fanart_object is MovieDB_Fanart moviedb_fanart)
                    images.Add(new Image(ctx, moviedb_fanart.MovieDB_FanartID, ImageEntityType.MovieDB_FanArt));
                
                var banner = tvdbIDs.SelectMany(a => Repo.Instance.TvDB_ImageWideBanner.GetBySeriesID(a.TvDBID))
                    .GetRandomElement(rand);
                if (banner != null)
                    images.Add(new Image(ctx, banner.TvDB_ImageWideBannerID, ImageEntityType.TvDB_Banner));
            }
        }

        #endregion

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