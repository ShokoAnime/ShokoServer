using System.IO;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using Shoko.Server.Data.TypeConverters;
using Shoko.Server.Models.TMDB;
using Shoko.Server.Server;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

namespace Shoko.Server.Data.Context;

public class DataContext : DbContext
{
    private readonly ISettingsProvider _settingsProvider;
    public string DatabaseType { get; internal set; }
    public string ConnectionString { get; internal set; }

    public DataContext()
    {
    }

    public DataContext(DbContextOptions<DataContext> options, ISettingsProvider settingsProvider)
        : base(options)
    {
        _settingsProvider = settingsProvider;
    }

    public virtual DbSet<TMDB_AlternateOrdering> TmdbAlternateOrdering { get; set; }

    public virtual DbSet<TMDB_AlternateOrdering_Episode> TmdbAlternateOrderingEpisode { get; set; }

    public virtual DbSet<TMDB_AlternateOrdering_Season> TmdbAlternateOrderingSeason { get; set; }

    public virtual DbSet<TMDB_Collection> TmdbCollection { get; set; }

    public virtual DbSet<TMDB_Collection_Movie> TmdbCollectionMovie { get; set; }

    public virtual DbSet<TMDB_Company> TmdbCompany { get; set; }

    public virtual DbSet<TMDB_Company_Entity> TmdbCompanyEntity { get; set; }

    public virtual DbSet<TMDB_Episode> TmdbEpisode { get; set; }

    public virtual DbSet<TMDB_Episode_Cast> TmdbEpisodeCast { get; set; }

    public virtual DbSet<TMDB_Episode_Crew> TmdbEpisodeCrew { get; set; }

    public virtual DbSet<TMDB_Image> TmdbImage { get; set; }

    public virtual DbSet<TMDB_Image_Entity> TmdbImageEntity { get; set; }

    public virtual DbSet<TMDB_Movie> TmdbMovie { get; set; }

    public virtual DbSet<TMDB_Movie_Cast> TmdbMovieCast { get; set; }

    public virtual DbSet<TMDB_Movie_Crew> TmdbMovieCrew { get; set; }

    public virtual DbSet<TMDB_Network> TmdbNetwork { get; set; }

    public virtual DbSet<TMDB_Overview> TmdbOverview { get; set; }

    public virtual DbSet<TMDB_Person> TmdbPerson { get; set; }

    public virtual DbSet<TMDB_Season> TmdbSeason { get; set; }

    public virtual DbSet<TMDB_Show> TmdbShow { get; set; }

    public virtual DbSet<TMDB_Show_Network> TmdbShowNetwork { get; set; }

    public virtual DbSet<TMDB_Title> TmdbTitle { get; set; }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (string.IsNullOrEmpty(DatabaseType) && string.IsNullOrEmpty(ConnectionString))
        {
            if (_settingsProvider == null)
            {
                // load dev settings for dotnet ef tools
                optionsBuilder.UseSqlite("C:\\ProgramData\\ShokoServer\\SQLite\\ShokoServer.db3");
                return;
            }

            var settings = _settingsProvider.GetSettings();
            DatabaseType = settings.Database.Type;
            // for now, only SQLite
            switch (DatabaseType)
            {
                case Constants.DatabaseType.Sqlite:
                {
                    var connectionString = settings.Database.OverrideConnectionString;
                    if (string.IsNullOrEmpty(connectionString))
                    {
                        var dirPath = settings.Database.MySqliteDirectory;
                        if (string.IsNullOrWhiteSpace(dirPath))
                            dirPath = Path.Combine(Utils.ApplicationPath, "SQLite");
                        else
                            dirPath = Path.Combine(Utils.ApplicationPath, dirPath);

                        var dbName = Path.Combine(dirPath, settings.Database.SQLite_DatabaseFileEF);

                        var csBuilder = new SqliteConnectionStringBuilder
                        {
                            DataSource = dbName,
                            Mode = SqliteOpenMode.ReadWriteCreate,
                            ForeignKeys = true,
                            Pooling = true,
                            DefaultTimeout = 90
                        };

                        connectionString = csBuilder.ToString();
                    }

                    ConnectionString = connectionString;
                    break;
                }
                case Constants.DatabaseType.MySQL:
                {
                    var connectionString = settings.Database.OverrideConnectionString;
                    if (string.IsNullOrEmpty(connectionString))
                    {
                        var csBuilder = new MySqlConnectionStringBuilder
                        {
                            Database = settings.Database.Schema,
                            Server = settings.Database.Hostname,
                            Port = (uint)settings.Database.Port,
                            UserID = settings.Database.Username,
                            Password = settings.Database.Password,
                            AllowUserVariables = true
                        };

                        connectionString = csBuilder.ToString();
                    }

                    ConnectionString = connectionString;
                    break;
                }
                case Constants.DatabaseType.SqlServer:
                {
                    var connectionString = settings.Database.OverrideConnectionString;
                    if (string.IsNullOrEmpty(connectionString))
                    {
                        var csBuilder = new SqlConnectionStringBuilder
                        {
                            InitialCatalog = settings.Database.Schema,
                            DataSource = $"{settings.Database.Host},{settings.Database.Port}",
                            UserID = settings.Database.Username,
                            Password = settings.Database.Password,
                            MultipleActiveResultSets = true,
                            TrustServerCertificate = true
                        };

                        connectionString = csBuilder.ToString();
                    }

                    ConnectionString = connectionString;
                    break;
                }
            }
        }

        switch (DatabaseType)
        {
            case Constants.DatabaseType.Sqlite:
                optionsBuilder.UseSqlite(ConnectionString);
                break; 
            case Constants.DatabaseType.MySQL:
                optionsBuilder.UseMySql(ConnectionString, ServerVersion.AutoDetect(ConnectionString));
                break;
            case Constants.DatabaseType.SqlServer:
                optionsBuilder.UseSqlServer(ConnectionString);
                break;
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TMDB_AlternateOrdering>(entity =>
        {
            entity.ToTable("TMDB_AlternateOrdering");

            entity.HasIndex(e => new { e.TmdbEpisodeGroupCollectionID, e.TmdbShowID }, "IX_TMDB_AlternateOrdering_TmdbEpisodeGroupCollectionID_TmdbShowID");

            entity.HasIndex(e => e.TmdbShowID, "IX_TMDB_AlternateOrdering_TmdbShowID");

            entity.HasIndex(e => e.TmdbEpisodeGroupCollectionID, "UIX_TMDB_AlternateOrdering_TmdbEpisodeGroupCollectionID").IsUnique();

            entity.Property(e => e.TMDB_AlternateOrderingID).HasColumnName("TMDB_AlternateOrderingID");
            entity.Property(e => e.CreatedAt).HasColumnType("DATETIME");
            entity.Property(e => e.EnglishOverview).IsRequired();
            entity.Property(e => e.EnglishTitle).IsRequired();
            entity.Property(e => e.LastUpdatedAt).HasColumnType("DATETIME");
            entity.Property(e => e.TmdbEpisodeGroupCollectionID).IsRequired().HasColumnName("TmdbEpisodeGroupCollectionID");
            entity.Property(e => e.TmdbNetworkID).HasColumnName("TmdbNetworkID");
            entity.Property(e => e.TmdbShowID).HasColumnName("TmdbShowID");

            // Foreign Keys
            entity.HasOne(d => d.Show).WithMany(p => p.AlternateOrderings).HasForeignKey(d => d.TmdbShowID).HasPrincipalKey(a => a.TmdbShowID);
            entity.HasMany(d => d.AlternateOrderingEpisodes).WithOne(p => p.TmdbAlternateOrdering).HasForeignKey(d => d.TmdbEpisodeGroupCollectionID).HasPrincipalKey(a => a.TmdbEpisodeGroupCollectionID);
            entity.HasMany(d => d.AlternateOrderingSeasons).WithOne(p => p.TmdbAlternateOrdering).HasForeignKey(d => d.TmdbEpisodeGroupCollectionID).HasPrincipalKey(a => a.TmdbEpisodeGroupCollectionID);
        });

        modelBuilder.Entity<TMDB_AlternateOrdering_Episode>(entity =>
        {
            entity.ToTable("TMDB_AlternateOrdering_Episode");

            entity.Property(e => e.TMDB_AlternateOrdering_EpisodeID).HasColumnName("TMDB_AlternateOrdering_EpisodeID");
            entity.Property(e => e.CreatedAt).HasColumnType("DATETIME");
            entity.Property(e => e.LastUpdatedAt).HasColumnType("DATETIME");
            entity.Property(e => e.TmdbEpisodeGroupCollectionID).IsRequired().HasColumnName("TmdbEpisodeGroupCollectionID");
            entity.Property(e => e.TmdbEpisodeGroupID).IsRequired().HasColumnName("TmdbEpisodeGroupID");
            entity.Property(e => e.TmdbEpisodeID).HasColumnName("TmdbEpisodeID");
            entity.Property(e => e.TmdbShowID).HasColumnName("TmdbShowID");

            // Foreign Keys
            entity.HasOne(d => d.TmdbEpisode).WithMany(p => p.TmdbAlternateOrderingEpisodes).HasForeignKey(d => d.TmdbEpisodeID)
                .HasPrincipalKey(a => a.TmdbEpisodeID);
            entity.HasOne(d => d.TmdbShow).WithMany().HasForeignKey(d => d.TmdbShowID).HasPrincipalKey(a => a.TmdbShowID);
            entity.HasOne(d => d.TmdbAlternateOrdering).WithMany(p => p.AlternateOrderingEpisodes).HasForeignKey(d => d.TmdbEpisodeGroupCollectionID)
                .HasPrincipalKey(a => a.TmdbEpisodeGroupCollectionID);
            entity.HasOne(d => d.TmdbAlternateOrderingSeason).WithMany(p => p.TmdbAlternateOrderingEpisodes).HasForeignKey(d => d.TmdbEpisodeGroupCollectionID)
                .HasPrincipalKey(a => a.TmdbEpisodeGroupCollectionID);
        });

        modelBuilder.Entity<TMDB_AlternateOrdering_Season>(entity =>
        {
            entity.ToTable("TMDB_AlternateOrdering_Season");

            entity.HasIndex(e => e.TmdbEpisodeGroupCollectionID, "IX_TMDB_AlternateOrdering_Season_TmdbEpisodeGroupCollectionID");

            entity.HasIndex(e => e.TmdbShowID, "IX_TMDB_AlternateOrdering_Season_TmdbShowID");

            entity.HasIndex(e => e.TmdbEpisodeGroupID, "UIX_TMDB_AlternateOrdering_Season_TmdbEpisodeGroupID").IsUnique();

            entity.Property(e => e.TMDB_AlternateOrdering_SeasonID).HasColumnName("TMDB_AlternateOrdering_SeasonID");
            entity.Property(e => e.CreatedAt).HasColumnType("DATETIME");
            entity.Property(e => e.EnglishTitle).IsRequired();
            entity.Property(e => e.LastUpdatedAt).HasColumnType("DATETIME");
            entity.Property(e => e.TmdbEpisodeGroupCollectionID).IsRequired().HasColumnName("TmdbEpisodeGroupCollectionID");
            entity.Property(e => e.TmdbEpisodeGroupID).IsRequired().HasColumnName("TmdbEpisodeGroupID");
            entity.Property(e => e.TmdbShowID).HasColumnName("TmdbShowID");

            // Foreign Keys
            entity.HasOne(d => d.TmdbShow).WithMany().HasForeignKey(d => d.TmdbShowID).HasPrincipalKey(a => a.TmdbShowID);
            entity.HasOne(d => d.TmdbAlternateOrdering).WithMany(p => p.AlternateOrderingSeasons).HasForeignKey(d => d.TmdbEpisodeGroupCollectionID)
                .HasPrincipalKey(a => a.TmdbEpisodeGroupCollectionID);
            entity.HasMany(d => d.TmdbAlternateOrderingEpisodes).WithOne(p => p.TmdbAlternateOrderingSeason).HasForeignKey(d => d.TmdbEpisodeGroupCollectionID)
                .HasPrincipalKey(a => a.TmdbEpisodeGroupCollectionID);
        });

        modelBuilder.Entity<TMDB_Collection>(entity =>
        {
            entity.ToTable("TMDB_Collection");

            entity.HasIndex(e => e.TMDB_CollectionID, "UIX_TMDB_Collection_TmdbCollectionID").IsUnique();

            entity.Property(e => e.TMDB_CollectionID).HasColumnName("TMDB_CollectionID");
            entity.Property(e => e.CreatedAt).HasColumnType("DATETIME");
            entity.Property(e => e.EnglishOverview).IsRequired();
            entity.Property(e => e.EnglishTitle).IsRequired();
            entity.Property(e => e.LastUpdatedAt).HasColumnType("DATETIME");
            entity.Property(e => e.TmdbCollectionID).HasColumnName("TmdbCollectionID");

            // Foreign Keys
            entity.HasMany(d => d.Movies).WithOne(p => p.TmdbCollection).HasForeignKey(d => d.TmdbCollectionID).HasPrincipalKey(a => a.TmdbCollectionID);
            entity.HasMany(d => d.ImageXRefs).WithOne().HasPrincipalKey(a => a.TmdbCollectionID).HasForeignKey(d => d.TmdbEntityID)
                .HasPrincipalKey(a => a.TmdbCollectionID);
            entity.HasMany(d => d.Titles).WithOne().HasForeignKey(d => d.ParentID).HasPrincipalKey(a => a.TmdbCollectionID)
                .HasPrincipalKey(a => a.TmdbCollectionID);
            entity.HasMany(d => d.Overviews).WithOne().HasForeignKey(d => d.ParentID).HasPrincipalKey(a => a.TmdbCollectionID)
                .HasPrincipalKey(a => a.TmdbCollectionID);
        });

        modelBuilder.Entity<TMDB_Collection_Movie>(entity =>
        {
            entity.ToTable("TMDB_Collection_Movie");

            entity.HasIndex(e => e.TmdbCollectionID, "IX_TMDB_Collection_Movie_TmdbCollectionID");

            entity.HasIndex(e => e.TmdbMovieID, "IX_TMDB_Collection_Movie_TmdbMovieID");

            entity.Property(e => e.TMDB_Collection_MovieID).HasColumnName("TMDB_Collection_MovieID");
            entity.Property(e => e.TmdbCollectionID).HasColumnName("TmdbCollectionID");
            entity.Property(e => e.TmdbMovieID).HasColumnName("TmdbMovieID");
        });

        modelBuilder.Entity<TMDB_Company>(entity =>
        {
            entity.ToTable("TMDB_Company");

            entity.HasIndex(e => e.TMDB_CompanyID, "IX_TMDB_Company_TmdbCompanyID");

            entity.Property(e => e.TMDB_CompanyID).HasColumnName("TMDB_CompanyID");
            entity.Property(e => e.CountryOfOrigin).IsRequired();
            entity.Property(e => e.Name).IsRequired();
            entity.Property(e => e.TmdbCompanyID).HasColumnName("TmdbCompanyID");

            // Foreign Keys
            entity.HasMany(d => d.ImageXRefs).WithOne().HasPrincipalKey(a => a.TmdbCompanyID).HasForeignKey(d => d.TmdbEntityID);
            entity.HasMany(d => d.XRefs).WithOne(d => d.Company).HasForeignKey(d => d.TmdbCompanyID).HasPrincipalKey(a => a.TmdbCompanyID);
        });

        modelBuilder.Entity<TMDB_Company_Entity>(entity =>
        {
            entity.ToTable("TMDB_Company_Entity");
            entity.HasDiscriminator(a => a.TmdbEntityType).HasValue<TMDB_Company_Show>(ForeignEntityType.Show).HasValue<TMDB_Company_Movie>(ForeignEntityType.Movie);

            entity.HasIndex(e => e.TmdbCompanyID, "IX_TMDB_Company_Entity_TmdbCompanyID");

            entity.HasIndex(e => new { e.TmdbEntityType, e.TmdbEntityID }, "IX_TMDB_Company_Entity_TmdbEntityType_TmdbEntityID");

            entity.Property(e => e.TMDB_Company_EntityID).HasColumnName("TMDB_Company_EntityID");
            entity.Property(e => e.ReleasedAt).HasColumnType("DATE").HasConversion<DateOnlyToString>();
            entity.Property(e => e.TmdbCompanyID).HasColumnName("TmdbCompanyID");
            entity.Property(e => e.TmdbEntityID).HasColumnName("TmdbEntityID");

            // Foreign Keys
            entity.HasOne(a => a.TVShow).WithMany().HasForeignKey(a => a.TmdbEntityID).HasPrincipalKey(a => a.TmdbShowID);
            entity.HasOne(a => a.Movie).WithMany().HasForeignKey(a => a.TmdbEntityID).HasPrincipalKey(a => a.TmdbMovieID);
        });

        modelBuilder.Entity<TMDB_Episode>(entity =>
        {
            entity.ToTable("TMDB_Episode");

            entity.HasIndex(e => e.TmdbSeasonID, "IX_TMDB_Episode_TmdbSeasonID");

            entity.HasIndex(e => e.TmdbShowID, "IX_TMDB_Episode_TmdbShowID");

            entity.HasIndex(e => e.TMDB_EpisodeID, "UIX_TMDB_Episode_TmdbEpisodeID").IsUnique();

            entity.Property(e => e.TMDB_EpisodeID).HasColumnName("TMDB_EpisodeID");
            entity.Property(e => e.AiredAt).HasColumnType("DATE").HasConversion<DateOnlyToString>();
            entity.Property(e => e.CreatedAt).HasColumnType("DATETIME");
            entity.Property(e => e.EnglishOverview).IsRequired();
            entity.Property(e => e.EnglishTitle).IsRequired();
            entity.Property(e => e.LastUpdatedAt).HasColumnType("DATETIME");
            entity.Property(e => e.ThumbnailPath).HasDefaultValueSql("NULL");
            entity.Property(e => e.TmdbEpisodeID).HasColumnName("TmdbEpisodeID");
            entity.Property(e => e.TmdbSeasonID).HasColumnName("TmdbSeasonID");
            entity.Property(e => e.TmdbShowID).HasColumnName("TmdbShowID");
            entity.Property(e => e.TvdbEpisodeID).HasDefaultValueSql("NULL").HasColumnName("TvdbEpisodeID");

            // Foreign Keys
            entity.HasMany(d => d.ImageXRefs).WithOne().HasPrincipalKey(a => a.TmdbEpisodeID).HasForeignKey(d => d.TmdbEntityID);
            entity.HasMany(a => a.AllTitles).WithOne().HasForeignKey(a => a.ParentID).HasPrincipalKey(a => a.TmdbEpisodeID);
            entity.HasMany(a => a.AllOverviews).WithOne().HasForeignKey(a => a.ParentID).HasPrincipalKey(a => a.TmdbEpisodeID);
            entity.HasOne(d => d.Season).WithMany(p => p.Episodes).HasForeignKey(d => d.TmdbSeasonID).HasPrincipalKey(a => a.TmdbSeasonID);
            entity.HasOne(d => d.Show).WithMany(p => p.Episodes).HasForeignKey(d => d.TmdbShowID).HasPrincipalKey(a => a.TmdbShowID);
            entity.HasMany(a => a.Cast).WithOne(a => a.Episode).HasForeignKey(a => a.TmdbEpisodeID).HasPrincipalKey(a => a.TmdbEpisodeID);
            entity.HasMany(a => a.Crew).WithOne(a => a.Episode).HasForeignKey(a => a.TmdbEpisodeID).HasPrincipalKey(a => a.TmdbEpisodeID);
            entity.HasMany(a => a.TmdbAlternateOrderingEpisodes).WithOne(a => a.TmdbEpisode).HasForeignKey(a => a.TmdbEpisodeID).HasPrincipalKey(a => a.TmdbEpisodeID);
        });

        modelBuilder.Entity<TMDB_Episode_Cast>(entity =>
        {
            entity.ToTable("TMDB_Episode_Cast");

            entity.HasIndex(e => e.TmdbEpisodeID, "IX_TMDB_Episode_Cast_TmdbEpisodeID");

            entity.HasIndex(e => e.TmdbPersonID, "IX_TMDB_Episode_Cast_TmdbPersonID");

            entity.HasIndex(e => e.TmdbSeasonID, "IX_TMDB_Episode_Cast_TmdbSeasonID");

            entity.HasIndex(e => e.TmdbShowID, "IX_TMDB_Episode_Cast_TmdbShowID");

            entity.Property(e => e.TMDB_Episode_CastID).HasColumnName("TMDB_Episode_CastID");
            entity.Property(e => e.CharacterName).IsRequired();
            entity.Property(e => e.TmdbCreditID).IsRequired().HasColumnName("TmdbCreditID");
            entity.Property(e => e.TmdbEpisodeID).HasColumnName("TmdbEpisodeID");
            entity.Property(e => e.TmdbPersonID).HasColumnName("TmdbPersonID");
            entity.Property(e => e.TmdbSeasonID).HasColumnName("TmdbSeasonID");
            entity.Property(e => e.TmdbShowID).HasColumnName("TmdbShowID");

            entity.HasOne(a => a.Person).WithMany().HasForeignKey(a => a.TmdbPersonID).HasPrincipalKey(a => a.TmdbPersonID);
            entity.HasOne(a => a.Episode).WithMany().HasForeignKey(a => a.TmdbEpisodeID).HasPrincipalKey(a => a.TmdbEpisodeID);
        });

        modelBuilder.Entity<TMDB_Episode_Crew>(entity =>
        {
            entity.ToTable("TMDB_Episode_Crew");

            entity.HasIndex(e => e.TmdbEpisodeID, "IX_TMDB_Episode_Crew_TmdbEpisodeID");

            entity.HasIndex(e => e.TmdbPersonID, "IX_TMDB_Episode_Crew_TmdbPersonID");

            entity.HasIndex(e => e.TmdbSeasonID, "IX_TMDB_Episode_Crew_TmdbSeasonID");

            entity.HasIndex(e => e.TmdbShowID, "IX_TMDB_Episode_Crew_TmdbShowID");

            entity.Property(e => e.TMDB_Episode_CrewID).HasColumnName("TMDB_Episode_CrewID");
            entity.Property(e => e.Department).IsRequired();
            entity.Property(e => e.Job).IsRequired();
            entity.Property(e => e.TmdbCreditID).IsRequired().HasColumnName("TmdbCreditID");
            entity.Property(e => e.TmdbEpisodeID).HasColumnName("TmdbEpisodeID");
            entity.Property(e => e.TmdbPersonID).HasColumnName("TmdbPersonID");
            entity.Property(e => e.TmdbSeasonID).HasColumnName("TmdbSeasonID");
            entity.Property(e => e.TmdbShowID).HasColumnName("TmdbShowID");

            entity.HasOne(a => a.Person).WithMany().HasForeignKey(a => a.TmdbPersonID).HasPrincipalKey(a => a.TmdbPersonID);
            entity.HasOne(a => a.Episode).WithMany().HasForeignKey(a => a.TmdbEpisodeID).HasPrincipalKey(a => a.TmdbEpisodeID);
        });

        modelBuilder.Entity<TMDB_Image>(entity =>
        {
            entity.ToTable("TMDB_Image");

            entity.Property(e => e.TMDB_ImageID).HasColumnName("TMDB_ImageID");
            entity.Property(e => e.Language).IsRequired().HasConversion<TitleLanguageToString>();
            entity.Property(e => e.RemoteFileName).IsRequired();
        });

        modelBuilder.Entity<TMDB_Image_Entity>(entity =>
        {
            entity.ToTable("TMDB_Image_Entity");
            entity.HasDiscriminator(a => a.TmdbEntityType)
                .HasValue<TMDB_Image_Movie>(ForeignEntityType.Movie)
                .HasValue<TMDB_Image_TVShow>(ForeignEntityType.Show)
                .HasValue<TMDB_Image_Episode>(ForeignEntityType.Episode)
                .HasValue<TMDB_Image_Season>(ForeignEntityType.Season)
                .HasValue<TMDB_Image_Collection>(ForeignEntityType.Collection)
                .HasValue<TMDB_Image_Company>(ForeignEntityType.Company)
                .HasValue<TMDB_Image_Character>(ForeignEntityType.Character)
                .HasValue<TMDB_Image_Person>(ForeignEntityType.Person)
                .HasValue<TMDB_Image_Network>(ForeignEntityType.Network)
                .HasValue<TMDB_Image_Studio>(ForeignEntityType.Studio);

            entity.Property(e => e.TMDB_Image_EntityID).HasColumnName("TMDB_Image_EntityID");
            entity.Property(e => e.ReleasedAt).HasColumnType("DATE").HasConversion<DateOnlyToString>();
            entity.Property(e => e.RemoteFileName).IsRequired();
            entity.Property(e => e.TmdbEntityID).HasColumnName("TmdbEntityID");

            entity.HasOne(a => a.Image).WithMany().HasForeignKey(a => a.RemoteFileName).HasPrincipalKey(a => a.RemoteFileName);
        });

        modelBuilder.Entity<TMDB_Movie>(entity =>
        {
            entity.ToTable("TMDB_Movie");

            entity.HasIndex(e => e.TmdbCollectionID, "IX_TMDB_Movie_TmdbCollectionID");

            entity.HasIndex(e => e.TmdbMovieID, "UIX_TMDB_Movie_TmdbMovieID").IsUnique();

            entity.Property(e => e.TMDB_MovieID).HasColumnName("TMDB_MovieID");
            entity.Property(e => e.BackdropPath).HasDefaultValueSql("NULL");
            entity.Property(e => e.ContentRatings).IsRequired().HasConversion<ContentRatingsToString, ContentRatingsComparer>();
            entity.Property(e => e.CreatedAt).HasColumnType("DATETIME");
            entity.Property(e => e.EnglishOverview).IsRequired();
            entity.Property(e => e.EnglishTitle).IsRequired();
            entity.Property(e => e.Genres).IsRequired().HasConversion<StringListToString, StringListComparer>();
            entity.Property(e => e.ImdbMovieID).HasDefaultValueSql("NULL").HasColumnName("ImdbMovieID");
            entity.Property(e => e.Keywords).HasDefaultValueSql("NULL").HasConversion<StringListToString, StringListComparer>();
            entity.Property(e => e.LastUpdatedAt).HasColumnType("DATETIME");
            entity.Property(e => e.OriginalLanguageCode).IsRequired();
            entity.Property(e => e.OriginalTitle).IsRequired();
            entity.Property(e => e.PosterPath).HasDefaultValueSql("NULL");
            entity.Property(e => e.ProductionCountries).HasDefaultValueSql("NULL").HasConversion<ProductionCountriesToString, ProductionCountriesComparer>();
            entity.Property(e => e.ReleasedAt).HasColumnType("DATE").HasConversion<DateOnlyToString>();
            entity.Property(e => e.TmdbCollectionID).HasColumnName("TmdbCollectionID");
            entity.Property(e => e.TmdbMovieID).HasColumnName("TmdbMovieID");

            entity.HasMany(d => d.ImageXRefs).WithOne().HasPrincipalKey(a => a.TmdbMovieID).HasForeignKey(d => d.TmdbEntityID);
            entity.HasMany(a => a.AllTitles).WithOne().HasForeignKey(a => a.ParentID).HasPrincipalKey(a => a.TmdbMovieID);
            entity.HasMany(a => a.AllOverviews).WithOne().HasForeignKey(a => a.ParentID).HasPrincipalKey(a => a.TmdbMovieID);
            entity.HasMany(a => a.Cast).WithOne(a => a.Movie).HasForeignKey(a => a.TmdbMovieID).HasPrincipalKey(a => a.TmdbMovieID);
            entity.HasMany(a => a.Crew).WithOne(a => a.Movie).HasForeignKey(a => a.TmdbMovieID).HasPrincipalKey(a => a.TmdbMovieID);
        });

        modelBuilder.Entity<TMDB_Movie_Cast>(entity =>
        {
            entity.ToTable("TMDB_Movie_Cast");

            entity.HasIndex(e => e.TmdbMovieID, "IX_TMDB_Movie_Cast_TmdbMovieID");

            entity.HasIndex(e => e.TmdbPersonID, "IX_TMDB_Movie_Cast_TmdbPersonID");

            entity.Property(e => e.TMDB_Movie_CastID).HasColumnName("TMDB_Movie_CastID");
            entity.Property(e => e.CharacterName).IsRequired();
            entity.Property(e => e.TmdbCreditID).IsRequired().HasColumnName("TmdbCreditID");
            entity.Property(e => e.TmdbMovieID).HasColumnType("INT").HasColumnName("TmdbMovieID");
            entity.Property(e => e.TmdbPersonID).HasColumnType("INT").HasColumnName("TmdbPersonID");

            entity.HasOne(a => a.Person).WithMany().HasForeignKey(a => a.TmdbPersonID).HasPrincipalKey(a => a.TmdbPersonID);
            entity.HasOne(a => a.Movie).WithMany().HasForeignKey(a => a.TmdbMovieID).HasPrincipalKey(a => a.TmdbMovieID);
        });

        modelBuilder.Entity<TMDB_Movie_Crew>(entity =>
        {
            entity.ToTable("TMDB_Movie_Crew");

            entity.HasIndex(e => e.TmdbMovieID, "IX_TMDB_Movie_Crew_TmdbMovieID");

            entity.HasIndex(e => e.TmdbPersonID, "IX_TMDB_Movie_Crew_TmdbPersonID");

            entity.Property(e => e.TMDB_Movie_CrewID).HasColumnName("TMDB_Movie_CrewID");
            entity.Property(e => e.Department).IsRequired();
            entity.Property(e => e.Job).IsRequired();
            entity.Property(e => e.TmdbCreditID).IsRequired().HasColumnName("TmdbCreditID");
            entity.Property(e => e.TmdbMovieID).HasColumnName("TmdbMovieID");
            entity.Property(e => e.TmdbPersonID).HasColumnName("TmdbPersonID");

            entity.HasOne(a => a.Person).WithMany().HasForeignKey(a => a.TmdbPersonID).HasPrincipalKey(a => a.TmdbPersonID);
            entity.HasOne(a => a.Movie).WithMany().HasForeignKey(a => a.TmdbMovieID).HasPrincipalKey(a => a.TmdbMovieID);
        });

        modelBuilder.Entity<TMDB_Network>(entity =>
        {
            entity.ToTable("TMDB_Network");

            entity.HasIndex(e => e.TmdbNetworkID, "UIX_TMDB_Network_TmdbNetworkID").IsUnique();

            entity.Property(e => e.TMDB_NetworkID).HasColumnName("TMDB_NetworkID");
            entity.Property(e => e.CountryOfOrigin).IsRequired();
            entity.Property(e => e.Name).IsRequired();
            entity.Property(e => e.TmdbNetworkID).HasColumnName("TmdbNetworkID");
        });

        modelBuilder.Entity<TMDB_Overview>(entity =>
        {
            entity.ToTable("TMDB_Overview");
            entity.HasDiscriminator(a => a.ParentType)
                .HasValue<TMDB_Overview_Episode>(ForeignEntityType.Episode)
                .HasValue<TMDB_Overview_Season>(ForeignEntityType.Season)
                .HasValue<TMDB_Overview_Movie>(ForeignEntityType.Movie)
                .HasValue<TMDB_Overview_TVShow>(ForeignEntityType.Show)
                .HasValue<TMDB_Overview_Collection>(ForeignEntityType.Collection)
                .HasValue<TMDB_Overview_Person>(ForeignEntityType.Person);

            entity.HasIndex(e => new { e.ParentType, e.ParentID }, "IX_TMDB_Overview");

            entity.Property(e => e.TMDB_OverviewID).HasColumnName("TMDB_OverviewID");
            entity.Property(e => e.CountryCode).IsRequired();
            entity.Property(e => e.LanguageCode).IsRequired();
            entity.Property(e => e.ParentID).HasColumnName("ParentID");
            entity.Property(e => e.Value).IsRequired();
        });

        modelBuilder.Entity<TMDB_Person>(entity =>
        {
            entity.ToTable("TMDB_Person");

            entity.HasIndex(e => e.TmdbPersonID, "IX_TMDB_Person_TmdbPersonID");

            entity.Property(e => e.TMDB_PersonID).HasColumnName("TMDB_PersonID");
            entity.Property(e => e.Aliases).IsRequired().HasConversion<StringListToString, StringListComparer>();
            entity.Property(e => e.BirthDay).HasColumnType("DATE").HasConversion<DateOnlyToString>();
            entity.Property(e => e.CreatedAt).HasColumnType("DATETIME");
            entity.Property(e => e.DeathDay).HasColumnType("DATE").HasConversion<DateOnlyToString>();
            entity.Property(e => e.EnglishBiography).IsRequired();
            entity.Property(e => e.EnglishName).IsRequired();
            entity.Property(e => e.LastUpdatedAt).HasColumnType("DATETIME");
            entity.Property(e => e.TmdbPersonID).HasColumnName("TmdbPersonID");

            entity.HasMany(d => d.ImageXRefs).WithOne().HasPrincipalKey(a => a.TmdbPersonID).HasForeignKey(d => d.TmdbEntityID);
            entity.HasMany(a => a.AllBiographies).WithOne().HasForeignKey(a => a.ParentID).HasPrincipalKey(a => a.TmdbPersonID);
            entity.HasMany(a => a.EpisodeRoles).WithOne(a => a.Person).HasForeignKey(a => a.TmdbPersonID).HasPrincipalKey(a => a.TmdbPersonID);
            entity.HasMany(a => a.MovieRoles).WithOne(a => a.Person).HasForeignKey(a => a.TmdbPersonID).HasPrincipalKey(a => a.TmdbPersonID);
            entity.HasMany(a => a.EpisodeCrew).WithOne(a => a.Person).HasForeignKey(a => a.TmdbPersonID).HasPrincipalKey(a => a.TmdbPersonID);
            entity.HasMany(a => a.MovieCrew).WithOne(a => a.Person).HasForeignKey(a => a.TmdbPersonID).HasPrincipalKey(a => a.TmdbPersonID);
        });

        modelBuilder.Entity<TMDB_Season>(entity =>
        {
            entity.ToTable("TMDB_Season");

            entity.HasIndex(e => e.TmdbShowID, "IX_TMDB_Season_TmdbShowID");

            entity.HasIndex(e => e.TmdbSeasonID, "UIX_TMDB_Season_TmdbSeasonID").IsUnique();

            entity.Property(e => e.TMDB_SeasonID).HasColumnName("TMDB_SeasonID");
            entity.Property(e => e.CreatedAt).HasColumnType("DATETIME");
            entity.Property(e => e.EnglishOverview).IsRequired();
            entity.Property(e => e.EnglishTitle).IsRequired();
            entity.Property(e => e.LastUpdatedAt).HasColumnType("DATETIME");
            entity.Property(e => e.PosterPath).HasDefaultValueSql("NULL");
            entity.Property(e => e.TmdbSeasonID).HasColumnName("TmdbSeasonID");
            entity.Property(e => e.TmdbShowID).HasColumnName("TmdbShowID");

            entity.HasMany(d => d.ImageXRefs).WithOne().HasPrincipalKey(a => a.TmdbSeasonID).HasForeignKey(d => d.TmdbEntityID);
            entity.HasMany(a => a.AllTitles).WithOne().HasForeignKey(a => a.ParentID).HasPrincipalKey(a => a.TmdbSeasonID);
            entity.HasMany(a => a.AllOverviews).WithOne().HasForeignKey(a => a.ParentID).HasPrincipalKey(a => a.TmdbSeasonID);
            entity.HasOne(a => a.Show).WithMany(a => a.Seasons).HasForeignKey(a => a.TmdbShowID).HasPrincipalKey(a => a.TmdbShowID);
            entity.HasMany(a => a.Episodes).WithOne().HasForeignKey(a => a.TmdbSeasonID).HasPrincipalKey(a => a.TmdbSeasonID);
        });

        modelBuilder.Entity<TMDB_Show>(entity =>
        {
            entity.ToTable("TMDB_Show");

            entity.HasIndex(e => e.TmdbShowID, "UIX_TMDB_Show_TmdbShowID").IsUnique();

            entity.Property(e => e.TMDB_ShowID).HasColumnName("TMDB_ShowID");
            entity.Property(e => e.BackdropPath).HasDefaultValueSql("NULL");
            entity.Property(e => e.ContentRatings).IsRequired().HasConversion<ContentRatingsToString, ContentRatingsComparer>();
            entity.Property(e => e.CreatedAt).HasColumnType("DATETIME");
            entity.Property(e => e.EnglishOverview).IsRequired();
            entity.Property(e => e.EnglishTitle).IsRequired();
            entity.Property(e => e.FirstAiredAt).HasColumnType("DATE").HasConversion<DateOnlyToString>();
            entity.Property(e => e.Genres).IsRequired().HasConversion<StringListToString, StringListComparer>();
            entity.Property(e => e.Keywords).HasDefaultValueSql("NULL").HasConversion<StringListToString, StringListComparer>();
            entity.Property(e => e.LastAiredAt).HasColumnType("DATE").HasConversion<DateOnlyToString>();
            entity.Property(e => e.LastUpdatedAt).HasColumnType("DATETIME");
            entity.Property(e => e.OriginalLanguageCode).IsRequired();
            entity.Property(e => e.OriginalTitle).IsRequired();
            entity.Property(e => e.PosterPath).HasDefaultValueSql("NULL");
            entity.Property(e => e.PreferredAlternateOrderingID).HasDefaultValueSql("NULL").HasColumnName("PreferredAlternateOrderingID");
            entity.Property(e => e.ProductionCountries).HasDefaultValueSql("NULL").HasConversion<ProductionCountriesToString, ProductionCountriesComparer>();
            entity.Property(e => e.TmdbShowID).HasColumnName("TmdbShowID");
            entity.Property(e => e.TvdbShowID).HasDefaultValueSql("NULL").HasColumnName("TvdbShowID");

            entity.HasMany(d => d.ImageXRefs).WithOne().HasPrincipalKey(a => a.TmdbShowID).HasForeignKey(d => d.TmdbEntityID);
            entity.HasMany(a => a.AllTitles).WithOne().HasForeignKey(a => a.ParentID).HasPrincipalKey(a => a.TmdbShowID);
            entity.HasMany(a => a.AllOverviews).WithOne().HasForeignKey(a => a.ParentID).HasPrincipalKey(a => a.TmdbShowID);
            entity.HasMany(a => a.Episodes).WithOne().HasForeignKey(a => a.TmdbSeasonID).HasPrincipalKey(a => a.TmdbShowID);
            entity.HasMany(a => a.Seasons).WithOne().HasForeignKey(a => a.TmdbShowID).HasPrincipalKey(a => a.TmdbShowID);
            entity.HasMany(a => a.CompanyXRefs).WithOne().HasForeignKey(a => a.TmdbEntityID).HasPrincipalKey(a => a.TmdbShowID);
            entity.HasMany(a => a.NetworkXRefs).WithOne().HasForeignKey(a => a.TmdbShowID).HasPrincipalKey(a => a.TmdbShowID);
            entity.HasMany(a => a.AlternateOrderings).WithOne(a => a.Show).HasForeignKey(a => a.TmdbShowID).HasPrincipalKey(a => a.TmdbShowID);
        });

        modelBuilder.Entity<TMDB_Show_Network>(entity =>
        {
            entity.ToTable("Tmdb_Show_Network");

            entity.Property(e => e.TMDB_Show_NetworkID).HasColumnName("TMDB_Show_NetworkID");
            entity.Property(e => e.TmdbNetworkID).HasColumnName("TmdbNetworkID");
            entity.Property(e => e.TmdbShowID).HasColumnName("TmdbShowID");

            entity.HasOne(d => d.Network).WithMany(a => a.NetworkXRefs).HasPrincipalKey(a => a.TmdbNetworkID).HasForeignKey(d => d.TmdbNetworkID);
            entity.HasOne(d => d.Show).WithMany(a => a.NetworkXRefs).HasPrincipalKey(a => a.TmdbShowID).HasForeignKey(d => d.TmdbShowID);
        });

        modelBuilder.Entity<TMDB_Title>(entity =>
        {
            entity.ToTable("TMDB_Title");
            entity.HasDiscriminator(a => a.ParentType)
                .HasValue<TMDB_Title_Episode>(ForeignEntityType.Episode)
                .HasValue<TMDB_Title_Season>(ForeignEntityType.Season)
                .HasValue<TMDB_Title_Movie>(ForeignEntityType.Movie)
                .HasValue<TMDB_Title_TVShow>(ForeignEntityType.Show)
                .HasValue<TMDB_Title_Collection>(ForeignEntityType.Collection);

            entity.HasIndex(e => new { e.ParentType, e.ParentID }, "IX_TMDB_Title");

            entity.Property(e => e.TMDB_TitleID).HasColumnName("TMDB_TitleID");
            entity.Property(e => e.CountryCode).IsRequired();
            entity.Property(e => e.LanguageCode).IsRequired();
            entity.Property(e => e.ParentID).HasColumnName("ParentID");
            entity.Property(e => e.Value).IsRequired();
        });
    }
}
