using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FluentNHibernate.Cfg;
using FluentNHibernate.Cfg.Db;
using MySql.Data.MySqlClient;
using NHibernate;
using Shoko.Commons.Properties;
using NHibernate.Cfg;
using Shoko.Server.Repositories;
using Shoko.Server.Server;
using Shoko.Server.Settings;

// ReSharper disable InconsistentNaming


namespace Shoko.Server.Databases
{
    public class MySQL : BaseDatabase<MySqlConnection>, IDatabase
    {
        public string Name { get; } = "MySQL";
        public int RequiredVersion { get; } = 98;


        private List<DatabaseCommand> createVersionTable = new List<DatabaseCommand>
        {
            new DatabaseCommand(0, 1,
                "CREATE TABLE `Versions` ( `VersionsID` INT NOT NULL AUTO_INCREMENT , `VersionType` VARCHAR(100) NOT NULL , `VersionValue` VARCHAR(100) NOT NULL ,  PRIMARY KEY (`VersionsID`) ) ; "),
            new DatabaseCommand(0, 2,
                "ALTER TABLE `Versions` ADD UNIQUE INDEX `UIX_Versions_VersionType` (`VersionType` ASC) ;"),
        };

        private List<DatabaseCommand> createTables = new List<DatabaseCommand>
        {
            new DatabaseCommand(1, 3,
                "CREATE TABLE `AniDB_Anime` ( `AniDB_AnimeID` INT NOT NULL AUTO_INCREMENT, `AnimeID` INT NOT NULL, `EpisodeCount` INT NOT NULL, `AirDate` datetime NULL, `EndDate` datetime NULL, `URL` text character set utf8 NULL, `Picname` text character set utf8 NULL, `BeginYear` INT NOT NULL, `EndYear` INT NOT NULL, `AnimeType` INT NOT NULL, `MainTitle` varchar(500) character set utf8 NOT NULL, `AllTitles` varchar(1500) character set utf8 NOT NULL, `AllCategories` text character set utf8 NOT NULL, `AllTags` text character set utf8 NOT NULL, `Description` text character set utf8 NOT NULL, `EpisodeCountNormal` INT NOT NULL, `EpisodeCountSpecial` INT NOT NULL, `Rating` INT NOT NULL, `VoteCount` INT NOT NULL, `TempRating` INT NOT NULL, `TempVoteCount` INT NOT NULL, `AvgReviewRating` INT NOT NULL, `ReviewCount` int NOT NULL, `DateTimeUpdated` datetime NOT NULL, `DateTimeDescUpdated` datetime NOT NULL, `ImageEnabled` int NOT NULL, `AwardList` text character set utf8 NOT NULL, `Restricted` int NOT NULL, `AnimePlanetID` int NULL, `ANNID` int NULL, `AllCinemaID` int NULL, `AnimeNfo` int NULL, `LatestEpisodeNumber` int NULL, PRIMARY KEY (`AniDB_AnimeID`) ) ; "),
            new DatabaseCommand(1, 4,
                "ALTER TABLE `AniDB_Anime` ADD UNIQUE INDEX `UIX_AniDB_Anime_AnimeID` (`AnimeID` ASC) ;"),
            new DatabaseCommand(1, 5,
                "CREATE TABLE `AniDB_Anime_Category` ( `AniDB_Anime_CategoryID` INT NOT NULL AUTO_INCREMENT, `AnimeID` int NOT NULL, `CategoryID` int NOT NULL, `Weighting` int NOT NULL, PRIMARY KEY (`AniDB_Anime_CategoryID`) ) ; "),
            new DatabaseCommand(1, 6,
                "ALTER TABLE `AniDB_Anime_Category` ADD INDEX `IX_AniDB_Anime_Category_AnimeID` (`AnimeID` ASC) ;"),
            new DatabaseCommand(1, 7,
                "ALTER TABLE `AniDB_Anime_Category` ADD UNIQUE INDEX `UIX_AniDB_Anime_Category_AnimeID_CategoryID` (`AnimeID` ASC, `CategoryID` ASC) ;"),
            new DatabaseCommand(1, 8,
                "CREATE TABLE AniDB_Anime_Character ( `AniDB_Anime_CharacterID`  INT NOT NULL AUTO_INCREMENT, `AnimeID` int NOT NULL, `CharID` int NOT NULL, `CharType` varchar(100) NOT NULL, `EpisodeListRaw` text NULL, PRIMARY KEY (`AniDB_Anime_CharacterID`) ) ; "),
            new DatabaseCommand(1, 9,
                "ALTER TABLE `AniDB_Anime_Character` ADD INDEX `IX_AniDB_Anime_Character_AnimeID` (`AnimeID` ASC) ;"),
            new DatabaseCommand(1, 10,
                "ALTER TABLE `AniDB_Anime_Character` ADD UNIQUE INDEX `UIX_AniDB_Anime_Character_AnimeID_CharID` (`AnimeID` ASC, `CharID` ASC) ;"),
            new DatabaseCommand(1, 11,
                "CREATE TABLE `AniDB_Anime_Relation` ( `AniDB_Anime_RelationID`  INT NOT NULL AUTO_INCREMENT, `AnimeID` int NOT NULL, `RelatedAnimeID` int NOT NULL, `RelationType` varchar(100) NOT NULL, PRIMARY KEY (`AniDB_Anime_RelationID`) ) ; "),
            new DatabaseCommand(1, 12,
                "ALTER TABLE `AniDB_Anime_Relation` ADD INDEX `IX_AniDB_Anime_Relation_AnimeID` (`AnimeID` ASC) ;"),
            new DatabaseCommand(1, 13,
                "ALTER TABLE `AniDB_Anime_Relation` ADD UNIQUE INDEX `UIX_AniDB_Anime_Relation_AnimeID_RelatedAnimeID` (`AnimeID` ASC, `RelatedAnimeID` ASC) ;"),
            new DatabaseCommand(1, 14,
                "CREATE TABLE `AniDB_Anime_Review` ( `AniDB_Anime_ReviewID` INT NOT NULL AUTO_INCREMENT, `AnimeID` int NOT NULL, `ReviewID` int NOT NULL, PRIMARY KEY (`AniDB_Anime_ReviewID`) ) ; "),
            new DatabaseCommand(1, 15,
                "ALTER TABLE `AniDB_Anime_Review` ADD INDEX `IX_AniDB_Anime_Review_AnimeID` (`AnimeID` ASC) ;"),
            new DatabaseCommand(1, 16,
                "ALTER TABLE `AniDB_Anime_Review` ADD UNIQUE INDEX `UIX_AniDB_Anime_Review_AnimeID_ReviewID` (`AnimeID` ASC, `ReviewID` ASC) ;"),
            new DatabaseCommand(1, 17,
                "CREATE TABLE `AniDB_Anime_Similar` ( `AniDB_Anime_SimilarID` INT NOT NULL AUTO_INCREMENT, `AnimeID` int NOT NULL, `SimilarAnimeID` int NOT NULL, `Approval` int NOT NULL, `Total` int NOT NULL, PRIMARY KEY (`AniDB_Anime_SimilarID`) ) ; "),
            new DatabaseCommand(1, 18,
                "ALTER TABLE `AniDB_Anime_Similar` ADD INDEX `IX_AniDB_Anime_Similar_AnimeID` (`AnimeID` ASC) ;"),
            new DatabaseCommand(1, 19,
                "ALTER TABLE `AniDB_Anime_Similar` ADD UNIQUE INDEX `UIX_AniDB_Anime_Similar_AnimeID_SimilarAnimeID` (`AnimeID` ASC, `SimilarAnimeID` ASC) ;"),
            new DatabaseCommand(1, 20,
                "CREATE TABLE `AniDB_Anime_Tag` ( `AniDB_Anime_TagID` INT NOT NULL AUTO_INCREMENT, `AnimeID` int NOT NULL, `TagID` int NOT NULL, `Approval` int NOT NULL, PRIMARY KEY (`AniDB_Anime_TagID`) ) ; "),
            new DatabaseCommand(1, 21,
                "ALTER TABLE `AniDB_Anime_Tag` ADD INDEX `IX_AniDB_Anime_Tag_AnimeID` (`AnimeID` ASC) ;"),
            new DatabaseCommand(1, 22,
                "ALTER TABLE `AniDB_Anime_Tag` ADD UNIQUE INDEX `UIX_AniDB_Anime_Tag_AnimeID_TagID` (`AnimeID` ASC, `TagID` ASC) ;"),
            new DatabaseCommand(1, 23,
                "CREATE TABLE `AniDB_Anime_Title` ( `AniDB_Anime_TitleID` INT NOT NULL AUTO_INCREMENT, `AnimeID` int NOT NULL, `TitleType` varchar(50) character set utf8 NOT NULL, `Language` varchar(50) character set utf8 NOT NULL, `Title` varchar(500) character set utf8 NOT NULL, PRIMARY KEY (`AniDB_Anime_TitleID`) ) ; "),
            new DatabaseCommand(1, 24,
                "ALTER TABLE `AniDB_Anime_Title` ADD INDEX `IX_AniDB_Anime_Title_AnimeID` (`AnimeID` ASC) ;"),
            new DatabaseCommand(1, 25,
                "CREATE TABLE `AniDB_Category` ( `AniDB_CategoryID` INT NOT NULL AUTO_INCREMENT, `CategoryID` int NOT NULL, `ParentID` int NOT NULL, `IsHentai` int NOT NULL, `CategoryName` varchar(50) NOT NULL, `CategoryDescription` text NOT NULL, PRIMARY KEY (`AniDB_CategoryID`) ) ; "),
            new DatabaseCommand(1, 26,
                "ALTER TABLE `AniDB_Category` ADD UNIQUE INDEX `UIX_AniDB_Category_CategoryID` (`CategoryID` ASC) ;"),
            new DatabaseCommand(1, 27,
                "CREATE TABLE `AniDB_Character` ( `AniDB_CharacterID` INT NOT NULL AUTO_INCREMENT, `CharID` int NOT NULL, `CharName` varchar(200) character set utf8 NOT NULL, `PicName` varchar(100) NOT NULL, `CharKanjiName` text character set utf8 NOT NULL, `CharDescription` text character set utf8 NOT NULL, `CreatorListRaw` text NOT NULL, PRIMARY KEY (`AniDB_CharacterID`) ) ; "),
            new DatabaseCommand(1, 28,
                "ALTER TABLE `AniDB_Character` ADD UNIQUE INDEX `UIX_AniDB_Character_CharID` (`CharID` ASC) ;"),
            new DatabaseCommand(1, 29,
                "CREATE TABLE `AniDB_Character_Seiyuu` ( `AniDB_Character_SeiyuuID` INT NOT NULL AUTO_INCREMENT, `CharID` int NOT NULL, `SeiyuuID` int NOT NULL, PRIMARY KEY (`AniDB_Character_SeiyuuID`) ) ; "),
            new DatabaseCommand(1, 30,
                "ALTER TABLE `AniDB_Character_Seiyuu` ADD INDEX `IX_AniDB_Character_Seiyuu_CharID` (`CharID` ASC) ;"),
            new DatabaseCommand(1, 31,
                "ALTER TABLE `AniDB_Character_Seiyuu` ADD INDEX `IX_AniDB_Character_Seiyuu_SeiyuuID` (`SeiyuuID` ASC) ;"),
            new DatabaseCommand(1, 32,
                "ALTER TABLE `AniDB_Character_Seiyuu` ADD UNIQUE INDEX `UIX_AniDB_Character_Seiyuu_CharID_SeiyuuID` (`CharID` ASC, `SeiyuuID` ASC) ;"),
            new DatabaseCommand(1, 33,
                "CREATE TABLE `AniDB_Seiyuu` ( `AniDB_SeiyuuID` INT NOT NULL AUTO_INCREMENT, `SeiyuuID` int NOT NULL, `SeiyuuName` varchar(200) character set utf8 NOT NULL, `PicName` varchar(100) NOT NULL, PRIMARY KEY (`AniDB_SeiyuuID`) ) ; "),
            new DatabaseCommand(1, 34,
                "ALTER TABLE `AniDB_Seiyuu` ADD UNIQUE INDEX `UIX_AniDB_Seiyuu_SeiyuuID` (`SeiyuuID` ASC) ;"),
            new DatabaseCommand(1, 35,
                "CREATE TABLE `AniDB_Episode` ( `AniDB_EpisodeID` INT NOT NULL AUTO_INCREMENT, `EpisodeID` int NOT NULL, `AnimeID` int NOT NULL, `LengthSeconds` int NOT NULL, `Rating` varchar(200) NOT NULL, `Votes` varchar(200) NOT NULL, `EpisodeNumber` int NOT NULL, `EpisodeType` int NOT NULL, `RomajiName` varchar(200) character set utf8 NOT NULL, `EnglishName` varchar(200) character set utf8 NOT NULL, `AirDate` int NOT NULL, `DateTimeUpdated` datetime NOT NULL, PRIMARY KEY (`AniDB_EpisodeID`) ) ; "),
            new DatabaseCommand(1, 36,
                "ALTER TABLE `AniDB_Episode` ADD INDEX `IX_AniDB_Episode_AnimeID` (`AnimeID` ASC) ;"),
            new DatabaseCommand(1, 37,
                "ALTER TABLE `AniDB_Episode` ADD UNIQUE INDEX `UIX_AniDB_Episode_EpisodeID` (`EpisodeID` ASC) ;"),
            new DatabaseCommand(1, 38,
                "CREATE TABLE `AniDB_File`( `AniDB_FileID` INT NOT NULL AUTO_INCREMENT, `FileID` int NOT NULL, `Hash` varchar(50) NOT NULL, `AnimeID` int NOT NULL, `GroupID` int NOT NULL, `File_Source` varchar(200) NOT NULL, `File_AudioCodec` varchar(500) NOT NULL, `File_VideoCodec` varchar(200) NOT NULL, `File_VideoResolution` varchar(200) NOT NULL, `File_FileExtension` varchar(200) NOT NULL, `File_LengthSeconds` int NOT NULL, `File_Description` varchar(500) NOT NULL, `File_ReleaseDate` int NOT NULL, `Anime_GroupName` varchar(200) character set utf8 NOT NULL, `Anime_GroupNameShort` varchar(50) character set utf8 NOT NULL, `Episode_Rating` int NOT NULL, `Episode_Votes` int NOT NULL, `DateTimeUpdated` datetime NOT NULL, `IsWatched` int NOT NULL, `WatchedDate` datetime NULL, `CRC` varchar(200) NOT NULL, `MD5` varchar(200) NOT NULL, `SHA1` varchar(200) NOT NULL, `FileName` varchar(500) character set utf8 NOT NULL, `FileSize` bigint NOT NULL, PRIMARY KEY (`AniDB_FileID`) ) ; "),
            new DatabaseCommand(1, 39,
                "ALTER TABLE `AniDB_File` ADD UNIQUE INDEX `UIX_AniDB_File_Hash` (`Hash` ASC) ;"),
            new DatabaseCommand(1, 40,
                "ALTER TABLE `AniDB_File` ADD UNIQUE INDEX `UIX_AniDB_File_FileID` (`FileID` ASC) ;"),
            new DatabaseCommand(1, 41,
                "CREATE TABLE `AniDB_GroupStatus` ( `AniDB_GroupStatusID` INT NOT NULL AUTO_INCREMENT, `AnimeID` int NOT NULL, `GroupID` int NOT NULL, `GroupName` varchar(200) character set utf8 NOT NULL, `CompletionState` int NOT NULL, `LastEpisodeNumber` int NOT NULL, `Rating` int NOT NULL, `Votes` int NOT NULL, `EpisodeRange` text NOT NULL, PRIMARY KEY (`AniDB_GroupStatusID`) ) ; "),
            new DatabaseCommand(1, 42,
                "ALTER TABLE `AniDB_GroupStatus` ADD INDEX `IX_AniDB_GroupStatus_AnimeID` (`AnimeID` ASC) ;"),
            new DatabaseCommand(1, 43,
                "ALTER TABLE `AniDB_GroupStatus` ADD UNIQUE INDEX `UIX_AniDB_GroupStatus_AnimeID_GroupID` (`AnimeID` ASC, `GroupID` ASC) ;"),
            new DatabaseCommand(1, 44,
                "CREATE TABLE `AniDB_ReleaseGroup` ( `AniDB_ReleaseGroupID` INT NOT NULL AUTO_INCREMENT, `GroupID` int NOT NULL, `Rating` int NOT NULL, `Votes` int NOT NULL, `AnimeCount` int NOT NULL, `FileCount` int NOT NULL, `GroupName` varchar(200) character set utf8 NOT NULL, `GroupNameShort` varchar(50) character set utf8 NOT NULL, `IRCChannel` varchar(200) character set utf8 NOT NULL, `IRCServer` varchar(200) character set utf8 NOT NULL, `URL` varchar(200) character set utf8 NOT NULL, `Picname` varchar(50) NOT NULL, PRIMARY KEY (`AniDB_ReleaseGroupID`) ) ; "),
            new DatabaseCommand(1, 45,
                "ALTER TABLE `AniDB_ReleaseGroup` ADD UNIQUE INDEX `UIX_AniDB_ReleaseGroup_GroupID` (`GroupID` ASC) ;"),
            new DatabaseCommand(1, 46,
                "CREATE TABLE `AniDB_Review` ( `AniDB_ReviewID` INT NOT NULL AUTO_INCREMENT, `ReviewID` int NOT NULL, `AuthorID` int NOT NULL, `RatingAnimation` int NOT NULL, `RatingSound` int NOT NULL, `RatingStory` int NOT NULL, `RatingCharacter` int NOT NULL, `RatingValue` int NOT NULL, `RatingEnjoyment` int NOT NULL, `ReviewText` text character set utf8 NOT NULL, PRIMARY KEY (`AniDB_ReviewID`) ) ; "),
            new DatabaseCommand(1, 47,
                "ALTER TABLE `AniDB_Review` ADD UNIQUE INDEX `UIX_AniDB_Review_ReviewID` (`ReviewID` ASC) ;"),
            new DatabaseCommand(1, 48,
                "CREATE TABLE `AniDB_Tag` ( `AniDB_TagID` INT NOT NULL AUTO_INCREMENT, `TagID` int NOT NULL, `Spoiler` int NOT NULL, `LocalSpoiler` int NOT NULL, `GlobalSpoiler` int NOT NULL, `TagName` varchar(150) character set utf8 NOT NULL, `TagCount` int NOT NULL, `TagDescription` text character set utf8 NOT NULL, PRIMARY KEY (`AniDB_TagID`) ) ; "),
            new DatabaseCommand(1, 49,
                "ALTER TABLE `AniDB_Tag` ADD UNIQUE INDEX `UIX_AniDB_Tag_TagID` (`TagID` ASC) ;"),
            new DatabaseCommand(1, 50,
                "CREATE TABLE `AnimeEpisode` ( `AnimeEpisodeID` INT NOT NULL AUTO_INCREMENT, `AnimeSeriesID` int NOT NULL, `AniDB_EpisodeID` int NOT NULL, `DateTimeUpdated` datetime NOT NULL, `DateTimeCreated` datetime NOT NULL, PRIMARY KEY (`AnimeEpisodeID`) ) ; "),
            new DatabaseCommand(1, 51,
                "ALTER TABLE `AnimeEpisode` ADD UNIQUE INDEX `UIX_AnimeEpisode_AniDB_EpisodeID` (`AniDB_EpisodeID` ASC) ;"),
            new DatabaseCommand(1, 52,
                "ALTER TABLE `AnimeEpisode` ADD INDEX `IX_AnimeEpisode_AnimeSeriesID` (`AnimeSeriesID` ASC) ;"),
            new DatabaseCommand(1, 53,
                "CREATE TABLE `AnimeEpisode_User` ( `AnimeEpisode_UserID` INT NOT NULL AUTO_INCREMENT, `JMMUserID` int NOT NULL, `AnimeEpisodeID` int NOT NULL, `AnimeSeriesID` int NOT NULL, `WatchedDate` datetime NULL, `PlayedCount` int NOT NULL, `WatchedCount` int NOT NULL, `StoppedCount` int NOT NULL, PRIMARY KEY (`AnimeEpisode_UserID`) ) ; "),
            new DatabaseCommand(1, 54,
                "ALTER TABLE `AnimeEpisode_User` ADD UNIQUE INDEX `UIX_AnimeEpisode_User_User_EpisodeID` (`JMMUserID` ASC, `AnimeEpisodeID` ASC) ;"),
            new DatabaseCommand(1, 55,
                "ALTER TABLE `AnimeEpisode_User` ADD INDEX `IX_AnimeEpisode_User_User_AnimeSeriesID` (`JMMUserID` ASC, `AnimeSeriesID` ASC) ;"),
            new DatabaseCommand(1, 56,
                "CREATE TABLE `AnimeGroup` ( `AnimeGroupID` INT NOT NULL AUTO_INCREMENT, `AnimeGroupParentID` int NULL, `GroupName` varchar(200) character set utf8 NOT NULL, `Description` text character set utf8 NULL, `IsManuallyNamed` int NOT NULL, `DateTimeUpdated` datetime NOT NULL, `DateTimeCreated` datetime NOT NULL, `SortName` varchar(200) character set utf8 NOT NULL, `MissingEpisodeCount` int NOT NULL, `MissingEpisodeCountGroups` int NOT NULL, `OverrideDescription` int NOT NULL, `EpisodeAddedDate` datetime NULL, PRIMARY KEY (`AnimeGroupID`) ) ; "),
            new DatabaseCommand(1, 57,
                "CREATE TABLE `AnimeSeries` ( `AnimeSeriesID` INT NOT NULL AUTO_INCREMENT, `AnimeGroupID` int NOT NULL, `AniDB_ID` int NOT NULL, `DateTimeUpdated` datetime NOT NULL, `DateTimeCreated` datetime NOT NULL, `DefaultAudioLanguage` varchar(50) NULL, `DefaultSubtitleLanguage` varchar(50) NULL, `MissingEpisodeCount` int NOT NULL, `MissingEpisodeCountGroups` int NOT NULL, `LatestLocalEpisodeNumber` int NOT NULL, `EpisodeAddedDate` datetime NULL, PRIMARY KEY (`AnimeSeriesID`) ) ; "),
            new DatabaseCommand(1, 58,
                "ALTER TABLE `AnimeSeries` ADD UNIQUE INDEX `UIX_AnimeSeries_AniDB_ID` (`AniDB_ID` ASC) ;"),
            new DatabaseCommand(1, 59,
                "CREATE TABLE `AnimeSeries_User` ( `AnimeSeries_UserID` INT NOT NULL AUTO_INCREMENT, `JMMUserID` int NOT NULL, `AnimeSeriesID` int NOT NULL, `UnwatchedEpisodeCount` int NOT NULL, `WatchedEpisodeCount` int NOT NULL, `WatchedDate` datetime NULL, `PlayedCount` int NOT NULL, `WatchedCount` int NOT NULL, `StoppedCount` int NOT NULL, PRIMARY KEY (`AnimeSeries_UserID`) ) ; "),
            new DatabaseCommand(1, 60,
                "ALTER TABLE `AnimeSeries_User` ADD UNIQUE INDEX `UIX_AnimeSeries_User_User_SeriesID` (`JMMUserID` ASC, `AnimeSeriesID` ASC) ;"),
            new DatabaseCommand(1, 61,
                "CREATE TABLE `AnimeGroup_User` ( `AnimeGroup_UserID` INT NOT NULL AUTO_INCREMENT, `JMMUserID` int NOT NULL, `AnimeGroupID` int NOT NULL, `IsFave` int NOT NULL, `UnwatchedEpisodeCount` int NOT NULL, `WatchedEpisodeCount` int NOT NULL, `WatchedDate` datetime NULL, `PlayedCount` int NOT NULL, `WatchedCount` int NOT NULL, `StoppedCount` int NOT NULL, PRIMARY KEY (`AnimeGroup_UserID`) ) ; "),
            new DatabaseCommand(1, 62,
                "ALTER TABLE `AnimeGroup_User` ADD UNIQUE INDEX `UIX_AnimeGroup_User_User_GroupID` (`JMMUserID` ASC, `AnimeGroupID` ASC) ;"),
            new DatabaseCommand(1, 63,
                "CREATE TABLE `VideoLocal` ( `VideoLocalID` INT NOT NULL AUTO_INCREMENT, `FilePath` text character set utf8 NOT NULL, `ImportFolderID` int NOT NULL, `Hash` varchar(50) NOT NULL, `CRC32` varchar(50) NULL, `MD5` varchar(50) NULL, `SHA1` varchar(50) NULL, `HashSource` int NOT NULL, `FileSize` bigint NOT NULL, `IsIgnored` int NOT NULL, `DateTimeUpdated` datetime NOT NULL, PRIMARY KEY (`VideoLocalID`) ) ; "),
            new DatabaseCommand(1, 64,
                "ALTER TABLE `VideoLocal` ADD UNIQUE INDEX `UIX_VideoLocal_Hash` (`Hash` ASC) ;"),
            new DatabaseCommand(1, 65,
                "CREATE TABLE VideoLocal_User( `VideoLocal_UserID` INT NOT NULL AUTO_INCREMENT, `JMMUserID` int NOT NULL, `VideoLocalID` int NOT NULL, `WatchedDate` datetime NOT NULL, PRIMARY KEY (`VideoLocal_UserID`) ) ; "),
            new DatabaseCommand(1, 66,
                "ALTER TABLE `VideoLocal_User` ADD UNIQUE INDEX `UIX_VideoLocal_User_User_VideoLocalID` (`JMMUserID` ASC, `VideoLocalID` ASC) ;"),
            new DatabaseCommand(1, 67,
                "CREATE TABLE `CommandRequest` ( `CommandRequestID` INT NOT NULL AUTO_INCREMENT, `Priority` int NOT NULL, `CommandType` int NOT NULL, `CommandID` varchar(250) NOT NULL, `CommandDetails` text character set utf8 NOT NULL, `DateTimeUpdated` datetime NOT NULL, PRIMARY KEY (`CommandRequestID`) ) ; "),
            new DatabaseCommand(1, 68,
                "CREATE TABLE `CrossRef_AniDB_Other` ( `CrossRef_AniDB_OtherID` INT NOT NULL AUTO_INCREMENT, `AnimeID` int NOT NULL, `CrossRefID` varchar(100) character set utf8 NOT NULL, `CrossRefSource` int NOT NULL, `CrossRefType` int NOT NULL, PRIMARY KEY (`CrossRef_AniDB_OtherID`) ) ; "),
            new DatabaseCommand(1, 69,
                "ALTER TABLE `CrossRef_AniDB_Other` ADD UNIQUE INDEX `UIX_CrossRef_AniDB_Other` (`AnimeID` ASC, `CrossRefID` ASC, `CrossRefSource` ASC, `CrossRefType` ASC) ;"),
            new DatabaseCommand(1, 70,
                "CREATE TABLE `CrossRef_AniDB_TvDB` ( `CrossRef_AniDB_TvDBID` INT NOT NULL AUTO_INCREMENT, `AnimeID` int NOT NULL, `TvDBID` int NOT NULL, `TvDBSeasonNumber` int NOT NULL, `CrossRefSource` int NOT NULL, PRIMARY KEY (`CrossRef_AniDB_TvDBID`) ) ; "),
            new DatabaseCommand(1, 71,
                "ALTER TABLE `CrossRef_AniDB_TvDB` ADD UNIQUE INDEX `UIX_CrossRef_AniDB_TvDB_AnimeID` (`AnimeID` ASC) ;"),
            new DatabaseCommand(1, 72,
                "CREATE TABLE `CrossRef_File_Episode` ( `CrossRef_File_EpisodeID` INT NOT NULL AUTO_INCREMENT, `Hash` varchar(50) NULL, `FileName` varchar(500) character set utf8 NOT NULL, `FileSize` bigint NOT NULL, `CrossRefSource` int NOT NULL, `AnimeID` int NOT NULL, `EpisodeID` int NOT NULL, `Percentage` int NOT NULL, `EpisodeOrder` int NOT NULL, PRIMARY KEY (`CrossRef_File_EpisodeID`) ) ; "),
            new DatabaseCommand(1, 73,
                "ALTER TABLE `CrossRef_File_Episode` ADD UNIQUE INDEX `UIX_CrossRef_File_Episode_Hash_EpisodeID` (`Hash` ASC, `EpisodeID` ASC) ;"),
            new DatabaseCommand(1, 74,
                "CREATE TABLE `CrossRef_Languages_AniDB_File` ( `CrossRef_Languages_AniDB_FileID` INT NOT NULL AUTO_INCREMENT, `FileID` int NOT NULL, `LanguageID` int NOT NULL, PRIMARY KEY (`CrossRef_Languages_AniDB_FileID`) ) ; "),
            new DatabaseCommand(1, 75,
                "CREATE TABLE `CrossRef_Subtitles_AniDB_File` ( `CrossRef_Subtitles_AniDB_FileID` INT NOT NULL AUTO_INCREMENT, `FileID` int NOT NULL, `LanguageID` int NOT NULL, PRIMARY KEY (`CrossRef_Subtitles_AniDB_FileID`) ) ; "),
            new DatabaseCommand(1, 76,
                "CREATE TABLE `FileNameHash` ( `FileNameHashID` INT NOT NULL AUTO_INCREMENT, `FileName` varchar(500) character set utf8 NOT NULL, `FileSize` bigint NOT NULL, `Hash` varchar(50) NOT NULL, `DateTimeUpdated` datetime NOT NULL, PRIMARY KEY (`FileNameHashID`) ) ; "),
            new DatabaseCommand(1, 77,
                "CREATE TABLE `Language` ( `LanguageID` INT NOT NULL AUTO_INCREMENT, `LanguageName` varchar(100) NOT NULL, PRIMARY KEY (`LanguageID`) ) ; "),
            new DatabaseCommand(1, 78,
                "ALTER TABLE `Language` ADD UNIQUE INDEX `UIX_Language_LanguageName` (`LanguageName` ASC) ;"),
            new DatabaseCommand(1, 79,
                "CREATE TABLE `ImportFolder` ( `ImportFolderID` INT NOT NULL AUTO_INCREMENT, `ImportFolderType` int NOT NULL, `ImportFolderName` varchar(500) character set utf8 NOT NULL, `ImportFolderLocation` varchar(500) character set utf8 NOT NULL, `IsDropSource` int NOT NULL, `IsDropDestination` int NOT NULL, PRIMARY KEY (`ImportFolderID`) ) ; "),
            new DatabaseCommand(1, 80,
                "CREATE TABLE `ScheduledUpdate` ( `ScheduledUpdateID` INT NOT NULL AUTO_INCREMENT, `UpdateType` int NOT NULL, `LastUpdate` datetime NOT NULL, `UpdateDetails` text character set utf8 NOT NULL, PRIMARY KEY (`ScheduledUpdateID`) ) ; "),
            new DatabaseCommand(1, 81,
                "ALTER TABLE `ScheduledUpdate` ADD UNIQUE INDEX `UIX_ScheduledUpdate_UpdateType` (`UpdateType` ASC) ;"),
            new DatabaseCommand(1, 82,
                "CREATE TABLE `VideoInfo` ( `VideoInfoID` INT NOT NULL AUTO_INCREMENT, `Hash` varchar(50) NOT NULL, `FileSize` bigint NOT NULL, `FileName` text character set utf8 NOT NULL, `DateTimeUpdated` datetime NOT NULL, `VideoCodec` varchar(100) NOT NULL, `VideoBitrate` varchar(100) NOT NULL, `VideoFrameRate` varchar(100) NOT NULL, `VideoResolution` varchar(100) NOT NULL, `AudioCodec` varchar(100) NOT NULL, `AudioBitrate` varchar(100) NOT NULL, `Duration` bigint NOT NULL, PRIMARY KEY (`VideoInfoID`) ) ; "),
            new DatabaseCommand(1, 83, "ALTER TABLE `VideoInfo` ADD UNIQUE INDEX `UIX_VideoInfo_Hash` (`Hash` ASC) ;"),
            new DatabaseCommand(1, 84,
                "CREATE TABLE `DuplicateFile` ( `DuplicateFileID` INT NOT NULL AUTO_INCREMENT, `FilePathFile1` varchar(500) character set utf8 NOT NULL, `FilePathFile2` varchar(500) character set utf8 NOT NULL, `ImportFolderIDFile1` int NOT NULL, `ImportFolderIDFile2` int NOT NULL, `Hash` varchar(50) NOT NULL, `DateTimeUpdated` datetime NOT NULL, PRIMARY KEY (`DuplicateFileID`) ) ; "),
            new DatabaseCommand(1, 85,
                "CREATE TABLE `GroupFilter` ( `GroupFilterID` INT NOT NULL AUTO_INCREMENT, `GroupFilterName` varchar(500) character set utf8 NOT NULL, `ApplyToSeries` int NOT NULL, `BaseCondition` int NOT NULL, `SortingCriteria` text character set utf8, PRIMARY KEY (`GroupFilterID`) ) ; "),
            new DatabaseCommand(1, 86,
                "CREATE TABLE `GroupFilterCondition` ( `GroupFilterConditionID` INT NOT NULL AUTO_INCREMENT, `GroupFilterID` int NOT NULL, `ConditionType` int NOT NULL, `ConditionOperator` int NOT NULL, `ConditionParameter` text character set utf8 NOT NULL, PRIMARY KEY (`GroupFilterConditionID`) ) ; "),
            new DatabaseCommand(1, 87,
                "CREATE TABLE `AniDB_Vote` ( `AniDB_VoteID` INT NOT NULL AUTO_INCREMENT, `EntityID` int NOT NULL, `VoteValue` int NOT NULL, `VoteType` int NOT NULL, PRIMARY KEY (`AniDB_VoteID`) ) ; "),
            new DatabaseCommand(1, 88,
                "CREATE TABLE `TvDB_ImageFanart` ( `TvDB_ImageFanartID` INT NOT NULL AUTO_INCREMENT, `Id` int NOT NULL, `SeriesID` int NOT NULL, `BannerPath` varchar(200) character set utf8,  `BannerType` varchar(200) character set utf8,  `BannerType2` varchar(200) character set utf8,  `Colors` varchar(200) character set utf8,  `Language` varchar(200) character set utf8,  `ThumbnailPath` varchar(200) character set utf8,  `VignettePath` varchar(200) character set utf8,  `Enabled` int NOT NULL, `Chosen` int NOT NULL, PRIMARY KEY (`TvDB_ImageFanartID`) ) ; "),
            new DatabaseCommand(1, 89,
                "ALTER TABLE `TvDB_ImageFanart` ADD UNIQUE INDEX `UIX_TvDB_ImageFanart_Id` (`Id` ASC) ;"),
            new DatabaseCommand(1, 90,
                "CREATE TABLE `TvDB_ImageWideBanner` ( `TvDB_ImageWideBannerID` INT NOT NULL AUTO_INCREMENT, `Id` int NOT NULL, `SeriesID` int NOT NULL, `BannerPath` varchar(200) character set utf8,  `BannerType` varchar(200) character set utf8,  `BannerType2` varchar(200) character set utf8,  `Language`varchar(200) character set utf8,  `Enabled` int NOT NULL, `SeasonNumber` int, PRIMARY KEY (`TvDB_ImageWideBannerID`) ) ; "),
            new DatabaseCommand(1, 91,
                "ALTER TABLE `TvDB_ImageWideBanner` ADD UNIQUE INDEX `UIX_TvDB_ImageWideBanner_Id` (`Id` ASC) ;"),
            new DatabaseCommand(1, 92,
                "CREATE TABLE `TvDB_ImagePoster` ( `TvDB_ImagePosterID` INT NOT NULL AUTO_INCREMENT, `Id` int NOT NULL, `SeriesID` int NOT NULL, `BannerPath` varchar(200) character set utf8,  `BannerType` varchar(200) character set utf8,  `BannerType2` varchar(200) character set utf8,  `Language` varchar(200) character set utf8,  `Enabled` int NOT NULL, `SeasonNumber` int, PRIMARY KEY (`TvDB_ImagePosterID`) ) ; "),
            new DatabaseCommand(1, 93,
                "ALTER TABLE `TvDB_ImagePoster` ADD UNIQUE INDEX `UIX_TvDB_ImagePoster_Id` (`Id` ASC) ;"),
            new DatabaseCommand(1, 94,
                "CREATE TABLE `TvDB_Episode` ( `TvDB_EpisodeID` INT NOT NULL AUTO_INCREMENT, `Id` int NOT NULL, `SeriesID` int NOT NULL, `SeasonID` int NOT NULL, `SeasonNumber` int NOT NULL, `EpisodeNumber` int NOT NULL, `EpisodeName` varchar(200) character set utf8, `Overview` text character set utf8, `Filename` varchar(500) character set utf8, `EpImgFlag` int NOT NULL, `FirstAired` varchar(100) character set utf8, `AbsoluteNumber` int, `AirsAfterSeason` int, `AirsBeforeEpisode` int, `AirsBeforeSeason` int, PRIMARY KEY (`TvDB_EpisodeID`) ) ; "),
            new DatabaseCommand(1, 95,
                "ALTER TABLE `TvDB_Episode` ADD UNIQUE INDEX `UIX_TvDB_Episode_Id` (`Id` ASC) ;"),
            new DatabaseCommand(1, 96,
                "CREATE TABLE `TvDB_Series` ( `TvDB_SeriesID` INT NOT NULL AUTO_INCREMENT, `SeriesID` int NOT NULL, `Overview` text character set utf8, `SeriesName` varchar(250) character set utf8, `Status` varchar(100), `Banner` varchar(100), `Fanart` varchar(100), `Poster` varchar(100), `Lastupdated` varchar(100), PRIMARY KEY (`TvDB_SeriesID`) ) ; "),
            new DatabaseCommand(1, 97,
                "ALTER TABLE `TvDB_Series` ADD UNIQUE INDEX `UIX_TvDB_Series_Id` (`SeriesID` ASC) ;"),
            new DatabaseCommand(1, 98,
                "CREATE TABLE `AniDB_Anime_DefaultImage` ( `AniDB_Anime_DefaultImageID` INT NOT NULL AUTO_INCREMENT, `AnimeID` int NOT NULL, `ImageParentID` int NOT NULL, `ImageParentType` int NOT NULL, `ImageType` int NOT NULL, PRIMARY KEY (`AniDB_Anime_DefaultImageID`) ) ; "),
            new DatabaseCommand(1, 99,
                "ALTER TABLE `AniDB_Anime_DefaultImage` ADD UNIQUE INDEX `UIX_AniDB_Anime_DefaultImage_ImageType` (`AnimeID` ASC, `ImageType` ASC) ;"),
            new DatabaseCommand(1, 100,
                "CREATE TABLE `MovieDB_Movie` ( `MovieDB_MovieID` INT NOT NULL AUTO_INCREMENT, `MovieId` int NOT NULL, `MovieName` varchar(250) character set utf8, `OriginalName` varchar(250) character set utf8, `Overview` text character set utf8, PRIMARY KEY (`MovieDB_MovieID`) ) ; "),
            new DatabaseCommand(1, 101,
                "ALTER TABLE `MovieDB_Movie` ADD UNIQUE INDEX `UIX_MovieDB_Movie_Id` (`MovieId` ASC) ;"),
            new DatabaseCommand(1, 102,
                "CREATE TABLE `MovieDB_Poster` ( `MovieDB_PosterID` INT NOT NULL AUTO_INCREMENT, `ImageID` varchar(100), `MovieId` int NOT NULL, `ImageType` varchar(100), `ImageSize` varchar(100),  `URL` text character set utf8,  `ImageWidth` int NOT NULL,  `ImageHeight` int NOT NULL,  `Enabled` int NOT NULL, PRIMARY KEY (`MovieDB_PosterID`) ) ; "),
            new DatabaseCommand(1, 103,
                "CREATE TABLE `MovieDB_Fanart` ( `MovieDB_FanartID` INT NOT NULL AUTO_INCREMENT, `ImageID` varchar(100), `MovieId` int NOT NULL, `ImageType` varchar(100), `ImageSize` varchar(100),  `URL` text character set utf8,  `ImageWidth` int NOT NULL,  `ImageHeight` int NOT NULL,  `Enabled` int NOT NULL, PRIMARY KEY (`MovieDB_FanartID`) ) ; "),
            new DatabaseCommand(1, 104,
                "CREATE TABLE `JMMUser` ( `JMMUserID` INT NOT NULL AUTO_INCREMENT, `Username` varchar(100) character set utf8, `Password` varchar(100) character set utf8, `IsAdmin` int NOT NULL, `IsAniDBUser` int NOT NULL, `IsTraktUser` int NOT NULL, `HideCategories` text character set utf8, PRIMARY KEY (`JMMUserID`) ) ; "),
            new DatabaseCommand(1, 105,
                "CREATE TABLE `Trakt_Episode` ( `Trakt_EpisodeID` INT NOT NULL AUTO_INCREMENT, `Trakt_ShowID` int NOT NULL, `Season` int NOT NULL, `EpisodeNumber` int NOT NULL, `Title` varchar(500) character set utf8, `URL` text character set utf8, `Overview` text character set utf8, `EpisodeImage` varchar(500) character set utf8, PRIMARY KEY (`Trakt_EpisodeID`) ) ; "),
            new DatabaseCommand(1, 106,
                "CREATE TABLE `Trakt_ImagePoster` ( `Trakt_ImagePosterID` INT NOT NULL AUTO_INCREMENT, `Trakt_ShowID` int NOT NULL, `Season` int NOT NULL, `ImageURL` varchar(500) character set utf8, `Enabled` int NOT NULL, PRIMARY KEY (`Trakt_ImagePosterID`) ) ; "),
            new DatabaseCommand(1, 107,
                "CREATE TABLE `Trakt_ImageFanart` ( `Trakt_ImageFanartID` INT NOT NULL AUTO_INCREMENT, `Trakt_ShowID` int NOT NULL, `Season` int NOT NULL, `ImageURL` varchar(500) character set utf8, `Enabled` int NOT NULL, PRIMARY KEY (`Trakt_ImageFanartID`) ) ; "),
            new DatabaseCommand(1, 108,
                "CREATE TABLE `Trakt_Show` ( `Trakt_ShowID` INT NOT NULL AUTO_INCREMENT, `TraktID` varchar(100) character set utf8, `Title` varchar(500) character set utf8, `Year` varchar(50) character set utf8, `URL` text character set utf8, `Overview` text character set utf8, `TvDB_ID` int NULL, PRIMARY KEY (`Trakt_ShowID`) ) ; "),
            new DatabaseCommand(1, 109,
                "CREATE TABLE `Trakt_Season` ( `Trakt_SeasonID` INT NOT NULL AUTO_INCREMENT, `Trakt_ShowID` int NOT NULL, `Season` int NOT NULL, `URL` text character set utf8, PRIMARY KEY (`Trakt_SeasonID`) ) ; "),
            new DatabaseCommand(1, 110,
                "CREATE TABLE `CrossRef_AniDB_Trakt` ( `CrossRef_AniDB_TraktID` INT NOT NULL AUTO_INCREMENT, `AnimeID` int NOT NULL, `TraktID` varchar(100) character set utf8, `TraktSeasonNumber` int NOT NULL, `CrossRefSource` int NOT NULL, PRIMARY KEY (`CrossRef_AniDB_TraktID`) ) ; "),
        };

        private List<DatabaseCommand> patchCommands = new List<DatabaseCommand>
        {
            //Patches
            new DatabaseCommand(2, 1,
                "CREATE TABLE `IgnoreAnime` ( `IgnoreAnimeID` INT NOT NULL AUTO_INCREMENT ,  `JMMUserID` int NOT NULL,  `AnimeID` int NOT NULL,  `IgnoreType` int NOT NULL,  PRIMARY KEY (`IgnoreAnimeID`) ) ; "),
            new DatabaseCommand(2, 2,
                "ALTER TABLE `IgnoreAnime` ADD UNIQUE INDEX `UIX_IgnoreAnime_User_AnimeID` (`JMMUserID` ASC, `AnimeID` ASC, `IgnoreType` ASC) ;"),
            new DatabaseCommand(3, 1,
                "CREATE TABLE `Trakt_Friend` ( `Trakt_FriendID` INT NOT NULL AUTO_INCREMENT , `Username` varchar(100) character set utf8 NOT NULL, `FullName` varchar(100) character set utf8 NULL, `Gender` varchar(100) character set utf8 NULL, `Age` varchar(100) character set utf8 NULL, `Location` varchar(100) character set utf8 NULL, `About` text character set utf8 NULL, `Joined` int NOT NULL, `Avatar` text character set utf8 NULL, `Url` text character set utf8 NULL, `LastAvatarUpdate` datetime NOT NULL, PRIMARY KEY (`Trakt_FriendID`) ) ; "),
            new DatabaseCommand(3, 2,
                "ALTER TABLE `Trakt_Friend` ADD UNIQUE INDEX `UIX_Trakt_Friend_Username` (`Username` ASC) ;"),
            new DatabaseCommand(4, 1, "ALTER TABLE AnimeGroup ADD DefaultAnimeSeriesID int NULL"),
            new DatabaseCommand(5, 1, "ALTER TABLE JMMUser ADD CanEditServerSettings int NULL"),
            new DatabaseCommand(6, 1, "ALTER TABLE VideoInfo ADD VideoBitDepth varchar(100) NULL"),
            new DatabaseCommand(7, 1, DatabaseFixes.FixDuplicateTvDBLinks),
            new DatabaseCommand(7, 2, DatabaseFixes.FixDuplicateTraktLinks),
            new DatabaseCommand(7, 3,
                "ALTER TABLE `CrossRef_AniDB_TvDB` ADD UNIQUE INDEX `UIX_CrossRef_AniDB_TvDB_Season` (`TvDBID` ASC, `TvDBSeasonNumber` ASC) ;"),
            new DatabaseCommand(7, 4,
                "ALTER TABLE `CrossRef_AniDB_Trakt` ADD UNIQUE INDEX `UIX_CrossRef_AniDB_Trakt_Season` (`TraktID` ASC, `TraktSeasonNumber` ASC) ;"),
            new DatabaseCommand(7, 5,
                "ALTER TABLE `CrossRef_AniDB_Trakt` ADD UNIQUE INDEX `UIX_CrossRef_AniDB_Trakt_Anime` (`AnimeID` ASC) ;"),
            new DatabaseCommand(8, 1,
                "ALTER TABLE JMMUser CHANGE COLUMN Password Password VARCHAR(150) NULL DEFAULT NULL ;"),
            new DatabaseCommand(9, 1,
                "ALTER TABLE `CommandRequest` CHANGE COLUMN `CommandID` `CommandID` text character set utf8 NOT NULL ;"),
            new DatabaseCommand(9, 2,
                "ALTER TABLE `CrossRef_File_Episode` CHANGE COLUMN `FileName` `FileName` text character set utf8 NOT NULL ;"),
            new DatabaseCommand(9, 3,
                "ALTER TABLE `FileNameHash` CHANGE COLUMN `FileName` `FileName` text character set utf8 NOT NULL ;"),
            new DatabaseCommand(10, 1,
                "ALTER TABLE `AniDB_Category` CHANGE COLUMN `CategoryName` `CategoryName` text character set utf8 NOT NULL ;"),
            new DatabaseCommand(10, 2,
                "ALTER TABLE `AniDB_Category` CHANGE COLUMN `CategoryDescription` `CategoryDescription` text character set utf8 NOT NULL ;"),
            new DatabaseCommand(10, 3,
                "ALTER TABLE `AniDB_Episode` CHANGE COLUMN `RomajiName` `RomajiName` text character set utf8 NOT NULL ;"),
            new DatabaseCommand(10, 4,
                "ALTER TABLE `AniDB_Episode` CHANGE COLUMN `EnglishName` `EnglishName` text character set utf8 NOT NULL ;"),
            new DatabaseCommand(10, 5,
                "ALTER TABLE `AniDB_Anime_Relation` CHANGE COLUMN `RelationType` `RelationType` text character set utf8 NOT NULL ;"),
            new DatabaseCommand(10, 6,
                "ALTER TABLE `AniDB_Character` CHANGE COLUMN `CharName` `CharName` text character set utf8 NOT NULL ;"),
            new DatabaseCommand(10, 7,
                "ALTER TABLE `AniDB_Seiyuu` CHANGE COLUMN `SeiyuuName` `SeiyuuName` text character set utf8 NOT NULL ;"),
            new DatabaseCommand(10, 8,
                "ALTER TABLE `AniDB_File` CHANGE COLUMN `File_Description` `File_Description` text character set utf8 NOT NULL ;"),
            new DatabaseCommand(10, 9,
                "ALTER TABLE `AniDB_File` CHANGE COLUMN `Anime_GroupName` `Anime_GroupName` text character set utf8 NOT NULL ;"),
            new DatabaseCommand(10, 10,
                "ALTER TABLE `AniDB_File` CHANGE COLUMN `Anime_GroupNameShort` `Anime_GroupNameShort` text character set utf8 NOT NULL ;"),
            new DatabaseCommand(10, 11,
                "ALTER TABLE `AniDB_File` CHANGE COLUMN `FileName` `FileName` text character set utf8 NOT NULL ;"),
            new DatabaseCommand(10, 12,
                "ALTER TABLE `AniDB_GroupStatus` CHANGE COLUMN `GroupName` `GroupName` text character set utf8 NOT NULL ;"),
            new DatabaseCommand(10, 13,
                "ALTER TABLE `AniDB_ReleaseGroup` CHANGE COLUMN `GroupName` `GroupName` text character set utf8 NOT NULL ;"),
            new DatabaseCommand(10, 14,
                "ALTER TABLE `AniDB_ReleaseGroup` CHANGE COLUMN `GroupNameShort` `GroupNameShort` text character set utf8 NOT NULL ;"),
            new DatabaseCommand(10, 15,
                "ALTER TABLE `AniDB_ReleaseGroup` CHANGE COLUMN `URL` `URL` text character set utf8 NOT NULL ;"),
            new DatabaseCommand(10, 16,
                "ALTER TABLE `AnimeGroup` CHANGE COLUMN `GroupName` `GroupName` text character set utf8 NOT NULL ;"),
            new DatabaseCommand(10, 17,
                "ALTER TABLE `AnimeGroup` CHANGE COLUMN `SortName` `SortName` text character set utf8 NOT NULL ;"),
            new DatabaseCommand(10, 18,
                "ALTER TABLE `CommandRequest` CHANGE COLUMN `CommandID` `CommandID` text character set utf8 NOT NULL ;"),
            new DatabaseCommand(10, 19,
                "ALTER TABLE `CrossRef_File_Episode` CHANGE COLUMN `FileName` `FileName` text character set utf8 NOT NULL ;"),
            new DatabaseCommand(10, 20,
                "ALTER TABLE `FileNameHash` CHANGE COLUMN `FileName` `FileName` text character set utf8 NOT NULL ;"),
            new DatabaseCommand(10, 21,
                "ALTER TABLE `ImportFolder` CHANGE COLUMN `ImportFolderLocation` `ImportFolderLocation` text character set utf8 NOT NULL ;"),
            new DatabaseCommand(10, 22,
                "ALTER TABLE `DuplicateFile` CHANGE COLUMN `FilePathFile1` `FilePathFile1` text character set utf8 NOT NULL ;"),
            new DatabaseCommand(10, 23,
                "ALTER TABLE `DuplicateFile` CHANGE COLUMN `FilePathFile2` `FilePathFile2` text character set utf8 NOT NULL ;"),
            new DatabaseCommand(10, 24,
                "ALTER TABLE `TvDB_Episode` CHANGE COLUMN `Filename` `Filename` text character set utf8 NOT NULL ;"),
            new DatabaseCommand(10, 25,
                "ALTER TABLE `TvDB_Episode` CHANGE COLUMN `EpisodeName` `EpisodeName` text character set utf8 NOT NULL ;"),
            new DatabaseCommand(10, 26,
                "ALTER TABLE `TvDB_Series` CHANGE COLUMN `SeriesName` `SeriesName` text character set utf8 NOT NULL ;"),
            new DatabaseCommand(10, 27,
                "ALTER TABLE `DuplicateFile` CHANGE COLUMN `FilePathFile2` `FilePathFile2` text character set utf8 NOT NULL ;"),
            new DatabaseCommand(10, 28,
                "ALTER TABLE `DuplicateFile` CHANGE COLUMN `FilePathFile2` `FilePathFile2` text character set utf8 NOT NULL ;"),
            new DatabaseCommand(11, 1, "ALTER TABLE `ImportFolder` ADD `IsWatched` int NULL ;"),
            new DatabaseCommand(11, 2, "UPDATE ImportFolder SET IsWatched = 1 ;"),
            new DatabaseCommand(11, 3,
                "ALTER TABLE `ImportFolder` CHANGE COLUMN `IsWatched` `IsWatched` int NOT NULL ;"),
            new DatabaseCommand(12, 1,
                "CREATE TABLE CrossRef_AniDB_MAL( CrossRef_AniDB_MALID INT NOT NULL AUTO_INCREMENT, AnimeID int NOT NULL, MALID int NOT NULL, MALTitle text, CrossRefSource int NOT NULL, PRIMARY KEY (`CrossRef_AniDB_MALID`) ) ; "),
            new DatabaseCommand(12, 2,
                "ALTER TABLE `CrossRef_AniDB_MAL` ADD UNIQUE INDEX `UIX_CrossRef_AniDB_MAL_AnimeID` (`AnimeID` ASC) ;"),
            new DatabaseCommand(12, 3,
                "ALTER TABLE `CrossRef_AniDB_MAL` ADD UNIQUE INDEX `UIX_CrossRef_AniDB_MAL_MALID` (`MALID` ASC) ;"),
            new DatabaseCommand(13, 1, "drop table `CrossRef_AniDB_MAL`;"),
            new DatabaseCommand(13, 2,
                "CREATE TABLE CrossRef_AniDB_MAL( CrossRef_AniDB_MALID INT NOT NULL AUTO_INCREMENT, AnimeID int NOT NULL, MALID int NOT NULL, MALTitle text, StartEpisodeType int NOT NULL, StartEpisodeNumber int NOT NULL, CrossRefSource int NOT NULL, PRIMARY KEY (`CrossRef_AniDB_MALID`) ) ; "),
            new DatabaseCommand(13, 3,
                "ALTER TABLE `CrossRef_AniDB_MAL` ADD UNIQUE INDEX `UIX_CrossRef_AniDB_MAL_AnimeID` (`AnimeID` ASC) ;"),
            new DatabaseCommand(13, 4,
                "ALTER TABLE `CrossRef_AniDB_MAL` ADD UNIQUE INDEX `UIX_CrossRef_AniDB_MAL_Anime` (`MALID` ASC, `AnimeID` ASC, `StartEpisodeType` ASC, `StartEpisodeNumber` ASC) ;"),
            new DatabaseCommand(14, 1,
                "CREATE TABLE Playlist( PlaylistID INT NOT NULL AUTO_INCREMENT, PlaylistName text character set utf8, PlaylistItems text character set utf8, DefaultPlayOrder int NOT NULL, PlayWatched int NOT NULL, PlayUnwatched int NOT NULL, PRIMARY KEY (`PlaylistID`) ) ; "),
            new DatabaseCommand(15, 1, "ALTER TABLE `AnimeSeries` ADD `SeriesNameOverride` text NULL ;"),
            new DatabaseCommand(16, 1,
                "CREATE TABLE BookmarkedAnime( BookmarkedAnimeID INT NOT NULL AUTO_INCREMENT, AnimeID int NOT NULL, Priority int NOT NULL, Notes text character set utf8, Downloading int NOT NULL, PRIMARY KEY (`BookmarkedAnimeID`) ) ; "),
            new DatabaseCommand(16, 2,
                "ALTER TABLE `BookmarkedAnime` ADD UNIQUE INDEX `UIX_BookmarkedAnime_AnimeID` (`AnimeID` ASC) ;"),
            new DatabaseCommand(17, 1, "ALTER TABLE `VideoLocal` ADD `DateTimeCreated` datetime NULL ;"),
            new DatabaseCommand(17, 2, "UPDATE VideoLocal SET DateTimeCreated = DateTimeUpdated ;"),
            new DatabaseCommand(17, 3,
                "ALTER TABLE `VideoLocal` CHANGE COLUMN `DateTimeCreated` `DateTimeCreated` datetime NOT NULL ;"),
            new DatabaseCommand(18, 1,
                "CREATE TABLE `CrossRef_AniDB_TvDB_Episode` ( `CrossRef_AniDB_TvDB_EpisodeID` INT NOT NULL AUTO_INCREMENT, `AnimeID` int NOT NULL, `AniDBEpisodeID` int NOT NULL, `TvDBEpisodeID` int NOT NULL, PRIMARY KEY (`CrossRef_AniDB_TvDB_EpisodeID`) ) ; "),
            new DatabaseCommand(18, 2,
                "ALTER TABLE `CrossRef_AniDB_TvDB_Episode` ADD UNIQUE INDEX `UIX_CrossRef_AniDB_TvDB_Episode_AniDBEpisodeID` (`AniDBEpisodeID` ASC) ;"),
            new DatabaseCommand(19, 1,
                "CREATE TABLE `AniDB_MylistStats` ( `AniDB_MylistStatsID` INT NOT NULL AUTO_INCREMENT, `Animes` int NOT NULL, `Episodes` int NOT NULL, `Files` int NOT NULL, `SizeOfFiles` bigint NOT NULL, `AddedAnimes` int NOT NULL, `AddedEpisodes` int NOT NULL, `AddedFiles` int NOT NULL, `AddedGroups` int NOT NULL, `LeechPct` int NOT NULL, `GloryPct` int NOT NULL, `ViewedPct` int NOT NULL, `MylistPct` int NOT NULL, `ViewedMylistPct` int NOT NULL, `EpisodesViewed` int NOT NULL, `Votes` int NOT NULL, `Reviews` int NOT NULL, `ViewiedLength` int NOT NULL, PRIMARY KEY (`AniDB_MylistStatsID`) ) ; "),
            new DatabaseCommand(20, 1,
                "CREATE TABLE `FileFfdshowPreset` ( `FileFfdshowPresetID` INT NOT NULL AUTO_INCREMENT, `Hash` varchar(50) NOT NULL, `FileSize` bigint NOT NULL, `Preset` text character set utf8, PRIMARY KEY (`FileFfdshowPresetID`) ) ; "),
            new DatabaseCommand(20, 2,
                "ALTER TABLE `FileFfdshowPreset` ADD UNIQUE INDEX `UIX_FileFfdshowPreset_Hash` (`Hash` ASC, `FileSize` ASC) ;"),
            new DatabaseCommand(21, 1, "ALTER TABLE `AniDB_Anime` ADD `DisableExternalLinksFlag` int NULL ;"),
            new DatabaseCommand(21, 2, "UPDATE AniDB_Anime SET DisableExternalLinksFlag = 0 ;"),
            new DatabaseCommand(21, 3,
                "ALTER TABLE `AniDB_Anime` CHANGE COLUMN `DisableExternalLinksFlag` `DisableExternalLinksFlag` int NOT NULL ;"),
            new DatabaseCommand(22, 1, "ALTER TABLE `AniDB_File` ADD `FileVersion` int NULL ;"),
            new DatabaseCommand(22, 2, "UPDATE AniDB_File SET FileVersion = 1 ;"),
            new DatabaseCommand(22, 3,
                "ALTER TABLE `AniDB_File` CHANGE COLUMN `FileVersion` `FileVersion` int NOT NULL ;"),
            new DatabaseCommand(23, 1,
                "CREATE TABLE RenameScript( RenameScriptID INT NOT NULL AUTO_INCREMENT, ScriptName text character set utf8, Script text character set utf8, IsEnabledOnImport int NOT NULL, PRIMARY KEY (`RenameScriptID`) ) ; "),
            new DatabaseCommand(24, 1, "ALTER TABLE `AniDB_File` ADD `IsCensored` int NULL ;"),
            new DatabaseCommand(24, 2, "ALTER TABLE `AniDB_File` ADD `IsDeprecated` int NULL ;"),
            new DatabaseCommand(24, 3, "ALTER TABLE `AniDB_File` ADD `InternalVersion` int NULL ;"),
            new DatabaseCommand(24, 4, "UPDATE AniDB_File SET IsCensored = 0 ;"),
            new DatabaseCommand(24, 5, "UPDATE AniDB_File SET IsDeprecated = 0 ;"),
            new DatabaseCommand(24, 6, "UPDATE AniDB_File SET InternalVersion = 1 ;"),
            new DatabaseCommand(24, 7,
                "ALTER TABLE `AniDB_File` CHANGE COLUMN `IsCensored` `IsCensored` int NOT NULL ;"),
            new DatabaseCommand(24, 8,
                "ALTER TABLE `AniDB_File` CHANGE COLUMN `IsDeprecated` `IsDeprecated` int NOT NULL ;"),
            new DatabaseCommand(24, 9,
                "ALTER TABLE `AniDB_File` CHANGE COLUMN `InternalVersion` `InternalVersion` int NOT NULL ;"),
            new DatabaseCommand(25, 1, "ALTER TABLE `VideoLocal` ADD `IsVariation` int NULL ;"),
            new DatabaseCommand(25, 2, "UPDATE VideoLocal SET IsVariation = 0 ;"),
            new DatabaseCommand(25, 3,
                "ALTER TABLE `VideoLocal` CHANGE COLUMN `IsVariation` `IsVariation` int NOT NULL ;"),
            new DatabaseCommand(26, 1,
                "CREATE TABLE AniDB_Recommendation( AniDB_RecommendationID INT NOT NULL AUTO_INCREMENT, AnimeID int NOT NULL, UserID int NOT NULL, RecommendationType int NOT NULL, RecommendationText text character set utf8, PRIMARY KEY (`AniDB_RecommendationID`) ) ; "),
            new DatabaseCommand(26, 2,
                "ALTER TABLE `AniDB_Recommendation` ADD UNIQUE INDEX `UIX_AniDB_Recommendation` (`AnimeID` ASC, `UserID` ASC) ;"),
            new DatabaseCommand(27, 1,
                "update CrossRef_File_Episode SET CrossRefSource=1 WHERE Hash IN (Select Hash from AniDB_File) AND CrossRefSource=2 ;"),
            new DatabaseCommand(28, 1,
                "CREATE TABLE LogMessage( LogMessageID INT NOT NULL AUTO_INCREMENT, LogType text character set utf8, LogContent text character set utf8, LogDate datetime NOT NULL, PRIMARY KEY (`LogMessageID`) ) ; "),
            new DatabaseCommand(29, 1,
                "CREATE TABLE CrossRef_AniDB_TvDBV2( CrossRef_AniDB_TvDBV2ID INT NOT NULL AUTO_INCREMENT, AnimeID int NOT NULL, AniDBStartEpisodeType int NOT NULL, AniDBStartEpisodeNumber int NOT NULL, TvDBID int NOT NULL, TvDBSeasonNumber int NOT NULL, TvDBStartEpisodeNumber int NOT NULL, TvDBTitle text character set utf8, CrossRefSource int NOT NULL, PRIMARY KEY (`CrossRef_AniDB_TvDBV2ID`) ) ; "),
            new DatabaseCommand(29, 2,
                "ALTER TABLE `CrossRef_AniDB_TvDBV2` ADD UNIQUE INDEX `UIX_CrossRef_AniDB_TvDBV2` (`AnimeID` ASC, `TvDBID` ASC, `TvDBSeasonNumber` ASC, `TvDBStartEpisodeNumber` ASC, `AniDBStartEpisodeType` ASC, `AniDBStartEpisodeNumber` ASC) ;"),
            new DatabaseCommand(29, 3, DatabaseFixes.MigrateTvDBLinks_V1_to_V2),
            new DatabaseCommand(30, 1, "ALTER TABLE `GroupFilter` ADD `Locked` int NULL ;"),
            new DatabaseCommand(31, 1, "ALTER TABLE VideoInfo ADD FullInfo varchar(10000) NULL"),
            new DatabaseCommand(32, 1,
                "CREATE TABLE CrossRef_AniDB_TraktV2( CrossRef_AniDB_TraktV2ID INT NOT NULL AUTO_INCREMENT, AnimeID int NOT NULL, AniDBStartEpisodeType int NOT NULL, AniDBStartEpisodeNumber int NOT NULL, TraktID varchar(100) character set utf8, TraktSeasonNumber int NOT NULL, TraktStartEpisodeNumber int NOT NULL, TraktTitle text character set utf8, CrossRefSource int NOT NULL, PRIMARY KEY (`CrossRef_AniDB_TraktV2ID`) ) ; "),
            new DatabaseCommand(32, 2,
                "ALTER TABLE `CrossRef_AniDB_TraktV2` ADD UNIQUE INDEX `UIX_CrossRef_AniDB_TraktV2` (`AnimeID` ASC, `TraktSeasonNumber` ASC, `TraktStartEpisodeNumber` ASC, `AniDBStartEpisodeType` ASC, `AniDBStartEpisodeNumber` ASC) ;"),
            new DatabaseCommand(32, 3, DatabaseFixes.MigrateTraktLinks_V1_to_V2),
            new DatabaseCommand(33, 1,
                "CREATE TABLE `CrossRef_AniDB_Trakt_Episode` ( `CrossRef_AniDB_Trakt_EpisodeID` INT NOT NULL AUTO_INCREMENT, `AnimeID` int NOT NULL, `AniDBEpisodeID` int NOT NULL, `TraktID` varchar(100) character set utf8, `Season` int NOT NULL, `EpisodeNumber` int NOT NULL, PRIMARY KEY (`CrossRef_AniDB_Trakt_EpisodeID`) ) ; "),
            new DatabaseCommand(33, 2,
                "ALTER TABLE `CrossRef_AniDB_Trakt_Episode` ADD UNIQUE INDEX `UIX_CrossRef_AniDB_Trakt_Episode_AniDBEpisodeID` (`AniDBEpisodeID` ASC) ;"),
            new DatabaseCommand(34, 1, DatabaseFixes.RemoveOldMovieDBImageRecords),
            new DatabaseCommand(35, 1,
                "CREATE TABLE `CustomTag` ( `CustomTagID` INT NOT NULL AUTO_INCREMENT, `TagName` text character set utf8, `TagDescription` text character set utf8, PRIMARY KEY (`CustomTagID`) ) ; "),
            new DatabaseCommand(35, 2,
                "CREATE TABLE `CrossRef_CustomTag` ( `CrossRef_CustomTagID` INT NOT NULL AUTO_INCREMENT, `CustomTagID` int NOT NULL, `CrossRefID` int NOT NULL, `CrossRefType` int NOT NULL, PRIMARY KEY (`CrossRef_CustomTagID`) ) ; "),
            new DatabaseCommand(36, 1,
                $"ALTER DATABASE {ServerSettings.Instance.Database.Schema} CHARACTER SET = utf8mb4 COLLATE = utf8mb4_unicode_ci;"),
            new DatabaseCommand(37, 1,
                "ALTER TABLE `CrossRef_AniDB_MAL` DROP INDEX `UIX_CrossRef_AniDB_MAL_AnimeID` ;"),
            new DatabaseCommand(37, 2, "ALTER TABLE `CrossRef_AniDB_MAL` DROP INDEX `UIX_CrossRef_AniDB_MAL_Anime` ;"),
            new DatabaseCommand(37, 3,
                "ALTER TABLE `CrossRef_AniDB_MAL` ADD UNIQUE INDEX `UIX_CrossRef_AniDB_MAL_MALID` (`MALID` ASC) ;"),
            new DatabaseCommand(37, 4,
                "ALTER TABLE `CrossRef_AniDB_MAL` ADD UNIQUE INDEX `UIX_CrossRef_AniDB_MAL_Anime` (`AnimeID` ASC, `StartEpisodeType` ASC, `StartEpisodeNumber` ASC) ;"),
            new DatabaseCommand(38, 1, "ALTER TABLE AniDB_Anime_Tag ADD Weight int NULL"),
            new DatabaseCommand(39, 1, DatabaseFixes.PopulateTagWeight),
            new DatabaseCommand(40, 1, "ALTER TABLE Trakt_Episode ADD TraktID int NULL"),
            new DatabaseCommand(41, 1, DatabaseFixes.FixHashes),
            new DatabaseCommand(42, 1, "drop table `LogMessage`;"),
            new DatabaseCommand(43, 1, "ALTER TABLE AnimeSeries ADD DefaultFolder text character set utf8"),
            new DatabaseCommand(44, 1, "ALTER TABLE JMMUser ADD PlexUsers text character set utf8"),
            new DatabaseCommand(45, 1, "ALTER TABLE `GroupFilter` ADD `FilterType` int NULL ;"),
            new DatabaseCommand(45, 2, "UPDATE GroupFilter SET FilterType = 1 ;"),
            new DatabaseCommand(45, 3,
                "ALTER TABLE `GroupFilter` CHANGE COLUMN `FilterType` `FilterType` int NOT NULL ;"),
            new DatabaseCommand(45, 4, DatabaseFixes.FixContinueWatchingGroupFilter_20160406),
            new DatabaseCommand(46, 1, "ALTER TABLE `AniDB_Anime` ADD `ContractVersion` int NOT NULL DEFAULT 0"),
            new DatabaseCommand(46, 2,
                "ALTER TABLE `AniDB_Anime` ADD `ContractString` mediumtext character set utf8 NULL"),
            new DatabaseCommand(46, 3, "ALTER TABLE `AnimeGroup` ADD `ContractVersion` int NOT NULL DEFAULT 0"),
            new DatabaseCommand(46, 4,
                "ALTER TABLE `AnimeGroup` ADD `ContractString` mediumtext character set utf8 NULL"),
            new DatabaseCommand(46, 5,
                "ALTER TABLE `AnimeGroup_User` ADD `PlexContractVersion` int NOT NULL DEFAULT 0"),
            new DatabaseCommand(46, 6,
                "ALTER TABLE `AnimeGroup_User` ADD `PlexContractString` mediumtext character set utf8 NULL"),
            new DatabaseCommand(46, 7,
                "ALTER TABLE `AnimeGroup_User` ADD `KodiContractVersion` int NOT NULL DEFAULT 0"),
            new DatabaseCommand(46, 8,
                "ALTER TABLE `AnimeGroup_User` ADD `KodiContractString` mediumtext character set utf8 NULL"),
            new DatabaseCommand(46, 9, "ALTER TABLE `AnimeSeries` ADD `ContractVersion` int NOT NULL DEFAULT 0"),
            new DatabaseCommand(46, 10,
                "ALTER TABLE `AnimeSeries` ADD `ContractString` mediumtext character set utf8 NULL"),
            new DatabaseCommand(46, 11,
                "ALTER TABLE `AnimeSeries_User` ADD `PlexContractVersion` int NOT NULL DEFAULT 0"),
            new DatabaseCommand(46, 12,
                "ALTER TABLE `AnimeSeries_User` ADD `PlexContractString` mediumtext character set utf8 NULL"),
            new DatabaseCommand(46, 13,
                "ALTER TABLE `AnimeSeries_User` ADD `KodiContractVersion` int NOT NULL DEFAULT 0"),
            new DatabaseCommand(46, 14,
                "ALTER TABLE `AnimeSeries_User` ADD `KodiContractString` mediumtext character set utf8 NULL"),
            new DatabaseCommand(46, 15, "ALTER TABLE `GroupFilter` ADD `GroupsIdsVersion` int NOT NULL DEFAULT 0"),
            new DatabaseCommand(46, 16,
                "ALTER TABLE `GroupFilter` ADD `GroupsIdsString` mediumtext character set utf8 NULL"),
            new DatabaseCommand(46, 17, "ALTER TABLE `AnimeEpisode_User` ADD `ContractVersion` int NOT NULL DEFAULT 0"),
            new DatabaseCommand(46, 18,
                "ALTER TABLE `AnimeEpisode_User` ADD `ContractString` mediumtext character set utf8 NULL"),
            new DatabaseCommand(47, 1, "ALTER TABLE `AnimeEpisode` ADD `PlexContractVersion` int NOT NULL DEFAULT 0"),
            new DatabaseCommand(47, 2,
                "ALTER TABLE `AnimeEpisode` ADD `PlexContractString` mediumtext character set utf8 NULL"),
            new DatabaseCommand(47, 3, "ALTER TABLE `VideoLocal` ADD `MediaVersion` int NOT NULL DEFAULT 0"),
            new DatabaseCommand(47, 4, "ALTER TABLE `VideoLocal` ADD `MediaString` mediumtext character set utf8 NULL"),
            new DatabaseCommand(48, 1, "ALTER TABLE `AnimeSeries_User` DROP COLUMN `KodiContractVersion`"),
            new DatabaseCommand(48, 2, "ALTER TABLE `AnimeSeries_User` DROP COLUMN `KodiContractString`"),
            new DatabaseCommand(48, 3, "ALTER TABLE `AnimeGroup_User` DROP COLUMN `KodiContractVersion`"),
            new DatabaseCommand(48, 4, "ALTER TABLE `AnimeGroup_User` DROP COLUMN `KodiContractString`"),
            new DatabaseCommand(49, 1, "ALTER TABLE AnimeSeries ADD LatestEpisodeAirDate datetime NULL"),
            new DatabaseCommand(49, 2, "ALTER TABLE AnimeGroup ADD LatestEpisodeAirDate datetime NULL"),
            new DatabaseCommand(50, 1, "ALTER TABLE `GroupFilter` ADD `GroupConditionsVersion` int NOT NULL DEFAULT 0"),
            new DatabaseCommand(50, 2,
                "ALTER TABLE `GroupFilter` ADD `GroupConditions` mediumtext character set utf8 NULL"),
            new DatabaseCommand(50, 3, "ALTER TABLE `GroupFilter` ADD `ParentGroupFilterID` int NULL"),
            new DatabaseCommand(50, 4, "ALTER TABLE `GroupFilter` ADD `InvisibleInClients` int NOT NULL DEFAULT 0"),
            new DatabaseCommand(50, 5, "ALTER TABLE `GroupFilter` ADD `SeriesIdsVersion` int NOT NULL DEFAULT 0"),
            new DatabaseCommand(50, 6,
                "ALTER TABLE `GroupFilter` ADD `SeriesIdsString` mediumtext character set utf8 NULL"),
            new DatabaseCommand(51, 1, "ALTER TABLE `AniDB_Anime` ADD `ContractBlob` mediumblob NULL"),
            new DatabaseCommand(51, 2, "ALTER TABLE `AniDB_Anime` ADD `ContractSize` int NOT NULL DEFAULT 0"),
            new DatabaseCommand(51, 3, "ALTER TABLE `AniDB_Anime` DROP COLUMN `ContractString`"),
            new DatabaseCommand(51, 4, "ALTER TABLE `VideoLocal` ADD `MediaBlob` mediumblob NULL"),
            new DatabaseCommand(51, 5, "ALTER TABLE `VideoLocal` ADD `MediaSize` int NOT NULL DEFAULT 0"),
            new DatabaseCommand(51, 6, "ALTER TABLE `VideoLocal` DROP COLUMN `MediaString`"),
            new DatabaseCommand(51, 7, "ALTER TABLE `AnimeEpisode` ADD `PlexContractBlob` mediumblob NULL"),
            new DatabaseCommand(51, 8, "ALTER TABLE `AnimeEpisode` ADD `PlexContractSize` int NOT NULL DEFAULT 0"),
            new DatabaseCommand(51, 9, "ALTER TABLE `AnimeEpisode` DROP COLUMN `PlexContractString`"),
            new DatabaseCommand(51, 10, "ALTER TABLE `AnimeEpisode_User` ADD `ContractBlob` mediumblob NULL"),
            new DatabaseCommand(51, 11, "ALTER TABLE `AnimeEpisode_User` ADD `ContractSize` int NOT NULL DEFAULT 0"),
            new DatabaseCommand(51, 12, "ALTER TABLE `AnimeEpisode_User` DROP COLUMN `ContractString`"),
            new DatabaseCommand(51, 13, "ALTER TABLE `AnimeSeries` ADD `ContractBlob` mediumblob NULL"),
            new DatabaseCommand(51, 14, "ALTER TABLE `AnimeSeries` ADD `ContractSize` int NOT NULL DEFAULT 0"),
            new DatabaseCommand(51, 15, "ALTER TABLE `AnimeSeries` DROP COLUMN `ContractString`"),
            new DatabaseCommand(51, 16, "ALTER TABLE `AnimeSeries_User` ADD `PlexContractBlob` mediumblob NULL"),
            new DatabaseCommand(51, 17, "ALTER TABLE `AnimeSeries_User` ADD `PlexContractSize` int NOT NULL DEFAULT 0"),
            new DatabaseCommand(51, 18, "ALTER TABLE `AnimeSeries_User` DROP COLUMN `PlexContractString`"),
            new DatabaseCommand(51, 19, "ALTER TABLE `AnimeGroup_User` ADD `PlexContractBlob` mediumblob NULL"),
            new DatabaseCommand(51, 20, "ALTER TABLE `AnimeGroup_User` ADD `PlexContractSize` int NOT NULL DEFAULT 0"),
            new DatabaseCommand(51, 21, "ALTER TABLE `AnimeGroup_User` DROP COLUMN `PlexContractString`"),
            new DatabaseCommand(51, 22, "ALTER TABLE `AnimeGroup` ADD `ContractBlob` mediumblob NULL"),
            new DatabaseCommand(51, 23, "ALTER TABLE `AnimeGroup` ADD `ContractSize` int NOT NULL DEFAULT 0"),
            new DatabaseCommand(51, 24, "ALTER TABLE `AnimeGroup` DROP COLUMN `ContractString`"),
            new DatabaseCommand(52, 1, "ALTER TABLE `AniDB_Anime` DROP COLUMN `AllCategories`"),
            new DatabaseCommand(53, 1, DatabaseFixes.DeleteSerieUsersWithoutSeries),
            new DatabaseCommand(54, 1,
                "CREATE TABLE `VideoLocal_Place` ( `VideoLocal_Place_ID` INT NOT NULL AUTO_INCREMENT, `VideoLocalID` int NOT NULL, `FilePath` text character set utf8 NOT NULL, `ImportFolderID` int NOT NULL, `ImportFolderType` int NOT NULL, PRIMARY KEY (`VideoLocal_Place_ID`) ) ; "),
            new DatabaseCommand(54, 2, "ALTER TABLE `VideoLocal` ADD `FileName` text character set utf8 NOT NULL"),
            new DatabaseCommand(54, 3, "ALTER TABLE `VideoLocal` ADD `VideoCodec` varchar(100) NOT NULL DEFAULT ''"),
            new DatabaseCommand(54, 4, "ALTER TABLE `VideoLocal` ADD `VideoBitrate` varchar(100) NOT NULL DEFAULT ''"),
            new DatabaseCommand(54, 5, "ALTER TABLE `VideoLocal` ADD `VideoBitDepth` varchar(100) NOT NULL DEFAULT ''"),
            new DatabaseCommand(54, 6,
                "ALTER TABLE `VideoLocal` ADD `VideoFrameRate` varchar(100) NOT NULL DEFAULT ''"),
            new DatabaseCommand(54, 7,
                "ALTER TABLE `VideoLocal` ADD `VideoResolution` varchar(100) NOT NULL DEFAULT ''"),
            new DatabaseCommand(54, 8, "ALTER TABLE `VideoLocal` ADD `AudioCodec` varchar(100) NOT NULL DEFAULT ''"),
            new DatabaseCommand(54, 9, "ALTER TABLE `VideoLocal` ADD `AudioBitrate` varchar(100) NOT NULL DEFAULT ''"),
            new DatabaseCommand(54, 10, "ALTER TABLE `VideoLocal` ADD `Duration` bigint NOT NULL DEFAULT 0"),
            new DatabaseCommand(54, 11,
                "INSERT INTO `VideoLocal_Place` (`VideoLocalID`, `FilePath`, `ImportFolderID`, `ImportFolderType`) SELECT `VideoLocalID`, `FilePath`, `ImportFolderID`, 1 as `ImportFolderType` FROM `VideoLocal`"),
            new DatabaseCommand(54, 12, "ALTER TABLE `VideoLocal` DROP COLUMN `FilePath`"),
            new DatabaseCommand(54, 13, "ALTER TABLE `VideoLocal` DROP COLUMN `ImportFolderID`"),
            new DatabaseCommand(54, 14,
                "CREATE TABLE `CloudAccount` ( `CloudID` INT NOT NULL AUTO_INCREMENT,  `ConnectionString` text character set utf8 NOT NULL,  `Provider` varchar(100) NOT NULL DEFAULT '', `Name` varchar(256) NOT NULL DEFAULT '',  PRIMARY KEY (`CloudID`) ) ; "),
            new DatabaseCommand(54, 15, "ALTER TABLE `ImportFolder` ADD `CloudID` int NULL"),
            new DatabaseCommand(54, 16, "ALTER TABLE `VideoLocal_User` MODIFY COLUMN `WatchedDate` datetime NULL"),
            new DatabaseCommand(54, 17, "ALTER TABLE `VideoLocal_User` ADD `ResumePosition` bigint NOT NULL DEFAULT 0"),
            new DatabaseCommand(54, 17, "ALTER TABLE `VideoLocal_User` ADD `ResumePosition` bigint NOT NULL DEFAULT 0"),
            new DatabaseCommand(54, 18,
                "UPDATE `VideoLocal` INNER JOIN `VideoInfo` ON `VideoLocal`.`Hash`=`VideoInfo`.`Hash` SET `VideoLocal`.`FileName`=`VideoInfo`.`FileName`,`VideoLocal`.`VideoCodec`=`VideoInfo`.`VideoCodec`,`VideoLocal`.`VideoBitrate`=`VideoInfo`.`VideoBitrate`,`VideoLocal`.`VideoBitDepth`=`VideoInfo`.`VideoBitDepth`,`VideoLocal`.`VideoFrameRate`=`VideoInfo`.`VideoFrameRate`,`VideoLocal`.`VideoResolution`=`VideoInfo`.`VideoResolution`,`VideoLocal`.`AudioCodec`=`VideoInfo`.`AudioCodec`,`VideoLocal`.`AudioBitrate`=`VideoInfo`.`AudioBitrate`,`VideoLocal`.`Duration`=`VideoInfo`.`Duration`"),
            new DatabaseCommand(54, 19, "DROP TABLE `VideoInfo`"),
            new DatabaseCommand(55, 1, "ALTER TABLE `VideoLocal` DROP INDEX `UIX_VideoLocal_Hash` ;"),
            new DatabaseCommand(55, 2, "ALTER TABLE `VideoLocal` ADD INDEX `IX_VideoLocal_Hash` (`Hash` ASC) ;"),
            new DatabaseCommand(56, 1,
                "CREATE TABLE `AuthTokens` ( `AuthID` INT NOT NULL AUTO_INCREMENT, `UserID` int NOT NULL, `DeviceName` text character set utf8, `Token` text character set utf8, PRIMARY KEY (`AuthID`) ) ; "),
            new DatabaseCommand(57, 1,
                "CREATE TABLE `Scan` ( `ScanID` INT NOT NULL AUTO_INCREMENT, `CreationTime` datetime NOT NULL, `ImportFolders` text character set utf8, `Status` int NOT NULL, PRIMARY KEY (`ScanID`) ) ; "),
            new DatabaseCommand(57, 2,
                "CREATE TABLE `ScanFile` ( `ScanFileID` INT NOT NULL AUTO_INCREMENT, `ScanID` int NOT NULL, `ImportFolderID` int NOT NULL, `VideoLocal_Place_ID` int NOT NULL, `FullName` text character set utf8, `FileSize` bigint NOT NULL, `Status` int NOT NULL, `CheckDate` datetime NULL, `Hash` text character set utf8, `HashResult` text character set utf8 NULL, PRIMARY KEY (`ScanFileID`) ) ; "),
            new DatabaseCommand(57, 3,
                "ALTER TABLE `ScanFile` ADD  INDEX `UIX_ScanFileStatus` (`ScanID` ASC, `Status` ASC, `CheckDate` ASC) ;"),
            new DatabaseCommand(58, 1, DatabaseFixes.FixEmptyVideoInfos),
            new DatabaseCommand(59, 1,
                "ALTER TABLE `GroupFilter` ADD INDEX `IX_groupfilter_GroupFilterName` (`GroupFilterName`(250));"),
            new DatabaseCommand(60, 1, DatabaseFixes.FixTagsWithInclude),
            new DatabaseCommand(61, 1, DatabaseFixes.MakeYearsApplyToSeries),
            new DatabaseCommand(62, 1, "ALTER TABLE JMMUser ADD PlexToken text character set utf8"),
            new DatabaseCommand(63, 1, "ALTER TABLE AniDB_File ADD IsChaptered INT NOT NULL DEFAULT -1"),
            new DatabaseCommand(64, 1, "ALTER TABLE `CrossRef_File_Episode` ADD INDEX `IX_Xref_Epid` (`episodeid` ASC) ;"),
            new DatabaseCommand(64, 2, "ALTER TABLE `CrossRef_Subtitles_AniDB_File` ADD INDEX `IX_Xref_Sub_AniDBFile` (`fileid` ASC) ;"),
            new DatabaseCommand(64, 3, "ALTER TABLE `CrossRef_Languages_AniDB_File` ADD INDEX `IX_Xref_Epid` (`fileid` ASC) ;"),
            new DatabaseCommand(65, 1, "ALTER TABLE RenameScript ADD RenamerType varchar(255) character set utf8 NOT NULL DEFAULT 'Legacy'"),
            new DatabaseCommand(65, 2, "ALTER TABLE RenameScript ADD ExtraData TEXT character set utf8"),
            new DatabaseCommand(66, 1,
                "ALTER TABLE `AniDB_Anime_Character` ADD INDEX `IX_AniDB_Anime_Character_CharID` (`CharID` ASC) ;"),
            new DatabaseCommand(67, 1, "ALTER TABLE `TvDB_Episode` ADD `Rating` int NULL"),
            new DatabaseCommand(67, 2, "ALTER TABLE `TvDB_Episode` ADD `AirDate` datetime NULL"),
            new DatabaseCommand(67, 3, "ALTER TABLE `TvDB_Episode` DROP COLUMN `FirstAired`"),
            new DatabaseCommand(67, 4, DatabaseFixes.UpdateAllTvDBSeries),
            new DatabaseCommand(68, 1, "ALTER TABLE `AnimeSeries` ADD `AirsOn` TEXT character set utf8 NULL"),
            new DatabaseCommand(69, 1, "DROP TABLE `Trakt_ImageFanart`"),
            new DatabaseCommand(69, 2, "DROP TABLE `Trakt_ImagePoster`"),
            new DatabaseCommand(70, 1, "CREATE TABLE `AnimeCharacter` ( `CharacterID` INT NOT NULL AUTO_INCREMENT, `AniDBID` INT NOT NULL, `Name` text character set utf8 NOT NULL, `AlternateName` text character set utf8 NULL, `Description` text character set utf8 NULL, `ImagePath` text character set utf8 NULL, PRIMARY KEY (`CharacterID`) )"),
            new DatabaseCommand(70, 2, "CREATE TABLE `AnimeStaff` ( `StaffID` INT NOT NULL AUTO_INCREMENT, `AniDBID` INT NOT NULL, `Name` text character set utf8 NOT NULL, `AlternateName` text character set utf8 NULL, `Description` text character set utf8 NULL, `ImagePath` text character set utf8 NULL, PRIMARY KEY (`StaffID`) )"),
            new DatabaseCommand(70, 3, "CREATE TABLE `CrossRef_Anime_Staff` ( `CrossRef_Anime_StaffID` INT NOT NULL AUTO_INCREMENT, `AniDB_AnimeID` INT NOT NULL, `StaffID` INT NOT NULL, `Role` text character set utf8 NULL, `RoleID` INT, `RoleType` INT NOT NULL, `Language` text character set utf8 NOT NULL, PRIMARY KEY (`CrossRef_Anime_StaffID`) )"),
            new DatabaseCommand(70, 4, DatabaseFixes.PopulateCharactersAndStaff),
            new DatabaseCommand(71, 1, "ALTER TABLE `MovieDB_Movie` ADD `Rating` INT NOT NULL DEFAULT 0"),
            new DatabaseCommand(71, 2, "ALTER TABLE `TvDB_Series` ADD `Rating` INT NULL"),
            new DatabaseCommand(72, 1, "ALTER TABLE `AniDB_Episode` ADD `Description` text character set utf8 NOT NULL"),
            new DatabaseCommand(72, 2, DatabaseFixes.FixCharactersWithGrave),
            new DatabaseCommand(73, 1, DatabaseFixes.RefreshAniDBInfoFromXML),
            new DatabaseCommand(74, 1, DatabaseFixes.MakeTagsApplyToSeries),
            new DatabaseCommand(74, 2, Importer.UpdateAllStats),
            new DatabaseCommand(75, 1, DatabaseFixes.RemoveBasePathsFromStaffAndCharacters),
            new DatabaseCommand(76, 1, "CREATE TABLE `AniDB_AnimeUpdate` ( `AniDB_AnimeUpdateID` INT NOT NULL AUTO_INCREMENT, `AnimeID` INT NOT NULL, `UpdatedAt` datetime NOT NULL, PRIMARY KEY (`AniDB_AnimeUpdateID`) );"),
            new DatabaseCommand(76, 2, "ALTER TABLE `AniDB_AnimeUpdate` ADD INDEX `UIX_AniDB_AnimeUpdate` (`AnimeID` ASC) ;"),
            new DatabaseCommand(76, 3, DatabaseFixes.MigrateAniDB_AnimeUpdates),
            new DatabaseCommand(77, 1, DatabaseFixes.RemoveBasePathsFromStaffAndCharacters),
            new DatabaseCommand(78, 1, DatabaseFixes.FixDuplicateTagFiltersAndUpdateSeasons),
            new DatabaseCommand(79, 1, DatabaseFixes.RecalculateYears),
            new DatabaseCommand(80, 1, "ALTER TABLE `CrossRef_AniDB_MAL` DROP INDEX `UIX_CrossRef_AniDB_MAL_Anime` ;"),
            new DatabaseCommand(80, 2, "ALTER TABLE `AniDB_Anime` ADD ( `Site_JP` text character set utf8 null, `Site_EN` text character set utf8 null, `Wikipedia_ID` text character set utf8 null, `WikipediaJP_ID` text character set utf8 null, `SyoboiID` INT NULL, `AnisonID` INT NULL, `CrunchyrollID` text character set utf8 null );"),
            new DatabaseCommand(80, 3, DatabaseFixes.PopulateResourceLinks),
            new DatabaseCommand(81, 1, "ALTER TABLE `VideoLocal` ADD `MyListID` INT NOT NULL DEFAULT 0"),
            new DatabaseCommand(81, 2, DatabaseFixes.PopulateMyListIDs),
            new DatabaseCommand(82, 1, MySQLFixUTF8),
            new DatabaseCommand(83, 1, "ALTER TABLE `AniDB_Episode` DROP COLUMN `EnglishName`"),
            new DatabaseCommand(83, 2, "ALTER TABLE `AniDB_Episode` DROP COLUMN `RomajiName`"),
            new DatabaseCommand(83, 3, "CREATE TABLE `AniDB_Episode_Title` ( `AniDB_Episode_TitleID` INT NOT NULL AUTO_INCREMENT, `AniDB_EpisodeID` int NOT NULL, `Language` varchar(50) character set utf8 NOT NULL, `Title` varchar(500) character set utf8 NOT NULL, PRIMARY KEY (`AniDB_Episode_TitleID`) ) ; "),
            new DatabaseCommand(83, 4, DatabaseFixes.DummyMigrationOfObsoletion),
            new DatabaseCommand(84, 1, "ALTER TABLE `CrossRef_AniDB_TvDB_Episode` DROP INDEX `UIX_CrossRef_AniDB_TvDB_Episode_AniDBEpisodeID`;"),
            new DatabaseCommand(84, 2, "RENAME TABLE `CrossRef_AniDB_TvDB_Episode` TO `CrossRef_AniDB_TvDB_Episode_Override`;"),
            new DatabaseCommand(84, 3, "ALTER TABLE `CrossRef_AniDB_TvDB_Episode_Override` DROP COLUMN `AnimeID`"),
            new DatabaseCommand(84, 4, "ALTER TABLE `CrossRef_AniDB_TvDB_Episode_Override` CHANGE `CrossRef_AniDB_TvDB_EpisodeID` `CrossRef_AniDB_TvDB_Episode_OverrideID` INT NOT NULL AUTO_INCREMENT;"),
            new DatabaseCommand(84, 5, "ALTER TABLE `CrossRef_AniDB_TvDB_Episode_Override` ADD UNIQUE INDEX `UIX_AniDB_TvDB_Episode_Override_AniDBEpisodeID_TvDBEpisodeID` (`AniDBEpisodeID` ASC, `TvDBEpisodeID` ASC);"),
            // For some reason, this was never dropped
            new DatabaseCommand(84, 6, "DROP TABLE `CrossRef_AniDB_TvDB`;"),
            new DatabaseCommand(84, 7, "CREATE TABLE `CrossRef_AniDB_TvDB` ( `CrossRef_AniDB_TvDBID` INT NOT NULL AUTO_INCREMENT, `AniDBID` int NOT NULL, `TvDBID` int NOT NULL, `CrossRefSource` INT NOT NULL, PRIMARY KEY (`CrossRef_AniDB_TvDBID`));"),
            new DatabaseCommand(84, 8, "ALTER TABLE `CrossRef_AniDB_TvDB` ADD UNIQUE INDEX `UIX_AniDB_TvDB_AniDBID_TvDBID` (`AniDBID` ASC, `TvDBID` ASC);"),
            new DatabaseCommand(84, 9, "CREATE TABLE `CrossRef_AniDB_TvDB_Episode` ( `CrossRef_AniDB_TvDB_EpisodeID` INT NOT NULL AUTO_INCREMENT, `AniDBEpisodeID` int NOT NULL, `TvDBEpisodeID` int NOT NULL, `MatchRating` INT NOT NULL, PRIMARY KEY (`CrossRef_AniDB_TvDB_EpisodeID`) );"),
            new DatabaseCommand(84, 10, "ALTER TABLE `CrossRef_AniDB_TvDB_Episode` ADD UNIQUE INDEX `UIX_CrossRef_AniDB_TvDB_Episode_AniDBID_TvDBID` ( `AniDBEpisodeID` ASC, `TvDBEpisodeID` ASC);"),
            new DatabaseCommand(84, 11, DatabaseFixes.MigrateTvDBLinks_v2_to_V3),
            // DatabaseFixes.MigrateTvDBLinks_v2_to_V3() drops the CrossRef_AniDB_TvDBV2 table. We do it after init to migrate
            new DatabaseCommand(85, 1, DatabaseFixes.FixAniDB_EpisodesWithMissingTitles),
            new DatabaseCommand(86, 1, DatabaseFixes.RegenTvDBMatches),
            new DatabaseCommand(87, 1,"ALTER TABLE `AniDB_File` CHANGE COLUMN `File_AudioCodec` `File_AudioCodec` VARCHAR(500) NOT NULL;"),
            new DatabaseCommand(88, 1,"ALTER TABLE `AnimeSeries` ADD `UpdatedAt` datetime NOT NULL DEFAULT '2000-01-01 00:00:00';"),
            new DatabaseCommand(89, 1, DatabaseFixes.MigrateAniDBToNet),
            new DatabaseCommand(90, 1, "ALTER TABLE VideoLocal DROP COLUMN VideoCodec, DROP COLUMN VideoBitrate, DROP COLUMN VideoFrameRate, DROP COLUMN VideoResolution, DROP COLUMN AudioCodec, DROP COLUMN AudioBitrate, DROP COLUMN Duration;"),
            new DatabaseCommand(91, 1, DropMALIndex),
            new DatabaseCommand(92, 1, DropAniDBUniqueIndex),
            new DatabaseCommand(93, 1, "CREATE TABLE `AniDB_Anime_Staff` ( `AniDB_Anime_StaffID` INT NOT NULL AUTO_INCREMENT, `AnimeID` int NOT NULL, `CreatorID` int NOT NULL, `CreatorType` varchar(50) NOT NULL, PRIMARY KEY (`AniDB_Anime_StaffID`) );"),
            new DatabaseCommand(93, 2, DatabaseFixes.RefreshAniDBInfoFromXML),
            new DatabaseCommand(94, 1, DatabaseFixes.EnsureNoOrphanedGroupsOrSeries),
            new DatabaseCommand(95, 1, "UPDATE VideoLocal_User SET WatchedDate = NULL WHERE WatchedDate = '1970-01-01 00:00:00';"),
            new DatabaseCommand(95, 2, "ALTER TABLE VideoLocal_User ADD WatchedCount INT NOT NULL DEFAULT 0;"),
            new DatabaseCommand(95, 3, "ALTER TABLE VideoLocal_User ADD LastUpdated datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP;"),
            new DatabaseCommand(95, 4, "UPDATE VideoLocal_User SET WatchedCount = 1, LastUpdated = WatchedDate WHERE WatchedDate IS NOT NULL;"),
            new DatabaseCommand(96, 1, "ALTER TABLE AnimeSeries_User ADD LastEpisodeUpdate datetime DEFAULT NULL;"),
            new DatabaseCommand(96, 2, DatabaseFixes.FixWatchDates),
            new DatabaseCommand(97, 1, "ALTER TABLE AnimeGroup ADD MainAniDBAnimeID INT DEFAULT NULL;"),
            new DatabaseCommand(98, 1, "ALTER TABLE AnimeEpisode_User DROP COLUMN ContractSize, DROP COLUMN ContractBlob, DROP COLUMN ContractVersion;"),
        };

        private DatabaseCommand linuxTableVersionsFix = new DatabaseCommand("RENAME TABLE versions TO Versions;");


        private List<DatabaseCommand> linuxTableFixes = new List<DatabaseCommand>
        {
            new DatabaseCommand("RENAME TABLE anidb_anime TO AniDB_Anime;"),
            new DatabaseCommand("RENAME TABLE anidb_anime_category TO AniDB_Anime_Category;"),
            new DatabaseCommand("RENAME TABLE anidb_anime_character TO AniDB_Anime_Character;"),
            new DatabaseCommand("RENAME TABLE anidb_anime_defaultimage TO AniDB_Anime_DefaultImage;"),
            new DatabaseCommand("RENAME TABLE anidb_anime_relation TO AniDB_Anime_Relation;"),
            new DatabaseCommand("RENAME TABLE anidb_anime_review TO AniDB_Anime_Review;"),
            new DatabaseCommand("RENAME TABLE anidb_anime_similar TO AniDB_Anime_Similar;"),
            new DatabaseCommand("RENAME TABLE anidb_anime_tag TO AniDB_Anime_Tag;"),
            new DatabaseCommand("RENAME TABLE anidb_anime_title TO AniDB_Anime_Title;"),
            new DatabaseCommand("RENAME TABLE anidb_category TO AniDB_Category;"),
            new DatabaseCommand("RENAME TABLE anidb_character TO AniDB_Character;"),
            new DatabaseCommand("RENAME TABLE anidb_character_seiyuu TO AniDB_Character_Seiyuu;"),
            new DatabaseCommand("RENAME TABLE anidb_episode TO AniDB_Episode;"),
            new DatabaseCommand("RENAME TABLE anidb_file TO AniDB_File;"),
            new DatabaseCommand("RENAME TABLE anidb_groupstatus TO AniDB_GroupStatus;"),
            new DatabaseCommand("RENAME TABLE anidb_myliststats TO AniDB_MylistStats;"),
            new DatabaseCommand("RENAME TABLE anidb_recommendation TO AniDB_Recommendation;"),
            new DatabaseCommand("RENAME TABLE anidb_releasegroup TO AniDB_ReleaseGroup;"),
            new DatabaseCommand("RENAME TABLE anidb_review TO AniDB_Review;"),
            new DatabaseCommand("RENAME TABLE anidb_seiyuu TO AniDB_Seiyuu;"),
            new DatabaseCommand("RENAME TABLE anidb_tag TO AniDB_Tag;"),
            new DatabaseCommand("RENAME TABLE anidb_vote TO AniDB_Vote;"),
            new DatabaseCommand("RENAME TABLE animeepisode TO AnimeEpisode;"),
            new DatabaseCommand("RENAME TABLE animeepisode_user TO AnimeEpisode_User;"),
            new DatabaseCommand("RENAME TABLE animegroup TO AnimeGroup;"),
            new DatabaseCommand("RENAME TABLE animegroup_user TO AnimeGroup_User;"),
            new DatabaseCommand("RENAME TABLE animeseries TO AnimeSeries;"),
            new DatabaseCommand("RENAME TABLE animeseries_user TO AnimeSeries_User;"),
            new DatabaseCommand("RENAME TABLE bookmarkedanime TO BookmarkedAnime;"),
            new DatabaseCommand("RENAME TABLE commandrequest TO CommandRequest;"),
            new DatabaseCommand("RENAME TABLE crossref_anidb_mal TO CrossRef_AniDB_MAL;"),
            new DatabaseCommand("RENAME TABLE crossref_anidb_other TO CrossRef_AniDB_Other;"),
            new DatabaseCommand("RENAME TABLE crossref_anidb_trakt TO CrossRef_AniDB_Trakt;"),
            new DatabaseCommand("RENAME TABLE crossref_anidb_trakt_episode TO CrossRef_AniDB_Trakt_Episode;"),
            new DatabaseCommand("RENAME TABLE crossref_anidb_traktv2 TO CrossRef_AniDB_TraktV2;"),
            new DatabaseCommand("RENAME TABLE crossref_anidb_tvdb TO CrossRef_AniDB_TvDB;"),
            new DatabaseCommand("RENAME TABLE crossref_anidb_tvdb_episode TO CrossRef_AniDB_TvDB_Episode;"),
            new DatabaseCommand("RENAME TABLE crossref_anidb_tvdbv2 TO CrossRef_AniDB_TvDBV2;"),
            new DatabaseCommand("RENAME TABLE crossref_customtag TO CrossRef_CustomTag;"),
            new DatabaseCommand("RENAME TABLE crossref_file_episode TO CrossRef_File_Episode;"),
            new DatabaseCommand("RENAME TABLE crossref_languages_anidb_file TO CrossRef_Languages_AniDB_File;"),
            new DatabaseCommand("RENAME TABLE crossref_subtitles_anidb_file TO CrossRef_Subtitles_AniDB_File;"),
            new DatabaseCommand("RENAME TABLE customtag TO CustomTag;"),
            new DatabaseCommand("RENAME TABLE duplicatefile TO DuplicateFile;"),
            new DatabaseCommand("RENAME TABLE fileffdshowpreset TO FileFfdshowPreset;"),
            new DatabaseCommand("RENAME TABLE filenamehash TO FileNameHash;"),
            new DatabaseCommand("RENAME TABLE groupfilter TO GroupFilter;"),
            new DatabaseCommand("RENAME TABLE groupfiltercondition TO GroupFilterCondition;"),
            new DatabaseCommand("RENAME TABLE ignoreanime TO IgnoreAnime;"),
            new DatabaseCommand("RENAME TABLE importfolder TO ImportFolder;"),
            new DatabaseCommand("RENAME TABLE jmmuser TO JMMUser;"),
            new DatabaseCommand("RENAME TABLE language TO Language;"),
            new DatabaseCommand("RENAME TABLE logmessage TO LogMessage;"),
            new DatabaseCommand("RENAME TABLE moviedb_fanart TO MovieDB_Fanart;"),
            new DatabaseCommand("RENAME TABLE moviedb_movie TO MovieDB_Movie;"),
            new DatabaseCommand("RENAME TABLE moviedb_poster TO MovieDB_Poster;"),
            new DatabaseCommand("RENAME TABLE playlist TO Playlist;"),
            new DatabaseCommand("RENAME TABLE renamescript TO RenameScript;"),
            new DatabaseCommand("RENAME TABLE scheduledupdate TO ScheduledUpdate;"),
            new DatabaseCommand("RENAME TABLE trakt_episode TO Trakt_Episode;"),
            new DatabaseCommand("RENAME TABLE trakt_friend TO Trakt_Friend;"),
            new DatabaseCommand("RENAME TABLE trakt_imagefanart TO Trakt_ImageFanart;"),
            new DatabaseCommand("RENAME TABLE trakt_imageposter TO Trakt_ImagePoster;"),
            new DatabaseCommand("RENAME TABLE trakt_season TO Trakt_Season;"),
            new DatabaseCommand("RENAME TABLE trakt_show TO Trakt_Show;"),
            new DatabaseCommand("RENAME TABLE tvdb_episode TO TvDB_Episode;"),
            new DatabaseCommand("RENAME TABLE tvdb_imagefanart TO TvDB_ImageFanart;"),
            new DatabaseCommand("RENAME TABLE tvdb_imageposter TO TvDB_ImagePoster;"),
            new DatabaseCommand("RENAME TABLE tvdb_imagewidebanner TO TvDB_ImageWideBanner;"),
            new DatabaseCommand("RENAME TABLE tvdb_series TO TvDB_Series;"),
            new DatabaseCommand("RENAME TABLE videoinfo TO VideoInfo;"),
            new DatabaseCommand("RENAME TABLE videolocal TO VideoLocal;"),
            new DatabaseCommand("RENAME TABLE videolocal_user TO VideoLocal_User;"),
        };


        private List<DatabaseCommand> updateVersionTable = new List<DatabaseCommand>
        {
            new DatabaseCommand("ALTER TABLE `Versions` ADD `VersionRevision` varchar(100) NULL;"),
            new DatabaseCommand("ALTER TABLE `Versions` ADD `VersionCommand` text NULL;"),
            new DatabaseCommand("ALTER TABLE `Versions` ADD `VersionProgram` varchar(100) NULL;"),
            new DatabaseCommand("ALTER TABLE `Versions` DROP INDEX `UIX_Versions_VersionType` ;"),
            new DatabaseCommand(
                "ALTER TABLE `Versions` ADD INDEX `IX_Versions_VersionType` (`VersionType`,`VersionValue`,`VersionRevision`);"),
        };

        public void BackupDatabase(string fullfilename)
        {
            fullfilename += ".sql";
            using (MySqlConnection conn = new MySqlConnection(GetConnectionString()))
            {
                using (MySqlCommand cmd = new MySqlCommand())
                {
                    using (MySqlBackup mb = new MySqlBackup(cmd))
                    {
                        cmd.CommandTimeout = 0;
                        cmd.Connection = conn;
                        conn.Open();
                        mb.ExportToFile(fullfilename);
                        conn.Close();
                    }
                }
            }
        }

        public static void DropMALIndex()
        {
            MySQL mysql = new();
            using MySqlConnection conn = new(mysql.GetConnectionString());
            conn.Open();
            string query = @"SELECT 1 FROM INFORMATION_SCHEMA.STATISTICS WHERE TABLE_NAME = 'CrossRef_AniDB_MAL' AND INDEX_NAME = 'UIX_CrossRef_AniDB_MAL_MALID';";
            MySqlCommand cmd = new(query, conn);
            object result = cmd.ExecuteScalar();
            // not exists
            if (result == null) return;
            query = "DROP INDEX `UIX_CrossRef_AniDB_MAL_MALID` ON `CrossRef_AniDB_MAL`;";
            cmd = new MySqlCommand(query, conn);
            cmd.ExecuteScalar();
        }

        public static void DropAniDBUniqueIndex()
        {
            MySQL mysql = new();
            using MySqlConnection conn = new(mysql.GetConnectionString());
            conn.Open();
            string query = @"SELECT 1 FROM INFORMATION_SCHEMA.STATISTICS WHERE TABLE_NAME = 'AniDB_File' AND INDEX_NAME = 'UIX_AniDB_File_FileID';";
            MySqlCommand cmd = new(query, conn);
            object result = cmd.ExecuteScalar();
            // not exists
            if (result == null) return;
            query = "DROP INDEX `UIX_AniDB_File_FileID` ON `AniDB_File`;";
            cmd = new MySqlCommand(query, conn);
            cmd.ExecuteScalar();
        }

        public override bool TestConnection()
        {
            try
            {
                using (MySqlConnection conn = new MySqlConnection(GetConnectionString()))
                {
                    var query = "select 1";
                    MySqlCommand cmd = new MySqlCommand(query, conn);
                    conn.Open();
                    cmd.ExecuteScalar();
                    return true;
                }
            }
            catch
            {
                // ignore
            }
            return false;
        }

        protected override Tuple<bool, string> ExecuteCommand(MySqlConnection connection, string command)
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

        protected override void Execute(MySqlConnection connection, string command)
        {
            using (MySqlCommand scommand = new MySqlCommand(command, connection))
            {
                scommand.CommandTimeout = 0;
                scommand.ExecuteNonQuery();
            }
        }

        protected override long ExecuteScalar(MySqlConnection connection, string command)
        {
            using (MySqlCommand cmd = new MySqlCommand(command, connection))
            {
                cmd.CommandTimeout = 0;
                object result = cmd.ExecuteScalar();
                return long.Parse(result.ToString());
            }
        }

        protected override ArrayList ExecuteReader(MySqlConnection connection, string command)
        {
            using (MySqlCommand cmd = new MySqlCommand(command, connection))
            {
                cmd.CommandTimeout = 0;
                using (MySqlDataReader reader = cmd.ExecuteReader())
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

        protected override void ConnectionWrapper(string connectionstring, Action<MySqlConnection> action)
        {
            using (MySqlConnection conn = new MySqlConnection(connectionstring))
            {
                conn.Open();
                action(conn);
            }
        }


        public override string GetConnectionString()
        {
            return
                $"Server={ServerSettings.Instance.Database.Hostname};Database={ServerSettings.Instance.Database.Schema};User ID={ServerSettings.Instance.Database.Username};Password={ServerSettings.Instance.Database.Password};Default Command Timeout=3600";
        }


        public ISessionFactory CreateSessionFactory()
        {
            return Fluently.Configure()
                .Database(MySQLConfiguration.Standard.ConnectionString(
                    x => x.Database(ServerSettings.Instance.Database.Schema + ";CharSet=utf8mb4")
                        .Server(ServerSettings.Instance.Database.Hostname)
                        .Username(ServerSettings.Instance.Database.Username)
                        .Password(ServerSettings.Instance.Database.Password)))
                .Mappings(m => m.FluentMappings.AddFromAssemblyOf<ShokoService>())
                .ExposeConfiguration(c => c.DataBaseIntegration(prop =>
                {
                    // uncomment this for SQL output
                    //prop.LogSqlInConsole = true;
                }))
                .BuildSessionFactory();
        }

        public bool DatabaseAlreadyExists()
        {
            try
            {
                string connStr =
                    $"Server={ServerSettings.Instance.Database.Hostname};User ID={ServerSettings.Instance.Database.Username};Password={ServerSettings.Instance.Database.Password}";

                string sql =
                    $"SELECT SCHEMA_NAME FROM INFORMATION_SCHEMA.SCHEMATA WHERE SCHEMA_NAME = '{ServerSettings.Instance.Database.Schema}'";
                Logger.Trace(sql);

                using (MySqlConnection conn = new MySqlConnection(connStr))
                {
                    conn.Open();
                    ArrayList rows = ExecuteReader(conn, sql);
                    if (rows.Count > 0)
                    {
                        string db = (string) ((object[]) rows[0])[0];
                        Logger.Trace("Found db already exists: {0}", db);
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, ex.ToString());
            }

            Logger.Trace("db does not exist: {0}", ServerSettings.Instance.Database.Schema);
            return false;
        }

        public void CreateDatabase()
        {
            try
            {
                if (DatabaseAlreadyExists()) return;

                string connStr =
                    $"Server={ServerSettings.Instance.Database.Hostname};User ID={ServerSettings.Instance.Database.Username};Password={ServerSettings.Instance.Database.Password}";
                Logger.Trace(connStr);
                string sql =
                    $"CREATE DATABASE {ServerSettings.Instance.Database.Schema} DEFAULT CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;";
                Logger.Trace(sql);

                using (MySqlConnection conn = new MySqlConnection(connStr))
                {
                    conn.Open();
                    Execute(conn, sql);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, ex.ToString());
            }
        }


        public void CreateAndUpdateSchema()
        {
            ConnectionWrapper(GetConnectionString(), myConn =>
            {
                bool create = false;
                bool fixtablesforlinux = false;
                long count = ExecuteScalar(myConn,
                    $"select count(*) from information_schema.tables where table_schema='{ServerSettings.Instance.Database.Schema}' and table_name = 'Versions'");
                if (count == 0)
                {
                    count = ExecuteScalar(myConn,
                        $"select count(*) from information_schema.tables where table_schema='{ServerSettings.Instance.Database.Schema}' and table_name = 'versions'");
                    if (count > 0)
                    {
                        fixtablesforlinux = true;
                        ExecuteWithException(myConn, linuxTableVersionsFix);
                    }
                    else
                        create = true;
                }
                if (create)
                {
                    ServerState.Instance.ServerStartingStatus = Resources.Database_CreateSchema;
                    ExecuteWithException(myConn, createVersionTable);
                }
                count = ExecuteScalar(myConn,
                    $"select count(*) from information_schema.columns where table_schema='{ServerSettings.Instance.Database.Schema}' and table_name = 'Versions' and column_name = 'VersionRevision'");
                if (count == 0)
                {
                    ExecuteWithException(myConn, updateVersionTable);
                    AllVersions = RepoFactory.Versions.GetAllByType(Constants.DatabaseTypeKey);
                }
                PreFillVersions(createTables.Union(patchCommands));
                if (create)
                    ExecuteWithException(myConn, createTables);
                if (fixtablesforlinux)
                    ExecuteWithException(myConn, linuxTableFixes);
                ServerState.Instance.ServerStartingStatus = Resources.Database_ApplySchema;

                ExecuteWithException(myConn, patchCommands);
            });
        }

        private static void MySQLFixUTF8()
        {
            string sql = 
                "SELECT `TABLE_SCHEMA`, `TABLE_NAME`, `COLUMN_NAME`, `DATA_TYPE`, `CHARACTER_MAXIMUM_LENGTH` " +
                "FROM information_schema.COLUMNS " +
                $"WHERE table_schema = '{ServerSettings.Instance.Database.Schema}' " +
                "AND collation_name != 'utf8mb4_unicode_ci'";

            using (MySqlConnection conn = new MySqlConnection($"Server={ServerSettings.Instance.Database.Hostname};User ID={ServerSettings.Instance.Database.Username};Password={ServerSettings.Instance.Database.Password};database={ServerSettings.Instance.Database.Schema}"))
            {
                MySQL mySQL = ((MySQL)DatabaseFactory.Instance);
                conn.Open();
                ArrayList rows = mySQL.ExecuteReader(conn, sql);
                if (rows.Count > 0)
                {
                    foreach (object[] row in rows)
                    {
                        string alter = "";
                        switch (row[3].ToString().ToLowerInvariant())
                        {
                            case "text":
                            case "mediumtext":
                            case "tinytext":
                            case "longtext":
                                alter = $"ALTER TABLE `{row[1]}` MODIFY `{row[2]}` {row[3]} CHARACTER SET 'utf8mb4' COLLATE 'utf8mb4_unicode_ci'";
                                break;

                            default:
                                alter = $"ALTER TABLE `{row[1]}` MODIFY `{row[2]}` {row[3]}({row[4]}) CHARACTER SET 'utf8mb4' COLLATE 'utf8mb4_unicode_ci'";
                                break;
                        }
                        mySQL.ExecuteCommand(conn, alter);
                    }
                }
            }
        }
    }
}
