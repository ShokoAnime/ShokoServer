using System;
using System.IO;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Shoko.Server.Migrations
{
    public partial class Initial : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            Console.WriteLine(migrationBuilder.ActiveProvider);
            Console.WriteLine(migrationBuilder.ActiveProvider);
            Console.WriteLine(migrationBuilder.ActiveProvider);
            Console.WriteLine(migrationBuilder.ActiveProvider);
            Console.WriteLine(string.Join("||", this.GetType().Assembly.GetManifestResourceNames()));
            Console.WriteLine(string.Join("||", this.GetType().Assembly.GetManifestResourceNames()));
            Console.WriteLine(string.Join("||", this.GetType().Assembly.GetManifestResourceNames()));
            Console.WriteLine(string.Join("||", this.GetType().Assembly.GetManifestResourceNames()));
            using (var sr = new StreamReader(this.GetType().Assembly.GetManifestResourceStream($"Shoko.Server.Migrations.{migrationBuilder.ActiveProvider}.sql")))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    migrationBuilder.Sql(line);
                }
                return;
            }

            migrationBuilder.CreateTable(
                name: "AniDB_Anime",
                columns: table => new
                {
                    AniDB_AnimeID = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AnimeID = table.Column<int>(nullable: false),
                    EpisodeCount = table.Column<int>(nullable: false),
                    AirDate = table.Column<DateTime>(nullable: true),
                    EndDate = table.Column<DateTime>(nullable: true),
                    URL = table.Column<string>(nullable: true),
                    Picname = table.Column<string>(nullable: true),
                    BeginYear = table.Column<int>(nullable: false),
                    EndYear = table.Column<int>(nullable: false),
                    AnimeType = table.Column<int>(nullable: false),
                    MainTitle = table.Column<string>(maxLength: 500, nullable: false),
                    AllTitles = table.Column<string>(maxLength: 1500, nullable: false),
                    AllTags = table.Column<string>(nullable: false),
                    Description = table.Column<string>(nullable: false),
                    EpisodeCountNormal = table.Column<int>(nullable: false),
                    EpisodeCountSpecial = table.Column<int>(nullable: false),
                    Rating = table.Column<int>(nullable: false),
                    VoteCount = table.Column<int>(nullable: false),
                    TempRating = table.Column<int>(nullable: false),
                    TempVoteCount = table.Column<int>(nullable: false),
                    AvgReviewRating = table.Column<int>(nullable: false),
                    ReviewCount = table.Column<int>(nullable: false),
                    DateTimeUpdated = table.Column<DateTime>(nullable: false),
                    DateTimeDescUpdated = table.Column<DateTime>(nullable: false),
                    ImageEnabled = table.Column<int>(nullable: false),
                    AwardList = table.Column<string>(nullable: false),
                    Restricted = table.Column<int>(nullable: false),
                    AnimePlanetID = table.Column<int>(nullable: true),
                    ANNID = table.Column<int>(nullable: true),
                    AllCinemaID = table.Column<int>(nullable: true),
                    AnimeNfo = table.Column<int>(nullable: true),
                    AnisonID = table.Column<int>(nullable: true),
                    SyoboiID = table.Column<int>(nullable: true),
                    Site_JP = table.Column<string>(nullable: true),
                    Site_EN = table.Column<string>(nullable: true),
                    Wikipedia_ID = table.Column<string>(nullable: true),
                    WikipediaJP_ID = table.Column<string>(nullable: true),
                    CrunchyrollID = table.Column<string>(nullable: true),
                    LatestEpisodeNumber = table.Column<int>(nullable: true),
                    DisableExternalLinksFlag = table.Column<int>(nullable: false),
                    ContractVersion = table.Column<int>(nullable: false, defaultValue: 0)
                        .Annotation("Sqlite:Autoincrement", true),
                    ContractBlob = table.Column<byte[]>(nullable: true),
                    ContractSize = table.Column<int>(nullable: false, defaultValue: 0)
                        .Annotation("Sqlite:Autoincrement", true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AniDB_Anime", x => x.AniDB_AnimeID);
                });

            migrationBuilder.CreateTable(
                name: "AniDB_Anime_Character",
                columns: table => new
                {
                    AniDB_Anime_CharacterID = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AnimeID = table.Column<int>(nullable: false),
                    CharID = table.Column<int>(nullable: false),
                    CharType = table.Column<string>(maxLength: 100, nullable: false),
                    EpisodeListRaw = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AniDB_Anime_Character", x => x.AniDB_Anime_CharacterID);
                });

            migrationBuilder.CreateTable(
                name: "AniDB_Anime_Relation",
                columns: table => new
                {
                    AniDB_Anime_RelationID = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AnimeID = table.Column<int>(nullable: false),
                    RelationType = table.Column<string>(maxLength: 100, nullable: false),
                    RelatedAnimeID = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AniDB_Anime_Relation", x => x.AniDB_Anime_RelationID);
                });

            migrationBuilder.CreateTable(
                name: "AniDB_Anime_Similar",
                columns: table => new
                {
                    AniDB_Anime_SimilarID = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AnimeID = table.Column<int>(nullable: false),
                    SimilarAnimeID = table.Column<int>(nullable: false),
                    Approval = table.Column<int>(nullable: false),
                    Total = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AniDB_Anime_Similar", x => x.AniDB_Anime_SimilarID);
                });

            migrationBuilder.CreateTable(
                name: "AniDB_Anime_Tag",
                columns: table => new
                {
                    AniDB_Anime_TagID = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AnimeID = table.Column<int>(nullable: false),
                    TagID = table.Column<int>(nullable: false),
                    Approval = table.Column<int>(nullable: false),
                    Weight = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AniDB_Anime_Tag", x => x.AniDB_Anime_TagID);
                });

            migrationBuilder.CreateTable(
                name: "AniDB_Anime_Title",
                columns: table => new
                {
                    AniDB_Anime_TitleID = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AnimeID = table.Column<int>(nullable: false),
                    TitleType = table.Column<string>(maxLength: 50, nullable: false),
                    Language = table.Column<string>(maxLength: 50, nullable: false),
                    Title = table.Column<string>(maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AniDB_Anime_Title", x => x.AniDB_Anime_TitleID);
                });

            migrationBuilder.CreateTable(
                name: "AniDB_AnimeUpdate",
                columns: table => new
                {
                    AniDB_AnimeUpdateID = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AnimeID = table.Column<int>(nullable: false),
                    UpdatedAt = table.Column<DateTime>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AniDB_AnimeUpdate", x => x.AniDB_AnimeUpdateID);
                });

            migrationBuilder.CreateTable(
                name: "AniDB_Character",
                columns: table => new
                {
                    AniDB_CharacterID = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CharID = table.Column<int>(nullable: false),
                    PicName = table.Column<string>(maxLength: 100, nullable: false),
                    CreatorListRaw = table.Column<string>(nullable: false),
                    CharName = table.Column<string>(maxLength: 200, nullable: false),
                    CharKanjiName = table.Column<string>(nullable: false),
                    CharDescription = table.Column<string>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AniDB_Character", x => x.AniDB_CharacterID);
                });

            migrationBuilder.CreateTable(
                name: "AniDB_Character_Seiyuu",
                columns: table => new
                {
                    AniDB_Character_SeiyuuID = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CharID = table.Column<int>(nullable: false),
                    SeiyuuID = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AniDB_Character_Seiyuu", x => x.AniDB_Character_SeiyuuID);
                });

            migrationBuilder.CreateTable(
                name: "AniDB_Episode_Title",
                columns: table => new
                {
                    AniDB_Episode_TitleID = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AniDB_EpisodeID = table.Column<int>(nullable: false),
                    Language = table.Column<string>(nullable: true),
                    Title = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AniDB_Episode_Title", x => x.AniDB_Episode_TitleID);
                });

            migrationBuilder.CreateTable(
                name: "AniDB_File",
                columns: table => new
                {
                    AniDB_FileID = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FileID = table.Column<int>(nullable: false),
                    Hash = table.Column<string>(maxLength: 50, nullable: false),
                    AnimeID = table.Column<int>(nullable: false),
                    GroupID = table.Column<int>(nullable: false),
                    File_Source = table.Column<string>(nullable: false),
                    File_AudioCodec = table.Column<string>(nullable: false),
                    File_VideoCodec = table.Column<string>(nullable: false),
                    File_VideoResolution = table.Column<string>(nullable: false),
                    File_FileExtension = table.Column<string>(nullable: false),
                    File_LengthSeconds = table.Column<int>(nullable: false),
                    File_Description = table.Column<string>(nullable: false),
                    File_ReleaseDate = table.Column<int>(nullable: false),
                    Anime_GroupName = table.Column<string>(nullable: false),
                    Anime_GroupNameShort = table.Column<string>(nullable: false),
                    Episode_Rating = table.Column<int>(nullable: false),
                    Episode_Votes = table.Column<int>(nullable: false),
                    DateTimeUpdated = table.Column<DateTime>(nullable: false),
                    IsWatched = table.Column<int>(nullable: false),
                    WatchedDate = table.Column<DateTime>(nullable: true),
                    CRC = table.Column<string>(nullable: false),
                    MD5 = table.Column<string>(nullable: false),
                    SHA1 = table.Column<string>(nullable: false),
                    FileName = table.Column<string>(nullable: false),
                    FileSize = table.Column<long>(nullable: false),
                    FileVersion = table.Column<int>(nullable: false),
                    IsCensored = table.Column<int>(nullable: false),
                    IsDeprecated = table.Column<int>(nullable: false),
                    InternalVersion = table.Column<int>(nullable: false),
                    IsChaptered = table.Column<int>(nullable: false, defaultValue: -1)
                        .Annotation("Sqlite:Autoincrement", true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AniDB_File", x => x.AniDB_FileID);
                });

            migrationBuilder.CreateTable(
                name: "AniDB_GroupStatus",
                columns: table => new
                {
                    AniDB_GroupStatusID = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AnimeID = table.Column<int>(nullable: false),
                    GroupID = table.Column<int>(nullable: false),
                    GroupName = table.Column<string>(maxLength: 200, nullable: false),
                    CompletionState = table.Column<int>(nullable: false),
                    LastEpisodeNumber = table.Column<int>(nullable: false),
                    Rating = table.Column<int>(nullable: false),
                    Votes = table.Column<int>(nullable: false),
                    EpisodeRange = table.Column<string>(maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AniDB_GroupStatus", x => x.AniDB_GroupStatusID);
                });

            migrationBuilder.CreateTable(
                name: "AniDB_MylistStats",
                columns: table => new
                {
                    AniDB_MylistStatsID = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Animes = table.Column<int>(nullable: false),
                    Episodes = table.Column<int>(nullable: false),
                    Files = table.Column<int>(nullable: false),
                    SizeOfFiles = table.Column<long>(nullable: false),
                    AddedAnimes = table.Column<int>(nullable: false),
                    AddedEpisodes = table.Column<int>(nullable: false),
                    AddedFiles = table.Column<int>(nullable: false),
                    AddedGroups = table.Column<int>(nullable: false),
                    LeechPct = table.Column<int>(nullable: false),
                    GloryPct = table.Column<int>(nullable: false),
                    ViewedPct = table.Column<int>(nullable: false),
                    MylistPct = table.Column<int>(nullable: false),
                    ViewedMylistPct = table.Column<int>(nullable: false),
                    EpisodesViewed = table.Column<int>(nullable: false),
                    Votes = table.Column<int>(nullable: false),
                    Reviews = table.Column<int>(nullable: false),
                    ViewiedLength = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AniDB_MylistStats", x => x.AniDB_MylistStatsID);
                });

            migrationBuilder.CreateTable(
                name: "AniDB_Recommendation",
                columns: table => new
                {
                    AniDB_RecommendationID = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AnimeID = table.Column<int>(nullable: false),
                    UserID = table.Column<int>(nullable: false),
                    RecommendationType = table.Column<int>(nullable: false),
                    RecommendationText = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AniDB_Recommendation", x => x.AniDB_RecommendationID);
                });

            migrationBuilder.CreateTable(
                name: "AniDB_ReleaseGroup",
                columns: table => new
                {
                    AniDB_ReleaseGroupID = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GroupID = table.Column<int>(nullable: false),
                    Rating = table.Column<int>(nullable: false),
                    Votes = table.Column<int>(nullable: false),
                    AnimeCount = table.Column<int>(nullable: false),
                    FileCount = table.Column<int>(nullable: false),
                    GroupName = table.Column<string>(nullable: false),
                    GroupNameShort = table.Column<string>(maxLength: 200, nullable: false),
                    IRCChannel = table.Column<string>(maxLength: 200, nullable: false),
                    IRCServer = table.Column<string>(maxLength: 200, nullable: false),
                    URL = table.Column<string>(maxLength: 200, nullable: false),
                    Picname = table.Column<string>(maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AniDB_ReleaseGroup", x => x.AniDB_ReleaseGroupID);
                });

            migrationBuilder.CreateTable(
                name: "AniDB_Review",
                columns: table => new
                {
                    AniDB_ReviewID = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ReviewID = table.Column<int>(nullable: false),
                    AuthorID = table.Column<int>(nullable: false),
                    RatingAnimation = table.Column<int>(nullable: false),
                    RatingSound = table.Column<int>(nullable: false),
                    RatingStory = table.Column<int>(nullable: false),
                    RatingCharacter = table.Column<int>(nullable: false),
                    RatingValue = table.Column<int>(nullable: false),
                    RatingEnjoyment = table.Column<int>(nullable: false),
                    ReviewText = table.Column<string>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AniDB_Review", x => x.AniDB_ReviewID);
                });

            migrationBuilder.CreateTable(
                name: "AniDB_Seiyuu",
                columns: table => new
                {
                    AniDB_SeiyuuID = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SeiyuuID = table.Column<int>(nullable: false),
                    SeiyuuName = table.Column<string>(maxLength: 200, nullable: false),
                    PicName = table.Column<string>(maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AniDB_Seiyuu", x => x.AniDB_SeiyuuID);
                });

            migrationBuilder.CreateTable(
                name: "AniDB_Tag",
                columns: table => new
                {
                    AniDB_TagID = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TagID = table.Column<int>(nullable: false),
                    Spoiler = table.Column<int>(nullable: false),
                    LocalSpoiler = table.Column<int>(nullable: false),
                    GlobalSpoiler = table.Column<int>(nullable: false),
                    TagCount = table.Column<int>(nullable: false),
                    TagName = table.Column<string>(maxLength: 150, nullable: false),
                    TagDescription = table.Column<string>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AniDB_Tag", x => x.AniDB_TagID);
                });

            migrationBuilder.CreateTable(
                name: "AniDB_Vote",
                columns: table => new
                {
                    AniDB_VoteID = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EntityID = table.Column<int>(nullable: false),
                    VoteValue = table.Column<int>(nullable: false),
                    VoteType = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AniDB_Vote", x => x.AniDB_VoteID);
                });

            migrationBuilder.CreateTable(
                name: "AnimeCharacter",
                columns: table => new
                {
                    CharacterID = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AniDBID = table.Column<int>(nullable: false),
                    Name = table.Column<string>(nullable: false),
                    AlternateName = table.Column<string>(nullable: true),
                    Description = table.Column<string>(nullable: true, defaultValue: ""),
                    ImagePath = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnimeCharacter", x => x.CharacterID);
                });

            migrationBuilder.CreateTable(
                name: "AnimeEpisode",
                columns: table => new
                {
                    AnimeEpisodeID = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AnimeSeriesID = table.Column<int>(nullable: false),
                    AniDB_EpisodeID = table.Column<int>(nullable: false),
                    DateTimeUpdated = table.Column<DateTime>(nullable: false),
                    DateTimeCreated = table.Column<DateTime>(nullable: false),
                    PlexContractVersion = table.Column<int>(nullable: false, defaultValue: 0),
                    PlexContractBlob = table.Column<byte[]>("mediumblob", nullable: true),
                    PlexContractSize = table.Column<int>(nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnimeEpisode", x => x.AnimeEpisodeID);
                });

            migrationBuilder.CreateTable(
                name: "AnimeEpisode_User",
                columns: table => new
                {
                    AnimeEpisode_UserID = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    JMMUserID = table.Column<int>(nullable: false),
                    AnimeEpisodeID = table.Column<int>(nullable: false),
                    AnimeSeriesID = table.Column<int>(nullable: false),
                    WatchedDate = table.Column<DateTime>(nullable: true),
                    PlayedCount = table.Column<int>(nullable: false),
                    WatchedCount = table.Column<int>(nullable: false),
                    StoppedCount = table.Column<int>(nullable: false),
                    ContractVersion = table.Column<int>(nullable: false, defaultValue: 0)
                        .Annotation("Sqlite:Autoincrement", true),
                    ContractBlob = table.Column<byte[]>(nullable: true),
                    ContractSize = table.Column<int>(nullable: false, defaultValue: 0)
                        .Annotation("Sqlite:Autoincrement", true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnimeEpisode_User", x => x.AnimeEpisode_UserID);
                });

            migrationBuilder.CreateTable(
                name: "AnimeGroup",
                columns: table => new
                {
                    AnimeGroupID = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AnimeGroupParentID = table.Column<int>(nullable: true),
                    GroupName = table.Column<string>(nullable: false),
                    Description = table.Column<string>(nullable: true),
                    IsManuallyNamed = table.Column<int>(nullable: false),
                    DateTimeUpdated = table.Column<DateTime>(nullable: false),
                    DateTimeCreated = table.Column<DateTime>(nullable: false),
                    SortName = table.Column<string>(nullable: false),
                    EpisodeAddedDate = table.Column<DateTime>(nullable: true),
                    LatestEpisodeAirDate = table.Column<DateTime>(nullable: true),
                    MissingEpisodeCount = table.Column<int>(nullable: false),
                    MissingEpisodeCountGroups = table.Column<int>(nullable: false),
                    OverrideDescription = table.Column<int>(nullable: false),
                    DefaultAnimeSeriesID = table.Column<int>(nullable: true),
                    ContractVersion = table.Column<int>(nullable: false, defaultValue: 0)
                        .Annotation("Sqlite:Autoincrement", true),
                    ContractBlob = table.Column<byte[]>(nullable: true),
                    ContractSize = table.Column<int>(nullable: false, defaultValue: 0)
                        .Annotation("Sqlite:Autoincrement", true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnimeGroup", x => x.AnimeGroupID);
                });

            migrationBuilder.CreateTable(
                name: "AnimeGroup_User",
                columns: table => new
                {
                    AnimeGroup_UserID = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    JMMUserID = table.Column<int>(nullable: false),
                    AnimeGroupID = table.Column<int>(nullable: false),
                    IsFave = table.Column<int>(nullable: false),
                    UnwatchedEpisodeCount = table.Column<int>(nullable: false),
                    WatchedEpisodeCount = table.Column<int>(nullable: false),
                    WatchedDate = table.Column<DateTime>(nullable: true),
                    PlayedCount = table.Column<int>(nullable: false),
                    WatchedCount = table.Column<int>(nullable: false),
                    StoppedCount = table.Column<int>(nullable: false),
                    PlexContractVersion = table.Column<int>(nullable: false, defaultValue: 0),
                    PlexContractBlob = table.Column<byte[]>("mediumblob", nullable: true),
                    PlexContractSize = table.Column<int>(nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnimeGroup_User", x => x.AnimeGroup_UserID);
                });

            migrationBuilder.CreateTable(
                name: "AnimeSeries",
                columns: table => new
                {
                    AnimeSeriesID = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AnimeGroupID = table.Column<int>(nullable: false),
                    AniDB_ID = table.Column<int>(nullable: false),
                    DateTimeUpdated = table.Column<DateTime>(nullable: false),
                    DateTimeCreated = table.Column<DateTime>(nullable: false),
                    DefaultAudioLanguage = table.Column<string>(nullable: true),
                    DefaultSubtitleLanguage = table.Column<string>(nullable: true),
                    EpisodeAddedDate = table.Column<DateTime>(nullable: true),
                    LatestEpisodeAirDate = table.Column<DateTime>(nullable: true),
                    AirsOn = table.Column<int>(nullable: true),
                    MissingEpisodeCount = table.Column<int>(nullable: false),
                    MissingEpisodeCountGroups = table.Column<int>(nullable: false),
                    LatestLocalEpisodeNumber = table.Column<int>(nullable: false),
                    SeriesNameOverride = table.Column<string>(maxLength: 500, nullable: true),
                    DefaultFolder = table.Column<string>(nullable: true),
                    ContractVersion = table.Column<int>(nullable: false, defaultValue: 0)
                        .Annotation("Sqlite:Autoincrement", true),
                    ContractBlob = table.Column<byte[]>(nullable: true),
                    ContractSize = table.Column<int>(nullable: false, defaultValue: 0)
                        .Annotation("Sqlite:Autoincrement", true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnimeSeries", x => x.AnimeSeriesID);
                });

            migrationBuilder.CreateTable(
                name: "AnimeSeries_User",
                columns: table => new
                {
                    AnimeSeries_UserID = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    JMMUserID = table.Column<int>(nullable: false),
                    AnimeSeriesID = table.Column<int>(nullable: false),
                    UnwatchedEpisodeCount = table.Column<int>(nullable: false),
                    WatchedEpisodeCount = table.Column<int>(nullable: false),
                    WatchedDate = table.Column<DateTime>(nullable: true),
                    PlayedCount = table.Column<int>(nullable: false),
                    WatchedCount = table.Column<int>(nullable: false),
                    StoppedCount = table.Column<int>(nullable: false),
                    PlexContractVersion = table.Column<int>(nullable: false, defaultValue: 0),
                    PlexContractBlob = table.Column<byte[]>("mediumblob", nullable: true),
                    PlexContractSize = table.Column<int>(nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnimeSeries_User", x => x.AnimeSeries_UserID);
                });

            migrationBuilder.CreateTable(
                name: "AnimeStaff",
                columns: table => new
                {
                    StaffID = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AniDBID = table.Column<int>(nullable: false),
                    Name = table.Column<string>(nullable: false),
                    AlternateName = table.Column<string>(nullable: true),
                    Description = table.Column<string>(nullable: true),
                    ImagePath = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnimeStaff", x => x.StaffID);
                });

            migrationBuilder.CreateTable(
                name: "AuthTokens",
                columns: table => new
                {
                    AuthID = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserID = table.Column<int>(nullable: false),
                    DeviceName = table.Column<string>(nullable: false),
                    Token = table.Column<string>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuthTokens", x => x.AuthID);
                });

            migrationBuilder.CreateTable(
                name: "BookmarkedAnime",
                columns: table => new
                {
                    BookmarkedAnimeID = table.Column<int>(nullable: false),
                    AnimeID = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Priority = table.Column<int>(nullable: false),
                    Notes = table.Column<string>(nullable: true),
                    Downloading = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookmarkedAnime", x => x.AnimeID);
                });

            migrationBuilder.CreateTable(
                name: "CloudAccount",
                columns: table => new
                {
                    CloudID = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ConnectionString = table.Column<string>(nullable: false),
                    Name = table.Column<string>(nullable: false),
                    Provider = table.Column<string>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CloudAccount", x => x.CloudID);
                });

            migrationBuilder.CreateTable(
                name: "CommandRequest",
                columns: table => new
                {
                    CommandRequestID = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Priority = table.Column<int>(nullable: false),
                    CommandType = table.Column<int>(nullable: false),
                    CommandID = table.Column<string>(nullable: false),
                    CommandDetails = table.Column<string>(nullable: false),
                    DateTimeUpdated = table.Column<DateTime>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommandRequest", x => x.CommandRequestID);
                });

            migrationBuilder.CreateTable(
                name: "CrossRef_AniDB_MAL",
                columns: table => new
                {
                    CrossRef_AniDB_MALID = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AnimeID = table.Column<int>(nullable: false),
                    MALID = table.Column<int>(nullable: false),
                    MALTitle = table.Column<string>(maxLength: 500, nullable: true),
                    StartEpisodeType = table.Column<int>(nullable: false),
                    StartEpisodeNumber = table.Column<int>(nullable: false),
                    CrossRefSource = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrossRef_AniDB_MAL", x => x.CrossRef_AniDB_MALID);
                });

            migrationBuilder.CreateTable(
                name: "CrossRef_AniDB_Other",
                columns: table => new
                {
                    CrossRef_AniDB_OtherID = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AnimeID = table.Column<int>(nullable: false),
                    CrossRefID = table.Column<string>(maxLength: 500, nullable: false),
                    CrossRefSource = table.Column<int>(nullable: false),
                    CrossRefType = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrossRef_AniDB_Other", x => x.CrossRef_AniDB_OtherID);
                });

            migrationBuilder.CreateTable(
                name: "CrossRef_AniDB_Trakt_Episode",
                columns: table => new
                {
                    CrossRef_AniDB_Trakt_EpisodeID = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AnimeID = table.Column<int>(nullable: false),
                    AniDBEpisodeID = table.Column<int>(nullable: false),
                    TraktID = table.Column<string>(maxLength: 500, nullable: true),
                    Season = table.Column<int>(nullable: false),
                    EpisodeNumber = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrossRef_AniDB_Trakt_Episode", x => x.CrossRef_AniDB_Trakt_EpisodeID);
                });

            migrationBuilder.CreateTable(
                name: "CrossRef_AniDB_TraktV2",
                columns: table => new
                {
                    CrossRef_AniDB_TraktV2ID = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AnimeID = table.Column<int>(nullable: false),
                    AniDBStartEpisodeType = table.Column<int>(nullable: false),
                    AniDBStartEpisodeNumber = table.Column<int>(nullable: false),
                    TraktID = table.Column<string>(maxLength: 500, nullable: true),
                    TraktSeasonNumber = table.Column<int>(nullable: false),
                    TraktStartEpisodeNumber = table.Column<int>(nullable: false),
                    TraktTitle = table.Column<string>(nullable: true),
                    CrossRefSource = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrossRef_AniDB_TraktV2", x => x.CrossRef_AniDB_TraktV2ID);
                });

            migrationBuilder.CreateTable(
                name: "CrossRef_AniDB_TvDB",
                columns: table => new
                {
                    CrossRef_AniDB_TvDBID = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AniDBID = table.Column<int>(nullable: false),
                    TvDBID = table.Column<int>(nullable: false),
                    CrossRefSource = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrossRef_AniDB_TvDB", x => x.CrossRef_AniDB_TvDBID);
                });

            migrationBuilder.CreateTable(
                name: "CrossRef_AniDB_TvDB_Episode",
                columns: table => new
                {
                    CrossRef_AniDB_TvDB_EpisodeID = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AniDBEpisodeID = table.Column<int>(nullable: false),
                    TvDBEpisodeID = table.Column<int>(nullable: false),
                    MatchRating = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrossRef_AniDB_TvDB_Episode", x => x.CrossRef_AniDB_TvDB_EpisodeID);
                });

            migrationBuilder.CreateTable(
                name: "CrossRef_AniDB_TvDB_Episode_Override",
                columns: table => new
                {
                    CrossRef_AniDB_TvDB_Episode_OverrideID = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AniDBEpisodeID = table.Column<int>(nullable: false),
                    TvDBEpisodeID = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrossRef_AniDB_TvDB_Episode_Override", x => x.CrossRef_AniDB_TvDB_Episode_OverrideID);
                });

            migrationBuilder.CreateTable(
                name: "CrossRef_AniDB_TvDBV2",
                columns: table => new
                {
                    CrossRef_AniDB_TvDBV2ID = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AnimeID = table.Column<int>(nullable: false),
                    AniDBStartEpisodeType = table.Column<int>(nullable: false),
                    AniDBStartEpisodeNumber = table.Column<int>(nullable: false),
                    TvDBID = table.Column<int>(nullable: false),
                    TvDBSeasonNumber = table.Column<int>(nullable: false),
                    TvDBStartEpisodeNumber = table.Column<int>(nullable: false),
                    TvDBTitle = table.Column<string>(nullable: true),
                    CrossRefSource = table.Column<int>(nullable: false),
                    IsAdditive = table.Column<bool>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrossRef_AniDB_TvDBV2", x => x.CrossRef_AniDB_TvDBV2ID);
                });

            migrationBuilder.CreateTable(
                name: "CrossRef_Anime_Staff",
                columns: table => new
                {
                    CrossRef_Anime_StaffID = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AniDB_AnimeID = table.Column<int>(nullable: false),
                    StaffID = table.Column<int>(nullable: false),
                    Role = table.Column<string>(nullable: false),
                    RoleID = table.Column<int>(nullable: false),
                    RoleType = table.Column<int>(nullable: false),
                    Language = table.Column<string>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrossRef_Anime_Staff", x => x.CrossRef_Anime_StaffID);
                });

            migrationBuilder.CreateTable(
                name: "CrossRef_CustomTag",
                columns: table => new
                {
                    CrossRef_CustomTagID = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CustomTagID = table.Column<int>(nullable: false),
                    CrossRefID = table.Column<int>(nullable: false),
                    CrossRefType = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrossRef_CustomTag", x => x.CrossRef_CustomTagID);
                });

            migrationBuilder.CreateTable(
                name: "CrossRef_File_Episode",
                columns: table => new
                {
                    CrossRef_File_EpisodeID = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Hash = table.Column<string>(maxLength: 50, nullable: true),
                    FileName = table.Column<string>(maxLength: 500, nullable: false),
                    FileSize = table.Column<long>(nullable: false),
                    CrossRefSource = table.Column<int>(nullable: false),
                    AnimeID = table.Column<int>(nullable: false),
                    EpisodeID = table.Column<int>(nullable: false),
                    Percentage = table.Column<int>(nullable: false),
                    EpisodeOrder = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrossRef_File_Episode", x => x.CrossRef_File_EpisodeID);
                });

            migrationBuilder.CreateTable(
                name: "CrossRef_Languages_AniDB_File",
                columns: table => new
                {
                    CrossRef_Languages_AniDB_FileID = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FileID = table.Column<int>(nullable: false),
                    LanguageID = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrossRef_Languages_AniDB_File", x => x.CrossRef_Languages_AniDB_FileID);
                });

            migrationBuilder.CreateTable(
                name: "CrossRef_Subtitles_AniDB_File",
                columns: table => new
                {
                    CrossRef_Subtitles_AniDB_FileID = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FileID = table.Column<int>(nullable: false),
                    LanguageID = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrossRef_Subtitles_AniDB_File", x => x.CrossRef_Subtitles_AniDB_FileID);
                });

            migrationBuilder.CreateTable(
                name: "CustomTag",
                columns: table => new
                {
                    CustomTagID = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TagName = table.Column<string>(maxLength: 500, nullable: true),
                    TagDescription = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomTag", x => x.CustomTagID);
                });

            migrationBuilder.CreateTable(
                name: "DuplicateFile",
                columns: table => new
                {
                    DuplicateFileID = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FilePathFile1 = table.Column<string>(nullable: false),
                    FilePathFile2 = table.Column<string>(nullable: false),
                    Hash = table.Column<string>(maxLength: 50, nullable: false),
                    ImportFolderIDFile1 = table.Column<int>(nullable: false),
                    ImportFolderIDFile2 = table.Column<int>(nullable: false),
                    DateTimeUpdated = table.Column<DateTime>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DuplicateFile", x => x.DuplicateFileID);
                });

            migrationBuilder.CreateTable(
                name: "FileFfdshowPreset",
                columns: table => new
                {
                    FileFfdshowPresetID = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Hash = table.Column<string>(maxLength: 50, nullable: false),
                    FileSize = table.Column<long>(nullable: false),
                    Preset = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FileFfdshowPreset", x => x.FileFfdshowPresetID);
                });

            migrationBuilder.CreateTable(
                name: "FileNameHash",
                columns: table => new
                {
                    FileNameHashID = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FileName = table.Column<string>(maxLength: 500, nullable: false),
                    FileSize = table.Column<long>(nullable: false),
                    Hash = table.Column<string>(maxLength: 50, nullable: false),
                    DateTimeUpdated = table.Column<DateTime>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FileNameHash", x => x.FileNameHashID);
                });

            migrationBuilder.CreateTable(
                name: "GroupFilter",
                columns: table => new
                {
                    GroupFilterID = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GroupFilterName = table.Column<string>(nullable: false),
                    ApplyToSeries = table.Column<int>(nullable: false),
                    BaseCondition = table.Column<int>(nullable: false),
                    SortingCriteria = table.Column<string>(nullable: true),
                    Locked = table.Column<int>(nullable: true),
                    FilterType = table.Column<int>(nullable: false),
                    ParentGroupFilterID = table.Column<int>(nullable: true),
                    InvisibleInClients = table.Column<int>(nullable: false, defaultValue: 0)
                        .Annotation("Sqlite:Autoincrement", true),
                    GroupsIdsVersion = table.Column<int>(nullable: false, defaultValue: 0)
                        .Annotation("Sqlite:Autoincrement", true),
                    GroupsIdsString = table.Column<string>(nullable: true),
                    GroupConditionsVersion = table.Column<int>(nullable: false, defaultValue: 0)
                        .Annotation("Sqlite:Autoincrement", true),
                    GroupConditions = table.Column<string>(nullable: true),
                    SeriesIdsVersion = table.Column<int>(nullable: false, defaultValue: 0)
                        .Annotation("Sqlite:Autoincrement", true),
                    SeriesIdsString = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupFilter", x => x.GroupFilterID);
                });

            migrationBuilder.CreateTable(
                name: "GroupFilterCondition",
                columns: table => new
                {
                    GroupFilterConditionID = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GroupFilterID = table.Column<int>(nullable: false),
                    ConditionType = table.Column<int>(nullable: false),
                    ConditionOperator = table.Column<int>(nullable: false),
                    ConditionParameter = table.Column<string>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupFilterCondition", x => x.GroupFilterConditionID);
                });

            migrationBuilder.CreateTable(
                name: "IgnoreAnime",
                columns: table => new
                {
                    IgnoreAnimeID = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    JMMUserID = table.Column<int>(nullable: false),
                    AnimeID = table.Column<int>(nullable: false),
                    IgnoreType = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IgnoreAnime", x => x.IgnoreAnimeID);
                });

            migrationBuilder.CreateTable(
                name: "ImportFolder",
                columns: table => new
                {
                    ImportFolderID = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ImportFolderType = table.Column<int>(nullable: false),
                    ImportFolderName = table.Column<string>(nullable: false),
                    CloudID = table.Column<int>(nullable: true),
                    IsWatched = table.Column<int>(nullable: false),
                    IsDropSource = table.Column<int>(nullable: false),
                    IsDropDestination = table.Column<int>(nullable: false),
                    ImportFolderLocation = table.Column<string>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportFolder", x => x.ImportFolderID);
                });

            migrationBuilder.CreateTable(
                name: "JMMUser",
                columns: table => new
                {
                    JMMUserID = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Username = table.Column<string>(maxLength: 100, nullable: true),
                    Password = table.Column<string>(maxLength: 150, nullable: true),
                    IsAdmin = table.Column<int>(nullable: false),
                    IsAniDBUser = table.Column<int>(nullable: false),
                    IsTraktUser = table.Column<int>(nullable: false),
                    HideCategories = table.Column<string>(nullable: true),
                    CanEditServerSettings = table.Column<int>(nullable: true),
                    PlexUsers = table.Column<string>(nullable: true),
                    PlexToken = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JMMUser", x => x.JMMUserID);
                });

            migrationBuilder.CreateTable(
                name: "Language",
                columns: table => new
                {
                    LanguageID = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    LanguageName = table.Column<string>(maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Language", x => x.LanguageID);
                });

            migrationBuilder.CreateTable(
                name: "MovieDB_Fanart",
                columns: table => new
                {
                    MovieDB_FanartID = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ImageID = table.Column<string>(nullable: true),
                    MovieId = table.Column<int>(nullable: false),
                    ImageType = table.Column<string>(maxLength: 100, nullable: true),
                    ImageSize = table.Column<string>(maxLength: 100, nullable: true),
                    URL = table.Column<string>(nullable: true),
                    ImageWidth = table.Column<int>(nullable: false),
                    ImageHeight = table.Column<int>(nullable: false),
                    Enabled = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovieDB_Fanart", x => x.MovieDB_FanartID);
                });

            migrationBuilder.CreateTable(
                name: "MovieDB_Movie",
                columns: table => new
                {
                    MovieDB_MovieID = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MovieId = table.Column<int>(nullable: false),
                    MovieName = table.Column<string>(nullable: true),
                    OriginalName = table.Column<string>(nullable: true),
                    Overview = table.Column<string>(nullable: true),
                    Rating = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovieDB_Movie", x => x.MovieDB_MovieID);
                });

            migrationBuilder.CreateTable(
                name: "MovieDB_Poster",
                columns: table => new
                {
                    MovieDB_PosterID = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ImageID = table.Column<string>(maxLength: 100, nullable: true),
                    MovieId = table.Column<int>(nullable: false),
                    ImageType = table.Column<string>(maxLength: 100, nullable: true),
                    ImageSize = table.Column<string>(maxLength: 100, nullable: true),
                    URL = table.Column<string>(nullable: true),
                    ImageWidth = table.Column<int>(nullable: false),
                    ImageHeight = table.Column<int>(nullable: false),
                    Enabled = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovieDB_Poster", x => x.MovieDB_PosterID);
                });

            migrationBuilder.CreateTable(
                name: "Playlist",
                columns: table => new
                {
                    PlaylistID = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PlaylistName = table.Column<string>(nullable: true),
                    PlaylistItems = table.Column<string>(nullable: true),
                    DefaultPlayOrder = table.Column<int>(nullable: false),
                    PlayWatched = table.Column<int>(nullable: false),
                    PlayUnwatched = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Playlist", x => x.PlaylistID);
                });

            migrationBuilder.CreateTable(
                name: "RenameScript",
                columns: table => new
                {
                    RenameScriptID = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ScriptName = table.Column<string>(nullable: true),
                    Script = table.Column<string>(nullable: true),
                    IsEnabledOnImport = table.Column<int>(nullable: false),
                    RenamerType = table.Column<string>(nullable: false, defaultValue: "Legacy"),
                    ExtraData = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RenameScript", x => x.RenameScriptID);
                });

            migrationBuilder.CreateTable(
                name: "Scan",
                columns: table => new
                {
                    ScanID = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CreationTime = table.Column<DateTime>(nullable: false),
                    ImportFolders = table.Column<string>(nullable: false),
                    Status = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Scan", x => x.ScanID);
                });

            migrationBuilder.CreateTable(
                name: "ScanFile",
                columns: table => new
                {
                    ScanFileID = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ScanID = table.Column<int>(nullable: false),
                    ImportFolderID = table.Column<int>(nullable: false),
                    VideoLocal_Place_ID = table.Column<int>(nullable: false),
                    FullName = table.Column<string>(nullable: false),
                    FileSize = table.Column<long>(nullable: false),
                    Status = table.Column<int>(nullable: false),
                    CheckDate = table.Column<DateTime>(nullable: true),
                    Hash = table.Column<string>(maxLength: 100, nullable: false),
                    HashResult = table.Column<string>(maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScanFile", x => x.ScanFileID);
                });

            migrationBuilder.CreateTable(
                name: "ScheduledUpdate",
                columns: table => new
                {
                    ScheduledUpdateID = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UpdateType = table.Column<int>(nullable: false),
                    LastUpdate = table.Column<DateTime>(nullable: false),
                    UpdateDetails = table.Column<string>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduledUpdate", x => x.ScheduledUpdateID);
                });

            migrationBuilder.CreateTable(
                name: "Trakt_Episode",
                columns: table => new
                {
                    Trakt_EpisodeID = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Trakt_ShowID = table.Column<int>(nullable: false),
                    Season = table.Column<int>(nullable: false),
                    EpisodeNumber = table.Column<int>(nullable: false),
                    Title = table.Column<string>(nullable: true),
                    URL = table.Column<string>(maxLength: 500, nullable: true),
                    Overview = table.Column<string>(nullable: true),
                    TraktID = table.Column<int>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Trakt_Episode", x => x.Trakt_EpisodeID);
                });

            migrationBuilder.CreateTable(
                name: "Trakt_Friend",
                columns: table => new
                {
                    Trakt_FriendID = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Username = table.Column<string>(maxLength: 100, nullable: false),
                    FullName = table.Column<string>(maxLength: 100, nullable: true),
                    Gender = table.Column<string>(maxLength: 100, nullable: true),
                    Age = table.Column<string>(maxLength: 100, nullable: true),
                    Location = table.Column<string>(maxLength: 100, nullable: true),
                    About = table.Column<string>(nullable: true),
                    Joined = table.Column<int>(nullable: false),
                    Avatar = table.Column<string>(nullable: true),
                    Url = table.Column<string>(nullable: true),
                    LastAvatarUpdate = table.Column<DateTime>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Trakt_Friend", x => x.Trakt_FriendID);
                });

            migrationBuilder.CreateTable(
                name: "Trakt_Season",
                columns: table => new
                {
                    Trakt_SeasonID = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Trakt_ShowID = table.Column<int>(nullable: false),
                    Season = table.Column<int>(nullable: false),
                    URL = table.Column<string>(maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Trakt_Season", x => x.Trakt_SeasonID);
                });

            migrationBuilder.CreateTable(
                name: "Trakt_Show",
                columns: table => new
                {
                    Trakt_ShowID = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TraktID = table.Column<string>(maxLength: 500, nullable: true),
                    Title = table.Column<string>(nullable: true),
                    Year = table.Column<string>(maxLength: 500, nullable: true),
                    URL = table.Column<string>(maxLength: 500, nullable: true),
                    Overview = table.Column<string>(nullable: true),
                    TvDB_ID = table.Column<int>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Trakt_Show", x => x.Trakt_ShowID);
                });

            migrationBuilder.CreateTable(
                name: "TvDB_Episode",
                columns: table => new
                {
                    TvDB_EpisodeID = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Id = table.Column<int>(nullable: false),
                    SeriesID = table.Column<int>(nullable: false),
                    SeasonID = table.Column<int>(nullable: false),
                    SeasonNumber = table.Column<int>(nullable: false),
                    EpisodeNumber = table.Column<int>(nullable: false),
                    EpisodeName = table.Column<string>(nullable: true),
                    Overview = table.Column<string>(nullable: true),
                    Filename = table.Column<string>(nullable: true),
                    EpImgFlag = table.Column<int>(nullable: false),
                    AbsoluteNumber = table.Column<int>(nullable: true),
                    AirsAfterSeason = table.Column<int>(nullable: true),
                    AirsBeforeEpisode = table.Column<int>(nullable: true),
                    AirsBeforeSeason = table.Column<int>(nullable: true),
                    Rating = table.Column<int>(nullable: true),
                    AirDate = table.Column<DateTime>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TvDB_Episode", x => x.TvDB_EpisodeID);
                });

            migrationBuilder.CreateTable(
                name: "TvDB_ImageFanart",
                columns: table => new
                {
                    TvDB_ImageFanartID = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Id = table.Column<int>(nullable: false),
                    SeriesID = table.Column<int>(nullable: false),
                    BannerPath = table.Column<string>(nullable: true),
                    BannerType = table.Column<string>(nullable: true),
                    BannerType2 = table.Column<string>(nullable: true),
                    Colors = table.Column<string>(nullable: true),
                    Language = table.Column<string>(nullable: true),
                    ThumbnailPath = table.Column<string>(nullable: true),
                    VignettePath = table.Column<string>(nullable: true),
                    Enabled = table.Column<int>(nullable: false),
                    Chosen = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TvDB_ImageFanart", x => x.TvDB_ImageFanartID);
                });

            migrationBuilder.CreateTable(
                name: "TvDB_ImagePoster",
                columns: table => new
                {
                    TvDB_ImagePosterID = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Id = table.Column<int>(nullable: false),
                    SeriesID = table.Column<int>(nullable: false),
                    BannerPath = table.Column<string>(nullable: true),
                    BannerType = table.Column<string>(nullable: true),
                    BannerType2 = table.Column<string>(nullable: true),
                    Language = table.Column<string>(nullable: true),
                    Enabled = table.Column<int>(nullable: false),
                    SeasonNumber = table.Column<int>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TvDB_ImagePoster", x => x.TvDB_ImagePosterID);
                });

            migrationBuilder.CreateTable(
                name: "TvDB_ImageWideBanner",
                columns: table => new
                {
                    TvDB_ImageWideBannerID = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Id = table.Column<int>(nullable: false),
                    SeriesID = table.Column<int>(nullable: false),
                    BannerPath = table.Column<string>(nullable: true),
                    BannerType = table.Column<string>(nullable: true),
                    BannerType2 = table.Column<string>(nullable: true),
                    Language = table.Column<string>(nullable: true),
                    Enabled = table.Column<int>(nullable: false),
                    SeasonNumber = table.Column<int>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TvDB_ImageWideBanner", x => x.TvDB_ImageWideBannerID);
                });

            migrationBuilder.CreateTable(
                name: "TvDB_Series",
                columns: table => new
                {
                    TvDB_SeriesID = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SeriesID = table.Column<int>(nullable: false),
                    Overview = table.Column<string>(nullable: true),
                    SeriesName = table.Column<string>(nullable: true),
                    Status = table.Column<string>(maxLength: 100, nullable: true),
                    Banner = table.Column<string>(maxLength: 100, nullable: true),
                    Fanart = table.Column<string>(maxLength: 100, nullable: true),
                    Lastupdated = table.Column<string>(maxLength: 100, nullable: true),
                    Poster = table.Column<string>(maxLength: 100, nullable: true),
                    Rating = table.Column<int>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TvDB_Series", x => x.TvDB_SeriesID);
                });

            migrationBuilder.CreateTable(
                name: "VideoLocal",
                columns: table => new
                {
                    VideoLocalID = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Hash = table.Column<string>(maxLength: 50, nullable: false),
                    CRC32 = table.Column<string>(maxLength: 50, nullable: true),
                    MD5 = table.Column<string>(maxLength: 50, nullable: true),
                    SHA1 = table.Column<string>(maxLength: 50, nullable: true),
                    HashSource = table.Column<int>(nullable: false),
                    FileSize = table.Column<long>(nullable: false),
                    IsIgnored = table.Column<int>(nullable: false),
                    DateTimeUpdated = table.Column<DateTime>(nullable: false),
                    DateTimeCreated = table.Column<DateTime>(nullable: false),
                    IsVariation = table.Column<int>(nullable: false),
                    VideoCodec = table.Column<string>(nullable: false, defaultValue: ""),
                    VideoBitrate = table.Column<string>(nullable: false, defaultValue: ""),
                    VideoBitDepth = table.Column<string>(nullable: false, defaultValue: ""),
                    VideoFrameRate = table.Column<string>(nullable: false, defaultValue: ""),
                    VideoResolution = table.Column<string>(nullable: false, defaultValue: ""),
                    AudioCodec = table.Column<string>(nullable: false, defaultValue: ""),
                    AudioBitrate = table.Column<string>(nullable: false, defaultValue: ""),
                    Duration = table.Column<long>(nullable: false, defaultValue: 0L)
                        .Annotation("Sqlite:Autoincrement", true),
                    FileName = table.Column<string>(nullable: true),
                    MediaVersion = table.Column<int>(nullable: false, defaultValue: 0)
                        .Annotation("Sqlite:Autoincrement", true),
                    MediaBlob = table.Column<byte[]>(nullable: true),
                    MediaSize = table.Column<int>(nullable: false, defaultValue: 0)
                        .Annotation("Sqlite:Autoincrement", true),
                    MyListID = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VideoLocal", x => x.VideoLocalID);
                });

            migrationBuilder.CreateTable(
                name: "VideoLocal_Place",
                columns: table => new
                {
                    VideoLocal_Place_ID = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    VideoLocalID = table.Column<int>(nullable: false),
                    FilePath = table.Column<string>(nullable: false),
                    ImportFolderID = table.Column<int>(nullable: false),
                    ImportFolderType = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VideoLocal_Place", x => x.VideoLocal_Place_ID);
                });

            migrationBuilder.CreateTable(
                name: "VideoLocal_User",
                columns: table => new
                {
                    VideoLocal_UserID = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    JMMUserID = table.Column<int>(nullable: false),
                    VideoLocalID = table.Column<int>(nullable: false),
                    WatchedDate = table.Column<DateTime>(nullable: true),
                    ResumePosition = table.Column<long>(nullable: false, defaultValue: 0L)
                        .Annotation("Sqlite:Autoincrement", true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VideoLocal_User", x => x.VideoLocal_UserID);
                });

            migrationBuilder.CreateTable(
                name: "AniDB_Anime_DefaultImage",
                columns: table => new
                {
                    AniDB_Anime_DefaultImageID = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AnimeID = table.Column<int>(nullable: false),
                    ImageParentID = table.Column<int>(nullable: false),
                    ImageParentType = table.Column<int>(nullable: false),
                    ImageType = table.Column<int>(nullable: false),
                    SVR_AniDB_AnimeAniDB_AnimeID = table.Column<int>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AniDB_Anime_DefaultImage", x => x.AniDB_Anime_DefaultImageID);
                    table.ForeignKey(
                        name: "FK_AniDB_Anime_DefaultImage_AniDB_Anime_SVR_AniDB_AnimeAniDB_AnimeID",
                        column: x => x.SVR_AniDB_AnimeAniDB_AnimeID,
                        principalTable: "AniDB_Anime",
                        principalColumn: "AniDB_AnimeID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AniDB_Anime_Review",
                columns: table => new
                {
                    AniDB_Anime_ReviewID = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AnimeID = table.Column<int>(nullable: false),
                    ReviewID = table.Column<int>(nullable: false),
                    SVR_AniDB_AnimeAniDB_AnimeID = table.Column<int>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AniDB_Anime_Review", x => x.AniDB_Anime_ReviewID);
                    table.ForeignKey(
                        name: "FK_AniDB_Anime_Review_AniDB_Anime_SVR_AniDB_AnimeAniDB_AnimeID",
                        column: x => x.SVR_AniDB_AnimeAniDB_AnimeID,
                        principalTable: "AniDB_Anime",
                        principalColumn: "AniDB_AnimeID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AniDB_Episode",
                columns: table => new
                {
                    AniDB_EpisodeID = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EpisodeID = table.Column<int>(nullable: false),
                    AnimeID = table.Column<int>(nullable: false),
                    LengthSeconds = table.Column<int>(nullable: false),
                    Rating = table.Column<string>(nullable: false),
                    Votes = table.Column<string>(nullable: false),
                    EpisodeNumber = table.Column<int>(nullable: false),
                    EpisodeType = table.Column<int>(nullable: false),
                    Description = table.Column<string>(nullable: true),
                    AirDate = table.Column<int>(nullable: false),
                    DateTimeUpdated = table.Column<DateTime>(nullable: false),
                    SVR_AniDB_AnimeAniDB_AnimeID = table.Column<int>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AniDB_Episode", x => x.AniDB_EpisodeID);
                    table.ForeignKey(
                        name: "FK_AniDB_Episode_AniDB_Anime_SVR_AniDB_AnimeAniDB_AnimeID",
                        column: x => x.SVR_AniDB_AnimeAniDB_AnimeID,
                        principalTable: "AniDB_Anime",
                        principalColumn: "AniDB_AnimeID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AniDB_Anime_Character_AnimeID",
                table: "AniDB_Anime_Character",
                column: "AnimeID");

            migrationBuilder.CreateIndex(
                name: "IX_AniDB_Anime_Character_CharID",
                table: "AniDB_Anime_Character",
                column: "CharID");

            migrationBuilder.CreateIndex(
                name: "UIX_AniDB_Anime_Character_AnimeID_CharID",
                table: "AniDB_Anime_Character",
                columns: new[] { "AnimeID", "CharID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AniDB_Anime_DefaultImage_SVR_AniDB_AnimeAniDB_AnimeID",
                table: "AniDB_Anime_DefaultImage",
                column: "SVR_AniDB_AnimeAniDB_AnimeID");

            migrationBuilder.CreateIndex(
                name: "UIX_AniDB_Anime_DefaultImage_ImageType",
                table: "AniDB_Anime_DefaultImage",
                columns: new[] { "AnimeID", "ImageType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AniDB_Anime_Relation_AnimeID",
                table: "AniDB_Anime_Relation",
                column: "AnimeID");

            migrationBuilder.CreateIndex(
                name: "UIX_AniDB_Anime_Relation_AnimeID_RelatedAnimeID",
                table: "AniDB_Anime_Relation",
                columns: new[] { "AnimeID", "RelatedAnimeID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AniDB_Anime_Review_AnimeID",
                table: "AniDB_Anime_Review",
                column: "AnimeID");

            migrationBuilder.CreateIndex(
                name: "IX_AniDB_Anime_Review_SVR_AniDB_AnimeAniDB_AnimeID",
                table: "AniDB_Anime_Review",
                column: "SVR_AniDB_AnimeAniDB_AnimeID");

            migrationBuilder.CreateIndex(
                name: "UIX_AniDB_Anime_Review_AnimeID_ReviewID",
                table: "AniDB_Anime_Review",
                columns: new[] { "AnimeID", "ReviewID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AniDB_Anime_Similar_AnimeID",
                table: "AniDB_Anime_Similar",
                column: "AnimeID");

            migrationBuilder.CreateIndex(
                name: "UIX_AniDB_Anime_Similar_AnimeID_SimilarAnimeID",
                table: "AniDB_Anime_Similar",
                columns: new[] { "AnimeID", "SimilarAnimeID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AniDB_Anime_Tag_AnimeID",
                table: "AniDB_Anime_Tag",
                column: "AnimeID");

            migrationBuilder.CreateIndex(
                name: "UIX_AniDB_Anime_Tag_AnimeID_TagID",
                table: "AniDB_Anime_Tag",
                columns: new[] { "AnimeID", "TagID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AniDB_Anime_Title_AnimeID",
                table: "AniDB_Anime_Title",
                column: "AnimeID");

            migrationBuilder.CreateIndex(
                name: "IX_AniDB_Character_Seiyuu_CharID",
                table: "AniDB_Character_Seiyuu",
                column: "CharID");

            migrationBuilder.CreateIndex(
                name: "UIX_AniDB_Character_Seiyuu_CharID_SeiyuuID",
                table: "AniDB_Character_Seiyuu",
                columns: new[] { "CharID", "SeiyuuID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AniDB_Episode_AnimeID",
                table: "AniDB_Episode",
                column: "AnimeID");

            migrationBuilder.CreateIndex(
                name: "UIX_AniDB_Episode_EpisodeID",
                table: "AniDB_Episode",
                column: "EpisodeID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AniDB_Episode_SVR_AniDB_AnimeAniDB_AnimeID",
                table: "AniDB_Episode",
                column: "SVR_AniDB_AnimeAniDB_AnimeID");

            migrationBuilder.CreateIndex(
                name: "UIX_AniDB_File_Hash",
                table: "AniDB_File",
                column: "Hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AniDB_GroupStatus_AnimeID",
                table: "AniDB_GroupStatus",
                column: "AnimeID");

            migrationBuilder.CreateIndex(
                name: "UIX_AniDB_GroupStatus_AnimeID_GroupID",
                table: "AniDB_GroupStatus",
                columns: new[] { "AnimeID", "GroupID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UIX_AniDB_Recommendation",
                table: "AniDB_Recommendation",
                columns: new[] { "AnimeID", "UserID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UIX_AnimeEpisode_AniDB_EpisodeID",
                table: "AnimeEpisode",
                column: "AniDB_EpisodeID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnimeEpisode_AnimeSeriesID",
                table: "AnimeEpisode",
                column: "AnimeSeriesID");

            migrationBuilder.CreateIndex(
                name: "UIX_AnimeEpisode_User_User_EpisodeID",
                table: "AnimeEpisode_User",
                columns: new[] { "JMMUserID", "AnimeEpisodeID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnimeEpisode_User_User_AnimeSeriesID",
                table: "AnimeEpisode_User",
                columns: new[] { "JMMUserID", "AnimeSeriesID" });

            migrationBuilder.CreateIndex(
                name: "IX_AnimeGroup_AnimeGroupParentID",
                table: "AnimeGroup",
                column: "AnimeGroupParentID");

            migrationBuilder.CreateIndex(
                name: "UIX_AnimeGroup_User_User_GroupID",
                table: "AnimeGroup_User",
                columns: new[] { "JMMUserID", "AnimeGroupID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UIX_AnimeSeries_AniDB_ID",
                table: "AnimeSeries",
                column: "AniDB_ID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnimeSeries_AnimeGroupID",
                table: "AnimeSeries",
                column: "AnimeGroupID");

            migrationBuilder.CreateIndex(
                name: "UIX_AnimeSeries_User_User_SeriesID",
                table: "AnimeSeries_User",
                columns: new[] { "JMMUserID", "AnimeSeriesID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UIX_AnimeStaff_AniDBID",
                table: "AnimeStaff",
                column: "AniDBID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuthTokens_Token",
                table: "AuthTokens",
                column: "Token");

            migrationBuilder.CreateIndex(
                name: "UIX_CrossRef_AniDB_MAL_MALID",
                table: "CrossRef_AniDB_MAL",
                column: "MALID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UIX_CrossRef_AniDB_MAL_Anime",
                table: "CrossRef_AniDB_MAL",
                columns: new[] { "AnimeID", "StartEpisodeType", "StartEpisodeNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UIX_CrossRef_AniDB_Other",
                table: "CrossRef_AniDB_Other",
                columns: new[] { "AnimeID", "CrossRefID", "CrossRefSource", "CrossRefType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UIX_CrossRef_AniDB_Trakt_Episode_AniDBEpisodeID",
                table: "CrossRef_AniDB_Trakt_Episode",
                column: "AniDBEpisodeID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UIX_CrossRef_AniDB_TraktV2",
                table: "CrossRef_AniDB_TraktV2",
                columns: new[] { "AnimeID", "TraktSeasonNumber", "TraktStartEpisodeNumber", "AniDBStartEpisodeType", "AniDBStartEpisodeNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UIX_CrossRef_AniDB_TvDB_Episode_AniDBEpisodeID",
                table: "CrossRef_AniDB_TvDB_Episode",
                column: "AniDBEpisodeID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UIX_CrossRef_AniDB_TvDBV2",
                table: "CrossRef_AniDB_TvDBV2",
                columns: new[] { "AnimeID", "TvDBID", "TvDBSeasonNumber", "TvDBStartEpisodeNumber", "AniDBStartEpisodeType", "AniDBStartEpisodeNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CrossRef_CustomTag",
                table: "CrossRef_CustomTag",
                column: "CustomTagID");

            migrationBuilder.CreateIndex(
                name: "UIX_CrossRef_File_Episode_Hash_EpisodeID",
                table: "CrossRef_File_Episode",
                columns: new[] { "Hash", "EpisodeID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CrossRef_Languages_AniDB_File_FileID",
                table: "CrossRef_Languages_AniDB_File",
                column: "FileID");

            migrationBuilder.CreateIndex(
                name: "IX_CrossRef_Languages_AniDB_File_LanguageID",
                table: "CrossRef_Languages_AniDB_File",
                column: "LanguageID");

            migrationBuilder.CreateIndex(
                name: "IX_CrossRef_Subtitles_AniDB_File_FileID",
                table: "CrossRef_Subtitles_AniDB_File",
                column: "FileID");

            migrationBuilder.CreateIndex(
                name: "IX_CrossRef_Subtitles_AniDB_File_LanguageID",
                table: "CrossRef_Subtitles_AniDB_File",
                column: "LanguageID");

            migrationBuilder.CreateIndex(
                name: "UIX_FileFfdshowPreset_Hash",
                table: "FileFfdshowPreset",
                columns: new[] { "Hash", "FileSize" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UIX_FileNameHash",
                table: "FileNameHash",
                columns: new[] { "FileName", "FileSize", "Hash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GroupFilter_ParentGroupFilterID",
                table: "GroupFilter",
                column: "ParentGroupFilterID");

            migrationBuilder.CreateIndex(
                name: "UIX_IgnoreAnime_User_AnimeID",
                table: "IgnoreAnime",
                columns: new[] { "JMMUserID", "AnimeID", "IgnoreType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UIX_Language_LanguageName",
                table: "Language",
                column: "LanguageName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UIX_MovieDB_Movie_Id",
                table: "MovieDB_Movie",
                column: "MovieId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScanFileStatus",
                table: "ScanFile",
                columns: new[] { "ScanID", "Status" });

            migrationBuilder.CreateIndex(
                name: "UIX_ScheduledUpdate_Type",
                table: "ScheduledUpdate",
                column: "UpdateType",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UIX_Trakt_Friend_Username",
                table: "Trakt_Friend",
                column: "Username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UIX_TvDB_Episode_Id",
                table: "TvDB_Episode",
                column: "Id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UIX_TvDB_ImageFanart_Id",
                table: "TvDB_ImageFanart",
                column: "Id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UIX_TvDB_ImagePoster_Id",
                table: "TvDB_ImagePoster",
                column: "Id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UIX_TvDB_ImageWideBanner_Id",
                table: "TvDB_ImageWideBanner",
                column: "Id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UIX_TvDB_Series_SeriesID",
                table: "TvDB_Series",
                column: "SeriesID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UIX_IX_VideoLocal_Hash",
                table: "VideoLocal",
                column: "Hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UIX_VideoLocal_User_User_VideoLocalID",
                table: "VideoLocal_User",
                columns: new[] { "JMMUserID", "VideoLocalID" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AniDB_Anime_Character");

            migrationBuilder.DropTable(
                name: "AniDB_Anime_DefaultImage");

            migrationBuilder.DropTable(
                name: "AniDB_Anime_Relation");

            migrationBuilder.DropTable(
                name: "AniDB_Anime_Review");

            migrationBuilder.DropTable(
                name: "AniDB_Anime_Similar");

            migrationBuilder.DropTable(
                name: "AniDB_Anime_Tag");

            migrationBuilder.DropTable(
                name: "AniDB_Anime_Title");

            migrationBuilder.DropTable(
                name: "AniDB_AnimeUpdate");

            migrationBuilder.DropTable(
                name: "AniDB_Character");

            migrationBuilder.DropTable(
                name: "AniDB_Character_Seiyuu");

            migrationBuilder.DropTable(
                name: "AniDB_Episode");

            migrationBuilder.DropTable(
                name: "AniDB_Episode_Title");

            migrationBuilder.DropTable(
                name: "AniDB_File");

            migrationBuilder.DropTable(
                name: "AniDB_GroupStatus");

            migrationBuilder.DropTable(
                name: "AniDB_MylistStats");

            migrationBuilder.DropTable(
                name: "AniDB_Recommendation");

            migrationBuilder.DropTable(
                name: "AniDB_ReleaseGroup");

            migrationBuilder.DropTable(
                name: "AniDB_Review");

            migrationBuilder.DropTable(
                name: "AniDB_Seiyuu");

            migrationBuilder.DropTable(
                name: "AniDB_Tag");

            migrationBuilder.DropTable(
                name: "AniDB_Vote");

            migrationBuilder.DropTable(
                name: "AnimeCharacter");

            migrationBuilder.DropTable(
                name: "AnimeEpisode");

            migrationBuilder.DropTable(
                name: "AnimeEpisode_User");

            migrationBuilder.DropTable(
                name: "AnimeGroup");

            migrationBuilder.DropTable(
                name: "AnimeGroup_User");

            migrationBuilder.DropTable(
                name: "AnimeSeries");

            migrationBuilder.DropTable(
                name: "AnimeSeries_User");

            migrationBuilder.DropTable(
                name: "AnimeStaff");

            migrationBuilder.DropTable(
                name: "AuthTokens");

            migrationBuilder.DropTable(
                name: "BookmarkedAnime");

            migrationBuilder.DropTable(
                name: "CloudAccount");

            migrationBuilder.DropTable(
                name: "CommandRequest");

            migrationBuilder.DropTable(
                name: "CrossRef_AniDB_MAL");

            migrationBuilder.DropTable(
                name: "CrossRef_AniDB_Other");

            migrationBuilder.DropTable(
                name: "CrossRef_AniDB_Trakt_Episode");

            migrationBuilder.DropTable(
                name: "CrossRef_AniDB_TraktV2");

            migrationBuilder.DropTable(
                name: "CrossRef_AniDB_TvDB");

            migrationBuilder.DropTable(
                name: "CrossRef_AniDB_TvDB_Episode");

            migrationBuilder.DropTable(
                name: "CrossRef_AniDB_TvDB_Episode_Override");

            migrationBuilder.DropTable(
                name: "CrossRef_AniDB_TvDBV2");

            migrationBuilder.DropTable(
                name: "CrossRef_Anime_Staff");

            migrationBuilder.DropTable(
                name: "CrossRef_CustomTag");

            migrationBuilder.DropTable(
                name: "CrossRef_File_Episode");

            migrationBuilder.DropTable(
                name: "CrossRef_Languages_AniDB_File");

            migrationBuilder.DropTable(
                name: "CrossRef_Subtitles_AniDB_File");

            migrationBuilder.DropTable(
                name: "CustomTag");

            migrationBuilder.DropTable(
                name: "DuplicateFile");

            migrationBuilder.DropTable(
                name: "FileFfdshowPreset");

            migrationBuilder.DropTable(
                name: "FileNameHash");

            migrationBuilder.DropTable(
                name: "GroupFilter");

            migrationBuilder.DropTable(
                name: "GroupFilterCondition");

            migrationBuilder.DropTable(
                name: "IgnoreAnime");

            migrationBuilder.DropTable(
                name: "ImportFolder");

            migrationBuilder.DropTable(
                name: "JMMUser");

            migrationBuilder.DropTable(
                name: "Language");

            migrationBuilder.DropTable(
                name: "MovieDB_Fanart");

            migrationBuilder.DropTable(
                name: "MovieDB_Movie");

            migrationBuilder.DropTable(
                name: "MovieDB_Poster");

            migrationBuilder.DropTable(
                name: "Playlist");

            migrationBuilder.DropTable(
                name: "RenameScript");

            migrationBuilder.DropTable(
                name: "Scan");

            migrationBuilder.DropTable(
                name: "ScanFile");

            migrationBuilder.DropTable(
                name: "ScheduledUpdate");

            migrationBuilder.DropTable(
                name: "Trakt_Episode");

            migrationBuilder.DropTable(
                name: "Trakt_Friend");

            migrationBuilder.DropTable(
                name: "Trakt_Season");

            migrationBuilder.DropTable(
                name: "Trakt_Show");

            migrationBuilder.DropTable(
                name: "TvDB_Episode");

            migrationBuilder.DropTable(
                name: "TvDB_ImageFanart");

            migrationBuilder.DropTable(
                name: "TvDB_ImagePoster");

            migrationBuilder.DropTable(
                name: "TvDB_ImageWideBanner");

            migrationBuilder.DropTable(
                name: "TvDB_Series");

            migrationBuilder.DropTable(
                name: "VideoLocal");

            migrationBuilder.DropTable(
                name: "VideoLocal_Place");

            migrationBuilder.DropTable(
                name: "VideoLocal_User");

            migrationBuilder.DropTable(
                name: "AniDB_Anime");
        }
    }
}
