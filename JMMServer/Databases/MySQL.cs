using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NLog;
using System.Data.SqlClient;
using MySql.Data.MySqlClient;
using JMMServer.Repositories;
using JMMServer.Entities;


namespace JMMServer.Databases
{
	public class MySQL
	{
		private static Logger logger = LogManager.GetCurrentClassLogger();

		public static string GetConnectionString()
		{
			return string.Format("Server={0};Database={1};User ID={2};Password={3}",
					ServerSettings.MySQL_Hostname, ServerSettings.MySQL_SchemaName, ServerSettings.MySQL_Username, ServerSettings.MySQL_Password);
		}

		public static bool DatabaseAlreadyExists()
		{
			string connStr = string.Format("Server={0};User ID={1};Password={2}",
					ServerSettings.MySQL_Hostname, ServerSettings.MySQL_Username, ServerSettings.MySQL_Password);

			string sql = string.Format("SELECT SCHEMA_NAME FROM INFORMATION_SCHEMA.SCHEMATA WHERE SCHEMA_NAME = '{0}'", ServerSettings.MySQL_SchemaName);
			using (MySqlConnection conn = new MySqlConnection(connStr))
			{
				// if the Versions already exists, it means we have done this already
				MySqlCommand cmd = new MySqlCommand(sql, conn);
				conn.Open();
				MySqlDataReader reader = cmd.ExecuteReader();
				while (reader.Read())
				{
					string db = reader.GetString(0);
					return true;
				}
			}
			

			return false;
		}

		public static void CreateDatabase()
		{
			if (DatabaseAlreadyExists()) return;

			string connStr = string.Format("Server={0};User ID={1};Password={2}",
					ServerSettings.MySQL_Hostname, ServerSettings.MySQL_Username, ServerSettings.MySQL_Password);

			string sql = string.Format("CREATE DATABASE {0}", ServerSettings.MySQL_SchemaName);
			using (MySqlConnection conn = new MySqlConnection(connStr))
			{
				MySqlCommand cmd = new MySqlCommand(sql, conn);
				conn.Open();
				cmd.ExecuteNonQuery();
			}
		}

		public static bool TestLogin()
		{
			return true;
		}

		#region Create Initial Schema



		public static void CreateInitialSchema()
		{
			int count = 0;

			//string sql = string.Format("select count(VERSIONS) from INFORMATION_SCHEMA where TABLE_SCHEMA = '{0}' and TABLE_NAME = 'VERSIONS' group by TABLE_NAME",
			//	ServerSettings.MySQL_SchemaName);
			string sql = string.Format("select count(*) from information_schema.tables where table_name = 'VERSIONS'");
			using (MySqlConnection conn = new MySqlConnection(GetConnectionString()))
			{
				conn.Open();
				MySqlCommand cmd = new MySqlCommand(sql, conn);
				object result = cmd.ExecuteScalar();
				count = int.Parse(result.ToString());
			}

			// if the Versions already exists, it means we have done this already
			if (count > 0) return;

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
			/*commands.AddRange(CreateTableString_CommandRequest());
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

			
			*/

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
		}

		public static List<string> CreateTableString_Versions()
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

		public static List<string> CreateTableString_AniDB_Anime()
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

		public static List<string> CreateTableString_AniDB_Anime_Category()
		{
			List<string> cmds = new List<string>();
			cmds.Add("CREATE TABLE `AniDB_Anime_Category` ( " +
				" `AniDB_Anime_CategoryID` INT NOT NULL AUTO_INCREMENT, " +
				" `AnimeID` int NOT NULL, " +
				" `CategoryID` int NOT NULL, " +
				" `Weighting` int NOT NULL, " +
				" PRIMARY KEY (`AniDB_Anime_CategoryID`) ) ; ");

			cmds.Add("ALTER TABLE `AniDB_Anime_Category` ADD INDEX `IX_AniDB_Anime_Category_AnimeID` (`AnimeID` ASC) ;");
			cmds.Add("ALTER TABLE `AniDB_Anime_Category` ADD UNIQUE INDEX `UIX_AniDB_Anime_Category_AnimeID_CategoryID` (`AnimeID` ASC, `CategoryID` ASC) ;");

			return cmds;
		}

		public static List<string> CreateTableString_AniDB_Anime_Character()
		{
			List<string> cmds = new List<string>();
			cmds.Add("CREATE TABLE AniDB_Anime_Character ( " +
				" `AniDB_Anime_CharacterID`  INT NOT NULL AUTO_INCREMENT, " +
				" `AnimeID` int NOT NULL, " +
				" `CharID` int NOT NULL, " +
				" `CharType` varchar(100) NOT NULL, " +
				" `EpisodeListRaw` text NULL, " +
				" PRIMARY KEY (`AniDB_Anime_CharacterID`) ) ; ");

			cmds.Add("ALTER TABLE `AniDB_Anime_Character` ADD INDEX `IX_AniDB_Anime_Character_AnimeID` (`AnimeID` ASC) ;");
			cmds.Add("ALTER TABLE `AniDB_Anime_Character` ADD UNIQUE INDEX `UIX_AniDB_Anime_Character_AnimeID_CharID` (`AnimeID` ASC, `CharID` ASC) ;");

			return cmds;
		}

		public static List<string> CreateTableString_AniDB_Anime_Relation()
		{
			List<string> cmds = new List<string>();
			cmds.Add("CREATE TABLE `AniDB_Anime_Relation` ( " +
				" `AniDB_Anime_RelationID`  INT NOT NULL AUTO_INCREMENT, " +
				" `AnimeID` int NOT NULL, " +
				" `RelatedAnimeID` int NOT NULL, " +
				" `RelationType` varchar(100) NOT NULL, " +
				" PRIMARY KEY (`AniDB_Anime_RelationID`) ) ; ");

			cmds.Add("ALTER TABLE `AniDB_Anime_Relation` ADD INDEX `IX_AniDB_Anime_Relation_AnimeID` (`AnimeID` ASC) ;");
			cmds.Add("ALTER TABLE `AniDB_Anime_Relation` ADD UNIQUE INDEX `UIX_AniDB_Anime_Relation_AnimeID_RelatedAnimeID` (`AnimeID` ASC, `RelatedAnimeID` ASC) ;");

			return cmds;
		}

		public static List<string> CreateTableString_AniDB_Anime_Review()
		{
			List<string> cmds = new List<string>();
			cmds.Add("CREATE TABLE `AniDB_Anime_Review` ( " +
				" `AniDB_Anime_ReviewID` INT NOT NULL AUTO_INCREMENT, " +
				" `AnimeID` int NOT NULL, " +
				" `ReviewID` int NOT NULL, " +
				" PRIMARY KEY (`AniDB_Anime_ReviewID`) ) ; ");

			cmds.Add("ALTER TABLE `AniDB_Anime_Review` ADD INDEX `IX_AniDB_Anime_Review_AnimeID` (`AnimeID` ASC) ;");
			cmds.Add("ALTER TABLE `AniDB_Anime_Review` ADD UNIQUE INDEX `UIX_AniDB_Anime_Review_AnimeID_ReviewID` (`AnimeID` ASC, `ReviewID` ASC) ;");

			return cmds;
		}

		public static List<string> CreateTableString_AniDB_Anime_Similar()
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
			cmds.Add("ALTER TABLE `AniDB_Anime_Similar` ADD UNIQUE INDEX `UIX_AniDB_Anime_Similar_AnimeID_SimilarAnimeID` (`AnimeID` ASC, `SimilarAnimeID` ASC) ;");

			return cmds;
		}

		public static List<string> CreateTableString_AniDB_Anime_Tag()
		{
			List<string> cmds = new List<string>();
			cmds.Add("CREATE TABLE `AniDB_Anime_Tag` ( " +
				" `AniDB_Anime_TagID` INT NOT NULL AUTO_INCREMENT, " +
				" `AnimeID` int NOT NULL, " +
				" `TagID` int NOT NULL, " +
				" `Approval` int NOT NULL, " +
				" PRIMARY KEY (`AniDB_Anime_TagID`) ) ; ");

			cmds.Add("ALTER TABLE `AniDB_Anime_Tag` ADD INDEX `IX_AniDB_Anime_Tag_AnimeID` (`AnimeID` ASC) ;");
			cmds.Add("ALTER TABLE `AniDB_Anime_Tag` ADD UNIQUE INDEX `UIX_AniDB_Anime_Tag_AnimeID_TagID` (`AnimeID` ASC, `TagID` ASC) ;");

			return cmds;
		}

		public static List<string> CreateTableString_AniDB_Anime_Title()
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

		public static List<string> CreateTableString_AniDB_Category()
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

			cmds.Add("ALTER TABLE `AniDB_Category` ADD UNIQUE INDEX `UIX_AniDB_Category_CategoryID` (`CategoryID` ASC) ;");

			return cmds;
		}

		public static List<string> CreateTableString_AniDB_Character()
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

		public static List<string> CreateTableString_AniDB_Character_Seiyuu()
		{
			List<string> cmds = new List<string>();
			cmds.Add("CREATE TABLE `AniDB_Character_Seiyuu` ( " +
				" `AniDB_Character_SeiyuuID` INT NOT NULL AUTO_INCREMENT, " +
				" `CharID` int NOT NULL, " +
				" `SeiyuuID` int NOT NULL, " +
				" PRIMARY KEY (`AniDB_Character_SeiyuuID`) ) ; ");

			cmds.Add("ALTER TABLE `AniDB_Character_Seiyuu` ADD INDEX `IX_AniDB_Character_Seiyuu_CharID` (`CharID` ASC) ;");
			cmds.Add("ALTER TABLE `AniDB_Character_Seiyuu` ADD INDEX `IX_AniDB_Character_Seiyuu_SeiyuuID` (`SeiyuuID` ASC) ;");
			cmds.Add("ALTER TABLE `AniDB_Character_Seiyuu` ADD UNIQUE INDEX `UIX_AniDB_Character_Seiyuu_CharID_SeiyuuID` (`CharID` ASC, `SeiyuuID` ASC) ;");

			return cmds;
		}

		public static List<string> CreateTableString_AniDB_Seiyuu()
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

		public static List<string> CreateTableString_AniDB_Episode()
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

		public static List<string> CreateTableString_AniDB_File()
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

		public static List<string> CreateTableString_AniDB_GroupStatus()
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
			cmds.Add("ALTER TABLE `AniDB_GroupStatus` ADD UNIQUE INDEX `UIX_AniDB_GroupStatus_AnimeID_GroupID` (`AnimeID` ASC, `GroupID` ASC) ;");


			return cmds;
		}

		public static List<string> CreateTableString_AniDB_ReleaseGroup()
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

			cmds.Add("ALTER TABLE `AniDB_ReleaseGroup` ADD UNIQUE INDEX `UIX_AniDB_ReleaseGroup_GroupID` (`GroupID` ASC) ;");


			return cmds;
		}

		public static List<string> CreateTableString_AniDB_Review()
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

		public static List<string> CreateTableString_AniDB_Tag()
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

		public static List<string> CreateTableString_AnimeEpisode()
		{
			List<string> cmds = new List<string>();
			cmds.Add("CREATE TABLE `AnimeEpisode` ( " +
				" `AnimeEpisodeID` INT NOT NULL AUTO_INCREMENT, " +
				" `AnimeSeriesID` int NOT NULL, " +
				" `AniDB_EpisodeID` int NOT NULL, " +
				" `DateTimeUpdated` datetime NOT NULL, " +
				" `DateTimeCreated` datetime NOT NULL, " +
				" PRIMARY KEY (`AnimeEpisodeID`) ) ; ");

			cmds.Add("ALTER TABLE `AnimeEpisode` ADD UNIQUE INDEX `UIX_AnimeEpisode_AniDB_EpisodeID` (`AniDB_EpisodeID` ASC) ;");
			cmds.Add("ALTER TABLE `AnimeEpisode` ADD INDEX `IX_AnimeEpisode_AnimeSeriesID` (`AnimeSeriesID` ASC) ;");

			return cmds;
		}

		public static List<string> CreateTableString_AnimeEpisode_User()
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

			cmds.Add("ALTER TABLE `AnimeEpisode_User` ADD UNIQUE INDEX `UIX_AnimeEpisode_User_User_EpisodeID` (`JMMUserID` ASC, `AnimeEpisodeID` ASC) ;");
			cmds.Add("ALTER TABLE `AnimeEpisode_User` ADD INDEX `IX_AnimeEpisode_User_User_AnimeSeriesID` (`JMMUserID` ASC, `AnimeSeriesID` ASC) ;");

			return cmds;
		}

		public static List<string> CreateTableString_VideoLocal()
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

		public static List<string> CreateTableString_VideoLocal_User()
		{
			List<string> cmds = new List<string>();
			cmds.Add("CREATE TABLE VideoLocal_User( " +
				" `VideoLocal_UserID` INT NOT NULL AUTO_INCREMENT, " +
				" `JMMUserID` int NOT NULL, " +
				" `VideoLocalID` int NOT NULL, " +
				" `WatchedDate` datetime NOT NULL, " +
				" PRIMARY KEY (`VideoLocal_UserID`) ) ; ");

			cmds.Add("ALTER TABLE `VideoLocal_User` ADD UNIQUE INDEX `UIX_VideoLocal_User_User_VideoLocalID` (`JMMUserID` ASC, `VideoLocalID` ASC) ;");

			return cmds;
		}

		public static List<string> CreateTableString_AnimeGroup()
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

		public static List<string> CreateTableString_AnimeGroup_User()
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

			cmds.Add("ALTER TABLE `AnimeGroup_User` ADD UNIQUE INDEX `UIX_AnimeGroup_User_User_GroupID` (`JMMUserID` ASC, `AnimeGroupID` ASC) ;");

			return cmds;
		}

		public static List<string> CreateTableString_AnimeSeries()
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

		public static List<string> CreateTableString_AnimeSeries_User()
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

			cmds.Add("ALTER TABLE `AnimeSeries_User` ADD UNIQUE INDEX `UIX_AnimeSeries_User_User_SeriesID` (`JMMUserID` ASC, `AnimeSeriesID` ASC) ;");

			return cmds;
		}

		#endregion
	}
}
