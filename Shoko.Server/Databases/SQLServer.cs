using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using FluentNHibernate.Cfg;
using FluentNHibernate.Cfg.Db;
using MessagePack;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using NHibernate;
using NHibernate.AdoNet;
using NHibernate.Driver;
using Shoko.Plugin.Abstractions;
using Shoko.Server.Databases.NHibernate;
using Shoko.Server.Extensions;
using Shoko.Server.Models;
using Shoko.Server.Renamer;
using Shoko.Server.Repositories;
using Shoko.Server.Server;
using Shoko.Server.Utilities;

// ReSharper disable InconsistentNaming

namespace Shoko.Server.Databases;

public class SQLServer : BaseDatabase<SqlConnection>
{
    public override string Name { get; } = "SQLServer";
    public override int RequiredVersion { get; } = 147;

    public override void BackupDatabase(string fullfilename)
    {
        fullfilename = Path.GetFileName(fullfilename) + ".bak";
        //TODO We cannot write the backup anywhere, because
        //1) The server could be elsewhere,
        //2) The SqlServer running account should have read write access to our backup dir which is nono
        // So we backup in the default SQL SERVER BACKUP DIRECTORY.

        var settings = Utils.SettingsProvider.GetSettings();
        var cmd = "BACKUP DATABASE[" + settings.Database.Schema + "] TO DISK = '" +
                  fullfilename.Replace("'", "''") + "'";


        using var tmpConn = new SqlConnection(GetConnectionString());
        tmpConn.Open();

        using var command = new SqlCommand(cmd, tmpConn);
        command.CommandTimeout = 0;
        command.ExecuteNonQuery();
    }

    public override bool TestConnection()
    {
        try
        {
            using var connection = new SqlConnection(GetTestConnectionString());
            const string query = "select 1";
            var command = new SqlCommand(query, connection);
            connection.Open();
            command.ExecuteScalar();
            return true;
        }
        catch
        {
            // ignored
        }
        return false;
    }

    public override string GetTestConnectionString()
    {
        var settings = Utils.SettingsProvider.GetSettings();
        // we are assuming that if you have overridden the connection string, you know what you're doing, and have set up the database and perms
        if (!string.IsNullOrWhiteSpace(settings.Database.OverrideConnectionString))
            return settings.Database.OverrideConnectionString;
        return $"data source={settings.Database.Hostname},{settings.Database.Port};Initial Catalog=master;user id={settings.Database.Username};password={settings.Database.Password};persist security info=True;MultipleActiveResultSets=True;TrustServerCertificate=True";
    }

    public override string GetConnectionString()
    {
        var settings = Utils.SettingsProvider.GetSettings();
        if (!string.IsNullOrWhiteSpace(settings.Database.OverrideConnectionString))
            return settings.Database.OverrideConnectionString;
        return
            $"data source={settings.Database.Hostname},{settings.Database.Port};Initial Catalog={settings.Database.Schema};user id={settings.Database.Username};password={settings.Database.Password};persist security info=True;MultipleActiveResultSets=True;TrustServerCertificate=True";
    }

    public override ISessionFactory CreateSessionFactory()
    {
        var settings = Utils.SettingsProvider.GetSettings();
        return Fluently.Configure()
            .Database(MsSqlConfiguration.MsSql2012.Driver<MicrosoftDataSqlClientDriver>().ConnectionString(GetConnectionString()))
            .Mappings(m => m.FluentMappings.AddFromAssemblyOf<ShokoServer>())
            .ExposeConfiguration(c => c.DataBaseIntegration(prop =>
            {
                prop.Batcher<NonBatchingBatcherFactory>();
                prop.BatchSize = 0;
                prop.LogSqlInConsole = settings.Database.LogSqlInConsole;
            }).SetInterceptor(new NHibernateDependencyInjector(Utils.ServiceContainer)))
            .BuildSessionFactory();
    }

    public override bool DatabaseAlreadyExists()
    {
        var settings = Utils.SettingsProvider.GetSettings();
        var cmd = $"Select count(*) from sysdatabases where name = '{settings.Database.Schema}'";
        using var tmpConn = new SqlConnection(GetTestConnectionString());
        tmpConn.Open();
        var count = ExecuteScalar(tmpConn, cmd);

        // if the Versions already exists, it means we have done this already
        return count > 0;
    }

    public override void CreateDatabase()
    {
        if (DatabaseAlreadyExists()) return;

        var settings = Utils.SettingsProvider.GetSettings();
        var cmd = $"CREATE DATABASE {settings.Database.Schema}";
        using var connection = new SqlConnection(GetTestConnectionString());
        var command = new SqlCommand(cmd, connection);
        connection.Open();
        command.CommandTimeout = int.MaxValue;
        command.ExecuteNonQuery();
    }

    public override bool HasVersionsTable()
    {
        const string cmd = "SELECT Count(1) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Versions'";
        using var connection = new SqlConnection(GetConnectionString());
        var command = new SqlCommand(cmd, connection);
        connection.Open();
        var count = (int) (command.ExecuteScalar() ?? 0);
        return count > 0;
    }

    private List<DatabaseCommand> createVersionTable = new()
    {
        new DatabaseCommand(0, 1,
            "CREATE TABLE [Versions]( [VersionsID] [int] IDENTITY(1,1) NOT NULL, [VersionType] [varchar](100) NOT NULL, [VersionValue] [varchar](100) NOT NULL,  CONSTRAINT [PK_Versions] PRIMARY KEY CLUSTERED  ( [VersionsID] ASC )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] ) ON [PRIMARY] "),
        new DatabaseCommand(0, 2, "CREATE UNIQUE INDEX UIX_Versions_VersionType ON Versions(VersionType)"),
    };

    private List<DatabaseCommand> createTables = new()
    {
        new DatabaseCommand(1, 1,
            "CREATE TABLE AniDB_Anime( AniDB_AnimeID int IDENTITY(1,1) NOT NULL, AnimeID int NOT NULL, EpisodeCount int NOT NULL, AirDate datetime NULL, EndDate datetime NULL, URL varchar(max) NULL, Picname varchar(max) NULL, BeginYear int NOT NULL, EndYear int NOT NULL, AnimeType int NOT NULL, MainTitle nvarchar(500) NOT NULL, AllTitles nvarchar(1500) NOT NULL, AllCategories nvarchar(MAX) NOT NULL, AllTags nvarchar(MAX) NOT NULL, Description varchar(max) NOT NULL, EpisodeCountNormal int NOT NULL, EpisodeCountSpecial int NOT NULL, Rating int NOT NULL, VoteCount int NOT NULL, TempRating int NOT NULL, TempVoteCount int NOT NULL, AvgReviewRating int NOT NULL, ReviewCount int NOT NULL, DateTimeUpdated datetime NOT NULL, DateTimeDescUpdated datetime NOT NULL, ImageEnabled int NOT NULL, AwardList varchar(max) NOT NULL, Restricted int NOT NULL, AnimePlanetID int NULL, ANNID int NULL, AllCinemaID int NULL, AnimeNfo int NULL, [LatestEpisodeNumber] [int] NULL, CONSTRAINT [PK_AniDB_Anime] PRIMARY KEY CLUSTERED  ( [AniDB_AnimeID] ASC )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] ) ON [PRIMARY] "),
        new DatabaseCommand(1, 2, "CREATE UNIQUE INDEX UIX_AniDB_Anime_AnimeID ON AniDB_Anime(AnimeID)"),
        new DatabaseCommand(1, 3,
            "CREATE TABLE AniDB_Anime_Category ( AniDB_Anime_CategoryID int IDENTITY(1,1) NOT NULL, AnimeID int NOT NULL, CategoryID int NOT NULL, Weighting int NOT NULL, CONSTRAINT [PK_AniDB_Anime_Category] PRIMARY KEY CLUSTERED  ( AniDB_Anime_CategoryID ASC )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] ) ON [PRIMARY] "),
        new DatabaseCommand(1, 4, "CREATE INDEX IX_AniDB_Anime_Category_AnimeID on AniDB_Anime_Category(AnimeID)"),
        new DatabaseCommand(1, 5,
            "CREATE UNIQUE INDEX UIX_AniDB_Anime_Category_AnimeID_CategoryID ON AniDB_Anime_Category(AnimeID, CategoryID)"),
        new DatabaseCommand(1, 6,
            "CREATE TABLE AniDB_Anime_Character ( AniDB_Anime_CharacterID int IDENTITY(1,1) NOT NULL, AnimeID int NOT NULL, CharID int NOT NULL, CharType varchar(100) NOT NULL, EpisodeListRaw varchar(max) NULL, CONSTRAINT [PK_AniDB_Anime_Character] PRIMARY KEY CLUSTERED  ( AniDB_Anime_CharacterID ASC )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] ) ON [PRIMARY] "),
        new DatabaseCommand(1, 7,
            "CREATE INDEX IX_AniDB_Anime_Character_AnimeID on AniDB_Anime_Character(AnimeID)"),
        new DatabaseCommand(1, 8,
            "CREATE UNIQUE INDEX UIX_AniDB_Anime_Character_AnimeID_CharID ON AniDB_Anime_Character(AnimeID, CharID)"),
        new DatabaseCommand(1, 9,
            "CREATE TABLE AniDB_Anime_Relation ( AniDB_Anime_RelationID int IDENTITY(1,1) NOT NULL, AnimeID int NOT NULL, RelatedAnimeID int NOT NULL, RelationType varchar(100) NOT NULL, CONSTRAINT [PK_AniDB_Anime_Relation] PRIMARY KEY CLUSTERED  ( AniDB_Anime_RelationID ASC )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] ) ON [PRIMARY] "),
        new DatabaseCommand(1, 10, "CREATE INDEX IX_AniDB_Anime_Relation_AnimeID on AniDB_Anime_Relation(AnimeID)"),
        new DatabaseCommand(1, 11,
            "CREATE UNIQUE INDEX UIX_AniDB_Anime_Relation_AnimeID_RelatedAnimeID ON AniDB_Anime_Relation(AnimeID, RelatedAnimeID)"),
        new DatabaseCommand(1, 12,
            "CREATE TABLE AniDB_Anime_Review ( AniDB_Anime_ReviewID int IDENTITY(1,1) NOT NULL, AnimeID int NOT NULL, ReviewID int NOT NULL, CONSTRAINT [PK_AniDB_Anime_Review] PRIMARY KEY CLUSTERED  ( AniDB_Anime_ReviewID ASC )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] ) ON [PRIMARY] "),
        new DatabaseCommand(1, 13, "CREATE INDEX IX_AniDB_Anime_Review_AnimeID on AniDB_Anime_Review(AnimeID)"),
        new DatabaseCommand(1, 14,
            "CREATE UNIQUE INDEX UIX_AniDB_Anime_Review_AnimeID_ReviewID ON AniDB_Anime_Review(AnimeID, ReviewID)"),
        new DatabaseCommand(1, 15,
            "CREATE TABLE AniDB_Anime_Similar ( AniDB_Anime_SimilarID int IDENTITY(1,1) NOT NULL, AnimeID int NOT NULL, SimilarAnimeID int NOT NULL, Approval int NOT NULL, Total int NOT NULL, CONSTRAINT [PK_AniDB_Anime_Similar] PRIMARY KEY CLUSTERED  ( AniDB_Anime_SimilarID ASC )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] ) ON [PRIMARY] "),
        new DatabaseCommand(1, 16, "CREATE INDEX IX_AniDB_Anime_Similar_AnimeID on AniDB_Anime_Similar(AnimeID)"),
        new DatabaseCommand(1, 17,
            "CREATE UNIQUE INDEX UIX_AniDB_Anime_Similar_AnimeID_SimilarAnimeID ON AniDB_Anime_Similar(AnimeID, SimilarAnimeID)"),
        new DatabaseCommand(1, 18,
            "CREATE TABLE AniDB_Anime_Tag ( AniDB_Anime_TagID int IDENTITY(1,1) NOT NULL, AnimeID int NOT NULL, TagID int NOT NULL, Approval int NOT NULL, CONSTRAINT [PK_AniDB_Anime_Tag] PRIMARY KEY CLUSTERED  ( AniDB_Anime_TagID ASC )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] ) ON [PRIMARY] "),
        new DatabaseCommand(1, 19, "CREATE INDEX IX_AniDB_Anime_Tag_AnimeID on AniDB_Anime_Tag(AnimeID)"),
        new DatabaseCommand(1, 20,
            "CREATE UNIQUE INDEX UIX_AniDB_Anime_Tag_AnimeID_TagID ON AniDB_Anime_Tag(AnimeID, TagID)"),
        new DatabaseCommand(1, 21,
            "CREATE TABLE [AniDB_Anime_Title]( AniDB_Anime_TitleID int IDENTITY(1,1) NOT NULL, AnimeID int NOT NULL, TitleType varchar(50) NOT NULL, Language nvarchar(50) NOT NULL, Title nvarchar(500) NOT NULL, CONSTRAINT [PK_AniDB_Anime_Title] PRIMARY KEY CLUSTERED  ( AniDB_Anime_TitleID ASC )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] ) ON [PRIMARY] "),
        new DatabaseCommand(1, 22, "CREATE INDEX IX_AniDB_Anime_Title_AnimeID on AniDB_Anime_Title(AnimeID)"),
        new DatabaseCommand(1, 23,
            "CREATE TABLE AniDB_Category ( AniDB_CategoryID int IDENTITY(1,1) NOT NULL, CategoryID int NOT NULL, ParentID int NOT NULL, IsHentai int NOT NULL, CategoryName varchar(50) NOT NULL, CategoryDescription varchar(max) NOT NULL, CONSTRAINT [PK_AniDB_Category] PRIMARY KEY CLUSTERED  ( AniDB_CategoryID ASC )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] ) ON [PRIMARY] "),
        new DatabaseCommand(1, 24,
            "CREATE UNIQUE INDEX UIX_AniDB_Category_CategoryID ON AniDB_Category(CategoryID)"),
        new DatabaseCommand(1, 25,
            "CREATE TABLE AniDB_Character ( AniDB_CharacterID int IDENTITY(1,1) NOT NULL, CharID int NOT NULL, CharName nvarchar(200) NOT NULL, PicName varchar(100) NOT NULL, CharKanjiName nvarchar(max) NOT NULL, CharDescription nvarchar(max) NOT NULL, CreatorListRaw varchar(max) NOT NULL, CONSTRAINT [PK_AniDB_Character] PRIMARY KEY CLUSTERED  ( AniDB_CharacterID ASC )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] ) ON [PRIMARY] "),
        new DatabaseCommand(1, 26, "CREATE UNIQUE INDEX UIX_AniDB_Character_CharID ON AniDB_Character(CharID)"),
        new DatabaseCommand(1, 27,
            "CREATE TABLE AniDB_Character_Seiyuu ( AniDB_Character_SeiyuuID int IDENTITY(1,1) NOT NULL, CharID int NOT NULL, SeiyuuID int NOT NULL CONSTRAINT [PK_AniDB_Character_Seiyuu] PRIMARY KEY CLUSTERED  ( AniDB_Character_SeiyuuID ASC )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] ) ON [PRIMARY] "),
        new DatabaseCommand(1, 28,
            "CREATE INDEX IX_AniDB_Character_Seiyuu_CharID on AniDB_Character_Seiyuu(CharID)"),
        new DatabaseCommand(1, 29,
            "CREATE INDEX IX_AniDB_Character_Seiyuu_SeiyuuID on AniDB_Character_Seiyuu(SeiyuuID)"),
        new DatabaseCommand(1, 30,
            "CREATE UNIQUE INDEX UIX_AniDB_Character_Seiyuu_CharID_SeiyuuID ON AniDB_Character_Seiyuu(CharID, SeiyuuID)"),
        new DatabaseCommand(1, 31,
            "CREATE TABLE AniDB_Seiyuu ( AniDB_SeiyuuID int IDENTITY(1,1) NOT NULL, SeiyuuID int NOT NULL, SeiyuuName nvarchar(200) NOT NULL, PicName varchar(100) NOT NULL, CONSTRAINT [PK_AniDB_Seiyuu] PRIMARY KEY CLUSTERED  ( AniDB_SeiyuuID ASC )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] ) ON [PRIMARY] "),
        new DatabaseCommand(1, 32, "CREATE UNIQUE INDEX UIX_AniDB_Seiyuu_SeiyuuID ON AniDB_Seiyuu(SeiyuuID)"),
        new DatabaseCommand(1, 33,
            "CREATE TABLE AniDB_Episode( AniDB_EpisodeID int IDENTITY(1,1) NOT NULL, EpisodeID int NOT NULL, AnimeID int NOT NULL, LengthSeconds int NOT NULL, Rating varchar(max) NOT NULL, Votes varchar(max) NOT NULL, EpisodeNumber int NOT NULL, EpisodeType int NOT NULL, RomajiName varchar(max) NOT NULL, EnglishName varchar(max) NOT NULL, AirDate int NOT NULL, DateTimeUpdated datetime NOT NULL, CONSTRAINT [PK_AniDB_Episode] PRIMARY KEY CLUSTERED  ( AniDB_EpisodeID ASC )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] ) ON [PRIMARY] "),
        new DatabaseCommand(1, 34, "CREATE INDEX IX_AniDB_Episode_AnimeID on AniDB_Episode(AnimeID)"),
        new DatabaseCommand(1, 35, "CREATE UNIQUE INDEX UIX_AniDB_Episode_EpisodeID ON AniDB_Episode(EpisodeID)"),
        new DatabaseCommand(1, 36,
            "CREATE TABLE AniDB_File( AniDB_FileID int IDENTITY(1,1) NOT NULL, FileID int NOT NULL, Hash varchar(50) NOT NULL, AnimeID int NOT NULL, GroupID int NOT NULL, File_Source varchar(max) NOT NULL, File_AudioCodec varchar(max) NOT NULL, File_VideoCodec varchar(max) NOT NULL, File_VideoResolution varchar(max) NOT NULL, File_FileExtension varchar(max) NOT NULL, File_LengthSeconds int NOT NULL, File_Description varchar(max) NOT NULL, File_ReleaseDate int NOT NULL, Anime_GroupName nvarchar(max) NOT NULL, Anime_GroupNameShort nvarchar(max) NOT NULL, Episode_Rating int NOT NULL, Episode_Votes int NOT NULL, DateTimeUpdated datetime NOT NULL, IsWatched int NOT NULL, WatchedDate datetime NULL, CRC varchar(max) NOT NULL, MD5 varchar(max) NOT NULL, SHA1 varchar(max) NOT NULL, FileName nvarchar(max) NOT NULL, FileSize bigint NOT NULL, CONSTRAINT [PK_AniDB_File] PRIMARY KEY CLUSTERED  ( AniDB_FileID ASC )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] ) ON [PRIMARY] "),
        new DatabaseCommand(1, 37, "CREATE UNIQUE INDEX UIX_AniDB_File_Hash on AniDB_File(Hash)"),
        new DatabaseCommand(1, 38, "CREATE UNIQUE INDEX UIX_AniDB_File_FileID ON AniDB_File(FileID)"),
        new DatabaseCommand(1, 39,
            "CREATE TABLE AniDB_GroupStatus ( AniDB_GroupStatusID int IDENTITY(1,1) NOT NULL, AnimeID int NOT NULL, GroupID int NOT NULL, GroupName nvarchar(200) NOT NULL, CompletionState int NOT NULL, LastEpisodeNumber int NOT NULL, Rating int NOT NULL, Votes int NOT NULL, EpisodeRange nvarchar(200) NOT NULL, CONSTRAINT [PK_AniDB_GroupStatus] PRIMARY KEY CLUSTERED  ( AniDB_GroupStatusID ASC )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] ) ON [PRIMARY] "),
        new DatabaseCommand(1, 40, "CREATE INDEX IX_AniDB_GroupStatus_AnimeID on AniDB_GroupStatus(AnimeID)"),
        new DatabaseCommand(1, 41,
            "CREATE UNIQUE INDEX UIX_AniDB_GroupStatus_AnimeID_GroupID ON AniDB_GroupStatus(AnimeID, GroupID)"),
        new DatabaseCommand(1, 42,
            "CREATE TABLE AniDB_ReleaseGroup ( AniDB_ReleaseGroupID int IDENTITY(1,1) NOT NULL, GroupID int NOT NULL, Rating int NOT NULL, Votes int NOT NULL, AnimeCount int NOT NULL, FileCount int NOT NULL, GroupName nvarchar(MAX) NOT NULL, GroupNameShort nvarchar(200) NOT NULL, IRCChannel nvarchar(200) NOT NULL, IRCServer nvarchar(200) NOT NULL, URL nvarchar(200) NOT NULL, Picname nvarchar(200) NOT NULL, CONSTRAINT [PK_AniDB_ReleaseGroup] PRIMARY KEY CLUSTERED  ( AniDB_ReleaseGroupID ASC )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] ) ON [PRIMARY] "),
        new DatabaseCommand(1, 43,
            "CREATE UNIQUE INDEX UIX_AniDB_ReleaseGroup_GroupID ON AniDB_ReleaseGroup(GroupID)"),
        new DatabaseCommand(1, 44,
            "CREATE TABLE AniDB_Review ( AniDB_ReviewID int IDENTITY(1,1) NOT NULL, ReviewID int NOT NULL, AuthorID int NOT NULL, RatingAnimation int NOT NULL, RatingSound int NOT NULL, RatingStory int NOT NULL, RatingCharacter int NOT NULL, RatingValue int NOT NULL, RatingEnjoyment int NOT NULL, ReviewText nvarchar(MAX) NOT NULL, CONSTRAINT [PK_AniDB_Review] PRIMARY KEY CLUSTERED  ( AniDB_ReviewID ASC )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] ) ON [PRIMARY] "),
        new DatabaseCommand(1, 45, "CREATE UNIQUE INDEX UIX_AniDB_Review_ReviewID ON AniDB_Review(ReviewID)"),
        new DatabaseCommand(1, 46,
            "CREATE TABLE AniDB_Tag ( AniDB_TagID int IDENTITY(1,1) NOT NULL, TagID int NOT NULL, Spoiler int NOT NULL, LocalSpoiler int NOT NULL, GlobalSpoiler int NOT NULL, TagName nvarchar(150) NOT NULL, TagCount int NOT NULL, TagDescription nvarchar(max) NOT NULL, CONSTRAINT [PK_AniDB_Tag] PRIMARY KEY CLUSTERED  ( AniDB_TagID ASC )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] ) ON [PRIMARY] "),
        new DatabaseCommand(1, 47, "CREATE UNIQUE INDEX UIX_AniDB_Tag_TagID ON AniDB_Tag(TagID)"),
        new DatabaseCommand(1, 48,
            "CREATE TABLE AnimeEpisode( AnimeEpisodeID int IDENTITY(1,1) NOT NULL, AnimeSeriesID int NOT NULL, AniDB_EpisodeID int NOT NULL, DateTimeUpdated datetime NOT NULL, DateTimeCreated datetime NOT NULL, CONSTRAINT [PK_AnimeEpisode] PRIMARY KEY CLUSTERED  ( AnimeEpisodeID ASC )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] ) ON [PRIMARY]"),
        new DatabaseCommand(1, 49,
            "CREATE UNIQUE INDEX UIX_AnimeEpisode_AniDB_EpisodeID ON AnimeEpisode(AniDB_EpisodeID)"),
        new DatabaseCommand(1, 50, "CREATE INDEX IX_AnimeEpisode_AnimeSeriesID on AnimeEpisode(AnimeSeriesID)"),
        new DatabaseCommand(1, 51,
            "CREATE TABLE AnimeGroup( AnimeGroupID int IDENTITY(1,1) NOT NULL, AnimeGroupParentID int NULL, GroupName nvarchar(max) NOT NULL, Description nvarchar(max) NULL, IsManuallyNamed int NOT NULL, DateTimeUpdated datetime NOT NULL, DateTimeCreated datetime NOT NULL, SortName varchar(max) NOT NULL, MissingEpisodeCount int NOT NULL, MissingEpisodeCountGroups int NOT NULL, OverrideDescription int NOT NULL, EpisodeAddedDate datetime NULL, CONSTRAINT [PK_AnimeGroup] PRIMARY KEY CLUSTERED  ( [AnimeGroupID] ASC )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] ) ON [PRIMARY] "),
        new DatabaseCommand(1, 52,
            "CREATE TABLE AnimeSeries ( AnimeSeriesID int IDENTITY(1,1) NOT NULL, AnimeGroupID int NOT NULL, AniDB_ID int NOT NULL, DateTimeUpdated datetime NOT NULL, DateTimeCreated datetime NOT NULL, DefaultAudioLanguage varchar(max) NULL, DefaultSubtitleLanguage varchar(max) NULL, MissingEpisodeCount int NOT NULL, MissingEpisodeCountGroups int NOT NULL, LatestLocalEpisodeNumber int NOT NULL, EpisodeAddedDate datetime NULL, CONSTRAINT [PK_AnimeSeries] PRIMARY KEY CLUSTERED  ( AnimeSeriesID ASC )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] ) ON [PRIMARY] "),
        new DatabaseCommand(1, 53, "CREATE UNIQUE INDEX UIX_AnimeSeries_AniDB_ID ON AnimeSeries(AniDB_ID)"),
        new DatabaseCommand(1, 54,
            "CREATE TABLE CommandRequest( CommandRequestID int IDENTITY(1,1) NOT NULL, Priority int NOT NULL, CommandType int NOT NULL, CommandID nvarchar(max) NOT NULL, CommandDetails nvarchar(max) NOT NULL, DateTimeUpdated datetime NOT NULL, CONSTRAINT [PK_CommandRequest] PRIMARY KEY CLUSTERED  ( CommandRequestID ASC )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] ) ON [PRIMARY] "),
        new DatabaseCommand(1, 55,
            "CREATE TABLE CrossRef_AniDB_Other( CrossRef_AniDB_OtherID int IDENTITY(1,1) NOT NULL, AnimeID int NOT NULL, CrossRefID nvarchar(500) NOT NULL, CrossRefSource int NOT NULL, CrossRefType int NOT NULL, CONSTRAINT [PK_CrossRef_AniDB_Other] PRIMARY KEY CLUSTERED ( CrossRef_AniDB_OtherID ASC )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] ) ON [PRIMARY] "),
        new DatabaseCommand(1, 56,
            "CREATE UNIQUE INDEX UIX_CrossRef_AniDB_Other ON CrossRef_AniDB_Other(AnimeID, CrossRefID, CrossRefSource, CrossRefType)"),
        new DatabaseCommand(1, 57,
            "CREATE TABLE CrossRef_AniDB_TvDB( CrossRef_AniDB_TvDBID int IDENTITY(1,1) NOT NULL, AnimeID int NOT NULL, TvDBID int NOT NULL, TvDBSeasonNumber int NOT NULL, CrossRefSource int NOT NULL, CONSTRAINT [PK_CrossRef_AniDB_TvDB] PRIMARY KEY CLUSTERED ( CrossRef_AniDB_TvDBID ASC )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] ) ON [PRIMARY] "),
        new DatabaseCommand(1, 58,
            "CREATE UNIQUE INDEX UIX_CrossRef_AniDB_TvDB ON CrossRef_AniDB_TvDB(AnimeID, TvDBID, TvDBSeasonNumber, CrossRefSource)"),
        new DatabaseCommand(1, 59,
            "CREATE UNIQUE INDEX UIX_CrossRef_AniDB_TvDB_AnimeID ON CrossRef_AniDB_TvDB(AnimeID)"),
        new DatabaseCommand(1, 60,
            "CREATE TABLE CrossRef_File_Episode( CrossRef_File_EpisodeID int IDENTITY(1,1) NOT NULL, Hash varchar(50) NULL, FileName nvarchar(500) NOT NULL, FileSize bigint NOT NULL, CrossRefSource int NOT NULL, AnimeID int NOT NULL, EpisodeID int NOT NULL, Percentage int NOT NULL, EpisodeOrder int NOT NULL, CONSTRAINT [PK_CrossRef_File_Episode] PRIMARY KEY CLUSTERED ( CrossRef_File_EpisodeID ASC )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] ) ON [PRIMARY] "),
        new DatabaseCommand(1, 61,
            "CREATE UNIQUE INDEX UIX_CrossRef_File_Episode_Hash_EpisodeID ON CrossRef_File_Episode(Hash, EpisodeID)"),
        new DatabaseCommand(1, 62,
            "CREATE TABLE CrossRef_Languages_AniDB_File( CrossRef_Languages_AniDB_FileID int IDENTITY(1,1) NOT NULL, FileID int NOT NULL, LanguageID int NOT NULL, CONSTRAINT [PK_CrossRef_Languages_AniDB_File] PRIMARY KEY CLUSTERED  ( CrossRef_Languages_AniDB_FileID ASC )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] ) ON [PRIMARY] "),
        new DatabaseCommand(1, 63,
            "CREATE TABLE CrossRef_Subtitles_AniDB_File( CrossRef_Subtitles_AniDB_FileID int IDENTITY(1,1) NOT NULL, FileID int NOT NULL, LanguageID int NOT NULL, CONSTRAINT [PK_CrossRef_Subtitles_AniDB_File] PRIMARY KEY CLUSTERED  ( CrossRef_Subtitles_AniDB_FileID ASC )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] ) ON [PRIMARY] "),
        new DatabaseCommand(1, 64,
            "CREATE TABLE FileNameHash ( FileNameHashID int IDENTITY(1,1) NOT NULL, FileName nvarchar(500) NOT NULL, FileSize bigint NOT NULL, Hash varchar(50) NOT NULL, DateTimeUpdated datetime NOT NULL, CONSTRAINT [PK_FileNameHash] PRIMARY KEY CLUSTERED  ( FileNameHashID ASC )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] ) ON [PRIMARY] "),
        new DatabaseCommand(1, 65,
            "CREATE UNIQUE INDEX UIX_FileNameHash ON FileNameHash(FileName, FileSize, Hash)"),
        new DatabaseCommand(1, 66,
            "CREATE TABLE Language( LanguageID int IDENTITY(1,1) NOT NULL, LanguageName varchar(100) NOT NULL, CONSTRAINT [PK_Language] PRIMARY KEY CLUSTERED  ( LanguageID ASC )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] ) ON [PRIMARY] "),
        new DatabaseCommand(1, 67, "CREATE UNIQUE INDEX UIX_Language_LanguageName ON Language(LanguageName)"),
        new DatabaseCommand(1, 68,
            "CREATE TABLE ImportFolder( ImportFolderID int IDENTITY(1,1) NOT NULL, ImportFolderType int NOT NULL, ImportFolderName nvarchar(max) NOT NULL, ImportFolderLocation nvarchar(max) NOT NULL, IsDropSource int NOT NULL, IsDropDestination int NOT NULL, CONSTRAINT [PK_ImportFolder] PRIMARY KEY CLUSTERED  ( ImportFolderID ASC )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] ) ON [PRIMARY] "),
        new DatabaseCommand(1, 69,
            "CREATE TABLE ScheduledUpdate( ScheduledUpdateID int IDENTITY(1,1) NOT NULL, UpdateType int NOT NULL, LastUpdate datetime NOT NULL, UpdateDetails nvarchar(max) NOT NULL, CONSTRAINT [PK_ScheduledUpdate] PRIMARY KEY CLUSTERED  ( ScheduledUpdateID ASC )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] ) ON [PRIMARY] "),
        new DatabaseCommand(1, 70,
            "CREATE UNIQUE INDEX UIX_ScheduledUpdate_UpdateType ON ScheduledUpdate(UpdateType)"),
        new DatabaseCommand(1, 71,
            "CREATE TABLE VideoInfo ( VideoInfoID int IDENTITY(1,1) NOT NULL, Hash varchar(50) NOT NULL, FileSize bigint NOT NULL, FileName nvarchar(max) NOT NULL, DateTimeUpdated datetime NOT NULL, VideoCodec varchar(max) NOT NULL, VideoBitrate varchar(max) NOT NULL, VideoFrameRate varchar(max) NOT NULL, VideoResolution varchar(max) NOT NULL, AudioCodec varchar(max) NOT NULL, AudioBitrate varchar(max) NOT NULL, Duration bigint NOT NULL, CONSTRAINT [PK_VideoInfo] PRIMARY KEY CLUSTERED  ( VideoInfoID ASC )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] ) ON [PRIMARY] "),
        new DatabaseCommand(1, 72, "CREATE UNIQUE INDEX UIX_VideoInfo_Hash on VideoInfo(Hash)"),
        new DatabaseCommand(1, 73,
            "CREATE TABLE VideoLocal( VideoLocalID int IDENTITY(1,1) NOT NULL, FilePath nvarchar(max) NOT NULL, ImportFolderID int NOT NULL, Hash varchar(50) NOT NULL, CRC32 varchar(50) NULL, MD5 varchar(50) NULL, SHA1 varchar(50) NULL, HashSource int NOT NULL, FileSize bigint NOT NULL, IsIgnored int NOT NULL, DateTimeUpdated datetime NOT NULL, CONSTRAINT [PK_VideoLocal] PRIMARY KEY CLUSTERED  ( VideoLocalID ASC )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] ) ON [PRIMARY] "),
        new DatabaseCommand(1, 74, "CREATE UNIQUE INDEX UIX_VideoLocal_Hash on VideoLocal(Hash)"),
        new DatabaseCommand(1, 75,
            "CREATE TABLE DuplicateFile( DuplicateFileID int IDENTITY(1,1) NOT NULL, FilePathFile1 nvarchar(max) NOT NULL, FilePathFile2 nvarchar(max) NOT NULL, ImportFolderIDFile1 int NOT NULL, ImportFolderIDFile2 int NOT NULL, Hash varchar(50) NOT NULL, DateTimeUpdated datetime NOT NULL, CONSTRAINT [PK_DuplicateFile] PRIMARY KEY CLUSTERED  ( DuplicateFileID ASC )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] ) ON [PRIMARY] "),
        new DatabaseCommand(1, 76,
            "CREATE TABLE GroupFilter( GroupFilterID int IDENTITY(1,1) NOT NULL, GroupFilterName nvarchar(max) NOT NULL, ApplyToSeries int NOT NULL, BaseCondition int NOT NULL, SortingCriteria nvarchar(max), CONSTRAINT [PK_GroupFilter] PRIMARY KEY CLUSTERED  ( GroupFilterID ASC )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] ) ON [PRIMARY] "),
        new DatabaseCommand(1, 77,
            "CREATE TABLE GroupFilterCondition( GroupFilterConditionID int IDENTITY(1,1) NOT NULL, GroupFilterID int NOT NULL, ConditionType int NOT NULL, ConditionOperator int NOT NULL, ConditionParameter nvarchar(max) NOT NULL, CONSTRAINT [PK_GroupFilterCondition] PRIMARY KEY CLUSTERED  ( GroupFilterConditionID ASC )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] ) ON [PRIMARY] "),
        new DatabaseCommand(1, 78,
            "CREATE TABLE AniDB_Vote ( AniDB_VoteID int IDENTITY(1,1) NOT NULL, EntityID int NOT NULL, VoteValue int NOT NULL, VoteType int NOT NULL, CONSTRAINT [PK_AniDB_Vote] PRIMARY KEY CLUSTERED  ( AniDB_VoteID ASC )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] ) ON [PRIMARY] "),
        new DatabaseCommand(1, 79,
            "CREATE TABLE TvDB_ImageFanart( TvDB_ImageFanartID int IDENTITY(1,1) NOT NULL, Id int NOT NULL, SeriesID int NOT NULL, BannerPath nvarchar(MAX),  BannerType nvarchar(MAX),  BannerType2 nvarchar(MAX),  Colors nvarchar(MAX),  Language nvarchar(MAX),  ThumbnailPath nvarchar(MAX),  VignettePath nvarchar(MAX),  Enabled int NOT NULL, Chosen int NOT NULL, CONSTRAINT PK_TvDB_ImageFanart PRIMARY KEY CLUSTERED  ( TvDB_ImageFanartID ASC )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] ) ON [PRIMARY] "),
        new DatabaseCommand(1, 80, "CREATE UNIQUE INDEX UIX_TvDB_ImageFanart_Id ON TvDB_ImageFanart(Id)"),
        new DatabaseCommand(1, 81,
            "CREATE TABLE TvDB_ImageWideBanner( TvDB_ImageWideBannerID int IDENTITY(1,1) NOT NULL, Id int NOT NULL, SeriesID int NOT NULL, BannerPath nvarchar(MAX),  BannerType nvarchar(MAX),  BannerType2 nvarchar(MAX),  Language nvarchar(MAX),  Enabled int NOT NULL, SeasonNumber int, CONSTRAINT PK_TvDB_ImageWideBanner PRIMARY KEY CLUSTERED  ( TvDB_ImageWideBannerID ASC )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] ) ON [PRIMARY] "),
        new DatabaseCommand(1, 82, "CREATE UNIQUE INDEX UIX_TvDB_ImageWideBanner_Id ON TvDB_ImageWideBanner(Id)"),
        new DatabaseCommand(1, 83,
            "CREATE TABLE TvDB_ImagePoster( TvDB_ImagePosterID int IDENTITY(1,1) NOT NULL, Id int NOT NULL, SeriesID int NOT NULL, BannerPath nvarchar(MAX),  BannerType nvarchar(MAX),  BannerType2 nvarchar(MAX),  Language nvarchar(MAX),  Enabled int NOT NULL, SeasonNumber int, CONSTRAINT PK_TvDB_ImagePoster PRIMARY KEY CLUSTERED  ( TvDB_ImagePosterID ASC )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] ) ON [PRIMARY] "),
        new DatabaseCommand(1, 84, "CREATE UNIQUE INDEX UIX_TvDB_ImagePoster_Id ON TvDB_ImagePoster(Id)"),
        new DatabaseCommand(1, 85,
            "CREATE TABLE TvDB_Episode( TvDB_EpisodeID int IDENTITY(1,1) NOT NULL, Id int NOT NULL, SeriesID int NOT NULL, SeasonID int NOT NULL, SeasonNumber int NOT NULL, EpisodeNumber int NOT NULL, EpisodeName nvarchar(MAX), Overview nvarchar(MAX), Filename nvarchar(MAX), EpImgFlag int NOT NULL, FirstAired nvarchar(MAX), AbsoluteNumber int, AirsAfterSeason int, AirsBeforeEpisode int, AirsBeforeSeason int, CONSTRAINT PK_TvDB_Episode PRIMARY KEY CLUSTERED  ( TvDB_EpisodeID ASC )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] ) ON [PRIMARY] "),
        new DatabaseCommand(1, 86, "CREATE UNIQUE INDEX UIX_TvDB_Episode_Id ON TvDB_Episode(Id)"),
        new DatabaseCommand(1, 87,
            "CREATE TABLE TvDB_Series( TvDB_SeriesID int IDENTITY(1,1) NOT NULL, SeriesID int NOT NULL, Overview nvarchar(MAX), SeriesName nvarchar(MAX), Status varchar(100), Banner varchar(100), Fanart varchar(100), Poster varchar(100), Lastupdated varchar(100), CONSTRAINT PK_TvDB_Series PRIMARY KEY CLUSTERED  ( TvDB_SeriesID ASC )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] ) ON [PRIMARY] "),
        new DatabaseCommand(1, 88, "CREATE UNIQUE INDEX UIX_TvDB_Series_Id ON TvDB_Series(SeriesID)"),
        new DatabaseCommand(1, 89,
            "CREATE TABLE AniDB_Anime_DefaultImage ( AniDB_Anime_DefaultImageID int IDENTITY(1,1) NOT NULL, AnimeID int NOT NULL, ImageParentID int NOT NULL, ImageParentType int NOT NULL, ImageType int NOT NULL, CONSTRAINT [PK_AniDB_Anime_DefaultImage] PRIMARY KEY CLUSTERED  ( [AniDB_Anime_DefaultImageID] ASC )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] ) ON [PRIMARY] "),
        new DatabaseCommand(1, 90,
            "CREATE UNIQUE INDEX UIX_AniDB_Anime_DefaultImage_ImageType ON AniDB_Anime_DefaultImage(AnimeID, ImageType)"),
        new DatabaseCommand(1, 91,
            "CREATE TABLE MovieDB_Movie( MovieDB_MovieID int IDENTITY(1,1) NOT NULL, MovieId int NOT NULL, MovieName nvarchar(MAX), OriginalName nvarchar(MAX), Overview nvarchar(MAX), CONSTRAINT PK_MovieDB_Movie PRIMARY KEY CLUSTERED  ( MovieDB_MovieID ASC )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] ) ON [PRIMARY] "),
        new DatabaseCommand(1, 92, "CREATE UNIQUE INDEX UIX_MovieDB_Movie_Id ON MovieDB_Movie(MovieId)"),
        new DatabaseCommand(1, 93,
            "CREATE TABLE MovieDB_Poster( MovieDB_PosterID int IDENTITY(1,1) NOT NULL, ImageID varchar(100), MovieId int NOT NULL, ImageType varchar(100), ImageSize varchar(100),  URL nvarchar(MAX),  ImageWidth int NOT NULL,  ImageHeight int NOT NULL,  Enabled int NOT NULL, CONSTRAINT PK_MovieDB_Poster PRIMARY KEY CLUSTERED  ( MovieDB_PosterID ASC )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] ) ON [PRIMARY] "),
        new DatabaseCommand(1, 94,
            "CREATE TABLE MovieDB_Fanart( MovieDB_FanartID int IDENTITY(1,1) NOT NULL, ImageID varchar(100), MovieId int NOT NULL, ImageType varchar(100), ImageSize varchar(100),  URL nvarchar(MAX),  ImageWidth int NOT NULL,  ImageHeight int NOT NULL,  Enabled int NOT NULL, CONSTRAINT PK_MovieDB_Fanart PRIMARY KEY CLUSTERED  ( MovieDB_FanartID ASC )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] ) ON [PRIMARY] "),
        new DatabaseCommand(1, 95,
            "CREATE TABLE JMMUser( JMMUserID int IDENTITY(1,1) NOT NULL, Username nvarchar(100), Password nvarchar(100), IsAdmin int NOT NULL, IsAniDBUser int NOT NULL, IsTraktUser int NOT NULL, HideCategories nvarchar(MAX), CONSTRAINT PK_JMMUser PRIMARY KEY CLUSTERED  ( JMMUserID ASC )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] ) ON [PRIMARY] "),
        new DatabaseCommand(1, 96,
            "CREATE TABLE Trakt_Episode( Trakt_EpisodeID int IDENTITY(1,1) NOT NULL, Trakt_ShowID int NOT NULL, Season int NOT NULL, EpisodeNumber int NOT NULL, Title nvarchar(MAX), URL nvarchar(500), Overview nvarchar(MAX), EpisodeImage nvarchar(500), CONSTRAINT PK_Trakt_Episode PRIMARY KEY CLUSTERED  ( Trakt_EpisodeID ASC )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] ) ON [PRIMARY] "),
        new DatabaseCommand(1, 97,
            "CREATE TABLE Trakt_ImagePoster( Trakt_ImagePosterID int IDENTITY(1,1) NOT NULL, Trakt_ShowID int NOT NULL, Season int NOT NULL, ImageURL nvarchar(500), Enabled int NOT NULL, CONSTRAINT PK_Trakt_ImagePoster PRIMARY KEY CLUSTERED  ( Trakt_ImagePosterID ASC )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] ) ON [PRIMARY] "),
        new DatabaseCommand(1, 98,
            "CREATE TABLE Trakt_ImageFanart( Trakt_ImageFanartID int IDENTITY(1,1) NOT NULL, Trakt_ShowID int NOT NULL, Season int NOT NULL, ImageURL nvarchar(500), Enabled int NOT NULL, CONSTRAINT PK_Trakt_ImageFanart PRIMARY KEY CLUSTERED  ( Trakt_ImageFanartID ASC )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] ) ON [PRIMARY] "),
        new DatabaseCommand(1, 99,
            "CREATE TABLE Trakt_Show( Trakt_ShowID int IDENTITY(1,1) NOT NULL, TraktID nvarchar(500), Title nvarchar(MAX), Year nvarchar(500), URL nvarchar(500), Overview nvarchar(MAX), TvDB_ID int NULL, CONSTRAINT PK_Trakt_Show PRIMARY KEY CLUSTERED  ( Trakt_ShowID ASC )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] ) ON [PRIMARY] "),
        new DatabaseCommand(1, 100,
            "CREATE TABLE Trakt_Season( Trakt_SeasonID int IDENTITY(1,1) NOT NULL, Trakt_ShowID int NOT NULL, Season int NOT NULL, URL nvarchar(500), CONSTRAINT PK_Trakt_Season PRIMARY KEY CLUSTERED  ( Trakt_SeasonID ASC )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] ) ON [PRIMARY] "),
        new DatabaseCommand(1, 101,
            "CREATE TABLE CrossRef_AniDB_Trakt( CrossRef_AniDB_TraktID int IDENTITY(1,1) NOT NULL, AnimeID int NOT NULL, TraktID nvarchar(500), TraktSeasonNumber int NOT NULL, CrossRefSource int NOT NULL, CONSTRAINT [PK_CrossRef_AniDB_Trakt] PRIMARY KEY CLUSTERED ( CrossRef_AniDB_TraktID ASC )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] ) ON [PRIMARY] "),
        new DatabaseCommand(1, 102,
            "CREATE TABLE AnimeEpisode_User( AnimeEpisode_UserID int IDENTITY(1,1) NOT NULL, JMMUserID int NOT NULL, AnimeEpisodeID int NOT NULL, AnimeSeriesID int NOT NULL,  WatchedDate datetime NULL, PlayedCount int NOT NULL, WatchedCount int NOT NULL, StoppedCount int NOT NULL, CONSTRAINT [PK_AnimeEpisode_User] PRIMARY KEY CLUSTERED  ( AnimeEpisode_UserID ASC )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] ) ON [PRIMARY]"),
        new DatabaseCommand(1, 103,
            "CREATE UNIQUE INDEX UIX_AnimeEpisode_User_User_EpisodeID ON AnimeEpisode_User(JMMUserID, AnimeEpisodeID)"),
        new DatabaseCommand(1, 104,
            "CREATE INDEX IX_AnimeEpisode_User_User_AnimeSeriesID on AnimeEpisode_User(JMMUserID, AnimeSeriesID)"),
        new DatabaseCommand(1, 105,
            "CREATE TABLE AnimeSeries_User( AnimeSeries_UserID int IDENTITY(1,1) NOT NULL, JMMUserID int NOT NULL, AnimeSeriesID int NOT NULL, UnwatchedEpisodeCount int NOT NULL, WatchedEpisodeCount int NOT NULL, WatchedDate datetime NULL, PlayedCount int NOT NULL, WatchedCount int NOT NULL, StoppedCount int NOT NULL, CONSTRAINT [PK_AnimeSeries_User] PRIMARY KEY CLUSTERED  ( AnimeSeries_UserID ASC )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] ) ON [PRIMARY]"),
        new DatabaseCommand(1, 106,
            "CREATE UNIQUE INDEX UIX_AnimeSeries_User_User_SeriesID ON AnimeSeries_User(JMMUserID, AnimeSeriesID)"),
        new DatabaseCommand(1, 107,
            "CREATE TABLE AnimeGroup_User( AnimeGroup_UserID int IDENTITY(1,1) NOT NULL, JMMUserID int NOT NULL, AnimeGroupID int NOT NULL, IsFave int NOT NULL, UnwatchedEpisodeCount int NOT NULL, WatchedEpisodeCount int NOT NULL, WatchedDate datetime NULL, PlayedCount int NOT NULL, WatchedCount int NOT NULL, StoppedCount int NOT NULL, CONSTRAINT [PK_AnimeGroup_User] PRIMARY KEY CLUSTERED  ( AnimeGroup_UserID ASC )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] ) ON [PRIMARY]"),
        new DatabaseCommand(1, 108,
            "CREATE UNIQUE INDEX UIX_AnimeGroup_User_User_GroupID ON AnimeGroup_User(JMMUserID, AnimeGroupID)"),
        new DatabaseCommand(1, 109,
            "CREATE TABLE VideoLocal_User( VideoLocal_UserID int IDENTITY(1,1) NOT NULL, JMMUserID int NOT NULL, VideoLocalID int NOT NULL, WatchedDate datetime NOT NULL, CONSTRAINT [PK_VideoLocal_User] PRIMARY KEY CLUSTERED  ( VideoLocal_UserID ASC )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] ) ON [PRIMARY] "),
        new DatabaseCommand(1, 110,
            "CREATE UNIQUE INDEX UIX_VideoLocal_User_User_VideoLocalID ON VideoLocal_User(JMMUserID, VideoLocalID)"),
    };

    private List<DatabaseCommand> patchCommands = new()
    {
        new DatabaseCommand(2, 1,
            "CREATE TABLE IgnoreAnime( IgnoreAnimeID int IDENTITY(1,1) NOT NULL, JMMUserID int NOT NULL, AnimeID int NOT NULL, IgnoreType int NOT NULL, CONSTRAINT [PK_IgnoreAnime] PRIMARY KEY CLUSTERED  ( IgnoreAnimeID ASC )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] ) ON [PRIMARY]"),
        new DatabaseCommand(2, 2,
            "CREATE UNIQUE INDEX UIX_IgnoreAnime_User_AnimeID ON IgnoreAnime(JMMUserID, AnimeID, IgnoreType)"),
        new DatabaseCommand(3, 1,
            "CREATE TABLE Trakt_Friend( Trakt_FriendID int IDENTITY(1,1) NOT NULL, Username nvarchar(100) NOT NULL, FullName nvarchar(100) NULL, Gender nvarchar(100) NULL, Age nvarchar(100) NULL, Location nvarchar(100) NULL, About nvarchar(MAX) NULL, Joined int NOT NULL, Avatar nvarchar(MAX) NULL, Url nvarchar(MAX) NULL, LastAvatarUpdate datetime NOT NULL, CONSTRAINT [PK_Trakt_Friend] PRIMARY KEY CLUSTERED  ( Trakt_FriendID ASC )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] ) ON [PRIMARY]"),
        new DatabaseCommand(3, 2, "CREATE UNIQUE INDEX UIX_Trakt_Friend_Username ON Trakt_Friend(Username)"),
        new DatabaseCommand(4, 1, "ALTER TABLE AnimeGroup ADD DefaultAnimeSeriesID int NULL"),
        new DatabaseCommand(5, 1, "ALTER TABLE JMMUser ADD CanEditServerSettings int NULL"),
        new DatabaseCommand(6, 1, "ALTER TABLE VideoInfo ADD VideoBitDepth varchar(max) NULL"),
        new DatabaseCommand(7, 1, DatabaseFixes.NoOperation),
        new DatabaseCommand(7, 2, DatabaseFixes.NoOperation),
        new DatabaseCommand(7, 3,
            "CREATE UNIQUE INDEX UIX_CrossRef_AniDB_TvDB_Season ON CrossRef_AniDB_TvDB(TvDBID, TvDBSeasonNumber)"),
        new DatabaseCommand(7, 4,
            "CREATE UNIQUE INDEX UIX_CrossRef_AniDB_Trakt_Season ON CrossRef_AniDB_Trakt(TraktID, TraktSeasonNumber)"),
        new DatabaseCommand(7, 5,
            "CREATE UNIQUE INDEX UIX_CrossRef_AniDB_Trakt_Anime ON CrossRef_AniDB_Trakt(AnimeID)"),
        new DatabaseCommand(8, 1, "ALTER TABLE JMMUser ALTER COLUMN Password NVARCHAR(150) NULL"),
        new DatabaseCommand(9, 1, "ALTER TABLE ImportFolder ADD IsWatched int NULL"),
        new DatabaseCommand(9, 2, "UPDATE ImportFolder SET IsWatched = 1"),
        new DatabaseCommand(9, 3, "ALTER TABLE ImportFolder ALTER COLUMN IsWatched int NOT NULL"),
        new DatabaseCommand(10, 1,
            "CREATE TABLE CrossRef_AniDB_MAL( CrossRef_AniDB_MALID int IDENTITY(1,1) NOT NULL, AnimeID int NOT NULL, MALID int NOT NULL, MALTitle nvarchar(500), CrossRefSource int NOT NULL, CONSTRAINT [PK_CrossRef_AniDB_MAL] PRIMARY KEY CLUSTERED ( CrossRef_AniDB_MALID ASC )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] ) ON [PRIMARY] "),
        new DatabaseCommand(10, 2,
            "CREATE UNIQUE INDEX UIX_CrossRef_AniDB_MAL_AnimeID ON CrossRef_AniDB_MAL(AnimeID)"),
        new DatabaseCommand(10, 3, "CREATE UNIQUE INDEX UIX_CrossRef_AniDB_MAL_MALID ON CrossRef_AniDB_MAL(MALID)"),
        new DatabaseCommand(11, 1,
            "DROP INDEX [UIX_CrossRef_AniDB_MAL_AnimeID] ON [dbo].[CrossRef_AniDB_MAL] WITH ( ONLINE = OFF )"),
        new DatabaseCommand(11, 2,
            "DROP INDEX [UIX_CrossRef_AniDB_MAL_MALID] ON [dbo].[CrossRef_AniDB_MAL] WITH ( ONLINE = OFF )"),
        new DatabaseCommand(11, 3, "DROP TABLE [dbo].[CrossRef_AniDB_MAL]"),
        new DatabaseCommand(11, 4,
            "CREATE TABLE CrossRef_AniDB_MAL( CrossRef_AniDB_MALID int IDENTITY(1,1) NOT NULL, AnimeID int NOT NULL, MALID int NOT NULL, MALTitle nvarchar(500), StartEpisodeType int NOT NULL, StartEpisodeNumber int NOT NULL, CrossRefSource int NOT NULL, CONSTRAINT [PK_CrossRef_AniDB_MAL] PRIMARY KEY CLUSTERED ( CrossRef_AniDB_MALID ASC )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] ) ON [PRIMARY] "),
        new DatabaseCommand(11, 5, "CREATE UNIQUE INDEX UIX_CrossRef_AniDB_MAL_MALID ON CrossRef_AniDB_MAL(MALID)"),
        new DatabaseCommand(11, 6,
            "CREATE UNIQUE INDEX UIX_CrossRef_AniDB_MAL_Anime ON CrossRef_AniDB_MAL(AnimeID, StartEpisodeType, StartEpisodeNumber)"),
        new DatabaseCommand(12, 1,
            "CREATE TABLE Playlist( PlaylistID int IDENTITY(1,1) NOT NULL, PlaylistName nvarchar(MAX) NULL, PlaylistItems varchar(MAX) NULL, DefaultPlayOrder int NOT NULL, PlayWatched int NOT NULL, PlayUnwatched int NOT NULL, CONSTRAINT [PK_Playlist] PRIMARY KEY CLUSTERED ( PlaylistID ASC )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] ) ON [PRIMARY] "),
        new DatabaseCommand(13, 1, "ALTER TABLE AnimeSeries ADD SeriesNameOverride nvarchar(500) NULL"),
        new DatabaseCommand(14, 1,
            "CREATE TABLE BookmarkedAnime( BookmarkedAnimeID int IDENTITY(1,1) NOT NULL, AnimeID int NOT NULL, Priority int NOT NULL, Notes nvarchar(MAX) NULL, Downloading int NOT NULL, CONSTRAINT [PK_BookmarkedAnime] PRIMARY KEY CLUSTERED ( BookmarkedAnimeID ASC )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] ) ON [PRIMARY] "),
        new DatabaseCommand(14, 2,
            "CREATE UNIQUE INDEX UIX_BookmarkedAnime_AnimeID ON BookmarkedAnime(BookmarkedAnimeID)"),
        new DatabaseCommand(15, 1, "ALTER TABLE VideoLocal ADD DateTimeCreated datetime NULL"),
        new DatabaseCommand(15, 2, "UPDATE VideoLocal SET DateTimeCreated = DateTimeUpdated"),
        new DatabaseCommand(15, 3, "ALTER TABLE VideoLocal ALTER COLUMN DateTimeCreated datetime NOT NULL"),
        new DatabaseCommand(16, 1,
            "CREATE TABLE CrossRef_AniDB_TvDB_Episode( CrossRef_AniDB_TvDB_EpisodeID int IDENTITY(1,1) NOT NULL, AnimeID int NOT NULL, AniDBEpisodeID int NOT NULL, TvDBEpisodeID int NOT NULL, CONSTRAINT [PK_CrossRef_AniDB_TvDB_Episode] PRIMARY KEY CLUSTERED ( CrossRef_AniDB_TvDB_EpisodeID ASC )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] ) ON [PRIMARY] "),
        new DatabaseCommand(16, 2,
            "CREATE UNIQUE INDEX UIX_CrossRef_AniDB_TvDB_Episode_AniDBEpisodeID ON CrossRef_AniDB_TvDB_Episode(AniDBEpisodeID)"),
        new DatabaseCommand(17, 1,
            "CREATE TABLE AniDB_MylistStats( AniDB_MylistStatsID int IDENTITY(1,1) NOT NULL, Animes int NOT NULL, Episodes int NOT NULL, Files int NOT NULL, SizeOfFiles bigint NOT NULL, AddedAnimes int NOT NULL, AddedEpisodes int NOT NULL, AddedFiles int NOT NULL, AddedGroups int NOT NULL, LeechPct int NOT NULL, GloryPct int NOT NULL, ViewedPct int NOT NULL, MylistPct int NOT NULL, ViewedMylistPct int NOT NULL, EpisodesViewed int NOT NULL, Votes int NOT NULL, Reviews int NOT NULL, ViewiedLength int NOT NULL, CONSTRAINT [PK_AniDB_MylistStats] PRIMARY KEY CLUSTERED ( AniDB_MylistStatsID ASC )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] ) ON [PRIMARY] "),
        new DatabaseCommand(18, 1,
            "CREATE TABLE FileFfdshowPreset( FileFfdshowPresetID int IDENTITY(1,1) NOT NULL, Hash varchar(50) NOT NULL, FileSize bigint NOT NULL, Preset nvarchar(MAX) NULL, CONSTRAINT [PK_FileFfdshowPreset] PRIMARY KEY CLUSTERED ( FileFfdshowPresetID ASC )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] ) ON [PRIMARY] "),
        new DatabaseCommand(18, 2,
            "CREATE UNIQUE INDEX UIX_FileFfdshowPreset_Hash ON FileFfdshowPreset(Hash, FileSize)"),
        new DatabaseCommand(19, 1, "ALTER TABLE AniDB_Anime ADD DisableExternalLinksFlag int NULL"),
        new DatabaseCommand(19, 2, "UPDATE AniDB_Anime SET DisableExternalLinksFlag = 0"),
        new DatabaseCommand(19, 3, "ALTER TABLE AniDB_Anime ALTER COLUMN DisableExternalLinksFlag int NOT NULL"),
        new DatabaseCommand(20, 1, "ALTER TABLE AniDB_File ADD FileVersion int NULL"),
        new DatabaseCommand(20, 2, "UPDATE AniDB_File SET FileVersion = 1"),
        new DatabaseCommand(20, 3, "ALTER TABLE AniDB_File ALTER COLUMN FileVersion int NOT NULL"),
        new DatabaseCommand(21, 1,
            "CREATE TABLE RenameScript( RenameScriptID int IDENTITY(1,1) NOT NULL, ScriptName nvarchar(MAX) NULL, Script nvarchar(MAX) NULL, IsEnabledOnImport int NOT NULL, CONSTRAINT [PK_RenameScript] PRIMARY KEY CLUSTERED ( RenameScriptID ASC )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] ) ON [PRIMARY] "),
        new DatabaseCommand(22, 1, "ALTER TABLE AniDB_File ADD IsCensored int NULL"),
        new DatabaseCommand(22, 2, "ALTER TABLE AniDB_File ADD IsDeprecated int NULL"),
        new DatabaseCommand(22, 3, "ALTER TABLE AniDB_File ADD InternalVersion int NULL"),
        new DatabaseCommand(22, 4, "UPDATE AniDB_File SET IsCensored = 0"),
        new DatabaseCommand(22, 5, "UPDATE AniDB_File SET IsDeprecated = 0"),
        new DatabaseCommand(22, 6, "UPDATE AniDB_File SET InternalVersion = 1"),
        new DatabaseCommand(22, 7, "ALTER TABLE AniDB_File ALTER COLUMN IsCensored int NOT NULL"),
        new DatabaseCommand(22, 8, "ALTER TABLE AniDB_File ALTER COLUMN IsDeprecated int NOT NULL"),
        new DatabaseCommand(22, 9, "ALTER TABLE AniDB_File ALTER COLUMN InternalVersion int NOT NULL"),
        new DatabaseCommand(23, 1, "ALTER TABLE VideoLocal ADD IsVariation int NULL"),
        new DatabaseCommand(23, 2, "UPDATE VideoLocal SET IsVariation = 0"),
        new DatabaseCommand(23, 3, "ALTER TABLE VideoLocal ALTER COLUMN IsVariation int NOT NULL"),
        new DatabaseCommand(24, 1,
            "CREATE TABLE AniDB_Recommendation ( AniDB_RecommendationID int IDENTITY(1,1) NOT NULL, AnimeID int NOT NULL, UserID int NOT NULL, RecommendationType int NOT NULL, RecommendationText nvarchar(MAX), CONSTRAINT [PK_AniDB_Recommendation] PRIMARY KEY CLUSTERED ( AniDB_RecommendationID ASC )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] ) ON [PRIMARY] "),
        new DatabaseCommand(24, 2,
            "CREATE UNIQUE INDEX UIX_AniDB_Recommendation ON AniDB_Recommendation(AnimeID, UserID)"),
        new DatabaseCommand(25, 1,
            "update CrossRef_File_Episode SET CrossRefSource=1 WHERE Hash IN (Select Hash from AniDB_File) AND CrossRefSource=2"),
        new DatabaseCommand(26, 1,
            "CREATE TABLE LogMessage ( LogMessageID int IDENTITY(1,1) NOT NULL, LogType nvarchar(MAX) NOT NULL, LogContent nvarchar(MAX) NOT NULL, LogDate datetime NOT NULL, CONSTRAINT [PK_LogMessage] PRIMARY KEY CLUSTERED ( LogMessageID ASC )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] ) ON [PRIMARY] "),
        new DatabaseCommand(27, 1,
            "CREATE TABLE CrossRef_AniDB_TvDBV2( CrossRef_AniDB_TvDBV2ID int IDENTITY(1,1) NOT NULL, AnimeID int NOT NULL, AniDBStartEpisodeType int NOT NULL, AniDBStartEpisodeNumber int NOT NULL, TvDBID int NOT NULL, TvDBSeasonNumber int NOT NULL, TvDBStartEpisodeNumber int NOT NULL, TvDBTitle nvarchar(MAX), CrossRefSource int NOT NULL, CONSTRAINT [PK_CrossRef_AniDB_TvDBV2] PRIMARY KEY CLUSTERED ( CrossRef_AniDB_TvDBV2ID ASC )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] ) ON [PRIMARY] "),
        new DatabaseCommand(27, 2,
            "CREATE UNIQUE INDEX UIX_CrossRef_AniDB_TvDBV2 ON CrossRef_AniDB_TvDBV2(AnimeID, TvDBID, TvDBSeasonNumber, TvDBStartEpisodeNumber, AniDBStartEpisodeType, AniDBStartEpisodeNumber)"),
        new DatabaseCommand(27, 3, DatabaseFixes.NoOperation),
        new DatabaseCommand(28, 1, "ALTER TABLE GroupFilter ADD Locked int NULL"),
        new DatabaseCommand(29, 1, "ALTER TABLE VideoInfo ADD FullInfo varchar(max) NULL"),
        new DatabaseCommand(30, 1,
            "CREATE TABLE CrossRef_AniDB_TraktV2( CrossRef_AniDB_TraktV2ID int IDENTITY(1,1) NOT NULL, AnimeID int NOT NULL, AniDBStartEpisodeType int NOT NULL, AniDBStartEpisodeNumber int NOT NULL, TraktID nvarchar(500), TraktSeasonNumber int NOT NULL, TraktStartEpisodeNumber int NOT NULL, TraktTitle nvarchar(MAX), CrossRefSource int NOT NULL, CONSTRAINT [PK_CrossRef_AniDB_TraktV2] PRIMARY KEY CLUSTERED ( CrossRef_AniDB_TraktV2ID ASC )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] ) ON [PRIMARY] "),
        new DatabaseCommand(30, 2,
            "CREATE UNIQUE INDEX UIX_CrossRef_AniDB_TraktV2 ON CrossRef_AniDB_TraktV2(AnimeID, TraktSeasonNumber, TraktStartEpisodeNumber, AniDBStartEpisodeType, AniDBStartEpisodeNumber)"),
        new DatabaseCommand(30, 3, DatabaseFixes.NoOperation),
        new DatabaseCommand(31, 1,
            "CREATE TABLE CrossRef_AniDB_Trakt_Episode( CrossRef_AniDB_Trakt_EpisodeID int IDENTITY(1,1) NOT NULL, AnimeID int NOT NULL, AniDBEpisodeID int NOT NULL, TraktID nvarchar(500), Season int NOT NULL, EpisodeNumber int NOT NULL, CONSTRAINT [PK_CrossRef_AniDB_Trakt_Episode] PRIMARY KEY CLUSTERED ( CrossRef_AniDB_Trakt_EpisodeID ASC )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] ) ON [PRIMARY] "),
        new DatabaseCommand(31, 2,
            "CREATE UNIQUE INDEX UIX_CrossRef_AniDB_Trakt_Episode_AniDBEpisodeID ON CrossRef_AniDB_Trakt_Episode(AniDBEpisodeID)"),
        new DatabaseCommand(32, 3, DatabaseFixes.NoOperation),
        new DatabaseCommand(33, 1,
            "CREATE TABLE CustomTag( CustomTagID int IDENTITY(1,1) NOT NULL, TagName nvarchar(500), TagDescription nvarchar(MAX), CONSTRAINT [PK_CustomTag] PRIMARY KEY CLUSTERED ( CustomTagID ASC )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] ) ON [PRIMARY] "),
        new DatabaseCommand(33, 2,
            "CREATE TABLE CrossRef_CustomTag( CrossRef_CustomTagID int IDENTITY(1,1) NOT NULL, CustomTagID int NOT NULL, CrossRefID int NOT NULL, CrossRefType int NOT NULL, CONSTRAINT [PK_CrossRef_CustomTag] PRIMARY KEY CLUSTERED ( CrossRef_CustomTagID ASC )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] ) ON [PRIMARY] "),
        new DatabaseCommand(34, 1, "ALTER TABLE AniDB_Anime_Tag ADD Weight int NULL"),
        new DatabaseCommand(35, 1, DatabaseFixes.PopulateTagWeight),
        new DatabaseCommand(36, 1, "ALTER TABLE Trakt_Episode ADD TraktID int NULL"),
        new DatabaseCommand(37, 1, DatabaseFixes.FixHashes),
        new DatabaseCommand(38, 1, "DROP TABLE LogMessage"),
        new DatabaseCommand(39, 1, "ALTER TABLE AnimeSeries ADD DefaultFolder nvarchar(max) NULL"),
        new DatabaseCommand(40, 1, "ALTER TABLE JMMUser ADD PlexUsers nvarchar(max) NULL"),
        new DatabaseCommand(41, 1, "ALTER TABLE GroupFilter ADD FilterType int NULL"),
        new DatabaseCommand(41, 2, "UPDATE GroupFilter SET FilterType = 1"),
        new DatabaseCommand(41, 3, "ALTER TABLE GroupFilter ALTER COLUMN FilterType int NOT NULL"),
        new DatabaseCommand(41, 4, DatabaseFixes.NoOperation),
        new DatabaseCommand(42, 1,
            "ALTER TABLE AniDB_Anime ADD ContractVersion int NOT NULL DEFAULT(0), ContractString nvarchar(MAX) NULL"),
        new DatabaseCommand(42, 2,
            "ALTER TABLE AnimeGroup ADD ContractVersion int NOT NULL DEFAULT(0), ContractString nvarchar(MAX) NULL"),
        new DatabaseCommand(42, 3,
            "ALTER TABLE AnimeGroup_User ADD PlexContractVersion int NOT NULL DEFAULT(0), PlexContractString nvarchar(MAX) NULL, KodiContractVersion int NOT NULL DEFAULT(0), KodiContractString nvarchar(MAX) NULL"),
        new DatabaseCommand(42, 4,
            "ALTER TABLE AnimeSeries ADD ContractVersion int NOT NULL DEFAULT(0), ContractString nvarchar(MAX) NULL"),
        new DatabaseCommand(42, 5,
            "ALTER TABLE AnimeSeries_User ADD PlexContractVersion int NOT NULL DEFAULT(0), PlexContractString nvarchar(MAX) NULL, KodiContractVersion int NOT NULL DEFAULT(0), KodiContractString nvarchar(MAX) NULL"),
        new DatabaseCommand(42, 6,
            "ALTER TABLE GroupFilter ADD GroupsIdsVersion int NOT NULL DEFAULT(0), GroupsIdsString nvarchar(MAX) NULL"),
        new DatabaseCommand(42, 7,
            "ALTER TABLE AnimeEpisode_User ADD ContractVersion int NOT NULL DEFAULT(0), ContractString nvarchar(MAX) NULL"),
        new DatabaseCommand(43, 1,
            "ALTER TABLE AnimeEpisode ADD PlexContractVersion int NOT NULL DEFAULT(0), PlexContractString nvarchar(MAX) NULL"),
        new DatabaseCommand(43, 2,
            "ALTER TABLE VideoLocal ADD MediaVersion int NOT NULL DEFAULT(0), MediaString nvarchar(MAX) NULL"),
        new DatabaseCommand(44, 1,
            "DECLARE @tableName VARCHAR(MAX) = 'AnimeGroup_User'\r\nDECLARE @columnName VARCHAR(MAX) = 'KodiContractVersion'\r\nDECLARE @ConstraintName nvarchar(200)\r\nSELECT @ConstraintName = Name FROM sys.default_constraints WHERE PARENT_OBJECT_ID = OBJECT_ID(@tableName) AND PARENT_COLUMN_ID = (SELECT column_id FROM sys.columns WHERE NAME = @columnName AND object_id = OBJECT_ID(@tableName))\r\nIF @ConstraintName IS NOT NULL\r\nEXEC('ALTER TABLE ' + @tableName + ' DROP CONSTRAINT ' + @ConstraintName)"),
        new DatabaseCommand(44, 2, "ALTER TABLE AnimeGroup_User DROP COLUMN KodiContractVersion"),
        new DatabaseCommand(44, 3, "ALTER TABLE AnimeGroup_User DROP COLUMN KodiContractString"),
        new DatabaseCommand(44, 4,
            "DECLARE @tableName VARCHAR(MAX) = 'AnimeSeries_User'\r\nDECLARE @columnName VARCHAR(MAX) = 'KodiContractVersion'\r\nDECLARE @ConstraintName nvarchar(200)\r\nSELECT @ConstraintName = Name FROM sys.default_constraints WHERE PARENT_OBJECT_ID = OBJECT_ID(@tableName) AND PARENT_COLUMN_ID = (SELECT column_id FROM sys.columns WHERE NAME = @columnName AND object_id = OBJECT_ID(@tableName))\r\nIF @ConstraintName IS NOT NULL\r\nEXEC('ALTER TABLE ' + @tableName + ' DROP CONSTRAINT ' + @ConstraintName)"),
        new DatabaseCommand(44, 5, "ALTER TABLE AnimeSeries_User DROP COLUMN KodiContractVersion"),
        new DatabaseCommand(44, 6, "ALTER TABLE AnimeSeries_User DROP COLUMN KodiContractString"),
        new DatabaseCommand(45, 1, "ALTER TABLE AnimeSeries ADD LatestEpisodeAirDate [datetime] NULL"),
        new DatabaseCommand(45, 2, "ALTER TABLE AnimeGroup ADD LatestEpisodeAirDate [datetime] NULL"),
        new DatabaseCommand(46, 1,
            "ALTER TABLE GroupFilter ADD GroupConditionsVersion int NOT NULL DEFAULT(0), GroupConditions nvarchar(MAX) NULL,ParentGroupFilterID int NULL,InvisibleInClients  int NOT NULL DEFAULT(0)"),
        new DatabaseCommand(46, 2,
            "ALTER TABLE GroupFilter ADD SeriesIdsVersion int NOT NULL DEFAULT(0), SeriesIdsString nvarchar(MAX) NULL"),
        new DatabaseCommand(47, 1, "ALTER TABLE AniDB_Anime ADD ContractBlob varbinary(MAX) NULL"),
        new DatabaseCommand(47, 2, "ALTER TABLE AniDB_Anime ADD ContractSize int NOT NULL DEFAULT(0)"),
        new DatabaseCommand(47, 3, "ALTER TABLE AniDB_Anime DROP COLUMN ContractString"),
        new DatabaseCommand(47, 4, "ALTER TABLE VideoLocal ADD MediaBlob varbinary(MAX) NULL"),
        new DatabaseCommand(47, 5, "ALTER TABLE VideoLocal ADD MediaSize int NOT NULL DEFAULT(0)"),
        new DatabaseCommand(47, 6, "ALTER TABLE VideoLocal DROP COLUMN MediaString"),
        new DatabaseCommand(47, 7, "ALTER TABLE AnimeEpisode ADD PlexContractBlob varbinary(MAX) NULL"),
        new DatabaseCommand(47, 8, "ALTER TABLE AnimeEpisode ADD PlexContractSize int NOT NULL DEFAULT(0)"),
        new DatabaseCommand(47, 9, "ALTER TABLE AnimeEpisode DROP COLUMN PlexContractString"),
        new DatabaseCommand(47, 10, "ALTER TABLE AnimeEpisode_User ADD ContractBlob varbinary(MAX) NULL"),
        new DatabaseCommand(47, 11, "ALTER TABLE AnimeEpisode_User ADD ContractSize int NOT NULL DEFAULT(0)"),
        new DatabaseCommand(47, 12, "ALTER TABLE AnimeEpisode_User DROP COLUMN ContractString"),
        new DatabaseCommand(47, 13, "ALTER TABLE AnimeSeries ADD ContractBlob varbinary(MAX) NULL"),
        new DatabaseCommand(47, 14, "ALTER TABLE AnimeSeries ADD ContractSize int NOT NULL DEFAULT(0)"),
        new DatabaseCommand(47, 15, "ALTER TABLE AnimeSeries DROP COLUMN ContractString"),
        new DatabaseCommand(47, 16, "ALTER TABLE AnimeSeries_User ADD PlexContractBlob varbinary(MAX) NULL"),
        new DatabaseCommand(47, 17, "ALTER TABLE AnimeSeries_User ADD PlexContractSize int NOT NULL DEFAULT(0)"),
        new DatabaseCommand(47, 18, "ALTER TABLE AnimeSeries_User DROP COLUMN PlexContractString"),
        new DatabaseCommand(47, 19, "ALTER TABLE AnimeGroup_User ADD PlexContractBlob varbinary(MAX) NULL"),
        new DatabaseCommand(47, 20, "ALTER TABLE AnimeGroup_User ADD PlexContractSize int NOT NULL DEFAULT(0)"),
        new DatabaseCommand(47, 21, "ALTER TABLE AnimeGroup_User DROP COLUMN PlexContractString"),
        new DatabaseCommand(47, 22, "ALTER TABLE AnimeGroup ADD ContractBlob varbinary(MAX) NULL"),
        new DatabaseCommand(47, 23, "ALTER TABLE AnimeGroup ADD ContractSize int NOT NULL DEFAULT(0)"),
        new DatabaseCommand(47, 24, "ALTER TABLE AnimeGroup DROP COLUMN ContractString"),
        new DatabaseCommand(48, 1, "ALTER TABLE AniDB_Anime DROP COLUMN AllCategories"),
        new DatabaseCommand(49, 1, DatabaseFixes.DeleteSeriesUsersWithoutSeries),
        new DatabaseCommand(50, 1,
            "CREATE TABLE VideoLocal_Place ( VideoLocal_Place_ID int IDENTITY(1,1) NOT NULL, VideoLocalID int NOT NULL, FilePath nvarchar(MAX) NOT NULL,  ImportFolderID int NOT NULL, ImportFolderType int NOT NULL, CONSTRAINT [PK_VideoLocal_Place] PRIMARY KEY CLUSTERED (  VideoLocal_Place_ID ASC  ) WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] ) ON [PRIMARY]"),
        new DatabaseCommand(50, 2,
            "ALTER TABLE VideoLocal ADD FileName nvarchar(max) NOT NULL DEFAULT(''), VideoCodec varchar(max) NOT NULL DEFAULT(''), VideoBitrate varchar(max) NOT NULL DEFAULT(''), VideoBitDepth varchar(max) NOT NULL DEFAULT(''), VideoFrameRate varchar(max) NOT NULL DEFAULT(''), VideoResolution varchar(max) NOT NULL DEFAULT(''), AudioCodec varchar(max) NOT NULL DEFAULT(''), AudioBitrate varchar(max) NOT NULL DEFAULT(''),Duration bigint NOT NULL DEFAULT(0)"),
        new DatabaseCommand(50, 3,
            "INSERT INTO VideoLocal_Place (VideoLocalID, FilePath, ImportFolderID, ImportFolderType) SELECT VideoLocalID, FilePath, ImportFolderID, 1 as ImportFolderType FROM VideoLocal"),
        new DatabaseCommand(50, 4, "ALTER TABLE VideoLocal DROP COLUMN FilePath, ImportFolderID"),
        new DatabaseCommand(50, 5,
            "UPDATE VideoLocal SET VideoLocal.FileName=VideoInfo.FileName, VideoLocal.VideoCodec=VideoInfo.VideoCodec, VideoLocal.VideoBitrate=VideoInfo.VideoBitrate, VideoLocal.VideoBitDepth=VideoInfo.VideoBitDepth, VideoLocal.VideoFrameRate=VideoInfo.VideoFrameRate,VideoLocal.VideoResolution=VideoInfo.VideoResolution,VideoLocal.AudioCodec=VideoInfo.AudioCodec,VideoLocal.AudioBitrate=VideoInfo.AudioBitrate, VideoLocal.Duration=VideoInfo.Duration FROM VideoLocal INNER JOIN VideoInfo ON VideoLocal.Hash=VideoInfo.Hash"),
        new DatabaseCommand(50, 6,
            "CREATE TABLE CloudAccount (  CloudID int IDENTITY(1,1) NOT NULL, ConnectionString nvarchar(MAX) NOT NULL,  Provider nvarchar(MAX) NOT NULL, Name nvarchar(MAX) NOT NULL,  CONSTRAINT [PK_CloudAccount] PRIMARY KEY CLUSTERED   ( CloudID ASC ) WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]) ON [PRIMARY]"),
        new DatabaseCommand(50, 7, "ALTER TABLE ImportFolder ADD CloudID int NULL"),
        new DatabaseCommand(50, 8, "ALTER TABLE VideoLocal_User ALTER COLUMN WatchedDate datetime NULL"),
        new DatabaseCommand(50, 9, "ALTER TABLE VideoLocal_User ADD ResumePosition bigint NOT NULL DEFAULT (0)"),
        new DatabaseCommand(50, 10, "DROP TABLE VideoInfo"),
        new DatabaseCommand(51, 1, "DROP INDEX UIX_VideoLocal_Hash ON VideoLocal;"),
        new DatabaseCommand(51, 2, "CREATE INDEX IX_VideoLocal_Hash ON VideoLocal(Hash);"),
        new DatabaseCommand(52, 1,
            "CREATE TABLE AuthTokens ( AuthID int IDENTITY(1,1) NOT NULL, UserID int NOT NULL, DeviceName nvarchar(MAX) NOT NULL, Token nvarchar(MAX) NOT NULL )"),
        new DatabaseCommand(53, 1,
            "CREATE TABLE Scan ( ScanID int IDENTITY(1,1) NOT NULL, CreationTime datetime NOT NULL, ImportFolders nvarchar(MAX) NOT NULL, Status int NOT NULL )"),
        new DatabaseCommand(53, 2,
            "CREATE TABLE ScanFile ( ScanFileID int IDENTITY(1,1) NOT NULL, ScanID int NOT NULL, ImportFolderID int NOT NULL, VideoLocal_Place_ID int NOT NULL, FullName  nvarchar(MAX) NOT NULL, FileSize bigint NOT NULL, Status int NOT NULL, CheckDate datetime NULL, Hash nvarchar(100) NOT NULL, HashResult nvarchar(100) NULL )"),
        new DatabaseCommand(53, 3, "CREATE INDEX UIX_ScanFileStatus ON ScanFile(ScanID,Status,CheckDate);"),
        new DatabaseCommand(54, 1, DatabaseFixes.NoOperation),
        new DatabaseCommand(55, 1, DatabaseFixes.NoOperation),
        new DatabaseCommand(56, 1, DatabaseFixes.NoOperation),
        new DatabaseCommand(57, 1, "ALTER TABLE JMMUser ADD PlexToken nvarchar(max) NULL"),
        new DatabaseCommand(58, 1, "ALTER TABLE AniDB_File ADD IsChaptered INT NOT NULL DEFAULT(-1)"),
        new DatabaseCommand(59, 1, "ALTER TABLE RenameScript ADD RenamerType nvarchar(max) NOT NULL DEFAULT('Legacy')"),
        new DatabaseCommand(59, 2, "ALTER TABLE RenameScript ADD ExtraData nvarchar(max)"),
        new DatabaseCommand(60, 1,
            "CREATE INDEX IX_AniDB_Anime_Character_CharID ON AniDB_Anime_Character(CharID);"),
        new DatabaseCommand(61, 1, "ALTER TABLE TvDB_Episode ADD Rating INT NULL"),
        new DatabaseCommand(61, 2, "ALTER TABLE TvDB_Episode ADD AirDate datetime NULL"),
        new DatabaseCommand(61, 3, "ALTER TABLE TvDB_Episode DROP COLUMN FirstAired"),
        new DatabaseCommand(61, 4, DatabaseFixes.NoOperation),
        new DatabaseCommand(62, 1, "ALTER TABLE AnimeSeries ADD AirsOn varchar(10) NULL"),
        new DatabaseCommand(63, 1, "DROP TABLE Trakt_ImageFanart"),
        new DatabaseCommand(63, 2, "DROP TABLE Trakt_ImagePoster"),
        new DatabaseCommand(64, 1, "CREATE TABLE AnimeCharacter ( CharacterID INT IDENTITY(1,1) NOT NULL, AniDBID INT NOT NULL, Name NVARCHAR(MAX) NOT NULL, AlternateName NVARCHAR(MAX), Description NVARCHAR(MAX), ImagePath NVARCHAR(MAX) )"),
        new DatabaseCommand(64, 2, "CREATE TABLE AnimeStaff ( StaffID INT IDENTITY(1,1) NOT NULL, AniDBID INT NOT NULL, Name NVARCHAR(MAX) NOT NULL, AlternateName NVARCHAR(MAX), Description NVARCHAR(MAX), ImagePath NVARCHAR(MAX) )"),
        new DatabaseCommand(64, 3, "CREATE TABLE CrossRef_Anime_Staff ( CrossRef_Anime_StaffID INT IDENTITY(1,1) NOT NULL, AniDB_AnimeID INT NOT NULL, StaffID INT NOT NULL, Role NVARCHAR(MAX), RoleID INT, RoleType INT NOT NULL, Language NVARCHAR(MAX) NOT NULL )"),
        new DatabaseCommand(64, 4, DatabaseFixes.NoOperation),
        new DatabaseCommand(65, 1, "ALTER TABLE MovieDB_Movie ADD Rating INT NOT NULL DEFAULT(0)"),
        new DatabaseCommand(65, 2, "ALTER TABLE TvDB_Series ADD Rating INT NULL"),
        new DatabaseCommand(66, 1, "ALTER TABLE AniDB_Episode ADD Description nvarchar(max) NOT NULL DEFAULT('')"),
        new DatabaseCommand(66, 2, DatabaseFixes.NoOperation),
        new DatabaseCommand(67, 1, DatabaseFixes.RefreshAniDBInfoFromXML),
        new DatabaseCommand(68, 1, DatabaseFixes.NoOperation),
        new DatabaseCommand(68, 2, DatabaseFixes.UpdateAllStats),
        new DatabaseCommand(69, 1, DatabaseFixes.NoOperation),
        new DatabaseCommand(70, 1, "ALTER TABLE AniDB_Character ALTER COLUMN CharName nvarchar(max) NOT NULL"),
        new DatabaseCommand(71, 1, "CREATE TABLE AniDB_AnimeUpdate ( AniDB_AnimeUpdateID INT IDENTITY(1,1) NOT NULL, AnimeID INT NOT NULL, UpdatedAt datetime NOT NULL )"),
        new DatabaseCommand(71, 2, "CREATE UNIQUE INDEX UIX_AniDB_AnimeUpdate ON AniDB_AnimeUpdate(AnimeID)"),
        new DatabaseCommand(71, 3, DatabaseFixes.MigrateAniDB_AnimeUpdates),
        new DatabaseCommand(72, 1, DatabaseFixes.NoOperation),
        new DatabaseCommand(73, 1, DatabaseFixes.NoOperation),
        new DatabaseCommand(74, 1, DatabaseFixes.NoOperation),
        new DatabaseCommand(75, 1, "DROP INDEX UIX_CrossRef_AniDB_MAL_Anime ON CrossRef_AniDB_MAL;"),
        new DatabaseCommand(75, 2, "ALTER TABLE AniDB_Anime ADD Site_JP nvarchar(max), Site_EN nvarchar(max), Wikipedia_ID nvarchar(max), WikipediaJP_ID nvarchar(max), SyoboiID int, AnisonID int, CrunchyrollID nvarchar(max)"),
        new DatabaseCommand(75, 3, DatabaseFixes.NoOperation),
        new DatabaseCommand(76, 1, "ALTER TABLE VideoLocal ADD MyListID INT NOT NULL DEFAULT(0)"),
        new DatabaseCommand(76, 2, DatabaseFixes.NoOperation),
        new DatabaseCommand(77, 1, "ALTER TABLE AniDB_Episode DROP COLUMN EnglishName"),
        new DatabaseCommand(77, 2, "ALTER TABLE AniDB_Episode DROP COLUMN RomajiName"),
        new DatabaseCommand(77, 3, "CREATE TABLE AniDB_Episode_Title ( AniDB_Episode_TitleID int IDENTITY(1,1) NOT NULL, AniDB_EpisodeID int NOT NULL, Language nvarchar(50) NOT NULL, Title nvarchar(500) NOT NULL )"),
        new DatabaseCommand(77, 4, DatabaseFixes.NoOperation),
        new DatabaseCommand(78, 1, "DROP INDEX UIX_CrossRef_AniDB_TvDB_Episode_AniDBEpisodeID ON CrossRef_AniDB_TvDB_Episode;"),
        new DatabaseCommand(78, 2, "exec sp_rename CrossRef_AniDB_TvDB_Episode, CrossRef_AniDB_TvDB_Episode_Override;"),
        new DatabaseCommand(78, 3, "ALTER TABLE CrossRef_AniDB_TvDB_Episode_Override DROP COLUMN AnimeID"),
        new DatabaseCommand(78, 4, "exec sp_rename 'CrossRef_AniDB_TvDB_Episode_Override.CrossRef_AniDB_TvDB_EpisodeID', 'CrossRef_AniDB_TvDB_Episode_OverrideID', 'COLUMN';"),
        new DatabaseCommand(78, 5, "CREATE UNIQUE INDEX UIX_AniDB_TvDB_Episode_Override_AniDBEpisodeID_TvDBEpisodeID ON CrossRef_AniDB_TvDB_Episode_Override(AniDBEpisodeID,TvDBEpisodeID);"),
        // For some reason, this was never dropped
        new DatabaseCommand(78, 6, "DROP TABLE CrossRef_AniDB_TvDB;"),
        new DatabaseCommand(78, 7, "CREATE TABLE CrossRef_AniDB_TvDB(CrossRef_AniDB_TvDBID int IDENTITY(1,1) NOT NULL, AniDBID int NOT NULL, TvDBID int NOT NULL, CrossRefSource INT NOT NULL);"),
        new DatabaseCommand(78, 8, "CREATE UNIQUE INDEX UIX_AniDB_TvDB_AniDBID_TvDBID ON CrossRef_AniDB_TvDB(AniDBID,TvDBID);"),
        new DatabaseCommand(78, 9, "CREATE TABLE CrossRef_AniDB_TvDB_Episode(CrossRef_AniDB_TvDB_EpisodeID int IDENTITY(1,1) NOT NULL, AniDBEpisodeID int NOT NULL, TvDBEpisodeID int NOT NULL, MatchRating INT NOT NULL);"),
        new DatabaseCommand(78, 10, "CREATE UNIQUE INDEX UIX_CrossRef_AniDB_TvDB_Episode_AniDBID_TvDBID ON CrossRef_AniDB_TvDB_Episode(AniDBEpisodeID,TvDBEpisodeID);"),
        new DatabaseCommand(78, 11, DatabaseFixes.NoOperation),
        // DatabaseFixes.MigrateTvDBLinks_v2_to_V3() drops the CrossRef_AniDB_TvDBV2 table. We do it after init to migrate
        new DatabaseCommand(79, 1, DatabaseFixes.NoOperation),
        new DatabaseCommand(80, 1, DatabaseFixes.NoOperation),
        new DatabaseCommand(81, 1, "ALTER TABLE AnimeSeries ADD UpdatedAt datetime NOT NULL DEFAULT '2000-01-01 00:00:00';"),
        new DatabaseCommand(82, 1, DatabaseFixes.NoOperation),
        new DatabaseCommand(83, 1, DropVideoLocalMediaColumns),
        new DatabaseCommand(84, 1, "DROP INDEX IF EXISTS UIX_CrossRef_AniDB_MAL_MALID ON CrossRef_AniDB_MAL;"),
        new DatabaseCommand(85, 1, "DROP INDEX IF EXISTS UIX_AniDB_File_FileID ON AniDB_File;"),
        new DatabaseCommand(86, 1, "CREATE TABLE AniDB_Anime_Staff ( AniDB_Anime_StaffID int IDENTITY(1,1) NOT NULL, AnimeID int NOT NULL, CreatorID int NOT NULL, CreatorType varchar(50) NOT NULL );"),
        new DatabaseCommand(86, 2, DatabaseFixes.RefreshAniDBInfoFromXML),
        new DatabaseCommand(87, 1, DatabaseFixes.EnsureNoOrphanedGroupsOrSeries),
        new DatabaseCommand(88, 1, "UPDATE VideoLocal_User SET WatchedDate = NULL WHERE WatchedDate = '1970-01-01 00:00:00';"),
        new DatabaseCommand(88, 2, "ALTER TABLE VideoLocal_User ADD WatchedCount INT NOT NULL DEFAULT 0;"),
        new DatabaseCommand(88, 3, "ALTER TABLE VideoLocal_User ADD LastUpdated datetime NOT NULL DEFAULT CURRENT_TIMESTAMP;"),
        new DatabaseCommand(88, 4, "UPDATE VideoLocal_User SET WatchedCount = 1, LastUpdated = WatchedDate WHERE WatchedDate IS NOT NULL;"),
        new DatabaseCommand(89, 1, "ALTER TABLE AnimeSeries_User ADD LastEpisodeUpdate datetime DEFAULT NULL;"),
        new DatabaseCommand(89, 2, DatabaseFixes.FixWatchDates),
        new DatabaseCommand(90, 1, "ALTER TABLE AnimeGroup ADD MainAniDBAnimeID INT DEFAULT NULL;"),
        new DatabaseCommand(91, 1, DropDefaultsOnAnimeEpisode_User),
        new DatabaseCommand(91, 2, "ALTER TABLE AnimeEpisode_User DROP COLUMN ContractSize;"),
        new DatabaseCommand(91, 3, "ALTER TABLE AnimeEpisode_User DROP COLUMN ContractBlob;"),
        new DatabaseCommand(91, 4, "ALTER TABLE AnimeEpisode_User DROP COLUMN ContractVersion;"),
        new DatabaseCommand(92, 1, "ALTER TABLE AniDB_File DROP COLUMN File_AudioCodec, File_VideoCodec, File_VideoResolution, File_FileExtension, File_LengthSeconds, Anime_GroupName, Anime_GroupNameShort, Episode_Rating, Episode_Votes, IsWatched, WatchedDate, CRC, MD5, SHA1"),
        new DatabaseCommand(92, 2, DropDefaultOnChaptered),
        new DatabaseCommand(92, 3, "ALTER TABLE AniDB_File Alter COLUMN IsCensored bit NULL; ALTER TABLE AniDB_File ALTER COLUMN IsDeprecated bit not null; ALTER TABLE AniDB_File ALTER COLUMN IsChaptered bit not null"),
        new DatabaseCommand(92, 4, "ALTER TABLE AniDB_GroupStatus Alter COLUMN Rating decimal(6,2) NULL; UPDATE AniDB_GroupStatus SET Rating = Rating / 100 WHERE Rating > 10"),
        new DatabaseCommand(92, 5, "ALTER TABLE AniDB_Character DROP COLUMN CreatorListRaw;"),
        new DatabaseCommand(92, 6, "ALTER TABLE AniDB_Anime_Character DROP COLUMN EpisodeListRaw;"),
        new DatabaseCommand(92, 7, "ALTER TABLE AniDB_Anime DROP COLUMN AwardList;"),
        new DatabaseCommand(92, 8, "ALTER TABLE AniDB_File DROP COLUMN AnimeID;"),
        new DatabaseCommand(93, 1, "ALTER TABLE CrossRef_Languages_AniDB_File ADD LanguageName nvarchar(100) NOT NULL DEFAULT '';"),
        new DatabaseCommand(93, 2, "UPDATE c SET LanguageName = l.LanguageName FROM CrossRef_Languages_AniDB_File c INNER JOIN Language l ON l.LanguageID = c.LanguageID WHERE c.LanguageName = '';"),
        new DatabaseCommand(93, 3, "ALTER TABLE CrossRef_Languages_AniDB_File DROP COLUMN LanguageID;"),
        new DatabaseCommand(93, 4, "ALTER TABLE CrossRef_Subtitles_AniDB_File ADD LanguageName nvarchar(100) NOT NULL DEFAULT '';"),
        new DatabaseCommand(93, 5, "UPDATE c SET LanguageName = l.LanguageName FROM CrossRef_Subtitles_AniDB_File c INNER JOIN Language l ON l.LanguageID = c.LanguageID WHERE c.LanguageName = '';"),
        new DatabaseCommand(93, 6, "ALTER TABLE CrossRef_Subtitles_AniDB_File DROP COLUMN LanguageID;"),
        new DatabaseCommand(93, 7, "DROP TABLE Language;"),
        new DatabaseCommand(94, 1, "DROP TABLE AniDB_Anime_Category"),
        new DatabaseCommand(94, 2, "DROP TABLE AniDB_Anime_Review"),
        new DatabaseCommand(94, 3, "DROP TABLE AniDB_Category"),
        new DatabaseCommand(94, 4, "DROP TABLE AniDB_MylistStats"),
        new DatabaseCommand(94, 5, "DROP TABLE AniDB_Review"),
        new DatabaseCommand(94, 6, "DROP TABLE CloudAccount"),
        new DatabaseCommand(94, 7, "DROP TABLE FileFfdshowPreset"),
        new DatabaseCommand(94, 8, "DROP TABLE CrossRef_AniDB_Trakt"),
        new DatabaseCommand(94, 9, "DROP TABLE Trakt_Friend"),
        new DatabaseCommand(94, 10, "CREATE UNIQUE INDEX UIX_AniDB_File_FileID ON AniDB_File(FileID);"),
        new DatabaseCommand(95, 1, "UPDATE AniDB_File SET File_Source = 'Web' WHERE File_Source = 'www'; UPDATE AniDB_File SET File_Source = 'BluRay' WHERE File_Source = 'Blu-ray'; UPDATE AniDB_File SET File_Source = 'LaserDisc' WHERE File_Source = 'LD'; UPDATE AniDB_File SET File_Source = 'Unknown' WHERE File_Source = 'unknown';"),
        new DatabaseCommand(96, 1, "ALTER TABLE AniDB_GroupStatus ALTER COLUMN GroupName nvarchar(max); ALTER TABLE AniDB_GroupStatus ALTER COLUMN EpisodeRange nvarchar(max);"),
        new DatabaseCommand(97, 1, "CREATE INDEX IX_AniDB_Episode_EpisodeType ON AniDB_Episode(EpisodeType);"),
        new DatabaseCommand(98, 1, "ALTER TABLE AniDB_Episode_Title ALTER COLUMN Title nvarchar(max) NOT NULL;"),
        new DatabaseCommand(99, 1, "ALTER TABLE VideoLocal ADD DateTimeImported datetime DEFAULT NULL;"),
        new DatabaseCommand(99, 2, "UPDATE v SET DateTimeImported = DateTimeCreated FROM VideoLocal v INNER JOIN CrossRef_File_Episode CRFE on v.Hash = CRFE.Hash;"),
        new DatabaseCommand(100, 1, "ALTER TABLE AniDB_Tag ADD Verified integer NOT NULL DEFAULT 0;"),
        new DatabaseCommand(100, 2, "ALTER TABLE AniDB_Tag ADD ParentTagID integer DEFAULT NULL;"),
        new DatabaseCommand(100, 3, "ALTER TABLE AniDB_Tag ADD TagNameOverride varchar(150) DEFAULT NULL;"),
        new DatabaseCommand(100, 4, "ALTER TABLE AniDB_Tag ADD LastUpdated datetime NOT NULL DEFAULT '1970-01-01 00:00:00';"),
        new DatabaseCommand(100, 5, "ALTER TABLE AniDB_Tag DROP COLUMN Spoiler;"),
        new DatabaseCommand(100, 6, "ALTER TABLE AniDB_Tag DROP COLUMN LocalSpoiler;"),
        new DatabaseCommand(100, 7, "ALTER TABLE AniDB_Tag DROP COLUMN TagCount;"),
        new DatabaseCommand(100, 8, "ALTER TABLE AniDB_Anime_Tag ADD LocalSpoiler integer NOT NULL DEFAULT 0;"),
        new DatabaseCommand(100, 9, "ALTER TABLE AniDB_Anime_Tag DROP COLUMN Approval;"),
        new DatabaseCommand(100, 10, DatabaseFixes.FixTagParentIDsAndNameOverrides),
        new DatabaseCommand(101, 1, "ALTER TABLE AnimeEpisode ADD IsHidden integer NOT NULL DEFAULT 0;"),
        new DatabaseCommand(101, 2, "ALTER TABLE AnimeSeries_User ADD HiddenUnwatchedEpisodeCount integer NOT NULL DEFAULT 0;"),
        new DatabaseCommand(102, 1, "UPDATE v SET DateTimeImported = DateTimeCreated FROM VideoLocal v INNER JOIN CrossRef_File_Episode CRFE on v.Hash = CRFE.Hash;"),
        new DatabaseCommand(103, 1, "CREATE TABLE AniDB_FileUpdate ( AniDB_FileUpdateID INT IDENTITY(1,1) NOT NULL, FileSize BIGINT NOT NULL, Hash nvarchar(150) NOT NULL, HasResponse BIT NOT NULL, UpdatedAt datetime NOT NULL )"),
        new DatabaseCommand(103, 2, "CREATE INDEX IX_AniDB_FileUpdate ON AniDB_FileUpdate(FileSize, Hash)"),
        new DatabaseCommand(103, 3, DatabaseFixes.MigrateAniDB_FileUpdates),
        new DatabaseCommand(104, 1, "ALTER TABLE AniDB_Anime DROP COLUMN DisableExternalLinksFlag;"),
        new DatabaseCommand(104, 2, "ALTER TABLE AnimeSeries ADD DisableAutoMatchFlags integer NOT NULL DEFAULT 0;"),
        new DatabaseCommand(104, 3, "ALTER TABLE AniDB_Anime ADD VNDBID int, BangumiID int, LianID int, FunimationID nvarchar(max), HiDiveID nvarchar(max)"),
        new DatabaseCommand(105, 1, "ALTER TABLE AniDB_Anime DROP COLUMN LianID;"),
        new DatabaseCommand(105, 2, "ALTER TABLE AniDB_Anime DROP COLUMN AnimePlanetID;"),
        new DatabaseCommand(105, 3, "ALTER TABLE AniDB_Anime DROP COLUMN AnimeNfo;"),
        new DatabaseCommand(105, 4, "ALTER TABLE AniDB_Anime ADD LainID INT NULL"),
        new DatabaseCommand(106, 1, DatabaseFixes.FixEpisodeDateTimeUpdated),
        new DatabaseCommand(107, 1, "ALTER TABLE AnimeSeries ADD HiddenMissingEpisodeCount int NOT NULL DEFAULT 0;"),
        new DatabaseCommand(107, 2, "ALTER TABLE AnimeSeries ADD HiddenMissingEpisodeCountGroups int NOT NULL DEFAULT 0;"),
        new DatabaseCommand(107, 3, DatabaseFixes.UpdateSeriesWithHiddenEpisodes),
        new DatabaseCommand(108, 1, "UPDATE AniDB_Anime SET AirDate = NULL, BeginYear = 0 WHERE AirDate = '1970-01-01 00:00:00';"),
        new DatabaseCommand(109, 1, "ALTER TABLE JMMUser ADD AvatarImageBlob VARBINARY(MAX) NULL;"),
        new DatabaseCommand(109, 2, "ALTER TABLE JMMUser ADD AvatarImageMetadata NVARCHAR(128) NULL;"),
        new DatabaseCommand(110, 1, "ALTER TABLE VideoLocal ADD LastAVDumped datetime;"),
        new DatabaseCommand(110, 2, "ALTER TABLE VideoLocal ADD LastAVDumpVersion nvarchar(128);"),
        new DatabaseCommand(111, 1, DatabaseFixes.FixAnimeSourceLinks),
        new DatabaseCommand(111, 2, DatabaseFixes.FixOrphanedShokoEpisodes),
        new DatabaseCommand(112, 1,
            "CREATE TABLE FilterPreset( FilterPresetID INT IDENTITY(1,1), ParentFilterPresetID int, Name nvarchar(250) NOT NULL, FilterType int NOT NULL, Locked bit NOT NULL, Hidden bit NOT NULL, ApplyAtSeriesLevel bit NOT NULL, Expression nvarchar(max), SortingExpression nvarchar(max) ); "),
        new DatabaseCommand(112, 2,
            "CREATE INDEX IX_FilterPreset_ParentFilterPresetID ON FilterPreset(ParentFilterPresetID); CREATE INDEX IX_FilterPreset_Name ON FilterPreset(Name); CREATE INDEX IX_FilterPreset_FilterType ON FilterPreset(FilterType); CREATE INDEX IX_FilterPreset_LockedHidden ON FilterPreset(Locked, Hidden);"),
        new DatabaseCommand(112, 3, "DELETE FROM GroupFilter WHERE FilterType = 2; DELETE FROM GroupFilter WHERE FilterType = 16;"),
        new DatabaseCommand(112, 4, DatabaseFixes.MigrateGroupFilterToFilterPreset),
        new DatabaseCommand(112, 5, DatabaseFixes.DropGroupFilter),
        new DatabaseCommand(113, 1, "ALTER TABLE AnimeGroup DROP COLUMN SortName;"),
        new DatabaseCommand(114, 1, "ALTER TABLE AnimeEpisode DROP COLUMN PlexContractBlob;ALTER TABLE AnimeGroup_User DROP COLUMN PlexContractBlob;ALTER TABLE AnimeSeries_User DROP COLUMN PlexContractBlob;"),
        new DatabaseCommand(114, 2, DropPlexContractColumns),
        new DatabaseCommand(115, 1, "CREATE INDEX IX_CommandRequest_CommandType ON CommandRequest(CommandType); CREATE INDEX IX_CommandRequest_Priority_Date ON CommandRequest(Priority, DateTimeUpdated);"),
        new DatabaseCommand(116, 1, "DROP TABLE CommandRequest"),
        new DatabaseCommand(117, 1, "ALTER TABLE AnimeEpisode ADD EpisodeNameOverride nvarchar(500) NULL"),
        new DatabaseCommand(118, 1, "DELETE FROM FilterPreset WHERE FilterType IN (16, 24, 32, 40, 64, 72)"),
        new DatabaseCommand(119, 1, DropContracts),
        new DatabaseCommand(120, 1, DropVideoLocalMediaSize),
        new DatabaseCommand(121, 1, "CREATE TABLE AniDB_NotifyQueue( AniDB_NotifyQueueID int IDENTITY(1,1) NOT NULL, Type int NOT NULL, ID int NOT NULL, AddedAt datetime NOT NULL ); "),
        new DatabaseCommand(121, 2, "CREATE TABLE AniDB_Message( AniDB_MessageID int IDENTITY(1,1) NOT NULL, MessageID int NOT NULL, FromUserID int NOT NULL, FromUserName nvarchar(100), SentAt datetime NOT NULL, FetchedAt datetime NOT NULL, Type int NOT NULL, Title nvarchar(MAX), Body nvarchar(MAX), Flags int NOT NULL DEFAULT(0) ); "),
        new DatabaseCommand(122, 1, "CREATE TABLE CrossRef_AniDB_TMDB_Episode ( CrossRef_AniDB_TMDB_EpisodeID INT IDENTITY(1,1) NOT NULL, AnidbAnimeID INT NOT NULL, AnidbEpisodeID INT NOT NULL, TmdbShowID INT NOT NULL, TmdbEpisodeID INT NOT NULL, Ordering INT NOT NULL, MatchRating INT NOT NULL );"),
        new DatabaseCommand(122, 2, "CREATE TABLE CrossRef_AniDB_TMDB_Movie ( CrossRef_AniDB_TMDB_MovieID INT IDENTITY(1,1) NOT NULL, AnidbAnimeID INT NOT NULL, AnidbEpisodeID INT NULL, TmdbMovieID INT NOT NULL, Source INT NOT NULL );"),
        new DatabaseCommand(122, 3, "CREATE TABLE CrossRef_AniDB_TMDB_Show ( CrossRef_AniDB_TMDB_ShowID INT IDENTITY(1,1) NOT NULL, AnidbAnimeID INT NOT NULL, TmdbShowID INT NOT NULL, Source INT NOT NULL );"),
        new DatabaseCommand(122, 4, "CREATE TABLE TMDB_Image ( TMDB_ImageID INT IDENTITY(1,1) NOT NULL, TmdbMovieID INT NULL, TmdbEpisodeID INT NULL, TmdbSeasonID INT NULL, TmdbShowID INT NULL, TmdbCollectionID INT NULL, TmdbNetworkID INT NULL, TmdbCompanyID INT NULL, TmdbPersonID INT NULL, ForeignType INT NOT NULL, ImageType INT NOT NULL, IsEnabled INT NOT NULL, Width INT NOT NULL, Height INT NOT NULL, Language NVARCHAR(32) NOT NULL, RemoteFileName NVARCHAR(128) NOT NULL, UserRating decimal(6,2) NOT NULL, UserVotes INT NOT NULL );"),
        new DatabaseCommand(122, 5, "CREATE TABLE AniDB_Anime_PreferredImage ( AniDB_Anime_PreferredImageID INT IDENTITY(1,1) NOT NULL, AnidbAnimeID INT NOT NULL, ImageID INT NOT NULL, ImageType INT NOT NULL, ImageSource INT NOT NULL );"),
        new DatabaseCommand(122, 6, "CREATE TABLE TMDB_Title ( TMDB_TitleID INT IDENTITY(1,1) NOT NULL, ParentID INT NOT NULL, ParentType INT NOT NULL, LanguageCode NVARCHAR(5) NOT NULL, CountryCode NVARCHAR(5) NOT NULL, Value  NVARCHAR(512) NOT NULL );"),
        new DatabaseCommand(122, 7, "CREATE TABLE TMDB_Overview ( TMDB_OverviewID INT IDENTITY(1,1) NOT NULL, ParentID INT NOT NULL, ParentType INT NOT NULL, LanguageCode NVARCHAR(5) NOT NULL, CountryCode NVARCHAR(5) NOT NULL, Value NVARCHAR(MAX) NOT NULL );"),
        new DatabaseCommand(122, 8, "CREATE TABLE TMDB_Company ( TMDB_CompanyID INT IDENTITY(1,1) NOT NULL, TmdbCompanyID INT NOT NULL, Name NVARCHAR(512) NOT NULL, CountryOfOrigin NVARCHAR(3) NOT NULL );"),
        new DatabaseCommand(122, 9, "CREATE TABLE TMDB_Network ( TMDB_NetworkID INT IDENTITY(1,1) NOT NULL, TmdbNetworkID INT NOT NULL, Name NVARCHAR(512) NOT NULL, CountryOfOrigin NVARCHAR(3) NOT NULL );"),
        new DatabaseCommand(122, 10, "CREATE TABLE TMDB_Person ( TMDB_PersonID INT IDENTITY(1,1) NOT NULL, TmdbPersonID INT NOT NULL, EnglishName NVARCHAR(512) NOT NULL, EnglishBiography NVARCHAR(MAX) NOT NULL, Aliases NVARCHAR(MAX) NOT NULL, Gender INT NOT NULL, IsRestricted BIT NOT NULL, BirthDay DATE NULL, DeathDay DATE NULL, PlaceOfBirth NVARCHAR(MAX) NULL, CreatedAt DATETIME NOT NULL, LastUpdatedAt DATETIME NOT NULL );"),
        new DatabaseCommand(122, 11, "CREATE TABLE TMDB_Movie ( TMDB_MovieID INT IDENTITY(1,1) NOT NULL, TmdbMovieID INT NOT NULL, TmdbCollectionID INT NULL, EnglishTitle NVARCHAR(512) NOT NULL, EnglishOvervie NVARCHAR(MAX) NOT NULL, OriginalTitle NVARCHAR(512) NOT NULL, OriginalLanguageCode NVARCHAR(5) NOT NULL, IsRestricted BIT NOT NULL, IsVideo BIT NOT NULL, Genres NVARCHAR(128) NOT NULL, ContentRatings NVARCHAR(128) NOT NULL, Runtime INT NULL, UserRating decimal(6,2) NOT NULL, UserVotes INT NOT NULL, ReleasedAt DATE NULL, CreatedAt DATETIME NOT NULL, LastUpdatedAt DATETIME NOT NULL );"),
        new DatabaseCommand(122, 12, "CREATE TABLE TMDB_Movie_Cast ( TMDB_Movie_CastID INT IDENTITY(1,1) NOT NULL, TmdbMovieID INT NOT NULL, TmdbPersonID INT NOT NULL, TmdbCreditID NVARCHAR(64) NOT NULL, CharacterName NVARCHAR(512) NOT NULL, Ordering INT NOT NULL );"),
        new DatabaseCommand(122, 13, "CREATE TABLE TMDB_Company_Entity ( TMDB_Company_EntityID INT IDENTITY(1,1) NOT NULL, TmdbCompanyID INT NOT NULL, TmdbEntityType INT NOT NULL, TmdbEntityID INT NOT NULL, Ordering INT NOT NULL, ReleasedAt DATE NULL );"),
        new DatabaseCommand(122, 14, "CREATE TABLE TMDB_Movie_Crew ( TMDB_Movie_CrewID INT IDENTITY(1,1) NOT NULL, TmdbMovieID INT NOT NULL, TmdbPersonID INT NOT NULL, TmdbCreditID NVARCHAR(64) NOT NULL, Job NVARCHAR(64) NOT NULL, Department NVARCHAR(64) NOT NULL );"),
        new DatabaseCommand(122, 15, "CREATE TABLE TMDB_Show ( TMDB_ShowID INT IDENTITY(1,1) NOT NULL, TmdbShowID INT NOT NULL, EnglishTitle NVARCHAR(512) NOT NULL, EnglishOverview NVARCHAR(MAX) NOT NULL, OriginalTitle NVARCHAR(512) NOT NULL, OriginalLanguageCode NVARCHAR(5) NOT NULL, IsRestricted BIT NOT NULL, Genres NVARCHAR(128) NOT NULL, ContentRatings NVARCHAR(128) NOT NULL, EpisodeCount INT NOT NULL, SeasonCount INT NOT NULL, AlternateOrderingCount INT NOT NULL, UserRating decimal(6,2) NOT NULL, UserVotes INT NOT NULL, FirstAiredAt DATE, LastAiredAt DATE NULL, CreatedAt DATETIME NOT NULL, LastUpdatedAt DATETIME NOT NULL );"),
        new DatabaseCommand(122, 16, "CREATE TABLE Tmdb_Show_Network ( TMDB_Show_NetworkID INT IDENTITY(1,1) NOT NULL, TmdbShowID INT NOT NULL, TmdbNetworkID INT NOT NULL, Ordering INT NOT NULL );"),
        new DatabaseCommand(122, 17, "CREATE TABLE TMDB_Season ( TMDB_SeasonID INT IDENTITY(1,1) NOT NULL, TmdbShowID INT NOT NULL, TmdbSeasonID INT NOT NULL, EnglishTitle NVARCHAR(512) NOT NULL, EnglishOverview NVARCHAR(MAX) NOT NULL, EpisodeCount INT NOT NULL, SeasonNumber INT NOT NULL, CreatedAt DATETIME NOT NULL, LastUpdatedAt DATETIME NOT NULL );"),
        new DatabaseCommand(122, 18, "CREATE TABLE TMDB_Episode ( TMDB_EpisodeID INT IDENTITY(1,1) NOT NULL, TmdbShowID INT NOT NULL, TmdbSeasonID INT NOT NULL, TmdbEpisodeID INT NOT NULL, EnglishTitle NVARCHAR(512) NOT NULL, EnglishOverview NVARCHAR(MAX) NOT NULL, SeasonNumber INT NOT NULL, EpisodeNumber INT NOT NULL, Runtime INT NULL, UserRating decimal(6, 2) NOT NULL, UserVotes INT NOT NULL, AiredAt DATE NULL, CreatedAt DATETIME NOT NULL, LastUpdatedAt DATETIME NOT NULL );"),
        new DatabaseCommand(122, 19, "CREATE TABLE TMDB_Episode_Cast ( TMDB_Episode_CastID INT IDENTITY(1,1) NOT NULL, TmdbShowID INT NOT NULL, TmdbSeasonID INT NOT NULL, TmdbEpisodeID INT NOT NULL, TmdbPersonID INT NOT NULL, TmdbCreditID NVARCHAR(64) NOT NULL, CharacterName NVARCHAR(512) NOT NULL, IsGuestRole BIT NOT NULL, Ordering INT NOT NULL );"),
        new DatabaseCommand(122, 20, "CREATE TABLE TMDB_Episode_Crew ( TMDB_Episode_CrewID INT IDENTITY(1,1) NOT NULL, TmdbShowID INT NOT NULL, TmdbSeasonID INT NOT NULL, TmdbEpisodeID INT NOT NULL, TmdbPersonID INT NOT NULL, TmdbCreditID NVARCHAR(64) NOT NULL, Job NVARCHAR(512) NOT NULL, Department NVARCHAR(512) NOT NULL );"),
        new DatabaseCommand(122, 21, "CREATE TABLE TMDB_AlternateOrdering ( TMDB_AlternateOrderingID INT IDENTITY(1,1) NOT NULL, TmdbShowID INT NOT NULL, TmdbNetworkID INT NULL, TmdbEpisodeGroupCollectionID NVARCHAR(64) NOT NULL, EnglishTitle NVARCHAR(512) NOT NULL, EnglishOverview NVARCHAR(MAX) NOT NULL, EpisodeCount INT NOT NULL, SeasonCount INT NOT NULL, Type INT NOT NULL, CreatedAt DATETIME NOT NULL, LastUpdatedAt DATETIME NOT NULL );"),
        new DatabaseCommand(122, 22, "CREATE TABLE TMDB_AlternateOrdering_Season ( TMDB_AlternateOrdering_SeasonID INT IDENTITY(1,1) NOT NULL, TmdbShowID INT NOT NULL, TmdbEpisodeGroupCollectionID NVARCHAR(64) NOT NULL, TmdbEpisodeGroupID NVARCHAR(64) NOT NULL, EnglishTitle NVARCHAR(512) NOT NULL, SeasonNumber INT NOT NULL, EpisodeCount INT NOT NULL, IsLocked BIT NOT NULL, CreatedAt DATETIME NOT NULL, LastUpdatedAt DATETIME NOT NULL );"),
        new DatabaseCommand(122, 23, "CREATE TABLE TMDB_AlternateOrdering_Episode ( TMDB_AlternateOrdering_EpisodeID INT IDENTITY(1,1) NOT NULL, TmdbShowID INT NOT NULL, TmdbEpisodeGroupCollectionID NVARCHAR(64) NOT NULL, TmdbEpisodeGroupID NVARCHAR(64) NOT NULL, TmdbEpisodeID INT NOT NULL, SeasonNumber INT NOT NULL, EpisodeNumber INT NOT NULL, CreatedAt DATETIME NOT NULL, LastUpdatedAt DATETIME NOT NULL );"),
        new DatabaseCommand(122, 24, "CREATE TABLE TMDB_Collection ( TMDB_CollectionID INT IDENTITY(1,1) NOT NULL, TmdbCollectionID INT NOT NULL, EnglishTitle NVARCHAR(512) NOT NULL, EnglishOverview NVARCHAR(MAX) NOT NULL, MovieCount INT NOT NULL, CreatedAt DATETIME NOT NULL, LastUpdatedAt DATETIME NOT NULL );"),
        new DatabaseCommand(122, 25, "CREATE TABLE TMDB_Collection_Movie ( TMDB_Collection_MovieID INT IDENTITY(1,1) NOT NULL, TmdbCollectionID INT NOT NULL, TmdbMovieID INT NOT NULL, Ordering INT NOT NULL );"),
        new DatabaseCommand(122, 26, "INSERT INTO CrossRef_AniDB_TMDB_Movie ( AnidbAnimeID, TmdbMovieID, Source ) SELECT AnimeID, CAST ( CrossRefID AS INT ), CrossRefSource FROM CrossRef_AniDB_Other WHERE CrossRefType = 1;"),
        new DatabaseCommand(122, 27, "DROP TABLE CrossRef_AniDB_Other;"),
        new DatabaseCommand(122, 28, "DROP TABLE MovieDB_Fanart;"),
        new DatabaseCommand(122, 29, "DROP TABLE MovieDB_Movie;"),
        new DatabaseCommand(122, 30, "DROP TABLE MovieDB_Poster;"),
        new DatabaseCommand(122, 31, "DROP TABLE AniDB_Anime_DefaultImage;"),
        new DatabaseCommand(122, 32, "CREATE TABLE AniDB_Episode_PreferredImage ( AniDB_Episode_PreferredImageID INT IDENTITY(1,1) NOT NULL, AnidbAnimeID INT NOT NULL, AnidbEpisodeID INT NOT NULL, ImageID INT NOT NULL, ImageType INT NOT NULL, ImageSource INT NOT NULL );"),
        new DatabaseCommand(122, 33, DatabaseFixes.CleanupAfterAddingTMDB),
        new DatabaseCommand(122, 34, "UPDATE FilterPreset SET Expression = REPLACE(Expression, 'HasTMDbLinkExpression', 'HasTmdbLinkExpression');"),
        new DatabaseCommand(122, 35, "exec sp_rename 'TMDB_Movie.EnglishOvervie', 'EnglishOverview', 'COLUMN';"),
        new DatabaseCommand(122, 36, "UPDATE TMDB_Image SET IsEnabled = 1;"),
        new DatabaseCommand(123, 1, MigrateRenamers),
        new DatabaseCommand(123, 2, "DELETE FROM RenamerInstance WHERE Name = 'AAA_WORKINGFILE_TEMP_AAA';"),
        new DatabaseCommand(123, 3, DatabaseFixes.CreateDefaultRenamerConfig),
        new DatabaseCommand(124, 1, "ALTER TABLE TMDB_Show ADD TvdbShowID INT NULL DEFAULT NULL;"),
        new DatabaseCommand(124, 2, "ALTER TABLE TMDB_Episode ADD TvdbEpisodeID INT NULL DEFAULT NULL;"),
        new DatabaseCommand(124, 3, "ALTER TABLE TMDB_Movie ADD ImdbMovieID INT NULL DEFAULT NULL;"),
        new DatabaseCommand(124, 4, AlterImdbMovieIDType),
        new DatabaseCommand(124, 5, "CREATE INDEX IX_TMDB_Overview ON TMDB_Overview(ParentType, ParentID)"),
        new DatabaseCommand(124, 6, "CREATE INDEX IX_TMDB_Title ON TMDB_Title(ParentType, ParentID)"),
        new DatabaseCommand(124, 7, "CREATE UNIQUE INDEX UIX_TMDB_Episode_TmdbEpisodeID ON TMDB_Episode(TmdbEpisodeID)"),
        new DatabaseCommand(124, 8, "CREATE UNIQUE INDEX UIX_TMDB_Show_TmdbShowID ON TMDB_Show(TmdbShowID)"),
        new DatabaseCommand(125, 1, "UPDATE CrossRef_AniDB_TMDB_Movie SET AnidbEpisodeID = (SELECT TOP 1 EpisodeID FROM AniDB_Episode WHERE AniDB_Episode.AnimeID = CrossRef_AniDB_TMDB_Movie.AnidbAnimeID ORDER BY EpisodeType, EpisodeNumber) WHERE AnidbEpisodeID IS NULL AND EXISTS (SELECT 1 FROM AniDB_Episode WHERE AniDB_Episode.AnimeID = CrossRef_AniDB_TMDB_Movie.AnidbAnimeID);"),
        new DatabaseCommand(125, 2, "DELETE FROM CrossRef_AniDB_TMDB_Movie WHERE AnidbEpisodeID IS NULL;"),
        new DatabaseCommand(125, 3, "ALTER TABLE CrossRef_AniDB_TMDB_Movie ALTER COLUMN AnidbEpisodeID INT NOT NULL;"),
        new DatabaseCommand(125, 4, "ALTER TABLE CrossRef_AniDB_TMDB_Movie ADD CONSTRAINT DF_CrossRef_AniDB_TMDB_Movie_AnidbEpisodeID DEFAULT 0 FOR AnidbEpisodeID;"),
        new DatabaseCommand(126, 1, "ALTER TABLE TMDB_Movie ADD PosterPath NVARCHAR(64) NULL DEFAULT NULL;"),
        new DatabaseCommand(126, 2, "ALTER TABLE TMDB_Movie ADD BackdropPath NVARCHAR(64) NULL DEFAULT NULL;"),
        new DatabaseCommand(126, 3, "ALTER TABLE TMDB_Show ADD PosterPath NVARCHAR(64) NULL DEFAULT NULL;"),
        new DatabaseCommand(126, 4, "ALTER TABLE TMDB_Show ADD BackdropPath NVARCHAR(64) NULL DEFAULT NULL;"),
        new DatabaseCommand(127, 1, "UPDATE FilterPreset SET Expression = REPLACE(Expression, 'MissingTMDbLinkExpression', 'MissingTmdbLinkExpression');"),
        new DatabaseCommand(128, 1, "CREATE TABLE AniDB_Creator ( AniDB_CreatorID INT IDENTITY(1,1) NOT NULL, CreatorID INT NOT NULL, Name NVARCHAR(512) NOT NULL, OriginalName NVARCHAR(512) NULL, Type INT NOT NULL DEFAULT 0, ImagePath NVARCHAR(512) NULL, EnglishHomepageUrl NVARCHAR(512) NULL, JapaneseHomepageUrl NVARCHAR(512) NULL, EnglishWikiUrl NVARCHAR(512) NULL, JapaneseWikiUrl NVARCHAR(512) NULL, LastUpdatedAt DATETIME NOT NULL DEFAULT '2000-01-01 00:00:00', PRIMARY KEY (AniDB_CreatorID) );"),
        new DatabaseCommand(128, 2, "CREATE TABLE AniDB_Character_Creator ( AniDB_Character_CreatorID INT IDENTITY(1,1) NOT NULL, CharacterID INT NOT NULL, CreatorID INT NOT NULL, PRIMARY KEY (AniDB_Character_CreatorID) );"),
        new DatabaseCommand(128, 3, "CREATE UNIQUE INDEX UIX_AniDB_Creator_CreatorID ON AniDB_Creator(CreatorID);"),
        new DatabaseCommand(128, 4, "CREATE INDEX UIX_AniDB_Character_Creator_CreatorID ON AniDB_Character_Creator(CreatorID);"),
        new DatabaseCommand(128, 5, "CREATE INDEX UIX_AniDB_Character_Creator_CharacterID ON AniDB_Character_Creator(CharacterID);"),
        new DatabaseCommand(128, 6, "CREATE UNIQUE INDEX UIX_AniDB_Character_Creator_CharacterID_CreatorID ON AniDB_Character_Creator(CharacterID, CreatorID);"),
        new DatabaseCommand(128, 7, "INSERT INTO AniDB_Creator (CreatorID, Name, ImagePath) SELECT SeiyuuID, SeiyuuName, PicName FROM AniDB_Seiyuu;"),
        new DatabaseCommand(128, 8, "INSERT INTO AniDB_Character_Creator (CharacterID, CreatorID) SELECT CharID, SeiyuuID FROM AniDB_Character_Seiyuu;"),
        new DatabaseCommand(128, 9, "DROP TABLE AniDB_Seiyuu;"),
        new DatabaseCommand(128, 10, "DROP TABLE AniDB_Character_Seiyuu;"),
        new DatabaseCommand(129, 1, "ALTER TABLE TMDB_Show ADD PreferredAlternateOrderingID NVARCHAR(64) NULL DEFAULT NULL;"),
        new DatabaseCommand(130, 1, "ALTER TABLE TMDB_Show ALTER COLUMN ContentRatings NVARCHAR(512) NOT NULL;"),
        new DatabaseCommand(130, 2, "ALTER TABLE TMDB_Movie ALTER COLUMN ContentRatings NVARCHAR(512) NOT NULL;"),
        new DatabaseCommand(131, 1, "DROP TABLE TvDB_Episode;"),
        new DatabaseCommand(131, 2, "DROP TABLE TvDB_Series;"),
        new DatabaseCommand(131, 3, "DROP TABLE TvDB_ImageFanart;"),
        new DatabaseCommand(131, 4, "DROP TABLE TvDB_ImagePoster;"),
        new DatabaseCommand(131, 5, "DROP TABLE TvDB_ImageWideBanner;"),
        new DatabaseCommand(131, 6, "DROP TABLE CrossRef_AniDB_TvDB;"),
        new DatabaseCommand(131, 7, "DROP TABLE CrossRef_AniDB_TvDB_Episode;"),
        new DatabaseCommand(131, 8, "DROP TABLE CrossRef_AniDB_TvDB_Episode_Override;"),
        new DatabaseCommand(131, 9, "ALTER TABLE Trakt_Show DROP COLUMN TvDB_ID;"),
        new DatabaseCommand(131, 10, "ALTER TABLE Trakt_Show ADD TmdbShowID INT NULL;"),
        new DatabaseCommand(131, 11, DatabaseFixes.CleanupAfterRemovingTvDB),
        new DatabaseCommand(131, 12, DatabaseFixes.ClearQuartzQueue),
        new DatabaseCommand(132, 1, DatabaseFixes.RepairMissingTMDBPersons),
        new DatabaseCommand(133, 1, "ALTER TABLE TMDB_Movie ADD Keywords NVARCHAR(512) NULL DEFAULT NULL;"),
        new DatabaseCommand(133, 2, "ALTER TABLE TMDB_Movie ADD ProductionCountries NVARCHAR(32) NULL DEFAULT NULL;"),
        new DatabaseCommand(133, 3, "ALTER TABLE TMDB_Show ADD Keywords NVARCHAR(512) NULL DEFAULT NULL;"),
        new DatabaseCommand(133, 4, "ALTER TABLE TMDB_Show ADD ProductionCountries NVARCHAR(32) NULL DEFAULT NULL;"),
        new DatabaseCommand(134, 1, "CREATE INDEX IX_AniDB_Anime_Relation_RelatedAnimeID on AniDB_Anime_Relation(RelatedAnimeID);"),
        new DatabaseCommand(135, 1, "ALTER TABLE TMDB_Movie ALTER COLUMN ProductionCountries NVARCHAR(255) NULL;"),
        new DatabaseCommand(135, 2, "ALTER TABLE TMDB_Show ALTER COLUMN ProductionCountries NVARCHAR(255) NULL;"),
        new DatabaseCommand(136, 1, "CREATE INDEX IX_TMDB_Episode_TmdbSeasonID ON TMDB_Episode(TmdbSeasonID);"),
        new DatabaseCommand(136, 2, "CREATE INDEX IX_TMDB_Episode_TmdbShowID ON TMDB_Episode(TmdbShowID);"),
        new DatabaseCommand(137, 1, "ALTER TABLE TMDB_Episode ADD IsHidden int NOT NULL DEFAULT 0;"),
        new DatabaseCommand(137, 2, "ALTER TABLE TMDB_Season ADD HiddenEpisodeCount int NOT NULL DEFAULT 0;"),
        new DatabaseCommand(137, 3, "ALTER TABLE TMDB_Show ADD HiddenEpisodeCount int NOT NULL DEFAULT 0;"),
        new DatabaseCommand(137, 4, "ALTER TABLE TMDB_AlternateOrdering_Season ADD HiddenEpisodeCount int NOT NULL DEFAULT 0;"),
        new DatabaseCommand(137, 5, "ALTER TABLE TMDB_AlternateOrdering ADD HiddenEpisodeCount int NOT NULL DEFAULT 0;"),
        new DatabaseCommand(138, 1, "ALTER TABLE TMDB_Person ALTER COLUMN CreatedAt datetime2;"),
        new DatabaseCommand(138, 2, "ALTER TABLE TMDB_Person ALTER COLUMN LastUpdatedAt datetime2;"),
        new DatabaseCommand(138, 3, "ALTER TABLE TMDB_Movie ALTER COLUMN CreatedAt datetime2;"),
        new DatabaseCommand(138, 4, "ALTER TABLE TMDB_Movie ALTER COLUMN LastUpdatedAt datetime2;"),
        new DatabaseCommand(138, 5, "ALTER TABLE TMDB_Show ALTER COLUMN CreatedAt datetime2;"),
        new DatabaseCommand(138, 6, "ALTER TABLE TMDB_Show ALTER COLUMN LastUpdatedAt datetime2;"),
        new DatabaseCommand(138, 7, "ALTER TABLE TMDB_Season ALTER COLUMN CreatedAt datetime2;"),
        new DatabaseCommand(138, 8, "ALTER TABLE TMDB_Season ALTER COLUMN LastUpdatedAt datetime2;"),
        new DatabaseCommand(138, 9, "ALTER TABLE TMDB_Episode ALTER COLUMN CreatedAt datetime2;"),
        new DatabaseCommand(138, 10, "ALTER TABLE TMDB_Episode ALTER COLUMN LastUpdatedAt datetime2;"),
        new DatabaseCommand(138, 11, "ALTER TABLE TMDB_AlternateOrdering ALTER COLUMN CreatedAt datetime2;"),
        new DatabaseCommand(138, 12, "ALTER TABLE TMDB_AlternateOrdering ALTER COLUMN LastUpdatedAt datetime2;"),
        new DatabaseCommand(138, 13, "ALTER TABLE TMDB_AlternateOrdering_Season ALTER COLUMN CreatedAt datetime2;"),
        new DatabaseCommand(138, 14, "ALTER TABLE TMDB_AlternateOrdering_Season ALTER COLUMN LastUpdatedAt datetime2;"),
        new DatabaseCommand(138, 15, "ALTER TABLE TMDB_AlternateOrdering_Episode ALTER COLUMN CreatedAt datetime2;"),
        new DatabaseCommand(138, 16, "ALTER TABLE TMDB_AlternateOrdering_Episode ALTER COLUMN LastUpdatedAt datetime2;"),
        new DatabaseCommand(138, 17, "ALTER TABLE TMDB_Collection ALTER COLUMN CreatedAt datetime2;"),
        new DatabaseCommand(138, 18, "ALTER TABLE TMDB_Collection ALTER COLUMN LastUpdatedAt datetime2;"),
        new DatabaseCommand(138, 19, DropDefaultOnCreatorLastUpdatedAt),
        new DatabaseCommand(138, 20, "ALTER TABLE AniDB_Creator ALTER COLUMN LastUpdatedAt datetime2;"),
        new DatabaseCommand(139, 01, "CREATE INDEX IX_CrossRef_AniDB_TMDB_Episode_AnidbAnimeID ON CrossRef_AniDB_TMDB_Episode(AnidbAnimeID);"),
        new DatabaseCommand(139, 02, "CREATE INDEX IX_CrossRef_AniDB_TMDB_Episode_AnidbAnimeID_TmdbShowID ON CrossRef_AniDB_TMDB_Episode(AnidbAnimeID, TmdbShowID);"),
        new DatabaseCommand(139, 03, "CREATE INDEX IX_CrossRef_AniDB_TMDB_Episode_AnidbEpisodeID ON CrossRef_AniDB_TMDB_Episode(AnidbEpisodeID);"),
        new DatabaseCommand(139, 04, "CREATE INDEX IX_CrossRef_AniDB_TMDB_Episode_AnidbEpisodeID_TmdbEpisodeID ON CrossRef_AniDB_TMDB_Episode(AnidbEpisodeID, TmdbEpisodeID);"),
        new DatabaseCommand(139, 05, "CREATE INDEX IX_CrossRef_AniDB_TMDB_Episode_TmdbEpisodeID ON CrossRef_AniDB_TMDB_Episode(TmdbEpisodeID);"),
        new DatabaseCommand(139, 06, "CREATE INDEX IX_CrossRef_AniDB_TMDB_Episode_TmdbShowID ON CrossRef_AniDB_TMDB_Episode(TmdbShowID);"),
        new DatabaseCommand(139, 07, "CREATE INDEX IX_CrossRef_AniDB_TMDB_Movie_AnidbAnimeID ON CrossRef_AniDB_TMDB_Movie(AnidbAnimeID);"),
        new DatabaseCommand(139, 08, "CREATE INDEX IX_CrossRef_AniDB_TMDB_Movie_AnidbEpisodeID ON CrossRef_AniDB_TMDB_Movie(AnidbEpisodeID);"),
        new DatabaseCommand(139, 09, "CREATE INDEX IX_CrossRef_AniDB_TMDB_Movie_AnidbEpisodeID_TmdbMovieID ON CrossRef_AniDB_TMDB_Movie(AnidbEpisodeID, TmdbMovieID);"),
        new DatabaseCommand(139, 10, "CREATE INDEX IX_CrossRef_AniDB_TMDB_Movie_TmdbMovieID ON CrossRef_AniDB_TMDB_Movie(TmdbMovieID);"),
        new DatabaseCommand(139, 11, "CREATE INDEX IX_CrossRef_AniDB_TMDB_Show_AnidbAnimeID ON CrossRef_AniDB_TMDB_Show(AnidbAnimeID);"),
        new DatabaseCommand(139, 12, "CREATE INDEX IX_CrossRef_AniDB_TMDB_Show_AnidbAnimeID_TmdbShowID ON CrossRef_AniDB_TMDB_Show(AnidbAnimeID, TmdbShowID);"),
        new DatabaseCommand(139, 13, "CREATE INDEX IX_CrossRef_AniDB_TMDB_Show_TmdbShowID ON CrossRef_AniDB_TMDB_Show(TmdbShowID);"),
        new DatabaseCommand(139, 14, "CREATE UNIQUE INDEX UIX_TMDB_AlternateOrdering_Season_TmdbEpisodeGroupID ON TMDB_AlternateOrdering_Season(TmdbEpisodeGroupID);"),
        new DatabaseCommand(139, 15, "CREATE INDEX IX_TMDB_AlternateOrdering_Season_TmdbEpisodeGroupCollectionID ON TMDB_AlternateOrdering_Season(TmdbEpisodeGroupCollectionID);"),
        new DatabaseCommand(139, 16, "CREATE INDEX IX_TMDB_AlternateOrdering_Season_TmdbShowID ON TMDB_AlternateOrdering_Season(TmdbShowID);"),
        new DatabaseCommand(139, 17, "CREATE UNIQUE INDEX UIX_TMDB_AlternateOrdering_TmdbEpisodeGroupCollectionID ON TMDB_AlternateOrdering(TmdbEpisodeGroupCollectionID);"),
        new DatabaseCommand(139, 18, "CREATE INDEX IX_TMDB_AlternateOrdering_TmdbEpisodeGroupCollectionID_TmdbShowID ON TMDB_AlternateOrdering(TmdbEpisodeGroupCollectionID, TmdbShowID);"),
        new DatabaseCommand(139, 19, "CREATE INDEX IX_TMDB_AlternateOrdering_TmdbShowID ON TMDB_AlternateOrdering(TmdbShowID);"),
        new DatabaseCommand(139, 20, "CREATE UNIQUE INDEX UIX_TMDB_Collection_TmdbCollectionID ON TMDB_Collection(TmdbCollectionID);"),
        new DatabaseCommand(139, 21, "CREATE INDEX IX_TMDB_Collection_Movie_TmdbCollectionID ON TMDB_Collection_Movie(TmdbCollectionID);"),
        new DatabaseCommand(139, 22, "CREATE INDEX IX_TMDB_Collection_Movie_TmdbMovieID ON TMDB_Collection_Movie(TmdbMovieID);"),
        new DatabaseCommand(139, 23, "CREATE INDEX IX_TMDB_Company_Entity_TmdbCompanyID ON TMDB_Company_Entity(TmdbCompanyID);"),
        new DatabaseCommand(139, 24, "CREATE INDEX IX_TMDB_Company_Entity_TmdbEntityType_TmdbEntityID ON TMDB_Company_Entity(TmdbEntityType, TmdbEntityID);"),
        new DatabaseCommand(139, 25, "CREATE INDEX IX_TMDB_Company_TmdbCompanyID ON TMDB_Company(TmdbCompanyID);"),
        new DatabaseCommand(139, 26, "CREATE INDEX IX_TMDB_Episode_Cast_TmdbEpisodeID ON TMDB_Episode_Cast(TmdbEpisodeID);"),
        new DatabaseCommand(139, 27, "CREATE INDEX IX_TMDB_Episode_Cast_TmdbPersonID ON TMDB_Episode_Cast(TmdbPersonID);"),
        new DatabaseCommand(139, 28, "CREATE INDEX IX_TMDB_Episode_Cast_TmdbSeasonID ON TMDB_Episode_Cast(TmdbSeasonID);"),
        new DatabaseCommand(139, 29, "CREATE INDEX IX_TMDB_Episode_Cast_TmdbShowID ON TMDB_Episode_Cast(TmdbShowID);"),
        new DatabaseCommand(139, 30, "CREATE INDEX IX_TMDB_Episode_Crew_TmdbEpisodeID ON TMDB_Episode_Crew(TmdbEpisodeID);"),
        new DatabaseCommand(139, 31, "CREATE INDEX IX_TMDB_Episode_Crew_TmdbPersonID ON TMDB_Episode_Crew(TmdbPersonID);"),
        new DatabaseCommand(139, 32, "CREATE INDEX IX_TMDB_Episode_Crew_TmdbSeasonID ON TMDB_Episode_Crew(TmdbSeasonID);"),
        new DatabaseCommand(139, 33, "CREATE INDEX IX_TMDB_Episode_Crew_TmdbShowID ON TMDB_Episode_Crew(TmdbShowID);"),
        new DatabaseCommand(139, 34, "CREATE INDEX IX_TMDB_Movie_Cast_TmdbMovieID ON TMDB_Movie_Cast(TmdbMovieID);"),
        new DatabaseCommand(139, 35, "CREATE INDEX IX_TMDB_Movie_Cast_TmdbPersonID ON TMDB_Movie_Cast(TmdbPersonID);"),
        new DatabaseCommand(139, 36, "CREATE INDEX IX_TMDB_Movie_Crew_TmdbMovieID ON TMDB_Movie_Crew(TmdbMovieID);"),
        new DatabaseCommand(139, 37, "CREATE INDEX IX_TMDB_Movie_Crew_TmdbPersonID ON TMDB_Movie_Crew(TmdbPersonID);"),
        new DatabaseCommand(139, 38, "CREATE UNIQUE INDEX UIX_TMDB_Movie_TmdbMovieID ON TMDB_Movie(TmdbMovieID);"),
        new DatabaseCommand(139, 39, "CREATE INDEX IX_TMDB_Movie_TmdbCollectionID ON TMDB_Movie(TmdbCollectionID);"),
        new DatabaseCommand(139, 40, "CREATE INDEX IX_TMDB_Person_TmdbPersonID ON TMDB_Person(TmdbPersonID);"),
        new DatabaseCommand(139, 41, "CREATE UNIQUE INDEX UIX_TMDB_Season_TmdbSeasonID ON TMDB_Season(TmdbSeasonID);"),
        new DatabaseCommand(139, 42, "CREATE INDEX IX_TMDB_Season_TmdbShowID ON TMDB_Season(TmdbShowID);"),
        new DatabaseCommand(139, 43, "CREATE UNIQUE INDEX UIX_TMDB_Network_TmdbNetworkID ON TMDB_Network(TmdbNetworkID);"),
        new DatabaseCommand(140, 01, "DROP TABLE IF EXISTS AnimeStaff;"),
        new DatabaseCommand(140, 02, "DROP TABLE IF EXISTS CrossRef_Anime_Staff;"),
        new DatabaseCommand(140, 03, "DROP TABLE IF EXISTS AniDB_Character;"),
        new DatabaseCommand(140, 04, "DROP TABLE IF EXISTS AniDB_Anime_Staff;"),
        new DatabaseCommand(140, 05, "DROP TABLE IF EXISTS AniDB_Anime_Character;"),
        new DatabaseCommand(140, 06, "DROP TABLE IF EXISTS AniDB_Character_Creator;"),
        // One character's name is 502 characters long, so 512 it is. Blame Gintama.
        new DatabaseCommand(140, 07, "CREATE TABLE AniDB_Character (AniDB_CharacterID INT IDENTITY(1,1), CharacterID INT NOT NULL, Name NVARCHAR(512) NOT NULL, OriginalName NVARCHAR(512) NOT NULL, Description TEXT NOT NULL, ImagePath NVARCHAR(20) NOT NULL, Gender INT NOT NULL);"),
        new DatabaseCommand(140, 08, "CREATE TABLE AniDB_Anime_Staff (AniDB_Anime_StaffID INT IDENTITY(1,1), AnimeID INT NOT NULL, CreatorID INT NOT NULL, Role NVARCHAR(64) NOT NULL, RoleType INT NOT NULL, Ordering INT NOT NULL);"),
        new DatabaseCommand(140, 09, "CREATE TABLE AniDB_Anime_Character (AniDB_Anime_CharacterID INT IDENTITY(1,1), AnimeID INT NOT NULL, CharacterID INT NOT NULL, Appearance NVARCHAR(20) NOT NULL, AppearanceType INT NOT NULL, Ordering INT NOT NULL);"),
        new DatabaseCommand(140, 10, "CREATE TABLE AniDB_Anime_Character_Creator (AniDB_Anime_Character_CreatorID INT IDENTITY(1,1), AnimeID INT NOT NULL, CharacterID INT NOT NULL, CreatorID INT NOT NULL, Ordering INT NOT NULL);"),
        new DatabaseCommand(140, 11, "CREATE INDEX IX_AniDB_Anime_Staff_CreatorID ON AniDB_Anime_Staff(CreatorID);"),
        new DatabaseCommand(140, 12, DatabaseFixes.NoOperation),
        new DatabaseCommand(141, 01, "ALTER TABLE AniDB_Character ADD Type int NOT NULL DEFAULT 0;"),
        new DatabaseCommand(141, 02, "ALTER TABLE AniDB_Character ADD LastUpdated datetime2 NOT NULL DEFAULT '1970-01-01 00:00:00';"),
        new DatabaseCommand(141, 03, DatabaseFixes.RecreateAnimeCharactersAndCreators),
        new DatabaseCommand(142, 01, "CREATE TABLE TMDB_Image_Entity (TMDB_Image_EntityID INT IDENTITY(1,1), TmdbEntityID INT NULL, TmdbEntityType INT NOT NULL, ImageType INT NOT NULL, RemoteFileName NVARCHAR(128) NOT NULL, Ordering INT NOT NULL, ReleasedAt DATE NULL);"),
        new DatabaseCommand(142, 02, "ALTER TABLE TMDB_Image DROP COLUMN TmdbMovieID;"),
        new DatabaseCommand(142, 03, "ALTER TABLE TMDB_Image DROP COLUMN TmdbEpisodeID;"),
        new DatabaseCommand(142, 04, "ALTER TABLE TMDB_Image DROP COLUMN TmdbSeasonID;"),
        new DatabaseCommand(142, 05, "ALTER TABLE TMDB_Image DROP COLUMN TmdbShowID;"),
        new DatabaseCommand(142, 06, "ALTER TABLE TMDB_Image DROP COLUMN TmdbCollectionID;"),
        new DatabaseCommand(142, 07, "ALTER TABLE TMDB_Image DROP COLUMN TmdbNetworkID;"),
        new DatabaseCommand(142, 08, "ALTER TABLE TMDB_Image DROP COLUMN TmdbCompanyID;"),
        new DatabaseCommand(142, 09, "ALTER TABLE TMDB_Image DROP COLUMN TmdbPersonID;"),
        new DatabaseCommand(142, 10, "ALTER TABLE TMDB_Image DROP COLUMN ForeignType;"),
        new DatabaseCommand(142, 11, "ALTER TABLE TMDB_Image DROP COLUMN ImageType;"),
        new DatabaseCommand(142, 12, DatabaseFixes.ScheduleTmdbImageUpdates),
        new DatabaseCommand(143, 01, "ALTER TABLE TMDB_Season ADD PosterPath NVARCHAR(64) NULL DEFAULT NULL;"),
        new DatabaseCommand(143, 02, "ALTER TABLE TMDB_Episode ADD ThumbnailPath NVARCHAR(64) NULL DEFAULT NULL;"),
        new DatabaseCommand(144, 01, DatabaseFixes.NoOperation),
        new DatabaseCommand(144, 02, DatabaseFixes.MoveTmdbImagesOnDisc),
        new DatabaseCommand(145, 01, "DROP TABLE IF EXISTS DuplicateFile;"),
        new DatabaseCommand(145, 02, "DROP TABLE IF EXISTS AnimeCharacter;"),
        new DatabaseCommand(146, 01, DropDefaultOnTMDBShowMovieKeywords),
        new DatabaseCommand(146, 02, "ALTER TABLE TMDB_Show ALTER COLUMN Keywords NVARCHAR(MAX) NULL;"),
        new DatabaseCommand(146, 03, "ALTER TABLE TMDB_Movie ALTER COLUMN Keywords NVARCHAR(MAX) NULL;"),
        new DatabaseCommand(147, 01, "EXEC sp_rename 'Tmdb_Show_Network', 'TMDB_Show_Network';"),
        new DatabaseCommand(147, 01, "ALTER TABLE CrossRef_AniDB_TMDB_Movie ADD COLUMN MatchRating INT NOT NULL DEFAULT 1;"),
        new DatabaseCommand(147, 02, "UPDATE CrossRef_AniDB_TMDB_Movie SET MatchRating = 5 WHERE Source = 0;"),
        new DatabaseCommand(147, 03, "ALTER TABLE CrossRef_AniDB_TMDB_Movie DROP COLUMN Source;"),
        new DatabaseCommand(147, 04, "ALTER TABLE CrossRef_AniDB_TMDB_Show ADD COLUMN MatchRating INT NOT NULL DEFAULT 1;"),
        new DatabaseCommand(147, 05, "UPDATE CrossRef_AniDB_TMDB_Show SET MatchRating = 5 WHERE Source = 0;"),
        new DatabaseCommand(147, 06, "ALTER TABLE CrossRef_AniDB_TMDB_Show DROP COLUMN Source;"),
    };

    private static void AlterImdbMovieIDType()
    {
        DropColumnWithDefaultConstraint("TMDB_Movie", "ImdbMovieID");

        using var session = Utils.ServiceContainer.GetRequiredService<DatabaseFactory>().SessionFactory.OpenStatelessSession();
        using var transaction = session.BeginTransaction();

        const string alterCommand = "ALTER TABLE TMDB_Movie ADD ImdbMovieID NVARCHAR(12) NULL DEFAULT NULL;";
        session.CreateSQLQuery(alterCommand).ExecuteUpdate();
        transaction.Commit();
    }

    private static Tuple<bool, string> MigrateRenamers(object connection)
    {
        var factory = Utils.ServiceContainer.GetRequiredService<DatabaseFactory>().Instance;
        var renamerService = Utils.ServiceContainer.GetRequiredService<RenameFileService>();
        var settingsProvider = Utils.SettingsProvider;

        var sessionFactory = factory.CreateSessionFactory();
        using var session = sessionFactory.OpenSession();
        using var transaction = session.BeginTransaction();
        try
        {
            const string createCommand = """
                                         CREATE TABLE RenamerInstance (ID INT IDENTITY(1,1) PRIMARY KEY, Name nvarchar(250) NOT NULL, Type nvarchar(250) NOT NULL, Settings varbinary(MAX));
                                         CREATE INDEX IX_RenamerInstance_Name ON RenamerInstance(Name);
                                         CREATE INDEX IX_RenamerInstance_Type ON RenamerInstance(Type);
                                         """;

            session.CreateSQLQuery(createCommand).ExecuteUpdate();

            const string selectCommand = "SELECT ScriptName, RenamerType, IsEnabledOnImport, Script FROM RenameScript;";
            var reader = session.CreateSQLQuery(selectCommand)
                .AddScalar("ScriptName", NHibernateUtil.String)
                .AddScalar("RenamerType", NHibernateUtil.String)
                .AddScalar("IsEnabledOnImport", NHibernateUtil.Int32)
                .AddScalar("Script", NHibernateUtil.String)
                .List<object[]>();
            string defaultName = null;
            var renamerInstances = reader.Select(a =>
            {
                try
                {
                    var type = ((string)a[1]).Equals("Legacy")
                        ? typeof(WebAOMRenamer)
                        : renamerService.RenamersByKey.ContainsKey((string)a[1])
                            ? renamerService.RenamersByKey[(string)a[1]].GetType()
                            : Type.GetType((string)a[1]);
                    if (type == null)
                    {
                        if ((string)a[1] == "GroupAwareRenamer")
                            return (Renamer: new RenamerConfig
                            {
                                Name = (string)a[0],
                                Type = typeof(WebAOMRenamer),
                                Settings = new WebAOMSettings
                                {
                                    Script = (string)a[3], GroupAwareSorting = true
                                }
                            }, IsDefault: (int)a[2] == 1);

                        Logger.Warn("A RenameScipt could not be converted to RenamerConfig. Renamer name: " + (string)a[0] + " Renamer type: " + (string)a[1] +
                                    " Script: " + (string)a[3]);
                        return default;
                    }

                    var settingsType = type.GetInterfaces().FirstOrDefault(b => b.IsGenericType && b.GetGenericTypeDefinition() == typeof(IRenamer<>))
                        ?.GetGenericArguments().FirstOrDefault();
                    object settings = null;
                    if (settingsType != null)
                    {
                        settings = ActivatorUtilities.CreateInstance(Utils.ServiceContainer, settingsType);
                        settingsType.GetProperties(BindingFlags.Instance | BindingFlags.Public).FirstOrDefault(b => b.Name == "Script")
                            ?.SetValue(settings, (string)a[3]);
                    }

                    return (Renamer: new RenamerConfig
                    {
                        Name = (string)a[0], Type = type, Settings = settings
                    }, IsDefault: (int)a[2] == 1);
                }
                catch (Exception ex)
                {
                    if (a is { Length: >= 4 })
                    {
                        Logger.Warn(ex, "A RenameScipt could not be converted to RenamerConfig. Renamer name: " + a[0] + " Renamer type: " + a[1] +
                                        " Script: " + a[3]);
                    }
                    else
                    {
                        Logger.Warn(ex, "A RenameScipt could not be converted to RenamerConfig, but there wasn't enough data to log");
                    }

                    return default;
                }
            }).WhereNotDefault().GroupBy(a => a.Renamer.Name).SelectMany(a => a.Select((b, i) =>
            {
                // Names are distinct
                var renamer = b.Renamer;
                if (i > 0) renamer.Name = renamer.Name + "_" + (i + 1);
                if (b.IsDefault) defaultName = renamer.Name;
                return renamer;
            }));

            if (defaultName != null)
            {
                var settings = settingsProvider.GetSettings();
                settings.Plugins.Renamer.DefaultRenamer = defaultName;
                settingsProvider.SaveSettings(settings);
            }

            const string insertCommand = "INSERT INTO RenamerInstance (Name, Type, Settings) VALUES (:Name, :Type, :Settings);";
            foreach (var renamer in renamerInstances)
            {
                var command = session.CreateSQLQuery(insertCommand);
                command.SetParameter("Name", renamer.Name);
                command.SetParameter("Type", renamer.Type.ToString());
                command.SetParameter("Settings", renamer.Settings == null ? null : MessagePackSerializer.Typeless.Serialize(renamer.Settings));
                command.ExecuteUpdate();
            }

            const string dropCommand = "DROP TABLE RenameScript;";
            session.CreateSQLQuery(dropCommand).ExecuteUpdate();
            transaction.Commit();
        }
        catch (Exception e)
        {
            transaction.Rollback();
            return new Tuple<bool, string>(false, e.ToString());
        }

        return new Tuple<bool, string>(true, null);
    }

    private static Tuple<bool, string> DropDefaultsOnAnimeEpisode_User(object connection)
    {
        DropDefaultConstraint("AnimeEpisode_User", "ContractSize");
        DropDefaultConstraint("AnimeEpisode_User", "ContractVersion");
        return Tuple.Create<bool, string>(true, null);
    }

    private static Tuple<bool, string> DropDefaultOnChaptered(object connection)
    {
        DropDefaultConstraint("AniDB_File", "IsChaptered");
        return Tuple.Create<bool, string>(true, null);
    }

    private List<DatabaseCommand> updateVersionTable = new()
    {
        new DatabaseCommand("ALTER TABLE Versions ADD VersionRevision varchar(100) NULL;"),
        new DatabaseCommand("ALTER TABLE Versions ADD VersionCommand nvarchar(max) NULL;"),
        new DatabaseCommand("ALTER TABLE Versions ADD VersionProgram varchar(100) NULL;"),
        new DatabaseCommand("DROP INDEX UIX_Versions_VersionType ON Versions;"),
        new DatabaseCommand(
            "CREATE INDEX IX_Versions_VersionType ON Versions(VersionType,VersionValue,VersionRevision);"),
    };

    private static void DropVideoLocalMediaSize()
    {
        DropColumnWithDefaultConstraint("VideoLocal", "MediaSize");
    }

    private static void DropContracts()
    {
        string[] tables = ["AniDB_Anime", "AnimeSeries", "AnimeGroup"];
        string[] columns = ["ContractSize", "ContractVersion", "ContractBlob"];
        tables.ForEach(t => columns.ForEach(a => DropColumnWithDefaultConstraint(t, a)));
    }

    private static void DropPlexContractColumns()
    {
        var tables = new[]
        {
            "AnimeEpisode", "AnimeSeries_User",
            "AnimeGroup_User"
        };
        var columns = new[]
        {
            "PlexContractSize", "PlexContractVersion"
        };
        tables.ForEach(t => columns.ForEach(a => DropColumnWithDefaultConstraint(t, a)));
    }

    private static void DropVideoLocalMediaColumns()
    {
        string[] columns =
        {
            "VideoCodec", "VideoBitrate", "VideoBitDepth", "VideoFrameRate", "VideoResolution", "AudioCodec", "AudioBitrate",
            "Duration"
        };
        columns.ForEach(a => DropColumnWithDefaultConstraint("VideoLocal", a));
    }

    private static void DropColumnWithDefaultConstraint(string table, string column)
    {
        using var session = Utils.ServiceContainer.GetRequiredService<DatabaseFactory>().SessionFactory.OpenStatelessSession();
        using var trans = session.BeginTransaction();
        var query = $@"SELECT Name FROM sys.default_constraints
                        WHERE PARENT_OBJECT_ID = OBJECT_ID('{table}')
                          AND PARENT_COLUMN_ID = (
                            SELECT column_id FROM sys.columns
                            WHERE NAME = N'{column}' AND object_id = OBJECT_ID(N'{table}')
                            )";
        var name = session.CreateSQLQuery(query).UniqueResult<string>();
        if (name != null)
        {
            query = $@"ALTER TABLE {table} DROP CONSTRAINT {name}";
            session.CreateSQLQuery(query).ExecuteUpdate();
        }

        query = $@"ALTER TABLE {table} DROP COLUMN {column}";
        session.CreateSQLQuery(query).ExecuteUpdate();
        trans.Commit();
    }

    private static void DropDefaultConstraint(string table, string column)
    {
        using var session = Utils.ServiceContainer.GetRequiredService<DatabaseFactory>().SessionFactory.OpenStatelessSession();
        using var trans = session.BeginTransaction();
        var query = $@"SELECT Name FROM sys.default_constraints
                        WHERE PARENT_OBJECT_ID = OBJECT_ID('{table}')
                          AND PARENT_COLUMN_ID = (
                            SELECT column_id FROM sys.columns
                            WHERE NAME = N'{column}' AND object_id = OBJECT_ID(N'{table}')
                            )";
        var name = session.CreateSQLQuery(query).UniqueResult<string>();
        query = $@"ALTER TABLE {table} DROP CONSTRAINT {name}";
        session.CreateSQLQuery(query).ExecuteUpdate();
        trans.Commit();
    }
    private static Tuple<bool, string> DropDefaultOnCreatorLastUpdatedAt(object connection)
    {
        DropDefaultConstraint("AniDB_Creator", "LastUpdatedAt");
        return Tuple.Create<bool, string>(true, null);
    }
    
    private static Tuple<bool, string> DropDefaultOnTMDBShowMovieKeywords(object connection)
        {
            DropDefaultConstraint("TMDB_Show", "Keywords");
            DropDefaultConstraint("TMDB_Movie", "Keywords");
            return Tuple.Create<bool, string>(true, null);
        }

    protected override Tuple<bool, string> ExecuteCommand(SqlConnection connection, string command)
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

    protected override void Execute(SqlConnection connection, string command)
    {
        using (SqlCommand cmd = new SqlCommand(command, connection))
        {
            cmd.CommandTimeout = 0;
            cmd.ExecuteNonQuery();
        }
    }

    protected override long ExecuteScalar(SqlConnection connection, string command)
    {
        using var cmd = new SqlCommand(command, connection);
        cmd.CommandTimeout = 0;
        var result = cmd.ExecuteScalar();
        return long.Parse(result.ToString());
    }

    protected override List<object[]> ExecuteReader(SqlConnection connection, string command)
    {
        using var cmd = new SqlCommand(command, connection);
        cmd.CommandTimeout = 0;
        using var reader = cmd.ExecuteReader();
        var rows = new List<object[]>();
        while (reader.Read())
        {
            var values = new object[reader.FieldCount];
            reader.GetValues(values);
            rows.Add(values);
        }
        reader.Close();
        return rows;
    }

    protected override void ConnectionWrapper(string connectionstring, Action<SqlConnection> action)
    {
        using var conn = new SqlConnection(GetConnectionString());
        conn.Open();
        action(conn);
    }

    public override void CreateAndUpdateSchema()
    {
        ConnectionWrapper(GetConnectionString(), myConn =>
        {
            var create = (ExecuteScalar(myConn, "Select count(*) from sysobjects where name = 'Versions'") == 0);
            if (create)
            {
                ServerState.Instance.ServerStartingStatus = "Database - Creating Initial Schema...";
                ExecuteWithException(myConn, createVersionTable);
            }
            var update = (ExecuteScalar(myConn,
                              "SELECT count(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE [TABLE_NAME] = 'Versions' and [COLUMN_NAME]='VersionRevision'") ==
                          0);
            if (update)
            {
                ExecuteWithException(myConn, updateVersionTable);
                AllVersions = RepoFactory.Versions.GetAllByType(Constants.DatabaseTypeKey);
            }
            PreFillVersions(createTables.Union(patchCommands));
            if (create)
                ExecuteWithException(myConn, createTables);
            ServerState.Instance.ServerStartingStatus = "Database - Applying Schema Patches...";

            ExecuteWithException(myConn, patchCommands);
        });
    }
}
