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



        public DbSet<Models.CloudAccount> CloudAccount { get; set; }
        public DbSet<Models.ImportFolder> ImportFolder { get; set; }

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
                .UseMySql("server=192.168.1.3;uid=___;pwd=___;database=___");
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            //Because Fluent API only has this.
            builder.Entity<Models.AniDb.Anime>().HasIndex(p => p.AnimeId);  
        }
    }
}
