using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shoko.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialTMDB : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TMDB_AlternateOrdering",
                columns: table => new
                {
                    TMDB_AlternateOrderingID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TmdbShowID = table.Column<int>(type: "INTEGER", nullable: false),
                    TmdbNetworkID = table.Column<int>(type: "INTEGER", nullable: true),
                    TmdbEpisodeGroupCollectionID = table.Column<string>(type: "TEXT", nullable: false),
                    EnglishTitle = table.Column<string>(type: "TEXT", nullable: false),
                    EnglishOverview = table.Column<string>(type: "TEXT", nullable: false),
                    EpisodeCount = table.Column<int>(type: "INTEGER", nullable: false),
                    HiddenEpisodeCount = table.Column<int>(type: "INTEGER", nullable: false),
                    SeasonCount = table.Column<int>(type: "INTEGER", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "DATETIME", nullable: false),
                    LastUpdatedAt = table.Column<DateTime>(type: "DATETIME", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TMDB_AlternateOrdering", x => x.TMDB_AlternateOrderingID);
                });

            migrationBuilder.CreateTable(
                name: "TMDB_AlternateOrdering_Episode",
                columns: table => new
                {
                    TMDB_AlternateOrdering_EpisodeID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TmdbShowID = table.Column<int>(type: "INTEGER", nullable: false),
                    TmdbEpisodeGroupCollectionID = table.Column<string>(type: "TEXT", nullable: false),
                    TmdbEpisodeGroupID = table.Column<string>(type: "TEXT", nullable: false),
                    TmdbEpisodeID = table.Column<int>(type: "INTEGER", nullable: false),
                    SeasonNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    EpisodeNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "DATETIME", nullable: false),
                    LastUpdatedAt = table.Column<DateTime>(type: "DATETIME", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TMDB_AlternateOrdering_Episode", x => x.TMDB_AlternateOrdering_EpisodeID);
                });

            migrationBuilder.CreateTable(
                name: "TMDB_AlternateOrdering_Season",
                columns: table => new
                {
                    TMDB_AlternateOrdering_SeasonID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TmdbShowID = table.Column<int>(type: "INTEGER", nullable: false),
                    TmdbEpisodeGroupCollectionID = table.Column<string>(type: "TEXT", nullable: false),
                    TmdbEpisodeGroupID = table.Column<string>(type: "TEXT", nullable: false),
                    EnglishTitle = table.Column<string>(type: "TEXT", nullable: false),
                    SeasonNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    EpisodeCount = table.Column<int>(type: "INTEGER", nullable: false),
                    HiddenEpisodeCount = table.Column<int>(type: "INTEGER", nullable: false),
                    IsLocked = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "DATETIME", nullable: false),
                    LastUpdatedAt = table.Column<DateTime>(type: "DATETIME", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TMDB_AlternateOrdering_Season", x => x.TMDB_AlternateOrdering_SeasonID);
                });

            migrationBuilder.CreateTable(
                name: "TMDB_Collection",
                columns: table => new
                {
                    TMDB_CollectionID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TmdbCollectionID = table.Column<int>(type: "INTEGER", nullable: false),
                    EnglishTitle = table.Column<string>(type: "TEXT", nullable: false),
                    EnglishOverview = table.Column<string>(type: "TEXT", nullable: false),
                    MovieCount = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "DATETIME", nullable: false),
                    LastUpdatedAt = table.Column<DateTime>(type: "DATETIME", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TMDB_Collection", x => x.TMDB_CollectionID);
                });

            migrationBuilder.CreateTable(
                name: "TMDB_Collection_Movie",
                columns: table => new
                {
                    TMDB_Collection_MovieID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TmdbCollectionID = table.Column<int>(type: "INTEGER", nullable: false),
                    TmdbMovieID = table.Column<int>(type: "INTEGER", nullable: false),
                    Ordering = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TMDB_Collection_Movie", x => x.TMDB_Collection_MovieID);
                });

            migrationBuilder.CreateTable(
                name: "TMDB_Company",
                columns: table => new
                {
                    TMDB_CompanyID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TmdbCompanyID = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    CountryOfOrigin = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TMDB_Company", x => x.TMDB_CompanyID);
                });

            migrationBuilder.CreateTable(
                name: "TMDB_Company_Entity",
                columns: table => new
                {
                    TMDB_Company_EntityID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TmdbCompanyID = table.Column<int>(type: "INTEGER", nullable: false),
                    TmdbEntityType = table.Column<int>(type: "INTEGER", nullable: false),
                    TmdbEntityID = table.Column<int>(type: "INTEGER", nullable: false),
                    Ordering = table.Column<int>(type: "INTEGER", nullable: false),
                    ReleasedAt = table.Column<DateOnly>(type: "DATE", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TMDB_Company_Entity", x => x.TMDB_Company_EntityID);
                });

            migrationBuilder.CreateTable(
                name: "TMDB_Episode",
                columns: table => new
                {
                    TMDB_EpisodeID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TmdbShowID = table.Column<int>(type: "INTEGER", nullable: false),
                    TmdbSeasonID = table.Column<int>(type: "INTEGER", nullable: false),
                    TmdbEpisodeID = table.Column<int>(type: "INTEGER", nullable: false),
                    TvdbEpisodeID = table.Column<int>(type: "INTEGER", nullable: true, defaultValueSql: "NULL"),
                    ThumbnailPath = table.Column<string>(type: "TEXT", nullable: false, defaultValueSql: "NULL"),
                    EnglishTitle = table.Column<string>(type: "TEXT", nullable: false),
                    EnglishOverview = table.Column<string>(type: "TEXT", nullable: false),
                    IsHidden = table.Column<bool>(type: "INTEGER", nullable: false),
                    SeasonNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    EpisodeNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    RuntimeMinutes = table.Column<int>(type: "INTEGER", nullable: true),
                    UserRating = table.Column<double>(type: "REAL", nullable: false),
                    UserVotes = table.Column<int>(type: "INTEGER", nullable: false),
                    AiredAt = table.Column<DateOnly>(type: "DATE", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "DATETIME", nullable: false),
                    LastUpdatedAt = table.Column<DateTime>(type: "DATETIME", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TMDB_Episode", x => x.TMDB_EpisodeID);
                });

            migrationBuilder.CreateTable(
                name: "TMDB_Episode_Cast",
                columns: table => new
                {
                    TMDB_Episode_CastID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TmdbShowID = table.Column<int>(type: "INTEGER", nullable: false),
                    TmdbSeasonID = table.Column<int>(type: "INTEGER", nullable: false),
                    TmdbEpisodeID = table.Column<int>(type: "INTEGER", nullable: false),
                    IsGuestRole = table.Column<bool>(type: "INTEGER", nullable: false),
                    TmdbPersonID = table.Column<int>(type: "INTEGER", nullable: false),
                    TmdbCreditID = table.Column<string>(type: "TEXT", nullable: false),
                    CharacterName = table.Column<string>(type: "TEXT", nullable: false),
                    Ordering = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TMDB_Episode_Cast", x => x.TMDB_Episode_CastID);
                });

            migrationBuilder.CreateTable(
                name: "TMDB_Episode_Crew",
                columns: table => new
                {
                    TMDB_Episode_CrewID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TmdbShowID = table.Column<int>(type: "INTEGER", nullable: false),
                    TmdbSeasonID = table.Column<int>(type: "INTEGER", nullable: false),
                    TmdbEpisodeID = table.Column<int>(type: "INTEGER", nullable: false),
                    TmdbPersonID = table.Column<int>(type: "INTEGER", nullable: false),
                    TmdbCreditID = table.Column<string>(type: "TEXT", nullable: false),
                    Job = table.Column<string>(type: "TEXT", nullable: false),
                    Department = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TMDB_Episode_Crew", x => x.TMDB_Episode_CrewID);
                });

            migrationBuilder.CreateTable(
                name: "TMDB_Image",
                columns: table => new
                {
                    TMDB_ImageID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Width = table.Column<int>(type: "INTEGER", nullable: false),
                    Height = table.Column<int>(type: "INTEGER", nullable: false),
                    RemoteFileName = table.Column<string>(type: "TEXT", nullable: false),
                    UserRating = table.Column<double>(type: "REAL", nullable: false),
                    UserVotes = table.Column<int>(type: "INTEGER", nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    Language = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TMDB_Image", x => x.TMDB_ImageID);
                });

            migrationBuilder.CreateTable(
                name: "TMDB_Movie",
                columns: table => new
                {
                    TMDB_MovieID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TmdbMovieID = table.Column<int>(type: "INTEGER", nullable: false),
                    TmdbCollectionID = table.Column<int>(type: "INTEGER", nullable: true),
                    ImdbMovieID = table.Column<string>(type: "TEXT", nullable: true, defaultValueSql: "NULL"),
                    PosterPath = table.Column<string>(type: "TEXT", nullable: false, defaultValueSql: "NULL"),
                    BackdropPath = table.Column<string>(type: "TEXT", nullable: false, defaultValueSql: "NULL"),
                    EnglishTitle = table.Column<string>(type: "TEXT", nullable: false),
                    EnglishOverview = table.Column<string>(type: "TEXT", nullable: false),
                    OriginalTitle = table.Column<string>(type: "TEXT", nullable: false),
                    OriginalLanguageCode = table.Column<string>(type: "TEXT", nullable: false),
                    IsRestricted = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsVideo = table.Column<bool>(type: "INTEGER", nullable: false),
                    Genres = table.Column<string>(type: "TEXT", nullable: false),
                    Keywords = table.Column<string>(type: "TEXT", nullable: false, defaultValueSql: "NULL"),
                    ContentRatings = table.Column<string>(type: "TEXT", nullable: false),
                    ProductionCountries = table.Column<string>(type: "TEXT", nullable: false, defaultValueSql: "NULL"),
                    RuntimeMinutes = table.Column<int>(type: "INTEGER", nullable: true),
                    UserRating = table.Column<double>(type: "REAL", nullable: false),
                    UserVotes = table.Column<int>(type: "INTEGER", nullable: false),
                    ReleasedAt = table.Column<DateOnly>(type: "DATE", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "DATETIME", nullable: false),
                    LastUpdatedAt = table.Column<DateTime>(type: "DATETIME", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TMDB_Movie", x => x.TMDB_MovieID);
                });

            migrationBuilder.CreateTable(
                name: "TMDB_Movie_Cast",
                columns: table => new
                {
                    TMDB_Movie_CastID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TmdbMovieID = table.Column<int>(type: "INT", nullable: false),
                    TmdbPersonID = table.Column<int>(type: "INT", nullable: false),
                    TmdbCreditID = table.Column<string>(type: "TEXT", nullable: false),
                    CharacterName = table.Column<string>(type: "TEXT", nullable: false),
                    Ordering = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TMDB_Movie_Cast", x => x.TMDB_Movie_CastID);
                });

            migrationBuilder.CreateTable(
                name: "TMDB_Movie_Crew",
                columns: table => new
                {
                    TMDB_Movie_CrewID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TmdbMovieID = table.Column<int>(type: "INTEGER", nullable: false),
                    TmdbPersonID = table.Column<int>(type: "INTEGER", nullable: false),
                    TmdbCreditID = table.Column<string>(type: "TEXT", nullable: false),
                    Job = table.Column<string>(type: "TEXT", nullable: false),
                    Department = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TMDB_Movie_Crew", x => x.TMDB_Movie_CrewID);
                });

            migrationBuilder.CreateTable(
                name: "TMDB_Network",
                columns: table => new
                {
                    TMDB_NetworkID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TmdbNetworkID = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    CountryOfOrigin = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TMDB_Network", x => x.TMDB_NetworkID);
                });

            migrationBuilder.CreateTable(
                name: "TMDB_Overview",
                columns: table => new
                {
                    TMDB_OverviewID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ParentID = table.Column<int>(type: "INTEGER", nullable: false),
                    ParentType = table.Column<int>(type: "INTEGER", nullable: false),
                    LanguageCode = table.Column<string>(type: "TEXT", nullable: false),
                    CountryCode = table.Column<string>(type: "TEXT", nullable: false),
                    Value = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TMDB_Overview", x => x.TMDB_OverviewID);
                });

            migrationBuilder.CreateTable(
                name: "TMDB_Person",
                columns: table => new
                {
                    TMDB_PersonID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TmdbPersonID = table.Column<int>(type: "INTEGER", nullable: false),
                    EnglishName = table.Column<string>(type: "TEXT", nullable: false),
                    EnglishBiography = table.Column<string>(type: "TEXT", nullable: false),
                    Aliases = table.Column<string>(type: "TEXT", nullable: false),
                    Gender = table.Column<int>(type: "INTEGER", nullable: false),
                    IsRestricted = table.Column<bool>(type: "INTEGER", nullable: false),
                    BirthDay = table.Column<DateOnly>(type: "DATE", nullable: true),
                    DeathDay = table.Column<DateOnly>(type: "DATE", nullable: true),
                    PlaceOfBirth = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "DATETIME", nullable: false),
                    LastUpdatedAt = table.Column<DateTime>(type: "DATETIME", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TMDB_Person", x => x.TMDB_PersonID);
                });

            migrationBuilder.CreateTable(
                name: "TMDB_Season",
                columns: table => new
                {
                    TMDB_SeasonID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TmdbShowID = table.Column<int>(type: "INTEGER", nullable: false),
                    TmdbSeasonID = table.Column<int>(type: "INTEGER", nullable: false),
                    PosterPath = table.Column<string>(type: "TEXT", nullable: false, defaultValueSql: "NULL"),
                    EnglishTitle = table.Column<string>(type: "TEXT", nullable: false),
                    EnglishOverview = table.Column<string>(type: "TEXT", nullable: false),
                    EpisodeCount = table.Column<int>(type: "INTEGER", nullable: false),
                    HiddenEpisodeCount = table.Column<int>(type: "INTEGER", nullable: false),
                    SeasonNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "DATETIME", nullable: false),
                    LastUpdatedAt = table.Column<DateTime>(type: "DATETIME", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TMDB_Season", x => x.TMDB_SeasonID);
                });

            migrationBuilder.CreateTable(
                name: "TMDB_Show",
                columns: table => new
                {
                    TMDB_ShowID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TmdbShowID = table.Column<int>(type: "INTEGER", nullable: false),
                    TvdbShowID = table.Column<int>(type: "INTEGER", nullable: true, defaultValueSql: "NULL"),
                    PosterPath = table.Column<string>(type: "TEXT", nullable: false, defaultValueSql: "NULL"),
                    BackdropPath = table.Column<string>(type: "TEXT", nullable: false, defaultValueSql: "NULL"),
                    EnglishTitle = table.Column<string>(type: "TEXT", nullable: false),
                    EnglishOverview = table.Column<string>(type: "TEXT", nullable: false),
                    OriginalTitle = table.Column<string>(type: "TEXT", nullable: false),
                    OriginalLanguageCode = table.Column<string>(type: "TEXT", nullable: false),
                    IsRestricted = table.Column<bool>(type: "INTEGER", nullable: false),
                    Genres = table.Column<string>(type: "TEXT", nullable: false),
                    Keywords = table.Column<string>(type: "TEXT", nullable: false, defaultValueSql: "NULL"),
                    ContentRatings = table.Column<string>(type: "TEXT", nullable: false),
                    ProductionCountries = table.Column<string>(type: "TEXT", nullable: false, defaultValueSql: "NULL"),
                    EpisodeCount = table.Column<int>(type: "INTEGER", nullable: false),
                    HiddenEpisodeCount = table.Column<int>(type: "INTEGER", nullable: false),
                    SeasonCount = table.Column<int>(type: "INTEGER", nullable: false),
                    AlternateOrderingCount = table.Column<int>(type: "INTEGER", nullable: false),
                    UserRating = table.Column<double>(type: "REAL", nullable: false),
                    UserVotes = table.Column<int>(type: "INTEGER", nullable: false),
                    FirstAiredAt = table.Column<DateOnly>(type: "DATE", nullable: true),
                    LastAiredAt = table.Column<DateOnly>(type: "DATE", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "DATETIME", nullable: false),
                    LastUpdatedAt = table.Column<DateTime>(type: "DATETIME", nullable: false),
                    PreferredAlternateOrderingID = table.Column<string>(type: "TEXT", nullable: true, defaultValueSql: "NULL")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TMDB_Show", x => x.TMDB_ShowID);
                });

            migrationBuilder.CreateTable(
                name: "Tmdb_Show_Network",
                columns: table => new
                {
                    TMDB_Show_NetworkID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TmdbShowID = table.Column<int>(type: "INTEGER", nullable: false),
                    TmdbNetworkID = table.Column<int>(type: "INTEGER", nullable: false),
                    Ordering = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tmdb_Show_Network", x => x.TMDB_Show_NetworkID);
                });

            migrationBuilder.CreateTable(
                name: "TMDB_Title",
                columns: table => new
                {
                    TMDB_TitleID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ParentID = table.Column<int>(type: "INTEGER", nullable: false),
                    ParentType = table.Column<int>(type: "INTEGER", nullable: false),
                    LanguageCode = table.Column<string>(type: "TEXT", nullable: false),
                    CountryCode = table.Column<string>(type: "TEXT", nullable: false),
                    Value = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TMDB_Title", x => x.TMDB_TitleID);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TMDB_AlternateOrdering_TmdbEpisodeGroupCollectionID_TmdbShowID",
                table: "TMDB_AlternateOrdering",
                columns: new[] { "TmdbEpisodeGroupCollectionID", "TmdbShowID" });

            migrationBuilder.CreateIndex(
                name: "IX_TMDB_AlternateOrdering_TmdbShowID",
                table: "TMDB_AlternateOrdering",
                column: "TmdbShowID");

            migrationBuilder.CreateIndex(
                name: "UIX_TMDB_AlternateOrdering_TmdbEpisodeGroupCollectionID",
                table: "TMDB_AlternateOrdering",
                column: "TmdbEpisodeGroupCollectionID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TMDB_AlternateOrdering_Season_TmdbEpisodeGroupCollectionID",
                table: "TMDB_AlternateOrdering_Season",
                column: "TmdbEpisodeGroupCollectionID");

            migrationBuilder.CreateIndex(
                name: "IX_TMDB_AlternateOrdering_Season_TmdbShowID",
                table: "TMDB_AlternateOrdering_Season",
                column: "TmdbShowID");

            migrationBuilder.CreateIndex(
                name: "UIX_TMDB_AlternateOrdering_Season_TmdbEpisodeGroupID",
                table: "TMDB_AlternateOrdering_Season",
                column: "TmdbEpisodeGroupID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UIX_TMDB_Collection_TmdbCollectionID",
                table: "TMDB_Collection",
                column: "TMDB_CollectionID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TMDB_Collection_Movie_TmdbCollectionID",
                table: "TMDB_Collection_Movie",
                column: "TmdbCollectionID");

            migrationBuilder.CreateIndex(
                name: "IX_TMDB_Collection_Movie_TmdbMovieID",
                table: "TMDB_Collection_Movie",
                column: "TmdbMovieID");

            migrationBuilder.CreateIndex(
                name: "IX_TMDB_Company_TmdbCompanyID",
                table: "TMDB_Company",
                column: "TMDB_CompanyID");

            migrationBuilder.CreateIndex(
                name: "IX_TMDB_Company_Entity_TmdbCompanyID",
                table: "TMDB_Company_Entity",
                column: "TmdbCompanyID");

            migrationBuilder.CreateIndex(
                name: "IX_TMDB_Company_Entity_TmdbEntityType_TmdbEntityID",
                table: "TMDB_Company_Entity",
                columns: new[] { "TmdbEntityType", "TmdbEntityID" });

            migrationBuilder.CreateIndex(
                name: "IX_TMDB_Episode_TmdbSeasonID",
                table: "TMDB_Episode",
                column: "TmdbSeasonID");

            migrationBuilder.CreateIndex(
                name: "IX_TMDB_Episode_TmdbShowID",
                table: "TMDB_Episode",
                column: "TmdbShowID");

            migrationBuilder.CreateIndex(
                name: "UIX_TMDB_Episode_TmdbEpisodeID",
                table: "TMDB_Episode",
                column: "TMDB_EpisodeID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TMDB_Episode_Cast_TmdbEpisodeID",
                table: "TMDB_Episode_Cast",
                column: "TmdbEpisodeID");

            migrationBuilder.CreateIndex(
                name: "IX_TMDB_Episode_Cast_TmdbPersonID",
                table: "TMDB_Episode_Cast",
                column: "TmdbPersonID");

            migrationBuilder.CreateIndex(
                name: "IX_TMDB_Episode_Cast_TmdbSeasonID",
                table: "TMDB_Episode_Cast",
                column: "TmdbSeasonID");

            migrationBuilder.CreateIndex(
                name: "IX_TMDB_Episode_Cast_TmdbShowID",
                table: "TMDB_Episode_Cast",
                column: "TmdbShowID");

            migrationBuilder.CreateIndex(
                name: "IX_TMDB_Episode_Crew_TmdbEpisodeID",
                table: "TMDB_Episode_Crew",
                column: "TmdbEpisodeID");

            migrationBuilder.CreateIndex(
                name: "IX_TMDB_Episode_Crew_TmdbPersonID",
                table: "TMDB_Episode_Crew",
                column: "TmdbPersonID");

            migrationBuilder.CreateIndex(
                name: "IX_TMDB_Episode_Crew_TmdbSeasonID",
                table: "TMDB_Episode_Crew",
                column: "TmdbSeasonID");

            migrationBuilder.CreateIndex(
                name: "IX_TMDB_Episode_Crew_TmdbShowID",
                table: "TMDB_Episode_Crew",
                column: "TmdbShowID");

            migrationBuilder.CreateIndex(
                name: "IX_TMDB_Movie_TmdbCollectionID",
                table: "TMDB_Movie",
                column: "TmdbCollectionID");

            migrationBuilder.CreateIndex(
                name: "UIX_TMDB_Movie_TmdbMovieID",
                table: "TMDB_Movie",
                column: "TmdbMovieID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TMDB_Movie_Cast_TmdbMovieID",
                table: "TMDB_Movie_Cast",
                column: "TmdbMovieID");

            migrationBuilder.CreateIndex(
                name: "IX_TMDB_Movie_Cast_TmdbPersonID",
                table: "TMDB_Movie_Cast",
                column: "TmdbPersonID");

            migrationBuilder.CreateIndex(
                name: "IX_TMDB_Movie_Crew_TmdbMovieID",
                table: "TMDB_Movie_Crew",
                column: "TmdbMovieID");

            migrationBuilder.CreateIndex(
                name: "IX_TMDB_Movie_Crew_TmdbPersonID",
                table: "TMDB_Movie_Crew",
                column: "TmdbPersonID");

            migrationBuilder.CreateIndex(
                name: "UIX_TMDB_Network_TmdbNetworkID",
                table: "TMDB_Network",
                column: "TmdbNetworkID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TMDB_Overview",
                table: "TMDB_Overview",
                columns: new[] { "ParentType", "ParentID" });

            migrationBuilder.CreateIndex(
                name: "IX_TMDB_Person_TmdbPersonID",
                table: "TMDB_Person",
                column: "TmdbPersonID");

            migrationBuilder.CreateIndex(
                name: "IX_TMDB_Season_TmdbShowID",
                table: "TMDB_Season",
                column: "TmdbShowID");

            migrationBuilder.CreateIndex(
                name: "UIX_TMDB_Season_TmdbSeasonID",
                table: "TMDB_Season",
                column: "TmdbSeasonID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UIX_TMDB_Show_TmdbShowID",
                table: "TMDB_Show",
                column: "TmdbShowID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TMDB_Title",
                table: "TMDB_Title",
                columns: new[] { "ParentType", "ParentID" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TMDB_AlternateOrdering");

            migrationBuilder.DropTable(
                name: "TMDB_AlternateOrdering_Episode");

            migrationBuilder.DropTable(
                name: "TMDB_AlternateOrdering_Season");

            migrationBuilder.DropTable(
                name: "TMDB_Collection");

            migrationBuilder.DropTable(
                name: "TMDB_Collection_Movie");

            migrationBuilder.DropTable(
                name: "TMDB_Company");

            migrationBuilder.DropTable(
                name: "TMDB_Company_Entity");

            migrationBuilder.DropTable(
                name: "TMDB_Episode");

            migrationBuilder.DropTable(
                name: "TMDB_Episode_Cast");

            migrationBuilder.DropTable(
                name: "TMDB_Episode_Crew");

            migrationBuilder.DropTable(
                name: "TMDB_Image");

            migrationBuilder.DropTable(
                name: "TMDB_Movie");

            migrationBuilder.DropTable(
                name: "TMDB_Movie_Cast");

            migrationBuilder.DropTable(
                name: "TMDB_Movie_Crew");

            migrationBuilder.DropTable(
                name: "TMDB_Network");

            migrationBuilder.DropTable(
                name: "TMDB_Overview");

            migrationBuilder.DropTable(
                name: "TMDB_Person");

            migrationBuilder.DropTable(
                name: "TMDB_Season");

            migrationBuilder.DropTable(
                name: "TMDB_Show");

            migrationBuilder.DropTable(
                name: "Tmdb_Show_Network");

            migrationBuilder.DropTable(
                name: "TMDB_Title");
        }
    }
}
