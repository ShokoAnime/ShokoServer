using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using FluentNHibernate.Cfg;
using FluentNHibernate.Cfg.Db;
using Microsoft.SqlServer.Management.Common;
using Microsoft.SqlServer.Management.Smo;
using Microsoft.Win32;
using NHibernate;
using NHibernate.AdoNet;
using NHibernate.Cfg;
using Shoko.Commons.Extensions;
using Shoko.Commons.Properties;
using Shoko.Server.Repositories;
using Shoko.Server.Server;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

// ReSharper disable InconsistentNaming

namespace Shoko.Server.Databases
{
    public class SQLServer : BaseDatabase<SqlConnection>, IDatabase
    {
        public string Name { get; } = "SQLServer";
        public int RequiredVersion { get; } = 90;

        public void BackupDatabase(string fullfilename)
        {
            fullfilename = Path.GetFileName(fullfilename) + ".bak";
            //TODO We cannot write the backup anywhere, because
            //1) The server could be elsewhere,
            //2) The SqlServer running account should have read write access to our backup dir which is nono
            // So we backup in the default SQL SERVER BACKUP DIRECTORY.

            string cmd = "BACKUP DATABASE[" + ServerSettings.Instance.Database.Schema + "] TO DISK = '" +
                         fullfilename.Replace("'", "''") + "'";


            using (SqlConnection tmpConn = new SqlConnection(GetConnectionString()))
            {
                tmpConn.Open();

                using (SqlCommand command = new SqlCommand(cmd, tmpConn))
                {
                    command.CommandTimeout = 0;
                    command.ExecuteNonQuery();
                }
            }
        }

        public override bool TestConnection()
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(GetConnectionString()))
                {
                    var query = "select 1";

                    var command = new SqlCommand(query, connection);

                    connection.Open();

                    command.ExecuteScalar();
                    return true;
                }
            }
            catch
            {
                // ignored
            }
            return false;
        }


        public override string GetConnectionString()
        {
            return
                $"Server={ServerSettings.Instance.Database.Hostname};Database={ServerSettings.Instance.Database.Schema};UID={ServerSettings.Instance.Database.Username};PWD={ServerSettings.Instance.Database.Password};";
        }

        public ISessionFactory CreateSessionFactory()
        {
            string connectionstring = $@"data source={ServerSettings.Instance.Database.Hostname};initial catalog={
                    ServerSettings.Instance.Database.Schema
                };persist security info=True;user id={
                    ServerSettings.Instance.Database.Username
                };password={ServerSettings.Instance.Database.Password}";
            // SQL Server batching on Mono is busted atm.
            // Fixed in https://github.com/mono/corefx/commit/6e65509a17da898933705899677c22eae437d68a
            // but waiting for release
            return Fluently.Configure()
                .Database(MsSqlConfiguration.MsSql2008.ConnectionString(connectionstring))
                .Mappings(m => m.FluentMappings.AddFromAssemblyOf<ShokoService>())
                .ExposeConfiguration(c => c.DataBaseIntegration(prop =>
                {
                    // SQL Server batching on Mono is busted atm.
                    // Fixed in https://github.com/mono/corefx/commit/6e65509a17da898933705899677c22eae437d68a
                    // but waiting for release. This will negatively affect performance, but there's not much choice
                    if (!Utils.IsRunningOnLinuxOrMac()) return;
                    prop.Batcher<NonBatchingBatcherFactory>();
                    prop.BatchSize = 0;
                    // uncomment this for SQL output
                    //prop.LogSqlInConsole = true;
                }))
                .BuildSessionFactory();
        }


        public bool DatabaseAlreadyExists()
        {
            long count;
            string cmd = $"Select count(*) from sysdatabases where name = '{ServerSettings.Instance.Database.Schema}'";
            using (SqlConnection tmpConn =
                new SqlConnection(
                    $"Server={ServerSettings.Instance.Database.Hostname};User ID={ServerSettings.Instance.Database.Username};Password={ServerSettings.Instance.Database.Password};database={"master"}")
            )
            {
                tmpConn.Open();
                count = ExecuteScalar(tmpConn, cmd);
            }

            // if the Versions already exists, it means we have done this already
            if (count > 0) return true;

            return false;
        }


        public void CreateDatabase()
        {
            if (DatabaseAlreadyExists()) return;

            ServerConnection conn = new ServerConnection(ServerSettings.Instance.Database.Hostname,
                ServerSettings.Instance.Database.Username, ServerSettings.Instance.Database.Password);
            Microsoft.SqlServer.Management.Smo.Server srv = new Microsoft.SqlServer.Management.Smo.Server(conn);
            Database db = new Database(srv, ServerSettings.Instance.Database.Schema);
            db.Create();
        }

        private List<DatabaseCommand> createVersionTable = new List<DatabaseCommand>
        {
            new DatabaseCommand(0, 1,
                "CREATE TABLE [Versions]( [VersionsID] [int] IDENTITY(1,1) NOT NULL, [VersionType] [varchar](100) NOT NULL, [VersionValue] [varchar](100) NOT NULL,  CONSTRAINT [PK_Versions] PRIMARY KEY CLUSTERED  ( [VersionsID] ASC )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] ) ON [PRIMARY] "),
            new DatabaseCommand(0, 2, "CREATE UNIQUE INDEX UIX_Versions_VersionType ON Versions(VersionType)"),
        };

        private List<DatabaseCommand> createTables = new List<DatabaseCommand>
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

        private List<DatabaseCommand> patchCommands = new List<DatabaseCommand>
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
            new DatabaseCommand(7, 1, DatabaseFixes.FixDuplicateTvDBLinks),
            new DatabaseCommand(7, 2, DatabaseFixes.FixDuplicateTraktLinks),
            new DatabaseCommand(7, 3,
                "CREATE UNIQUE INDEX UIX_CrossRef_AniDB_TvDB_Season ON CrossRef_AniDB_TvDB(TvDBID, TvDBSeasonNumber)"),
            new DatabaseCommand(7, 4,
                "CREATE UNIQUE INDEX UIX_CrossRef_AniDB_Trakt_Season ON CrossRef_AniDB_Trakt(TraktID, TraktSeasonNumber)"),
            new DatabaseCommand(7, 5,
                "CREATE UNIQUE INDEX UIX_CrossRef_AniDB_Trakt_Anime ON CrossRef_AniDB_Trakt(AnimeID)"),
            new DatabaseCommand(8, 1, "ALTER TABLE jmmuser ALTER COLUMN Password NVARCHAR(150) NULL"),
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
                "update CrossRef_File_Episode SET CrossRefSource=1 WHERE Hash IN (Select Hash from ANIDB_File) AND CrossRefSource=2"),
            new DatabaseCommand(26, 1,
                "CREATE TABLE LogMessage ( LogMessageID int IDENTITY(1,1) NOT NULL, LogType nvarchar(MAX) NOT NULL, LogContent nvarchar(MAX) NOT NULL, LogDate datetime NOT NULL, CONSTRAINT [PK_LogMessage] PRIMARY KEY CLUSTERED ( LogMessageID ASC )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] ) ON [PRIMARY] "),
            new DatabaseCommand(27, 1,
                "CREATE TABLE CrossRef_AniDB_TvDBV2( CrossRef_AniDB_TvDBV2ID int IDENTITY(1,1) NOT NULL, AnimeID int NOT NULL, AniDBStartEpisodeType int NOT NULL, AniDBStartEpisodeNumber int NOT NULL, TvDBID int NOT NULL, TvDBSeasonNumber int NOT NULL, TvDBStartEpisodeNumber int NOT NULL, TvDBTitle nvarchar(MAX), CrossRefSource int NOT NULL, CONSTRAINT [PK_CrossRef_AniDB_TvDBV2] PRIMARY KEY CLUSTERED ( CrossRef_AniDB_TvDBV2ID ASC )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] ) ON [PRIMARY] "),
            new DatabaseCommand(27, 2,
                "CREATE UNIQUE INDEX UIX_CrossRef_AniDB_TvDBV2 ON CrossRef_AniDB_TvDBV2(AnimeID, TvDBID, TvDBSeasonNumber, TvDBStartEpisodeNumber, AniDBStartEpisodeType, AniDBStartEpisodeNumber)"),
            new DatabaseCommand(27, 3, DatabaseFixes.MigrateTvDBLinks_V1_to_V2),
            new DatabaseCommand(28, 1, "ALTER TABLE GroupFilter ADD Locked int NULL"),
            new DatabaseCommand(29, 1, "ALTER TABLE VideoInfo ADD FullInfo varchar(max) NULL"),
            new DatabaseCommand(30, 1,
                "CREATE TABLE CrossRef_AniDB_TraktV2( CrossRef_AniDB_TraktV2ID int IDENTITY(1,1) NOT NULL, AnimeID int NOT NULL, AniDBStartEpisodeType int NOT NULL, AniDBStartEpisodeNumber int NOT NULL, TraktID nvarchar(500), TraktSeasonNumber int NOT NULL, TraktStartEpisodeNumber int NOT NULL, TraktTitle nvarchar(MAX), CrossRefSource int NOT NULL, CONSTRAINT [PK_CrossRef_AniDB_TraktV2] PRIMARY KEY CLUSTERED ( CrossRef_AniDB_TraktV2ID ASC )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] ) ON [PRIMARY] "),
            new DatabaseCommand(30, 2,
                "CREATE UNIQUE INDEX UIX_CrossRef_AniDB_TraktV2 ON CrossRef_AniDB_TraktV2(AnimeID, TraktSeasonNumber, TraktStartEpisodeNumber, AniDBStartEpisodeType, AniDBStartEpisodeNumber)"),
            new DatabaseCommand(30, 3, DatabaseFixes.MigrateTraktLinks_V1_to_V2),
            new DatabaseCommand(31, 1,
                "CREATE TABLE CrossRef_AniDB_Trakt_Episode( CrossRef_AniDB_Trakt_EpisodeID int IDENTITY(1,1) NOT NULL, AnimeID int NOT NULL, AniDBEpisodeID int NOT NULL, TraktID nvarchar(500), Season int NOT NULL, EpisodeNumber int NOT NULL, CONSTRAINT [PK_CrossRef_AniDB_Trakt_Episode] PRIMARY KEY CLUSTERED ( CrossRef_AniDB_Trakt_EpisodeID ASC )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] ) ON [PRIMARY] "),
            new DatabaseCommand(31, 2,
                "CREATE UNIQUE INDEX UIX_CrossRef_AniDB_Trakt_Episode_AniDBEpisodeID ON CrossRef_AniDB_Trakt_Episode(AniDBEpisodeID)"),
            new DatabaseCommand(32, 3, DatabaseFixes.RemoveOldMovieDBImageRecords),
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
            new DatabaseCommand(41, 4, DatabaseFixes.FixContinueWatchingGroupFilter_20160406),
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
                "DECLARE @tableName VARCHAR(MAX) = 'AnimeGroup_User'\r\nDECLARE @columnName VARCHAR(MAX) = 'KodiContractVersion'\r\nDECLARE @ConstraintName nvarchar(200)\r\nSELECT @ConstraintName = Name FROM SYS.DEFAULT_CONSTRAINTS WHERE PARENT_OBJECT_ID = OBJECT_ID(@tableName) AND PARENT_COLUMN_ID = (SELECT column_id FROM sys.columns WHERE NAME = @columnName AND object_id = OBJECT_ID(@tableName))\r\nIF @ConstraintName IS NOT NULL\r\nEXEC('ALTER TABLE ' + @tableName + ' DROP CONSTRAINT ' + @ConstraintName)"),
            new DatabaseCommand(44, 2, "ALTER TABLE AnimeGroup_User DROP COLUMN KodiContractVersion"),
            new DatabaseCommand(44, 3, "ALTER TABLE AnimeGroup_User DROP COLUMN KodiContractString"),
            new DatabaseCommand(44, 4,
                "DECLARE @tableName VARCHAR(MAX) = 'AnimeSeries_User'\r\nDECLARE @columnName VARCHAR(MAX) = 'KodiContractVersion'\r\nDECLARE @ConstraintName nvarchar(200)\r\nSELECT @ConstraintName = Name FROM SYS.DEFAULT_CONSTRAINTS WHERE PARENT_OBJECT_ID = OBJECT_ID(@tableName) AND PARENT_COLUMN_ID = (SELECT column_id FROM sys.columns WHERE NAME = @columnName AND object_id = OBJECT_ID(@tableName))\r\nIF @ConstraintName IS NOT NULL\r\nEXEC('ALTER TABLE ' + @tableName + ' DROP CONSTRAINT ' + @ConstraintName)"),
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
            new DatabaseCommand(49, 1, DatabaseFixes.DeleteSerieUsersWithoutSeries),
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
            new DatabaseCommand(54, 1, DatabaseFixes.FixTagsWithInclude),
            new DatabaseCommand(55, 1, DatabaseFixes.MakeYearsApplyToSeries),
            new DatabaseCommand(56, 1, DatabaseFixes.FixEmptyVideoInfos),
            new DatabaseCommand(57, 1, "ALTER TABLE JMMUser ADD PlexToken nvarchar(max) NULL"),
            new DatabaseCommand(58, 1, "ALTER TABLE AniDB_File ADD IsChaptered INT NOT NULL DEFAULT(-1)"),
            new DatabaseCommand(59, 1, "ALTER TABLE RenameScript ADD RenamerType nvarchar(max) NOT NULL DEFAULT('Legacy')"),
            new DatabaseCommand(59, 2, "ALTER TABLE RenameScript ADD ExtraData nvarchar(max)"),
            new DatabaseCommand(60, 1,
                "CREATE INDEX IX_AniDB_Anime_Character_CharID ON AniDB_Anime_Character(CharID);"),
            new DatabaseCommand(61, 1, "ALTER TABLE TvDB_Episode ADD Rating INT NULL"),
            new DatabaseCommand(61, 2, "ALTER TABLE TvDB_Episode ADD AirDate datetime NULL"),
            new DatabaseCommand(61, 3, "ALTER TABLE TvDB_Episode DROP COLUMN FirstAired"),
            new DatabaseCommand(61, 4, DatabaseFixes.UpdateAllTvDBSeries),
            new DatabaseCommand(62, 1, "ALTER TABLE AnimeSeries ADD AirsOn varchar(10) NULL"),
            new DatabaseCommand(63, 1, "DROP TABLE Trakt_ImageFanart"),
            new DatabaseCommand(63, 2, "DROP TABLE Trakt_ImagePoster"),
            new DatabaseCommand(64, 1, "CREATE TABLE AnimeCharacter ( CharacterID INT IDENTITY(1,1) NOT NULL, AniDBID INT NOT NULL, Name NVARCHAR(MAX) NOT NULL, AlternateName NVARCHAR(MAX), Description NVARCHAR(MAX), ImagePath NVARCHAR(MAX) )"),
            new DatabaseCommand(64, 2, "CREATE TABLE AnimeStaff ( StaffID INT IDENTITY(1,1) NOT NULL, AniDBID INT NOT NULL, Name NVARCHAR(MAX) NOT NULL, AlternateName NVARCHAR(MAX), Description NVARCHAR(MAX), ImagePath NVARCHAR(MAX) )"),
            new DatabaseCommand(64, 3, "CREATE TABLE CrossRef_Anime_Staff ( CrossRef_Anime_StaffID INT IDENTITY(1,1) NOT NULL, AniDB_AnimeID INT NOT NULL, StaffID INT NOT NULL, Role NVARCHAR(MAX), RoleID INT, RoleType INT NOT NULL, Language NVARCHAR(MAX) NOT NULL )"),
            new DatabaseCommand(64, 4, DatabaseFixes.PopulateCharactersAndStaff),
            new DatabaseCommand(65, 1, "ALTER TABLE MovieDB_Movie ADD Rating INT NOT NULL DEFAULT(0)"),
            new DatabaseCommand(65, 2, "ALTER TABLE TvDB_Series ADD Rating INT NULL"),
            new DatabaseCommand(66, 1, "ALTER TABLE AniDB_Episode ADD Description nvarchar(max) NOT NULL DEFAULT('')"),
            new DatabaseCommand(66, 2, DatabaseFixes.FixCharactersWithGrave),
            new DatabaseCommand(67, 1, DatabaseFixes.RefreshAniDBInfoFromXML),
            new DatabaseCommand(68, 1, DatabaseFixes.MakeTagsApplyToSeries),
            new DatabaseCommand(68, 2, Importer.UpdateAllStats),
            new DatabaseCommand(69, 1, DatabaseFixes.RemoveBasePathsFromStaffAndCharacters),
            new DatabaseCommand(70, 1, "ALTER TABLE AniDB_Character ALTER COLUMN CharName nvarchar(max) NOT NULL"),
            new DatabaseCommand(71, 1, "CREATE TABLE AniDB_AnimeUpdate ( AniDB_AnimeUpdateID INT IDENTITY(1,1) NOT NULL, AnimeID INT NOT NULL, UpdatedAt datetime NOT NULL )"),
            new DatabaseCommand(71, 2, "CREATE UNIQUE INDEX UIX_AniDB_AnimeUpdate ON AniDB_AnimeUpdate(AnimeID)"),
            new DatabaseCommand(71, 3, DatabaseFixes.MigrateAniDB_AnimeUpdates),
            new DatabaseCommand(72, 1, DatabaseFixes.RemoveBasePathsFromStaffAndCharacters),
            new DatabaseCommand(73, 1, DatabaseFixes.FixDuplicateTagFiltersAndUpdateSeasons),
            new DatabaseCommand(74, 1, DatabaseFixes.RecalculateYears),
            new DatabaseCommand(75, 1, "DROP INDEX UIX_CrossRef_AniDB_MAL_Anime ON CrossRef_AniDB_MAL;"),
            new DatabaseCommand(75, 2, "ALTER TABLE AniDB_Anime ADD Site_JP nvarchar(max), Site_EN nvarchar(max), Wikipedia_ID nvarchar(max), WikipediaJP_ID nvarchar(max), SyoboiID int, AnisonID int, CrunchyrollID nvarchar(max)"),
            new DatabaseCommand(75, 3, DatabaseFixes.PopulateResourceLinks),
            new DatabaseCommand(76, 1, "ALTER TABLE VideoLocal ADD MyListID INT NOT NULL DEFAULT(0)"),
            new DatabaseCommand(76, 2, DatabaseFixes.PopulateMyListIDs),
            new DatabaseCommand(77, 1, "ALTER TABLE AniDB_Episode DROP COLUMN EnglishName"),
            new DatabaseCommand(77, 2, "ALTER TABLE AniDB_Episode DROP COLUMN RomajiName"),
            new DatabaseCommand(77, 3, "CREATE TABLE AniDB_Episode_Title ( AniDB_Episode_TitleID int IDENTITY(1,1) NOT NULL, AniDB_EpisodeID int NOT NULL, Language nvarchar(50) NOT NULL, Title nvarchar(500) NOT NULL )"),
            new DatabaseCommand(77, 4, DatabaseFixes.DummyMigrationOfObsoletion),
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
            new DatabaseCommand(78, 11, DatabaseFixes.MigrateTvDBLinks_v2_to_V3),
            // DatabaseFixes.MigrateTvDBLinks_v2_to_V3() drops the CrossRef_AniDB_TvDBV2 table. We do it after init to migrate
            new DatabaseCommand(79, 1, DatabaseFixes.FixAniDB_EpisodesWithMissingTitles),
            new DatabaseCommand(80, 1, DatabaseFixes.RegenTvDBMatches),
            new DatabaseCommand(81, 1, "ALTER TABLE AnimeSeries ADD UpdatedAt datetime NOT NULL DEFAULT '2000-01-01 00:00:00';"),
            new DatabaseCommand(82, 1, DatabaseFixes.MigrateAniDBToNet),
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
            new DatabaseCommand(91, 1, "ALTER TABLE AnimeEpisode_User DROP COLUMN ContractSize;"),
            new DatabaseCommand(91, 2, "ALTER TABLE AnimeEpisode_User DROP COLUMN ContractBlob;"),
            new DatabaseCommand(91, 3, "ALTER TABLE AnimeEpisode_User DROP COLUMN ContractVersion;"),
        };

        private List<DatabaseCommand> updateVersionTable = new List<DatabaseCommand>
        {
            new DatabaseCommand("ALTER TABLE Versions ADD VersionRevision varchar(100) NULL;"),
            new DatabaseCommand("ALTER TABLE Versions ADD VersionCommand nvarchar(max) NULL;"),
            new DatabaseCommand("ALTER TABLE Versions ADD VersionProgram varchar(100) NULL;"),
            new DatabaseCommand("DROP INDEX UIX_Versions_VersionType ON Versions;"),
            new DatabaseCommand(
                "CREATE INDEX IX_Versions_VersionType ON Versions(VersionType,VersionValue,VersionRevision);"),
        };

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
            using (var session = DatabaseFactory.SessionFactory.OpenStatelessSession())
            {
                using (var trans = session.BeginTransaction())
                {
                    string query = $@"DECLARE @ConstraintName nvarchar(200)
SELECT @ConstraintName = Name FROM SYS.DEFAULT_CONSTRAINTS
WHERE PARENT_OBJECT_ID = OBJECT_ID('{table}')
AND PARENT_COLUMN_ID = (SELECT column_id FROM sys.columns
                        WHERE NAME = N'{column}'
                        AND object_id = OBJECT_ID(N'{table}'))
IF @ConstraintName IS NOT NULL
EXEC('ALTER TABLE {table} DROP CONSTRAINT ' + @ConstraintName)";
                    session.CreateSQLQuery(query).ExecuteUpdate();

                    query = $@"ALTER TABLE {table} DROP COLUMN {column}";
                    session.CreateSQLQuery(query).ExecuteUpdate();
                    trans.Commit();
                }
            }
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
            using (SqlCommand cmd = new SqlCommand(command, connection))
            {
                cmd.CommandTimeout = 0;
                object result = cmd.ExecuteScalar();
                return long.Parse(result.ToString());
            }
        }

        protected override ArrayList ExecuteReader(SqlConnection connection, string command)
        {
            using (SqlCommand cmd = new SqlCommand(command, connection))
            {
                cmd.CommandTimeout = 0;
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    ArrayList rows = new ArrayList();
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

        protected override void ConnectionWrapper(string connectionstring, Action<SqlConnection> action)
        {
            using (SqlConnection conn = new SqlConnection(GetConnectionString()))
            {
                conn.Open();
                action(conn);
            }
        }


        public void CreateAndUpdateSchema()
        {
            ConnectionWrapper(GetConnectionString(), myConn =>
            {
                bool create = (ExecuteScalar(myConn, "Select count(*) from sysobjects where name = 'Versions'") == 0);
                if (create)
                {
                    ServerState.Instance.ServerStartingStatus = Resources.Database_CreateSchema;
                    ExecuteWithException(myConn, createVersionTable);
                }
                bool update = (ExecuteScalar(myConn,
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
                ServerState.Instance.ServerStartingStatus = Resources.Database_ApplySchema;

                ExecuteWithException(myConn, patchCommands);
            });
        }

        public string GetDatabasePath(string serverName)
        {
            // normally installed versions of sql server
            var dbPath = GetDatabasePath(serverName, @"SOFTWARE\Microsoft\Microsoft SQL Server");
            if (dbPath.Length > 0) return dbPath;

            // sql server 32bit version installed on 64bit OS
            dbPath = GetDatabasePath(serverName, @"SOFTWARE\Wow6432Node\Microsoft\Microsoft SQL Server");
            return dbPath;
        }

        public string GetDatabasePath(string serverName, string registryPoint)
        {
            string instName = GetInstanceNameFromServerName(serverName).Trim().ToUpper();


            //
            using (RegistryKey sqlServerKey = Registry.LocalMachine.OpenSubKey(registryPoint))
            {
                if (sqlServerKey == null)
                    return string.Empty;
                foreach (string subKeyName in sqlServerKey.GetSubKeyNames())
                {
                    if (subKeyName.StartsWith("MSSQL"))
                    {
                        using (RegistryKey instanceKey = sqlServerKey.OpenSubKey(subKeyName))
                        {
                            if (instanceKey == null)
                                return string.Empty;
                            object val = instanceKey.GetValue("");
                            if (val != null)
                            {
                                string instanceName = val.ToString().Trim().ToUpper();

                                if (instanceName == instName) //say
                                {
                                    RegistryKey pkey = instanceKey.OpenSubKey(@"Setup");
                                    if (pkey == null)
                                        return string.Empty;
                                    string path = pkey.GetValue("SQLDataRoot").ToString();
                                    path = Path.Combine(path, "Data");
                                    return path;
                                }
                            }
                        }
                    }
                }
            }
            return string.Empty;
        }

        public string GetInstanceNameFromServerName(string servername)
        {
            if (!servername.Contains('\\')) return "MSSQLSERVER"; //default instance

            int pos = servername.IndexOf('\\');
            string instancename = servername.Substring(pos + 1, servername.Length - pos - 1);

            return instancename;
        }
    }
}
