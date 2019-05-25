using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using System;
using System.Collections.Generic;
using System.Text;

namespace Shoko.Database
{
    public class ShokoDBContext : DbContext
    {
        #region AniDB
        public DbSet<Models.AniDb.Anime> AniDbAnime { get; set; }
        public DbSet<Models.AniDb.AnimeUpdate> AniDbAnimeUpdate { get; set; }
        public DbSet<Models.AniDb.AnimeCategory> AniDbAnimeCategory { get; set; }
        public DbSet<Models.AniDb.AnimeCharacter> AniDbAnimeCharacter { get; set; }
        public DbSet<Models.AniDb.AnimeDefaultImage> AniDbAnimeDefaultImage { get; set; }
        public DbSet<Models.AniDb.AnimeRelation> AniDbAnimeRelation { get; set; }
        public DbSet<Models.AniDb.AnimeReview> AniDbAnimeReview { get; set; }
        public DbSet<Models.AniDb.AnimeSimilar> AniDbAnimeSimilar { get; set; }
        public DbSet<Models.AniDb.AnimeTag> AniDbAnimeTag { get; set; }
        public DbSet<Models.AniDb.AnimeTitle> AniDbAnimeTitle { get; set; }
        public DbSet<Models.AniDb.Category> AniDbCategory { get; set; }
        public DbSet<Models.AniDb.Character> AniDbCharacter { get; set; }
        public DbSet<Models.AniDb.CharacterSeiyuu> AniDbCharacterSeiyuu { get; set; }
        public DbSet<Models.AniDb.Episode> AniDbEpisode { get; set; }
        public DbSet<Models.AniDb.EpisodeTitle> AniDbEpisodeTitle { get; set; }
        public DbSet<Models.AniDb.File> AniDbFile { get; set; }
        public DbSet<Models.AniDb.GroupStatus> AniDbGroupStatus { get; set; }
        public DbSet<Models.AniDb.MyListStatus> AniDbMylistStatus { get; set; }
        public DbSet<Models.AniDb.Recommendation> AniDbReccomendation { get; set; }
        public DbSet<Models.AniDb.Seiyuu> AniDbSeiuu { get; set; }
        public DbSet<Models.AniDb.Tag> AniDbTag { get; set; }
        public DbSet<Models.AniDb.Vote> AniDbVote { get; set; }
        #endregion


        public DbSet<Models.AnimeCharacter> AnimeCharacter { get; set; }
        public DbSet<Models.AnimeEpisode> AnimeEpisode { get; set; }
        public DbSet<Models.User.AnimeEpisode> AnimeEpisodeUser { get; set; }
        public DbSet<Models.AnimeGroup> AnimeGroup { get; set; }
        public DbSet<Models.User.AnimeGroup> AnimeGroupUser { get; set; }
        public DbSet<Models.AnimeSeries> AnimeSeries { get; set; }
        public DbSet<Models.User.AnimeSeries> AnimeSeriesUser { get; set; }
        public DbSet<Models.AnimeStaff> AnimeStaff { get; set; }
        public DbSet<Models.AuthToken> AuthTokens { get; set; }
        public DbSet<Models.BookmarkedAnime> BookmarkedAnime { get; set; }
        public DbSet<Models.CloudAccount> CloudAccount { get; set; }
        public DbSet<Models.CommandRequest> CommandRequest { get; set; }

        #region Crossref

        public DbSet<Models.Crossref.AniDbMAL> CrossRefAniDBMAL { get; set; }
        public DbSet<Models.Crossref.AniDbOther> CrossRefAniDBOther { get; set; }
        public DbSet<Models.Crossref.AniDbTrakt> CrossRefAniDBTrakt { get; set; }
        public DbSet<Models.Crossref.AniDBTraktEpisode> CrossRefAniDbTraktEpisode { get; set; }
        public DbSet<Models.Crossref.AniDbTraktV2> CrossRefAniDbTraktV2 { get; set; }
        public DbSet<Models.Crossref.AniDBTvDB> CrossRefAniDBTvDB { get; set; }
        public DbSet<Models.Crossref.AniDBTvDBEpisode> CrossRefAniDBTvDBEpisode { get; set; }
        public DbSet<Models.Crossref.AniDBTvDBEpisodeOverride> CrossRefAniDBTvDBEpisodeOverride { get; set; }
        public DbSet<Models.Crossref.AnimeStaff> CrossRefAnimeStaff { get; set; }
        public DbSet<Models.Crossref.CustomTag> CrossRefCustomTag { get; set; }
        public DbSet<Models.Crossref.FileEpisode> CrossRefFileEpisode { get; set; }
        public DbSet<Models.Crossref.LanguagesAniDBFile> CrossRefLanguagesAniDBFile { get; set; }
        public DbSet<Models.Crossref.SubtitlesAniDBFile> CrossRefSubtitlesAniDBFile { get; set; }

        #endregion


        public DbSet<Models.CustomTag> CustomTag { get; set; }
        public DbSet<Models.DuplicateFile> DuplicateFile { get; set; }
        public DbSet<Models.FileFfdshowPreset> FileFfdshowPreset { get; set; }
        public DbSet<Models.FileNameHash> FileNameHash { get; set; }
        public DbSet<Models.GroupFilter> GroupFilter { get; set; }
        public DbSet<Models.GroupFilterCondition> GroupFilterCondition { get; set; }
        public DbSet<Models.IgnoreAnime> IgnoreAnime { get; set; }
        public DbSet<Models.ImportFolder> ImportFolder { get; set; }
        public DbSet<Models.ShokoUser> ShokoUsers { get; set; }
        public DbSet<Models.Language> Language { get; set; }
        public DbSet<Models.MovieDB.Fanart> MovieDBFanarts { get; set; }
        public DbSet<Models.MovieDB.Movie> MovieDBMovie { get; set; }
        public DbSet<Models.MovieDB.Poster> MovieDBPoster { get; set; }
        public DbSet<Models.Playlist> Playlist { get; set; }
        public DbSet<Models.RenameScript> RenameScript { get; set; }
        public DbSet<Models.Scan> Scan { get; set; }
        public DbSet<Models.ScanFile> ScanFile { get; set; }
        public DbSet<Models.ScheduledUpdate> ScheduledUpdate { get; set; }
        public DbSet<Models.Trakt.Episode> TraktEpisodes { get; set; }
        public DbSet<Models.Trakt.Friend> TraktFriends { get; set; }
        public DbSet<Models.Trakt.Season> TraktSeasons { get; set; }
        public DbSet<Models.TvDB.Episode> TvDBEpisodes { get; set; }
        public DbSet<Models.TvDB.ImageFanart> TvDBImageFanarts { get; set; }
        public DbSet<Models.TvDB.ImagePoster> TvDBImagePosters { get; set; }
        public DbSet<Models.TvDB.ImageWideBanner> TvDBImageWideBanners { get; set; }
        public DbSet<Models.TvDB.Series> TvDBSeries { get; set; }



        public DbSet<Models.VideoLocalPlace> VideoLocalPlace { get; set; }
        public DbSet<Models.VideoLocal> VideoLocal { get; set; }

        public ShokoDBContext(DbContextOptions options) : base(options)
        {
        }

        public ShokoDBContext()
        {
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            base.OnConfiguring(optionsBuilder);
            optionsBuilder
                .UseLazyLoadingProxies()
                .UseMySql("");
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            //Because Fluent API only has this.
            builder.Entity<Models.AniDb.Anime>().HasIndex(p => p.AnimeId);  
        }
    }
}
