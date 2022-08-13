using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using FluentNHibernate.Cfg;
using FluentNHibernate.Cfg.Db;
using NHibernate;
using Shoko.Commons.Properties;
using NHibernate.Cfg;
using Shoko.Server.Repositories;
using Shoko.Server.Server;
using Shoko.Server.Settings;

// ReSharper disable InconsistentNaming

namespace Shoko.Server.Databases
{
    public class SQLite : BaseDatabase<SQLiteConnection>, IDatabase
    {

        public string Name { get; } = "SQLite";

        public int RequiredVersion { get; } = 85;


        public void BackupDatabase(string fullfilename)
        {
            fullfilename += ".db3";
            File.Copy(GetDatabaseFilePath(), fullfilename);
        }

        public static string GetDatabasePath()
        {
            return ServerSettings.Instance.Database.MySqliteDirectory;
        }

        public static string GetDatabaseFilePath()
        {
            string dbName = Path.Combine(GetDatabasePath(), ServerSettings.Instance.Database.SQLite_DatabaseFile);
            return dbName;
        }

        public override string GetConnectionString()
        {
            return $@"data source={GetDatabaseFilePath()};useutf16encoding=True";
        }

        public ISessionFactory CreateSessionFactory()
        {
            return Fluently.Configure()
                .Database(SQLiteConfiguration.Standard
                    .UsingFile(GetDatabaseFilePath()))
                .Mappings(m =>
                    m.FluentMappings.AddFromAssemblyOf<ShokoService>())
                .ExposeConfiguration(c => c.DataBaseIntegration(prop =>
                {
                    // uncomment this for SQL output
                    //prop.LogSqlInConsole = true;
                }))
                .BuildSessionFactory();
        }

        public bool DatabaseAlreadyExists()
        {
            if (GetDatabaseFilePath().Length == 0) return false;

            if (File.Exists(GetDatabaseFilePath()))
                return true;
            return false;
        }


        public void CreateDatabase()
        {
            if (DatabaseAlreadyExists()) return;

            if (!Directory.Exists(GetDatabasePath()))
                Directory.CreateDirectory(GetDatabasePath());

            if (!File.Exists(GetDatabaseFilePath()))
                SQLiteConnection.CreateFile(GetDatabaseFilePath());

            ServerSettings.Instance.Database.SQLite_DatabaseFile = GetDatabaseFilePath();
        }


        private List<DatabaseCommand> createVersionTable = new List<DatabaseCommand>
        {
            new DatabaseCommand(0, 1,
                "CREATE TABLE Versions ( VersionsID INTEGER PRIMARY KEY AUTOINCREMENT, VersionType Text NOT NULL, VersionValue Text NOT NULL)"),
        };

        private List<DatabaseCommand> createTables = new List<DatabaseCommand>
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

        private List<DatabaseCommand> updateVersionTable = new List<DatabaseCommand>
        {
            new DatabaseCommand("ALTER TABLE Versions ADD VersionRevision text NULL;"),
            new DatabaseCommand("ALTER TABLE Versions ADD VersionCommand text NULL;"),
            new DatabaseCommand("ALTER TABLE Versions ADD VersionProgram text NULL;"),
            new DatabaseCommand(
                "CREATE INDEX IX_Versions_VersionType ON Versions(VersionType,VersionValue,VersionRevision);"),
        };


        private List<DatabaseCommand> patchCommands = new List<DatabaseCommand>
        {
            new DatabaseCommand(2, 1,
                "CREATE TABLE IgnoreAnime( IgnoreAnimeID INTEGER PRIMARY KEY AUTOINCREMENT, JMMUserID int NOT NULL, AnimeID int NOT NULL, IgnoreType int NOT NULL)"),
            new DatabaseCommand(2, 2,
                "CREATE UNIQUE INDEX UIX_IgnoreAnime_User_AnimeID ON IgnoreAnime(JMMUserID, AnimeID, IgnoreType);"),
            new DatabaseCommand(3, 1,
                "CREATE TABLE Trakt_Friend( Trakt_FriendID INTEGER PRIMARY KEY AUTOINCREMENT, Username text NOT NULL, FullName text NULL, Gender text NULL, Age text NULL, Location text NULL, About text NULL, Joined int NOT NULL, Avatar text NULL, Url text NULL, LastAvatarUpdate timestamp NOT NULL)"),
            new DatabaseCommand(3, 2, "CREATE UNIQUE INDEX UIX_Trakt_Friend_Username ON Trakt_Friend(Username);"),
            new DatabaseCommand(4, 1, "ALTER TABLE AnimeGroup ADD DefaultAnimeSeriesID int NULL"),
            new DatabaseCommand(5, 1, "ALTER TABLE JMMUser ADD CanEditServerSettings int NULL"),
            new DatabaseCommand(6, 1, DatabaseFixes.FixDuplicateTvDBLinks),
            new DatabaseCommand(6, 2, DatabaseFixes.FixDuplicateTraktLinks),
            new DatabaseCommand(6, 3,
                "CREATE UNIQUE INDEX UIX_CrossRef_AniDB_TvDB_Season ON CrossRef_AniDB_TvDB(TvDBID, TvDBSeasonNumber);"),
            new DatabaseCommand(6, 4,
                "CREATE UNIQUE INDEX UIX_CrossRef_AniDB_TvDB_AnimeID ON CrossRef_AniDB_TvDB(AnimeID);"),
            new DatabaseCommand(6, 5,
                "CREATE UNIQUE INDEX UIX_CrossRef_AniDB_Trakt_Season ON CrossRef_AniDB_Trakt(TraktID, TraktSeasonNumber);"),
            new DatabaseCommand(6, 6,
                "CREATE UNIQUE INDEX UIX_CrossRef_AniDB_Trakt_Anime ON CrossRef_AniDB_Trakt(AnimeID);"),
            new DatabaseCommand(7, 1, "ALTER TABLE VideoInfo ADD VideoBitDepth text NULL"),
            new DatabaseCommand(9, 1, "ALTER TABLE ImportFolder ADD IsWatched int NOT NULL DEFAULT 1"),
            new DatabaseCommand(10, 1,
                "CREATE TABLE CrossRef_AniDB_MAL( CrossRef_AniDB_MALID INTEGER PRIMARY KEY AUTOINCREMENT, AnimeID int NOT NULL, MALID int NOT NULL, MALTitle text, CrossRefSource int NOT NULL ); "),
            new DatabaseCommand(10, 2,
                "CREATE UNIQUE INDEX UIX_CrossRef_AniDB_MAL_AnimeID ON CrossRef_AniDB_MAL(AnimeID);"),
            new DatabaseCommand(10, 3,
                "CREATE UNIQUE INDEX UIX_CrossRef_AniDB_MAL_MALID ON CrossRef_AniDB_MAL(MALID);"),
            new DatabaseCommand(11, 1, "DROP TABLE CrossRef_AniDB_MAL;"),
            new DatabaseCommand(11, 2,
                "CREATE TABLE CrossRef_AniDB_MAL( CrossRef_AniDB_MALID INTEGER PRIMARY KEY AUTOINCREMENT, AnimeID int NOT NULL, MALID int NOT NULL, MALTitle text, StartEpisodeType int NOT NULL, StartEpisodeNumber int NOT NULL, CrossRefSource int NOT NULL ); "),
            new DatabaseCommand(11, 3,
                "CREATE UNIQUE INDEX UIX_CrossRef_AniDB_MAL_MALID ON CrossRef_AniDB_MAL(MALID);"),
            new DatabaseCommand(11, 4,
                "CREATE UNIQUE INDEX UIX_CrossRef_AniDB_MAL_Anime ON CrossRef_AniDB_MAL(AnimeID, StartEpisodeType, StartEpisodeNumber);"),
            new DatabaseCommand(12, 1,
                "CREATE TABLE Playlist( PlaylistID INTEGER PRIMARY KEY AUTOINCREMENT, PlaylistName text, PlaylistItems text, DefaultPlayOrder int NOT NULL, PlayWatched int NOT NULL, PlayUnwatched int NOT NULL ); "),
            new DatabaseCommand(13, 1, "ALTER TABLE AnimeSeries ADD SeriesNameOverride text"),
            new DatabaseCommand(14, 1,
                "CREATE TABLE BookmarkedAnime( BookmarkedAnimeID INTEGER PRIMARY KEY AUTOINCREMENT, AnimeID int NOT NULL, Priority int NOT NULL, Notes text, Downloading int NOT NULL ); "),
            new DatabaseCommand(14, 2,
                "CREATE UNIQUE INDEX UIX_BookmarkedAnime_AnimeID ON BookmarkedAnime(BookmarkedAnimeID)"),
            new DatabaseCommand(15, 1, "ALTER TABLE VideoLocal ADD DateTimeCreated timestamp NULL"),
            new DatabaseCommand(15, 2, "UPDATE VideoLocal SET DateTimeCreated = DateTimeUpdated"),
            new DatabaseCommand(16, 1,
                "CREATE TABLE CrossRef_AniDB_TvDB_Episode( CrossRef_AniDB_TvDB_EpisodeID INTEGER PRIMARY KEY AUTOINCREMENT, AnimeID int NOT NULL, AniDBEpisodeID int NOT NULL, TvDBEpisodeID int NOT NULL ); "),
            new DatabaseCommand(16, 2,
                "CREATE UNIQUE INDEX UIX_CrossRef_AniDB_TvDB_Episode_AniDBEpisodeID ON CrossRef_AniDB_TvDB_Episode(AniDBEpisodeID);"),
            new DatabaseCommand(17, 1,
                "CREATE TABLE AniDB_MylistStats( AniDB_MylistStatsID INTEGER PRIMARY KEY AUTOINCREMENT, Animes int NOT NULL, Episodes int NOT NULL, Files int NOT NULL, SizeOfFiles INTEGER NOT NULL, AddedAnimes int NOT NULL, AddedEpisodes int NOT NULL, AddedFiles int NOT NULL, AddedGroups int NOT NULL, LeechPct int NOT NULL, GloryPct int NOT NULL, ViewedPct int NOT NULL, MylistPct int NOT NULL, ViewedMylistPct int NOT NULL, EpisodesViewed int NOT NULL, Votes int NOT NULL, Reviews int NOT NULL, ViewiedLength int NOT NULL ); "),
            new DatabaseCommand(18, 1,
                "CREATE TABLE FileFfdshowPreset( FileFfdshowPresetID INTEGER PRIMARY KEY AUTOINCREMENT, Hash int NOT NULL, FileSize INTEGER NOT NULL, Preset text ); "),
            new DatabaseCommand(18, 2,
                "CREATE UNIQUE INDEX UIX_FileFfdshowPreset_Hash ON FileFfdshowPreset(Hash, FileSize);"),
            new DatabaseCommand(19, 1, "ALTER TABLE AniDB_Anime ADD DisableExternalLinksFlag int NULL"),
            new DatabaseCommand(19, 2, "UPDATE AniDB_Anime SET DisableExternalLinksFlag = 0"),
            new DatabaseCommand(20, 1, "ALTER TABLE AniDB_File ADD FileVersion int NULL"),
            new DatabaseCommand(20, 2, "UPDATE AniDB_File SET FileVersion = 1"),
            new DatabaseCommand(21, 1,
                "CREATE TABLE RenameScript( RenameScriptID INTEGER PRIMARY KEY AUTOINCREMENT, ScriptName text, Script text, IsEnabledOnImport int NOT NULL ); "),
            new DatabaseCommand(22, 1, "ALTER TABLE AniDB_File ADD IsCensored int NULL"),
            new DatabaseCommand(22, 2, "ALTER TABLE AniDB_File ADD IsDeprecated int NULL"),
            new DatabaseCommand(22, 3, "ALTER TABLE AniDB_File ADD InternalVersion int NULL"),
            new DatabaseCommand(22, 4, "UPDATE AniDB_File SET IsCensored = 0"),
            new DatabaseCommand(22, 5, "UPDATE AniDB_File SET IsDeprecated = 0"),
            new DatabaseCommand(22, 6, "UPDATE AniDB_File SET InternalVersion = 1"),
            new DatabaseCommand(23, 1, "UPDATE JMMUser SET CanEditServerSettings = 1"),
            new DatabaseCommand(24, 1, "ALTER TABLE VideoLocal ADD IsVariation int NULL"),
            new DatabaseCommand(24, 2, "UPDATE VideoLocal SET IsVariation = 0"),
            new DatabaseCommand(25, 1,
                "CREATE TABLE AniDB_Recommendation( AniDB_RecommendationID INTEGER PRIMARY KEY AUTOINCREMENT, AnimeID int NOT NULL, UserID int NOT NULL, RecommendationType int NOT NULL, RecommendationText text ); "),
            new DatabaseCommand(25, 2,
                "CREATE UNIQUE INDEX UIX_AniDB_Recommendation ON AniDB_Recommendation(AnimeID, UserID);"),
            new DatabaseCommand(26, 1, "CREATE INDEX IX_CrossRef_File_Episode_Hash ON CrossRef_File_Episode(Hash);"),
            new DatabaseCommand(26, 2,
                "CREATE INDEX IX_CrossRef_File_Episode_EpisodeID ON CrossRef_File_Episode(EpisodeID);"),
            new DatabaseCommand(27, 1,
                "update CrossRef_File_Episode SET CrossRefSource=1 WHERE Hash IN (Select Hash from ANIDB_File) AND CrossRefSource=2;"),
            new DatabaseCommand(28, 1,
                "CREATE TABLE LogMessage( LogMessageID INTEGER PRIMARY KEY AUTOINCREMENT, LogType text, LogContent text, LogDate timestamp NOT NULL ); "),
            new DatabaseCommand(29, 1,
                "CREATE TABLE CrossRef_AniDB_TvDBV2( CrossRef_AniDB_TvDBV2ID INTEGER PRIMARY KEY AUTOINCREMENT, AnimeID int NOT NULL, AniDBStartEpisodeType int NOT NULL, AniDBStartEpisodeNumber int NOT NULL, TvDBID int NOT NULL, TvDBSeasonNumber int NOT NULL, TvDBStartEpisodeNumber int NOT NULL, TvDBTitle text, CrossRefSource int NOT NULL ); "),
            new DatabaseCommand(29, 2,
                "CREATE UNIQUE INDEX UIX_CrossRef_AniDB_TvDBV2 ON CrossRef_AniDB_TvDBV2(AnimeID, TvDBID, TvDBSeasonNumber, TvDBStartEpisodeNumber, AniDBStartEpisodeType, AniDBStartEpisodeNumber);"),
            new DatabaseCommand(29, 3, DatabaseFixes.MigrateTvDBLinks_V1_to_V2),
            new DatabaseCommand(30, 1, "ALTER TABLE GroupFilter ADD Locked int NULL"),
            new DatabaseCommand(31, 1, "ALTER TABLE VideoInfo ADD FullInfo text NULL"),
            new DatabaseCommand(32, 1,
                "CREATE TABLE CrossRef_AniDB_TraktV2( CrossRef_AniDB_TraktV2ID INTEGER PRIMARY KEY AUTOINCREMENT, AnimeID int NOT NULL, AniDBStartEpisodeType int NOT NULL, AniDBStartEpisodeNumber int NOT NULL, TraktID text, TraktSeasonNumber int NOT NULL, TraktStartEpisodeNumber int NOT NULL, TraktTitle text, CrossRefSource int NOT NULL ); "),
            new DatabaseCommand(32, 2,
                "CREATE UNIQUE INDEX UIX_CrossRef_AniDB_TraktV2 ON CrossRef_AniDB_TraktV2(AnimeID, TraktSeasonNumber, TraktStartEpisodeNumber, AniDBStartEpisodeType, AniDBStartEpisodeNumber);"),
            new DatabaseCommand(32, 3, DatabaseFixes.MigrateTraktLinks_V1_to_V2),
            new DatabaseCommand(33, 1,
                "CREATE TABLE CrossRef_AniDB_Trakt_Episode( CrossRef_AniDB_Trakt_EpisodeID INTEGER PRIMARY KEY AUTOINCREMENT, AnimeID int NOT NULL, AniDBEpisodeID int NOT NULL, TraktID text, Season int NOT NULL, EpisodeNumber int NOT NULL ); "),
            new DatabaseCommand(33, 2,
                "CREATE UNIQUE INDEX UIX_CrossRef_AniDB_Trakt_Episode_AniDBEpisodeID ON CrossRef_AniDB_Trakt_Episode(AniDBEpisodeID);"),
            new DatabaseCommand(34, 1, DatabaseFixes.RemoveOldMovieDBImageRecords),
            new DatabaseCommand(35, 1,
                "CREATE TABLE CustomTag( CustomTagID INTEGER PRIMARY KEY AUTOINCREMENT, TagName text, TagDescription text ); "),
            new DatabaseCommand(35, 2,
                "CREATE TABLE CrossRef_CustomTag( CrossRef_CustomTagID INTEGER PRIMARY KEY AUTOINCREMENT, CustomTagID int NOT NULL, CrossRefID int NOT NULL, CrossRefType int NOT NULL ); "),
            new DatabaseCommand(36, 1, "ALTER TABLE AniDB_Anime_Tag ADD Weight int NULL"),
            new DatabaseCommand(37, 1, DatabaseFixes.PopulateTagWeight),
            new DatabaseCommand(38, 1, "ALTER TABLE Trakt_Episode ADD TraktID int NULL"),
            new DatabaseCommand(39, 1, DatabaseFixes.FixHashes),
            new DatabaseCommand(40, 1, "DROP TABLE LogMessage;"),
            new DatabaseCommand(41, 1, "ALTER TABLE AnimeSeries ADD DefaultFolder text NULL"),
            new DatabaseCommand(42, 1, "ALTER TABLE JMMUser ADD PlexUsers text NULL"),
            new DatabaseCommand(43, 1, "ALTER TABLE GroupFilter ADD FilterType int NOT NULL DEFAULT 1"),
            new DatabaseCommand(43, 2,
                $"UPDATE GroupFilter SET FilterType = 2 WHERE GroupFilterName='{Constants.GroupFilterName.ContinueWatching}'"),
            new DatabaseCommand(43, 3, DatabaseFixes.FixContinueWatchingGroupFilter_20160406),
            new DatabaseCommand(44, 1, DropAniDB_AnimeAllCategories),
            new DatabaseCommand(44, 2, "ALTER TABLE AniDB_Anime ADD ContractVersion int NOT NULL DEFAULT 0"),
            new DatabaseCommand(44, 3, "ALTER TABLE AniDB_Anime ADD ContractBlob BLOB NULL"),
            new DatabaseCommand(44, 4, "ALTER TABLE AniDB_Anime ADD ContractSize int NOT NULL DEFAULT 0"),
            new DatabaseCommand(44, 5, "ALTER TABLE AnimeGroup ADD ContractVersion int NOT NULL DEFAULT 0"),
            new DatabaseCommand(44, 6, "ALTER TABLE AnimeGroup ADD LatestEpisodeAirDate timestamp NULL"),
            new DatabaseCommand(44, 7, "ALTER TABLE AnimeGroup ADD ContractBlob BLOB NULL"),
            new DatabaseCommand(44, 8, "ALTER TABLE AnimeGroup ADD ContractSize int NOT NULL DEFAULT 0"),
            new DatabaseCommand(44, 9, "ALTER TABLE AnimeGroup_User ADD PlexContractVersion int NOT NULL DEFAULT 0"),
            new DatabaseCommand(44, 10, "ALTER TABLE AnimeGroup_User ADD PlexContractBlob BLOB NULL"),
            new DatabaseCommand(44, 11, "ALTER TABLE AnimeGroup_User ADD PlexContractSize int NOT NULL DEFAULT 0"),
            new DatabaseCommand(44, 12, "ALTER TABLE AnimeSeries ADD ContractVersion int NOT NULL DEFAULT 0"),
            new DatabaseCommand(44, 13, "ALTER TABLE AnimeSeries ADD LatestEpisodeAirDate timestamp NULL"),
            new DatabaseCommand(44, 14, "ALTER TABLE AnimeSeries ADD ContractBlob BLOB NULL"),
            new DatabaseCommand(44, 15, "ALTER TABLE AnimeSeries ADD ContractSize int NOT NULL DEFAULT 0"),
            new DatabaseCommand(44, 16, "ALTER TABLE AnimeSeries_User ADD PlexContractVersion int NOT NULL DEFAULT 0"),
            new DatabaseCommand(44, 17, "ALTER TABLE AnimeSeries_User ADD PlexContractBlob BLOB NULL"),
            new DatabaseCommand(44, 18, "ALTER TABLE AnimeSeries_User ADD PlexContractSize int NOT NULL DEFAULT 0"),
            new DatabaseCommand(44, 19, "ALTER TABLE GroupFilter ADD GroupsIdsVersion int NOT NULL DEFAULT 0"),
            new DatabaseCommand(44, 20, "ALTER TABLE GroupFilter ADD GroupsIdsString text NULL"),
            new DatabaseCommand(44, 21, "ALTER TABLE GroupFilter ADD GroupConditionsVersion int NOT NULL DEFAULT 0"),
            new DatabaseCommand(44, 22, "ALTER TABLE GroupFilter ADD GroupConditions text NULL"),
            new DatabaseCommand(44, 23, "ALTER TABLE GroupFilter ADD ParentGroupFilterID int NULL"),
            new DatabaseCommand(44, 24, "ALTER TABLE GroupFilter ADD InvisibleInClients int NOT NULL DEFAULT 0"),
            new DatabaseCommand(44, 25, "ALTER TABLE GroupFilter ADD SeriesIdsVersion int NOT NULL DEFAULT 0"),
            new DatabaseCommand(44, 26, "ALTER TABLE GroupFilter ADD SeriesIdsString text NULL"),
            new DatabaseCommand(44, 27, "ALTER TABLE AnimeEpisode ADD PlexContractVersion int NOT NULL DEFAULT 0"),
            new DatabaseCommand(44, 28, "ALTER TABLE AnimeEpisode ADD PlexContractBlob BLOB NULL"),
            new DatabaseCommand(44, 29, "ALTER TABLE AnimeEpisode ADD PlexContractSize int NOT NULL DEFAULT 0"),
            new DatabaseCommand(44, 30, "ALTER TABLE AnimeEpisode_User ADD ContractVersion int NOT NULL DEFAULT 0"),
            new DatabaseCommand(44, 31, "ALTER TABLE AnimeEpisode_User ADD ContractBlob BLOB NULL"),
            new DatabaseCommand(44, 32, "ALTER TABLE AnimeEpisode_User ADD ContractSize int NOT NULL DEFAULT 0"),
            new DatabaseCommand(44, 33, "ALTER TABLE VideoLocal ADD MediaVersion int NOT NULL DEFAULT 0"),
            new DatabaseCommand(44, 34, "ALTER TABLE VideoLocal ADD MediaBlob BLOB NULL"),
            new DatabaseCommand(44, 35, "ALTER TABLE VideoLocal ADD MediaSize int NOT NULL DEFAULT 0"),
            new DatabaseCommand(45, 1, DatabaseFixes.DeleteSerieUsersWithoutSeries),
            new DatabaseCommand(46, 1,
                "CREATE TABLE VideoLocal_Place ( VideoLocal_Place_ID INTEGER PRIMARY KEY AUTOINCREMENT,VideoLocalID int NOT NULL, FilePath text NOT NULL,  ImportFolderID int NOT NULL, ImportFolderType int NOT NULL )"),
            new DatabaseCommand(46, 2,
                "CREATE UNIQUE INDEX [UIX_VideoLocal_ VideoLocal_Place_ID] ON [VideoLocal_Place] ([VideoLocal_Place_ID]);"),
            new DatabaseCommand(46, 3,
                "INSERT INTO VideoLocal_Place (VideoLocalID, FilePath, ImportFolderID, ImportFolderType) SELECT VideoLocalID, FilePath, ImportFolderID, 1 as ImportFolderType FROM VideoLocal"),
            new DatabaseCommand(46, 4, DropVideolocalColumns),
            new DatabaseCommand(46, 5,
                "UPDATE VideoLocal SET FileName=(SELECT FileName FROM VideoInfo WHERE VideoInfo.Hash=VideoLocal.Hash), VideoCodec=(SELECT VideoCodec FROM VideoInfo WHERE VideoInfo.Hash=VideoLocal.Hash), VideoBitrate=(SELECT VideoBitrate FROM VideoInfo WHERE VideoInfo.Hash=VideoLocal.Hash), VideoBitDepth=(SELECT VideoBitDepth FROM VideoInfo WHERE VideoInfo.Hash=VideoLocal.Hash), VideoFrameRate=(SELECT VideoFrameRate FROM VideoInfo WHERE VideoInfo.Hash=VideoLocal.Hash), VideoResolution=(SELECT VideoResolution FROM VideoInfo WHERE VideoInfo.Hash=VideoLocal.Hash), AudioCodec=(SELECT AudioCodec FROM VideoInfo WHERE VideoInfo.Hash=VideoLocal.Hash), AudioBitrate=(SELECT AudioBitrate FROM VideoInfo WHERE VideoInfo.Hash=VideoLocal.Hash), Duration=(SELECT Duration FROM VideoInfo WHERE VideoInfo.Hash=VideoLocal.Hash) WHERE RowId IN (SELECT RowId FROM VideoInfo WHERE VideoInfo.Hash=VideoLocal.Hash)"),
            new DatabaseCommand(46, 6,
                "CREATE TABLE CloudAccount (CloudID INTEGER PRIMARY KEY AUTOINCREMENT, ConnectionString text NOT NULL, Provider text NOT NULL, Name text NOT NULL);"),
            new DatabaseCommand(46, 7, "CREATE UNIQUE INDEX [UIX_CloudAccount_CloudID] ON [CloudAccount] ([CloudID]);"),
            new DatabaseCommand(46, 8, "ALTER TABLE ImportFolder ADD CloudID int NULL"),
            new DatabaseCommand(46, 9, "DROP TABLE VideoInfo"),
            new DatabaseCommand(46, 10, AlterVideoLocalUser),
            new DatabaseCommand(47, 1, "DROP INDEX UIX2_VideoLocal_Hash;"),
            new DatabaseCommand(47, 2, "CREATE INDEX IX_VideoLocal_Hash ON VideoLocal(Hash);"),
            new DatabaseCommand(48, 1,
                "CREATE TABLE AuthTokens ( AuthID INTEGER PRIMARY KEY AUTOINCREMENT, UserID int NOT NULL, DeviceName text NOT NULL, Token text NOT NULL )"),
            new DatabaseCommand(49, 1,
                "CREATE TABLE Scan ( ScanID INTEGER PRIMARY KEY AUTOINCREMENT, CreationTime timestamp NOT NULL, ImportFolders text NOT NULL, Status int NOT NULL )"),
            new DatabaseCommand(49, 2,
                "CREATE TABLE ScanFile ( ScanFileID INTEGER PRIMARY KEY AUTOINCREMENT, ScanID int NOT NULL, ImportFolderID int NOT NULL, VideoLocal_Place_ID int NOT NULL, FullName text NOT NULL, FileSize bigint NOT NULL, Status int NOT NULL, CheckDate timestamp NULL, Hash text NOT NULL, HashResult text NULL )"),
            new DatabaseCommand(49, 3, "CREATE INDEX UIX_ScanFileStatus ON ScanFile(ScanID,Status,CheckDate);"),
            new DatabaseCommand(50, 1, DatabaseFixes.FixTagsWithInclude),
            new DatabaseCommand(51, 1, DatabaseFixes.MakeYearsApplyToSeries),
            new DatabaseCommand(52, 1, DatabaseFixes.FixEmptyVideoInfos),
            new DatabaseCommand(53, 1, "ALTER TABLE JMMUser ADD PlexToken text NULL"),
            new DatabaseCommand(54, 1, "ALTER TABLE AniDB_File ADD IsChaptered INT NOT NULL DEFAULT -1"),
            new DatabaseCommand(55, 1, "ALTER TABLE RenameScript ADD RenamerType TEXT NOT NULL DEFAULT 'Legacy'"),
            new DatabaseCommand(55, 2, "ALTER TABLE RenameScript ADD ExtraData TEXT"),
            new DatabaseCommand(56, 1,
                "CREATE INDEX IX_AniDB_Anime_Character_CharID ON AniDB_Anime_Character(CharID);"),
            // This adds the new columns `AirDate` and `Rating` as well
            new DatabaseCommand(57, 1, "DROP INDEX UIX_TvDB_Episode_Id;"),
            new DatabaseCommand(57, 2, DropTvDB_EpisodeFirstAiredColumn),
            new DatabaseCommand(57, 3, DatabaseFixes.UpdateAllTvDBSeries),
            new DatabaseCommand(58, 1, "ALTER TABLE AnimeSeries ADD AirsOn TEXT NULL"),
            new DatabaseCommand(59, 1, "DROP TABLE Trakt_ImageFanart"),
            new DatabaseCommand(59, 2, "DROP TABLE Trakt_ImagePoster"),
            new DatabaseCommand(60, 1,
                "CREATE TABLE AnimeCharacter ( CharacterID INTEGER PRIMARY KEY AUTOINCREMENT, AniDBID INTEGER NOT NULL, Name TEXT NOT NULL, AlternateName TEXT NULL, Description TEXT NULL, ImagePath TEXT NULL )"),
            new DatabaseCommand(60, 2,
                "CREATE TABLE AnimeStaff ( StaffID INTEGER PRIMARY KEY AUTOINCREMENT, AniDBID INTEGER NOT NULL, Name TEXT NOT NULL, AlternateName TEXT NULL, Description TEXT NULL, ImagePath TEXT NULL )"),
            new DatabaseCommand(60, 3,
                "CREATE TABLE CrossRef_Anime_Staff ( CrossRef_Anime_StaffID INTEGER PRIMARY KEY AUTOINCREMENT, AniDB_AnimeID INTEGER NOT NULL, StaffID INTEGER NOT NULL, Role TEXT NULL, RoleID INTEGER, RoleType INTEGER NOT NULL, Language TEXT NOT NULL )"),
            new DatabaseCommand(60, 4, DatabaseFixes.PopulateCharactersAndStaff),
            new DatabaseCommand(61, 1, "ALTER TABLE MovieDB_Movie ADD Rating INT NOT NULL DEFAULT 0"),
            new DatabaseCommand(61, 2, "ALTER TABLE TvDB_Series ADD Rating INT NULL"),
            new DatabaseCommand(62, 1, "ALTER TABLE AniDB_Episode ADD Description TEXT NOT NULL DEFAULT ''"),
            new DatabaseCommand(62, 2, DatabaseFixes.FixCharactersWithGrave),
            new DatabaseCommand(63, 1, DatabaseFixes.RefreshAniDBInfoFromXML),
            new DatabaseCommand(64, 1, DatabaseFixes.MakeTagsApplyToSeries),
            new DatabaseCommand(64, 2, Importer.UpdateAllStats),
            new DatabaseCommand(65, 1, DatabaseFixes.RemoveBasePathsFromStaffAndCharacters),
            new DatabaseCommand(66, 1,
                "CREATE TABLE AniDB_AnimeUpdate ( AniDB_AnimeUpdateID INTEGER PRIMARY KEY AUTOINCREMENT, AnimeID INTEGER NOT NULL, UpdatedAt timestamp NOT NULL )"),
            new DatabaseCommand(66, 2, "CREATE UNIQUE INDEX UIX_AniDB_AnimeUpdate ON AniDB_AnimeUpdate(AnimeID)"),
            new DatabaseCommand(66, 3, DatabaseFixes.MigrateAniDB_AnimeUpdates),
            new DatabaseCommand(67, 1, DatabaseFixes.RemoveBasePathsFromStaffAndCharacters),
            new DatabaseCommand(68, 1, DatabaseFixes.FixDuplicateTagFiltersAndUpdateSeasons),
            new DatabaseCommand(69, 1, DatabaseFixes.RecalculateYears),
            new DatabaseCommand(70, 1, "DROP INDEX UIX_CrossRef_AniDB_MAL_Anime;"),
            new DatabaseCommand(70, 2, "ALTER TABLE AniDB_Anime ADD Site_JP TEXT NULL"),
            new DatabaseCommand(70, 3, "ALTER TABLE AniDB_Anime ADD Site_EN TEXT NULL"),
            new DatabaseCommand(70, 4, "ALTER TABLE AniDB_Anime ADD Wikipedia_ID TEXT NULL"),
            new DatabaseCommand(70, 5, "ALTER TABLE AniDB_Anime ADD WikipediaJP_ID TEXT NULL"),
            new DatabaseCommand(70, 6, "ALTER TABLE AniDB_Anime ADD SyoboiID INT NULL"),
            new DatabaseCommand(70, 7, "ALTER TABLE AniDB_Anime ADD AnisonID INT NULL"),
            new DatabaseCommand(70, 8, "ALTER TABLE AniDB_Anime ADD CrunchyrollID TEXT NULL"),
            new DatabaseCommand(70, 9, DatabaseFixes.PopulateResourceLinks),
            new DatabaseCommand(71, 1, "ALTER TABLE VideoLocal ADD MyListID INT NOT NULL DEFAULT 0"),
            new DatabaseCommand(71, 2, DatabaseFixes.PopulateMyListIDs),
            new DatabaseCommand(72, 1, DropAniDB_EpisodeTitles),
            new DatabaseCommand(72, 2,
                "CREATE TABLE AniDB_Episode_Title ( AniDB_Episode_TitleID INTEGER PRIMARY KEY AUTOINCREMENT, AniDB_EpisodeID int NOT NULL, Language text NOT NULL, Title text NOT NULL ); "),
            new DatabaseCommand(72, 3, DatabaseFixes.DummyMigrationOfObsoletion),
            new DatabaseCommand(73, 1, "DROP INDEX UIX_CrossRef_AniDB_TvDB_Episode_AniDBEpisodeID;"),
            // SQLite is stupid, so we need to create a new table and copy the contents to it
            new DatabaseCommand(73, 2, RenameCrossRef_AniDB_TvDB_Episode),
            // For some reason, this was never dropped
            new DatabaseCommand(73, 3, "DROP TABLE CrossRef_AniDB_TvDB;"),
            new DatabaseCommand(73, 4,
                "CREATE TABLE CrossRef_AniDB_TvDB(CrossRef_AniDB_TvDBID INTEGER PRIMARY KEY AUTOINCREMENT, AniDBID int NOT NULL, TvDBID int NOT NULL, CrossRefSource INT NOT NULL);"),
            new DatabaseCommand(73, 5,
                "CREATE UNIQUE INDEX UIX_AniDB_TvDB_AniDBID_TvDBID ON CrossRef_AniDB_TvDB(AniDBID,TvDBID);"),
            new DatabaseCommand(73, 6,
                "CREATE TABLE CrossRef_AniDB_TvDB_Episode(CrossRef_AniDB_TvDB_EpisodeID INTEGER PRIMARY KEY AUTOINCREMENT, AniDBEpisodeID int NOT NULL, TvDBEpisodeID int NOT NULL, MatchRating INT NOT NULL);"),
            new DatabaseCommand(73, 7,
                "CREATE UNIQUE INDEX UIX_CrossRef_AniDB_TvDB_Episode_AniDBID_TvDBID ON CrossRef_AniDB_TvDB_Episode(AniDBEpisodeID,TvDBEpisodeID);"),
            new DatabaseCommand(73, 9, DatabaseFixes.MigrateTvDBLinks_v2_to_V3),
            // DatabaseFixes.MigrateTvDBLinks_v2_to_V3() drops the CrossRef_AniDB_TvDBV2 table. We do it after init to migrate
            new DatabaseCommand(74, 1, DatabaseFixes.FixAniDB_EpisodesWithMissingTitles),
            new DatabaseCommand(75, 1, DatabaseFixes.RegenTvDBMatches),
            new DatabaseCommand(76, 1,
                "ALTER TABLE AnimeSeries ADD UpdatedAt timestamp NOT NULL default '2000-01-01 00:00:00'"),
            new DatabaseCommand(77, 1, DatabaseFixes.MigrateAniDBToNet),
            new DatabaseCommand(78, 1, DropVideoLocal_Media),
            new DatabaseCommand(79, 1, "DROP INDEX IF EXISTS UIX_CrossRef_AniDB_MAL_MALID;"),
            new DatabaseCommand(79, 1, "DROP INDEX IF EXISTS UIX_CrossRef_AniDB_MAL_MALID;"),
            new DatabaseCommand(80, 1, "DROP INDEX IF EXISTS UIX_AniDB_File_FileID;"),
            new DatabaseCommand(81, 1, "CREATE TABLE AniDB_Anime_Staff ( AniDB_Anime_StaffID INTEGER PRIMARY KEY AUTOINCREMENT, AnimeID INTEGER NOT NULL, CreatorID INTEGER NOT NULL, CreatorType TEXT NOT NULL );"),
            new DatabaseCommand(81, 2, DatabaseFixes.RefreshAniDBInfoFromXML),
            new DatabaseCommand(82, 1, DatabaseFixes.EnsureNoOrphanedGroupsOrSeries),
            new DatabaseCommand(83, 1, "UPDATE VideoLocal_User SET WatchedDate = NULL WHERE WatchedDate = '1970-01-01 00:00:00';"),
            new DatabaseCommand(83, 2, "ALTER TABLE VideoLocal_User ADD WatchedCount INT NOT NULL DEFAULT 0;"),
            new DatabaseCommand(83, 3, "ALTER TABLE VideoLocal_User ADD LastUpdated timestamp NOT NULL DEFAULT '2000-01-01 00:00:00';"),
            new DatabaseCommand(83, 4, "UPDATE VideoLocal_User SET WatchedCount = 1, LastUpdated = WatchedDate WHERE WatchedDate IS NOT NULL;"),
            new DatabaseCommand(84, 1, "ALTER TABLE AnimeSeries_User ADD LastEpisodeUpdate timestamp DEFAULT NULL;"),
            new DatabaseCommand(84, 2, DatabaseFixes.FixWatchDates),
            new DatabaseCommand(85, 1, "ALTER TABLE AnimeGroup ADD MainAniDBAnimeID INT DEFAULT NULL;"),
        };

        private static Tuple<bool, string> DropVideoLocal_Media(object connection)
        {
            try
            {
                SQLiteConnection myConn = (SQLiteConnection) connection;
                string createvlcommand =
                    "CREATE TABLE VideoLocal ( VideoLocalID INTEGER PRIMARY KEY AUTOINCREMENT, Hash text NOT NULL, CRC32 text NULL, MD5 text NULL, SHA1 text NULL, HashSource int NOT NULL, FileSize INTEGER NOT NULL, IsIgnored int NOT NULL, DateTimeUpdated timestamp NOT NULL, FileName text NOT NULL DEFAULT '', DateTimeCreated timestamp NULL, IsVariation int NULL,MediaVersion int NOT NULL DEFAULT 0,MediaBlob BLOB NULL,MediaSize int NOT NULL DEFAULT 0, MyListID INT NOT NULL DEFAULT 0);";
                List<string> indexvlcommands =
                    new List<string> {"CREATE UNIQUE INDEX UIX2_VideoLocal_Hash on VideoLocal(Hash)"};
                ((SQLite) DatabaseFactory.Instance).DropColumns(myConn, "VideoLocal",
                    new List<string>
                    {
                        "VideoCodec", "AudioCodec", "VideoBitrate", "AudioBitrate", "VideoBitDepth", "VideoFrameRate",
                        "VideoResolution", "Duration"
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
                SQLiteConnection myConn = (SQLiteConnection) connection;
                string createcommand =
                    "create table AniDB_Episode ( AniDB_EpisodeID integer primary key autoincrement, EpisodeID int not null, AnimeID int not null, LengthSeconds int not null, Rating text not null, Votes text not null, EpisodeNumber int not null, EpisodeType int not null, AirDate int not null, DateTimeUpdated datetime not null, Description text default '' not null )";
                List<string> indexcommands = new List<string>
                {
                    "create index IX_AniDB_Episode_AnimeID on AniDB_Episode (AnimeID)",
                    "create unique index UIX_AniDB_Episode_EpisodeID on AniDB_Episode (EpisodeID)"
                };
                ((SQLite) DatabaseFactory.Instance).DropColumns(myConn, "AniDB_Episode",
                    new List<string> {"EnglishName", "RomajiName"}, createcommand, indexcommands);
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
                // I'm doing this manually to save time
                SQLiteConnection myConn = (SQLiteConnection) connection;;

                // make the new one
                // create indexes
                // transfer data
                // drop old table
                List<string> cmds = new List<string>
                {
                    "CREATE TABLE CrossRef_AniDB_TvDB_Episode_Override( CrossRef_AniDB_TvDB_Episode_OverrideID INTEGER PRIMARY KEY AUTOINCREMENT, AniDBEpisodeID int NOT NULL, TvDBEpisodeID int NOT NULL );",
                    "CREATE UNIQUE INDEX UIX_AniDB_TvDB_Episode_Override_AniDBEpisodeID_TvDBEpisodeID ON CrossRef_AniDB_TvDB_Episode_Override(AniDBEpisodeID,TvDBEpisodeID);",
                    "INSERT INTO CrossRef_AniDB_TvDB_Episode_Override ( AniDBEpisodeID, TvDBEpisodeID ) SELECT AniDBEpisodeID, TvDBEpisodeID FROM CrossRef_AniDB_TvDB_Episode; ",
                    "DROP TABLE CrossRef_AniDB_TvDB_Episode;"
                };
                foreach (string cmdTable in cmds)
                {
                    ((SQLite) DatabaseFactory.Instance).Execute(myConn, cmdTable);
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
                SQLiteConnection myConn = (SQLiteConnection) connection;
                string createcommand =
                    "CREATE TABLE AniDB_Anime ( AniDB_AnimeID INTEGER PRIMARY KEY AUTOINCREMENT, AnimeID int NOT NULL, EpisodeCount int NOT NULL, AirDate timestamp NULL, EndDate timestamp NULL, URL text NULL, Picname text NULL, BeginYear int NOT NULL, EndYear int NOT NULL, AnimeType int NOT NULL, MainTitle text NOT NULL, AllTitles text NOT NULL, AllTags text NOT NULL, Description text NOT NULL, EpisodeCountNormal int NOT NULL, EpisodeCountSpecial int NOT NULL, Rating int NOT NULL, VoteCount int NOT NULL, TempRating int NOT NULL, TempVoteCount int NOT NULL, AvgReviewRating int NOT NULL, ReviewCount int NOT NULL, DateTimeUpdated timestamp NOT NULL, DateTimeDescUpdated timestamp NOT NULL, ImageEnabled int NOT NULL, AwardList text NOT NULL, Restricted int NOT NULL, AnimePlanetID int NULL, ANNID int NULL, AllCinemaID int NULL, AnimeNfo int NULL, LatestEpisodeNumber int NULL, DisableExternalLinksFlag int NULL );";
                List<string> indexcommands = new List<string>
                {
                    "CREATE UNIQUE INDEX [UIX2_AniDB_Anime_AnimeID] ON [AniDB_Anime] ([AnimeID]);"
                };
                ((SQLite) DatabaseFactory.Instance).DropColumns(myConn, "AniDB_Anime",
                    new List<string> {"AllCategories"}, createcommand, indexcommands);
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
                SQLiteConnection myConn = (SQLiteConnection) connection;
                string createvlcommand =
                    "CREATE TABLE VideoLocal ( VideoLocalID INTEGER PRIMARY KEY AUTOINCREMENT, Hash text NOT NULL, CRC32 text NULL, MD5 text NULL, SHA1 text NULL, HashSource int NOT NULL, FileSize INTEGER NOT NULL, IsIgnored int NOT NULL, DateTimeUpdated timestamp NOT NULL, FileName text NOT NULL DEFAULT '', VideoCodec text NOT NULL DEFAULT '', VideoBitrate text NOT NULL DEFAULT '',VideoBitDepth text NOT NULL DEFAULT '',VideoFrameRate text NOT NULL DEFAULT '',VideoResolution text NOT NULL DEFAULT '',AudioCodec text NOT NULL DEFAULT '',AudioBitrate text NOT NULL DEFAULT '',Duration INTEGER NOT NULL DEFAULT 0,DateTimeCreated timestamp NULL, IsVariation int NULL,MediaVersion int NOT NULL DEFAULT 0,MediaBlob BLOB NULL,MediaSize int NOT NULL DEFAULT 0 );";
                List<string> indexvlcommands =
                    new List<string> {"CREATE UNIQUE INDEX UIX2_VideoLocal_Hash on VideoLocal(Hash)"};
                ((SQLite) DatabaseFactory.Instance).DropColumns(myConn, "VideoLocal",
                    new List<string> {"FilePath", "ImportFolderID"}, createvlcommand, indexvlcommands);
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
                SQLiteConnection myConn = (SQLiteConnection) connection;
                string createtvepcommand =
                    "CREATE TABLE TvDB_Episode ( TvDB_EpisodeID INTEGER PRIMARY KEY AUTOINCREMENT, Id int NOT NULL, SeriesID int NOT NULL, SeasonID int NOT NULL, SeasonNumber int NOT NULL, EpisodeNumber int NOT NULL, EpisodeName text, Overview text, Filename text, EpImgFlag int NOT NULL, AbsoluteNumber int, AirsAfterSeason int, AirsBeforeEpisode int, AirsBeforeSeason int, AirDate timestamp, Rating int)";
                List<string> indextvepcommands =
                    new List<string> {"CREATE UNIQUE INDEX UIX_TvDB_Episode_Id ON TvDB_Episode(Id);"};
                ((SQLite) DatabaseFactory.Instance).DropColumns(myConn, "TvDB_Episode",
                    new List<string> {"FirstAired"}, createtvepcommand, indextvepcommands);
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
                SQLiteConnection myConn = (SQLiteConnection) connection;
                string createvluser =
                    "CREATE TABLE VideoLocal_User ( VideoLocal_UserID INTEGER PRIMARY KEY AUTOINCREMENT, JMMUserID int NOT NULL, VideoLocalID int NOT NULL, WatchedDate timestamp NULL, ResumePosition bigint NOT NULL DEFAULT 0); ";
                List<string> indexvluser = new List<string>
                {
                    "CREATE UNIQUE INDEX UIX2_VideoLocal_User_User_VideoLocalID ON VideoLocal_User(JMMUserID, VideoLocalID);"
                };
                ((SQLite) DatabaseFactory.Instance).Alter(myConn, "VideoLocal_User", createvluser, indexvluser);
                return new Tuple<bool, string>(true, null);
            }
            catch (Exception e)
            {
                return new Tuple<bool, string>(false, e.ToString());
            }
        }


        //WE NEED TO DROP SOME SQL LITE COLUMNS...

        private void DropColumns(SQLiteConnection db, string tableName, List<string> colsToRemove, string createcommand,
            List<string> indexcommands)
        {
            List<string> updatedTableColumns = GetTableColumns(db, tableName);
            colsToRemove.ForEach(a => updatedTableColumns.Remove(a));
            string columnsSeperated = string.Join(",", updatedTableColumns);

            // Drop indexes first. We can get them from the create commands
            // Ignore if they don't exist
            foreach (string indexcommand in indexcommands)
            {
                int position = indexcommand.IndexOf("index", StringComparison.InvariantCultureIgnoreCase) + 6;
                string indexname = indexcommand.Substring(position);
                position = indexname.IndexOf(' ');
                indexname = indexname.Substring(0, position);
                indexname = "DROP INDEX " + indexname + ";";
                try
                {
                    Execute(db, indexname);
                }
                catch
                {
                }
            }

            // Rename table to old
            // make the new one
            // recreate indexes
            // transfer data
            // drop old table
            List<string> cmds = new List<string>
            {
                "ALTER TABLE " + tableName + " RENAME TO " + tableName + "_old;",
                createcommand
            };
            cmds.AddRange(indexcommands);
            cmds.Add("INSERT INTO " + tableName + " (" + columnsSeperated + ") SELECT " + columnsSeperated + " FROM " +
                     tableName + "_old; ");
            cmds.Add("DROP TABLE " + tableName + "_old;");
            foreach (string cmdTable in cmds)
            {
                Execute(db, cmdTable);
            }
        }

        private void Alter(SQLiteConnection db, string tableName, string createcommand, List<string> indexcommands)
        {
            List<string> updatedTableColumns = GetTableColumns(db, tableName);
            string columnsSeperated = string.Join(",", updatedTableColumns);
            List<string> cmds = new List<string>
            {
                "ALTER TABLE " + tableName + " RENAME TO " + tableName + "_old;",
                createcommand
            };
            cmds.AddRange(indexcommands);
            cmds.Add("INSERT INTO " + tableName + " (" + columnsSeperated + ") SELECT " + columnsSeperated + " FROM " +
                     tableName + "_old; ");
            cmds.Add("DROP TABLE " + tableName + "_old;");
            foreach (string cmdTable in cmds)
            {
                Execute(db, cmdTable);
            }
        }

        private List<string> GetTableColumns(SQLiteConnection conn, string tableName)
        {
            string cmd = "pragma table_info(" + tableName + ")";
            List<string> columns = new List<string>();
            foreach (object o in ExecuteReader(conn, cmd))
            {
                object[] oo = (object[]) o;
                columns.Add((string) oo[1]);
            }
            return columns;
        }

        protected override Tuple<bool, string> ExecuteCommand(SQLiteConnection connection, string command)
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

        protected override void Execute(SQLiteConnection connection, string command)
        {
            using (SQLiteCommand sqCommand = new SQLiteCommand(command, connection))
            {
                sqCommand.CommandTimeout = 0;
                sqCommand.ExecuteNonQuery();
            }
        }

        protected override long ExecuteScalar(SQLiteConnection connection, string command)
        {
            using (SQLiteCommand sqCommand = new SQLiteCommand(command, connection))
            {
                sqCommand.CommandTimeout = 0;
                return long.Parse(sqCommand.ExecuteScalar().ToString());
            }
        }

        protected override ArrayList ExecuteReader(SQLiteConnection connection, string command)
        {
            using (SQLiteCommand sqCommand = new SQLiteCommand(command, connection))
            {
                ArrayList rows = new ArrayList();
                sqCommand.CommandTimeout = 0;
                using (SQLiteDataReader reader = sqCommand.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        object[] values = new object[reader.FieldCount];
                        reader.GetValues(values);
                        rows.Add(values);
                    }
                    reader.Close();
                    return rows;
                }
            }
        }

        protected override void ConnectionWrapper(string connectionstring, Action<SQLiteConnection> action)
        {
            using (SQLiteConnection con = new SQLiteConnection(connectionstring))
            {
                con.Open();
                action(con);
            }
        }

        public void CreateAndUpdateSchema()
        {
            ConnectionWrapper(GetConnectionString(), myConn =>
            {
                bool create = (ExecuteScalar(myConn,
                    "SELECT count(*) as NumTables FROM sqlite_master WHERE name='Versions'") == 0);
                if (create)
                {
                    ServerState.Instance.ServerStartingStatus = Resources.Database_CreateSchema;
                    ExecuteWithException(myConn, createVersionTable);
                }

                if (!GetTableColumns(myConn, "Versions").Contains("VersionRevision"))
                {
                    ExecuteWithException(myConn, updateVersionTable);
                    AllVersions = RepoFactory.Versions.GetAllByType(Constants.DatabaseTypeKey);
                }
                PreFillVersions(createTables.Union(patchCommands));
                if (create)
                    ExecuteWithException(myConn, createTables);
                ServerState.Instance.ServerStartingStatus = Resources.Database_ApplySchema;
                ExecuteWithException(myConn, patchCommands);
            });
        }
    }
}