using System;
using System.Collections;
using System.Collections.Generic;
using FluentNHibernate.Cfg;
using FluentNHibernate.Cfg.Db;
using JMMServer.Entities;
using JMMServer.Repositories;
using MySql.Data.MySqlClient;
using NHibernate;
using NLog;

namespace JMMServer.Databases
{
    public class MySQL : IDatabase
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public string Name { get; } = "MySQL";
        public int RequiredVersion { get; } = 55;

        public static MySQL Instance { get; } = new MySQL();


        public void BackupDatabase(string fullfilename)
        {
            fullfilename += ".sql";
            using (MySqlConnection conn = new MySqlConnection(GetConnectionString()))
            {
                using (MySqlCommand cmd = new MySqlCommand())
                {
                    using (MySqlBackup mb = new MySqlBackup(cmd))
                    {
                        cmd.Connection = conn;
                        conn.Open();
                        mb.ExportToFile(fullfilename);
                        conn.Close();
                    }
                }
            }
        }



        public static string GetConnectionString()
        {
            return string.Format("Server={0};Database={1};User ID={2};Password={3};Default Command Timeout=3600",
                ServerSettings.MySQL_Hostname, ServerSettings.MySQL_SchemaName, ServerSettings.MySQL_Username,
                ServerSettings.MySQL_Password);
        }


        public ISessionFactory CreateSessionFactory()
        {
            return Fluently.Configure()
                  .Database(MySQLConfiguration.Standard.ConnectionString(
                          x => x.Database(ServerSettings.MySQL_SchemaName + ";CharSet=utf8mb4")
                              .Server(ServerSettings.MySQL_Hostname)
                              .Username(ServerSettings.MySQL_Username)
                              .Password(ServerSettings.MySQL_Password)))
                  .Mappings(m => m.FluentMappings.AddFromAssemblyOf<JMMService>())
                  .BuildSessionFactory();
        }

        public bool DatabaseAlreadyExists()
        {
            try
            {
                string connStr = string.Format("Server={0};User ID={1};Password={2}", ServerSettings.MySQL_Hostname, ServerSettings.MySQL_Username, ServerSettings.MySQL_Password);

                string sql = string.Format("SELECT SCHEMA_NAME FROM INFORMATION_SCHEMA.SCHEMATA WHERE SCHEMA_NAME = '{0}'", ServerSettings.MySQL_SchemaName);
                logger.Trace(sql);
                using (MySqlConnection conn = new MySqlConnection(connStr))
                {
                    // if the Versions already exists, it means we have done this already
                    MySqlCommand cmd = new MySqlCommand(sql, conn);
                    conn.Open();
                    MySqlDataReader reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        string db = reader.GetString(0);
                        logger.Trace("Found db already exists: {0}", db);
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }

            logger.Trace("db does not exist: {0}", ServerSettings.MySQL_SchemaName);
            return false;
        }

        public void CreateDatabase()
        {
            try
            {
                if (DatabaseAlreadyExists()) return;

                string connStr = string.Format("Server={0};User ID={1};Password={2}",
                    ServerSettings.MySQL_Hostname, ServerSettings.MySQL_Username, ServerSettings.MySQL_Password);

                logger.Trace(connStr);
                string sql = string.Format("CREATE DATABASE {0} DEFAULT CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;", ServerSettings.MySQL_SchemaName);
                logger.Trace(sql);
                using (MySqlConnection conn = new MySqlConnection(connStr))
                {
                    MySqlCommand cmd = new MySqlCommand(sql, conn);
                    conn.Open();
                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }
        }

        public ArrayList GetData(string sql)
        {
            using (MySqlConnection conn = new MySqlConnection(GetConnectionString()))
            {
                ArrayList rowList = new ArrayList();
                conn.Open();
                using (MySqlCommand command = new MySqlCommand(sql, conn))
                {
                    try
                    {
                        MySqlDataReader reader = command.ExecuteReader();
                        while (reader.Read())
                        {
                            object[] values = new object[reader.FieldCount];
                            reader.GetValues(values);
                            rowList.Add(values);
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.Error(sql + " - " + ex.Message);
                    }
                }
                return rowList;
            }
        }

        public bool TestLogin()
        {
            return true;
        }

        #region Schema Updates


        public int GetDatabaseVersion()
        {
            VersionsRepository repVersions = new VersionsRepository();
            Versions ver = repVersions.GetByVersionType(Constants.DatabaseTypeKey);
            if (ver == null) return 0;

            int versionNumber = 0;
            int.TryParse(ver.VersionValue, out versionNumber);
            return versionNumber;
        }
        public void UpdateSchema()
        {
            int versionNumber = GetDatabaseVersion();
            try
            {
                UpdateSchema_002(versionNumber);
                UpdateSchema_003(versionNumber);
                UpdateSchema_004(versionNumber);
                UpdateSchema_005(versionNumber);
                UpdateSchema_006(versionNumber);
                UpdateSchema_007(versionNumber);
                UpdateSchema_008(versionNumber);
                UpdateSchema_009(versionNumber);
                UpdateSchema_010(versionNumber);
                UpdateSchema_011(versionNumber);
                UpdateSchema_012(versionNumber);
                UpdateSchema_013(versionNumber);
                UpdateSchema_014(versionNumber);
                UpdateSchema_015(versionNumber);
                UpdateSchema_016(versionNumber);
                UpdateSchema_017(versionNumber);
                UpdateSchema_018(versionNumber);
                UpdateSchema_019(versionNumber);
                UpdateSchema_020(versionNumber);
                UpdateSchema_021(versionNumber);
                UpdateSchema_022(versionNumber);
                UpdateSchema_023(versionNumber);
                UpdateSchema_024(versionNumber);
                UpdateSchema_025(versionNumber);
                UpdateSchema_026(versionNumber);
                UpdateSchema_027(versionNumber);
                UpdateSchema_028(versionNumber);
                UpdateSchema_029(versionNumber);
                UpdateSchema_030(versionNumber);
                UpdateSchema_031(versionNumber);
                UpdateSchema_032(versionNumber);
                UpdateSchema_033(versionNumber);
                UpdateSchema_034(versionNumber);
                UpdateSchema_035(versionNumber);
                UpdateSchema_036(versionNumber);
                UpdateSchema_037(versionNumber);
                UpdateSchema_038(versionNumber);
                UpdateSchema_039(versionNumber);
                UpdateSchema_040(versionNumber);
                UpdateSchema_041(versionNumber);
                UpdateSchema_042(versionNumber);
                UpdateSchema_043(versionNumber);
                UpdateSchema_044(versionNumber);
                UpdateSchema_045(versionNumber);
                UpdateSchema_046(versionNumber);
                UpdateSchema_047(versionNumber);
                UpdateSchema_048(versionNumber);
                UpdateSchema_049(versionNumber);
                UpdateSchema_050(versionNumber);
                UpdateSchema_051(versionNumber);
                UpdateSchema_052(versionNumber);
                UpdateSchema_053(versionNumber);
                UpdateSchema_054(versionNumber);
                UpdateSchema_055(versionNumber);

            }
            catch (Exception ex)
            {
                logger.ErrorException("Error updating schema: " + ex.ToString(), ex);
            }
        }

        private void UpdateSchema_002(int currentVersionNumber)
        {
            int thisVersion = 2;
            if (currentVersionNumber >= thisVersion) return;

            logger.Info("Updating schema to VERSION: {0}", thisVersion);

            List<string> cmds = new List<string>();

            cmds.Add("CREATE TABLE `IgnoreAnime` ( " +
                     " `IgnoreAnimeID` INT NOT NULL AUTO_INCREMENT , " +
                     " `JMMUserID` int NOT NULL, " +
                     " `AnimeID` int NOT NULL, " +
                     " `IgnoreType` int NOT NULL, " +
                     " PRIMARY KEY (`IgnoreAnimeID`) ) ; ");

            cmds.Add(
                "ALTER TABLE `IgnoreAnime` ADD UNIQUE INDEX `UIX_IgnoreAnime_User_AnimeID` (`JMMUserID` ASC, `AnimeID` ASC, `IgnoreType` ASC) ;");

            using (MySqlConnection conn = new MySqlConnection(GetConnectionString()))
            {
                conn.Open();

                foreach (string sql in cmds)
                {
                    using (MySqlCommand command = new MySqlCommand(sql, conn))
                    {
                        try
                        {
                            command.ExecuteNonQuery();
                        }
                        catch (Exception ex)
                        {
                            logger.Error(sql + " - " + ex.Message);
                        }
                    }
                }
            }

            UpdateDatabaseVersion(thisVersion);
        }

        private void UpdateSchema_003(int currentVersionNumber)
        {
            int thisVersion = 3;
            if (currentVersionNumber >= thisVersion) return;

            logger.Info("Updating schema to VERSION: {0}", thisVersion);

            List<string> cmds = new List<string>();

            cmds.Add("CREATE TABLE `Trakt_Friend` ( " +
                     " `Trakt_FriendID` INT NOT NULL AUTO_INCREMENT , " +
                     " `Username` varchar(100) character set utf8 NOT NULL, " +
                     " `FullName` varchar(100) character set utf8 NULL, " +
                     " `Gender` varchar(100) character set utf8 NULL, " +
                     " `Age` varchar(100) character set utf8 NULL, " +
                     " `Location` varchar(100) character set utf8 NULL, " +
                     " `About` text character set utf8 NULL, " +
                     " `Joined` int NOT NULL, " +
                     " `Avatar` text character set utf8 NULL, " +
                     " `Url` text character set utf8 NULL, " +
                     " `LastAvatarUpdate` datetime NOT NULL, " +
                     " PRIMARY KEY (`Trakt_FriendID`) ) ; ");

            cmds.Add("ALTER TABLE `Trakt_Friend` ADD UNIQUE INDEX `UIX_Trakt_Friend_Username` (`Username` ASC) ;");

            using (MySqlConnection conn = new MySqlConnection(GetConnectionString()))
            {
                conn.Open();

                foreach (string sql in cmds)
                {
                    using (MySqlCommand command = new MySqlCommand(sql, conn))
                    {
                        try
                        {
                            command.ExecuteNonQuery();
                        }
                        catch (Exception ex)
                        {
                            logger.Error(sql + " - " + ex.Message);
                        }
                    }
                }
            }

            UpdateDatabaseVersion(thisVersion);
        }

        private void UpdateSchema_004(int currentVersionNumber)
        {
            int thisVersion = 4;
            if (currentVersionNumber >= thisVersion) return;

            logger.Info("Updating schema to VERSION: {0}", thisVersion);

            List<string> cmds = new List<string>();

            cmds.Add("ALTER TABLE AnimeGroup ADD DefaultAnimeSeriesID int NULL");

            using (MySqlConnection conn = new MySqlConnection(GetConnectionString()))
            {
                conn.Open();

                foreach (string sql in cmds)
                {
                    using (MySqlCommand command = new MySqlCommand(sql, conn))
                    {
                        try
                        {
                            command.ExecuteNonQuery();
                        }
                        catch (Exception ex)
                        {
                            logger.Error(sql + " - " + ex.Message);
                        }
                    }
                }
            }

            UpdateDatabaseVersion(thisVersion);
        }

        private void UpdateSchema_005(int currentVersionNumber)
        {
            int thisVersion = 5;
            if (currentVersionNumber >= thisVersion) return;

            logger.Info("Updating schema to VERSION: {0}", thisVersion);

            List<string> cmds = new List<string>();

            cmds.Add("ALTER TABLE JMMUser ADD CanEditServerSettings int NULL");

            using (MySqlConnection conn = new MySqlConnection(GetConnectionString()))
            {
                conn.Open();

                foreach (string sql in cmds)
                {
                    using (MySqlCommand command = new MySqlCommand(sql, conn))
                    {
                        try
                        {
                            command.ExecuteNonQuery();
                        }
                        catch (Exception ex)
                        {
                            logger.Error(sql + " - " + ex.Message);
                        }
                    }
                }
            }

            UpdateDatabaseVersion(thisVersion);
        }

        private void UpdateSchema_006(int currentVersionNumber)
        {
            int thisVersion = 6;
            if (currentVersionNumber >= thisVersion) return;

            logger.Info("Updating schema to VERSION: {0}", thisVersion);

            List<string> cmds = new List<string>();

            cmds.Add("ALTER TABLE VideoInfo ADD VideoBitDepth varchar(100) NULL");

            using (MySqlConnection conn = new MySqlConnection(GetConnectionString()))
            {
                conn.Open();

                foreach (string sql in cmds)
                {
                    using (MySqlCommand command = new MySqlCommand(sql, conn))
                    {
                        try
                        {
                            command.ExecuteNonQuery();
                        }
                        catch (Exception ex)
                        {
                            logger.Error(sql + " - " + ex.Message);
                        }
                    }
                }
            }

            UpdateDatabaseVersion(thisVersion);
        }

        private void UpdateSchema_007(int currentVersionNumber)
        {
            int thisVersion = 7;
            if (currentVersionNumber >= thisVersion) return;

            logger.Info("Updating schema to VERSION: {0}", thisVersion);


            DatabaseFixes.Fixes.Add(DatabaseFixes.FixDuplicateTvDBLinks);
            DatabaseFixes.Fixes.Add(DatabaseFixes.FixDuplicateTraktLinks);

            List<string> cmds = new List<string>();

            cmds.Add(
                "ALTER TABLE `CrossRef_AniDB_TvDB` ADD UNIQUE INDEX `UIX_CrossRef_AniDB_TvDB_Season` (`TvDBID` ASC, `TvDBSeasonNumber` ASC) ;");

            cmds.Add(
                "ALTER TABLE `CrossRef_AniDB_Trakt` ADD UNIQUE INDEX `UIX_CrossRef_AniDB_Trakt_Season` (`TraktID` ASC, `TraktSeasonNumber` ASC) ;");
            cmds.Add(
                "ALTER TABLE `CrossRef_AniDB_Trakt` ADD UNIQUE INDEX `UIX_CrossRef_AniDB_Trakt_Anime` (`AnimeID` ASC) ;");

            using (MySqlConnection conn = new MySqlConnection(GetConnectionString()))
            {
                conn.Open();

                foreach (string sql in cmds)
                {
                    using (MySqlCommand command = new MySqlCommand(sql, conn))
                    {
                        try
                        {
                            command.ExecuteNonQuery();
                        }
                        catch (Exception ex)
                        {
                            logger.Error(sql + " - " + ex.Message);
                        }
                    }
                }
            }

            UpdateDatabaseVersion(thisVersion);
        }

        private void UpdateSchema_008(int currentVersionNumber)
        {
            int thisVersion = 8;
            if (currentVersionNumber >= thisVersion) return;

            logger.Info("Updating schema to VERSION: {0}", thisVersion);


            List<string> cmds = new List<string>();

            cmds.Add("ALTER TABLE JMMUser CHANGE COLUMN Password Password VARCHAR(150) NULL DEFAULT NULL ;");

            using (MySqlConnection conn = new MySqlConnection(GetConnectionString()))
            {
                conn.Open();

                foreach (string sql in cmds)
                {
                    using (MySqlCommand command = new MySqlCommand(sql, conn))
                    {
                        try
                        {
                            command.ExecuteNonQuery();
                        }
                        catch (Exception ex)
                        {
                            logger.Error(sql + " - " + ex.Message);
                        }
                    }
                }
            }

            UpdateDatabaseVersion(thisVersion);
        }

        private void UpdateSchema_009(int currentVersionNumber)
        {
            int thisVersion = 9;
            if (currentVersionNumber >= thisVersion) return;

            logger.Info("Updating schema to VERSION: {0}", thisVersion);

            List<string> cmds = new List<string>();

            cmds.Add(
                "ALTER TABLE `CommandRequest` CHANGE COLUMN `CommandID` `CommandID` text character set utf8 NOT NULL ;");
            cmds.Add(
                "ALTER TABLE `CrossRef_File_Episode` CHANGE COLUMN `FileName` `FileName` text character set utf8 NOT NULL ;");
            cmds.Add("ALTER TABLE `FileNameHash` CHANGE COLUMN `FileName` `FileName` text character set utf8 NOT NULL ;");

            using (MySqlConnection conn = new MySqlConnection(GetConnectionString()))
            {
                conn.Open();

                foreach (string sql in cmds)
                {
                    using (MySqlCommand command = new MySqlCommand(sql, conn))
                    {
                        try
                        {
                            command.ExecuteNonQuery();
                        }
                        catch (Exception ex)
                        {
                            logger.Error(sql + " - " + ex.Message);
                        }
                    }
                }
            }

            UpdateDatabaseVersion(thisVersion);
        }

        private void UpdateSchema_010(int currentVersionNumber)
        {
            int thisVersion = 10;
            if (currentVersionNumber >= thisVersion) return;

            logger.Info("Updating schema to VERSION: {0}", thisVersion);

            List<string> cmds = new List<string>();

            cmds.Add(
                "ALTER TABLE `AniDB_Category` CHANGE COLUMN `CategoryName` `CategoryName` text character set utf8 NOT NULL ;");
            cmds.Add(
                "ALTER TABLE `AniDB_Category` CHANGE COLUMN `CategoryDescription` `CategoryDescription` text character set utf8 NOT NULL ;");
            cmds.Add(
                "ALTER TABLE `AniDB_Episode` CHANGE COLUMN `RomajiName` `RomajiName` text character set utf8 NOT NULL ;");
            cmds.Add(
                "ALTER TABLE `AniDB_Episode` CHANGE COLUMN `EnglishName` `EnglishName` text character set utf8 NOT NULL ;");
            cmds.Add(
                "ALTER TABLE `AniDB_Anime_Relation` CHANGE COLUMN `RelationType` `RelationType` text character set utf8 NOT NULL ;");
            cmds.Add(
                "ALTER TABLE `AniDB_Character` CHANGE COLUMN `CharName` `CharName` text character set utf8 NOT NULL ;");
            cmds.Add(
                "ALTER TABLE `AniDB_Seiyuu` CHANGE COLUMN `SeiyuuName` `SeiyuuName` text character set utf8 NOT NULL ;");

            cmds.Add(
                "ALTER TABLE `AniDB_File` CHANGE COLUMN `File_Description` `File_Description` text character set utf8 NOT NULL ;");
            cmds.Add(
                "ALTER TABLE `AniDB_File` CHANGE COLUMN `Anime_GroupName` `Anime_GroupName` text character set utf8 NOT NULL ;");
            cmds.Add(
                "ALTER TABLE `AniDB_File` CHANGE COLUMN `Anime_GroupNameShort` `Anime_GroupNameShort` text character set utf8 NOT NULL ;");
            cmds.Add("ALTER TABLE `AniDB_File` CHANGE COLUMN `FileName` `FileName` text character set utf8 NOT NULL ;");
            cmds.Add(
                "ALTER TABLE `AniDB_GroupStatus` CHANGE COLUMN `GroupName` `GroupName` text character set utf8 NOT NULL ;");

            cmds.Add(
                "ALTER TABLE `AniDB_ReleaseGroup` CHANGE COLUMN `GroupName` `GroupName` text character set utf8 NOT NULL ;");
            cmds.Add(
                "ALTER TABLE `AniDB_ReleaseGroup` CHANGE COLUMN `GroupNameShort` `GroupNameShort` text character set utf8 NOT NULL ;");
            cmds.Add("ALTER TABLE `AniDB_ReleaseGroup` CHANGE COLUMN `URL` `URL` text character set utf8 NOT NULL ;");

            cmds.Add("ALTER TABLE `AnimeGroup` CHANGE COLUMN `GroupName` `GroupName` text character set utf8 NOT NULL ;");
            cmds.Add("ALTER TABLE `AnimeGroup` CHANGE COLUMN `SortName` `SortName` text character set utf8 NOT NULL ;");

            cmds.Add(
                "ALTER TABLE `CommandRequest` CHANGE COLUMN `CommandID` `CommandID` text character set utf8 NOT NULL ;");
            cmds.Add(
                "ALTER TABLE `CrossRef_File_Episode` CHANGE COLUMN `FileName` `FileName` text character set utf8 NOT NULL ;");
            cmds.Add("ALTER TABLE `FileNameHash` CHANGE COLUMN `FileName` `FileName` text character set utf8 NOT NULL ;");
            cmds.Add(
                "ALTER TABLE `ImportFolder` CHANGE COLUMN `ImportFolderLocation` `ImportFolderLocation` text character set utf8 NOT NULL ;");
            cmds.Add(
                "ALTER TABLE `DuplicateFile` CHANGE COLUMN `FilePathFile1` `FilePathFile1` text character set utf8 NOT NULL ;");
            cmds.Add(
                "ALTER TABLE `DuplicateFile` CHANGE COLUMN `FilePathFile2` `FilePathFile2` text character set utf8 NOT NULL ;");

            cmds.Add("ALTER TABLE `TvDB_Episode` CHANGE COLUMN `Filename` `Filename` text character set utf8 NOT NULL ;");
            cmds.Add(
                "ALTER TABLE `TvDB_Episode` CHANGE COLUMN `EpisodeName` `EpisodeName` text character set utf8 NOT NULL ;");
            cmds.Add(
                "ALTER TABLE `TvDB_Series` CHANGE COLUMN `SeriesName` `SeriesName` text character set utf8 NOT NULL ;");
            cmds.Add(
                "ALTER TABLE `DuplicateFile` CHANGE COLUMN `FilePathFile2` `FilePathFile2` text character set utf8 NOT NULL ;");
            cmds.Add(
                "ALTER TABLE `DuplicateFile` CHANGE COLUMN `FilePathFile2` `FilePathFile2` text character set utf8 NOT NULL ;");

            using (MySqlConnection conn = new MySqlConnection(GetConnectionString()))
            {
                conn.Open();

                foreach (string sql in cmds)
                {
                    using (MySqlCommand command = new MySqlCommand(sql, conn))
                    {
                        try
                        {
                            command.ExecuteNonQuery();
                        }
                        catch (Exception ex)
                        {
                            logger.Error(sql + " - " + ex.Message);
                        }
                    }
                }
            }

            UpdateDatabaseVersion(thisVersion);
        }

        private void UpdateSchema_011(int currentVersionNumber)
        {
            int thisVersion = 11;
            if (currentVersionNumber >= thisVersion) return;

            logger.Info("Updating schema to VERSION: {0}", thisVersion);

            List<string> cmds = new List<string>();

            cmds.Add("ALTER TABLE `ImportFolder` ADD `IsWatched` int NULL ;");
            cmds.Add("UPDATE ImportFolder SET IsWatched = 1 ;");
            cmds.Add("ALTER TABLE `ImportFolder` CHANGE COLUMN `IsWatched` `IsWatched` int NOT NULL ;");


            using (MySqlConnection conn = new MySqlConnection(GetConnectionString()))
            {
                conn.Open();

                foreach (string sql in cmds)
                {
                    using (MySqlCommand command = new MySqlCommand(sql, conn))
                    {
                        try
                        {
                            command.ExecuteNonQuery();
                        }
                        catch (Exception ex)
                        {
                            logger.Error(sql + " - " + ex.Message);
                        }
                    }
                }
            }

            UpdateDatabaseVersion(thisVersion);
        }

        private void UpdateSchema_012(int currentVersionNumber)
        {
            int thisVersion = 12;
            if (currentVersionNumber >= thisVersion) return;

            logger.Info("Updating schema to VERSION: {0}", thisVersion);

            List<string> cmds = new List<string>();

            cmds.Add("CREATE TABLE CrossRef_AniDB_MAL( " +
                     " CrossRef_AniDB_MALID INT NOT NULL AUTO_INCREMENT, " +
                     " AnimeID int NOT NULL, " +
                     " MALID int NOT NULL, " +
                     " MALTitle text, " +
                     " CrossRefSource int NOT NULL, " +
                     " PRIMARY KEY (`CrossRef_AniDB_MALID`) ) ; ");

            cmds.Add(
                "ALTER TABLE `CrossRef_AniDB_MAL` ADD UNIQUE INDEX `UIX_CrossRef_AniDB_MAL_AnimeID` (`AnimeID` ASC) ;");
            cmds.Add("ALTER TABLE `CrossRef_AniDB_MAL` ADD UNIQUE INDEX `UIX_CrossRef_AniDB_MAL_MALID` (`MALID` ASC) ;");


            using (MySqlConnection conn = new MySqlConnection(GetConnectionString()))
            {
                conn.Open();

                foreach (string sql in cmds)
                {
                    using (MySqlCommand command = new MySqlCommand(sql, conn))
                    {
                        try
                        {
                            command.ExecuteNonQuery();
                        }
                        catch (Exception ex)
                        {
                            logger.Error(sql + " - " + ex.Message);
                        }
                    }
                }
            }

            UpdateDatabaseVersion(thisVersion);
        }

        private void UpdateSchema_013(int currentVersionNumber)
        {
            int thisVersion = 13;
            if (currentVersionNumber >= thisVersion) return;

            logger.Info("Updating schema to VERSION: {0}", thisVersion);

            List<string> cmds = new List<string>();

            cmds.Add("drop table `CrossRef_AniDB_MAL`;");

            cmds.Add("CREATE TABLE CrossRef_AniDB_MAL( " +
                     " CrossRef_AniDB_MALID INT NOT NULL AUTO_INCREMENT, " +
                     " AnimeID int NOT NULL, " +
                     " MALID int NOT NULL, " +
                     " MALTitle text, " +
                     " StartEpisodeType int NOT NULL, " +
                     " StartEpisodeNumber int NOT NULL, " +
                     " CrossRefSource int NOT NULL, " +
                     " PRIMARY KEY (`CrossRef_AniDB_MALID`) ) ; ");

            cmds.Add(
                "ALTER TABLE `CrossRef_AniDB_MAL` ADD UNIQUE INDEX `UIX_CrossRef_AniDB_MAL_AnimeID` (`AnimeID` ASC) ;");
            cmds.Add(
                "ALTER TABLE `CrossRef_AniDB_MAL` ADD UNIQUE INDEX `UIX_CrossRef_AniDB_MAL_Anime` (`MALID` ASC, `AnimeID` ASC, `StartEpisodeType` ASC, `StartEpisodeNumber` ASC) ;");

            using (MySqlConnection conn = new MySqlConnection(GetConnectionString()))
            {
                conn.Open();

                foreach (string sql in cmds)
                {
                    using (MySqlCommand command = new MySqlCommand(sql, conn))
                    {
                        try
                        {
                            command.ExecuteNonQuery();
                        }
                        catch (Exception ex)
                        {
                            logger.Error(sql + " - " + ex.Message);
                        }
                    }
                }
            }

            UpdateDatabaseVersion(thisVersion);
        }

        private void UpdateSchema_014(int currentVersionNumber)
        {
            int thisVersion = 14;
            if (currentVersionNumber >= thisVersion) return;

            logger.Info("Updating schema to VERSION: {0}", thisVersion);

            List<string> cmds = new List<string>();

            cmds.Add("CREATE TABLE Playlist( " +
                     " PlaylistID INT NOT NULL AUTO_INCREMENT, " +
                     " PlaylistName text character set utf8, " +
                     " PlaylistItems text character set utf8, " +
                     " DefaultPlayOrder int NOT NULL, " +
                     " PlayWatched int NOT NULL, " +
                     " PlayUnwatched int NOT NULL, " +
                     " PRIMARY KEY (`PlaylistID`) ) ; ");


            using (MySqlConnection conn = new MySqlConnection(GetConnectionString()))
            {
                conn.Open();

                foreach (string sql in cmds)
                {
                    using (MySqlCommand command = new MySqlCommand(sql, conn))
                    {
                        try
                        {
                            command.ExecuteNonQuery();
                        }
                        catch (Exception ex)
                        {
                            logger.Error(sql + " - " + ex.Message);
                        }
                    }
                }
            }

            UpdateDatabaseVersion(thisVersion);
        }

        private void UpdateSchema_015(int currentVersionNumber)
        {
            int thisVersion = 15;
            if (currentVersionNumber >= thisVersion) return;

            logger.Info("Updating schema to VERSION: {0}", thisVersion);

            List<string> cmds = new List<string>();

            cmds.Add("ALTER TABLE `AnimeSeries` ADD `SeriesNameOverride` text NULL ;");

            using (MySqlConnection conn = new MySqlConnection(GetConnectionString()))
            {
                conn.Open();

                foreach (string sql in cmds)
                {
                    using (MySqlCommand command = new MySqlCommand(sql, conn))
                    {
                        try
                        {
                            command.ExecuteNonQuery();
                        }
                        catch (Exception ex)
                        {
                            logger.Error(sql + " - " + ex.Message);
                        }
                    }
                }
            }

            UpdateDatabaseVersion(thisVersion);
        }

        private void UpdateSchema_016(int currentVersionNumber)
        {
            int thisVersion = 16;
            if (currentVersionNumber >= thisVersion) return;

            logger.Info("Updating schema to VERSION: {0}", thisVersion);

            List<string> cmds = new List<string>();

            cmds.Add("CREATE TABLE BookmarkedAnime( " +
                     " BookmarkedAnimeID INT NOT NULL AUTO_INCREMENT, " +
                     " AnimeID int NOT NULL, " +
                     " Priority int NOT NULL, " +
                     " Notes text character set utf8, " +
                     " Downloading int NOT NULL, " +
                     " PRIMARY KEY (`BookmarkedAnimeID`) ) ; ");

            cmds.Add("ALTER TABLE `BookmarkedAnime` ADD UNIQUE INDEX `UIX_BookmarkedAnime_AnimeID` (`AnimeID` ASC) ;");


            using (MySqlConnection conn = new MySqlConnection(GetConnectionString()))
            {
                conn.Open();

                foreach (string sql in cmds)
                {
                    using (MySqlCommand command = new MySqlCommand(sql, conn))
                    {
                        try
                        {
                            command.ExecuteNonQuery();
                        }
                        catch (Exception ex)
                        {
                            logger.Error(sql + " - " + ex.Message);
                        }
                    }
                }
            }

            UpdateDatabaseVersion(thisVersion);
        }

        private void UpdateSchema_017(int currentVersionNumber)
        {
            int thisVersion = 17;
            if (currentVersionNumber >= thisVersion) return;

            logger.Info("Updating schema to VERSION: {0}", thisVersion);

            List<string> cmds = new List<string>();

            cmds.Add("ALTER TABLE `VideoLocal` ADD `DateTimeCreated` datetime NULL ;");
            cmds.Add("UPDATE VideoLocal SET DateTimeCreated = DateTimeUpdated ;");
            cmds.Add("ALTER TABLE `VideoLocal` CHANGE COLUMN `DateTimeCreated` `DateTimeCreated` datetime NOT NULL ;");


            using (MySqlConnection conn = new MySqlConnection(GetConnectionString()))
            {
                conn.Open();

                foreach (string sql in cmds)
                {
                    using (MySqlCommand command = new MySqlCommand(sql, conn))
                    {
                        try
                        {
                            command.ExecuteNonQuery();
                        }
                        catch (Exception ex)
                        {
                            logger.Error(sql + " - " + ex.Message);
                        }
                    }
                }
            }

            UpdateDatabaseVersion(thisVersion);
        }

        private void UpdateSchema_018(int currentVersionNumber)
        {
            int thisVersion = 18;
            if (currentVersionNumber >= thisVersion) return;

            logger.Info("Updating schema to VERSION: {0}", thisVersion);

            List<string> cmds = new List<string>();


            cmds.Add("CREATE TABLE `CrossRef_AniDB_TvDB_Episode` ( " +
                     " `CrossRef_AniDB_TvDB_EpisodeID` INT NOT NULL AUTO_INCREMENT, " +
                     " `AnimeID` int NOT NULL, " +
                     " `AniDBEpisodeID` int NOT NULL, " +
                     " `TvDBEpisodeID` int NOT NULL, " +
                     " PRIMARY KEY (`CrossRef_AniDB_TvDB_EpisodeID`) ) ; ");

            cmds.Add(
                "ALTER TABLE `CrossRef_AniDB_TvDB_Episode` ADD UNIQUE INDEX `UIX_CrossRef_AniDB_TvDB_Episode_AniDBEpisodeID` (`AniDBEpisodeID` ASC) ;");

            using (MySqlConnection conn = new MySqlConnection(GetConnectionString()))
            {
                conn.Open();

                foreach (string sql in cmds)
                {
                    using (MySqlCommand command = new MySqlCommand(sql, conn))
                    {
                        try
                        {
                            command.ExecuteNonQuery();
                        }
                        catch (Exception ex)
                        {
                            logger.Error(sql + " - " + ex.Message);
                        }
                    }
                }
            }

            UpdateDatabaseVersion(thisVersion);
        }

        private void UpdateSchema_019(int currentVersionNumber)
        {
            int thisVersion = 19;
            if (currentVersionNumber >= thisVersion) return;

            logger.Info("Updating schema to VERSION: {0}", thisVersion);

            List<string> cmds = new List<string>();


            cmds.Add("CREATE TABLE `AniDB_MylistStats` ( " +
                     " `AniDB_MylistStatsID` INT NOT NULL AUTO_INCREMENT, " +
                     " `Animes` int NOT NULL, " +
                     " `Episodes` int NOT NULL, " +
                     " `Files` int NOT NULL, " +
                     " `SizeOfFiles` bigint NOT NULL, " +
                     " `AddedAnimes` int NOT NULL, " +
                     " `AddedEpisodes` int NOT NULL, " +
                     " `AddedFiles` int NOT NULL, " +
                     " `AddedGroups` int NOT NULL, " +
                     " `LeechPct` int NOT NULL, " +
                     " `GloryPct` int NOT NULL, " +
                     " `ViewedPct` int NOT NULL, " +
                     " `MylistPct` int NOT NULL, " +
                     " `ViewedMylistPct` int NOT NULL, " +
                     " `EpisodesViewed` int NOT NULL, " +
                     " `Votes` int NOT NULL, " +
                     " `Reviews` int NOT NULL, " +
                     " `ViewiedLength` int NOT NULL, " +
                     " PRIMARY KEY (`AniDB_MylistStatsID`) ) ; ");


            using (MySqlConnection conn = new MySqlConnection(GetConnectionString()))
            {
                conn.Open();

                foreach (string sql in cmds)
                {
                    using (MySqlCommand command = new MySqlCommand(sql, conn))
                    {
                        try
                        {
                            command.ExecuteNonQuery();
                        }
                        catch (Exception ex)
                        {
                            logger.Error(sql + " - " + ex.Message);
                        }
                    }
                }
            }

            UpdateDatabaseVersion(thisVersion);
        }

        private void UpdateSchema_020(int currentVersionNumber)
        {
            int thisVersion = 20;
            if (currentVersionNumber >= thisVersion) return;

            logger.Info("Updating schema to VERSION: {0}", thisVersion);

            List<string> cmds = new List<string>();


            cmds.Add("CREATE TABLE `FileFfdshowPreset` ( " +
                     " `FileFfdshowPresetID` INT NOT NULL AUTO_INCREMENT, " +
                     " `Hash` varchar(50) NOT NULL, " +
                     " `FileSize` bigint NOT NULL, " +
                     " `Preset` text character set utf8, " +
                     " PRIMARY KEY (`FileFfdshowPresetID`) ) ; ");

            cmds.Add(
                "ALTER TABLE `FileFfdshowPreset` ADD UNIQUE INDEX `UIX_FileFfdshowPreset_Hash` (`Hash` ASC, `FileSize` ASC) ;");


            using (MySqlConnection conn = new MySqlConnection(GetConnectionString()))
            {
                conn.Open();

                foreach (string sql in cmds)
                {
                    using (MySqlCommand command = new MySqlCommand(sql, conn))
                    {
                        try
                        {
                            command.ExecuteNonQuery();
                        }
                        catch (Exception ex)
                        {
                            logger.Error(sql + " - " + ex.Message);
                        }
                    }
                }
            }

            UpdateDatabaseVersion(thisVersion);
        }

        private void UpdateSchema_021(int currentVersionNumber)
        {
            int thisVersion = 21;
            if (currentVersionNumber >= thisVersion) return;

            logger.Info("Updating schema to VERSION: {0}", thisVersion);

            List<string> cmds = new List<string>();

            cmds.Add("ALTER TABLE `AniDB_Anime` ADD `DisableExternalLinksFlag` int NULL ;");
            cmds.Add("UPDATE AniDB_Anime SET DisableExternalLinksFlag = 0 ;");
            cmds.Add(
                "ALTER TABLE `AniDB_Anime` CHANGE COLUMN `DisableExternalLinksFlag` `DisableExternalLinksFlag` int NOT NULL ;");


            using (MySqlConnection conn = new MySqlConnection(GetConnectionString()))
            {
                conn.Open();

                foreach (string sql in cmds)
                {
                    using (MySqlCommand command = new MySqlCommand(sql, conn))
                    {
                        try
                        {
                            command.ExecuteNonQuery();
                        }
                        catch (Exception ex)
                        {
                            logger.Error(sql + " - " + ex.Message);
                        }
                    }
                }
            }

            UpdateDatabaseVersion(thisVersion);
        }

        private void UpdateSchema_022(int currentVersionNumber)
        {
            int thisVersion = 22;
            if (currentVersionNumber >= thisVersion) return;

            logger.Info("Updating schema to VERSION: {0}", thisVersion);

            List<string> cmds = new List<string>();

            cmds.Add("ALTER TABLE `AniDB_File` ADD `FileVersion` int NULL ;");
            cmds.Add("UPDATE AniDB_File SET FileVersion = 1 ;");
            cmds.Add("ALTER TABLE `AniDB_File` CHANGE COLUMN `FileVersion` `FileVersion` int NOT NULL ;");


            using (MySqlConnection conn = new MySqlConnection(GetConnectionString()))
            {
                conn.Open();

                foreach (string sql in cmds)
                {
                    using (MySqlCommand command = new MySqlCommand(sql, conn))
                    {
                        try
                        {
                            command.ExecuteNonQuery();
                        }
                        catch (Exception ex)
                        {
                            logger.Error(sql + " - " + ex.Message);
                        }
                    }
                }
            }

            UpdateDatabaseVersion(thisVersion);
        }

        private void UpdateSchema_023(int currentVersionNumber)
        {
            int thisVersion = 23;
            if (currentVersionNumber >= thisVersion) return;

            logger.Info("Updating schema to VERSION: {0}", thisVersion);

            List<string> cmds = new List<string>();

            cmds.Add("CREATE TABLE RenameScript( " +
                     " RenameScriptID INT NOT NULL AUTO_INCREMENT, " +
                     " ScriptName text character set utf8, " +
                     " Script text character set utf8, " +
                     " IsEnabledOnImport int NOT NULL, " +
                     " PRIMARY KEY (`RenameScriptID`) ) ; ");


            using (MySqlConnection conn = new MySqlConnection(GetConnectionString()))
            {
                conn.Open();

                foreach (string sql in cmds)
                {
                    using (MySqlCommand command = new MySqlCommand(sql, conn))
                    {
                        try
                        {
                            command.ExecuteNonQuery();
                        }
                        catch (Exception ex)
                        {
                            logger.Error(sql + " - " + ex.Message);
                        }
                    }
                }
            }

            UpdateDatabaseVersion(thisVersion);
        }

        private void UpdateSchema_024(int currentVersionNumber)
        {
            int thisVersion = 24;
            if (currentVersionNumber >= thisVersion) return;

            logger.Info("Updating schema to VERSION: {0}", thisVersion);

            List<string> cmds = new List<string>();

            cmds.Add("ALTER TABLE `AniDB_File` ADD `IsCensored` int NULL ;");
            cmds.Add("ALTER TABLE `AniDB_File` ADD `IsDeprecated` int NULL ;");
            cmds.Add("ALTER TABLE `AniDB_File` ADD `InternalVersion` int NULL ;");

            cmds.Add("UPDATE AniDB_File SET IsCensored = 0 ;");
            cmds.Add("UPDATE AniDB_File SET IsDeprecated = 0 ;");
            cmds.Add("UPDATE AniDB_File SET InternalVersion = 1 ;");

            cmds.Add("ALTER TABLE `AniDB_File` CHANGE COLUMN `IsCensored` `IsCensored` int NOT NULL ;");
            cmds.Add("ALTER TABLE `AniDB_File` CHANGE COLUMN `IsDeprecated` `IsDeprecated` int NOT NULL ;");
            cmds.Add("ALTER TABLE `AniDB_File` CHANGE COLUMN `InternalVersion` `InternalVersion` int NOT NULL ;");


            using (MySqlConnection conn = new MySqlConnection(GetConnectionString()))
            {
                conn.Open();

                foreach (string sql in cmds)
                {
                    using (MySqlCommand command = new MySqlCommand(sql, conn))
                    {
                        try
                        {
                            command.ExecuteNonQuery();
                        }
                        catch (Exception ex)
                        {
                            logger.Error(sql + " - " + ex.Message);
                        }
                    }
                }
            }

            UpdateDatabaseVersion(thisVersion);
        }

        private void UpdateSchema_025(int currentVersionNumber)
        {
            int thisVersion = 25;
            if (currentVersionNumber >= thisVersion) return;

            logger.Info("Updating schema to VERSION: {0}", thisVersion);

            List<string> cmds = new List<string>();

            cmds.Add("ALTER TABLE `VideoLocal` ADD `IsVariation` int NULL ;");
            cmds.Add("UPDATE VideoLocal SET IsVariation = 0 ;");
            cmds.Add("ALTER TABLE `VideoLocal` CHANGE COLUMN `IsVariation` `IsVariation` int NOT NULL ;");


            using (MySqlConnection conn = new MySqlConnection(GetConnectionString()))
            {
                conn.Open();

                foreach (string sql in cmds)
                {
                    using (MySqlCommand command = new MySqlCommand(sql, conn))
                    {
                        try
                        {
                            command.ExecuteNonQuery();
                        }
                        catch (Exception ex)
                        {
                            logger.Error(sql + " - " + ex.Message);
                        }
                    }
                }
            }

            UpdateDatabaseVersion(thisVersion);
        }

        private void UpdateSchema_026(int currentVersionNumber)
        {
            int thisVersion = 26;
            if (currentVersionNumber >= thisVersion) return;

            logger.Info("Updating schema to VERSION: {0}", thisVersion);

            List<string> cmds = new List<string>();

            cmds.Add("CREATE TABLE AniDB_Recommendation( " +
                     " AniDB_RecommendationID INT NOT NULL AUTO_INCREMENT, " +
                     " AnimeID int NOT NULL, " +
                     " UserID int NOT NULL, " +
                     " RecommendationType int NOT NULL, " +
                     " RecommendationText text character set utf8, " +
                     " PRIMARY KEY (`AniDB_RecommendationID`) ) ; ");

            cmds.Add(
                "ALTER TABLE `AniDB_Recommendation` ADD UNIQUE INDEX `UIX_AniDB_Recommendation` (`AnimeID` ASC, `UserID` ASC) ;");


            using (MySqlConnection conn = new MySqlConnection(GetConnectionString()))
            {
                conn.Open();

                foreach (string sql in cmds)
                {
                    using (MySqlCommand command = new MySqlCommand(sql, conn))
                    {
                        try
                        {
                            command.ExecuteNonQuery();
                        }
                        catch (Exception ex)
                        {
                            logger.Error(sql + " - " + ex.Message);
                        }
                    }
                }
            }

            UpdateDatabaseVersion(thisVersion);
        }

        private void UpdateSchema_027(int currentVersionNumber)
        {
            int thisVersion = 27;
            if (currentVersionNumber >= thisVersion) return;

            logger.Info("Updating schema to VERSION: {0}", thisVersion);

            List<string> cmds = new List<string>();

            cmds.Add(
                "update CrossRef_File_Episode SET CrossRefSource=1 WHERE Hash IN (Select Hash from AniDB_File) AND CrossRefSource=2 ;");


            using (MySqlConnection conn = new MySqlConnection(GetConnectionString()))
            {
                conn.Open();

                foreach (string sql in cmds)
                {
                    using (MySqlCommand command = new MySqlCommand(sql, conn))
                    {
                        try
                        {
                            command.ExecuteNonQuery();
                        }
                        catch (Exception ex)
                        {
                            logger.Error(sql + " - " + ex.Message);
                        }
                    }
                }
            }

            UpdateDatabaseVersion(thisVersion);
        }

        private void UpdateSchema_028(int currentVersionNumber)
        {
            int thisVersion = 28;
            if (currentVersionNumber >= thisVersion) return;

            logger.Info("Updating schema to VERSION: {0}", thisVersion);

            List<string> cmds = new List<string>();

            cmds.Add("CREATE TABLE LogMessage( " +
                     " LogMessageID INT NOT NULL AUTO_INCREMENT, " +
                     " LogType text character set utf8, " +
                     " LogContent text character set utf8, " +
                     " LogDate datetime NOT NULL, " +
                     " PRIMARY KEY (`LogMessageID`) ) ; ");

            using (MySqlConnection conn = new MySqlConnection(GetConnectionString()))
            {
                conn.Open();

                foreach (string sql in cmds)
                {
                    using (MySqlCommand command = new MySqlCommand(sql, conn))
                    {
                        try
                        {
                            command.ExecuteNonQuery();
                        }
                        catch (Exception ex)
                        {
                            logger.Error(sql + " - " + ex.Message);
                        }
                    }
                }
            }

            UpdateDatabaseVersion(thisVersion);
        }

        private void UpdateSchema_029(int currentVersionNumber)
        {
            int thisVersion = 29;
            if (currentVersionNumber >= thisVersion) return;

            logger.Info("Updating schema to VERSION: {0}", thisVersion);

            List<string> cmds = new List<string>();

            cmds.Add("CREATE TABLE CrossRef_AniDB_TvDBV2( " +
                     " CrossRef_AniDB_TvDBV2ID INT NOT NULL AUTO_INCREMENT, " +
                     " AnimeID int NOT NULL, " +
                     " AniDBStartEpisodeType int NOT NULL, " +
                     " AniDBStartEpisodeNumber int NOT NULL, " +
                     " TvDBID int NOT NULL, " +
                     " TvDBSeasonNumber int NOT NULL, " +
                     " TvDBStartEpisodeNumber int NOT NULL, " +
                     " TvDBTitle text character set utf8, " +
                     " CrossRefSource int NOT NULL, " +
                     " PRIMARY KEY (`CrossRef_AniDB_TvDBV2ID`) ) ; ");

            cmds.Add(
                "ALTER TABLE `CrossRef_AniDB_TvDBV2` ADD UNIQUE INDEX `UIX_CrossRef_AniDB_TvDBV2` (`AnimeID` ASC, `TvDBID` ASC, `TvDBSeasonNumber` ASC, `TvDBStartEpisodeNumber` ASC, `AniDBStartEpisodeType` ASC, `AniDBStartEpisodeNumber` ASC) ;");

            using (MySqlConnection conn = new MySqlConnection(GetConnectionString()))
            {
                conn.Open();

                foreach (string sql in cmds)
                {
                    using (MySqlCommand command = new MySqlCommand(sql, conn))
                    {
                        try
                        {
                            command.ExecuteNonQuery();
                        }
                        catch (Exception ex)
                        {
                            logger.Error(sql + " - " + ex.Message);
                        }
                    }
                }
            }

            UpdateDatabaseVersion(thisVersion);

            // Now do the migratiuon
            DatabaseFixes.Fixes.Add(DatabaseFixes.MigrateTvDBLinks_V1_to_V2);
        }

        private void UpdateSchema_030(int currentVersionNumber)
        {
            int thisVersion = 30;
            if (currentVersionNumber >= thisVersion) return;

            logger.Info("Updating schema to VERSION: {0}", thisVersion);

            List<string> cmds = new List<string>();

            cmds.Add("ALTER TABLE `GroupFilter` ADD `Locked` int NULL ;");

            ExecuteSQLCommands(cmds);

            UpdateDatabaseVersion(thisVersion);
        }

        private void UpdateSchema_031(int currentVersionNumber)
        {
            int thisVersion = 31;
            if (currentVersionNumber >= thisVersion) return;

            logger.Info("Updating schema to VERSION: {0}", thisVersion);

            List<string> cmds = new List<string>();

            cmds.Add("ALTER TABLE VideoInfo ADD FullInfo varchar(10000) NULL");

            ExecuteSQLCommands(cmds);

            UpdateDatabaseVersion(thisVersion);
        }

        private void UpdateSchema_032(int currentVersionNumber)
        {
            int thisVersion = 32;
            if (currentVersionNumber >= thisVersion) return;

            logger.Info("Updating schema to VERSION: {0}", thisVersion);

            List<string> cmds = new List<string>();

            cmds.Add("CREATE TABLE CrossRef_AniDB_TraktV2( " +
                     " CrossRef_AniDB_TraktV2ID INT NOT NULL AUTO_INCREMENT, " +
                     " AnimeID int NOT NULL, " +
                     " AniDBStartEpisodeType int NOT NULL, " +
                     " AniDBStartEpisodeNumber int NOT NULL, " +
                     " TraktID varchar(100) character set utf8, " +
                     " TraktSeasonNumber int NOT NULL, " +
                     " TraktStartEpisodeNumber int NOT NULL, " +
                     " TraktTitle text character set utf8, " +
                     " CrossRefSource int NOT NULL, " +
                     " PRIMARY KEY (`CrossRef_AniDB_TraktV2ID`) ) ; ");

            cmds.Add(
                "ALTER TABLE `CrossRef_AniDB_TraktV2` ADD UNIQUE INDEX `UIX_CrossRef_AniDB_TraktV2` (`AnimeID` ASC, `TraktSeasonNumber` ASC, `TraktStartEpisodeNumber` ASC, `AniDBStartEpisodeType` ASC, `AniDBStartEpisodeNumber` ASC) ;");

            using (MySqlConnection conn = new MySqlConnection(GetConnectionString()))
            {
                conn.Open();

                foreach (string sql in cmds)
                {
                    using (MySqlCommand command = new MySqlCommand(sql, conn))
                    {
                        try
                        {
                            command.ExecuteNonQuery();
                        }
                        catch (Exception ex)
                        {
                            logger.Error(sql + " - " + ex.Message);
                        }
                    }
                }
            }

            UpdateDatabaseVersion(thisVersion);

            // Now do the migratiuon
            DatabaseFixes.Fixes.Add(DatabaseFixes.MigrateTraktLinks_V1_to_V2);
        }

        private void UpdateSchema_033(int currentVersionNumber)
        {
            int thisVersion = 33;
            if (currentVersionNumber >= thisVersion) return;

            logger.Info("Updating schema to VERSION: {0}", thisVersion);

            List<string> cmds = new List<string>();


            cmds.Add("CREATE TABLE `CrossRef_AniDB_Trakt_Episode` ( " +
                     " `CrossRef_AniDB_Trakt_EpisodeID` INT NOT NULL AUTO_INCREMENT, " +
                     " `AnimeID` int NOT NULL, " +
                     " `AniDBEpisodeID` int NOT NULL, " +
                     " `TraktID` varchar(100) character set utf8, " +
                     " `Season` int NOT NULL, " +
                     " `EpisodeNumber` int NOT NULL, " +
                     " PRIMARY KEY (`CrossRef_AniDB_Trakt_EpisodeID`) ) ; ");

            cmds.Add(
                "ALTER TABLE `CrossRef_AniDB_Trakt_Episode` ADD UNIQUE INDEX `UIX_CrossRef_AniDB_Trakt_Episode_AniDBEpisodeID` (`AniDBEpisodeID` ASC) ;");

            using (MySqlConnection conn = new MySqlConnection(GetConnectionString()))
            {
                conn.Open();

                foreach (string sql in cmds)
                {
                    using (MySqlCommand command = new MySqlCommand(sql, conn))
                    {
                        try
                        {
                            command.ExecuteNonQuery();
                        }
                        catch (Exception ex)
                        {
                            logger.Error(sql + " - " + ex.Message);
                        }
                    }
                }
            }

            UpdateDatabaseVersion(thisVersion);
        }

        private void UpdateSchema_034(int currentVersionNumber)
        {
            int thisVersion = 34;
            if (currentVersionNumber >= thisVersion) return;

            logger.Info("Updating schema to VERSION: {0}", thisVersion);

            UpdateDatabaseVersion(thisVersion);

            // Now do the migration
            DatabaseFixes.Fixes.Add(DatabaseFixes.RemoveOldMovieDBImageRecords);
        }

        private void UpdateSchema_035(int currentVersionNumber)
        {
            int thisVersion = 35;
            if (currentVersionNumber >= thisVersion) return;

            logger.Info("Updating schema to VERSION: {0}", thisVersion);

            List<string> cmds = new List<string>();

            cmds.Add("CREATE TABLE `CustomTag` ( " +
                     " `CustomTagID` INT NOT NULL AUTO_INCREMENT, " +
                     " `TagName` text character set utf8, " +
                     " `TagDescription` text character set utf8, " +
                     " PRIMARY KEY (`CustomTagID`) ) ; ");

            cmds.Add("CREATE TABLE `CrossRef_CustomTag` ( " +
                     " `CrossRef_CustomTagID` INT NOT NULL AUTO_INCREMENT, " +
                     " `CustomTagID` int NOT NULL, " +
                     " `CrossRefID` int NOT NULL, " +
                     " `CrossRefType` int NOT NULL, " +
                     " PRIMARY KEY (`CrossRef_CustomTagID`) ) ; ");


            using (MySqlConnection conn = new MySqlConnection(GetConnectionString()))
            {
                conn.Open();

                foreach (string sql in cmds)
                {
                    using (MySqlCommand command = new MySqlCommand(sql, conn))
                    {
                        try
                        {
                            command.ExecuteNonQuery();
                        }
                        catch (Exception ex)
                        {
                            logger.Error(sql + " - " + ex.Message);
                        }
                    }
                }
            }

            UpdateDatabaseVersion(thisVersion);
            this.CreateInitialCustomTags();
        }

        private void UpdateSchema_036(int currentVersionNumber)
        {
            int thisVersion = 36;
            if (currentVersionNumber >= thisVersion) return;

            logger.Info("Updating schema to VERSION: {0}", thisVersion);

            List<string> cmds = new List<string>();

            cmds.Add(string.Format("ALTER DATABASE {0} CHARACTER SET = utf8mb4 COLLATE = utf8mb4_unicode_ci;",
                ServerSettings.MySQL_SchemaName));

            using (MySqlConnection conn = new MySqlConnection(GetConnectionString()))
            {
                conn.Open();

                foreach (string sql in cmds)
                {
                    using (MySqlCommand command = new MySqlCommand(sql, conn))
                    {
                        try
                        {
                            command.ExecuteNonQuery();
                        }
                        catch (Exception ex)
                        {
                            logger.Error(sql + " - " + ex.Message);
                        }
                    }
                }
            }

            UpdateDatabaseVersion(thisVersion);

        }

        private void UpdateSchema_037(int currentVersionNumber)
        {
            int thisVersion = 37;
            if (currentVersionNumber >= thisVersion) return;

            logger.Info("Updating schema to VERSION: {0}", thisVersion);

            List<string> cmds = new List<string>();

            cmds.Add("ALTER TABLE `CrossRef_AniDB_MAL` DROP INDEX `UIX_CrossRef_AniDB_MAL_AnimeID` ;");
            cmds.Add("ALTER TABLE `CrossRef_AniDB_MAL` DROP INDEX `UIX_CrossRef_AniDB_MAL_Anime` ;");

            cmds.Add("ALTER TABLE `CrossRef_AniDB_MAL` ADD UNIQUE INDEX `UIX_CrossRef_AniDB_MAL_MALID` (`MALID` ASC) ;");
            cmds.Add(
                "ALTER TABLE `CrossRef_AniDB_MAL` ADD UNIQUE INDEX `UIX_CrossRef_AniDB_MAL_Anime` (`AnimeID` ASC, `StartEpisodeType` ASC, `StartEpisodeNumber` ASC) ;");


            using (MySqlConnection conn = new MySqlConnection(GetConnectionString()))
            {
                conn.Open();

                foreach (string sql in cmds)
                {
                    using (MySqlCommand command = new MySqlCommand(sql, conn))
                    {
                        try
                        {
                            command.ExecuteNonQuery();
                        }
                        catch (Exception ex)
                        {
                            logger.Error(sql + " - " + ex.Message);
                        }
                    }
                }
            }

            UpdateDatabaseVersion(thisVersion);

        }

        private void UpdateSchema_038(int currentVersionNumber)
        {
            int thisVersion = 38;
            if (currentVersionNumber >= thisVersion) return;

            logger.Info("Updating schema to VERSION: {0}", thisVersion);

            List<string> cmds = new List<string>();

            cmds.Add("ALTER TABLE AniDB_Anime_Tag ADD Weight int NULL");

            ExecuteSQLCommands(cmds);

            UpdateDatabaseVersion(thisVersion);
        }

        private void UpdateSchema_039(int currentVersionNumber)
        {
            int thisVersion = 39;
            if (currentVersionNumber >= thisVersion) return;

            logger.Info("Updating schema to VERSION: {0}", thisVersion);

            UpdateDatabaseVersion(thisVersion);

            DatabaseFixes.Fixes.Add(DatabaseFixes.PopulateTagWeight);
        }

        private void UpdateSchema_040(int currentVersionNumber)
        {
            int thisVersion = 40;
            if (currentVersionNumber >= thisVersion) return;

            logger.Info("Updating schema to VERSION: {0}", thisVersion);

            List<string> cmds = new List<string>();

            cmds.Add("ALTER TABLE Trakt_Episode ADD TraktID int NULL");

            ExecuteSQLCommands(cmds);

            UpdateDatabaseVersion(thisVersion);
        }

        private void UpdateSchema_041(int currentVersionNumber)
        {
            int thisVersion = 41;
            if (currentVersionNumber >= thisVersion) return;

            logger.Info("Updating schema to VERSION: {0}", thisVersion);

            // Now do the migration
            DatabaseFixes.Fixes.Add(DatabaseFixes.FixHashes);

            UpdateDatabaseVersion(thisVersion);
        }

        private void UpdateSchema_042(int currentVersionNumber)
        {
            int thisVersion = 42;
            if (currentVersionNumber >= thisVersion) return;

            logger.Info("Updating schema to VERSION: {0}", thisVersion);

            List<string> cmds = new List<string>();
            cmds.Add("drop table `LogMessage`;");

            ExecuteSQLCommands(cmds);

            UpdateDatabaseVersion(thisVersion);
        }

        private void UpdateSchema_043(int currentVersionNumber)
        {
            int thisVersion = 43;
            if (currentVersionNumber >= thisVersion) return;

            logger.Info("Updating schema to VERSION: {0}", thisVersion);

            List<string> cmds = new List<string>();

            cmds.Add("ALTER TABLE AnimeSeries ADD DefaultFolder text character set utf8");

            ExecuteSQLCommands(cmds);

            UpdateDatabaseVersion(thisVersion);
        }

        private void UpdateSchema_044(int currentVersionNumber)
        {
            int thisVersion = 44;
            if (currentVersionNumber >= thisVersion) return;

            logger.Info("Updating schema to VERSION: {0}", thisVersion);

            List<string> cmds = new List<string>();

            cmds.Add("ALTER TABLE JMMUser ADD PlexUsers text character set utf8");

            ExecuteSQLCommands(cmds);

            UpdateDatabaseVersion(thisVersion);
        }

        private void UpdateSchema_045(int currentVersionNumber)
        {
            int thisVersion = 45;
            if (currentVersionNumber >= thisVersion) return;

            logger.Info("Updating schema to VERSION: {0}", thisVersion);

            List<string> cmds = new List<string>();

            cmds.Add("ALTER TABLE `GroupFilter` ADD `FilterType` int NULL ;");
            cmds.Add("UPDATE GroupFilter SET FilterType = 1 ;");
            cmds.Add("ALTER TABLE `GroupFilter` CHANGE COLUMN `FilterType` `FilterType` int NOT NULL ;");

            using (MySqlConnection conn = new MySqlConnection(GetConnectionString()))
            {
                conn.Open();

                foreach (string sql in cmds)
                {
                    using (MySqlCommand command = new MySqlCommand(sql, conn))
                    {
                        try
                        {
                            command.ExecuteNonQuery();
                        }
                        catch (Exception ex)
                        {
                            logger.Error(sql + " - " + ex.Message);
                        }
                    }
                }
            }

            UpdateDatabaseVersion(thisVersion);

            DatabaseFixes.Fixes.Add(DatabaseFixes.FixContinueWatchingGroupFilter_20160406);
        }

        private void UpdateSchema_046(int currentVersionNumber)
        {
            int thisVersion = 46;
            if (currentVersionNumber >= thisVersion) return;

            logger.Info("Updating schema to VERSION: {0}", thisVersion);

            List<string> cmds = new List<string>();

            cmds.Add("ALTER TABLE `AniDB_Anime` ADD `ContractVersion` int NOT NULL DEFAULT 0");
            cmds.Add("ALTER TABLE `AniDB_Anime` ADD `ContractString` mediumtext character set utf8 NULL");
            cmds.Add("ALTER TABLE `AnimeGroup` ADD `ContractVersion` int NOT NULL DEFAULT 0");
            cmds.Add("ALTER TABLE `AnimeGroup` ADD `ContractString` mediumtext character set utf8 NULL");
            cmds.Add("ALTER TABLE `AnimeGroup_User` ADD `PlexContractVersion` int NOT NULL DEFAULT 0");
            cmds.Add("ALTER TABLE `AnimeGroup_User` ADD `PlexContractString` mediumtext character set utf8 NULL");
            cmds.Add("ALTER TABLE `AnimeGroup_User` ADD `KodiContractVersion` int NOT NULL DEFAULT 0");
            cmds.Add("ALTER TABLE `AnimeGroup_User` ADD `KodiContractString` mediumtext character set utf8 NULL");
            cmds.Add("ALTER TABLE `AnimeSeries` ADD `ContractVersion` int NOT NULL DEFAULT 0");
            cmds.Add("ALTER TABLE `AnimeSeries` ADD `ContractString` mediumtext character set utf8 NULL");
            cmds.Add("ALTER TABLE `AnimeSeries_User` ADD `PlexContractVersion` int NOT NULL DEFAULT 0");
            cmds.Add("ALTER TABLE `AnimeSeries_User` ADD `PlexContractString` mediumtext character set utf8 NULL");
            cmds.Add("ALTER TABLE `AnimeSeries_User` ADD `KodiContractVersion` int NOT NULL DEFAULT 0");
            cmds.Add("ALTER TABLE `AnimeSeries_User` ADD `KodiContractString` mediumtext character set utf8 NULL");
            cmds.Add("ALTER TABLE `GroupFilter` ADD `GroupsIdsVersion` int NOT NULL DEFAULT 0");
            cmds.Add("ALTER TABLE `GroupFilter` ADD `GroupsIdsString` mediumtext character set utf8 NULL");
            cmds.Add("ALTER TABLE `AnimeEpisode_User` ADD `ContractVersion` int NOT NULL DEFAULT 0");
            cmds.Add("ALTER TABLE `AnimeEpisode_User` ADD `ContractString` mediumtext character set utf8 NULL");


            try
            {
                using (MySqlConnection conn = new MySqlConnection(GetConnectionString()))
                {
                    conn.Open();

                    foreach (string sql in cmds)
                    {
                        using (MySqlCommand command = new MySqlCommand(sql, conn))
                        {
                            command.ExecuteNonQuery();
                        }
                    }
                }
                UpdateDatabaseVersion(thisVersion);
            }
            catch (Exception ex)
            {
                logger.Error(ex.Message);
            }
        }

        private void UpdateSchema_047(int currentVersionNumber)
        {
            int thisVersion = 47;
            if (currentVersionNumber >= thisVersion) return;

            logger.Info("Updating schema to VERSION: {0}", thisVersion);

            List<string> cmds = new List<string>();

            cmds.Add("ALTER TABLE `AnimeEpisode` ADD `PlexContractVersion` int NOT NULL DEFAULT 0");
            cmds.Add("ALTER TABLE `AnimeEpisode` ADD `PlexContractString` mediumtext character set utf8 NULL");
            cmds.Add("ALTER TABLE `VideoLocal` ADD `MediaVersion` int NOT NULL DEFAULT 0");
            cmds.Add("ALTER TABLE `VideoLocal` ADD `MediaString` mediumtext character set utf8 NULL");


            try
            {
                using (MySqlConnection conn = new MySqlConnection(GetConnectionString()))
                {
                    conn.Open();

                    foreach (string sql in cmds)
                    {
                        using (MySqlCommand command = new MySqlCommand(sql, conn))
                        {
                            command.ExecuteNonQuery();
                        }
                    }
                }
                UpdateDatabaseVersion(thisVersion);
            }
            catch (Exception ex)
            {
                logger.Error(ex.Message);
            }
        }

        private void UpdateSchema_048(int currentVersionNumber)
        {
            int thisVersion = 48;
            if (currentVersionNumber >= thisVersion) return;

            logger.Info("Updating schema to VERSION: {0}", thisVersion);

            List<string> cmds = new List<string>();

            cmds.Add("ALTER TABLE `AnimeSeries_User` DROP COLUMN `KodiContractVersion`");
            cmds.Add("ALTER TABLE `AnimeSeries_User` DROP COLUMN `KodiContractString`");
            cmds.Add("ALTER TABLE `AnimeGroup_User` DROP COLUMN `KodiContractVersion`");
            cmds.Add("ALTER TABLE `AnimeGroup_User` DROP COLUMN `KodiContractString`");

            try
            {
                using (MySqlConnection conn = new MySqlConnection(GetConnectionString()))
                {
                    conn.Open();

                    foreach (string sql in cmds)
                    {
                        using (MySqlCommand command = new MySqlCommand(sql, conn))
                        {
                            command.ExecuteNonQuery();
                        }
                    }
                }
                UpdateDatabaseVersion(thisVersion);
            }
            catch (Exception ex)
            {
                logger.Error(ex.Message);
            }
        }

        private void UpdateSchema_049(int currentVersionNumber)
        {
            int thisVersion = 49;
            if (currentVersionNumber >= thisVersion) return;

            logger.Info("Updating schema to VERSION: {0}", thisVersion);

            List<string> cmds = new List<string>();

            cmds.Add("ALTER TABLE AnimeSeries ADD LatestEpisodeAirDate datetime NULL");
            cmds.Add("ALTER TABLE AnimeGroup ADD LatestEpisodeAirDate datetime NULL");

            ExecuteSQLCommands(cmds);

            UpdateDatabaseVersion(thisVersion);
        }

        private void UpdateSchema_050(int currentVersionNumber)
        {
            int thisVersion = 50;
            if (currentVersionNumber >= thisVersion) return;

            logger.Info("Updating schema to VERSION: {0}", thisVersion);

            List<string> cmds = new List<string>();
            cmds.Add("ALTER TABLE `GroupFilter` ADD `GroupConditionsVersion` int NOT NULL DEFAULT 0");
            cmds.Add("ALTER TABLE `GroupFilter` ADD `GroupConditions` mediumtext character set utf8 NULL");
            cmds.Add("ALTER TABLE `GroupFilter` ADD `ParentGroupFilterID` int NULL");
            cmds.Add("ALTER TABLE `GroupFilter` ADD `InvisibleInClients` int NOT NULL DEFAULT 0");
            cmds.Add("ALTER TABLE `GroupFilter` ADD `SeriesIdsVersion` int NOT NULL DEFAULT 0");
            cmds.Add("ALTER TABLE `GroupFilter` ADD `SeriesIdsString` mediumtext character set utf8 NULL");

            try
            {
                using (MySqlConnection conn = new MySqlConnection(GetConnectionString()))
                {
                    conn.Open();

                    foreach (string sql in cmds)
                    {
                        using (MySqlCommand command = new MySqlCommand(sql, conn))
                        {
                            command.ExecuteNonQuery();
                        }
                    }
                }
                UpdateDatabaseVersion(thisVersion);
            }
            catch (Exception ex)
            {
                logger.Error(ex.Message);
            }
        }

        private static void UpdateSchema_051(int currentVersionNumber)
        {
            int thisVersion = 51;
            if (currentVersionNumber >= thisVersion) return;

            logger.Info("Updating schema to VERSION: {0}", thisVersion);

            List<string> cmds = new List<string>();
            cmds.Add("ALTER TABLE `AniDB_Anime` ADD `ContractBlob` mediumblob NULL");
            cmds.Add("ALTER TABLE `AniDB_Anime` ADD `ContractSize` int NOT NULL DEFAULT 0");
            cmds.Add("ALTER TABLE `AniDB_Anime` DROP COLUMN `ContractString`");
            cmds.Add("ALTER TABLE `VideoLocal` ADD `MediaBlob` mediumblob NULL");
            cmds.Add("ALTER TABLE `VideoLocal` ADD `MediaSize` int NOT NULL DEFAULT 0");
            cmds.Add("ALTER TABLE `VideoLocal` DROP COLUMN `MediaString`");
            cmds.Add("ALTER TABLE `AnimeEpisode` ADD `PlexContractBlob` mediumblob NULL");
            cmds.Add("ALTER TABLE `AnimeEpisode` ADD `PlexContractSize` int NOT NULL DEFAULT 0");
            cmds.Add("ALTER TABLE `AnimeEpisode` DROP COLUMN `PlexContractString`");
            cmds.Add("ALTER TABLE `AnimeEpisode_User` ADD `ContractBlob` mediumblob NULL");
            cmds.Add("ALTER TABLE `AnimeEpisode_User` ADD `ContractSize` int NOT NULL DEFAULT 0");
            cmds.Add("ALTER TABLE `AnimeEpisode_User` DROP COLUMN `ContractString`");
            cmds.Add("ALTER TABLE `AnimeSeries` ADD `ContractBlob` mediumblob NULL");
            cmds.Add("ALTER TABLE `AnimeSeries` ADD `ContractSize` int NOT NULL DEFAULT 0");
            cmds.Add("ALTER TABLE `AnimeSeries` DROP COLUMN `ContractString`");
            cmds.Add("ALTER TABLE `AnimeSeries_User` ADD `PlexContractBlob` mediumblob NULL");
            cmds.Add("ALTER TABLE `AnimeSeries_User` ADD `PlexContractSize` int NOT NULL DEFAULT 0");
            cmds.Add("ALTER TABLE `AnimeSeries_User` DROP COLUMN `PlexContractString`");
            cmds.Add("ALTER TABLE `AnimeGroup_User` ADD `PlexContractBlob` mediumblob NULL");
            cmds.Add("ALTER TABLE `AnimeGroup_User` ADD `PlexContractSize` int NOT NULL DEFAULT 0");
            cmds.Add("ALTER TABLE `AnimeGroup_User` DROP COLUMN `PlexContractString`");
            cmds.Add("ALTER TABLE `AnimeGroup` ADD `ContractBlob` mediumblob NULL");
            cmds.Add("ALTER TABLE `AnimeGroup` ADD `ContractSize` int NOT NULL DEFAULT 0");
            cmds.Add("ALTER TABLE `AnimeGroup` DROP COLUMN `ContractString`");

            try
            {
                using (MySqlConnection conn = new MySqlConnection(GetConnectionString()))
                {
                    conn.Open();

                    foreach (string sql in cmds)
                    {
                        using (MySqlCommand command = new MySqlCommand(sql, conn))
                        {
                            command.ExecuteNonQuery();
                        }
                    }
                }
                UpdateDatabaseVersion(thisVersion);
            }
            catch (Exception ex)
            {
                logger.Error(ex.Message);
            }
        }

        private static void UpdateSchema_052(int currentVersionNumber)
        {
            int thisVersion = 52;
            if (currentVersionNumber >= thisVersion) return;

            logger.Info("Updating schema to VERSION: {0}", thisVersion);

            List<string> cmds = new List<string>();
            cmds.Add("ALTER TABLE `AniDB_Anime` DROP COLUMN `AllCategories`");

            try
            {
                using (MySqlConnection conn = new MySqlConnection(GetConnectionString()))
                {
                    conn.Open();

                    foreach (string sql in cmds)
                    {
                        using (MySqlCommand command = new MySqlCommand(sql, conn))
                        {
                            command.ExecuteNonQuery();
                        }
                    }
                }
                UpdateDatabaseVersion(thisVersion);
            }
            catch (Exception ex)
            {
                logger.Error(ex.Message);
            }
        }

        private static void UpdateSchema_053(int currentVersionNumber)
        {
            int thisVersion = 53;
            if (currentVersionNumber >= thisVersion) return;

            logger.Info("Updating schema to VERSION: {0}", thisVersion);

            try
            {
                DatabaseFixes.Fixes.Add(DatabaseFixes.DeleteSerieUsersWithoutSeries);
                UpdateDatabaseVersion(thisVersion);
            }
            catch (Exception ex)
            {
                logger.Error(ex.Message);
            }
        }
        private void UpdateSchema_054(int currentVersionNumber)
        {
            int thisVersion = 54;
            if (currentVersionNumber >= thisVersion) return;

            logger.Info("Updating schema to VERSION: {0}", thisVersion);

            List<string> cmds = new List<string>();
            cmds.Add("CREATE TABLE `VideoLocal_Place` ( " +
         " `VideoLocal_Place_ID` INT NOT NULL AUTO_INCREMENT, " +
         " `VideoLocalID` int NOT NULL, " +
         " `FilePath` text character set utf8 NOT NULL, " +
         " `ImportFolderID` int NOT NULL, " +
         " `ImportFolderType` int NOT NULL, "+
         " PRIMARY KEY (`VideoLocal_Place_ID`) ) ; ");
            cmds.Add("ALTER TABLE `VideoLocal` ADD `FileName` text character set utf8 NOT NULL");
            cmds.Add("ALTER TABLE `VideoLocal` ADD `VideoCodec` varchar(100) NOT NULL DEFAULT ''");
            cmds.Add("ALTER TABLE `VideoLocal` ADD `VideoBitrate` varchar(100) NOT NULL DEFAULT ''");
            cmds.Add("ALTER TABLE `VideoLocal` ADD `VideoBitDepth` varchar(100) NOT NULL DEFAULT ''");
            cmds.Add("ALTER TABLE `VideoLocal` ADD `VideoFrameRate` varchar(100) NOT NULL DEFAULT ''");
            cmds.Add("ALTER TABLE `VideoLocal` ADD `VideoResolution` varchar(100) NOT NULL DEFAULT ''");
            cmds.Add("ALTER TABLE `VideoLocal` ADD `AudioCodec` varchar(100) NOT NULL DEFAULT ''");
            cmds.Add("ALTER TABLE `VideoLocal` ADD `AudioBitrate` varchar(100) NOT NULL DEFAULT ''");
            cmds.Add("ALTER TABLE `VideoLocal` ADD `Duration` bigint NOT NULL DEFAULT 0");
            cmds.Add("INSERT INTO `VideoLocal_Place` (`VideoLocalID`, `FilePath`, `ImportFolderID`, `ImportFolderType`) SELECT `VideoLocalID`, `FilePath`, `ImportFolderID`, 1 as `ImportFolderType` FROM `VideoLocal`");
            cmds.Add("ALTER TABLE `VideoLocal` DROP COLUMN `FilePath`");
            cmds.Add("ALTER TABLE `VideoLocal` DROP COLUMN `ImportFolderID`");
            cmds.Add("CREATE TABLE `CloudAccount` ( `CloudID` INT NOT NULL AUTO_INCREMENT,  `ConnectionString` text character set utf8 NOT NULL,  `Provider` varchar(100) NOT NULL DEFAULT '', `Name` varchar(256) NOT NULL DEFAULT '',  PRIMARY KEY (`CloudID`) ) ; ");
            cmds.Add("ALTER TABLE `ImportFolder` ADD `CloudID` int NULL");
            cmds.Add("ALTER TABLE `VideoLocal_User` MODIFY COLUMN `WatchedDate` datetime NULL");
            cmds.Add("ALTER TABLE `VideoLocal_User` ADD `ResumePosition` bigint NOT NULL DEFAULT 0");
            cmds.Add("DROP TABLE `VideoInfo`");
            try
            {
                using (MySqlConnection conn = new MySqlConnection(GetConnectionString()))
                {
                    conn.Open();

                    foreach (string sql in cmds)
                    {
                        using (MySqlCommand command = new MySqlCommand(sql, conn))
                        {
                            command.ExecuteNonQuery();
                        }
                    }
                }
                UpdateDatabaseVersion(thisVersion);
            }
            catch (Exception ex)
            {
                logger.Error(ex.Message);
            }
        }
        private void UpdateSchema_055(int currentVersionNumber)
        {
            int thisVersion = 55;
            if (currentVersionNumber >= thisVersion) return;

            logger.Info("Updating schema to VERSION: {0}", thisVersion);
            //Remove Videolocal Hash unique constraint. Since we use videolocal to store the non hashed files in cloud drop folders.Empty Hash.

            List<string> cmds = new List<string>();
            cmds.Add("ALTER TABLE `Videolocal` DROP INDEX `UIX_VideoLocal_Hash` ;");
            cmds.Add("ALTER TABLE `VideoLocal` ADD INDEX `UIX_VideoLocal_Hash` (`Hash` ASC) ;");
            try
            {
                using (MySqlConnection conn = new MySqlConnection(GetConnectionString()))
                {
                    conn.Open();

                    foreach (string sql in cmds)
                    {
                        using (MySqlCommand command = new MySqlCommand(sql, conn))
                        {
                            command.ExecuteNonQuery();
                        }
                    }
                }
                UpdateDatabaseVersion(thisVersion);
            }
            catch (Exception ex)
            {
                logger.Error(ex.Message);
            }
        }
        private void ExecuteSQLCommands(List<string> cmds)
        {
            using (MySqlConnection conn = new MySqlConnection(GetConnectionString()))
            {
                conn.Open();

                foreach (string sql in cmds)
                {
                    using (MySqlCommand command = new MySqlCommand(sql, conn))
                    {
                        try
                        {
                            command.ExecuteNonQuery();
                        }
                        catch (Exception ex)
                        {
                            logger.Error(sql + " - " + ex.Message);
                        }
                    }
                }
            }
        }

        public void UpdateSchema_Fix()
        {
            List<string> cmds = new List<string>();

            cmds.Add("drop table `crossref_anidb_mal`;");

            cmds.Add("CREATE TABLE CrossRef_AniDB_MAL( " +
                     " CrossRef_AniDB_MALID INT NOT NULL AUTO_INCREMENT, " +
                     " AnimeID int NOT NULL, " +
                     " MALID int NOT NULL, " +
                     " MALTitle text, " +
                     " StartEpisodeType int NOT NULL, " +
                     " StartEpisodeNumber int NOT NULL, " +
                     " CrossRefSource int NOT NULL, " +
                     " PRIMARY KEY (`CrossRef_AniDB_MALID`) ) ; ");

            cmds.Add(
                "ALTER TABLE `CrossRef_AniDB_MAL` ADD UNIQUE INDEX `UIX_CrossRef_AniDB_MAL_AnimeID` (`AnimeID` ASC) ;");
            cmds.Add(
                "ALTER TABLE `CrossRef_AniDB_MAL` ADD UNIQUE INDEX `UIX_CrossRef_AniDB_MAL_Anime` (`MALID` ASC, `AnimeID` ASC, `StartEpisodeType` ASC, `StartEpisodeNumber` ASC) ;");

            using (MySqlConnection conn = new MySqlConnection(GetConnectionString()))
            {
                conn.Open();

                foreach (string sql in cmds)
                {
                    using (MySqlCommand command = new MySqlCommand(sql, conn))
                    {
                        try
                        {
                            command.ExecuteNonQuery();
                        }
                        catch (Exception ex)
                        {
                            logger.Error(sql + " - " + ex.Message);
                        }
                    }
                }
            }
        }

        private static void UpdateDatabaseVersion(int versionNumber)
        {
            VersionsRepository repVersions = new VersionsRepository();
            Versions ver = repVersions.GetByVersionType(Constants.DatabaseTypeKey);
            if (ver == null) return;

            ver.VersionValue = versionNumber.ToString();
            repVersions.Save(ver);
        }

        #endregion

        #region Create Initial Schema

        public bool CreateInitialSchema()
        {
            int count = 0;

            //string sql = string.Format("select count(VERSIONS) from INFORMATION_SCHEMA where TABLE_SCHEMA = '{0}' and TABLE_NAME = 'VERSIONS' group by TABLE_NAME",
            //	ServerSettings.MySQL_SchemaName);
            string sql =
                string.Format(
                    "select count(*) from information_schema.tables where table_schema='{0}' and table_name = 'Versions'",
                    ServerSettings.MySQL_SchemaName);
            logger.Trace(sql);
            using (MySqlConnection conn = new MySqlConnection(GetConnectionString()))
            {
                conn.Open();
                MySqlCommand cmd = new MySqlCommand(sql, conn);
                object result = cmd.ExecuteScalar();
                count = int.Parse(result.ToString());
            }

            // if the Versions already exists, it means we have done this already
            if (count > 0)
            {
                logger.Trace("Initial schema already exists");
                return false;
            }

            // let's check for Linux MySQL users who have renamed all thier table to lower case
            sql =
                string.Format(
                    "select count(*) from information_schema.tables where table_schema='{0}' and table_name = 'versions'",
                    ServerSettings.MySQL_SchemaName);
            using (MySqlConnection conn = new MySqlConnection(GetConnectionString()))
            {
                conn.Open();
                MySqlCommand cmd = new MySqlCommand(sql, conn);
                object result = cmd.ExecuteScalar();
                count = int.Parse(result.ToString());
            }

            // if the 'versions' already exists, it means we need to fix up the table names
            if (count > 0)
            {
                FixLinuxTables();
                return false;
            }

            logger.Trace("Initial schema doesn't exists, creating now...");

            //Create all the commands to be executed
            List<string> commands = new List<string>();
            commands.AddRange(CreateTableString_Versions());
            commands.AddRange(CreateTableString_AniDB_Anime());
            commands.AddRange(CreateTableString_AniDB_Anime_Category());
            commands.AddRange(CreateTableString_AniDB_Anime_Character());
            commands.AddRange(CreateTableString_AniDB_Anime_Relation());
            commands.AddRange(CreateTableString_AniDB_Anime_Review());
            commands.AddRange(CreateTableString_AniDB_Anime_Similar());
            commands.AddRange(CreateTableString_AniDB_Anime_Tag());
            commands.AddRange(CreateTableString_AniDB_Anime_Title());
            commands.AddRange(CreateTableString_AniDB_Category());
            commands.AddRange(CreateTableString_AniDB_Character());
            commands.AddRange(CreateTableString_AniDB_Character_Seiyuu());
            commands.AddRange(CreateTableString_AniDB_Seiyuu());
            commands.AddRange(CreateTableString_AniDB_Episode());
            commands.AddRange(CreateTableString_AniDB_File());
            commands.AddRange(CreateTableString_AniDB_GroupStatus());
            commands.AddRange(CreateTableString_AniDB_ReleaseGroup());
            commands.AddRange(CreateTableString_AniDB_Review());
            commands.AddRange(CreateTableString_AniDB_Tag());
            commands.AddRange(CreateTableString_AnimeEpisode());
            commands.AddRange(CreateTableString_AnimeEpisode_User());
            commands.AddRange(CreateTableString_AnimeGroup());
            commands.AddRange(CreateTableString_AnimeSeries());
            commands.AddRange(CreateTableString_AnimeSeries_User());
            commands.AddRange(CreateTableString_AnimeGroup_User());
            commands.AddRange(CreateTableString_VideoLocal());
            commands.AddRange(CreateTableString_VideoLocal_User());
            commands.AddRange(CreateTableString_CommandRequest());
            commands.AddRange(CreateTableString_CrossRef_AniDB_Other());
            commands.AddRange(CreateTableString_CrossRef_AniDB_TvDB());
            commands.AddRange(CreateTableString_CrossRef_File_Episode());
            commands.AddRange(CreateTableString_CrossRef_Languages_AniDB_File());
            commands.AddRange(CreateTableString_CrossRef_Subtitles_AniDB_File());
            commands.AddRange(CreateTableString_FileNameHash());
            commands.AddRange(CreateTableString_Language());
            commands.AddRange(CreateTableString_ImportFolder());
            commands.AddRange(CreateTableString_ScheduledUpdate());
            commands.AddRange(CreateTableString_VideoInfo());

            commands.AddRange(CreateTableString_DuplicateFile());
            commands.AddRange(CreateTableString_GroupFilter());
            commands.AddRange(CreateTableString_GroupFilterCondition());
            commands.AddRange(CreateTableString_AniDB_Vote());
            commands.AddRange(CreateTableString_TvDB_ImageFanart());
            commands.AddRange(CreateTableString_TvDB_ImageWideBanner());
            commands.AddRange(CreateTableString_TvDB_ImagePoster());
            commands.AddRange(CreateTableString_TvDB_Episode());
            commands.AddRange(CreateTableString_TvDB_Series());
            commands.AddRange(CreateTableString_AniDB_Anime_DefaultImage());
            commands.AddRange(CreateTableString_MovieDB_Movie());
            commands.AddRange(CreateTableString_MovieDB_Poster());
            commands.AddRange(CreateTableString_MovieDB_Fanart());
            commands.AddRange(CreateTableString_JMMUser());
            commands.AddRange(CreateTableString_Trakt_Episode());
            commands.AddRange(CreateTableString_Trakt_ImagePoster());
            commands.AddRange(CreateTableString_Trakt_ImageFanart());
            commands.AddRange(CreateTableString_Trakt_Show());
            commands.AddRange(CreateTableString_Trakt_Season());
            commands.AddRange(CreateTableString_CrossRef_AniDB_Trakt());


            using (MySqlConnection conn = new MySqlConnection(GetConnectionString()))
            {
                conn.Open();

                foreach (string cmdTable in commands)
                {
                    using (MySqlCommand command = new MySqlCommand(cmdTable, conn))
                    {
                        try
                        {
                            command.ExecuteNonQuery();
                        }
                        catch (Exception ex)
                        {
                            logger.Error(cmdTable + " - " + ex.Message);
                        }
                    }
                }
            }

            Console.WriteLine("Creating version...");
            Versions ver1 = new Versions();
            ver1.VersionType = Constants.DatabaseTypeKey;
            ver1.VersionValue = "1";

            VersionsRepository repVer = new VersionsRepository();
            repVer.Save(ver1);
            return true;
        }

        private void FixLinuxTables()
        {
            logger.Info("Fixing MySQL/Linux table names");

            List<string> fixCmds = new List<string>();
            fixCmds.Add("RENAME TABLE anidb_anime TO AniDB_Anime;");
            fixCmds.Add("RENAME TABLE anidb_anime_category TO AniDB_Anime_Category;");
            fixCmds.Add("RENAME TABLE anidb_anime_character TO AniDB_Anime_Character;");
            fixCmds.Add("RENAME TABLE anidb_anime_defaultimage TO AniDB_Anime_DefaultImage;");
            fixCmds.Add("RENAME TABLE anidb_anime_relation TO AniDB_Anime_Relation;");
            fixCmds.Add("RENAME TABLE anidb_anime_review TO AniDB_Anime_Review;");
            fixCmds.Add("RENAME TABLE anidb_anime_similar TO AniDB_Anime_Similar;");
            fixCmds.Add("RENAME TABLE anidb_anime_tag TO AniDB_Anime_Tag;");
            fixCmds.Add("RENAME TABLE anidb_anime_title TO AniDB_Anime_Title;");
            fixCmds.Add("RENAME TABLE anidb_category TO AniDB_Category;");
            fixCmds.Add("RENAME TABLE anidb_character TO AniDB_Character;");
            fixCmds.Add("RENAME TABLE anidb_character_seiyuu TO AniDB_Character_Seiyuu;");
            fixCmds.Add("RENAME TABLE anidb_episode TO AniDB_Episode;");
            fixCmds.Add("RENAME TABLE anidb_file TO AniDB_File;");
            fixCmds.Add("RENAME TABLE anidb_groupstatus TO AniDB_GroupStatus;");
            fixCmds.Add("RENAME TABLE anidb_myliststats TO AniDB_MylistStats;");
            fixCmds.Add("RENAME TABLE anidb_recommendation TO AniDB_Recommendation;");
            fixCmds.Add("RENAME TABLE anidb_releasegroup TO AniDB_ReleaseGroup;");
            fixCmds.Add("RENAME TABLE anidb_review TO AniDB_Review;");
            fixCmds.Add("RENAME TABLE anidb_seiyuu TO AniDB_Seiyuu;");
            fixCmds.Add("RENAME TABLE anidb_tag TO AniDB_Tag;");
            fixCmds.Add("RENAME TABLE anidb_vote TO AniDB_Vote;");
            fixCmds.Add("RENAME TABLE animeepisode TO AnimeEpisode;");
            fixCmds.Add("RENAME TABLE animeepisode_user TO AnimeEpisode_User;");
            fixCmds.Add("RENAME TABLE animegroup TO AnimeGroup;");
            fixCmds.Add("RENAME TABLE animegroup_user TO AnimeGroup_User;");
            fixCmds.Add("RENAME TABLE animeseries TO AnimeSeries;");
            fixCmds.Add("RENAME TABLE animeseries_user TO AnimeSeries_User;");
            fixCmds.Add("RENAME TABLE bookmarkedanime TO BookmarkedAnime;");
            fixCmds.Add("RENAME TABLE commandrequest TO CommandRequest;");
            fixCmds.Add("RENAME TABLE crossref_anidb_mal TO CrossRef_AniDB_MAL;");
            fixCmds.Add("RENAME TABLE crossref_anidb_other TO CrossRef_AniDB_Other;");
            fixCmds.Add("RENAME TABLE crossref_anidb_trakt TO CrossRef_AniDB_Trakt;");
            fixCmds.Add("RENAME TABLE crossref_anidb_trakt_episode TO CrossRef_AniDB_Trakt_Episode;");
            fixCmds.Add("RENAME TABLE crossref_anidb_traktv2 TO CrossRef_AniDB_TraktV2;");
            fixCmds.Add("RENAME TABLE crossref_anidb_tvdb TO CrossRef_AniDB_TvDB;");
            fixCmds.Add("RENAME TABLE crossref_anidb_tvdb_episode TO CrossRef_AniDB_TvDB_Episode;");
            fixCmds.Add("RENAME TABLE crossref_anidb_tvdbv2 TO CrossRef_AniDB_TvDBV2;");
            fixCmds.Add("RENAME TABLE crossref_customtag TO CrossRef_CustomTag;");
            fixCmds.Add("RENAME TABLE crossref_file_episode TO CrossRef_File_Episode;");
            fixCmds.Add("RENAME TABLE crossref_languages_anidb_file TO CrossRef_Languages_AniDB_File;");
            fixCmds.Add("RENAME TABLE crossref_subtitles_anidb_file TO CrossRef_Subtitles_AniDB_File;");
            fixCmds.Add("RENAME TABLE customtag TO CustomTag;");
            fixCmds.Add("RENAME TABLE duplicatefile TO DuplicateFile;");
            fixCmds.Add("RENAME TABLE fileffdshowpreset TO FileFfdshowPreset;");
            fixCmds.Add("RENAME TABLE filenamehash TO FileNameHash;");
            fixCmds.Add("RENAME TABLE groupfilter TO GroupFilter;");
            fixCmds.Add("RENAME TABLE groupfiltercondition TO GroupFilterCondition;");
            fixCmds.Add("RENAME TABLE ignoreanime TO IgnoreAnime;");
            fixCmds.Add("RENAME TABLE importfolder TO ImportFolder;");
            fixCmds.Add("RENAME TABLE jmmuser TO JMMUser;");
            fixCmds.Add("RENAME TABLE language TO Language;");
            fixCmds.Add("RENAME TABLE logmessage TO LogMessage;");
            fixCmds.Add("RENAME TABLE moviedb_fanart TO MovieDB_Fanart;");
            fixCmds.Add("RENAME TABLE moviedb_movie TO MovieDB_Movie;");
            fixCmds.Add("RENAME TABLE moviedb_poster TO MovieDB_Poster;");
            fixCmds.Add("RENAME TABLE playlist TO Playlist;");
            fixCmds.Add("RENAME TABLE renamescript TO RenameScript;");
            fixCmds.Add("RENAME TABLE scheduledupdate TO ScheduledUpdate;");
            fixCmds.Add("RENAME TABLE trakt_episode TO Trakt_Episode;");
            fixCmds.Add("RENAME TABLE trakt_friend TO Trakt_Friend;");
            fixCmds.Add("RENAME TABLE trakt_imagefanart TO Trakt_ImageFanart;");
            fixCmds.Add("RENAME TABLE trakt_imageposter TO Trakt_ImagePoster;");
            fixCmds.Add("RENAME TABLE trakt_season TO Trakt_Season;");
            fixCmds.Add("RENAME TABLE trakt_show TO Trakt_Show;");
            fixCmds.Add("RENAME TABLE tvdb_episode TO TvDB_Episode;");
            fixCmds.Add("RENAME TABLE tvdb_imagefanart TO TvDB_ImageFanart;");
            fixCmds.Add("RENAME TABLE tvdb_imageposter TO TvDB_ImagePoster;");
            fixCmds.Add("RENAME TABLE tvdb_imagewidebanner TO TvDB_ImageWideBanner;");
            fixCmds.Add("RENAME TABLE tvdb_series TO TvDB_Series;");
            fixCmds.Add("RENAME TABLE versions TO Versions;");
            fixCmds.Add("RENAME TABLE videoinfo TO VideoInfo;");
            fixCmds.Add("RENAME TABLE videolocal TO VideoLocal;");
            fixCmds.Add("RENAME TABLE videolocal_user TO VideoLocal_User;");

            using (MySqlConnection conn = new MySqlConnection(GetConnectionString()))
            {
                conn.Open();

                foreach (string cmdFix in fixCmds)
                {
                    try
                    {
                        logger.Info(cmdFix);
                        using (MySqlCommand command = new MySqlCommand(cmdFix, conn))
                        {
                            try
                            {
                                command.ExecuteNonQuery();
                            }
                            catch (Exception ex)
                            {
                                logger.Error(cmdFix + " - " + ex.Message);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.ErrorException(ex.ToString(), ex);
                    }
                }
            }
        }

        public List<string> CreateTableString_Versions()
        {
            List<string> cmds = new List<string>();
            cmds.Add("CREATE TABLE `Versions` ( " +
                     " `VersionsID` INT NOT NULL AUTO_INCREMENT , " +
                     " `VersionType` VARCHAR(100) NOT NULL , " +
                     " `VersionValue` VARCHAR(100) NOT NULL ,  " +
                     " PRIMARY KEY (`VersionsID`) ) ; ");

            cmds.Add("ALTER TABLE `Versions` ADD UNIQUE INDEX `UIX_Versions_VersionType` (`VersionType` ASC) ;");

            return cmds;
        }

        public List<string> CreateTableString_AniDB_Anime()
        {
            List<string> cmds = new List<string>();
            cmds.Add("CREATE TABLE `AniDB_Anime` ( " +
                     " `AniDB_AnimeID` INT NOT NULL AUTO_INCREMENT, " +
                     " `AnimeID` INT NOT NULL, " +
                     " `EpisodeCount` INT NOT NULL, " +
                     " `AirDate` datetime NULL, " +
                     " `EndDate` datetime NULL, " +
                     " `URL` text character set utf8 NULL, " +
                     " `Picname` text character set utf8 NULL, " +
                     " `BeginYear` INT NOT NULL, " +
                     " `EndYear` INT NOT NULL, " +
                     " `AnimeType` INT NOT NULL, " +
                     " `MainTitle` varchar(500) character set utf8 NOT NULL, " +
                     " `AllTitles` varchar(1500) character set utf8 NOT NULL, " +
                     " `AllCategories` text character set utf8 NOT NULL, " +
                     " `AllTags` text character set utf8 NOT NULL, " +
                     " `Description` text character set utf8 NOT NULL, " +
                     " `EpisodeCountNormal` INT NOT NULL, " +
                     " `EpisodeCountSpecial` INT NOT NULL, " +
                     " `Rating` INT NOT NULL, " +
                     " `VoteCount` INT NOT NULL, " +
                     " `TempRating` INT NOT NULL, " +
                     " `TempVoteCount` INT NOT NULL, " +
                     " `AvgReviewRating` INT NOT NULL, " +
                     " `ReviewCount` int NOT NULL, " +
                     " `DateTimeUpdated` datetime NOT NULL, " +
                     " `DateTimeDescUpdated` datetime NOT NULL, " +
                     " `ImageEnabled` int NOT NULL, " +
                     " `AwardList` text character set utf8 NOT NULL, " +
                     " `Restricted` int NOT NULL, " +
                     " `AnimePlanetID` int NULL, " +
                     " `ANNID` int NULL, " +
                     " `AllCinemaID` int NULL, " +
                     " `AnimeNfo` int NULL, " +
                     " `LatestEpisodeNumber` int NULL, " +
                     " PRIMARY KEY (`AniDB_AnimeID`) ) ; ");

            cmds.Add("ALTER TABLE `AniDB_Anime` ADD UNIQUE INDEX `UIX_AniDB_Anime_AnimeID` (`AnimeID` ASC) ;");

            return cmds;
        }

        public List<string> CreateTableString_AniDB_Anime_Category()
        {
            List<string> cmds = new List<string>();
            cmds.Add("CREATE TABLE `AniDB_Anime_Category` ( " +
                     " `AniDB_Anime_CategoryID` INT NOT NULL AUTO_INCREMENT, " +
                     " `AnimeID` int NOT NULL, " +
                     " `CategoryID` int NOT NULL, " +
                     " `Weighting` int NOT NULL, " +
                     " PRIMARY KEY (`AniDB_Anime_CategoryID`) ) ; ");

            cmds.Add("ALTER TABLE `AniDB_Anime_Category` ADD INDEX `IX_AniDB_Anime_Category_AnimeID` (`AnimeID` ASC) ;");
            cmds.Add(
                "ALTER TABLE `AniDB_Anime_Category` ADD UNIQUE INDEX `UIX_AniDB_Anime_Category_AnimeID_CategoryID` (`AnimeID` ASC, `CategoryID` ASC) ;");

            return cmds;
        }

        public List<string> CreateTableString_AniDB_Anime_Character()
        {
            List<string> cmds = new List<string>();
            cmds.Add("CREATE TABLE AniDB_Anime_Character ( " +
                     " `AniDB_Anime_CharacterID`  INT NOT NULL AUTO_INCREMENT, " +
                     " `AnimeID` int NOT NULL, " +
                     " `CharID` int NOT NULL, " +
                     " `CharType` varchar(100) NOT NULL, " +
                     " `EpisodeListRaw` text NULL, " +
                     " PRIMARY KEY (`AniDB_Anime_CharacterID`) ) ; ");

            cmds.Add(
                "ALTER TABLE `AniDB_Anime_Character` ADD INDEX `IX_AniDB_Anime_Character_AnimeID` (`AnimeID` ASC) ;");
            cmds.Add(
                "ALTER TABLE `AniDB_Anime_Character` ADD UNIQUE INDEX `UIX_AniDB_Anime_Character_AnimeID_CharID` (`AnimeID` ASC, `CharID` ASC) ;");

            return cmds;
        }

        public List<string> CreateTableString_AniDB_Anime_Relation()
        {
            List<string> cmds = new List<string>();
            cmds.Add("CREATE TABLE `AniDB_Anime_Relation` ( " +
                     " `AniDB_Anime_RelationID`  INT NOT NULL AUTO_INCREMENT, " +
                     " `AnimeID` int NOT NULL, " +
                     " `RelatedAnimeID` int NOT NULL, " +
                     " `RelationType` varchar(100) NOT NULL, " +
                     " PRIMARY KEY (`AniDB_Anime_RelationID`) ) ; ");

            cmds.Add("ALTER TABLE `AniDB_Anime_Relation` ADD INDEX `IX_AniDB_Anime_Relation_AnimeID` (`AnimeID` ASC) ;");
            cmds.Add(
                "ALTER TABLE `AniDB_Anime_Relation` ADD UNIQUE INDEX `UIX_AniDB_Anime_Relation_AnimeID_RelatedAnimeID` (`AnimeID` ASC, `RelatedAnimeID` ASC) ;");

            return cmds;
        }

        public List<string> CreateTableString_AniDB_Anime_Review()
        {
            List<string> cmds = new List<string>();
            cmds.Add("CREATE TABLE `AniDB_Anime_Review` ( " +
                     " `AniDB_Anime_ReviewID` INT NOT NULL AUTO_INCREMENT, " +
                     " `AnimeID` int NOT NULL, " +
                     " `ReviewID` int NOT NULL, " +
                     " PRIMARY KEY (`AniDB_Anime_ReviewID`) ) ; ");

            cmds.Add("ALTER TABLE `AniDB_Anime_Review` ADD INDEX `IX_AniDB_Anime_Review_AnimeID` (`AnimeID` ASC) ;");
            cmds.Add(
                "ALTER TABLE `AniDB_Anime_Review` ADD UNIQUE INDEX `UIX_AniDB_Anime_Review_AnimeID_ReviewID` (`AnimeID` ASC, `ReviewID` ASC) ;");

            return cmds;
        }

        public List<string> CreateTableString_AniDB_Anime_Similar()
        {
            List<string> cmds = new List<string>();
            cmds.Add("CREATE TABLE `AniDB_Anime_Similar` ( " +
                     " `AniDB_Anime_SimilarID` INT NOT NULL AUTO_INCREMENT, " +
                     " `AnimeID` int NOT NULL, " +
                     " `SimilarAnimeID` int NOT NULL, " +
                     " `Approval` int NOT NULL, " +
                     " `Total` int NOT NULL, " +
                     " PRIMARY KEY (`AniDB_Anime_SimilarID`) ) ; ");

            cmds.Add("ALTER TABLE `AniDB_Anime_Similar` ADD INDEX `IX_AniDB_Anime_Similar_AnimeID` (`AnimeID` ASC) ;");
            cmds.Add(
                "ALTER TABLE `AniDB_Anime_Similar` ADD UNIQUE INDEX `UIX_AniDB_Anime_Similar_AnimeID_SimilarAnimeID` (`AnimeID` ASC, `SimilarAnimeID` ASC) ;");

            return cmds;
        }

        public List<string> CreateTableString_AniDB_Anime_Tag()
        {
            List<string> cmds = new List<string>();
            cmds.Add("CREATE TABLE `AniDB_Anime_Tag` ( " +
                     " `AniDB_Anime_TagID` INT NOT NULL AUTO_INCREMENT, " +
                     " `AnimeID` int NOT NULL, " +
                     " `TagID` int NOT NULL, " +
                     " `Approval` int NOT NULL, " +
                     " PRIMARY KEY (`AniDB_Anime_TagID`) ) ; ");

            cmds.Add("ALTER TABLE `AniDB_Anime_Tag` ADD INDEX `IX_AniDB_Anime_Tag_AnimeID` (`AnimeID` ASC) ;");
            cmds.Add(
                "ALTER TABLE `AniDB_Anime_Tag` ADD UNIQUE INDEX `UIX_AniDB_Anime_Tag_AnimeID_TagID` (`AnimeID` ASC, `TagID` ASC) ;");

            return cmds;
        }

        public List<string> CreateTableString_AniDB_Anime_Title()
        {
            List<string> cmds = new List<string>();
            cmds.Add("CREATE TABLE `AniDB_Anime_Title` ( " +
                     " `AniDB_Anime_TitleID` INT NOT NULL AUTO_INCREMENT, " +
                     " `AnimeID` int NOT NULL, " +
                     " `TitleType` varchar(50) character set utf8 NOT NULL, " +
                     " `Language` varchar(50) character set utf8 NOT NULL, " +
                     " `Title` varchar(500) character set utf8 NOT NULL, " +
                     " PRIMARY KEY (`AniDB_Anime_TitleID`) ) ; ");

            cmds.Add("ALTER TABLE `AniDB_Anime_Title` ADD INDEX `IX_AniDB_Anime_Title_AnimeID` (`AnimeID` ASC) ;");


            return cmds;
        }

        public List<string> CreateTableString_AniDB_Category()
        {
            List<string> cmds = new List<string>();
            cmds.Add("CREATE TABLE `AniDB_Category` ( " +
                     " `AniDB_CategoryID` INT NOT NULL AUTO_INCREMENT, " +
                     " `CategoryID` int NOT NULL, " +
                     " `ParentID` int NOT NULL, " +
                     " `IsHentai` int NOT NULL, " +
                     " `CategoryName` varchar(50) NOT NULL, " +
                     " `CategoryDescription` text NOT NULL, " +
                     " PRIMARY KEY (`AniDB_CategoryID`) ) ; ");

            cmds.Add(
                "ALTER TABLE `AniDB_Category` ADD UNIQUE INDEX `UIX_AniDB_Category_CategoryID` (`CategoryID` ASC) ;");

            return cmds;
        }

        public List<string> CreateTableString_AniDB_Character()
        {
            List<string> cmds = new List<string>();
            cmds.Add("CREATE TABLE `AniDB_Character` ( " +
                     " `AniDB_CharacterID` INT NOT NULL AUTO_INCREMENT, " +
                     " `CharID` int NOT NULL, " +
                     " `CharName` varchar(200) character set utf8 NOT NULL, " +
                     " `PicName` varchar(100) NOT NULL, " +
                     " `CharKanjiName` text character set utf8 NOT NULL, " +
                     " `CharDescription` text character set utf8 NOT NULL, " +
                     " `CreatorListRaw` text NOT NULL, " +
                     " PRIMARY KEY (`AniDB_CharacterID`) ) ; ");

            cmds.Add("ALTER TABLE `AniDB_Character` ADD UNIQUE INDEX `UIX_AniDB_Character_CharID` (`CharID` ASC) ;");

            return cmds;
        }

        public List<string> CreateTableString_AniDB_Character_Seiyuu()
        {
            List<string> cmds = new List<string>();
            cmds.Add("CREATE TABLE `AniDB_Character_Seiyuu` ( " +
                     " `AniDB_Character_SeiyuuID` INT NOT NULL AUTO_INCREMENT, " +
                     " `CharID` int NOT NULL, " +
                     " `SeiyuuID` int NOT NULL, " +
                     " PRIMARY KEY (`AniDB_Character_SeiyuuID`) ) ; ");

            cmds.Add(
                "ALTER TABLE `AniDB_Character_Seiyuu` ADD INDEX `IX_AniDB_Character_Seiyuu_CharID` (`CharID` ASC) ;");
            cmds.Add(
                "ALTER TABLE `AniDB_Character_Seiyuu` ADD INDEX `IX_AniDB_Character_Seiyuu_SeiyuuID` (`SeiyuuID` ASC) ;");
            cmds.Add(
                "ALTER TABLE `AniDB_Character_Seiyuu` ADD UNIQUE INDEX `UIX_AniDB_Character_Seiyuu_CharID_SeiyuuID` (`CharID` ASC, `SeiyuuID` ASC) ;");

            return cmds;
        }

        public List<string> CreateTableString_AniDB_Seiyuu()
        {
            List<string> cmds = new List<string>();
            cmds.Add("CREATE TABLE `AniDB_Seiyuu` ( " +
                     " `AniDB_SeiyuuID` INT NOT NULL AUTO_INCREMENT, " +
                     " `SeiyuuID` int NOT NULL, " +
                     " `SeiyuuName` varchar(200) character set utf8 NOT NULL, " +
                     " `PicName` varchar(100) NOT NULL, " +
                     " PRIMARY KEY (`AniDB_SeiyuuID`) ) ; ");

            cmds.Add("ALTER TABLE `AniDB_Seiyuu` ADD UNIQUE INDEX `UIX_AniDB_Seiyuu_SeiyuuID` (`SeiyuuID` ASC) ;");

            return cmds;
        }

        public List<string> CreateTableString_AniDB_Episode()
        {
            List<string> cmds = new List<string>();
            cmds.Add("CREATE TABLE `AniDB_Episode` ( " +
                     " `AniDB_EpisodeID` INT NOT NULL AUTO_INCREMENT, " +
                     " `EpisodeID` int NOT NULL, " +
                     " `AnimeID` int NOT NULL, " +
                     " `LengthSeconds` int NOT NULL, " +
                     " `Rating` varchar(200) NOT NULL, " +
                     " `Votes` varchar(200) NOT NULL, " +
                     " `EpisodeNumber` int NOT NULL, " +
                     " `EpisodeType` int NOT NULL, " +
                     " `RomajiName` varchar(200) character set utf8 NOT NULL, " +
                     " `EnglishName` varchar(200) character set utf8 NOT NULL, " +
                     " `AirDate` int NOT NULL, " +
                     " `DateTimeUpdated` datetime NOT NULL, " +
                     " PRIMARY KEY (`AniDB_EpisodeID`) ) ; ");

            cmds.Add("ALTER TABLE `AniDB_Episode` ADD INDEX `IX_AniDB_Episode_AnimeID` (`AnimeID` ASC) ;");
            cmds.Add("ALTER TABLE `AniDB_Episode` ADD UNIQUE INDEX `UIX_AniDB_Episode_EpisodeID` (`EpisodeID` ASC) ;");

            return cmds;
        }

        public List<string> CreateTableString_AniDB_File()
        {
            List<string> cmds = new List<string>();
            cmds.Add("CREATE TABLE `AniDB_File`( " +
                     " `AniDB_FileID` INT NOT NULL AUTO_INCREMENT, " +
                     " `FileID` int NOT NULL, " +
                     " `Hash` varchar(50) NOT NULL, " +
                     " `AnimeID` int NOT NULL, " +
                     " `GroupID` int NOT NULL, " +
                     " `File_Source` varchar(200) NOT NULL, " +
                     " `File_AudioCodec` varchar(200) NOT NULL, " +
                     " `File_VideoCodec` varchar(200) NOT NULL, " +
                     " `File_VideoResolution` varchar(200) NOT NULL, " +
                     " `File_FileExtension` varchar(200) NOT NULL, " +
                     " `File_LengthSeconds` int NOT NULL, " +
                     " `File_Description` varchar(500) NOT NULL, " +
                     " `File_ReleaseDate` int NOT NULL, " +
                     " `Anime_GroupName` varchar(200) character set utf8 NOT NULL, " +
                     " `Anime_GroupNameShort` varchar(50) character set utf8 NOT NULL, " +
                     " `Episode_Rating` int NOT NULL, " +
                     " `Episode_Votes` int NOT NULL, " +
                     " `DateTimeUpdated` datetime NOT NULL, " +
                     " `IsWatched` int NOT NULL, " +
                     " `WatchedDate` datetime NULL, " +
                     " `CRC` varchar(200) NOT NULL, " +
                     " `MD5` varchar(200) NOT NULL, " +
                     " `SHA1` varchar(200) NOT NULL, " +
                     " `FileName` varchar(500) character set utf8 NOT NULL, " +
                     " `FileSize` bigint NOT NULL, " +
                     " PRIMARY KEY (`AniDB_FileID`) ) ; ");

            cmds.Add("ALTER TABLE `AniDB_File` ADD UNIQUE INDEX `UIX_AniDB_File_Hash` (`Hash` ASC) ;");
            cmds.Add("ALTER TABLE `AniDB_File` ADD UNIQUE INDEX `UIX_AniDB_File_FileID` (`FileID` ASC) ;");

            return cmds;
        }

        public List<string> CreateTableString_AniDB_GroupStatus()
        {
            List<string> cmds = new List<string>();
            cmds.Add("CREATE TABLE `AniDB_GroupStatus` ( " +
                     " `AniDB_GroupStatusID` INT NOT NULL AUTO_INCREMENT, " +
                     " `AnimeID` int NOT NULL, " +
                     " `GroupID` int NOT NULL, " +
                     " `GroupName` varchar(200) character set utf8 NOT NULL, " +
                     " `CompletionState` int NOT NULL, " +
                     " `LastEpisodeNumber` int NOT NULL, " +
                     " `Rating` int NOT NULL, " +
                     " `Votes` int NOT NULL, " +
                     " `EpisodeRange` text NOT NULL, " +
                     " PRIMARY KEY (`AniDB_GroupStatusID`) ) ; ");

            cmds.Add("ALTER TABLE `AniDB_GroupStatus` ADD INDEX `IX_AniDB_GroupStatus_AnimeID` (`AnimeID` ASC) ;");
            cmds.Add(
                "ALTER TABLE `AniDB_GroupStatus` ADD UNIQUE INDEX `UIX_AniDB_GroupStatus_AnimeID_GroupID` (`AnimeID` ASC, `GroupID` ASC) ;");


            return cmds;
        }

        public List<string> CreateTableString_AniDB_ReleaseGroup()
        {
            List<string> cmds = new List<string>();
            cmds.Add("CREATE TABLE `AniDB_ReleaseGroup` ( " +
                     " `AniDB_ReleaseGroupID` INT NOT NULL AUTO_INCREMENT, " +
                     " `GroupID` int NOT NULL, " +
                     " `Rating` int NOT NULL, " +
                     " `Votes` int NOT NULL, " +
                     " `AnimeCount` int NOT NULL, " +
                     " `FileCount` int NOT NULL, " +
                     " `GroupName` varchar(200) character set utf8 NOT NULL, " +
                     " `GroupNameShort` varchar(50) character set utf8 NOT NULL, " +
                     " `IRCChannel` varchar(200) character set utf8 NOT NULL, " +
                     " `IRCServer` varchar(200) character set utf8 NOT NULL, " +
                     " `URL` varchar(200) character set utf8 NOT NULL, " +
                     " `Picname` varchar(50) NOT NULL, " +
                     " PRIMARY KEY (`AniDB_ReleaseGroupID`) ) ; ");

            cmds.Add(
                "ALTER TABLE `AniDB_ReleaseGroup` ADD UNIQUE INDEX `UIX_AniDB_ReleaseGroup_GroupID` (`GroupID` ASC) ;");


            return cmds;
        }

        public List<string> CreateTableString_AniDB_Review()
        {
            List<string> cmds = new List<string>();
            cmds.Add("CREATE TABLE `AniDB_Review` ( " +
                     " `AniDB_ReviewID` INT NOT NULL AUTO_INCREMENT, " +
                     " `ReviewID` int NOT NULL, " +
                     " `AuthorID` int NOT NULL, " +
                     " `RatingAnimation` int NOT NULL, " +
                     " `RatingSound` int NOT NULL, " +
                     " `RatingStory` int NOT NULL, " +
                     " `RatingCharacter` int NOT NULL, " +
                     " `RatingValue` int NOT NULL, " +
                     " `RatingEnjoyment` int NOT NULL, " +
                     " `ReviewText` text character set utf8 NOT NULL, " +
                     " PRIMARY KEY (`AniDB_ReviewID`) ) ; ");

            cmds.Add("ALTER TABLE `AniDB_Review` ADD UNIQUE INDEX `UIX_AniDB_Review_ReviewID` (`ReviewID` ASC) ;");

            return cmds;
        }

        public List<string> CreateTableString_AniDB_Tag()
        {
            List<string> cmds = new List<string>();
            cmds.Add("CREATE TABLE `AniDB_Tag` ( " +
                     " `AniDB_TagID` INT NOT NULL AUTO_INCREMENT, " +
                     " `TagID` int NOT NULL, " +
                     " `Spoiler` int NOT NULL, " +
                     " `LocalSpoiler` int NOT NULL, " +
                     " `GlobalSpoiler` int NOT NULL, " +
                     " `TagName` varchar(150) character set utf8 NOT NULL, " +
                     " `TagCount` int NOT NULL, " +
                     " `TagDescription` text character set utf8 NOT NULL, " +
                     " PRIMARY KEY (`AniDB_TagID`) ) ; ");

            cmds.Add("ALTER TABLE `AniDB_Tag` ADD UNIQUE INDEX `UIX_AniDB_Tag_TagID` (`TagID` ASC) ;");

            return cmds;
        }

        public List<string> CreateTableString_AnimeEpisode()
        {
            List<string> cmds = new List<string>();
            cmds.Add("CREATE TABLE `AnimeEpisode` ( " +
                     " `AnimeEpisodeID` INT NOT NULL AUTO_INCREMENT, " +
                     " `AnimeSeriesID` int NOT NULL, " +
                     " `AniDB_EpisodeID` int NOT NULL, " +
                     " `DateTimeUpdated` datetime NOT NULL, " +
                     " `DateTimeCreated` datetime NOT NULL, " +
                     " PRIMARY KEY (`AnimeEpisodeID`) ) ; ");

            cmds.Add(
                "ALTER TABLE `AnimeEpisode` ADD UNIQUE INDEX `UIX_AnimeEpisode_AniDB_EpisodeID` (`AniDB_EpisodeID` ASC) ;");
            cmds.Add("ALTER TABLE `AnimeEpisode` ADD INDEX `IX_AnimeEpisode_AnimeSeriesID` (`AnimeSeriesID` ASC) ;");

            return cmds;
        }

        public List<string> CreateTableString_AnimeEpisode_User()
        {
            List<string> cmds = new List<string>();
            cmds.Add("CREATE TABLE `AnimeEpisode_User` ( " +
                     " `AnimeEpisode_UserID` INT NOT NULL AUTO_INCREMENT, " +
                     " `JMMUserID` int NOT NULL, " +
                     " `AnimeEpisodeID` int NOT NULL, " +
                     " `AnimeSeriesID` int NOT NULL, " + // we only have this column to improve performance
                     " `WatchedDate` datetime NULL, " +
                     " `PlayedCount` int NOT NULL, " +
                     " `WatchedCount` int NOT NULL, " +
                     " `StoppedCount` int NOT NULL, " +
                     " PRIMARY KEY (`AnimeEpisode_UserID`) ) ; ");

            cmds.Add(
                "ALTER TABLE `AnimeEpisode_User` ADD UNIQUE INDEX `UIX_AnimeEpisode_User_User_EpisodeID` (`JMMUserID` ASC, `AnimeEpisodeID` ASC) ;");
            cmds.Add(
                "ALTER TABLE `AnimeEpisode_User` ADD INDEX `IX_AnimeEpisode_User_User_AnimeSeriesID` (`JMMUserID` ASC, `AnimeSeriesID` ASC) ;");

            return cmds;
        }

        public List<string> CreateTableString_VideoLocal()
        {
            List<string> cmds = new List<string>();
            cmds.Add("CREATE TABLE `VideoLocal` ( " +
                     " `VideoLocalID` INT NOT NULL AUTO_INCREMENT, " +
                     " `FilePath` text character set utf8 NOT NULL, " +
                     " `ImportFolderID` int NOT NULL, " +
                     " `Hash` varchar(50) NOT NULL, " +
                     " `CRC32` varchar(50) NULL, " +
                     " `MD5` varchar(50) NULL, " +
                     " `SHA1` varchar(50) NULL, " +
                     " `HashSource` int NOT NULL, " +
                     " `FileSize` bigint NOT NULL, " +
                     " `IsIgnored` int NOT NULL, " +
                     " `DateTimeUpdated` datetime NOT NULL, " +
                     " PRIMARY KEY (`VideoLocalID`) ) ; ");

            cmds.Add("ALTER TABLE `VideoLocal` ADD UNIQUE INDEX `UIX_VideoLocal_Hash` (`Hash` ASC) ;");

            return cmds;
        }

        public List<string> CreateTableString_VideoLocal_User()
        {
            List<string> cmds = new List<string>();
            cmds.Add("CREATE TABLE VideoLocal_User( " +
                     " `VideoLocal_UserID` INT NOT NULL AUTO_INCREMENT, " +
                     " `JMMUserID` int NOT NULL, " +
                     " `VideoLocalID` int NOT NULL, " +
                     " `WatchedDate` datetime NOT NULL, " +
                     " PRIMARY KEY (`VideoLocal_UserID`) ) ; ");

            cmds.Add(
                "ALTER TABLE `VideoLocal_User` ADD UNIQUE INDEX `UIX_VideoLocal_User_User_VideoLocalID` (`JMMUserID` ASC, `VideoLocalID` ASC) ;");

            return cmds;
        }

        public List<string> CreateTableString_AnimeGroup()
        {
            List<string> cmds = new List<string>();
            cmds.Add("CREATE TABLE `AnimeGroup` ( " +
                     " `AnimeGroupID` INT NOT NULL AUTO_INCREMENT, " +
                     " `AnimeGroupParentID` int NULL, " +
                     " `GroupName` varchar(200) character set utf8 NOT NULL, " +
                     " `Description` text character set utf8 NULL, " +
                     " `IsManuallyNamed` int NOT NULL, " +
                     " `DateTimeUpdated` datetime NOT NULL, " +
                     " `DateTimeCreated` datetime NOT NULL, " +
                     " `SortName` varchar(200) character set utf8 NOT NULL, " +
                     " `MissingEpisodeCount` int NOT NULL, " +
                     " `MissingEpisodeCountGroups` int NOT NULL, " +
                     " `OverrideDescription` int NOT NULL, " +
                     " `EpisodeAddedDate` datetime NULL, " +
                     " PRIMARY KEY (`AnimeGroupID`) ) ; ");

            return cmds;
        }

        public List<string> CreateTableString_AnimeGroup_User()
        {
            List<string> cmds = new List<string>();
            cmds.Add("CREATE TABLE `AnimeGroup_User` ( " +
                     " `AnimeGroup_UserID` INT NOT NULL AUTO_INCREMENT, " +
                     " `JMMUserID` int NOT NULL, " +
                     " `AnimeGroupID` int NOT NULL, " +
                     " `IsFave` int NOT NULL, " +
                     " `UnwatchedEpisodeCount` int NOT NULL, " +
                     " `WatchedEpisodeCount` int NOT NULL, " +
                     " `WatchedDate` datetime NULL, " +
                     " `PlayedCount` int NOT NULL, " +
                     " `WatchedCount` int NOT NULL, " +
                     " `StoppedCount` int NOT NULL, " +
                     " PRIMARY KEY (`AnimeGroup_UserID`) ) ; ");

            cmds.Add(
                "ALTER TABLE `AnimeGroup_User` ADD UNIQUE INDEX `UIX_AnimeGroup_User_User_GroupID` (`JMMUserID` ASC, `AnimeGroupID` ASC) ;");

            return cmds;
        }

        public List<string> CreateTableString_AnimeSeries()
        {
            List<string> cmds = new List<string>();
            cmds.Add("CREATE TABLE `AnimeSeries` ( " +
                     " `AnimeSeriesID` INT NOT NULL AUTO_INCREMENT, " +
                     " `AnimeGroupID` int NOT NULL, " +
                     " `AniDB_ID` int NOT NULL, " +
                     " `DateTimeUpdated` datetime NOT NULL, " +
                     " `DateTimeCreated` datetime NOT NULL, " +
                     " `DefaultAudioLanguage` varchar(50) NULL, " +
                     " `DefaultSubtitleLanguage` varchar(50) NULL, " +
                     " `MissingEpisodeCount` int NOT NULL, " +
                     " `MissingEpisodeCountGroups` int NOT NULL, " +
                     " `LatestLocalEpisodeNumber` int NOT NULL, " +
                     " `EpisodeAddedDate` datetime NULL, " +
                     " PRIMARY KEY (`AnimeSeriesID`) ) ; ");

            cmds.Add("ALTER TABLE `AnimeSeries` ADD UNIQUE INDEX `UIX_AnimeSeries_AniDB_ID` (`AniDB_ID` ASC) ;");

            return cmds;
        }

        public List<string> CreateTableString_AnimeSeries_User()
        {
            List<string> cmds = new List<string>();
            cmds.Add("CREATE TABLE `AnimeSeries_User` ( " +
                     " `AnimeSeries_UserID` INT NOT NULL AUTO_INCREMENT, " +
                     " `JMMUserID` int NOT NULL, " +
                     " `AnimeSeriesID` int NOT NULL, " +
                     " `UnwatchedEpisodeCount` int NOT NULL, " +
                     " `WatchedEpisodeCount` int NOT NULL, " +
                     " `WatchedDate` datetime NULL, " +
                     " `PlayedCount` int NOT NULL, " +
                     " `WatchedCount` int NOT NULL, " +
                     " `StoppedCount` int NOT NULL, " +
                     " PRIMARY KEY (`AnimeSeries_UserID`) ) ; ");

            cmds.Add(
                "ALTER TABLE `AnimeSeries_User` ADD UNIQUE INDEX `UIX_AnimeSeries_User_User_SeriesID` (`JMMUserID` ASC, `AnimeSeriesID` ASC) ;");

            return cmds;
        }

        public List<string> CreateTableString_CommandRequest()
        {
            List<string> cmds = new List<string>();
            cmds.Add("CREATE TABLE `CommandRequest` ( " +
                     " `CommandRequestID` INT NOT NULL AUTO_INCREMENT, " +
                     " `Priority` int NOT NULL, " +
                     " `CommandType` int NOT NULL, " +
                     " `CommandID` varchar(250) NOT NULL, " +
                     " `CommandDetails` text character set utf8 NOT NULL, " +
                     " `DateTimeUpdated` datetime NOT NULL, " +
                     " PRIMARY KEY (`CommandRequestID`) ) ; ");

            return cmds;
        }


        public List<string> CreateTableString_CrossRef_AniDB_TvDB()
        {
            List<string> cmds = new List<string>();
            cmds.Add("CREATE TABLE `CrossRef_AniDB_TvDB` ( " +
                     " `CrossRef_AniDB_TvDBID` INT NOT NULL AUTO_INCREMENT, " +
                     " `AnimeID` int NOT NULL, " +
                     " `TvDBID` int NOT NULL, " +
                     " `TvDBSeasonNumber` int NOT NULL, " +
                     " `CrossRefSource` int NOT NULL, " +
                     " PRIMARY KEY (`CrossRef_AniDB_TvDBID`) ) ; ");

            cmds.Add(
                "ALTER TABLE `CrossRef_AniDB_TvDB` ADD UNIQUE INDEX `UIX_CrossRef_AniDB_TvDB_AnimeID` (`AnimeID` ASC) ;");

            return cmds;
        }

        public List<string> CreateTableString_CrossRef_AniDB_Other()
        {
            List<string> cmds = new List<string>();
            cmds.Add("CREATE TABLE `CrossRef_AniDB_Other` ( " +
                     " `CrossRef_AniDB_OtherID` INT NOT NULL AUTO_INCREMENT, " +
                     " `AnimeID` int NOT NULL, " +
                     " `CrossRefID` varchar(100) character set utf8 NOT NULL, " +
                     " `CrossRefSource` int NOT NULL, " +
                     " `CrossRefType` int NOT NULL, " +
                     " PRIMARY KEY (`CrossRef_AniDB_OtherID`) ) ; ");

            cmds.Add(
                "ALTER TABLE `CrossRef_AniDB_Other` ADD UNIQUE INDEX `UIX_CrossRef_AniDB_Other` (`AnimeID` ASC, `CrossRefID` ASC, `CrossRefSource` ASC, `CrossRefType` ASC) ;");

            return cmds;
        }

        public List<string> CreateTableString_CrossRef_File_Episode()
        {
            List<string> cmds = new List<string>();
            cmds.Add("CREATE TABLE `CrossRef_File_Episode` ( " +
                     " `CrossRef_File_EpisodeID` INT NOT NULL AUTO_INCREMENT, " +
                     " `Hash` varchar(50) NULL, " +
                     " `FileName` varchar(500) character set utf8 NOT NULL, " +
                     " `FileSize` bigint NOT NULL, " +
                     " `CrossRefSource` int NOT NULL, " +
                     " `AnimeID` int NOT NULL, " +
                     " `EpisodeID` int NOT NULL, " +
                     " `Percentage` int NOT NULL, " +
                     " `EpisodeOrder` int NOT NULL, " +
                     " PRIMARY KEY (`CrossRef_File_EpisodeID`) ) ; ");

            cmds.Add(
                "ALTER TABLE `CrossRef_File_Episode` ADD UNIQUE INDEX `UIX_CrossRef_File_Episode_Hash_EpisodeID` (`Hash` ASC, `EpisodeID` ASC) ;");

            return cmds;
        }

        public List<string> CreateTableString_CrossRef_Languages_AniDB_File()
        {
            List<string> cmds = new List<string>();
            cmds.Add("CREATE TABLE `CrossRef_Languages_AniDB_File` ( " +
                     " `CrossRef_Languages_AniDB_FileID` INT NOT NULL AUTO_INCREMENT, " +
                     " `FileID` int NOT NULL, " +
                     " `LanguageID` int NOT NULL, " +
                     " PRIMARY KEY (`CrossRef_Languages_AniDB_FileID`) ) ; ");

            return cmds;
        }

        public List<string> CreateTableString_CrossRef_Subtitles_AniDB_File()
        {
            List<string> cmds = new List<string>();
            cmds.Add("CREATE TABLE `CrossRef_Subtitles_AniDB_File` ( " +
                     " `CrossRef_Subtitles_AniDB_FileID` INT NOT NULL AUTO_INCREMENT, " +
                     " `FileID` int NOT NULL, " +
                     " `LanguageID` int NOT NULL, " +
                     " PRIMARY KEY (`CrossRef_Subtitles_AniDB_FileID`) ) ; ");

            return cmds;
        }

        public List<string> CreateTableString_FileNameHash()
        {
            List<string> cmds = new List<string>();
            cmds.Add("CREATE TABLE `FileNameHash` ( " +
                     " `FileNameHashID` INT NOT NULL AUTO_INCREMENT, " +
                     " `FileName` varchar(500) character set utf8 NOT NULL, " +
                     " `FileSize` bigint NOT NULL, " +
                     " `Hash` varchar(50) NOT NULL, " +
                     " `DateTimeUpdated` datetime NOT NULL, " +
                     " PRIMARY KEY (`FileNameHashID`) ) ; ");

            // can't do this because of restrictions on index key sizes
            //cmds.Add("ALTER TABLE `FileNameHash` ADD UNIQUE INDEX `UIX_FileNameHash` (`FileName` ASC, `FileSize` ASC, `Hash` ASC) ;");

            return cmds;
        }

        public List<string> CreateTableString_Language()
        {
            List<string> cmds = new List<string>();
            cmds.Add("CREATE TABLE `Language` ( " +
                     " `LanguageID` INT NOT NULL AUTO_INCREMENT, " +
                     " `LanguageName` varchar(100) NOT NULL, " +
                     " PRIMARY KEY (`LanguageID`) ) ; ");

            cmds.Add("ALTER TABLE `Language` ADD UNIQUE INDEX `UIX_Language_LanguageName` (`LanguageName` ASC) ;");

            return cmds;
        }

        public List<string> CreateTableString_ImportFolder()
        {
            List<string> cmds = new List<string>();
            cmds.Add("CREATE TABLE `ImportFolder` ( " +
                     " `ImportFolderID` INT NOT NULL AUTO_INCREMENT, " +
                     " `ImportFolderType` int NOT NULL, " +
                     " `ImportFolderName` varchar(500) character set utf8 NOT NULL, " +
                     " `ImportFolderLocation` varchar(500) character set utf8 NOT NULL, " +
                     " `IsDropSource` int NOT NULL, " +
                     " `IsDropDestination` int NOT NULL, " +
                     " PRIMARY KEY (`ImportFolderID`) ) ; ");

            return cmds;
        }

        public List<string> CreateTableString_ScheduledUpdate()
        {
            List<string> cmds = new List<string>();
            cmds.Add("CREATE TABLE `ScheduledUpdate` ( " +
                     " `ScheduledUpdateID` INT NOT NULL AUTO_INCREMENT, " +
                     " `UpdateType` int NOT NULL, " +
                     " `LastUpdate` datetime NOT NULL, " +
                     " `UpdateDetails` text character set utf8 NOT NULL, " +
                     " PRIMARY KEY (`ScheduledUpdateID`) ) ; ");

            cmds.Add(
                "ALTER TABLE `ScheduledUpdate` ADD UNIQUE INDEX `UIX_ScheduledUpdate_UpdateType` (`UpdateType` ASC) ;");

            return cmds;
        }

        public List<string> CreateTableString_VideoInfo()
        {
            List<string> cmds = new List<string>();
            cmds.Add("CREATE TABLE `VideoInfo` ( " +
                     " `VideoInfoID` INT NOT NULL AUTO_INCREMENT, " +
                     " `Hash` varchar(50) NOT NULL, " +
                     " `FileSize` bigint NOT NULL, " +
                     " `FileName` text character set utf8 NOT NULL, " +
                     " `DateTimeUpdated` datetime NOT NULL, " +
                     " `VideoCodec` varchar(100) NOT NULL, " +
                     " `VideoBitrate` varchar(100) NOT NULL, " +
                     " `VideoFrameRate` varchar(100) NOT NULL, " +
                     " `VideoResolution` varchar(100) NOT NULL, " +
                     " `AudioCodec` varchar(100) NOT NULL, " +
                     " `AudioBitrate` varchar(100) NOT NULL, " +
                     " `Duration` bigint NOT NULL, " +
                     " PRIMARY KEY (`VideoInfoID`) ) ; ");

            cmds.Add("ALTER TABLE `VideoInfo` ADD UNIQUE INDEX `UIX_VideoInfo_Hash` (`Hash` ASC) ;");

            return cmds;
        }

        public List<string> CreateTableString_DuplicateFile()
        {
            List<string> cmds = new List<string>();
            cmds.Add("CREATE TABLE `DuplicateFile` ( " +
                     " `DuplicateFileID` INT NOT NULL AUTO_INCREMENT, " +
                     " `FilePathFile1` varchar(500) character set utf8 NOT NULL, " +
                     " `FilePathFile2` varchar(500) character set utf8 NOT NULL, " +
                     " `ImportFolderIDFile1` int NOT NULL, " +
                     " `ImportFolderIDFile2` int NOT NULL, " +
                     " `Hash` varchar(50) NOT NULL, " +
                     " `DateTimeUpdated` datetime NOT NULL, " +
                     " PRIMARY KEY (`DuplicateFileID`) ) ; ");

            return cmds;
        }

        public List<string> CreateTableString_GroupFilter()
        {
            List<string> cmds = new List<string>();
            cmds.Add("CREATE TABLE `GroupFilter` ( " +
                     " `GroupFilterID` INT NOT NULL AUTO_INCREMENT, " +
                     " `GroupFilterName` varchar(500) character set utf8 NOT NULL, " +
                     " `ApplyToSeries` int NOT NULL, " +
                     " `BaseCondition` int NOT NULL, " +
                     " `SortingCriteria` text character set utf8, " +
                     " PRIMARY KEY (`GroupFilterID`) ) ; ");

            return cmds;
        }

        public List<string> CreateTableString_GroupFilterCondition()
        {
            List<string> cmds = new List<string>();
            cmds.Add("CREATE TABLE `GroupFilterCondition` ( " +
                     " `GroupFilterConditionID` INT NOT NULL AUTO_INCREMENT, " +
                     " `GroupFilterID` int NOT NULL, " +
                     " `ConditionType` int NOT NULL, " +
                     " `ConditionOperator` int NOT NULL, " +
                     " `ConditionParameter` text character set utf8 NOT NULL, " +
                     " PRIMARY KEY (`GroupFilterConditionID`) ) ; ");

            return cmds;
        }

        public List<string> CreateTableString_AniDB_Vote()
        {
            List<string> cmds = new List<string>();
            cmds.Add("CREATE TABLE `AniDB_Vote` ( " +
                     " `AniDB_VoteID` INT NOT NULL AUTO_INCREMENT, " +
                     " `EntityID` int NOT NULL, " +
                     " `VoteValue` int NOT NULL, " +
                     " `VoteType` int NOT NULL, " +
                     " PRIMARY KEY (`AniDB_VoteID`) ) ; ");

            return cmds;
        }

        public List<string> CreateTableString_TvDB_ImageFanart()
        {
            List<string> cmds = new List<string>();
            cmds.Add("CREATE TABLE `TvDB_ImageFanart` ( " +
                     " `TvDB_ImageFanartID` INT NOT NULL AUTO_INCREMENT, " +
                     " `Id` int NOT NULL, " +
                     " `SeriesID` int NOT NULL, " +
                     " `BannerPath` varchar(200) character set utf8,  " +
                     " `BannerType` varchar(200) character set utf8,  " +
                     " `BannerType2` varchar(200) character set utf8,  " +
                     " `Colors` varchar(200) character set utf8,  " +
                     " `Language` varchar(200) character set utf8,  " +
                     " `ThumbnailPath` varchar(200) character set utf8,  " +
                     " `VignettePath` varchar(200) character set utf8,  " +
                     " `Enabled` int NOT NULL, " +
                     " `Chosen` int NOT NULL, " +
                     " PRIMARY KEY (`TvDB_ImageFanartID`) ) ; ");

            cmds.Add("ALTER TABLE `TvDB_ImageFanart` ADD UNIQUE INDEX `UIX_TvDB_ImageFanart_Id` (`Id` ASC) ;");

            return cmds;
        }

        public List<string> CreateTableString_TvDB_ImageWideBanner()
        {
            List<string> cmds = new List<string>();
            cmds.Add("CREATE TABLE `TvDB_ImageWideBanner` ( " +
                     " `TvDB_ImageWideBannerID` INT NOT NULL AUTO_INCREMENT, " +
                     " `Id` int NOT NULL, " +
                     " `SeriesID` int NOT NULL, " +
                     " `BannerPath` varchar(200) character set utf8,  " +
                     " `BannerType` varchar(200) character set utf8,  " +
                     " `BannerType2` varchar(200) character set utf8,  " +
                     " `Language`varchar(200) character set utf8,  " +
                     " `Enabled` int NOT NULL, " +
                     " `SeasonNumber` int, " +
                     " PRIMARY KEY (`TvDB_ImageWideBannerID`) ) ; ");

            cmds.Add("ALTER TABLE `TvDB_ImageWideBanner` ADD UNIQUE INDEX `UIX_TvDB_ImageWideBanner_Id` (`Id` ASC) ;");

            return cmds;
        }

        public List<string> CreateTableString_TvDB_ImagePoster()
        {
            List<string> cmds = new List<string>();
            cmds.Add("CREATE TABLE `TvDB_ImagePoster` ( " +
                     " `TvDB_ImagePosterID` INT NOT NULL AUTO_INCREMENT, " +
                     " `Id` int NOT NULL, " +
                     " `SeriesID` int NOT NULL, " +
                     " `BannerPath` varchar(200) character set utf8,  " +
                     " `BannerType` varchar(200) character set utf8,  " +
                     " `BannerType2` varchar(200) character set utf8,  " +
                     " `Language` varchar(200) character set utf8,  " +
                     " `Enabled` int NOT NULL, " +
                     " `SeasonNumber` int, " +
                     " PRIMARY KEY (`TvDB_ImagePosterID`) ) ; ");

            cmds.Add("ALTER TABLE `TvDB_ImagePoster` ADD UNIQUE INDEX `UIX_TvDB_ImagePoster_Id` (`Id` ASC) ;");

            return cmds;
        }

        public List<string> CreateTableString_TvDB_Episode()
        {
            List<string> cmds = new List<string>();
            cmds.Add("CREATE TABLE `TvDB_Episode` ( " +
                     " `TvDB_EpisodeID` INT NOT NULL AUTO_INCREMENT, " +
                     " `Id` int NOT NULL, " +
                     " `SeriesID` int NOT NULL, " +
                     " `SeasonID` int NOT NULL, " +
                     " `SeasonNumber` int NOT NULL, " +
                     " `EpisodeNumber` int NOT NULL, " +
                     " `EpisodeName` varchar(200) character set utf8, " +
                     " `Overview` text character set utf8, " +
                     " `Filename` varchar(500) character set utf8, " +
                     " `EpImgFlag` int NOT NULL, " +
                     " `FirstAired` varchar(100) character set utf8, " +
                     " `AbsoluteNumber` int, " +
                     " `AirsAfterSeason` int, " +
                     " `AirsBeforeEpisode` int, " +
                     " `AirsBeforeSeason` int, " +
                     " PRIMARY KEY (`TvDB_EpisodeID`) ) ; ");

            cmds.Add("ALTER TABLE `TvDB_Episode` ADD UNIQUE INDEX `UIX_TvDB_Episode_Id` (`Id` ASC) ;");

            return cmds;
        }

        public List<string> CreateTableString_TvDB_Series()
        {
            List<string> cmds = new List<string>();
            cmds.Add("CREATE TABLE `TvDB_Series` ( " +
                     " `TvDB_SeriesID` INT NOT NULL AUTO_INCREMENT, " +
                     " `SeriesID` int NOT NULL, " +
                     " `Overview` text character set utf8, " +
                     " `SeriesName` varchar(250) character set utf8, " +
                     " `Status` varchar(100), " +
                     " `Banner` varchar(100), " +
                     " `Fanart` varchar(100), " +
                     " `Poster` varchar(100), " +
                     " `Lastupdated` varchar(100), " +
                     " PRIMARY KEY (`TvDB_SeriesID`) ) ; ");

            cmds.Add("ALTER TABLE `TvDB_Series` ADD UNIQUE INDEX `UIX_TvDB_Series_Id` (`SeriesID` ASC) ;");

            return cmds;
        }

        public List<string> CreateTableString_AniDB_Anime_DefaultImage()
        {
            List<string> cmds = new List<string>();
            cmds.Add("CREATE TABLE `AniDB_Anime_DefaultImage` ( " +
                     " `AniDB_Anime_DefaultImageID` INT NOT NULL AUTO_INCREMENT, " +
                     " `AnimeID` int NOT NULL, " +
                     " `ImageParentID` int NOT NULL, " +
                     " `ImageParentType` int NOT NULL, " +
                     " `ImageType` int NOT NULL, " +
                     " PRIMARY KEY (`AniDB_Anime_DefaultImageID`) ) ; ");

            cmds.Add(
                "ALTER TABLE `AniDB_Anime_DefaultImage` ADD UNIQUE INDEX `UIX_AniDB_Anime_DefaultImage_ImageType` (`AnimeID` ASC, `ImageType` ASC) ;");

            return cmds;
        }

        public List<string> CreateTableString_MovieDB_Movie()
        {
            List<string> cmds = new List<string>();

            cmds.Add("CREATE TABLE `MovieDB_Movie` ( " +
                     " `MovieDB_MovieID` INT NOT NULL AUTO_INCREMENT, " +
                     " `MovieId` int NOT NULL, " +
                     " `MovieName` varchar(250) character set utf8, " +
                     " `OriginalName` varchar(250) character set utf8, " +
                     " `Overview` text character set utf8, " +
                     " PRIMARY KEY (`MovieDB_MovieID`) ) ; ");

            cmds.Add("ALTER TABLE `MovieDB_Movie` ADD UNIQUE INDEX `UIX_MovieDB_Movie_Id` (`MovieId` ASC) ;");

            return cmds;
        }

        public List<string> CreateTableString_MovieDB_Poster()
        {
            List<string> cmds = new List<string>();

            cmds.Add("CREATE TABLE `MovieDB_Poster` ( " +
                     " `MovieDB_PosterID` INT NOT NULL AUTO_INCREMENT, " +
                     " `ImageID` varchar(100), " +
                     " `MovieId` int NOT NULL, " +
                     " `ImageType` varchar(100), " +
                     " `ImageSize` varchar(100),  " +
                     " `URL` text character set utf8,  " +
                     " `ImageWidth` int NOT NULL,  " +
                     " `ImageHeight` int NOT NULL,  " +
                     " `Enabled` int NOT NULL, " +
                     " PRIMARY KEY (`MovieDB_PosterID`) ) ; ");

            return cmds;
        }

        public List<string> CreateTableString_MovieDB_Fanart()
        {
            List<string> cmds = new List<string>();

            cmds.Add("CREATE TABLE `MovieDB_Fanart` ( " +
                     " `MovieDB_FanartID` INT NOT NULL AUTO_INCREMENT, " +
                     " `ImageID` varchar(100), " +
                     " `MovieId` int NOT NULL, " +
                     " `ImageType` varchar(100), " +
                     " `ImageSize` varchar(100),  " +
                     " `URL` text character set utf8,  " +
                     " `ImageWidth` int NOT NULL,  " +
                     " `ImageHeight` int NOT NULL,  " +
                     " `Enabled` int NOT NULL, " +
                     " PRIMARY KEY (`MovieDB_FanartID`) ) ; ");

            return cmds;
        }

        public List<string> CreateTableString_JMMUser()
        {
            List<string> cmds = new List<string>();

            cmds.Add("CREATE TABLE `JMMUser` ( " +
                     " `JMMUserID` INT NOT NULL AUTO_INCREMENT, " +
                     " `Username` varchar(100) character set utf8, " +
                     " `Password` varchar(100) character set utf8, " +
                     " `IsAdmin` int NOT NULL, " +
                     " `IsAniDBUser` int NOT NULL, " +
                     " `IsTraktUser` int NOT NULL, " +
                     " `HideCategories` text character set utf8, " +
                     " PRIMARY KEY (`JMMUserID`) ) ; ");

            return cmds;
        }

        public List<string> CreateTableString_Trakt_Episode()
        {
            List<string> cmds = new List<string>();

            cmds.Add("CREATE TABLE `Trakt_Episode` ( " +
                     " `Trakt_EpisodeID` INT NOT NULL AUTO_INCREMENT, " +
                     " `Trakt_ShowID` int NOT NULL, " +
                     " `Season` int NOT NULL, " +
                     " `EpisodeNumber` int NOT NULL, " +
                     " `Title` varchar(500) character set utf8, " +
                     " `URL` text character set utf8, " +
                     " `Overview` text character set utf8, " +
                     " `EpisodeImage` varchar(500) character set utf8, " +
                     " PRIMARY KEY (`Trakt_EpisodeID`) ) ; ");

            return cmds;
        }

        public List<string> CreateTableString_Trakt_ImagePoster()
        {
            List<string> cmds = new List<string>();

            cmds.Add("CREATE TABLE `Trakt_ImagePoster` ( " +
                     " `Trakt_ImagePosterID` INT NOT NULL AUTO_INCREMENT, " +
                     " `Trakt_ShowID` int NOT NULL, " +
                     " `Season` int NOT NULL, " +
                     " `ImageURL` varchar(500) character set utf8, " +
                     " `Enabled` int NOT NULL, " +
                     " PRIMARY KEY (`Trakt_ImagePosterID`) ) ; ");

            return cmds;
        }

        public List<string> CreateTableString_Trakt_ImageFanart()
        {
            List<string> cmds = new List<string>();

            cmds.Add("CREATE TABLE `Trakt_ImageFanart` ( " +
                     " `Trakt_ImageFanartID` INT NOT NULL AUTO_INCREMENT, " +
                     " `Trakt_ShowID` int NOT NULL, " +
                     " `Season` int NOT NULL, " +
                     " `ImageURL` varchar(500) character set utf8, " +
                     " `Enabled` int NOT NULL, " +
                     " PRIMARY KEY (`Trakt_ImageFanartID`) ) ; ");

            return cmds;
        }

        public List<string> CreateTableString_Trakt_Show()
        {
            List<string> cmds = new List<string>();

            cmds.Add("CREATE TABLE `Trakt_Show` ( " +
                     " `Trakt_ShowID` INT NOT NULL AUTO_INCREMENT, " +
                     " `TraktID` varchar(100) character set utf8, " +
                     " `Title` varchar(500) character set utf8, " +
                     " `Year` varchar(50) character set utf8, " +
                     " `URL` text character set utf8, " +
                     " `Overview` text character set utf8, " +
                     " `TvDB_ID` int NULL, " +
                     " PRIMARY KEY (`Trakt_ShowID`) ) ; ");

            return cmds;
        }

        public List<string> CreateTableString_Trakt_Season()
        {
            List<string> cmds = new List<string>();

            cmds.Add("CREATE TABLE `Trakt_Season` ( " +
                     " `Trakt_SeasonID` INT NOT NULL AUTO_INCREMENT, " +
                     " `Trakt_ShowID` int NOT NULL, " +
                     " `Season` int NOT NULL, " +
                     " `URL` text character set utf8, " +
                     " PRIMARY KEY (`Trakt_SeasonID`) ) ; ");

            return cmds;
        }

        public List<string> CreateTableString_CrossRef_AniDB_Trakt()
        {
            List<string> cmds = new List<string>();

            cmds.Add("CREATE TABLE `CrossRef_AniDB_Trakt` ( " +
                     " `CrossRef_AniDB_TraktID` INT NOT NULL AUTO_INCREMENT, " +
                     " `AnimeID` int NOT NULL, " +
                     " `TraktID` varchar(100) character set utf8, " +
                     " `TraktSeasonNumber` int NOT NULL, " +
                     " `CrossRefSource` int NOT NULL, " +
                     " PRIMARY KEY (`CrossRef_AniDB_TraktID`) ) ; ");

            return cmds;
        }

        #endregion
    }
}