using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentNHibernate.Cfg;
using FluentNHibernate.Cfg.Db;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using NHibernate;
using Shoko.Server.Databases.NHibernate;
using Shoko.Server.Databases.SqliteFixes;
using Shoko.Server.Repositories;
using Shoko.Server.Server;
using Shoko.Server.Utilities;

// ReSharper disable InconsistentNaming

namespace Shoko.Server.Databases;

public class SQLite : BaseDatabase<SqliteConnection>
{
    public override string Name => "SQLite";

    public override int RequiredVersion => 139;

    public override void BackupDatabase(string fullfilename)
    {
        fullfilename += ".db3";
        File.Copy(GetDatabaseFilePath(), fullfilename);
    }

    private static string _databasePath;
    private static string DatabasePath
    {
        get
        {
            if (_databasePath != null)
                return _databasePath;

            var dirPath = Utils.SettingsProvider.GetSettings().Database.MySqliteDirectory;
            if (string.IsNullOrWhiteSpace(dirPath))
                return _databasePath = Utils.ApplicationPath;

            return _databasePath = Path.Combine(Utils.ApplicationPath, dirPath);
        }
    }

    private static string GetDatabaseFilePath()
    {
        var dbName = Path.Combine(DatabasePath, Utils.SettingsProvider.GetSettings().Database.SQLite_DatabaseFile);
        return dbName;
    }

    public override string GetConnectionString()
    {
        var settings = Utils.SettingsProvider.GetSettings();
        // we are assuming that if you have overridden the connection string, you know what you're doing, and have set up the database and perms
        if (!string.IsNullOrWhiteSpace(settings.Database.OverrideConnectionString))
            return settings.Database.OverrideConnectionString;

        return $@"data source={GetDatabaseFilePath()};";
    }

    public override string GetTestConnectionString()
    {
        return GetConnectionString();
    }

    public override ISessionFactory CreateSessionFactory()
    {
        var settings = Utils.SettingsProvider.GetSettings();
        return Fluently.Configure()
            .Database(MsSqliteConfiguration.Standard.ConnectionString(c => c.Is(GetConnectionString()))
                .Dialect<SqliteDialectFix>().Driver<SqliteDriverFix>())
            .Mappings(m => m.FluentMappings.AddFromAssemblyOf<ShokoServer>())
            .ExposeConfiguration(c => c.DataBaseIntegration(prop =>
            {
                prop.LogSqlInConsole = settings.Database.LogSqlInConsole;
            }).SetInterceptor(new NHibernateDependencyInjector(Utils.ServiceContainer)))
            .BuildSessionFactory();
    }

    public override bool DatabaseAlreadyExists()
    {
        if (GetDatabaseFilePath().Length == 0)
        {
            return false;
        }

        if (File.Exists(GetDatabaseFilePath()))
        {
            return true;
        }

        return false;
    }

    public override bool HasVersionsTable()
    {
        using var myConn = new SqliteConnection(GetConnectionString());
        myConn.Open();
        const string sql = "SELECT COUNT(name) FROM sqlite_master WHERE type='table' AND name='Versions'";
        var cmd = new SqliteCommand(sql, myConn);
        var count = (long)(cmd.ExecuteScalar() ?? 0);
        myConn.Close();

        return count > 0;
    }

    public override void CreateDatabase()
    {
        if (DatabaseAlreadyExists())
        {
            return;
        }

        if (!Directory.Exists(DatabasePath))
        {
            Directory.CreateDirectory(DatabasePath);
        }

        // Microsoft.Data.Sqlite automatically creates the file if it doesn't exist during init
        // if (!File.Exists(GetDatabaseFilePath()))
        //     SqliteConnection.CreateFile(GetDatabaseFilePath());

        Utils.SettingsProvider.GetSettings().Database.SQLite_DatabaseFile = GetDatabaseFilePath();
    }


    private List<DatabaseCommand> createVersionTable = new()
    {
        new DatabaseCommand(0, 1,
            "CREATE TABLE Versions ( VersionsID INTEGER PRIMARY KEY AUTOINCREMENT, VersionType Text NOT NULL, VersionValue Text NOT NULL)")
    };

    private List<DatabaseCommand> createTables = new()
    {
        new DatabaseCommand(1, 1,
            "CREATE TABLE AniDB_Anime ( AniDB_AnimeID INTEGER PRIMARY KEY AUTOINCREMENT, AnimeID int NOT NULL, EpisodeCount int NOT NULL, AirDate timestamp NULL, EndDate timestamp NULL, URL text NULL, Picname text NULL, BeginYear int NOT NULL, EndYear int NOT NULL, AnimeType int NOT NULL, MainTitle text NOT NULL, AllTitles text NOT NULL, AllCategories text NOT NULL, AllTags text NOT NULL, Description text NOT NULL, EpisodeCountNormal int NOT NULL, EpisodeCountSpecial int NOT NULL, Rating int NOT NULL, VoteCount int NOT NULL, TempRating int NOT NULL, TempVoteCount int NOT NULL, AvgReviewRating int NOT NULL, ReviewCount int NOT NULL, DateTimeUpdated timestamp NOT NULL, DateTimeDescUpdated timestamp NOT NULL, ImageEnabled int NOT NULL, AwardList text NOT NULL, Restricted int NOT NULL, AnimePlanetID int NULL, ANNID int NULL, AllCinemaID int NULL, AnimeNfo int NULL, LatestEpisodeNumber int NULL );"),
        new DatabaseCommand(1, 2, "CREATE UNIQUE INDEX [UIX_AniDB_Anime_AnimeID] ON [AniDB_Anime] ([AnimeID]);"),
        new DatabaseCommand(1, 3,
            "CREATE TABLE AniDB_Anime_Category ( AniDB_Anime_CategoryID INTEGER PRIMARY KEY AUTOINCREMENT, AnimeID int NOT NULL, CategoryID int NOT NULL, Weighting int NOT NULL ); "),
        new DatabaseCommand(1, 4, "CREATE INDEX IX_AniDB_Anime_Category_AnimeID on AniDB_Anime_Category(AnimeID);"),
        new DatabaseCommand(1, 5,
            "CREATE UNIQUE INDEX UIX_AniDB_Anime_Category_AnimeID_CategoryID ON AniDB_Anime_Category (AnimeID, CategoryID);"),
        new DatabaseCommand(1, 6,
            "CREATE TABLE AniDB_Anime_Character ( AniDB_Anime_CharacterID INTEGER PRIMARY KEY AUTOINCREMENT, AnimeID int NOT NULL, CharID int NOT NULL, CharType text NOT NULL, EpisodeListRaw text NOT NULL ); "),
        new DatabaseCommand(1, 7,
            "CREATE INDEX IX_AniDB_Anime_Character_AnimeID on AniDB_Anime_Character(AnimeID);"),
        new DatabaseCommand(1, 8,
            "CREATE UNIQUE INDEX UIX_AniDB_Anime_Character_AnimeID_CharID ON AniDB_Anime_Character(AnimeID, CharID);"),
        new DatabaseCommand(1, 9,
            "CREATE TABLE AniDB_Anime_Relation ( AniDB_Anime_RelationID INTEGER PRIMARY KEY AUTOINCREMENT, AnimeID int NOT NULL, RelatedAnimeID int NOT NULL, RelationType text NOT NULL ); "),
        new DatabaseCommand(1, 10,
            "CREATE INDEX IX_AniDB_Anime_Relation_AnimeID on AniDB_Anime_Relation(AnimeID);"),
        new DatabaseCommand(1, 11,
            "CREATE UNIQUE INDEX UIX_AniDB_Anime_Relation_AnimeID_RelatedAnimeID ON AniDB_Anime_Relation(AnimeID, RelatedAnimeID);"),
        new DatabaseCommand(1, 12,
            "CREATE TABLE AniDB_Anime_Review ( AniDB_Anime_ReviewID INTEGER PRIMARY KEY AUTOINCREMENT, AnimeID int NOT NULL, ReviewID int NOT NULL ); "),
        new DatabaseCommand(1, 13, "CREATE INDEX IX_AniDB_Anime_Review_AnimeID on AniDB_Anime_Review(AnimeID);"),
        new DatabaseCommand(1, 14,
            "CREATE UNIQUE INDEX UIX_AniDB_Anime_Review_AnimeID_ReviewID ON AniDB_Anime_Review(AnimeID, ReviewID);"),
        new DatabaseCommand(1, 15,
            "CREATE TABLE AniDB_Anime_Similar ( AniDB_Anime_SimilarID INTEGER PRIMARY KEY AUTOINCREMENT, AnimeID int NOT NULL, SimilarAnimeID int NOT NULL, Approval int NOT NULL, Total int NOT NULL ); "),
        new DatabaseCommand(1, 16, "CREATE INDEX IX_AniDB_Anime_Similar_AnimeID on AniDB_Anime_Similar(AnimeID);"),
        new DatabaseCommand(1, 17,
            "CREATE UNIQUE INDEX UIX_AniDB_Anime_Similar_AnimeID_SimilarAnimeID ON AniDB_Anime_Similar(AnimeID, SimilarAnimeID);"),
        new DatabaseCommand(1, 18,
            "CREATE TABLE AniDB_Anime_Tag ( AniDB_Anime_TagID INTEGER PRIMARY KEY AUTOINCREMENT, AnimeID int NOT NULL, TagID int NOT NULL, Approval int NOT NULL ); "),
        new DatabaseCommand(1, 19, "CREATE INDEX IX_AniDB_Anime_Tag_AnimeID on AniDB_Anime_Tag(AnimeID);"),
        new DatabaseCommand(1, 20,
            "CREATE UNIQUE INDEX UIX_AniDB_Anime_Tag_AnimeID_TagID ON AniDB_Anime_Tag(AnimeID, TagID);"),
        new DatabaseCommand(1, 21,
            "CREATE TABLE AniDB_Anime_Title ( AniDB_Anime_TitleID INTEGER PRIMARY KEY AUTOINCREMENT, AnimeID int NOT NULL, TitleType text NOT NULL, Language text NOT NULL, Title text NULL ); "),
        new DatabaseCommand(1, 22, "CREATE INDEX IX_AniDB_Anime_Title_AnimeID on AniDB_Anime_Title(AnimeID);"),
        new DatabaseCommand(1, 23,
            "CREATE TABLE AniDB_Category ( AniDB_CategoryID INTEGER PRIMARY KEY AUTOINCREMENT, CategoryID int NOT NULL, ParentID int NOT NULL, IsHentai int NOT NULL, CategoryName text NOT NULL, CategoryDescription text NOT NULL  ); "),
        new DatabaseCommand(1, 24,
            "CREATE UNIQUE INDEX UIX_AniDB_Category_CategoryID ON AniDB_Category(CategoryID);"),
        new DatabaseCommand(1, 25,
            "CREATE TABLE AniDB_Character ( AniDB_CharacterID INTEGER PRIMARY KEY AUTOINCREMENT, CharID int NOT NULL, CharName text NOT NULL, PicName text NOT NULL, CharKanjiName text NOT NULL, CharDescription text NOT NULL, CreatorListRaw text NOT NULL ); "),
        new DatabaseCommand(1, 26, "CREATE UNIQUE INDEX UIX_AniDB_Character_CharID ON AniDB_Character(CharID);"),
        new DatabaseCommand(1, 27,
            "CREATE TABLE AniDB_Character_Seiyuu ( AniDB_Character_SeiyuuID INTEGER PRIMARY KEY AUTOINCREMENT, CharID int NOT NULL, SeiyuuID int NOT NULL ); "),
        new DatabaseCommand(1, 28,
            "CREATE INDEX IX_AniDB_Character_Seiyuu_CharID on AniDB_Character_Seiyuu(CharID);"),
        new DatabaseCommand(1, 29,
            "CREATE INDEX IX_AniDB_Character_Seiyuu_SeiyuuID on AniDB_Character_Seiyuu(SeiyuuID);"),
        new DatabaseCommand(1, 30,
            "CREATE UNIQUE INDEX UIX_AniDB_Character_Seiyuu_CharID_SeiyuuID ON AniDB_Character_Seiyuu(CharID, SeiyuuID);"),
        new DatabaseCommand(1, 31,
            "CREATE TABLE AniDB_Seiyuu ( AniDB_SeiyuuID INTEGER PRIMARY KEY AUTOINCREMENT, SeiyuuID int NOT NULL, SeiyuuName text NOT NULL, PicName text NOT NULL ); "),
        new DatabaseCommand(1, 32, "CREATE UNIQUE INDEX UIX_AniDB_Seiyuu_SeiyuuID ON AniDB_Seiyuu(SeiyuuID);"),
        new DatabaseCommand(1, 33,
            "CREATE TABLE AniDB_Episode ( AniDB_EpisodeID INTEGER PRIMARY KEY AUTOINCREMENT, EpisodeID int NOT NULL, AnimeID int NOT NULL, LengthSeconds int NOT NULL, Rating text NOT NULL, Votes text NOT NULL, EpisodeNumber int NOT NULL, EpisodeType int NOT NULL, RomajiName text NOT NULL, EnglishName text NOT NULL, AirDate int NOT NULL, DateTimeUpdated timestamp NOT NULL ); "),
        new DatabaseCommand(1, 34, "CREATE INDEX IX_AniDB_Episode_AnimeID on AniDB_Episode(AnimeID);"),
        new DatabaseCommand(1, 35, "CREATE UNIQUE INDEX UIX_AniDB_Episode_EpisodeID ON AniDB_Episode(EpisodeID);"),
        new DatabaseCommand(1, 36,
            "CREATE TABLE AniDB_File ( AniDB_FileID INTEGER PRIMARY KEY AUTOINCREMENT, FileID int NOT NULL, Hash text NOT NULL, AnimeID int NOT NULL, GroupID int NOT NULL, File_Source text NOT NULL, File_AudioCodec text NOT NULL, File_VideoCodec text NOT NULL, File_VideoResolution text NOT NULL, File_FileExtension text NOT NULL, File_LengthSeconds int NOT NULL, File_Description text NOT NULL, File_ReleaseDate int NOT NULL, Anime_GroupName text NOT NULL, Anime_GroupNameShort text NOT NULL, Episode_Rating int NOT NULL, Episode_Votes int NOT NULL, DateTimeUpdated timestamp NOT NULL, IsWatched int NOT NULL, WatchedDate timestamp NULL, CRC text NOT NULL, MD5 text NOT NULL, SHA1 text NOT NULL, FileName text NOT NULL, FileSize INTEGER NOT NULL ); "),
        new DatabaseCommand(1, 37, "CREATE UNIQUE INDEX UIX_AniDB_File_Hash on AniDB_File(Hash);"),
        new DatabaseCommand(1, 38, "CREATE UNIQUE INDEX UIX_AniDB_File_FileID ON AniDB_File(FileID);"),
        new DatabaseCommand(1, 39, "CREATE INDEX IX_AniDB_File_File_Source on AniDB_File(File_Source);"),
        new DatabaseCommand(1, 40,
            "CREATE TABLE AniDB_GroupStatus ( AniDB_GroupStatusID INTEGER PRIMARY KEY AUTOINCREMENT, AnimeID int NOT NULL, GroupID int NOT NULL, GroupName text NOT NULL, CompletionState int NOT NULL, LastEpisodeNumber int NOT NULL, Rating int NOT NULL, Votes int NOT NULL, EpisodeRange text NOT NULL ); "),
        new DatabaseCommand(1, 41, "CREATE INDEX IX_AniDB_GroupStatus_AnimeID on AniDB_GroupStatus(AnimeID);"),
        new DatabaseCommand(1, 42,
            "CREATE UNIQUE INDEX UIX_AniDB_GroupStatus_AnimeID_GroupID ON AniDB_GroupStatus(AnimeID, GroupID);"),
        new DatabaseCommand(1, 43,
            "CREATE TABLE AniDB_ReleaseGroup ( AniDB_ReleaseGroupID INTEGER PRIMARY KEY AUTOINCREMENT, GroupID int NOT NULL, Rating int NOT NULL, Votes int NOT NULL, AnimeCount int NOT NULL, FileCount int NOT NULL, GroupName text NOT NULL, GroupNameShort text NOT NULL, IRCChannel text NOT NULL, IRCServer text NOT NULL, URL text NOT NULL, Picname text NOT NULL ); "),
        new DatabaseCommand(1, 44,
            "CREATE UNIQUE INDEX UIX_AniDB_ReleaseGroup_GroupID ON AniDB_ReleaseGroup(GroupID);"),
        new DatabaseCommand(1, 45,
            "CREATE TABLE AniDB_Review ( AniDB_ReviewID INTEGER PRIMARY KEY AUTOINCREMENT, ReviewID int NOT NULL, AuthorID int NOT NULL, RatingAnimation int NOT NULL, RatingSound int NOT NULL, RatingStory int NOT NULL, RatingCharacter int NOT NULL, RatingValue int NOT NULL, RatingEnjoyment int NOT NULL, ReviewText text NOT NULL ); "),
        new DatabaseCommand(1, 46, "CREATE UNIQUE INDEX UIX_AniDB_Review_ReviewID ON AniDB_Review(ReviewID);"),
        new DatabaseCommand(1, 47,
            "CREATE TABLE AniDB_Tag ( AniDB_TagID INTEGER PRIMARY KEY AUTOINCREMENT, TagID int NOT NULL, Spoiler int NOT NULL, LocalSpoiler int NOT NULL, GlobalSpoiler int NOT NULL, TagName text NOT NULL, TagCount int NOT NULL, TagDescription text NOT NULL ); "),
        new DatabaseCommand(1, 48, "CREATE UNIQUE INDEX UIX_AniDB_Tag_TagID ON AniDB_Tag(TagID);"),
        new DatabaseCommand(1, 49,
            "CREATE TABLE [AnimeEpisode]( AnimeEpisodeID INTEGER PRIMARY KEY AUTOINCREMENT, AnimeSeriesID int NOT NULL, AniDB_EpisodeID int NOT NULL, DateTimeUpdated timestamp NOT NULL, DateTimeCreated timestamp NOT NULL );"),
        new DatabaseCommand(1, 50,
            "CREATE UNIQUE INDEX UIX_AnimeEpisode_AniDB_EpisodeID ON AnimeEpisode(AniDB_EpisodeID);"),
        new DatabaseCommand(1, 51, "CREATE INDEX IX_AnimeEpisode_AnimeSeriesID on AnimeEpisode(AnimeSeriesID);"),
        new DatabaseCommand(1, 52,
            "CREATE TABLE AnimeGroup ( AnimeGroupID INTEGER PRIMARY KEY AUTOINCREMENT, AnimeGroupParentID int NULL, GroupName text NOT NULL, Description text NULL, IsManuallyNamed int NOT NULL, DateTimeUpdated timestamp NOT NULL, DateTimeCreated timestamp NOT NULL, SortName text NOT NULL, MissingEpisodeCount int NOT NULL, MissingEpisodeCountGroups int NOT NULL, OverrideDescription int NOT NULL, EpisodeAddedDate timestamp NULL ); "),
        new DatabaseCommand(1, 53,
            "CREATE TABLE AnimeSeries ( AnimeSeriesID INTEGER PRIMARY KEY AUTOINCREMENT, AnimeGroupID int NOT NULL, AniDB_ID int NOT NULL, DateTimeUpdated timestamp NOT NULL, DateTimeCreated timestamp NOT NULL, DefaultAudioLanguage text NULL, DefaultSubtitleLanguage text NULL, MissingEpisodeCount int NOT NULL, MissingEpisodeCountGroups int NOT NULL, LatestLocalEpisodeNumber int NOT NULL, EpisodeAddedDate timestamp NULL ); "),
        new DatabaseCommand(1, 54, "CREATE UNIQUE INDEX UIX_AnimeSeries_AniDB_ID ON AnimeSeries(AniDB_ID);"),
        new DatabaseCommand(1, 55,
            "CREATE TABLE CommandRequest ( CommandRequestID INTEGER PRIMARY KEY AUTOINCREMENT, Priority int NOT NULL, CommandType int NOT NULL, CommandID text NOT NULL, CommandDetails text NOT NULL, DateTimeUpdated timestamp NOT NULL ); "),
        new DatabaseCommand(1, 56,
            "CREATE TABLE CrossRef_AniDB_Other( CrossRef_AniDB_OtherID INTEGER PRIMARY KEY AUTOINCREMENT, AnimeID int NOT NULL, CrossRefID text NOT NULL, CrossRefSource int NOT NULL, CrossRefType int NOT NULL ); "),
        new DatabaseCommand(1, 57,
            "CREATE UNIQUE INDEX UIX_CrossRef_AniDB_Other ON CrossRef_AniDB_Other(AnimeID, CrossRefID, CrossRefSource, CrossRefType);"),
        new DatabaseCommand(1, 58,
            "CREATE TABLE CrossRef_AniDB_TvDB( CrossRef_AniDB_TvDBID INTEGER PRIMARY KEY AUTOINCREMENT, AnimeID int NOT NULL, TvDBID int NOT NULL, TvDBSeasonNumber int NOT NULL, CrossRefSource int NOT NULL ); "),
        new DatabaseCommand(1, 59,
            "CREATE UNIQUE INDEX UIX_CrossRef_AniDB_TvDB ON CrossRef_AniDB_TvDB(AnimeID, TvDBID, TvDBSeasonNumber, CrossRefSource);"),
        new DatabaseCommand(1, 60,
            "CREATE TABLE CrossRef_File_Episode ( CrossRef_File_EpisodeID INTEGER PRIMARY KEY AUTOINCREMENT, Hash text NULL, FileName text NOT NULL, FileSize INTEGER NOT NULL, CrossRefSource int NOT NULL, AnimeID int NOT NULL, EpisodeID int NOT NULL, Percentage int NOT NULL, EpisodeOrder int NOT NULL ); "),
        new DatabaseCommand(1, 61,
            "CREATE UNIQUE INDEX UIX_CrossRef_File_Episode_Hash_EpisodeID ON CrossRef_File_Episode(Hash, EpisodeID);"),
        new DatabaseCommand(1, 62,
            "CREATE TABLE CrossRef_Languages_AniDB_File ( CrossRef_Languages_AniDB_FileID INTEGER PRIMARY KEY AUTOINCREMENT, FileID int NOT NULL, LanguageID int NOT NULL ); "),
        new DatabaseCommand(1, 63,
            "CREATE TABLE CrossRef_Subtitles_AniDB_File ( CrossRef_Subtitles_AniDB_FileID INTEGER PRIMARY KEY AUTOINCREMENT, FileID int NOT NULL, LanguageID int NOT NULL ); "),
        new DatabaseCommand(1, 64,
            "CREATE TABLE FileNameHash ( FileNameHashID INTEGER PRIMARY KEY AUTOINCREMENT, FileName text NOT NULL, FileSize INTEGER NOT NULL, Hash text NOT NULL, DateTimeUpdated timestamp NOT NULL ); "),
        new DatabaseCommand(1, 65,
            "CREATE UNIQUE INDEX UIX_FileNameHash ON FileNameHash(FileName, FileSize, Hash);"),
        new DatabaseCommand(1, 66,
            "CREATE TABLE Language ( LanguageID INTEGER PRIMARY KEY AUTOINCREMENT, LanguageName text NOT NULL ); "),
        new DatabaseCommand(1, 67, "CREATE UNIQUE INDEX UIX_Language_LanguageName ON Language(LanguageName);"),
        new DatabaseCommand(1, 68,
            "CREATE TABLE ImportFolder ( ImportFolderID INTEGER PRIMARY KEY AUTOINCREMENT, ImportFolderType int NOT NULL, ImportFolderName text NOT NULL, ImportFolderLocation text NOT NULL, IsDropSource int NOT NULL, IsDropDestination int NOT NULL ); "),
        new DatabaseCommand(1, 69,
            "CREATE TABLE ScheduledUpdate( ScheduledUpdateID INTEGER PRIMARY KEY AUTOINCREMENT,  UpdateType int NOT NULL, LastUpdate timestamp NOT NULL, UpdateDetails text NOT NULL ); "),
        new DatabaseCommand(1, 70,
            "CREATE UNIQUE INDEX UIX_ScheduledUpdate_UpdateType ON ScheduledUpdate(UpdateType);"),
        new DatabaseCommand(1, 71,
            "CREATE TABLE VideoInfo ( VideoInfoID INTEGER PRIMARY KEY AUTOINCREMENT, Hash text NOT NULL, FileSize INTEGER NOT NULL, FileName text NOT NULL, DateTimeUpdated timestamp NOT NULL, VideoCodec text NOT NULL, VideoBitrate text NOT NULL, VideoFrameRate text NOT NULL, VideoResolution text NOT NULL, AudioCodec text NOT NULL, AudioBitrate text NOT NULL, Duration INTEGER NOT NULL ); "),
        new DatabaseCommand(1, 72, "CREATE UNIQUE INDEX UIX_VideoInfo_Hash on VideoInfo(Hash);"),
        new DatabaseCommand(1, 73,
            "CREATE TABLE VideoLocal ( VideoLocalID INTEGER PRIMARY KEY AUTOINCREMENT, FilePath text NOT NULL, ImportFolderID int NOT NULL, Hash text NOT NULL, CRC32 text NULL, MD5 text NULL, SHA1 text NULL, HashSource int NOT NULL, FileSize INTEGER NOT NULL, IsIgnored int NOT NULL, DateTimeUpdated timestamp NOT NULL ); "),
        new DatabaseCommand(1, 74, "CREATE UNIQUE INDEX UIX_VideoLocal_Hash on VideoLocal(Hash)"),
        new DatabaseCommand(1, 75,
            "CREATE TABLE DuplicateFile ( DuplicateFileID INTEGER PRIMARY KEY AUTOINCREMENT, FilePathFile1 text NOT NULL, FilePathFile2 text NOT NULL, ImportFolderIDFile1 int NOT NULL, ImportFolderIDFile2 int NOT NULL, Hash text NOT NULL, DateTimeUpdated timestamp NOT NULL ); "),
        new DatabaseCommand(1, 76,
            "CREATE TABLE GroupFilter( GroupFilterID INTEGER PRIMARY KEY AUTOINCREMENT, GroupFilterName text NOT NULL, ApplyToSeries int NOT NULL, BaseCondition int NOT NULL, SortingCriteria text ); "),
        new DatabaseCommand(1, 77,
            "CREATE TABLE GroupFilterCondition( GroupFilterConditionID INTEGER PRIMARY KEY AUTOINCREMENT, GroupFilterID int NOT NULL, ConditionType int NOT NULL, ConditionOperator int NOT NULL, ConditionParameter text NOT NULL ); "),
        new DatabaseCommand(1, 78,
            "CREATE TABLE AniDB_Vote ( AniDB_VoteID INTEGER PRIMARY KEY AUTOINCREMENT, EntityID int NOT NULL, VoteValue int NOT NULL, VoteType int NOT NULL ); "),
        new DatabaseCommand(1, 79,
            "CREATE TABLE TvDB_ImageFanart ( TvDB_ImageFanartID INTEGER PRIMARY KEY AUTOINCREMENT, Id integer NOT NULL, SeriesID integer NOT NULL, BannerPath text, BannerType text, BannerType2 text, Colors text, Language text, ThumbnailPath text, VignettePath text, Enabled integer NOT NULL, Chosen INTEGER NULL)"),
        new DatabaseCommand(1, 80, "CREATE UNIQUE INDEX UIX_TvDB_ImageFanart_Id ON TvDB_ImageFanart(Id)"),
        new DatabaseCommand(1, 81,
            "CREATE TABLE TvDB_ImageWideBanner ( TvDB_ImageWideBannerID INTEGER PRIMARY KEY AUTOINCREMENT, Id integer NOT NULL, SeriesID integer NOT NULL, BannerPath text, BannerType text, BannerType2 text, Language text, Enabled integer NOT NULL, SeasonNumber integer)"),
        new DatabaseCommand(1, 82, "CREATE UNIQUE INDEX UIX_TvDB_ImageWideBanner_Id ON TvDB_ImageWideBanner(Id);"),
        new DatabaseCommand(1, 83,
            "CREATE TABLE TvDB_ImagePoster ( TvDB_ImagePosterID INTEGER PRIMARY KEY AUTOINCREMENT, Id integer NOT NULL, SeriesID integer NOT NULL, BannerPath text, BannerType text, BannerType2 text, Language text, Enabled integer NOT NULL, SeasonNumber integer)"),
        new DatabaseCommand(1, 84, "CREATE UNIQUE INDEX UIX_TvDB_ImagePoster_Id ON TvDB_ImagePoster(Id)"),
        new DatabaseCommand(1, 85,
            "CREATE TABLE TvDB_Episode ( TvDB_EpisodeID INTEGER PRIMARY KEY AUTOINCREMENT, Id integer NOT NULL, SeriesID integer NOT NULL, SeasonID integer NOT NULL, SeasonNumber integer NOT NULL, EpisodeNumber integer NOT NULL, EpisodeName text, Overview text, Filename text, EpImgFlag integer NOT NULL, FirstAired text, AbsoluteNumber integer, AirsAfterSeason integer, AirsBeforeEpisode integer, AirsBeforeSeason integer)"),
        new DatabaseCommand(1, 86, "CREATE UNIQUE INDEX UIX_TvDB_Episode_Id ON TvDB_Episode(Id);"),
        new DatabaseCommand(1, 87,
            "CREATE TABLE TvDB_Series( TvDB_SeriesID INTEGER PRIMARY KEY AUTOINCREMENT, SeriesID integer NOT NULL, Overview text, SeriesName text, Status text, Banner text, Fanart text, Poster text, Lastupdated text)"),
        new DatabaseCommand(1, 88, "CREATE UNIQUE INDEX UIX_TvDB_Series_Id ON TvDB_Series(SeriesID);"),
        new DatabaseCommand(1, 89,
            "CREATE TABLE AniDB_Anime_DefaultImage ( AniDB_Anime_DefaultImageID INTEGER PRIMARY KEY AUTOINCREMENT, AnimeID int NOT NULL, ImageParentID int NOT NULL, ImageParentType int NOT NULL, ImageType int NOT NULL );"),
        new DatabaseCommand(1, 90,
            "CREATE UNIQUE INDEX UIX_AniDB_Anime_DefaultImage_ImageType ON AniDB_Anime_DefaultImage(AnimeID, ImageType)"),
        new DatabaseCommand(1, 91,
            "CREATE TABLE MovieDB_Movie( MovieDB_MovieID INTEGER PRIMARY KEY AUTOINCREMENT, MovieId int NOT NULL, MovieName text, OriginalName text, Overview text );"),
        new DatabaseCommand(1, 92, "CREATE UNIQUE INDEX UIX_MovieDB_Movie_Id ON MovieDB_Movie(MovieId)"),
        new DatabaseCommand(1, 93,
            "CREATE TABLE MovieDB_Poster( MovieDB_PosterID INTEGER PRIMARY KEY AUTOINCREMENT, ImageID text, MovieId int NOT NULL, ImageType text, ImageSize text,  URL text,  ImageWidth int NOT NULL,  ImageHeight int NOT NULL,  Enabled int NOT NULL );"),
        new DatabaseCommand(1, 94,
            "CREATE TABLE MovieDB_Fanart( MovieDB_FanartID INTEGER PRIMARY KEY AUTOINCREMENT, ImageID text, MovieId int NOT NULL, ImageType text, ImageSize text,  URL text,  ImageWidth int NOT NULL,  ImageHeight int NOT NULL,  Enabled int NOT NULL );"),
        new DatabaseCommand(1, 95,
            "CREATE TABLE JMMUser( JMMUserID INTEGER PRIMARY KEY AUTOINCREMENT, Username text, Password text, IsAdmin int NOT NULL, IsAniDBUser int NOT NULL, IsTraktUser int NOT NULL, HideCategories text );"),
        new DatabaseCommand(1, 96,
            "CREATE TABLE Trakt_Episode( Trakt_EpisodeID INTEGER PRIMARY KEY AUTOINCREMENT, Trakt_ShowID int NOT NULL, Season int NOT NULL, EpisodeNumber int NOT NULL, Title text, URL text, Overview text, EpisodeImage text );"),
        new DatabaseCommand(1, 97,
            "CREATE TABLE Trakt_ImagePoster( Trakt_ImagePosterID INTEGER PRIMARY KEY AUTOINCREMENT, Trakt_ShowID int NOT NULL, Season int NOT NULL, ImageURL text, Enabled int NOT NULL );"),
        new DatabaseCommand(1, 98,
            "CREATE TABLE Trakt_ImageFanart( Trakt_ImageFanartID INTEGER PRIMARY KEY AUTOINCREMENT, Trakt_ShowID int NOT NULL, Season int NOT NULL, ImageURL text, Enabled int NOT NULL );"),
        new DatabaseCommand(1, 99,
            "CREATE TABLE Trakt_Show( Trakt_ShowID INTEGER PRIMARY KEY AUTOINCREMENT, TraktID text, Title text, Year text, URL text, Overview text, TvDB_ID int NULL );"),
        new DatabaseCommand(1, 100,
            "CREATE TABLE Trakt_Season( Trakt_SeasonID INTEGER PRIMARY KEY AUTOINCREMENT, Trakt_ShowID int NOT NULL, Season int NOT NULL, URL text );"),
        new DatabaseCommand(1, 101,
            "CREATE TABLE CrossRef_AniDB_Trakt( CrossRef_AniDB_TraktID INTEGER PRIMARY KEY AUTOINCREMENT, AnimeID int NOT NULL, TraktID text, TraktSeasonNumber int NOT NULL, CrossRefSource int NOT NULL );"),
        new DatabaseCommand(1, 102,
            "CREATE TABLE AnimeEpisode_User( AnimeEpisode_UserID INTEGER PRIMARY KEY AUTOINCREMENT, JMMUserID int NOT NULL, AnimeEpisodeID int NOT NULL, AnimeSeriesID int NOT NULL, WatchedDate timestamp NULL, PlayedCount int NOT NULL, WatchedCount int NOT NULL, StoppedCount int NOT NULL );"),
        new DatabaseCommand(1, 103,
            "CREATE UNIQUE INDEX UIX_AnimeEpisode_User_User_EpisodeID ON AnimeEpisode_User(JMMUserID, AnimeEpisodeID);"),
        new DatabaseCommand(1, 104,
            "CREATE INDEX IX_AnimeEpisode_User_User_AnimeSeriesID on AnimeEpisode_User(JMMUserID, AnimeSeriesID);"),
        new DatabaseCommand(1, 105,
            "CREATE TABLE AnimeSeries_User( AnimeSeries_UserID INTEGER PRIMARY KEY AUTOINCREMENT, JMMUserID int NOT NULL, AnimeSeriesID int NOT NULL, UnwatchedEpisodeCount int NOT NULL, WatchedEpisodeCount int NOT NULL, WatchedDate timestamp NULL, PlayedCount int NOT NULL, WatchedCount int NOT NULL, StoppedCount int NOT NULL ); "),
        new DatabaseCommand(1, 106,
            "CREATE UNIQUE INDEX UIX_AnimeSeries_User_User_SeriesID ON AnimeSeries_User(JMMUserID, AnimeSeriesID);"),
        new DatabaseCommand(1, 107,
            "CREATE TABLE AnimeGroup_User( AnimeGroup_UserID INTEGER PRIMARY KEY AUTOINCREMENT, JMMUserID int NOT NULL, AnimeGroupID int NOT NULL, IsFave int NOT NULL, UnwatchedEpisodeCount int NOT NULL, WatchedEpisodeCount int NOT NULL, WatchedDate timestamp NULL, PlayedCount int NOT NULL, WatchedCount int NOT NULL, StoppedCount int NOT NULL ); "),
        new DatabaseCommand(1, 108,
            "CREATE UNIQUE INDEX UIX_AnimeGroup_User_User_GroupID ON AnimeGroup_User(JMMUserID, AnimeGroupID);"),
        new DatabaseCommand(1, 109,
            "CREATE TABLE VideoLocal_User( VideoLocal_UserID INTEGER PRIMARY KEY AUTOINCREMENT, JMMUserID int NOT NULL, VideoLocalID int NOT NULL, WatchedDate timestamp NOT NULL ); "),
        new DatabaseCommand(1, 110,
            "CREATE UNIQUE INDEX UIX_VideoLocal_User_User_VideoLocalID ON VideoLocal_User(JMMUserID, VideoLocalID);"),
    };

    private List<DatabaseCommand> updateVersionTable = new()
    {
        new DatabaseCommand("ALTER TABLE Versions ADD VersionRevision text NULL;"),
        new DatabaseCommand("ALTER TABLE Versions ADD VersionCommand text NULL;"),
        new DatabaseCommand("ALTER TABLE Versions ADD VersionProgram text NULL;"),
        new DatabaseCommand(
            "CREATE INDEX IX_Versions_VersionType ON Versions(VersionType,VersionValue,VersionRevision);")
    };


    private List<DatabaseCommand> patchCommands = new()
    {
        new(2, 1,
            "CREATE TABLE IgnoreAnime( IgnoreAnimeID INTEGER PRIMARY KEY AUTOINCREMENT, JMMUserID int NOT NULL, AnimeID int NOT NULL, IgnoreType int NOT NULL)"),
        new(2, 2,
            "CREATE UNIQUE INDEX UIX_IgnoreAnime_User_AnimeID ON IgnoreAnime(JMMUserID, AnimeID, IgnoreType);"),
        new(3, 1,
            "CREATE TABLE Trakt_Friend( Trakt_FriendID INTEGER PRIMARY KEY AUTOINCREMENT, Username text NOT NULL, FullName text NULL, Gender text NULL, Age text NULL, Location text NULL, About text NULL, Joined int NOT NULL, Avatar text NULL, Url text NULL, LastAvatarUpdate timestamp NOT NULL)"),
        new(3, 2, "CREATE UNIQUE INDEX UIX_Trakt_Friend_Username ON Trakt_Friend(Username);"),
        new(4, 1, "ALTER TABLE AnimeGroup ADD DefaultAnimeSeriesID int NULL"),
        new(5, 1, "ALTER TABLE JMMUser ADD CanEditServerSettings int NULL"),
        new(6, 1, DatabaseFixes.NoOperation),
        new(6, 2, DatabaseFixes.NoOperation),
        new(6, 3,
            "CREATE UNIQUE INDEX UIX_CrossRef_AniDB_TvDB_Season ON CrossRef_AniDB_TvDB(TvDBID, TvDBSeasonNumber);"),
        new(6, 4,
            "CREATE UNIQUE INDEX UIX_CrossRef_AniDB_TvDB_AnimeID ON CrossRef_AniDB_TvDB(AnimeID);"),
        new(6, 5,
            "CREATE UNIQUE INDEX UIX_CrossRef_AniDB_Trakt_Season ON CrossRef_AniDB_Trakt(TraktID, TraktSeasonNumber);"),
        new(6, 6,
            "CREATE UNIQUE INDEX UIX_CrossRef_AniDB_Trakt_Anime ON CrossRef_AniDB_Trakt(AnimeID);"),
        new(7, 1, "ALTER TABLE VideoInfo ADD VideoBitDepth text NULL"),
        new(9, 1, "ALTER TABLE ImportFolder ADD IsWatched int NOT NULL DEFAULT 1"),
        new(10, 1,
            "CREATE TABLE CrossRef_AniDB_MAL( CrossRef_AniDB_MALID INTEGER PRIMARY KEY AUTOINCREMENT, AnimeID int NOT NULL, MALID int NOT NULL, MALTitle text, CrossRefSource int NOT NULL ); "),
        new(10, 2,
            "CREATE UNIQUE INDEX UIX_CrossRef_AniDB_MAL_AnimeID ON CrossRef_AniDB_MAL(AnimeID);"),
        new(10, 3,
            "CREATE UNIQUE INDEX UIX_CrossRef_AniDB_MAL_MALID ON CrossRef_AniDB_MAL(MALID);"),
        new(11, 1, "DROP TABLE CrossRef_AniDB_MAL;"),
        new(11, 2,
            "CREATE TABLE CrossRef_AniDB_MAL( CrossRef_AniDB_MALID INTEGER PRIMARY KEY AUTOINCREMENT, AnimeID int NOT NULL, MALID int NOT NULL, MALTitle text, StartEpisodeType int NOT NULL, StartEpisodeNumber int NOT NULL, CrossRefSource int NOT NULL ); "),
        new(11, 3,
            "CREATE UNIQUE INDEX UIX_CrossRef_AniDB_MAL_MALID ON CrossRef_AniDB_MAL(MALID);"),
        new(11, 4,
            "CREATE UNIQUE INDEX UIX_CrossRef_AniDB_MAL_Anime ON CrossRef_AniDB_MAL(AnimeID, StartEpisodeType, StartEpisodeNumber);"),
        new(12, 1,
            "CREATE TABLE Playlist( PlaylistID INTEGER PRIMARY KEY AUTOINCREMENT, PlaylistName text, PlaylistItems text, DefaultPlayOrder int NOT NULL, PlayWatched int NOT NULL, PlayUnwatched int NOT NULL ); "),
        new(13, 1, "ALTER TABLE AnimeSeries ADD SeriesNameOverride text"),
        new(14, 1,
            "CREATE TABLE BookmarkedAnime( BookmarkedAnimeID INTEGER PRIMARY KEY AUTOINCREMENT, AnimeID int NOT NULL, Priority int NOT NULL, Notes text, Downloading int NOT NULL ); "),
        new(14, 2,
            "CREATE UNIQUE INDEX UIX_BookmarkedAnime_AnimeID ON BookmarkedAnime(BookmarkedAnimeID)"),
        new(15, 1, "ALTER TABLE VideoLocal ADD DateTimeCreated timestamp NULL"),
        new(15, 2, "UPDATE VideoLocal SET DateTimeCreated = DateTimeUpdated"),
        new(16, 1,
            "CREATE TABLE CrossRef_AniDB_TvDB_Episode( CrossRef_AniDB_TvDB_EpisodeID INTEGER PRIMARY KEY AUTOINCREMENT, AnimeID int NOT NULL, AniDBEpisodeID int NOT NULL, TvDBEpisodeID int NOT NULL ); "),
        new(16, 2,
            "CREATE UNIQUE INDEX UIX_CrossRef_AniDB_TvDB_Episode_AniDBEpisodeID ON CrossRef_AniDB_TvDB_Episode(AniDBEpisodeID);"),
        new(17, 1,
            "CREATE TABLE AniDB_MylistStats( AniDB_MylistStatsID INTEGER PRIMARY KEY AUTOINCREMENT, Animes int NOT NULL, Episodes int NOT NULL, Files int NOT NULL, SizeOfFiles INTEGER NOT NULL, AddedAnimes int NOT NULL, AddedEpisodes int NOT NULL, AddedFiles int NOT NULL, AddedGroups int NOT NULL, LeechPct int NOT NULL, GloryPct int NOT NULL, ViewedPct int NOT NULL, MylistPct int NOT NULL, ViewedMylistPct int NOT NULL, EpisodesViewed int NOT NULL, Votes int NOT NULL, Reviews int NOT NULL, ViewiedLength int NOT NULL ); "),
        new(18, 1,
            "CREATE TABLE FileFfdshowPreset( FileFfdshowPresetID INTEGER PRIMARY KEY AUTOINCREMENT, Hash int NOT NULL, FileSize INTEGER NOT NULL, Preset text ); "),
        new(18, 2,
            "CREATE UNIQUE INDEX UIX_FileFfdshowPreset_Hash ON FileFfdshowPreset(Hash, FileSize);"),
        new(19, 1, "ALTER TABLE AniDB_Anime ADD DisableExternalLinksFlag int NULL"),
        new(19, 2, "UPDATE AniDB_Anime SET DisableExternalLinksFlag = 0"),
        new(20, 1, "ALTER TABLE AniDB_File ADD FileVersion int NULL"),
        new(20, 2, "UPDATE AniDB_File SET FileVersion = 1"),
        new(21, 1,
            "CREATE TABLE RenameScript( RenameScriptID INTEGER PRIMARY KEY AUTOINCREMENT, ScriptName text, Script text, IsEnabledOnImport int NOT NULL ); "),
        new(22, 1, "ALTER TABLE AniDB_File ADD IsCensored int NULL"),
        new(22, 2, "ALTER TABLE AniDB_File ADD IsDeprecated int NULL"),
        new(22, 3, "ALTER TABLE AniDB_File ADD InternalVersion int NULL"),
        new(22, 4, "UPDATE AniDB_File SET IsCensored = 0"),
        new(22, 5, "UPDATE AniDB_File SET IsDeprecated = 0"),
        new(22, 6, "UPDATE AniDB_File SET InternalVersion = 1"),
        new(23, 1, "UPDATE JMMUser SET CanEditServerSettings = 1"),
        new(24, 1, "ALTER TABLE VideoLocal ADD IsVariation int NULL"),
        new(24, 2, "UPDATE VideoLocal SET IsVariation = 0"),
        new(25, 1,
            "CREATE TABLE AniDB_Recommendation( AniDB_RecommendationID INTEGER PRIMARY KEY AUTOINCREMENT, AnimeID int NOT NULL, UserID int NOT NULL, RecommendationType int NOT NULL, RecommendationText text ); "),
        new(25, 2,
            "CREATE UNIQUE INDEX UIX_AniDB_Recommendation ON AniDB_Recommendation(AnimeID, UserID);"),
        new(26, 1, "CREATE INDEX IX_CrossRef_File_Episode_Hash ON CrossRef_File_Episode(Hash);"),
        new(26, 2,
            "CREATE INDEX IX_CrossRef_File_Episode_EpisodeID ON CrossRef_File_Episode(EpisodeID);"),
        new(27, 1,
            "update CrossRef_File_Episode SET CrossRefSource=1 WHERE Hash IN (Select Hash from ANIDB_File) AND CrossRefSource=2;"),
        new(28, 1,
            "CREATE TABLE LogMessage( LogMessageID INTEGER PRIMARY KEY AUTOINCREMENT, LogType text, LogContent text, LogDate timestamp NOT NULL ); "),
        new(29, 1,
            "CREATE TABLE CrossRef_AniDB_TvDBV2( CrossRef_AniDB_TvDBV2ID INTEGER PRIMARY KEY AUTOINCREMENT, AnimeID int NOT NULL, AniDBStartEpisodeType int NOT NULL, AniDBStartEpisodeNumber int NOT NULL, TvDBID int NOT NULL, TvDBSeasonNumber int NOT NULL, TvDBStartEpisodeNumber int NOT NULL, TvDBTitle text, CrossRefSource int NOT NULL ); "),
        new(29, 2,
            "CREATE UNIQUE INDEX UIX_CrossRef_AniDB_TvDBV2 ON CrossRef_AniDB_TvDBV2(AnimeID, TvDBID, TvDBSeasonNumber, TvDBStartEpisodeNumber, AniDBStartEpisodeType, AniDBStartEpisodeNumber);"),
        new(29, 3, DatabaseFixes.NoOperation),
        new(30, 1, "ALTER TABLE GroupFilter ADD Locked int NULL"),
        new(31, 1, "ALTER TABLE VideoInfo ADD FullInfo text NULL"),
        new(32, 1,
            "CREATE TABLE CrossRef_AniDB_TraktV2( CrossRef_AniDB_TraktV2ID INTEGER PRIMARY KEY AUTOINCREMENT, AnimeID int NOT NULL, AniDBStartEpisodeType int NOT NULL, AniDBStartEpisodeNumber int NOT NULL, TraktID text, TraktSeasonNumber int NOT NULL, TraktStartEpisodeNumber int NOT NULL, TraktTitle text, CrossRefSource int NOT NULL ); "),
        new(32, 2,
            "CREATE UNIQUE INDEX UIX_CrossRef_AniDB_TraktV2 ON CrossRef_AniDB_TraktV2(AnimeID, TraktSeasonNumber, TraktStartEpisodeNumber, AniDBStartEpisodeType, AniDBStartEpisodeNumber);"),
        new(32, 3, DatabaseFixes.NoOperation),
        new(33, 1,
            "CREATE TABLE CrossRef_AniDB_Trakt_Episode( CrossRef_AniDB_Trakt_EpisodeID INTEGER PRIMARY KEY AUTOINCREMENT, AnimeID int NOT NULL, AniDBEpisodeID int NOT NULL, TraktID text, Season int NOT NULL, EpisodeNumber int NOT NULL ); "),
        new(33, 2,
            "CREATE UNIQUE INDEX UIX_CrossRef_AniDB_Trakt_Episode_AniDBEpisodeID ON CrossRef_AniDB_Trakt_Episode(AniDBEpisodeID);"),
        new(34, 1, DatabaseFixes.NoOperation),
        new(35, 1,
            "CREATE TABLE CustomTag( CustomTagID INTEGER PRIMARY KEY AUTOINCREMENT, TagName text, TagDescription text ); "),
        new(35, 2,
            "CREATE TABLE CrossRef_CustomTag( CrossRef_CustomTagID INTEGER PRIMARY KEY AUTOINCREMENT, CustomTagID int NOT NULL, CrossRefID int NOT NULL, CrossRefType int NOT NULL ); "),
        new(36, 1, "ALTER TABLE AniDB_Anime_Tag ADD Weight int NULL"),
        new(37, 1, DatabaseFixes.PopulateTagWeight),
        new(38, 1, "ALTER TABLE Trakt_Episode ADD TraktID int NULL"),
        new(39, 1, DatabaseFixes.NoOperation),
        new(40, 1, "DROP TABLE LogMessage;"),
        new(41, 1, "ALTER TABLE AnimeSeries ADD DefaultFolder text NULL"),
        new(42, 1, "ALTER TABLE JMMUser ADD PlexUsers text NULL"),
        new(43, 1, "ALTER TABLE GroupFilter ADD FilterType int NOT NULL DEFAULT 1"),
        new(43, 2,
            $"UPDATE GroupFilter SET FilterType = 2 WHERE GroupFilterName='{Constants.GroupFilterName.ContinueWatching}'"),
        new(43, 3, DatabaseFixes.NoOperation),
        new(44, 1, DropAniDB_AnimeAllCategories),
        new(44, 2, "ALTER TABLE AniDB_Anime ADD ContractVersion int NOT NULL DEFAULT 0"),
        new(44, 3, "ALTER TABLE AniDB_Anime ADD ContractBlob BLOB NULL"),
        new(44, 4, "ALTER TABLE AniDB_Anime ADD ContractSize int NOT NULL DEFAULT 0"),
        new(44, 5, "ALTER TABLE AnimeGroup ADD ContractVersion int NOT NULL DEFAULT 0"),
        new(44, 6, "ALTER TABLE AnimeGroup ADD LatestEpisodeAirDate timestamp NULL"),
        new(44, 7, "ALTER TABLE AnimeGroup ADD ContractBlob BLOB NULL"),
        new(44, 8, "ALTER TABLE AnimeGroup ADD ContractSize int NOT NULL DEFAULT 0"),
        new(44, 9, "ALTER TABLE AnimeGroup_User ADD PlexContractVersion int NOT NULL DEFAULT 0"),
        new(44, 10, "ALTER TABLE AnimeGroup_User ADD PlexContractBlob BLOB NULL"),
        new(44, 11, "ALTER TABLE AnimeGroup_User ADD PlexContractSize int NOT NULL DEFAULT 0"),
        new(44, 12, "ALTER TABLE AnimeSeries ADD ContractVersion int NOT NULL DEFAULT 0"),
        new(44, 13, "ALTER TABLE AnimeSeries ADD LatestEpisodeAirDate timestamp NULL"),
        new(44, 14, "ALTER TABLE AnimeSeries ADD ContractBlob BLOB NULL"),
        new(44, 15, "ALTER TABLE AnimeSeries ADD ContractSize int NOT NULL DEFAULT 0"),
        new(44, 16, "ALTER TABLE AnimeSeries_User ADD PlexContractVersion int NOT NULL DEFAULT 0"),
        new(44, 17, "ALTER TABLE AnimeSeries_User ADD PlexContractBlob BLOB NULL"),
        new(44, 18, "ALTER TABLE AnimeSeries_User ADD PlexContractSize int NOT NULL DEFAULT 0"),
        new(44, 19, "ALTER TABLE GroupFilter ADD GroupsIdsVersion int NOT NULL DEFAULT 0"),
        new(44, 20, "ALTER TABLE GroupFilter ADD GroupsIdsString text NULL"),
        new(44, 21, "ALTER TABLE GroupFilter ADD GroupConditionsVersion int NOT NULL DEFAULT 0"),
        new(44, 22, "ALTER TABLE GroupFilter ADD GroupConditions text NULL"),
        new(44, 23, "ALTER TABLE GroupFilter ADD ParentGroupFilterID int NULL"),
        new(44, 24, "ALTER TABLE GroupFilter ADD InvisibleInClients int NOT NULL DEFAULT 0"),
        new(44, 25, "ALTER TABLE GroupFilter ADD SeriesIdsVersion int NOT NULL DEFAULT 0"),
        new(44, 26, "ALTER TABLE GroupFilter ADD SeriesIdsString text NULL"),
        new(44, 27, "ALTER TABLE AnimeEpisode ADD PlexContractVersion int NOT NULL DEFAULT 0"),
        new(44, 28, "ALTER TABLE AnimeEpisode ADD PlexContractBlob BLOB NULL"),
        new(44, 29, "ALTER TABLE AnimeEpisode ADD PlexContractSize int NOT NULL DEFAULT 0"),
        new(44, 30, "ALTER TABLE AnimeEpisode_User ADD ContractVersion int NOT NULL DEFAULT 0"),
        new(44, 31, "ALTER TABLE AnimeEpisode_User ADD ContractBlob BLOB NULL"),
        new(44, 32, "ALTER TABLE AnimeEpisode_User ADD ContractSize int NOT NULL DEFAULT 0"),
        new(44, 33, "ALTER TABLE VideoLocal ADD MediaVersion int NOT NULL DEFAULT 0"),
        new(44, 34, "ALTER TABLE VideoLocal ADD MediaBlob BLOB NULL"),
        new(44, 35, "ALTER TABLE VideoLocal ADD MediaSize int NOT NULL DEFAULT 0"),
        new(45, 1, DatabaseFixes.DeleteSeriesUsersWithoutSeries),
        new(46, 1,
            "CREATE TABLE VideoLocal_Place ( VideoLocal_Place_ID INTEGER PRIMARY KEY AUTOINCREMENT,VideoLocalID int NOT NULL, FilePath text NOT NULL,  ImportFolderID int NOT NULL, ImportFolderType int NOT NULL )"),
        new(46, 2,
            "CREATE UNIQUE INDEX [UIX_VideoLocal_ VideoLocal_Place_ID] ON [VideoLocal_Place] ([VideoLocal_Place_ID]);"),
        new(46, 3,
            "INSERT INTO VideoLocal_Place (VideoLocalID, FilePath, ImportFolderID, ImportFolderType) SELECT VideoLocalID, FilePath, ImportFolderID, 1 as ImportFolderType FROM VideoLocal"),
        new(46, 4, DropVideolocalColumns),
        new(46, 5,
            "UPDATE VideoLocal SET FileName=(SELECT FileName FROM VideoInfo WHERE VideoInfo.Hash=VideoLocal.Hash), VideoCodec=(SELECT VideoCodec FROM VideoInfo WHERE VideoInfo.Hash=VideoLocal.Hash), VideoBitrate=(SELECT VideoBitrate FROM VideoInfo WHERE VideoInfo.Hash=VideoLocal.Hash), VideoBitDepth=(SELECT VideoBitDepth FROM VideoInfo WHERE VideoInfo.Hash=VideoLocal.Hash), VideoFrameRate=(SELECT VideoFrameRate FROM VideoInfo WHERE VideoInfo.Hash=VideoLocal.Hash), VideoResolution=(SELECT VideoResolution FROM VideoInfo WHERE VideoInfo.Hash=VideoLocal.Hash), AudioCodec=(SELECT AudioCodec FROM VideoInfo WHERE VideoInfo.Hash=VideoLocal.Hash), AudioBitrate=(SELECT AudioBitrate FROM VideoInfo WHERE VideoInfo.Hash=VideoLocal.Hash), Duration=(SELECT Duration FROM VideoInfo WHERE VideoInfo.Hash=VideoLocal.Hash) WHERE RowId IN (SELECT RowId FROM VideoInfo WHERE VideoInfo.Hash=VideoLocal.Hash)"),
        new(46, 6,
            "CREATE TABLE CloudAccount (CloudID INTEGER PRIMARY KEY AUTOINCREMENT, ConnectionString text NOT NULL, Provider text NOT NULL, Name text NOT NULL);"),
        new(46, 7, "CREATE UNIQUE INDEX [UIX_CloudAccount_CloudID] ON [CloudAccount] ([CloudID]);"),
        new(46, 8, "ALTER TABLE ImportFolder ADD CloudID int NULL"),
        new(46, 9, "DROP TABLE VideoInfo"),
        new(46, 10, AlterVideoLocalUser),
        new(47, 1, "DROP INDEX UIX2_VideoLocal_Hash;"),
        new(47, 2, "CREATE INDEX IX_VideoLocal_Hash ON VideoLocal(Hash);"),
        new(48, 1,
            "CREATE TABLE AuthTokens ( AuthID INTEGER PRIMARY KEY AUTOINCREMENT, UserID int NOT NULL, DeviceName text NOT NULL, Token text NOT NULL )"),
        new(49, 1,
            "CREATE TABLE Scan ( ScanID INTEGER PRIMARY KEY AUTOINCREMENT, CreationTime timestamp NOT NULL, ImportFolders text NOT NULL, Status int NOT NULL )"),
        new(49, 2,
            "CREATE TABLE ScanFile ( ScanFileID INTEGER PRIMARY KEY AUTOINCREMENT, ScanID int NOT NULL, ImportFolderID int NOT NULL, VideoLocal_Place_ID int NOT NULL, FullName text NOT NULL, FileSize bigint NOT NULL, Status int NOT NULL, CheckDate timestamp NULL, Hash text NOT NULL, HashResult text NULL )"),
        new(49, 3, "CREATE INDEX UIX_ScanFileStatus ON ScanFile(ScanID,Status,CheckDate);"),
        new(50, 1, DatabaseFixes.NoOperation),
        new(51, 1, DatabaseFixes.NoOperation),
        new(52, 1, DatabaseFixes.NoOperation),
        new(53, 1, "ALTER TABLE JMMUser ADD PlexToken text NULL"),
        new(54, 1, "ALTER TABLE AniDB_File ADD IsChaptered INT NOT NULL DEFAULT -1"),
        new(55, 1, "ALTER TABLE RenameScript ADD RenamerType TEXT NOT NULL DEFAULT 'Legacy'"),
        new(55, 2, "ALTER TABLE RenameScript ADD ExtraData TEXT"),
        new(56, 1,
            "CREATE INDEX IX_AniDB_Anime_Character_CharID ON AniDB_Anime_Character(CharID);"),
        // This adds the new columns `AirDate` and `Rating` as well
        new(57, 1, "DROP INDEX UIX_TvDB_Episode_Id;"),
        new(57, 2, DropTvDB_EpisodeFirstAiredColumn),
        new(57, 3, DatabaseFixes.NoOperation),
        new(58, 1, "ALTER TABLE AnimeSeries ADD AirsOn TEXT NULL"),
        new(59, 1, "DROP TABLE Trakt_ImageFanart"),
        new(59, 2, "DROP TABLE Trakt_ImagePoster"),
        new(60, 1,
            "CREATE TABLE AnimeCharacter ( CharacterID INTEGER PRIMARY KEY AUTOINCREMENT, AniDBID INTEGER NOT NULL, Name TEXT NOT NULL, AlternateName TEXT NULL, Description TEXT NULL, ImagePath TEXT NULL )"),
        new(60, 2,
            "CREATE TABLE AnimeStaff ( StaffID INTEGER PRIMARY KEY AUTOINCREMENT, AniDBID INTEGER NOT NULL, Name TEXT NOT NULL, AlternateName TEXT NULL, Description TEXT NULL, ImagePath TEXT NULL )"),
        new(60, 3,
            "CREATE TABLE CrossRef_Anime_Staff ( CrossRef_Anime_StaffID INTEGER PRIMARY KEY AUTOINCREMENT, AniDB_AnimeID INTEGER NOT NULL, StaffID INTEGER NOT NULL, Role TEXT NULL, RoleID INTEGER, RoleType INTEGER NOT NULL, Language TEXT NOT NULL )"),
        new(60, 4, DatabaseFixes.NoOperation),
        new(61, 1, "ALTER TABLE MovieDB_Movie ADD Rating INT NOT NULL DEFAULT 0"),
        new(61, 2, "ALTER TABLE TvDB_Series ADD Rating INT NULL"),
        new(62, 1, "ALTER TABLE AniDB_Episode ADD Description TEXT NOT NULL DEFAULT ''"),
        new(62, 2, DatabaseFixes.NoOperation),
        new(63, 1, DatabaseFixes.RefreshAniDBInfoFromXML),
        new(64, 1, DatabaseFixes.NoOperation),
        new(64, 2, DatabaseFixes.UpdateAllStats),
        new(65, 1, DatabaseFixes.NoOperation),
        new(66, 1,
            "CREATE TABLE AniDB_AnimeUpdate ( AniDB_AnimeUpdateID INTEGER PRIMARY KEY AUTOINCREMENT, AnimeID INTEGER NOT NULL, UpdatedAt timestamp NOT NULL )"),
        new(66, 2, "CREATE UNIQUE INDEX UIX_AniDB_AnimeUpdate ON AniDB_AnimeUpdate(AnimeID)"),
        new(66, 3, DatabaseFixes.MigrateAniDB_AnimeUpdates),
        new(67, 1, DatabaseFixes.NoOperation),
        new(68, 1, DatabaseFixes.NoOperation),
        new(69, 1, DatabaseFixes.NoOperation),
        new(70, 1, "DROP INDEX UIX_CrossRef_AniDB_MAL_Anime;"),
        new(70, 2, "ALTER TABLE AniDB_Anime ADD Site_JP TEXT NULL"),
        new(70, 3, "ALTER TABLE AniDB_Anime ADD Site_EN TEXT NULL"),
        new(70, 4, "ALTER TABLE AniDB_Anime ADD Wikipedia_ID TEXT NULL"),
        new(70, 5, "ALTER TABLE AniDB_Anime ADD WikipediaJP_ID TEXT NULL"),
        new(70, 6, "ALTER TABLE AniDB_Anime ADD SyoboiID INT NULL"),
        new(70, 7, "ALTER TABLE AniDB_Anime ADD AnisonID INT NULL"),
        new(70, 8, "ALTER TABLE AniDB_Anime ADD CrunchyrollID TEXT NULL"),
        new(70, 9, DatabaseFixes.NoOperation),
        new(71, 1, "ALTER TABLE VideoLocal ADD MyListID INT NOT NULL DEFAULT 0"),
        new(71, 2, DatabaseFixes.NoOperation),
        new(72, 1, DropAniDB_EpisodeTitles),
        new(72, 2,
            "CREATE TABLE AniDB_Episode_Title ( AniDB_Episode_TitleID INTEGER PRIMARY KEY AUTOINCREMENT, AniDB_EpisodeID int NOT NULL, Language text NOT NULL, Title text NOT NULL ); "),
        new(72, 3, DatabaseFixes.NoOperation),
        new(73, 1, "DROP INDEX UIX_CrossRef_AniDB_TvDB_Episode_AniDBEpisodeID;"),
        // SQLite is stupid, so we need to create a new table and copy the contents to it
        new(73, 2, RenameCrossRef_AniDB_TvDB_Episode),
        // For some reason, this was never dropped
        new(73, 3, "DROP TABLE CrossRef_AniDB_TvDB;"),
        new(73, 4,
            "CREATE TABLE CrossRef_AniDB_TvDB(CrossRef_AniDB_TvDBID INTEGER PRIMARY KEY AUTOINCREMENT, AniDBID int NOT NULL, TvDBID int NOT NULL, CrossRefSource INT NOT NULL);"),
        new(73, 5,
            "CREATE UNIQUE INDEX UIX_AniDB_TvDB_AniDBID_TvDBID ON CrossRef_AniDB_TvDB(AniDBID,TvDBID);"),
        new(73, 6,
            "CREATE TABLE CrossRef_AniDB_TvDB_Episode(CrossRef_AniDB_TvDB_EpisodeID INTEGER PRIMARY KEY AUTOINCREMENT, AniDBEpisodeID int NOT NULL, TvDBEpisodeID int NOT NULL, MatchRating INT NOT NULL);"),
        new(73, 7,
            "CREATE UNIQUE INDEX UIX_CrossRef_AniDB_TvDB_Episode_AniDBID_TvDBID ON CrossRef_AniDB_TvDB_Episode(AniDBEpisodeID,TvDBEpisodeID);"),
        new(73, 9, DatabaseFixes.NoOperation),
        // DatabaseFixes.MigrateTvDBLinks_v2_to_V3() drops the CrossRef_AniDB_TvDBV2 table. We do it after init to migrate
        new(74, 1, DatabaseFixes.NoOperation),
        new(75, 1, DatabaseFixes.NoOperation),
        new(76, 1,
            "ALTER TABLE AnimeSeries ADD UpdatedAt timestamp NOT NULL default '2000-01-01 00:00:00'"),
        new(77, 1, DatabaseFixes.NoOperation),
        new(78, 1, DropVideoLocal_Media),
        new(79, 1, "DROP INDEX IF EXISTS UIX_CrossRef_AniDB_MAL_MALID;"),
        new(79, 1, "DROP INDEX IF EXISTS UIX_CrossRef_AniDB_MAL_MALID;"),
        new(80, 1, "DROP INDEX IF EXISTS UIX_AniDB_File_FileID;"),
        new(81, 1,
            "CREATE TABLE AniDB_Anime_Staff ( AniDB_Anime_StaffID INTEGER PRIMARY KEY AUTOINCREMENT, AnimeID INTEGER NOT NULL, CreatorID INTEGER NOT NULL, CreatorType TEXT NOT NULL );"),
        new(81, 2, DatabaseFixes.RefreshAniDBInfoFromXML),
        new(82, 1, DatabaseFixes.EnsureNoOrphanedGroupsOrSeries),
        new(83, 1, "UPDATE VideoLocal_User SET WatchedDate = NULL WHERE WatchedDate = '1970-01-01 00:00:00';"),
        new(83, 2, "ALTER TABLE VideoLocal_User ADD WatchedCount INT NOT NULL DEFAULT 0;"),
        new(83, 3, "ALTER TABLE VideoLocal_User ADD LastUpdated timestamp NOT NULL DEFAULT '2000-01-01 00:00:00';"),
        new(83, 4,
            "UPDATE VideoLocal_User SET WatchedCount = 1, LastUpdated = WatchedDate WHERE WatchedDate IS NOT NULL;"),
        new(84, 1, "ALTER TABLE AnimeSeries_User ADD LastEpisodeUpdate timestamp DEFAULT NULL;"),
        new(84, 2, DatabaseFixes.FixWatchDates),
        new(85, 1, "ALTER TABLE AnimeGroup ADD MainAniDBAnimeID INT DEFAULT NULL;"),
        new(86, 1, DropAnimeEpisode_UserColumns),
        new(87, 1, DropAniDB_FileColumns),
        new(87, 2, AlterAniDB_GroupStatus),
        new(87, 3, DropAniDB_CharacterColumns),
        new(87, 4, DropAniDB_Anime_CharacterColumns),
        new(87, 5, DropAniDB_AnimeColumns),
        new(88, 1, DropLanguage),
        new(89, 1, "DROP TABLE AniDB_Anime_Category"),
        new(89, 2, "DROP TABLE AniDB_Anime_Review"),
        new(89, 3, "DROP TABLE AniDB_Category"),
        new(89, 4, "DROP TABLE AniDB_MylistStats"),
        new(89, 5, "DROP TABLE AniDB_Review"),
        new(89, 6, "DROP TABLE CloudAccount"),
        new(89, 7, "DROP TABLE FileFfdshowPreset"),
        new(89, 8, "DROP TABLE CrossRef_AniDB_Trakt"),
        new(89, 9, "DROP TABLE Trakt_Friend"),
        new(89, 10,
            "ALTER TABLE AniDB_Anime RENAME COLUMN DisableExternalLinksFlag TO DisableExternalLinksFlag_old; ALTER TABLE AniDB_Anime ADD DisableExternalLinksFlag INT NOT NULL DEFAULT 0; UPDATE AniDB_Anime SET DisableExternalLinksFlag = DisableExternalLinksFlag_old WHERE DisableExternalLinksFlag_old > 0; ALTER TABLE AniDB_Anime DROP COLUMN DisableExternalLinksFlag_old;"),
        new(89, 11,
            "ALTER TABLE ImportFolder RENAME COLUMN IsWatched TO IsWatched_old; ALTER TABLE ImportFolder ADD IsWatched INT NOT NULL DEFAULT 0; UPDATE ImportFolder SET IsWatched = IsWatched_old WHERE IsWatched_old > 0; ALTER TABLE ImportFolder DROP COLUMN IsWatched_old;"),
        new(89, 12,
            "ALTER TABLE VideoLocal RENAME COLUMN IsVariation TO IsVariation_old; ALTER TABLE VideoLocal ADD IsVariation INT NOT NULL DEFAULT 0; UPDATE VideoLocal SET IsVariation = IsVariation_old WHERE IsVariation_old > 0; ALTER TABLE VideoLocal DROP COLUMN IsVariation_old;"),
        new(89, 13,
            "DROP INDEX UIX2_AniDB_Anime_AnimeID; CREATE UNIQUE INDEX UIX_AniDB_Anime_AnimeID ON AniDB_Anime(AnimeID);"),
        new(89, 14, "DROP INDEX IX_AniDB_File_File_Source;"),
        new(89, 15, "DROP INDEX IX_CrossRef_File_Episode_EpisodeID;"),
        new(89, 16, "DROP INDEX IX_CrossRef_File_Episode_Hash;"),
        new(89, 17, "DROP INDEX UIX2_VideoLocal_Hash; CREATE UNIQUE INDEX UIX_VideoLocal_Hash ON VideoLocal(Hash);"),
        new(89, 18,
            "DROP INDEX UIX2_VideoLocal_User_User_VideoLocalID; CREATE UNIQUE INDEX UIX_VideoLocal_User_User_VideoLocalID ON VideoLocal_User(JMMUserID, VideoLocalID);"),
        new(89, 19, "DROP INDEX \"UIX_VideoLocal_ VideoLocal_Place_ID\";"),
        new(90, 1, "UPDATE AniDB_File SET File_Source = 'Web' WHERE File_Source = 'www'; UPDATE AniDB_File SET File_Source = 'BluRay' WHERE File_Source = 'Blu-ray'; UPDATE AniDB_File SET File_Source = 'LaserDisc' WHERE File_Source = 'LD'; UPDATE AniDB_File SET File_Source = 'Unknown' WHERE File_Source = 'unknown';"),
        new(91, 1, "CREATE INDEX IX_AniDB_Episode_EpisodeType ON AniDB_Episode(EpisodeType);"),
        new(92, 1, "ALTER TABLE VideoLocal ADD DateTimeImported timestamp DEFAULT NULL;"),
        new(92, 2, "UPDATE VideoLocal SET DateTimeImported = DateTimeCreated WHERE EXISTS(SELECT Hash FROM CrossRef_File_Episode xref WHERE xref.Hash = VideoLocal.Hash)"),
        new(93, 1, "ALTER TABLE AniDB_Tag ADD Verified integer NOT NULL DEFAULT 0;"),
        new(93, 2, "ALTER TABLE AniDB_Tag ADD ParentTagID integer DEFAULT NULL;"),
        new(93, 3, "ALTER TABLE AniDB_Tag ADD TagNameOverride text DEFAULT NULL;"),
        new(93, 4, "ALTER TABLE AniDB_Tag ADD LastUpdated timestamp NOT NULL DEFAULT '1970-01-01 00:00:00';"),
        new(93, 5, "ALTER TABLE AniDB_Tag DROP COLUMN Spoiler;"),
        new(93, 6, "ALTER TABLE AniDB_Tag DROP COLUMN LocalSpoiler;"),
        new(93, 7, "ALTER TABLE AniDB_Tag DROP COLUMN TagCount;"),
        new(93, 8, "ALTER TABLE AniDB_Anime_Tag ADD LocalSpoiler integer NOT NULL DEFAULT 0;"),
        new(93, 9, "ALTER TABLE AniDB_Anime_Tag DROP COLUMN Approval;"),
        new(93, 10, DatabaseFixes.FixTagParentIDsAndNameOverrides),
        new(94, 1, "ALTER TABLE AnimeEpisode ADD IsHidden integer NOT NULL DEFAULT 0;"),
        new(94, 2, "ALTER TABLE AnimeSeries_User ADD HiddenUnwatchedEpisodeCount integer NOT NULL DEFAULT 0;"),
        new(95, 1, "UPDATE VideoLocal SET DateTimeImported = DateTimeCreated WHERE EXISTS(SELECT Hash FROM CrossRef_File_Episode xref WHERE xref.Hash = VideoLocal.Hash)"),
        new(96, 1, "CREATE TABLE AniDB_FileUpdate ( AniDB_FileUpdateID INTEGER PRIMARY KEY AUTOINCREMENT, FileSize INTEGER NOT NULL, Hash TEXT NOT NULL, HasResponse INTEGER NOT NULL, UpdatedAt timestamp NOT NULL )"),
        new(96, 2, "CREATE INDEX IX_AniDB_FileUpdate ON AniDB_FileUpdate(FileSize, Hash)"),
        new(96, 3, DatabaseFixes.NoOperation),
        new(97, 1, "ALTER TABLE AniDB_Anime DROP COLUMN DisableExternalLinksFlag;"),
        new(97, 2, "ALTER TABLE AnimeSeries ADD DisableAutoMatchFlags integer NOT NULL DEFAULT 0;"),
        new(97, 3, "ALTER TABLE AniDB_Anime ADD VNDBID INT NULL"),
        new(97, 4, "ALTER TABLE AniDB_Anime ADD BangumiID INT NULL"),
        new(97, 5, "ALTER TABLE AniDB_Anime ADD LianID INT NULL"),
        new(97, 6, "ALTER TABLE AniDB_Anime ADD FunimationID TEXT NULL"),
        new(97, 7, "ALTER TABLE AniDB_Anime ADD HiDiveID TEXT NULL"),
        new(98, 1, "ALTER TABLE AniDB_Anime DROP COLUMN LianID;"),
        new(98, 2, "ALTER TABLE AniDB_Anime DROP COLUMN AnimePlanetID;"),
        new(98, 3, "ALTER TABLE AniDB_Anime DROP COLUMN AnimeNfo;"),
        new(98, 4, "ALTER TABLE AniDB_Anime ADD LainID INT NULL"),
        new(99, 1, DatabaseFixes.FixEpisodeDateTimeUpdated),
        new(100, 1, "ALTER TABLE AnimeSeries ADD HiddenMissingEpisodeCount integer NOT NULL DEFAULT 0;"),
        new(100, 2, "ALTER TABLE AnimeSeries ADD HiddenMissingEpisodeCountGroups integer NOT NULL DEFAULT 0;"),
        new(100, 3, DatabaseFixes.UpdateSeriesWithHiddenEpisodes),
        new(101, 1, "UPDATE AniDB_Anime SET AirDate = NULL, BeginYear = 0 WHERE AirDate = '1970-01-01 00:00:00';"),
        new(102, 1, "ALTER TABLE JMMUser ADD AvatarImageBlob BLOB NULL;"),
        new(102, 2, "ALTER TABLE JMMUser ADD AvatarImageMetadata VARCHAR(128) NULL;"),
        new(103, 1, "ALTER TABLE VideoLocal ADD LastAVDumped timestamp;"),
        new(103, 2, "ALTER TABLE VideoLocal ADD LastAVDumpVersion text;"),
        new(104, 1, DatabaseFixes.FixAnimeSourceLinks),
        new(104, 2, DatabaseFixes.FixOrphanedShokoEpisodes),
        new(105, 1, "CREATE TABLE FilterPreset( FilterPresetID INTEGER PRIMARY KEY AUTOINCREMENT, ParentFilterPresetID int, Name text NOT NULL, FilterType int NOT NULL, Locked int NOT NULL, Hidden int NOT NULL, ApplyAtSeriesLevel int NOT NULL, Expression text, SortingExpression text ); "),
        new(105, 2, "CREATE INDEX IX_FilterPreset_ParentFilterPresetID ON FilterPreset(ParentFilterPresetID); CREATE INDEX IX_FilterPreset_Name ON FilterPreset(Name); CREATE INDEX IX_FilterPreset_FilterType ON FilterPreset(FilterType); CREATE INDEX IX_FilterPreset_LockedHidden ON FilterPreset(Locked, Hidden);"),
        new(105, 3, "DELETE FROM GroupFilter WHERE FilterType = 2; DELETE FROM GroupFilter WHERE FilterType = 16;"),
        new(105, 4, DatabaseFixes.MigrateGroupFilterToFilterPreset),
        new(105, 5, DatabaseFixes.DropGroupFilter),
        new(106, 1, "ALTER TABLE AnimeGroup DROP COLUMN SortName;"),
        new(107, 1, "ALTER TABLE AnimeEpisode DROP COLUMN PlexContractVersion;ALTER TABLE AnimeEpisode DROP COLUMN PlexContractBlob;ALTER TABLE AnimeEpisode DROP COLUMN PlexContractSize;ALTER TABLE AnimeGroup_User DROP COLUMN PlexContractVersion;ALTER TABLE AnimeGroup_User DROP COLUMN PlexContractBlob;ALTER TABLE AnimeGroup_User DROP COLUMN PlexContractSize;ALTER TABLE AnimeSeries_User DROP COLUMN PlexContractVersion;ALTER TABLE AnimeSeries_User DROP COLUMN PlexContractBlob;ALTER TABLE AnimeSeries_User DROP COLUMN PlexContractSize;"),
        new(108, 1, "CREATE INDEX IX_CommandRequest_CommandType ON CommandRequest(CommandType); CREATE INDEX IX_CommandRequest_Priority_Date ON CommandRequest(Priority, DateTimeUpdated);"),
        new(109, 1, "DROP TABLE CommandRequest"),
        new(110, 1, "ALTER TABLE AnimeEpisode ADD EpisodeNameOverride text"),
        new(111, 1, "DELETE FROM FilterPreset WHERE FilterType IN (16, 24, 32, 40, 64, 72)"),
        new(112, 1, "ALTER TABLE AniDB_Anime DROP COLUMN ContractVersion;ALTER TABLE AniDB_Anime DROP COLUMN ContractBlob;ALTER TABLE AniDB_Anime DROP COLUMN ContractSize;"),
        new(112, 2, "ALTER TABLE AnimeSeries DROP COLUMN ContractVersion;ALTER TABLE AnimeSeries DROP COLUMN ContractBlob;ALTER TABLE AnimeSeries DROP COLUMN ContractSize;"),
        new(112, 3, "ALTER TABLE AnimeGroup DROP COLUMN ContractVersion;ALTER TABLE AnimeGroup DROP COLUMN ContractBlob;ALTER TABLE AnimeGroup DROP COLUMN ContractSize;"),
        new(113, 1, "ALTER TABLE VideoLocal DROP COLUMN MediaSize;"),
        new(114, 1, "CREATE TABLE AniDB_NotifyQueue( AniDB_NotifyQueueID INTEGER PRIMARY KEY AUTOINCREMENT, Type int NOT NULL, ID int NOT NULL, AddedAt timestamp NOT NULL ); "),
        new(114, 2, "CREATE TABLE AniDB_Message( AniDB_MessageID INTEGER PRIMARY KEY AUTOINCREMENT, MessageID int NOT NULL, FromUserID int NOT NULL, FromUserName text NOT NULL, SentAt timestamp NOT NULL, FetchedAt timestamp NOT NULL, Type int NOT NULL, Title text NOT NULL, Body text NOT NULL, Flags int NOT NULL DEFAULT 0 ); "),
        new(115, 1, "CREATE TABLE CrossRef_AniDB_TMDB_Episode ( CrossRef_AniDB_TMDB_EpisodeID INTEGER PRIMARY KEY AUTOINCREMENT, AnidbAnimeID INTEGER NOT NULL, AnidbEpisodeID INTEGER NOT NULL, TmdbShowID INTEGER NOT NULL, TmdbEpisodeID INTEGER NOT NULL, 'Ordering' INTEGER NOT NULL, MatchRating INTEGER NOT NULL);"),
        new(115, 2, "CREATE TABLE CrossRef_AniDB_TMDB_Movie ( CrossRef_AniDB_TMDB_MovieID INTEGER PRIMARY KEY AUTOINCREMENT, AnidbAnimeID INTEGER NOT NULL, AnidbEpisodeID INTEGER NULL, TmdbMovieID INTEGER NOT NULL, Source INTEGER NOT NULL);"),
        new(115, 3, "CREATE TABLE CrossRef_AniDB_TMDB_Show ( CrossRef_AniDB_TMDB_ShowID INTEGER PRIMARY KEY AUTOINCREMENT, AnidbAnimeID INTEGER NOT NULL, TmdbShowID INTEGER NOT NULL, Source INTEGER NOT NULL);"),
        new(115, 4, "CREATE TABLE TMDB_Image ( TMDB_ImageID INTEGER PRIMARY KEY AUTOINCREMENT, TmdbMovieID INTEGER NULL, TmdbEpisodeID INTEGER NULL, TmdbSeasonID INTEGER NULL, TmdbShowID INTEGER NULL, TmdbCollectionID INTEGER NULL, TmdbNetworkID INTEGER NULL, TmdbCompanyID INTEGER NULL, TmdbPersonID INTEGER NULL, ForeignType INTEGER NOT NULL, ImageType INTEGER NOT NULL, IsEnabled INTEGER NOT NULL, Width INTEGER NOT NULL, Height INTEGER NOT NULL, Language TEXT NOT NULL, RemoteFileName TEXT NOT NULL, UserRating REAL NOT NULL, UserVotes INTEGER NOT NULL );"),
        new(115, 5, "CREATE TABLE AniDB_Anime_PreferredImage ( AniDB_Anime_PreferredImageID INTEGER PRIMARY KEY AUTOINCREMENT, AnidbAnimeID INTEGER NOT NULL, ImageID INTEGER NOT NULL, ImageType INTEGER NOT NULL, ImageSource INTEGER NOT NULL );"),
        new(115, 6, "CREATE TABLE TMDB_Title ( TMDB_TitleID INTEGER PRIMARY KEY AUTOINCREMENT, ParentID INTEGER NOT NULL, ParentType INTEGER NOT NULL, LanguageCode TEXT NOT NULL, CountryCode TEXT NOT NULL, Value TEXT NOT NULL );"),
        new(115, 7, "CREATE TABLE TMDB_Overview ( TMDB_OverviewID INTEGER PRIMARY KEY AUTOINCREMENT, ParentID INTEGER NOT NULL, ParentType INTEGER NOT NULL, LanguageCode TEXT NOT NULL, CountryCode TEXT NOT NULL, Value TEXT NOT NULL );"),
        new(115, 8, "CREATE TABLE TMDB_Company ( TMDB_CompanyID INTEGER PRIMARY KEY AUTOINCREMENT, TmdbCompanyID INTEGER NOT NULL, Name TEXT NOT NULL, CountryOfOrigin TEXT NOT NULL );"),
        new(115, 9, "CREATE TABLE TMDB_Network ( TMDB_NetworkID INTEGER PRIMARY KEY AUTOINCREMENT, TmdbNetworkID INTEGER NOT NULL, Name TEXT NOT NULL, CountryOfOrigin TEXT NOT NULL );"),
        new(115, 10, "CREATE TABLE TMDB_Person ( TMDB_PersonID INTEGER PRIMARY KEY AUTOINCREMENT, TmdbPersonID INTEGER NOT NULL, EnglishName TEXT NOT NULL, EnglishBiography TEXT NOT NULL, Aliases TEXT NOT NULL, Gender INTEGER NOT NULL, IsRestricted INTEGER NOT NULL, BirthDay DATE NULL, DeathDay DATE NULL, PlaceOfBirth TEXT NULL, CreatedAt DATETIME NOT NULL, LastUpdatedAt DATETIME NOT NULL );"),
        new(115, 11, "CREATE TABLE TMDB_Movie ( TMDB_MovieID INTEGER PRIMARY KEY AUTOINCREMENT, TmdbMovieID INTEGER NOT NULL, TmdbCollectionID INTEGER NULL, EnglishTitle TEXT NOT NULL, EnglishOverview TEXT NOT NULL, OriginalTitle TEXT NOT NULL, OriginalLanguageCode TEXT NOT NULL, IsRestricted INTEGER NOT NULL, IsVideo INTEGER NOT NULL, Genres TEXT NOT NULL, ContentRatings TEXT NOT NULL, Runtime TEXT NULL, UserRating REAL NOT NULL, UserVotes INTEGER NOT NULL, ReleasedAt DATE NULL, CreatedAt DATETIME NOT NULL, LastUpdatedAt DATETIME NOT NULL );"),
        new(115, 12, "CREATE TABLE TMDB_Movie_Cast ( TMDB_Movie_CastID INTEGER PRIMARY KEY AUTOINCREMENT, TmdbMovieID INT NOT NULL, TmdbPersonID INT NOT NULL, TmdbCreditID TEXT NOT NULL, CharacterName TEXT NOT NULL, Ordering INTEGER NOT NULL );"),
        new(115, 13, "CREATE TABLE TMDB_Company_Entity ( TMDB_Company_EntityID INTEGER PRIMARY KEY AUTOINCREMENT, TmdbCompanyID INTEGER NOT NULL, TmdbEntityType INTEGER NOT NULL, TmdbEntityID INTEGER NOT NULL, 'Ordering' INTEGER NOT NULL, ReleasedAt DATE NULL );"),
        new(115, 14, "CREATE TABLE TMDB_Movie_Crew ( TMDB_Movie_CrewID INTEGER PRIMARY KEY AUTOINCREMENT, TmdbMovieID INTEGER NOT NULL, TmdbPersonID INTEGER NOT NULL, TmdbCreditID TEXT NOT NULL, Job TEXT NOT NULL, Department TEXT NOT NULL );"),
        new(115, 15, "CREATE TABLE TMDB_Show ( TMDB_ShowID INTEGER PRIMARY KEY AUTOINCREMENT, TmdbShowID INTEGER NOT NULL, EnglishTitle TEXT NOT NULL, EnglishOverview TEXT NOT NULL, OriginalTitle TEXT NOT NULL, OriginalLanguageCode TEXT NOT NULL, IsRestricted INTEGER NOT NULL, Genres TEXT NOT NULL, ContentRatings TEXT NOT NULL, EpisodeCount INTEGER NOT NULL, SeasonCount INTEGER NOT NULL, AlternateOrderingCount INTEGER NOT NULL, UserRating REAL NOT NULL, UserVotes INTEGER NOT NULL, FirstAiredAt DATE, LastAiredAt DATE NULL, CreatedAt DATETIME NOT NULL, LastUpdatedAt DATETIME NOT NULL );"),
        new(115, 16, "CREATE TABLE Tmdb_Show_Network ( TMDB_Show_NetworkID INTEGER PRIMARY KEY AUTOINCREMENT, TmdbShowID INTEGER NOT NULL, TmdbNetworkID INTEGER NOT NULL, Ordering INTEGER NOT NULL );"),
        new(115, 17, "CREATE TABLE TMDB_Season ( TMDB_SeasonID INTEGER PRIMARY KEY AUTOINCREMENT, TmdbShowID INTEGER NOT NULL, TmdbSeasonID INTEGER NOT NULL, EnglishTitle TEXT NOT NULL, EnglishOverview TEXT NOT NULL, EpisodeCount INTEGER NOT NULL, SeasonNumber INTEGER NOT NULL, CreatedAt DATETIME NOT NULL, LastUpdatedAt DATETIME NOT NULL );"),
        new(115, 18, "CREATE TABLE TMDB_Episode ( TMDB_EpisodeID INTEGER PRIMARY KEY AUTOINCREMENT, TmdbShowID INTEGER NOT NULL, TmdbSeasonID INTEGER NOT NULL, TmdbEpisodeID INTEGER NOT NULL, EnglishTitle TEXT NOT NULL, EnglishOverview TEXT NOT NULL, SeasonNumber INTEGER NOT NULL, EpisodeNumber INTEGER NOT NULL, Runtime TEXT NULL, UserRating REAL NOT NULL, UserVotes INTEGER NOT NULL, AiredAt DATE NULL, CreatedAt DATETIME NOT NULL, LastUpdatedAt DATETIME NOT NULL );"),
        new(115, 19, "CREATE TABLE TMDB_Episode_Cast ( TMDB_Episode_CastID INTEGER PRIMARY KEY AUTOINCREMENT, TmdbShowID INTEGER NOT NULL, TmdbSeasonID INTEGER NOT NULL, TmdbEpisodeID INTEGER NOT NULL, TmdbPersonID INTEGER NOT NULL, TmdbCreditID TEXT NOT NULL, CharacterName TEXT NOT NULL, IsGuestRole INTEGER NOT NULL, Ordering INTEGER NOT NULL );"),
        new(115, 20, "CREATE TABLE TMDB_Episode_Crew ( TMDB_Episode_CrewID INTEGER PRIMARY KEY AUTOINCREMENT, TmdbShowID INTEGER NOT NULL, TmdbSeasonID INTEGER NOT NULL, TmdbEpisodeID INTEGER NOT NULL, TmdbPersonID INTEGER NOT NULL, TmdbCreditID TEXT NOT NULL, Job TEXT NOT NULL, Department TEXT NOT NULL );"),
        new(115, 21, "CREATE TABLE TMDB_AlternateOrdering ( TMDB_AlternateOrderingID INTEGER PRIMARY KEY AUTOINCREMENT, TmdbShowID INTEGER NOT NULL, TmdbNetworkID INTEGER NULL, TmdbEpisodeGroupCollectionID TEXT NOT NULL, EnglishTitle TEXT NOT NULL, EnglishOverview TEXT NOT NULL, EpisodeCount INTEGER NOT NULL, SeasonCount INTEGER NOT NULL, Type INTEGER NOT NULL, CreatedAt DATETIME NOT NULL, LastUpdatedAt DATETIME NOT NULL );"),
        new(115, 22, "CREATE TABLE TMDB_AlternateOrdering_Season ( TMDB_AlternateOrdering_SeasonID INTEGER PRIMARY KEY AUTOINCREMENT, TmdbShowID INTEGER NOT NULL, TmdbEpisodeGroupCollectionID TEXT NOT NULL, TmdbEpisodeGroupID TEXT NOT NULL, EnglishTitle TEXT NOT NULL, SeasonNumber INTEGER NOT NULL, EpisodeCount INTEGER NOT NULL, IsLocked INTEGER NOT NULL, CreatedAt DATETIME NOT NULL, LastUpdatedAt DATETIME NOT NULL );"),
        new(115, 23, "CREATE TABLE TMDB_AlternateOrdering_Episode ( TMDB_AlternateOrdering_EpisodeID INTEGER PRIMARY KEY AUTOINCREMENT, TmdbShowID INTEGER NOT NULL, TmdbEpisodeGroupCollectionID TEXT NOT NULL, TmdbEpisodeGroupID TEXT NOT NULL, TmdbEpisodeID INTEGER NOT NULL, SeasonNumber INTEGER NOT NULL, EpisodeNumber INTEGER NOT NULL, CreatedAt DATETIME NOT NULL, LastUpdatedAt DATETIME NOT NULL );"),
        new(115, 24, "CREATE TABLE TMDB_Collection ( TMDB_CollectionID INTEGER PRIMARY KEY AUTOINCREMENT, TmdbCollectionID INTEGER NOT NULL, EnglishTitle TEXT NOT NULL, EnglishOverview TEXT NOT NULL, MovieCount INTEGER NOT NULL, CreatedAt DATETIME NOT NULL, LastUpdatedAt DATETIME NOT NULL );"),
        new(115, 25, "CREATE TABLE TMDB_Collection_Movie ( TMDB_Collection_MovieID INTEGER PRIMARY KEY AUTOINCREMENT, TmdbCollectionID INTEGER NOT NULL, TmdbMovieID INTEGER NOT NULL, Ordering INTEGER NOT NULL );"),
        new(115, 26, "INSERT INTO CrossRef_AniDB_TMDB_Movie (AnidbAnimeID, TmdbMovieID, Source) SELECT AnimeID, CAST(CrossRefID AS INTEGER), CrossRefSource FROM CrossRef_AniDB_Other WHERE CrossRefType = 1;"),
        new(115, 27, "DROP TABLE CrossRef_AniDB_Other;"),
        new(115, 28, "DROP TABLE MovieDB_Fanart;"),
        new(115, 29, "DROP TABLE MovieDB_Movie;"),
        new(115, 30, "DROP TABLE MovieDB_Poster;"),
        new(115, 31, "DROP TABLE AniDB_Anime_DefaultImage;"),
        new(115, 32, "CREATE TABLE AniDB_Episode_PreferredImage ( AniDB_Episode_PreferredImageID INTEGER PRIMARY KEY AUTOINCREMENT, AnidbAnimeID INTEGER NOT NULL, AnidbEpisodeID INTEGER NOT NULL, ImageID INTEGER NOT NULL, ImageType INTEGER NOT NULL, ImageSource INTEGER NOT NULL );"),
        new(115, 33, DatabaseFixes.CleanupAfterAddingTMDB),
        new(115, 34, "UPDATE FilterPreset SET Expression = REPLACE(Expression, 'HasTMDbLinkExpression', 'HasTmdbLinkExpression');"),
        new(115, 35, "UPDATE TMDB_Image SET IsEnabled = 1;"),
        new(116, 1, DatabaseFixes.NoOperation),
        new(116, 2, DatabaseFixes.NoOperation),
        new(116, 3, DatabaseFixes.NoOperation),
        new(117, 1, "UPDATE CrossRef_AniDB_TMDB_Episode SET MatchRating = CASE MatchRating WHEN 'UserVerified' THEN 1 WHEN 'DateAndTitleMatches' THEN 2 WHEN 'DateMatches' THEN 3 WHEN 'TitleMatches' THEN 4 WHEN 'FirstAvailable' THEN 5 WHEN 'SarahJessicaParker' THEN 6 ELSE MatchRating END;"),
        new(117, 2, "UPDATE CrossRef_AniDB_TMDB_Show SET Source = CASE Source WHEN 'Automatic' THEN 0 WHEN 'User' THEN 2 ELSE Source END;"),
        new(117, 3, "ALTER TABLE TMDB_Show ADD COLUMN TvdbShowID INTEGER NULL DEFAULT NULL;"),
        new(117, 4, "ALTER TABLE TMDB_Episode ADD COLUMN TvdbEpisodeID INTEGER NULL DEFAULT NULL;"),
        new(117, 5, "ALTER TABLE TMDB_Movie ADD COLUMN ImdbMovieID INTEGER NULL DEFAULT NULL;"),
        new(117, 6, "ALTER TABLE TMDB_Movie DROP COLUMN ImdbMovieID;"),
        new(117, 7, "ALTER TABLE TMDB_Movie ADD COLUMN ImdbMovieID TEXT NULL DEFAULT NULL;"),
        new(117, 8, "CREATE INDEX IX_TMDB_Overview ON TMDB_Overview(ParentType, ParentID)"),
        new(117, 9, "CREATE INDEX IX_TMDB_Title ON TMDB_Title(ParentType, ParentID)"),
        new(117, 10, "CREATE UNIQUE INDEX UIX_TMDB_Episode_TmdbEpisodeID ON TMDB_Episode(TmdbEpisodeID)"),
        new(117, 11, "CREATE UNIQUE INDEX UIX_TMDB_Show_TmdbShowID ON TMDB_Show(TmdbShowID)"),
        new(118, 1, "UPDATE CrossRef_AniDB_TMDB_Movie SET AnidbEpisodeID = (SELECT EpisodeID FROM AniDB_Episode WHERE AniDB_Episode.AnimeID = CrossRef_AniDB_TMDB_Movie.AnidbAnimeID ORDER BY EpisodeType, EpisodeNumber LIMIT 1) WHERE AnidbEpisodeID IS NULL AND EXISTS (SELECT 1 FROM AniDB_Episode WHERE AniDB_Episode.AnimeID = CrossRef_AniDB_TMDB_Movie.AnidbAnimeID);"),
        new(118, 2, "DELETE FROM CrossRef_AniDB_TMDB_Movie WHERE AnidbEpisodeID IS NULL;"),
        new(118, 3, "ALTER TABLE CrossRef_AniDB_TMDB_Movie RENAME AnidbEpisodeID TO AniDBEpisodeID_OLD; ALTER TABLE CrossRef_AniDB_TMDB_Movie ADD COLUMN AnidbEpisodeID INT NOT NULL DEFAULT 0; UPDATE CrossRef_AniDB_TMDB_Movie SET AnidbEpisodeID = AniDBEpisodeID_OLD WHERE AniDBEpisodeID_OLD > 0; ALTER TABLE CrossRef_AniDB_TMDB_Movie DROP COLUMN AniDBEpisodeID_OLD;"),
        new(119, 1, "ALTER TABLE TMDB_Movie ADD COLUMN PosterPath TEXT NULL DEFAULT NULL;"),
        new(119, 2, "ALTER TABLE TMDB_Movie ADD COLUMN BackdropPath TEXT NULL DEFAULT NULL;"),
        new(119, 3, "ALTER TABLE TMDB_Show ADD COLUMN PosterPath TEXT NULL DEFAULT NULL;"),
        new(119, 4, "ALTER TABLE TMDB_Show ADD COLUMN BackdropPath TEXT NULL DEFAULT NULL;"),
        new(120, 1, "UPDATE FilterPreset SET Expression = REPLACE(Expression, 'MissingTMDbLinkExpression', 'MissingTmdbLinkExpression');"),
        new(121, 1, "CREATE TABLE AniDB_Creator ( AniDB_CreatorID INTEGER PRIMARY KEY AUTOINCREMENT, CreatorID INTEGER NOT NULL, Name TEXT NOT NULL, OriginalName TEXT, Type INTEGER NOT NULL DEFAULT 0, ImagePath TEXT, EnglishHomepageUrl TEXT, JapaneseHomepageUrl TEXT, EnglishWikiUrl TEXT, JapaneseWikiUrl TEXT, LastUpdatedAt DATETIME NOT NULL DEFAULT '2000-01-01 00:00:00' );"),
        new(121, 2, "CREATE TABLE AniDB_Character_Creator ( AniDB_Character_CreatorID INTEGER PRIMARY KEY AUTOINCREMENT, CharacterID INTEGER NOT NULL, CreatorID INTEGER NOT NULL );"),
        new(121, 3, "CREATE UNIQUE INDEX UIX_AniDB_Creator_CreatorID ON AniDB_Creator(CreatorID);"),
        new(121, 4, "CREATE INDEX UIX_AniDB_Character_Creator_CreatorID ON AniDB_Character_Creator(CreatorID);"),
        new(121, 5, "CREATE INDEX UIX_AniDB_Character_Creator_CharacterID ON AniDB_Character_Creator(CharacterID);"),
        new(121, 6, "CREATE UNIQUE INDEX UIX_AniDB_Character_Creator_CharacterID_CreatorID ON AniDB_Character_Creator(CharacterID, CreatorID);"),
        new(121, 7, "INSERT INTO AniDB_Creator (CreatorID, Name, ImagePath) SELECT SeiyuuID, SeiyuuName, PicName FROM AniDB_Seiyuu;"),
        new(121, 8, "INSERT INTO AniDB_Character_Creator (CharacterID, CreatorID) SELECT CharID, SeiyuuID FROM AniDB_Character_Seiyuu;"),
        new(121, 9, "DROP TABLE AniDB_Seiyuu;"),
        new(121, 10, "DROP TABLE AniDB_Character_Seiyuu;"),
        new(122, 1, "ALTER TABLE TMDB_Show ADD COLUMN PreferredAlternateOrderingID TEXT NULL DEFAULT NULL;"),
        new(123, 1, "DROP TABLE TvDB_Episode;"),
        new(123, 2, "DROP TABLE TvDB_Series;"),
        new(123, 3, "DROP TABLE TvDB_ImageFanart;"),
        new(123, 4, "DROP TABLE TvDB_ImagePoster;"),
        new(123, 5, "DROP TABLE TvDB_ImageWideBanner;"),
        new(123, 6, "DROP TABLE CrossRef_AniDB_TvDB;"),
        new(123, 7, "DROP TABLE CrossRef_AniDB_TvDB_Episode;"),
        new(123, 8, "DROP TABLE CrossRef_AniDB_TvDB_Episode_Override;"),
        new(123, 9, "ALTER TABLE Trakt_Show DROP COLUMN TvDB_ID;"),
        new(123, 10, "ALTER TABLE Trakt_Show ADD COLUMN TmdbShowID INTEGER NULL;"),
        new(123, 11, DatabaseFixes.CleanupAfterRemovingTvDB),
        new(123, 12, DatabaseFixes.ClearQuartzQueue),
        new(124, 1, DatabaseFixes.RepairMissingTMDBPersons),
        new(125, 1, "ALTER TABLE TMDB_Movie ADD COLUMN Keywords TEXT NULL DEFAULT NULL;"),
        new(125, 2, "ALTER TABLE TMDB_Movie ADD COLUMN ProductionCountries TEXT NULL DEFAULT NULL;"),
        new(125, 3, "ALTER TABLE TMDB_Show ADD COLUMN Keywords TEXT NULL DEFAULT NULL;"),
        new(125, 4, "ALTER TABLE TMDB_Show ADD COLUMN ProductionCountries TEXT NULL DEFAULT NULL;"),
        new(126, 1, "CREATE INDEX IX_AniDB_Anime_Relation_RelatedAnimeID on AniDB_Anime_Relation(RelatedAnimeID);"),
        new(127, 1, "CREATE INDEX IX_TMDB_Episode_TmdbSeasonID ON TMDB_Episode(TmdbSeasonID);"),
        new(127, 2, "CREATE INDEX IX_TMDB_Episode_TmdbShowID ON TMDB_Episode(TmdbShowID);"),
        new(128, 1, "ALTER TABLE TMDB_Episode ADD COLUMN IsHidden INTEGER NOT NULL DEFAULT 0;"),
        new(128, 2, "ALTER TABLE TMDB_Season ADD COLUMN HiddenEpisodeCount INTEGER NOT NULL DEFAULT 0;"),
        new(128, 3, "ALTER TABLE TMDB_Show ADD COLUMN HiddenEpisodeCount INTEGER NOT NULL DEFAULT 0;"),
        new(128, 4, "ALTER TABLE TMDB_AlternateOrdering_Season ADD COLUMN HiddenEpisodeCount INTEGER NOT NULL DEFAULT 0;"),
        new(128, 5, "ALTER TABLE TMDB_AlternateOrdering ADD COLUMN HiddenEpisodeCount INTEGER NOT NULL DEFAULT 0;"),
        new(129, 01, "CREATE INDEX IX_CrossRef_AniDB_TMDB_Episode_AnidbAnimeID ON CrossRef_AniDB_TMDB_Episode(AnidbAnimeID);"),
        new(129, 02, "CREATE INDEX IX_CrossRef_AniDB_TMDB_Episode_AnidbAnimeID_TmdbShowID ON CrossRef_AniDB_TMDB_Episode(AnidbAnimeID, TmdbShowID);"),
        new(129, 03, "CREATE INDEX IX_CrossRef_AniDB_TMDB_Episode_AnidbEpisodeID ON CrossRef_AniDB_TMDB_Episode(AnidbEpisodeID);"),
        new(129, 04, "CREATE INDEX IX_CrossRef_AniDB_TMDB_Episode_AnidbEpisodeID_TmdbEpisodeID ON CrossRef_AniDB_TMDB_Episode(AnidbEpisodeID, TmdbEpisodeID);"),
        new(129, 05, "CREATE INDEX IX_CrossRef_AniDB_TMDB_Episode_TmdbEpisodeID ON CrossRef_AniDB_TMDB_Episode(TmdbEpisodeID);"),
        new(129, 06, "CREATE INDEX IX_CrossRef_AniDB_TMDB_Episode_TmdbShowID ON CrossRef_AniDB_TMDB_Episode(TmdbShowID);"),
        new(129, 07, "CREATE INDEX IX_CrossRef_AniDB_TMDB_Movie_AnidbAnimeID ON CrossRef_AniDB_TMDB_Movie(AnidbAnimeID);"),
        new(129, 08, "CREATE INDEX IX_CrossRef_AniDB_TMDB_Movie_AnidbEpisodeID ON CrossRef_AniDB_TMDB_Movie(AnidbEpisodeID);"),
        new(129, 09, "CREATE INDEX IX_CrossRef_AniDB_TMDB_Movie_AnidbEpisodeID_TmdbMovieID ON CrossRef_AniDB_TMDB_Movie(AnidbEpisodeID, TmdbMovieID);"),
        new(129, 10, "CREATE INDEX IX_CrossRef_AniDB_TMDB_Movie_TmdbMovieID ON CrossRef_AniDB_TMDB_Movie(TmdbMovieID);"),
        new(129, 11, "CREATE INDEX IX_CrossRef_AniDB_TMDB_Show_AnidbAnimeID ON CrossRef_AniDB_TMDB_Show(AnidbAnimeID);"),
        new(129, 12, "CREATE INDEX IX_CrossRef_AniDB_TMDB_Show_AnidbAnimeID_TmdbShowID ON CrossRef_AniDB_TMDB_Show(AnidbAnimeID, TmdbShowID);"),
        new(129, 13, "CREATE INDEX IX_CrossRef_AniDB_TMDB_Show_TmdbShowID ON CrossRef_AniDB_TMDB_Show(TmdbShowID);"),
        new(129, 14, "CREATE UNIQUE INDEX UIX_TMDB_AlternateOrdering_Season_TmdbEpisodeGroupID ON TMDB_AlternateOrdering_Season(TmdbEpisodeGroupID);"),
        new(129, 15, "CREATE INDEX IX_TMDB_AlternateOrdering_Season_TmdbEpisodeGroupCollectionID ON TMDB_AlternateOrdering_Season(TmdbEpisodeGroupCollectionID);"),
        new(129, 16, "CREATE INDEX IX_TMDB_AlternateOrdering_Season_TmdbShowID ON TMDB_AlternateOrdering_Season(TmdbShowID);"),
        new(129, 17, "CREATE UNIQUE INDEX UIX_TMDB_AlternateOrdering_TmdbEpisodeGroupCollectionID ON TMDB_AlternateOrdering(TmdbEpisodeGroupCollectionID);"),
        new(129, 18, "CREATE INDEX IX_TMDB_AlternateOrdering_TmdbEpisodeGroupCollectionID_TmdbShowID ON TMDB_AlternateOrdering(TmdbEpisodeGroupCollectionID, TmdbShowID);"),
        new(129, 19, "CREATE INDEX IX_TMDB_AlternateOrdering_TmdbShowID ON TMDB_AlternateOrdering(TmdbShowID);"),
        new(129, 20, "CREATE UNIQUE INDEX UIX_TMDB_Collection_TmdbCollectionID ON TMDB_Collection(TmdbCollectionID);"),
        new(129, 21, "CREATE INDEX IX_TMDB_Collection_Movie_TmdbCollectionID ON TMDB_Collection_Movie(TmdbCollectionID);"),
        new(129, 22, "CREATE INDEX IX_TMDB_Collection_Movie_TmdbMovieID ON TMDB_Collection_Movie(TmdbMovieID);"),
        new(129, 23, "CREATE INDEX IX_TMDB_Company_Entity_TmdbCompanyID ON TMDB_Company_Entity(TmdbCompanyID);"),
        new(129, 24, "CREATE INDEX IX_TMDB_Company_Entity_TmdbEntityType_TmdbEntityID ON TMDB_Company_Entity(TmdbEntityType, TmdbEntityID);"),
        new(129, 25, "CREATE INDEX IX_TMDB_Company_TmdbCompanyID ON TMDB_Company(TmdbCompanyID);"),
        new(129, 26, "CREATE INDEX IX_TMDB_Episode_Cast_TmdbEpisodeID ON TMDB_Episode_Cast(TmdbEpisodeID);"),
        new(129, 27, "CREATE INDEX IX_TMDB_Episode_Cast_TmdbPersonID ON TMDB_Episode_Cast(TmdbPersonID);"),
        new(129, 28, "CREATE INDEX IX_TMDB_Episode_Cast_TmdbSeasonID ON TMDB_Episode_Cast(TmdbSeasonID);"),
        new(129, 29, "CREATE INDEX IX_TMDB_Episode_Cast_TmdbShowID ON TMDB_Episode_Cast(TmdbShowID);"),
        new(129, 30, "CREATE INDEX IX_TMDB_Episode_Crew_TmdbEpisodeID ON TMDB_Episode_Crew(TmdbEpisodeID);"),
        new(129, 31, "CREATE INDEX IX_TMDB_Episode_Crew_TmdbPersonID ON TMDB_Episode_Crew(TmdbPersonID);"),
        new(129, 32, "CREATE INDEX IX_TMDB_Episode_Crew_TmdbSeasonID ON TMDB_Episode_Crew(TmdbSeasonID);"),
        new(129, 33, "CREATE INDEX IX_TMDB_Episode_Crew_TmdbShowID ON TMDB_Episode_Crew(TmdbShowID);"),
        new(129, 34, "CREATE INDEX IX_TMDB_Movie_Cast_TmdbMovieID ON TMDB_Movie_Cast(TmdbMovieID);"),
        new(129, 35, "CREATE INDEX IX_TMDB_Movie_Cast_TmdbPersonID ON TMDB_Movie_Cast(TmdbPersonID);"),
        new(129, 36, "CREATE INDEX IX_TMDB_Movie_Crew_TmdbMovieID ON TMDB_Movie_Crew(TmdbMovieID);"),
        new(129, 37, "CREATE INDEX IX_TMDB_Movie_Crew_TmdbPersonID ON TMDB_Movie_Crew(TmdbPersonID);"),
        new(129, 38, "CREATE UNIQUE INDEX UIX_TMDB_Movie_TmdbMovieID ON TMDB_Movie(TmdbMovieID);"),
        new(129, 39, "CREATE INDEX IX_TMDB_Movie_TmdbCollectionID ON TMDB_Movie(TmdbCollectionID);"),
        new(129, 40, "CREATE INDEX IX_TMDB_Person_TmdbPersonID ON TMDB_Person(TmdbPersonID);"),
        new(129, 41, "CREATE UNIQUE INDEX UIX_TMDB_Season_TmdbSeasonID ON TMDB_Season(TmdbSeasonID);"),
        new(129, 42, "CREATE INDEX IX_TMDB_Season_TmdbShowID ON TMDB_Season(TmdbShowID);"),
        new(129, 43, "CREATE UNIQUE INDEX UIX_TMDB_Network_TmdbNetworkID ON TMDB_Network(TmdbNetworkID);"),
        new(130, 01, "DROP TABLE IF EXISTS AnimeStaff"),
        new(130, 02, "DROP TABLE IF EXISTS CrossRef_Anime_Staff"),
        new(130, 03, "DROP TABLE IF EXISTS AniDB_Character"),
        new(130, 04, "DROP TABLE IF EXISTS AniDB_Anime_Staff"),
        new(130, 05, "DROP TABLE IF EXISTS AniDB_Anime_Character"),
        new(130, 06, "DROP TABLE IF EXISTS AniDB_Character_Creator"),
        new(130, 07, "CREATE TABLE AniDB_Character (AniDB_CharacterID INTEGER PRIMARY KEY AUTOINCREMENT, CharacterID INTEGER NOT NULL, Name TEXT NOT NULL, OriginalName TEXT NOT NULL, Description TEXT NOT NULL, ImagePath TEXT NOT NULL, Gender INTEGER NOT NULL);"),
        new(130, 08, "CREATE TABLE AniDB_Anime_Staff (AniDB_Anime_StaffID INTEGER PRIMARY KEY AUTOINCREMENT, AnimeID INTEGER NOT NULL, CreatorID INTEGER NOT NULL, Role TEXT NOT NULL, RoleType INTEGER NOT NULL, Ordering INTEGER NOT NULL);"),
        new(130, 09, "CREATE TABLE AniDB_Anime_Character (AniDB_Anime_CharacterID INTEGER PRIMARY KEY AUTOINCREMENT, AnimeID INTEGER NOT NULL, CharacterID INTEGER NOT NULL, Appearance TEXT NOT NULL, AppearanceType INTEGER NOT NULL, Ordering INTEGER NOT NULL);"),
        new(130, 10, "CREATE TABLE AniDB_Anime_Character_Creator (AniDB_Anime_Character_CreatorID INTEGER PRIMARY KEY AUTOINCREMENT, AnimeID INTEGER NOT NULL, CharacterID INTEGER NOT NULL, CreatorID INTEGER NOT NULL, Ordering INTEGER NOT NULL);"),
        new(130, 11, "CREATE INDEX IX_AniDB_Anime_Staff_CreatorID ON AniDB_Anime_Staff(CreatorID);"),
        new(130, 12, DatabaseFixes.NoOperation),
        new(131, 01, "ALTER TABLE AniDB_Character ADD COLUMN Type INTEGER NOT NULL DEFAULT 0;"),
        new(131, 02, "ALTER TABLE AniDB_Character ADD COLUMN LastUpdated DATETIME NOT NULL DEFAULT '1970-01-01 00:00:00';"),
        new(131, 03, DatabaseFixes.RecreateAnimeCharactersAndCreators),
        new(132, 01, "CREATE TABLE TMDB_Image_Entity (TMDB_Image_EntityID INTEGER PRIMARY KEY AUTOINCREMENT, TmdbEntityID INTEGER NULL, TmdbEntityType INTEGER NOT NULL, ImageType INTEGER NOT NULL, RemoteFileName TEXT NOT NULL, Ordering INTEGER NOT NULL, ReleasedAt DATE NULL);"),
        new(132, 02, "ALTER TABLE TMDB_Image DROP COLUMN TmdbMovieID;"),
        new(132, 03, "ALTER TABLE TMDB_Image DROP COLUMN TmdbEpisodeID;"),
        new(132, 04, "ALTER TABLE TMDB_Image DROP COLUMN TmdbSeasonID;"),
        new(132, 05, "ALTER TABLE TMDB_Image DROP COLUMN TmdbShowID;"),
        new(132, 06, "ALTER TABLE TMDB_Image DROP COLUMN TmdbCollectionID;"),
        new(132, 07, "ALTER TABLE TMDB_Image DROP COLUMN TmdbNetworkID;"),
        new(132, 08, "ALTER TABLE TMDB_Image DROP COLUMN TmdbCompanyID;"),
        new(132, 09, "ALTER TABLE TMDB_Image DROP COLUMN TmdbPersonID;"),
        new(132, 10, "ALTER TABLE TMDB_Image DROP COLUMN ForeignType;"),
        new(132, 11, "ALTER TABLE TMDB_Image DROP COLUMN ImageType;"),
        new(132, 12, DatabaseFixes.ScheduleTmdbImageUpdates),
        new(133, 01, "ALTER TABLE TMDB_Season ADD COLUMN PosterPath TEXT NULL DEFAULT NULL;"),
        new(133, 02, "ALTER TABLE TMDB_Episode ADD COLUMN ThumbnailPath TEXT NULL DEFAULT NULL;"),
        new(134, 01, DatabaseFixes.NoOperation),
        new(134, 02, DatabaseFixes.MoveTmdbImagesOnDisc),
        new(135, 01, "DROP TABLE IF EXISTS DuplicateFile;"),
        new(135, 02, "DROP TABLE IF EXISTS AnimeCharacter;"),
        new(136, 01, "ALTER TABLE Tmdb_Show_Network RENAME TO Tmdb_Show_Network_old;"),
        new(136, 02, "ALTER TABLE Tmdb_Show_Network_old RENAME TO TMDB_Show_Network;"),
        new(137, 01, "ALTER TABLE CrossRef_AniDB_TMDB_Movie ADD COLUMN MatchRating INTEGER NOT NULL DEFAULT 1;"),
        new(137, 02, "UPDATE CrossRef_AniDB_TMDB_Movie SET MatchRating = 5 WHERE Source = 0;"),
        new(137, 03, "ALTER TABLE CrossRef_AniDB_TMDB_Movie DROP COLUMN Source;"),
        new(137, 04, "ALTER TABLE CrossRef_AniDB_TMDB_Show ADD COLUMN MatchRating INTEGER NOT NULL DEFAULT 1;"),
        new(137, 05, "UPDATE CrossRef_AniDB_TMDB_Show SET MatchRating = 5 WHERE Source = 0;"),
        new(137, 06, "ALTER TABLE CrossRef_AniDB_TMDB_Show DROP COLUMN Source;"),
        new(138, 01, "CREATE TABLE StoredReleaseInfo (StoredReleaseInfoID INTEGER PRIMARY KEY AUTOINCREMENT, ED2K TEXT NOT NULL, FileSize INTEGER NOT NULL, ID TEXT, ProviderName TEXT NOT NULL, ReleaseURI TEXT, Version INTEGER NOT NULL, ProvidedFileSize INTEGER, Comment TEXT, OriginalFilename TEXT, IsCensored INTEGER, IsChaptered INTEGER, IsCreditless INTEGER, IsCorrupted INTEGER NOT NULL, Source INTEGER NOT NULL, GroupID TEXT, GroupSource TEXT, GroupName TEXT, GroupShortName TEXT, Hashes TEXT NULL, AudioLanguages TEXT, SubtitleLanguages TEXT, CrossReferences TEXT NOT NULL, Metadata TEXT NULL, ReleasedAt DATE, LastUpdatedAt DATETIME NOT NULL, CreatedAt DATETIME NOT NULL);"),
        new(138, 02, "CREATE TABLE StoredReleaseInfo_MatchAttempt (StoredReleaseInfo_MatchAttemptID INTEGER PRIMARY KEY AUTOINCREMENT, AttemptProviderNames TEXT NOT NULL, ProviderName TEXT, ProviderID TEXT, ED2K TEXT NOT NULL, FileSize INTEGER NOT NULL, AttemptStartedAt DATETIME NOT NULL, AttemptEndedAt DATETIME NOT NULL);"),
        new(138, 03, "CREATE TABLE VideoLocal_HashDigest (VideoLocal_HashDigestID INTEGER PRIMARY KEY AUTOINCREMENT, VideoLocalID INTEGER NOT NULL, Type TEXT NOT NULL, Value TEXT NOT NULL, Metadata TEXT);"),
        new(138, 04, DatabaseFixes.MoveAnidbFileDataToReleaseInfoFormat),
        new(138, 05, "ALTER TABLE ImportFolder DROP COLUMN ImportFolderType;"),
        new(138, 06, "ALTER TABLE VideoLocal_Place DROP COLUMN ImportFolderType;"),
        new(139, 01, "CREATE TABLE StoredRelocationPipe (StoredRelocationPipeID INTEGER PRIMARY KEY AUTOINCREMENT, ProviderID text NOT NULL, Name text NOT NULL, Configuration BLOB);"),
        new(139, 02, "CREATE INDEX IX_StoredRelocationPipe_ProviderID ON StoredRelocationPipe(ProviderID);"),
        new(139, 03, "CREATE INDEX IX_StoredRelocationPipe_Name ON StoredRelocationPipe(Name);"),
        new(139, 04, DatabaseFixes.MigrateRenamers),
    };


    private static Tuple<bool, string> DropLanguage(object connection)
    {
        try
        {
            var myConn = (SqliteConnection)connection;
            var factory = (SQLite)Utils.ServiceContainer.GetRequiredService<DatabaseFactory>().Instance;

            var addCommand = "ALTER TABLE CrossRef_Languages_AniDB_File ADD LanguageName TEXT NOT NULL DEFAULT '';";
            var updateCommand =
                "UPDATE CrossRef_Languages_AniDB_File SET LanguageName = l.LanguageName FROM CrossRef_Languages_AniDB_File c INNER JOIN Language l ON l.LanguageID = c.LanguageID WHERE c.LanguageName = '';";
            factory.Execute(myConn, addCommand);
            factory.Execute(myConn, updateCommand);

            addCommand = "ALTER TABLE CrossRef_Subtitles_AniDB_File ADD LanguageName TEXT NOT NULL DEFAULT '';";
            updateCommand =
                "UPDATE CrossRef_Subtitles_AniDB_File SET LanguageName = l.LanguageName FROM CrossRef_Subtitles_AniDB_File c INNER JOIN Language l ON l.LanguageID = c.LanguageID WHERE c.LanguageName = '';";
            factory.Execute(myConn, addCommand);
            factory.Execute(myConn, updateCommand);

            var createCommand =
                "CREATE TABLE CrossRef_Languages_AniDB_File ( CrossRef_Languages_AniDB_FileID INTEGER PRIMARY KEY AUTOINCREMENT, FileID int NOT NULL, LanguageName TEXT NOT NULL);";

            factory.DropColumns(
                myConn, "CrossRef_Languages_AniDB_File",
                new List<string> { "LanguageID" }, createCommand, new List<string>()
            );

            createCommand =
                "CREATE TABLE CrossRef_Subtitles_AniDB_File ( CrossRef_Subtitles_AniDB_FileID INTEGER PRIMARY KEY AUTOINCREMENT, FileID int NOT NULL, LanguageName TEXT NOT NULL);";

            factory.DropColumns(
                myConn, "CrossRef_Subtitles_AniDB_File",
                new List<string> { "LanguageID" }, createCommand, new List<string>()
            );

            var dropCommand = "DROP TABLE Language";
            factory.Execute(myConn, dropCommand);
        }
        catch (Exception e)
        {
            return new Tuple<bool, string>(false, e.ToString());
        }

        return new Tuple<bool, string>(true, null);
    }

    private static Tuple<bool, string> DropAniDB_AnimeColumns(object connection)
    {
        try
        {
            var factory = (SQLite)Utils.ServiceContainer.GetRequiredService<DatabaseFactory>().Instance;
            var myConn = (SqliteConnection)connection;
            var indexCommands = new List<string>
            {
                "CREATE UNIQUE INDEX UIX2_AniDB_Anime_AnimeID ON AniDB_Anime (AnimeID);"
            };
            var createCommand =
                "CREATE TABLE AniDB_Anime ( AniDB_AnimeID INTEGER PRIMARY KEY AUTOINCREMENT, AnimeID int NOT NULL, EpisodeCount int NOT NULL, AirDate timestamp, EndDate timestamp, URL text, Picname text, BeginYear int NOT NULL, EndYear int NOT NULL, AnimeType int NOT NULL, MainTitle text NOT NULL, AllTitles text NOT NULL, AllTags text NOT NULL, Description text NOT NULL, EpisodeCountNormal int NOT NULL, EpisodeCountSpecial int NOT NULL, Rating int NOT NULL, VoteCount int NOT NULL, TempRating int NOT NULL, TempVoteCount int NOT NULL, AvgReviewRating int NOT NULL, ReviewCount int NOT NULL, DateTimeUpdated timestamp NOT NULL, DateTimeDescUpdated timestamp NOT NULL, ImageEnabled int NOT NULL, Restricted int NOT NULL, AnimePlanetID int, ANNID int, AllCinemaID int, AnimeNfo int, LatestEpisodeNumber int, DisableExternalLinksFlag int, ContractVersion int DEFAULT 0 NOT NULL, ContractBlob BLOB, ContractSize int DEFAULT 0 NOT NULL, Site_JP TEXT, Site_EN TEXT, Wikipedia_ID TEXT, WikipediaJP_ID TEXT, SyoboiID INT, AnisonID INT, CrunchyrollID TEXT );";

            factory.DropColumns(
                myConn, "AniDB_Anime",
                new List<string> { "AwardList" }, createCommand, indexCommands
            );
        }
        catch (Exception e)
        {
            return new Tuple<bool, string>(false, e.ToString());
        }

        return new Tuple<bool, string>(true, null);
    }

    private static Tuple<bool, string> DropAniDB_Anime_CharacterColumns(object connection)
    {
        try
        {
            var factory = (SQLite)Utils.ServiceContainer.GetRequiredService<DatabaseFactory>().Instance;
            var myConn = (SqliteConnection)connection;
            var indexCommands = new List<string>
            {
                "CREATE INDEX IX_AniDB_Anime_Character_AnimeID ON AniDB_Anime_Character (AnimeID);",
                "CREATE INDEX IX_AniDB_Anime_Character_CharID ON AniDB_Anime_Character (CharID);",
                "CREATE UNIQUE INDEX UIX_AniDB_Anime_Character_AnimeID_CharID ON AniDB_Anime_Character (AnimeID, CharID);"
            };
            var createCommand =
                "CREATE TABLE AniDB_Anime_Character ( AniDB_Anime_CharacterID INTEGER PRIMARY KEY AUTOINCREMENT, AnimeID int  NOT NULL, CharID int  NOT NULL, CharType text NOT NULL);";

            factory.DropColumns(
                myConn, "AniDB_Anime_Character",
                new List<string> { "EpisodeListRaw" }, createCommand, indexCommands
            );
        }
        catch (Exception e)
        {
            return new Tuple<bool, string>(false, e.ToString());
        }

        return new Tuple<bool, string>(true, null);
    }

    private static Tuple<bool, string> DropAniDB_CharacterColumns(object connection)
    {
        try
        {
            var factory = (SQLite)Utils.ServiceContainer.GetRequiredService<DatabaseFactory>().Instance;
            var myConn = (SqliteConnection)connection;
            var indexCommands = new List<string>
            {
                "CREATE UNIQUE INDEX UIX_AniDB_Character_CharID ON AniDB_Character (CharID);"
            };
            var createCommand =
                "CREATE TABLE AniDB_Character (AniDB_CharacterID INTEGER PRIMARY KEY AUTOINCREMENT, CharID int NOT NULL, CharName text NOT NULL, PicName text NOT NULL, CharKanjiName text NOT NULL, CharDescription text NOT NULL);";

            factory.DropColumns(
                myConn, "AniDB_Character",
                new List<string> { "CreatorListRaw" }, createCommand, indexCommands
            );
        }
        catch (Exception e)
        {
            return new Tuple<bool, string>(false, e.ToString());
        }

        return new Tuple<bool, string>(true, null);
    }

    private static Tuple<bool, string> AlterAniDB_GroupStatus(object connection)
    {
        try
        {
            var factory = (SQLite)Utils.ServiceContainer.GetRequiredService<DatabaseFactory>().Instance;
            var myConn = (SqliteConnection)connection;
            var commands = new List<string>
            {
                "ALTER TABLE AniDB_GroupStatus RENAME TO AniDB_GroupStatus_old;",
                "CREATE TABLE AniDB_GroupStatus ( AniDB_GroupStatusID INTEGER PRIMARY KEY AUTOINCREMENT, AnimeID int NOT NULL, GroupID int NOT NULL, GroupName text NOT NULL, CompletionState int NOT NULL, LastEpisodeNumber int NOT NULL, Rating decimal(6,2) NOT NULL, Votes int NOT NULL, EpisodeRange text NOT NULL ); ",
                "DROP INDEX IX_AniDB_GroupStatus_AnimeID;",
                "CREATE INDEX IX_AniDB_GroupStatus_AnimeID on AniDB_GroupStatus(AnimeID);",
                "DROP INDEX UIX_AniDB_GroupStatus_AnimeID_GroupID;",
                "CREATE UNIQUE INDEX UIX_AniDB_GroupStatus_AnimeID_GroupID ON AniDB_GroupStatus(AnimeID, GroupID);",
                "INSERT INTO AniDB_GroupStatus (AnimeID, GroupID, GroupName, CompletionState, LastEpisodeNumber, Rating, Votes, EpisodeRange) SELECT AnimeID, GroupID, GroupName, CompletionState, LastEpisodeNumber, CAST(Rating AS decimal(5,3)) / 100, Votes, EpisodeRange FROM AniDB_GroupStatus_old",
                "DROP TABLE AniDB_GroupStatus_old"
            };

            foreach (var command in commands)
            {
                factory.Execute(myConn, command);
            }
        }
        catch (Exception e)
        {
            return new Tuple<bool, string>(false, e.ToString());
        }

        return new Tuple<bool, string>(true, null);
    }

    private static Tuple<bool, string> DropAniDB_FileColumns(object connection)
    {
        try
        {
            var factory = (SQLite)Utils.ServiceContainer.GetRequiredService<DatabaseFactory>().Instance;
            var myConn = (SqliteConnection)connection;
            var indexCommands = new List<string>
            {
                "CREATE UNIQUE INDEX UIX_AniDB_File_Hash on AniDB_File(Hash, FileSize);",
                "CREATE UNIQUE INDEX UIX_AniDB_File_FileID ON AniDB_File(FileID);",
                "CREATE INDEX IX_AniDB_File_File_Source on AniDB_File(File_Source);"
            };
            var createCommand =
                "CREATE TABLE AniDB_File ( AniDB_FileID INTEGER PRIMARY KEY AUTOINCREMENT, FileID int NOT NULL, Hash text NOT NULL, GroupID int NOT NULL, File_Source text NOT NULL, File_Description text NOT NULL, File_ReleaseDate int NOT NULL, DateTimeUpdated timestamp NOT NULL, FileName text NOT NULL, FileSize INTEGER NOT NULL,  FileVersion int NULL, InternalVersion int NULL, IsDeprecated int NOT NULL, IsCensored int NULL, IsChaptered INT NOT NULL);";

            factory.DropColumns(
                myConn, "AniDB_File",
                new List<string>
                {
                    "File_AudioCodec",
                    "File_VideoCodec",
                    "File_VideoResolution",
                    "File_FileExtension",
                    "File_LengthSeconds",
                    "Anime_GroupName",
                    "Anime_GroupNameShort",
                    "Episode_Rating",
                    "Episode_Votes",
                    "IsWatched",
                    "WatchedDate",
                    "CRC",
                    "MD5",
                    "SHA1",
                    "AnimeID"
                }, createCommand, indexCommands
            );
        }
        catch (Exception e)
        {
            return new Tuple<bool, string>(false, e.ToString());
        }

        return new Tuple<bool, string>(true, null);
    }

    private static Tuple<bool, string> DropAnimeEpisode_UserColumns(object connection)
    {
        try
        {
            var factory = (SQLite)Utils.ServiceContainer.GetRequiredService<DatabaseFactory>().Instance;
            var myConn = (SqliteConnection)connection;
            var indexCommands = new List<string>
            {
                "CREATE INDEX IX_AnimeEpisode_User_User_AnimeSeriesID on AnimeEpisode_User (JMMUserID, AnimeSeriesID);",
                "CREATE UNIQUE INDEX UIX_AnimeEpisode_User_User_EpisodeID on AnimeEpisode_User (JMMUserID, AnimeEpisodeID);"
            };
            var createCommand =
                "CREATE TABLE AnimeEpisode_User (AnimeEpisode_UserID INTEGER PRIMARY KEY AUTOINCREMENT, JMMUserID int NOT NULL, AnimeEpisodeID int NOT NULL, AnimeSeriesID int NOT NULL, WatchedDate timestamp, PlayedCount int NOT NULL, WatchedCount int NOT NULL, StoppedCount int NOT NULL);";

            factory.DropColumns(
                myConn, "AnimeEpisode_User",
                new List<string> { "ContractSize", "ContractBlob", "ContractVersion" }, createCommand, indexCommands
            );
        }
        catch (Exception e)
        {
            return new Tuple<bool, string>(false, e.ToString());
        }

        return new Tuple<bool, string>(true, null);
    }

    private static Tuple<bool, string> DropVideoLocal_Media(object connection)
    {
        try
        {
            var factory = (SQLite)Utils.ServiceContainer.GetRequiredService<DatabaseFactory>().Instance;
            var myConn = (SqliteConnection)connection;
            var createvlcommand =
                "CREATE TABLE VideoLocal ( VideoLocalID INTEGER PRIMARY KEY AUTOINCREMENT, Hash text NOT NULL, CRC32 text NULL, MD5 text NULL, SHA1 text NULL, HashSource int NOT NULL, FileSize INTEGER NOT NULL, IsIgnored int NOT NULL, DateTimeUpdated timestamp NOT NULL, FileName text NOT NULL DEFAULT '', DateTimeCreated timestamp NULL, IsVariation int NULL,MediaVersion int NOT NULL DEFAULT 0,MediaBlob BLOB NULL,MediaSize int NOT NULL DEFAULT 0, MyListID INT NOT NULL DEFAULT 0);";
            var indexvlcommands =
                new List<string> { "CREATE UNIQUE INDEX UIX2_VideoLocal_Hash on VideoLocal(Hash)" };
            factory.DropColumns(myConn, "VideoLocal",
                new List<string>
                {
                    "VideoCodec",
                    "AudioCodec",
                    "VideoBitrate",
                    "AudioBitrate",
                    "VideoBitDepth",
                    "VideoFrameRate",
                    "VideoResolution",
                    "Duration"
                }, createvlcommand, indexvlcommands);
            return new Tuple<bool, string>(true, null);
        }
        catch (Exception e)
        {
            return new Tuple<bool, string>(false, e.ToString());
        }
    }

    private static Tuple<bool, string> DropAniDB_EpisodeTitles(object connection)
    {
        try
        {
            var factory = (SQLite)Utils.ServiceContainer.GetRequiredService<DatabaseFactory>().Instance;
            var myConn = (SqliteConnection)connection;
            var createcommand =
                "create table AniDB_Episode ( AniDB_EpisodeID integer primary key autoincrement, EpisodeID int not null, AnimeID int not null, LengthSeconds int not null, Rating text not null, Votes text not null, EpisodeNumber int not null, EpisodeType int not null, AirDate int not null, DateTimeUpdated datetime not null, Description text default '' not null )";
            var indexcommands = new List<string>
            {
                "create index IX_AniDB_Episode_AnimeID on AniDB_Episode (AnimeID)",
                "create unique index UIX_AniDB_Episode_EpisodeID on AniDB_Episode (EpisodeID)"
            };
            factory.DropColumns(myConn, "AniDB_Episode",
                new List<string> { "EnglishName", "RomajiName" }, createcommand, indexcommands);
            return new Tuple<bool, string>(true, null);
        }
        catch (Exception e)
        {
            return new Tuple<bool, string>(false, e.ToString());
        }
    }

    private static Tuple<bool, string> RenameCrossRef_AniDB_TvDB_Episode(object connection)
    {
        try
        {
            var factory = (SQLite)Utils.ServiceContainer.GetRequiredService<DatabaseFactory>().Instance;
            // I'm doing this manually to save time
            var myConn = (SqliteConnection)connection;

            // make the new one
            // create indexes
            // transfer data
            // drop old table
            var cmds = new List<string>
            {
                "CREATE TABLE CrossRef_AniDB_TvDB_Episode_Override( CrossRef_AniDB_TvDB_Episode_OverrideID INTEGER PRIMARY KEY AUTOINCREMENT, AniDBEpisodeID int NOT NULL, TvDBEpisodeID int NOT NULL );",
                "CREATE UNIQUE INDEX UIX_AniDB_TvDB_Episode_Override_AniDBEpisodeID_TvDBEpisodeID ON CrossRef_AniDB_TvDB_Episode_Override(AniDBEpisodeID,TvDBEpisodeID);",
                "INSERT INTO CrossRef_AniDB_TvDB_Episode_Override ( AniDBEpisodeID, TvDBEpisodeID ) SELECT AniDBEpisodeID, TvDBEpisodeID FROM CrossRef_AniDB_TvDB_Episode; ",
                "DROP TABLE CrossRef_AniDB_TvDB_Episode;"
            };
            foreach (var cmdTable in cmds)
            {
                factory.Execute(myConn, cmdTable);
            }

            return new Tuple<bool, string>(true, null);
        }
        catch (Exception e)
        {
            return new Tuple<bool, string>(false, e.ToString());
        }
    }

    private static Tuple<bool, string> DropAniDB_AnimeAllCategories(object connection)
    {
        try
        {
            var factory = (SQLite)Utils.ServiceContainer.GetRequiredService<DatabaseFactory>().Instance;
            var myConn = (SqliteConnection)connection;
            var createcommand =
                "CREATE TABLE AniDB_Anime ( AniDB_AnimeID INTEGER PRIMARY KEY AUTOINCREMENT, AnimeID int NOT NULL, EpisodeCount int NOT NULL, AirDate timestamp NULL, EndDate timestamp NULL, URL text NULL, Picname text NULL, BeginYear int NOT NULL, EndYear int NOT NULL, AnimeType int NOT NULL, MainTitle text NOT NULL, AllTitles text NOT NULL, AllTags text NOT NULL, Description text NOT NULL, EpisodeCountNormal int NOT NULL, EpisodeCountSpecial int NOT NULL, Rating int NOT NULL, VoteCount int NOT NULL, TempRating int NOT NULL, TempVoteCount int NOT NULL, AvgReviewRating int NOT NULL, ReviewCount int NOT NULL, DateTimeUpdated timestamp NOT NULL, DateTimeDescUpdated timestamp NOT NULL, ImageEnabled int NOT NULL, AwardList text NOT NULL, Restricted int NOT NULL, AnimePlanetID int NULL, ANNID int NULL, AllCinemaID int NULL, AnimeNfo int NULL, LatestEpisodeNumber int NULL, DisableExternalLinksFlag int NULL );";
            var indexcommands = new List<string>
            {
                "CREATE UNIQUE INDEX [UIX2_AniDB_Anime_AnimeID] ON [AniDB_Anime] ([AnimeID]);"
            };
            factory.DropColumns(myConn, "AniDB_Anime",
                new List<string> { "AllCategories" }, createcommand, indexcommands);
            return new Tuple<bool, string>(true, null);
        }
        catch (Exception e)
        {
            return new Tuple<bool, string>(false, e.ToString());
        }
    }


    private static Tuple<bool, string> DropVideolocalColumns(object connection)
    {
        try
        {
            var factory = (SQLite)Utils.ServiceContainer.GetRequiredService<DatabaseFactory>().Instance;
            var myConn = (SqliteConnection)connection;
            var createvlcommand =
                "CREATE TABLE VideoLocal ( VideoLocalID INTEGER PRIMARY KEY AUTOINCREMENT, Hash text NOT NULL, CRC32 text NULL, MD5 text NULL, SHA1 text NULL, HashSource int NOT NULL, FileSize INTEGER NOT NULL, IsIgnored int NOT NULL, DateTimeUpdated timestamp NOT NULL, FileName text NOT NULL DEFAULT '', VideoCodec text NOT NULL DEFAULT '', VideoBitrate text NOT NULL DEFAULT '',VideoBitDepth text NOT NULL DEFAULT '',VideoFrameRate text NOT NULL DEFAULT '',VideoResolution text NOT NULL DEFAULT '',AudioCodec text NOT NULL DEFAULT '',AudioBitrate text NOT NULL DEFAULT '',Duration INTEGER NOT NULL DEFAULT 0,DateTimeCreated timestamp NULL, IsVariation int NULL,MediaVersion int NOT NULL DEFAULT 0,MediaBlob BLOB NULL,MediaSize int NOT NULL DEFAULT 0 );";
            var indexvlcommands =
                new List<string> { "CREATE UNIQUE INDEX UIX2_VideoLocal_Hash on VideoLocal(Hash)" };
            factory.DropColumns(myConn, "VideoLocal",
                new List<string> { "FilePath", "ImportFolderID" }, createvlcommand, indexvlcommands);
            return new Tuple<bool, string>(true, null);
        }
        catch (Exception e)
        {
            return new Tuple<bool, string>(false, e.ToString());
        }
    }

    private static Tuple<bool, string> DropTvDB_EpisodeFirstAiredColumn(object connection)
    {
        try
        {
            var factory = (SQLite)Utils.ServiceContainer.GetRequiredService<DatabaseFactory>().Instance;
            var myConn = (SqliteConnection)connection;
            var createtvepcommand =
                "CREATE TABLE TvDB_Episode ( TvDB_EpisodeID INTEGER PRIMARY KEY AUTOINCREMENT, Id int NOT NULL, SeriesID int NOT NULL, SeasonID int NOT NULL, SeasonNumber int NOT NULL, EpisodeNumber int NOT NULL, EpisodeName text, Overview text, Filename text, EpImgFlag int NOT NULL, AbsoluteNumber int, AirsAfterSeason int, AirsBeforeEpisode int, AirsBeforeSeason int, AirDate timestamp, Rating int)";
            var indextvepcommands =
                new List<string> { "CREATE UNIQUE INDEX UIX_TvDB_Episode_Id ON TvDB_Episode(Id);" };
            factory.DropColumns(myConn, "TvDB_Episode",
                new List<string> { "FirstAired" }, createtvepcommand, indextvepcommands);
            return new Tuple<bool, string>(true, null);
        }
        catch (Exception e)
        {
            return new Tuple<bool, string>(false, e.ToString());
        }
    }

    private static Tuple<bool, string> AlterVideoLocalUser(object connection)
    {
        try
        {
            var factory = (SQLite)Utils.ServiceContainer.GetRequiredService<DatabaseFactory>().Instance;
            var myConn = (SqliteConnection)connection;
            var createvluser =
                "CREATE TABLE VideoLocal_User ( VideoLocal_UserID INTEGER PRIMARY KEY AUTOINCREMENT, JMMUserID int NOT NULL, VideoLocalID int NOT NULL, WatchedDate timestamp NULL, ResumePosition bigint NOT NULL DEFAULT 0); ";
            var indexvluser = new List<string>
            {
                "CREATE UNIQUE INDEX UIX2_VideoLocal_User_User_VideoLocalID ON VideoLocal_User(JMMUserID, VideoLocalID);"
            };
            factory.Alter(myConn, "VideoLocal_User", createvluser, indexvluser);
            return new Tuple<bool, string>(true, null);
        }
        catch (Exception e)
        {
            return new Tuple<bool, string>(false, e.ToString());
        }
    }


    //WE NEED TO DROP SOME SQL LITE COLUMNS...

    private void DropColumns(SqliteConnection db, string tableName, List<string> colsToRemove, string createcommand,
        List<string> indexcommands)
    {
        var updatedTableColumns = GetTableColumns(db, tableName);
        colsToRemove.ForEach(a => updatedTableColumns.Remove(a));
        var columnsSeperated = string.Join(",", updatedTableColumns);

        // Drop indexes first. We can get them from the create commands
        // Ignore if they don't exist
        foreach (var indexcommand in indexcommands)
        {
            var position = indexcommand.IndexOf("index", StringComparison.InvariantCultureIgnoreCase) + 6;
            var indexname = indexcommand.Substring(position);
            position = indexname.IndexOf(' ');
            indexname = indexname.Substring(0, position);
            indexname = "DROP INDEX " + indexname + ";";
            try
            {
                Execute(db, indexname);
            }
            catch
            {
                // ignore
            }
        }

        // Rename table to old
        // make the new one
        // recreate indexes
        // transfer data
        // drop old table
        var cmds = new List<string> { "ALTER TABLE " + tableName + " RENAME TO " + tableName + "_old;", createcommand };
        cmds.AddRange(indexcommands);
        cmds.Add("INSERT INTO " + tableName + " (" + columnsSeperated + ") SELECT " + columnsSeperated + " FROM " +
                 tableName + "_old; ");
        cmds.Add("DROP TABLE " + tableName + "_old;");
        foreach (var cmdTable in cmds)
        {
            Execute(db, cmdTable);
        }
    }

    private void Alter(SqliteConnection db, string tableName, string createcommand, List<string> indexcommands)
    {
        var updatedTableColumns = GetTableColumns(db, tableName);
        var columnsSeperated = string.Join(",", updatedTableColumns);
        var cmds = new List<string> { "ALTER TABLE " + tableName + " RENAME TO " + tableName + "_old;", createcommand };
        cmds.AddRange(indexcommands);
        cmds.Add("INSERT INTO " + tableName + " (" + columnsSeperated + ") SELECT " + columnsSeperated + " FROM " +
                 tableName + "_old; ");
        cmds.Add("DROP TABLE " + tableName + "_old;");
        foreach (var cmdTable in cmds)
        {
            Execute(db, cmdTable);
        }
    }

    private List<string> GetTableColumns(SqliteConnection conn, string tableName)
    {
        var cmd = "pragma table_info(" + tableName + ")";
        var columns = new List<string>();
        foreach (var o in ExecuteReader(conn, cmd))
        {
            var oo = (object[])o;
            columns.Add((string)oo[1]);
        }

        return columns;
    }

    protected override Tuple<bool, string> ExecuteCommand(SqliteConnection connection, string command)
    {
        try
        {
            Execute(connection, command);
            return new Tuple<bool, string>(true, null);
        }
        catch (Exception ex)
        {
            return new Tuple<bool, string>(false, ex.ToString());
        }
    }

    protected override void Execute(SqliteConnection connection, string command)
    {
        using var sqCommand = new SqliteCommand(command, connection);
        sqCommand.CommandTimeout = 0;
        sqCommand.ExecuteNonQuery();
    }

    protected override long ExecuteScalar(SqliteConnection connection, string command)
    {
        using var sqCommand = new SqliteCommand(command, connection);
        sqCommand.CommandTimeout = 0;
        return long.Parse(sqCommand.ExecuteScalar().ToString());
    }

    protected override List<object[]> ExecuteReader(SqliteConnection connection, string command)
    {
        using var sqCommand = new SqliteCommand(command, connection);
        var rows = new List<object[]>();
        sqCommand.CommandTimeout = 0;
        using var reader = sqCommand.ExecuteReader();
        while (reader.Read())
        {
            var values = new object[reader.FieldCount];
            reader.GetValues(values);
            rows.Add(values);
        }

        reader.Close();
        return rows;
    }

    protected override void ConnectionWrapper(string connectionstring, Action<SqliteConnection> action)
    {
        using var con = new SqliteConnection(connectionstring);
        con.Open();
        action(con);
    }

    public override void CreateAndUpdateSchema()
    {
        ConnectionWrapper(GetConnectionString(), myConn =>
        {
            Execute(myConn, "PRAGMA encoding = \"UTF-16\"");
            var create =
                ExecuteScalar(myConn, "SELECT count(*) as NumTables FROM sqlite_master WHERE name='Versions'") == 0;
            if (create)
            {
                ServerState.Instance.ServerStartingStatus = "Database - Creating Initial Schema...";
                ExecuteWithException(myConn, createVersionTable);
            }

            if (!GetTableColumns(myConn, "Versions").Contains("VersionRevision"))
            {
                ExecuteWithException(myConn, updateVersionTable);
                AllVersions = RepoFactory.Versions.GetAllByType(Constants.DatabaseTypeKey);
            }

            PreFillVersions(createTables.Union(patchCommands));
            if (create)
            {
                ExecuteWithException(myConn, createTables);
            }

            ServerState.Instance.ServerStartingStatus = "Database - Applying Schema Patches...";
            ExecuteWithException(myConn, patchCommands);
        });
    }
}
