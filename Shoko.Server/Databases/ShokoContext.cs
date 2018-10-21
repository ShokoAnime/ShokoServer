using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Nito.AsyncEx;
using NLog;
using Shoko.Models.Server;
using Shoko.Server.Models;

namespace Shoko.Server.Databases
{
    public class ShokoContext : DbContext
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();
        public static readonly LoggerFactory MyLoggerFactory = new LoggerFactory(new[] { new ConsoleLoggerProvider((a, __) => a == "Microsoft.EntityFrameworkCore.Database.Command", true) });


        private readonly string _connectionString;
        private readonly DatabaseTypes _type;
        //private AsyncLock _saveLock = new AsyncLock();

        public ShokoContext(DatabaseTypes type, string connectionstring)
        {
            _type = type;
            _connectionString = connectionstring;
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            Mappings.Map(modelBuilder);
            base.OnModelCreating(modelBuilder);
        }


        //
        // Summary:
        //     Saves all changes made in this context to the database.
        //
        // Parameters:
        //   acceptAllChangesOnSuccess:
        //     Indicates whether Microsoft.EntityFrameworkCore.ChangeTracking.ChangeTracker.AcceptAllChanges
        //     is called after the changes have been sent successfully to the database.
        //
        // Returns:
        //     The number of state entries written to the database.
        //
        // Exceptions:
        //   T:Microsoft.EntityFrameworkCore.DbUpdateException:
        //     An error is encountered while saving to the database.
        //
        //   T:Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException:
        //     A concurrency violation is encountered while saving to the database. A concurrency
        //     violation occurs when an unexpected number of rows are affected during save.
        //     This is usually because the data in the database has been modified since it was
        //     loaded into memory.
        //
        // Remarks:
        //     This method will automatically call Microsoft.EntityFrameworkCore.ChangeTracking.ChangeTracker.DetectChanges
        //     to discover any changes to entity instances before saving to the underlying database.
        //     This can be disabled via Microsoft.EntityFrameworkCore.ChangeTracking.ChangeTracker.AutoDetectChangesEnabled.
        /*
   public override int SaveChanges(bool acceptAllChangesOnSuccess)
   {
       try
       {
           using (_saveLock.Lock())
               return base.SaveChanges(acceptAllChangesOnSuccess);
       } catch (DbUpdateException ex)
       {
           logger.Log(NLog.LogLevel.Error, $"Error in {nameof(ShokoContext)}: {ex.InnerException.Message}", ex);
           throw;
       }
   }
   */
   //
   // Summary:
   //     Saves all changes made in this context to the database.
   //
   // Returns:
   //     The number of state entries written to the database.
   //
   // Exceptions:
   //   T:Microsoft.EntityFrameworkCore.DbUpdateException:
   //     An error is encountered while saving to the database.
   //
   //   T:Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException:
   //     A concurrency violation is encountered while saving to the database. A concurrency
   //     violation occurs when an unexpected number of rows are affected during save.
   //     This is usually because the data in the database has been modified since it was
   //     loaded into memory.
   //
   // Remarks:
   //     This method will automatically call Microsoft.EntityFrameworkCore.ChangeTracking.ChangeTracker.DetectChanges
   //     to discover any changes to entity instances before saving to the underlying database.
   //     This can be disabled via Microsoft.EntityFrameworkCore.ChangeTracking.ChangeTracker.AutoDetectChangesEnabled.
   /*
    * NOTE: Cazzar: This by the code for EFCore just calls SaveChanges(acceptAllChangesOnSuccess: true);
    *               Inherently I have removed this call to just stop save locking recursing where we have a database deadlock.
   public override int SaveChanges()
   {
       using (_saveLock.Lock())
           return base.SaveChanges();
   }*/

        //
        // Summary:
        //     Asynchronously saves all changes made in this context to the database.
        //
        // Parameters:
        //   acceptAllChangesOnSuccess:
        //     Indicates whether Microsoft.EntityFrameworkCore.ChangeTracking.ChangeTracker.AcceptAllChanges
        //     is called after the changes have been sent successfully to the database.
        //
        //   cancellationToken:
        //     A System.Threading.CancellationToken to observe while waiting for the task to
        //     complete.
        //
        // Returns:
        //     A task that represents the asynchronous save operation. The task result contains
        //     the number of state entries written to the database.
        //
        // Exceptions:
        //   T:Microsoft.EntityFrameworkCore.DbUpdateException:
        //     An error is encountered while saving to the database.
        //
        //   T:Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException:
        //     A concurrency violation is encountered while saving to the database. A concurrency
        //     violation occurs when an unexpected number of rows are affected during save.
        //     This is usually because the data in the database has been modified since it was
        //     loaded into memory.
        //
        // Remarks:
        //     This method will automatically call Microsoft.EntityFrameworkCore.ChangeTracking.ChangeTracker.DetectChanges
        //     to discover any changes to entity instances before saving to the underlying database.
        //     This can be disabled via Microsoft.EntityFrameworkCore.ChangeTracking.ChangeTracker.AutoDetectChangesEnabled.
        //     Multiple active operations on the same context instance are not supported. Use
        //     'await' to ensure that any asynchronous operations have completed before calling
        //     another method on this context.
        /*
        public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        {
            try
            {
                using (await _saveLock.LockAsync())
                    return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
            } catch (DbUpdateException ex)
            {
                logger.Log(NLog.LogLevel.Error, $"Error in {nameof(ShokoContext)}: {ex.InnerException.Message}", ex);
                throw;
            }
        }*/
        //
        // Summary:
        //     Asynchronously saves all changes made in this context to the database.
        //
        // Parameters:
        //   cancellationToken:
        //     A System.Threading.CancellationToken to observe while waiting for the task to
        //     complete.
        //
        // Returns:
        //     A task that represents the asynchronous save operation. The task result contains
        //     the number of state entries written to the database.
        //
        // Exceptions:
        //   T:Microsoft.EntityFrameworkCore.DbUpdateException:
        //     An error is encountered while saving to the database.
        //
        //   T:Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException:
        //     A concurrency violation is encountered while saving to the database. A concurrency
        //     violation occurs when an unexpected number of rows are affected during save.
        //     This is usually because the data in the database has been modified since it was
        //     loaded into memory.
        //
        // Remarks:
        //     This method will automatically call Microsoft.EntityFrameworkCore.ChangeTracking.ChangeTracker.DetectChanges
        //     to discover any changes to entity instances before saving to the underlying database.
        //     This can be disabled via Microsoft.EntityFrameworkCore.ChangeTracking.ChangeTracker.AutoDetectChangesEnabled.
        //     Multiple active operations on the same context instance are not supported. Use
        //     'await' to ensure that any asynchronous operations have completed before calling
        //     another method on this context.
        /*
         * Note: Cazzar: Same with SaveChanges, this calls SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            using (await _saveLock.LockAsync())
                return await base.SaveChangesAsync(cancellationToken);
        }
        */

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            switch (_type)
            {
                case DatabaseTypes.SqlServer:
                    optionsBuilder.UseSqlServer(_connectionString);
                    break;
                case DatabaseTypes.MySql:
                    optionsBuilder.UseMySql(_connectionString);
                    break;
                case DatabaseTypes.Sqlite:
                    optionsBuilder.UseSqlite(_connectionString);
                    break;
            }
            #if DEBUG
            optionsBuilder.UseLoggerFactory(MyLoggerFactory);
            optionsBuilder.EnableSensitiveDataLogging();
            #endif
        }

        public DbSet<SVR_AniDB_Anime> AniDB_Animes { get; set; } // AniDB_Anime
        public DbSet<AniDB_Anime_Character> AniDB_Anime_Characters { get; set; } // AniDB_Anime_Character
        public DbSet<AniDB_Anime_DefaultImage> AniDB_Anime_DefaultImages { get; set; } // AniDB_Anime_DefaultImage
        public DbSet<AniDB_Anime_Relation> AniDB_Anime_Relations { get; set; } // AniDB_Anime_Relation
        public DbSet<AniDB_Anime_Review> AniDB_Anime_Reviews { get; set; } // AniDB_Anime_Review
        public DbSet<AniDB_Anime_Similar> AniDB_Anime_Similars { get; set; } // AniDB_Anime_Similar
        public DbSet<AniDB_Anime_Tag> AniDB_Anime_Tags { get; set; } // AniDB_Anime_Tag
        public DbSet<AniDB_Anime_Title> AniDB_Anime_Titles { get; set; } // AniDB_Anime_Title
        public DbSet<AniDB_Character> AniDB_Characters { get; set; } // AniDB_Character
        public DbSet<AniDB_Character_Seiyuu> AniDB_Character_Seiyuus { get; set; } // AniDB_Character_Seiyuu
        public DbSet<AniDB_Episode> AniDB_Episodes { get; set; } // AniDB_Episode
        public DbSet<SVR_AniDB_File> AniDB_Files { get; set; } // AniDB_File
        public DbSet<AniDB_GroupStatus> AniDB_GroupStatus { get; set; } // AniDB_GroupStatus
        public DbSet<AniDB_MylistStats> AniDB_MylistStats { get; set; } // AniDB_MylistStats
        public DbSet<AniDB_Recommendation> AniDB_Recommendations { get; set; } // AniDB_Recommendation
        public DbSet<AniDB_ReleaseGroup> AniDB_ReleaseGroups { get; set; } // AniDB_ReleaseGroup
        public DbSet<AniDB_Review> AniDB_Reviews { get; set; } // AniDB_Review
        public DbSet<AniDB_Seiyuu> AniDB_Seiyuus { get; set; } // AniDB_Seiyuu
        public DbSet<AniDB_Tag> AniDB_Tags { get; set; } // AniDB_Tag
        public DbSet<AniDB_Vote> AniDB_Votes { get; set; } // AniDB_Vote
        public DbSet<AniDB_Episode_Title> AniDB_Episode_Title { get; set; } // AniDB_Episode_Title
        public DbSet<SVR_AnimeEpisode> AnimeEpisodes { get; set; } // AnimeEpisode
        public DbSet<SVR_AnimeEpisode_User> AnimeEpisode_Users { get; set; } // AnimeEpisode_User
        public DbSet<SVR_AnimeGroup> AnimeGroups { get; set; } // AnimeGroup
        public DbSet<SVR_AnimeGroup_User> AnimeGroup_Users { get; set; } // AnimeGroup_User
        public DbSet<SVR_AnimeSeries_User> AnimeSeries_Users { get; set; } // AnimeSeries_User
        public DbSet<SVR_AnimeSeries> AnimeSeries { get; set; } // AnimeSeries
        public DbSet<AuthTokens> AuthTokens { get; set; } // AuthTokens
        public DbSet<BookmarkedAnime> BookmarkedAnimes { get; set; } // BookmarkedAnime
        public DbSet<SVR_CloudAccount> CloudAccounts { get; set; } // CloudAccount
        public DbSet<CommandRequest> CommandRequests { get; set; } // CommandRequest
        public DbSet<CrossRef_AniDB_MAL> CrossRef_AniDB_MALs { get; set; } // CrossRef_AniDB_MAL
        public DbSet<CrossRef_AniDB_Other> CrossRef_AniDB_Other { get; set; } // CrossRef_AniDB_Other
        public DbSet<CrossRef_AniDB_Trakt_Episode> CrossRef_AniDB_Trakt_Episodes { get; set; } // CrossRef_AniDB_Trakt_Episode
        public DbSet<CrossRef_AniDB_TraktV2> CrossRef_AniDB_TraktV2 { get; set; } // CrossRef_AniDB_TraktV2
        public DbSet<CrossRef_AniDB_TvDB_Episode> CrossRef_AniDB_TvDB_Episodes { get; set; } // CrossRef_AniDB_TvDB_Episode
        public DbSet<CrossRef_AniDB_TvDBV2> CrossRef_AniDB_TvDBV2 { get; set; } // CrossRef_AniDB_TvDBV2
        public DbSet<CrossRef_CustomTag> CrossRef_CustomTags { get; set; } // CrossRef_CustomTag
        public DbSet<CrossRef_File_Episode> CrossRef_File_Episodes { get; set; } // CrossRef_File_Episode
        public DbSet<CrossRef_Languages_AniDB_File> CrossRef_Languages_AniDB_Files { get; set; } // CrossRef_Languages_AniDB_File
        public DbSet<CrossRef_Subtitles_AniDB_File> CrossRef_Subtitles_AniDB_Files { get; set; } // CrossRef_Subtitles_AniDB_File
        public DbSet<CustomTag> CustomTags { get; set; } // CustomTag
        public DbSet<DuplicateFile> DuplicateFiles { get; set; } // DuplicateFile
        public DbSet<FileFfdshowPreset> FileFfdshowPresets { get; set; } // FileFfdshowPreset
        public DbSet<FileNameHash> FileNameHashes { get; set; } // FileNameHash
        public DbSet<SVR_GroupFilter> GroupFilters { get; set; } // GroupFilter
        public DbSet<IgnoreAnime> IgnoreAnimes { get; set; } // IgnoreAnime
        public DbSet<SVR_ImportFolder> ImportFolders { get; set; } // ImportFolder
        public DbSet<SVR_JMMUser> JMMUsers { get; set; } // JMMUser
        public DbSet<Language> Languages { get; set; } // Language
        public DbSet<MovieDB_Fanart> MovieDB_Fanarts { get; set; } // MovieDB_Fanart
        public DbSet<MovieDB_Movie> MovieDB_Movies { get; set; } // MovieDB_Movie
        public DbSet<MovieDB_Poster> MovieDB_Posters { get; set; } // MovieDB_Poster
        public DbSet<Playlist> Playlists { get; set; } // Playlist
        public DbSet<SVR_Scan> Scans { get; set; } // Scan
        public DbSet<ScanFile> ScanFiles { get; set; } // ScanFile
        public DbSet<RenameScript> RenameScripts { get; set; } // RenameScript
        public DbSet<ScheduledUpdate> ScheduledUpdates { get; set; } // ScheduledUpdate
        public DbSet<Trakt_Episode> Trakt_Episodes { get; set; } // Trakt_Episode
        public DbSet<Trakt_Friend> Trakt_Friends { get; set; } // Trakt_Friend
        public DbSet<Trakt_Season> Trakt_Seasons { get; set; } // Trakt_Season
        public DbSet<Trakt_Show> Trakt_Shows { get; set; } // Trakt_Show
        public DbSet<TvDB_Episode> TvDB_Episodes { get; set; } // TvDB_Episode
        public DbSet<TvDB_ImageFanart> TvDB_ImageFanarts { get; set; } // TvDB_ImageFanart
        public DbSet<TvDB_ImagePoster> TvDB_ImagePosters { get; set; } // TvDB_ImagePoster
        public DbSet<TvDB_ImageWideBanner> TvDB_ImageWideBanners { get; set; } // TvDB_ImageWideBanner
        public DbSet<TvDB_Series> TvDB_Series { get; set; } // TvDB_Series
        public DbSet<SVR_VideoLocal> VideoLocals { get; set; } // VideoLocal
        public DbSet<SVR_VideoLocal_Place> VideoLocal_Places { get; set; } // VideoLocal_Place
        public DbSet<VideoLocal_User> VideoLocal_Users { get; set; } // VideoLocal_User
        public DbSet<CrossRef_AniDB_TvDB_Episode_Override> CrossRef_AniDB_TvDB_Episode_Override { get; internal set; }
        public DbSet<CrossRef_AniDB_TvDB> CrossRef_AniDB_TvDB { get; internal set; }
        public DbSet<CrossRef_Anime_Staff> CrossRef_Anime_Staff { get; internal set; }
        public DbSet<AnimeStaff> AnimeStaff { get; internal set; }
        public DbSet<AnimeCharacter> AnimeCharacter { get; internal set; }
        public DbSet<AniDB_AnimeUpdate> AniDB_AnimeUpdate { get; internal set; }
        public DbSet<GroupFilterCondition> GroupFilterConditions { get; internal set; }
    }
}
