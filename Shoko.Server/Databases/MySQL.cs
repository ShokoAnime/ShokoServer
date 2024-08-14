using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FluentNHibernate.Cfg;
using FluentNHibernate.Cfg.Db;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;
using NHibernate;
using NHibernate.Driver.MySqlConnector;
using Shoko.Commons.Extensions;
using Shoko.Commons.Properties;
using Shoko.Plugin.Abstractions;
using Shoko.Server.Databases.NHibernate;
using Shoko.Server.Models;
using Shoko.Server.Renamer;
using Shoko.Server.Repositories;
using Shoko.Server.Server;
using Shoko.Server.Utilities;

// ReSharper disable InconsistentNaming


namespace Shoko.Server.Databases;

public class MySQL : BaseDatabase<MySqlConnection>
{
    public override string Name { get; } = "MySQL";
    public override int RequiredVersion { get; } = 131;

    private List<DatabaseCommand> createVersionTable = new()
    {
        new DatabaseCommand(0, 1,
            "CREATE TABLE `Versions` ( `VersionsID` INT NOT NULL AUTO_INCREMENT , `VersionType` VARCHAR(100) NOT NULL , `VersionValue` VARCHAR(100) NOT NULL ,  PRIMARY KEY (`VersionsID`) ) ; "),
        new DatabaseCommand(0, 2,
            "ALTER TABLE `Versions` ADD UNIQUE INDEX `UIX_Versions_VersionType` (`VersionType` ASC) ;")
    };

    private List<DatabaseCommand> createTables = new()
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

    private List<DatabaseCommand> patchCommands = new()
    {
        //Patches
        new(2, 1,
            "CREATE TABLE `IgnoreAnime` ( `IgnoreAnimeID` INT NOT NULL AUTO_INCREMENT ,  `JMMUserID` int NOT NULL,  `AnimeID` int NOT NULL,  `IgnoreType` int NOT NULL,  PRIMARY KEY (`IgnoreAnimeID`) ) ; "),
        new(2, 2,
            "ALTER TABLE `IgnoreAnime` ADD UNIQUE INDEX `UIX_IgnoreAnime_User_AnimeID` (`JMMUserID` ASC, `AnimeID` ASC, `IgnoreType` ASC) ;"),
        new(3, 1,
            "CREATE TABLE `Trakt_Friend` ( `Trakt_FriendID` INT NOT NULL AUTO_INCREMENT , `Username` varchar(100) character set utf8 NOT NULL, `FullName` varchar(100) character set utf8 NULL, `Gender` varchar(100) character set utf8 NULL, `Age` varchar(100) character set utf8 NULL, `Location` varchar(100) character set utf8 NULL, `About` text character set utf8 NULL, `Joined` int NOT NULL, `Avatar` text character set utf8 NULL, `Url` text character set utf8 NULL, `LastAvatarUpdate` datetime NOT NULL, PRIMARY KEY (`Trakt_FriendID`) ) ; "),
        new(3, 2,
            "ALTER TABLE `Trakt_Friend` ADD UNIQUE INDEX `UIX_Trakt_Friend_Username` (`Username` ASC) ;"),
        new(4, 1, "ALTER TABLE AnimeGroup ADD DefaultAnimeSeriesID int NULL"),
        new(5, 1, "ALTER TABLE JMMUser ADD CanEditServerSettings int NULL"),
        new(6, 1, "ALTER TABLE VideoInfo ADD VideoBitDepth varchar(100) NULL"),
        new(7, 1, DatabaseFixes.NoOperation),
        new(7, 2, DatabaseFixes.NoOperation),
        new(7, 3,
            "ALTER TABLE `CrossRef_AniDB_TvDB` ADD UNIQUE INDEX `UIX_CrossRef_AniDB_TvDB_Season` (`TvDBID` ASC, `TvDBSeasonNumber` ASC) ;"),
        new(7, 4,
            "ALTER TABLE `CrossRef_AniDB_Trakt` ADD UNIQUE INDEX `UIX_CrossRef_AniDB_Trakt_Season` (`TraktID` ASC, `TraktSeasonNumber` ASC) ;"),
        new(7, 5,
            "ALTER TABLE `CrossRef_AniDB_Trakt` ADD UNIQUE INDEX `UIX_CrossRef_AniDB_Trakt_Anime` (`AnimeID` ASC) ;"),
        new(8, 1,
            "ALTER TABLE JMMUser CHANGE COLUMN Password Password VARCHAR(150) NULL DEFAULT NULL ;"),
        new(9, 1,
            "ALTER TABLE `CommandRequest` CHANGE COLUMN `CommandID` `CommandID` text character set utf8 NOT NULL ;"),
        new(9, 2,
            "ALTER TABLE `CrossRef_File_Episode` CHANGE COLUMN `FileName` `FileName` text character set utf8 NOT NULL ;"),
        new(9, 3,
            "ALTER TABLE `FileNameHash` CHANGE COLUMN `FileName` `FileName` text character set utf8 NOT NULL ;"),
        new(10, 1,
            "ALTER TABLE `AniDB_Category` CHANGE COLUMN `CategoryName` `CategoryName` text character set utf8 NOT NULL ;"),
        new(10, 2,
            "ALTER TABLE `AniDB_Category` CHANGE COLUMN `CategoryDescription` `CategoryDescription` text character set utf8 NOT NULL ;"),
        new(10, 3,
            "ALTER TABLE `AniDB_Episode` CHANGE COLUMN `RomajiName` `RomajiName` text character set utf8 NOT NULL ;"),
        new(10, 4,
            "ALTER TABLE `AniDB_Episode` CHANGE COLUMN `EnglishName` `EnglishName` text character set utf8 NOT NULL ;"),
        new(10, 5,
            "ALTER TABLE `AniDB_Anime_Relation` CHANGE COLUMN `RelationType` `RelationType` text character set utf8 NOT NULL ;"),
        new(10, 6,
            "ALTER TABLE `AniDB_Character` CHANGE COLUMN `CharName` `CharName` text character set utf8 NOT NULL ;"),
        new(10, 7,
            "ALTER TABLE `AniDB_Seiyuu` CHANGE COLUMN `SeiyuuName` `SeiyuuName` text character set utf8 NOT NULL ;"),
        new(10, 8,
            "ALTER TABLE `AniDB_File` CHANGE COLUMN `File_Description` `File_Description` text character set utf8 NOT NULL ;"),
        new(10, 9,
            "ALTER TABLE `AniDB_File` CHANGE COLUMN `Anime_GroupName` `Anime_GroupName` text character set utf8 NOT NULL ;"),
        new(10, 10,
            "ALTER TABLE `AniDB_File` CHANGE COLUMN `Anime_GroupNameShort` `Anime_GroupNameShort` text character set utf8 NOT NULL ;"),
        new(10, 11,
            "ALTER TABLE `AniDB_File` CHANGE COLUMN `FileName` `FileName` text character set utf8 NOT NULL ;"),
        new(10, 12,
            "ALTER TABLE `AniDB_GroupStatus` CHANGE COLUMN `GroupName` `GroupName` text character set utf8 NOT NULL ;"),
        new(10, 13,
            "ALTER TABLE `AniDB_ReleaseGroup` CHANGE COLUMN `GroupName` `GroupName` text character set utf8 NOT NULL ;"),
        new(10, 14,
            "ALTER TABLE `AniDB_ReleaseGroup` CHANGE COLUMN `GroupNameShort` `GroupNameShort` text character set utf8 NOT NULL ;"),
        new(10, 15,
            "ALTER TABLE `AniDB_ReleaseGroup` CHANGE COLUMN `URL` `URL` text character set utf8 NOT NULL ;"),
        new(10, 16,
            "ALTER TABLE `AnimeGroup` CHANGE COLUMN `GroupName` `GroupName` text character set utf8 NOT NULL ;"),
        new(10, 17,
            "ALTER TABLE `AnimeGroup` CHANGE COLUMN `SortName` `SortName` text character set utf8 NOT NULL ;"),
        new(10, 18,
            "ALTER TABLE `CommandRequest` CHANGE COLUMN `CommandID` `CommandID` text character set utf8 NOT NULL ;"),
        new(10, 19,
            "ALTER TABLE `CrossRef_File_Episode` CHANGE COLUMN `FileName` `FileName` text character set utf8 NOT NULL ;"),
        new(10, 20,
            "ALTER TABLE `FileNameHash` CHANGE COLUMN `FileName` `FileName` text character set utf8 NOT NULL ;"),
        new(10, 21,
            "ALTER TABLE `ImportFolder` CHANGE COLUMN `ImportFolderLocation` `ImportFolderLocation` text character set utf8 NOT NULL ;"),
        new(10, 22,
            "ALTER TABLE `DuplicateFile` CHANGE COLUMN `FilePathFile1` `FilePathFile1` text character set utf8 NOT NULL ;"),
        new(10, 23,
            "ALTER TABLE `DuplicateFile` CHANGE COLUMN `FilePathFile2` `FilePathFile2` text character set utf8 NOT NULL ;"),
        new(10, 24,
            "ALTER TABLE `TvDB_Episode` CHANGE COLUMN `Filename` `Filename` text character set utf8 NOT NULL ;"),
        new(10, 25,
            "ALTER TABLE `TvDB_Episode` CHANGE COLUMN `EpisodeName` `EpisodeName` text character set utf8 NOT NULL ;"),
        new(10, 26,
            "ALTER TABLE `TvDB_Series` CHANGE COLUMN `SeriesName` `SeriesName` text character set utf8 NOT NULL ;"),
        new(10, 27,
            "ALTER TABLE `DuplicateFile` CHANGE COLUMN `FilePathFile2` `FilePathFile2` text character set utf8 NOT NULL ;"),
        new(10, 28,
            "ALTER TABLE `DuplicateFile` CHANGE COLUMN `FilePathFile2` `FilePathFile2` text character set utf8 NOT NULL ;"),
        new(11, 1, "ALTER TABLE `ImportFolder` ADD `IsWatched` int NULL ;"),
        new(11, 2, "UPDATE ImportFolder SET IsWatched = 1 ;"),
        new(11, 3,
            "ALTER TABLE `ImportFolder` CHANGE COLUMN `IsWatched` `IsWatched` int NOT NULL ;"),
        new(12, 1,
            "CREATE TABLE CrossRef_AniDB_MAL( CrossRef_AniDB_MALID INT NOT NULL AUTO_INCREMENT, AnimeID int NOT NULL, MALID int NOT NULL, MALTitle text, CrossRefSource int NOT NULL, PRIMARY KEY (`CrossRef_AniDB_MALID`) ) ; "),
        new(12, 2,
            "ALTER TABLE `CrossRef_AniDB_MAL` ADD UNIQUE INDEX `UIX_CrossRef_AniDB_MAL_AnimeID` (`AnimeID` ASC) ;"),
        new(12, 3,
            "ALTER TABLE `CrossRef_AniDB_MAL` ADD UNIQUE INDEX `UIX_CrossRef_AniDB_MAL_MALID` (`MALID` ASC) ;"),
        new(13, 1, "drop table `CrossRef_AniDB_MAL`;"),
        new(13, 2,
            "CREATE TABLE CrossRef_AniDB_MAL( CrossRef_AniDB_MALID INT NOT NULL AUTO_INCREMENT, AnimeID int NOT NULL, MALID int NOT NULL, MALTitle text, StartEpisodeType int NOT NULL, StartEpisodeNumber int NOT NULL, CrossRefSource int NOT NULL, PRIMARY KEY (`CrossRef_AniDB_MALID`) ) ; "),
        new(13, 3,
            "ALTER TABLE `CrossRef_AniDB_MAL` ADD UNIQUE INDEX `UIX_CrossRef_AniDB_MAL_AnimeID` (`AnimeID` ASC) ;"),
        new(13, 4,
            "ALTER TABLE `CrossRef_AniDB_MAL` ADD UNIQUE INDEX `UIX_CrossRef_AniDB_MAL_Anime` (`MALID` ASC, `AnimeID` ASC, `StartEpisodeType` ASC, `StartEpisodeNumber` ASC) ;"),
        new(14, 1,
            "CREATE TABLE Playlist( PlaylistID INT NOT NULL AUTO_INCREMENT, PlaylistName text character set utf8, PlaylistItems text character set utf8, DefaultPlayOrder int NOT NULL, PlayWatched int NOT NULL, PlayUnwatched int NOT NULL, PRIMARY KEY (`PlaylistID`) ) ; "),
        new(15, 1, "ALTER TABLE `AnimeSeries` ADD `SeriesNameOverride` text NULL ;"),
        new(16, 1,
            "CREATE TABLE BookmarkedAnime( BookmarkedAnimeID INT NOT NULL AUTO_INCREMENT, AnimeID int NOT NULL, Priority int NOT NULL, Notes text character set utf8, Downloading int NOT NULL, PRIMARY KEY (`BookmarkedAnimeID`) ) ; "),
        new(16, 2,
            "ALTER TABLE `BookmarkedAnime` ADD UNIQUE INDEX `UIX_BookmarkedAnime_AnimeID` (`AnimeID` ASC) ;"),
        new(17, 1, "ALTER TABLE `VideoLocal` ADD `DateTimeCreated` datetime NULL ;"),
        new(17, 2, "UPDATE VideoLocal SET DateTimeCreated = DateTimeUpdated ;"),
        new(17, 3,
            "ALTER TABLE `VideoLocal` CHANGE COLUMN `DateTimeCreated` `DateTimeCreated` datetime NOT NULL ;"),
        new(18, 1,
            "CREATE TABLE `CrossRef_AniDB_TvDB_Episode` ( `CrossRef_AniDB_TvDB_EpisodeID` INT NOT NULL AUTO_INCREMENT, `AnimeID` int NOT NULL, `AniDBEpisodeID` int NOT NULL, `TvDBEpisodeID` int NOT NULL, PRIMARY KEY (`CrossRef_AniDB_TvDB_EpisodeID`) ) ; "),
        new(18, 2,
            "ALTER TABLE `CrossRef_AniDB_TvDB_Episode` ADD UNIQUE INDEX `UIX_CrossRef_AniDB_TvDB_Episode_AniDBEpisodeID` (`AniDBEpisodeID` ASC) ;"),
        new(19, 1,
            "CREATE TABLE `AniDB_MylistStats` ( `AniDB_MylistStatsID` INT NOT NULL AUTO_INCREMENT, `Animes` int NOT NULL, `Episodes` int NOT NULL, `Files` int NOT NULL, `SizeOfFiles` bigint NOT NULL, `AddedAnimes` int NOT NULL, `AddedEpisodes` int NOT NULL, `AddedFiles` int NOT NULL, `AddedGroups` int NOT NULL, `LeechPct` int NOT NULL, `GloryPct` int NOT NULL, `ViewedPct` int NOT NULL, `MylistPct` int NOT NULL, `ViewedMylistPct` int NOT NULL, `EpisodesViewed` int NOT NULL, `Votes` int NOT NULL, `Reviews` int NOT NULL, `ViewiedLength` int NOT NULL, PRIMARY KEY (`AniDB_MylistStatsID`) ) ; "),
        new(20, 1,
            "CREATE TABLE `FileFfdshowPreset` ( `FileFfdshowPresetID` INT NOT NULL AUTO_INCREMENT, `Hash` varchar(50) NOT NULL, `FileSize` bigint NOT NULL, `Preset` text character set utf8, PRIMARY KEY (`FileFfdshowPresetID`) ) ; "),
        new(20, 2,
            "ALTER TABLE `FileFfdshowPreset` ADD UNIQUE INDEX `UIX_FileFfdshowPreset_Hash` (`Hash` ASC, `FileSize` ASC) ;"),
        new(21, 1, "ALTER TABLE `AniDB_Anime` ADD `DisableExternalLinksFlag` int NULL ;"),
        new(21, 2, "UPDATE AniDB_Anime SET DisableExternalLinksFlag = 0 ;"),
        new(21, 3,
            "ALTER TABLE `AniDB_Anime` CHANGE COLUMN `DisableExternalLinksFlag` `DisableExternalLinksFlag` int NOT NULL ;"),
        new(22, 1, "ALTER TABLE `AniDB_File` ADD `FileVersion` int NULL ;"),
        new(22, 2, "UPDATE AniDB_File SET FileVersion = 1 ;"),
        new(22, 3,
            "ALTER TABLE `AniDB_File` CHANGE COLUMN `FileVersion` `FileVersion` int NOT NULL ;"),
        new(23, 1,
            "CREATE TABLE RenameScript( RenameScriptID INT NOT NULL AUTO_INCREMENT, ScriptName text character set utf8, Script text character set utf8, IsEnabledOnImport int NOT NULL, PRIMARY KEY (`RenameScriptID`) ) ; "),
        new(24, 1, "ALTER TABLE `AniDB_File` ADD `IsCensored` int NULL ;"),
        new(24, 2, "ALTER TABLE `AniDB_File` ADD `IsDeprecated` int NULL ;"),
        new(24, 3, "ALTER TABLE `AniDB_File` ADD `InternalVersion` int NULL ;"),
        new(24, 4, "UPDATE AniDB_File SET IsCensored = 0 ;"),
        new(24, 5, "UPDATE AniDB_File SET IsDeprecated = 0 ;"),
        new(24, 6, "UPDATE AniDB_File SET InternalVersion = 1 ;"),
        new(24, 7,
            "ALTER TABLE `AniDB_File` CHANGE COLUMN `IsCensored` `IsCensored` int NOT NULL ;"),
        new(24, 8,
            "ALTER TABLE `AniDB_File` CHANGE COLUMN `IsDeprecated` `IsDeprecated` int NOT NULL ;"),
        new(24, 9,
            "ALTER TABLE `AniDB_File` CHANGE COLUMN `InternalVersion` `InternalVersion` int NOT NULL ;"),
        new(25, 1, "ALTER TABLE `VideoLocal` ADD `IsVariation` int NULL ;"),
        new(25, 2, "UPDATE VideoLocal SET IsVariation = 0 ;"),
        new(25, 3,
            "ALTER TABLE `VideoLocal` CHANGE COLUMN `IsVariation` `IsVariation` int NOT NULL ;"),
        new(26, 1,
            "CREATE TABLE AniDB_Recommendation( AniDB_RecommendationID INT NOT NULL AUTO_INCREMENT, AnimeID int NOT NULL, UserID int NOT NULL, RecommendationType int NOT NULL, RecommendationText text character set utf8, PRIMARY KEY (`AniDB_RecommendationID`) ) ; "),
        new(26, 2,
            "ALTER TABLE `AniDB_Recommendation` ADD UNIQUE INDEX `UIX_AniDB_Recommendation` (`AnimeID` ASC, `UserID` ASC) ;"),
        new(27, 1,
            "update CrossRef_File_Episode SET CrossRefSource=1 WHERE Hash IN (Select Hash from AniDB_File) AND CrossRefSource=2 ;"),
        new(28, 1,
            "CREATE TABLE LogMessage( LogMessageID INT NOT NULL AUTO_INCREMENT, LogType text character set utf8, LogContent text character set utf8, LogDate datetime NOT NULL, PRIMARY KEY (`LogMessageID`) ) ; "),
        new(29, 1,
            "CREATE TABLE CrossRef_AniDB_TvDBV2( CrossRef_AniDB_TvDBV2ID INT NOT NULL AUTO_INCREMENT, AnimeID int NOT NULL, AniDBStartEpisodeType int NOT NULL, AniDBStartEpisodeNumber int NOT NULL, TvDBID int NOT NULL, TvDBSeasonNumber int NOT NULL, TvDBStartEpisodeNumber int NOT NULL, TvDBTitle text character set utf8, CrossRefSource int NOT NULL, PRIMARY KEY (`CrossRef_AniDB_TvDBV2ID`) ) ; "),
        new(29, 2,
            "ALTER TABLE `CrossRef_AniDB_TvDBV2` ADD UNIQUE INDEX `UIX_CrossRef_AniDB_TvDBV2` (`AnimeID` ASC, `TvDBID` ASC, `TvDBSeasonNumber` ASC, `TvDBStartEpisodeNumber` ASC, `AniDBStartEpisodeType` ASC, `AniDBStartEpisodeNumber` ASC) ;"),
        new(29, 3, DatabaseFixes.NoOperation),
        new(30, 1, "ALTER TABLE `GroupFilter` ADD `Locked` int NULL ;"),
        new(31, 1, "ALTER TABLE VideoInfo ADD FullInfo varchar(10000) NULL"),
        new(32, 1,
            "CREATE TABLE CrossRef_AniDB_TraktV2( CrossRef_AniDB_TraktV2ID INT NOT NULL AUTO_INCREMENT, AnimeID int NOT NULL, AniDBStartEpisodeType int NOT NULL, AniDBStartEpisodeNumber int NOT NULL, TraktID varchar(100) character set utf8, TraktSeasonNumber int NOT NULL, TraktStartEpisodeNumber int NOT NULL, TraktTitle text character set utf8, CrossRefSource int NOT NULL, PRIMARY KEY (`CrossRef_AniDB_TraktV2ID`) ) ; "),
        new(32, 2,
            "ALTER TABLE `CrossRef_AniDB_TraktV2` ADD UNIQUE INDEX `UIX_CrossRef_AniDB_TraktV2` (`AnimeID` ASC, `TraktSeasonNumber` ASC, `TraktStartEpisodeNumber` ASC, `AniDBStartEpisodeType` ASC, `AniDBStartEpisodeNumber` ASC) ;"),
        new(32, 3, DatabaseFixes.NoOperation),
        new(33, 1,
            "CREATE TABLE `CrossRef_AniDB_Trakt_Episode` ( `CrossRef_AniDB_Trakt_EpisodeID` INT NOT NULL AUTO_INCREMENT, `AnimeID` int NOT NULL, `AniDBEpisodeID` int NOT NULL, `TraktID` varchar(100) character set utf8, `Season` int NOT NULL, `EpisodeNumber` int NOT NULL, PRIMARY KEY (`CrossRef_AniDB_Trakt_EpisodeID`) ) ; "),
        new(33, 2,
            "ALTER TABLE `CrossRef_AniDB_Trakt_Episode` ADD UNIQUE INDEX `UIX_CrossRef_AniDB_Trakt_Episode_AniDBEpisodeID` (`AniDBEpisodeID` ASC) ;"),
        new(34, 1, DatabaseFixes.NoOperation),
        new(35, 1,
            "CREATE TABLE `CustomTag` ( `CustomTagID` INT NOT NULL AUTO_INCREMENT, `TagName` text character set utf8, `TagDescription` text character set utf8, PRIMARY KEY (`CustomTagID`) ) ; "),
        new(35, 2,
            "CREATE TABLE `CrossRef_CustomTag` ( `CrossRef_CustomTagID` INT NOT NULL AUTO_INCREMENT, `CustomTagID` int NOT NULL, `CrossRefID` int NOT NULL, `CrossRefType` int NOT NULL, PRIMARY KEY (`CrossRef_CustomTagID`) ) ; "),
        new(36, 1,
            $"ALTER DATABASE {Utils.SettingsProvider.GetSettings().Database.Schema} CHARACTER SET = utf8mb4 COLLATE = utf8mb4_unicode_ci;"),
        new(37, 1,
            "ALTER TABLE `CrossRef_AniDB_MAL` DROP INDEX `UIX_CrossRef_AniDB_MAL_AnimeID` ;"),
        new(37, 2, "ALTER TABLE `CrossRef_AniDB_MAL` DROP INDEX `UIX_CrossRef_AniDB_MAL_Anime` ;"),
        new(37, 3,
            "ALTER TABLE `CrossRef_AniDB_MAL` ADD UNIQUE INDEX `UIX_CrossRef_AniDB_MAL_MALID` (`MALID` ASC) ;"),
        new(37, 4,
            "ALTER TABLE `CrossRef_AniDB_MAL` ADD UNIQUE INDEX `UIX_CrossRef_AniDB_MAL_Anime` (`AnimeID` ASC, `StartEpisodeType` ASC, `StartEpisodeNumber` ASC) ;"),
        new(38, 1, "ALTER TABLE AniDB_Anime_Tag ADD Weight int NULL"),
        new(39, 1, DatabaseFixes.PopulateTagWeight),
        new(40, 1, "ALTER TABLE Trakt_Episode ADD TraktID int NULL"),
        new(41, 1, DatabaseFixes.FixHashes),
        new(42, 1, "drop table `LogMessage`;"),
        new(43, 1, "ALTER TABLE AnimeSeries ADD DefaultFolder text character set utf8"),
        new(44, 1, "ALTER TABLE JMMUser ADD PlexUsers text character set utf8"),
        new(45, 1, "ALTER TABLE `GroupFilter` ADD `FilterType` int NULL ;"),
        new(45, 2, "UPDATE GroupFilter SET FilterType = 1 ;"),
        new(45, 3,
            "ALTER TABLE `GroupFilter` CHANGE COLUMN `FilterType` `FilterType` int NOT NULL ;"),
        new(45, 4, DatabaseFixes.NoOperation),
        new(46, 1, "ALTER TABLE `AniDB_Anime` ADD `ContractVersion` int NOT NULL DEFAULT 0"),
        new(46, 2,
            "ALTER TABLE `AniDB_Anime` ADD `ContractString` mediumtext character set utf8 NULL"),
        new(46, 3, "ALTER TABLE `AnimeGroup` ADD `ContractVersion` int NOT NULL DEFAULT 0"),
        new(46, 4,
            "ALTER TABLE `AnimeGroup` ADD `ContractString` mediumtext character set utf8 NULL"),
        new(46, 5,
            "ALTER TABLE `AnimeGroup_User` ADD `PlexContractVersion` int NOT NULL DEFAULT 0"),
        new(46, 6,
            "ALTER TABLE `AnimeGroup_User` ADD `PlexContractString` mediumtext character set utf8 NULL"),
        new(46, 7,
            "ALTER TABLE `AnimeGroup_User` ADD `KodiContractVersion` int NOT NULL DEFAULT 0"),
        new(46, 8,
            "ALTER TABLE `AnimeGroup_User` ADD `KodiContractString` mediumtext character set utf8 NULL"),
        new(46, 9, "ALTER TABLE `AnimeSeries` ADD `ContractVersion` int NOT NULL DEFAULT 0"),
        new(46, 10,
            "ALTER TABLE `AnimeSeries` ADD `ContractString` mediumtext character set utf8 NULL"),
        new(46, 11,
            "ALTER TABLE `AnimeSeries_User` ADD `PlexContractVersion` int NOT NULL DEFAULT 0"),
        new(46, 12,
            "ALTER TABLE `AnimeSeries_User` ADD `PlexContractString` mediumtext character set utf8 NULL"),
        new(46, 13,
            "ALTER TABLE `AnimeSeries_User` ADD `KodiContractVersion` int NOT NULL DEFAULT 0"),
        new(46, 14,
            "ALTER TABLE `AnimeSeries_User` ADD `KodiContractString` mediumtext character set utf8 NULL"),
        new(46, 15, "ALTER TABLE `GroupFilter` ADD `GroupsIdsVersion` int NOT NULL DEFAULT 0"),
        new(46, 16,
            "ALTER TABLE `GroupFilter` ADD `GroupsIdsString` mediumtext character set utf8 NULL"),
        new(46, 17, "ALTER TABLE `AnimeEpisode_User` ADD `ContractVersion` int NOT NULL DEFAULT 0"),
        new(46, 18,
            "ALTER TABLE `AnimeEpisode_User` ADD `ContractString` mediumtext character set utf8 NULL"),
        new(47, 1, "ALTER TABLE `AnimeEpisode` ADD `PlexContractVersion` int NOT NULL DEFAULT 0"),
        new(47, 2,
            "ALTER TABLE `AnimeEpisode` ADD `PlexContractString` mediumtext character set utf8 NULL"),
        new(47, 3, "ALTER TABLE `VideoLocal` ADD `MediaVersion` int NOT NULL DEFAULT 0"),
        new(47, 4, "ALTER TABLE `VideoLocal` ADD `MediaString` mediumtext character set utf8 NULL"),
        new(48, 1, "ALTER TABLE `AnimeSeries_User` DROP COLUMN `KodiContractVersion`"),
        new(48, 2, "ALTER TABLE `AnimeSeries_User` DROP COLUMN `KodiContractString`"),
        new(48, 3, "ALTER TABLE `AnimeGroup_User` DROP COLUMN `KodiContractVersion`"),
        new(48, 4, "ALTER TABLE `AnimeGroup_User` DROP COLUMN `KodiContractString`"),
        new(49, 1, "ALTER TABLE AnimeSeries ADD LatestEpisodeAirDate datetime NULL"),
        new(49, 2, "ALTER TABLE AnimeGroup ADD LatestEpisodeAirDate datetime NULL"),
        new(50, 1, "ALTER TABLE `GroupFilter` ADD `GroupConditionsVersion` int NOT NULL DEFAULT 0"),
        new(50, 2,
            "ALTER TABLE `GroupFilter` ADD `GroupConditions` mediumtext character set utf8 NULL"),
        new(50, 3, "ALTER TABLE `GroupFilter` ADD `ParentGroupFilterID` int NULL"),
        new(50, 4, "ALTER TABLE `GroupFilter` ADD `InvisibleInClients` int NOT NULL DEFAULT 0"),
        new(50, 5, "ALTER TABLE `GroupFilter` ADD `SeriesIdsVersion` int NOT NULL DEFAULT 0"),
        new(50, 6,
            "ALTER TABLE `GroupFilter` ADD `SeriesIdsString` mediumtext character set utf8 NULL"),
        new(51, 1, "ALTER TABLE `AniDB_Anime` ADD `ContractBlob` mediumblob NULL"),
        new(51, 2, "ALTER TABLE `AniDB_Anime` ADD `ContractSize` int NOT NULL DEFAULT 0"),
        new(51, 3, "ALTER TABLE `AniDB_Anime` DROP COLUMN `ContractString`"),
        new(51, 4, "ALTER TABLE `VideoLocal` ADD `MediaBlob` mediumblob NULL"),
        new(51, 5, "ALTER TABLE `VideoLocal` ADD `MediaSize` int NOT NULL DEFAULT 0"),
        new(51, 6, "ALTER TABLE `VideoLocal` DROP COLUMN `MediaString`"),
        new(51, 7, "ALTER TABLE `AnimeEpisode` ADD `PlexContractBlob` mediumblob NULL"),
        new(51, 8, "ALTER TABLE `AnimeEpisode` ADD `PlexContractSize` int NOT NULL DEFAULT 0"),
        new(51, 9, "ALTER TABLE `AnimeEpisode` DROP COLUMN `PlexContractString`"),
        new(51, 10, "ALTER TABLE `AnimeEpisode_User` ADD `ContractBlob` mediumblob NULL"),
        new(51, 11, "ALTER TABLE `AnimeEpisode_User` ADD `ContractSize` int NOT NULL DEFAULT 0"),
        new(51, 12, "ALTER TABLE `AnimeEpisode_User` DROP COLUMN `ContractString`"),
        new(51, 13, "ALTER TABLE `AnimeSeries` ADD `ContractBlob` mediumblob NULL"),
        new(51, 14, "ALTER TABLE `AnimeSeries` ADD `ContractSize` int NOT NULL DEFAULT 0"),
        new(51, 15, "ALTER TABLE `AnimeSeries` DROP COLUMN `ContractString`"),
        new(51, 16, "ALTER TABLE `AnimeSeries_User` ADD `PlexContractBlob` mediumblob NULL"),
        new(51, 17, "ALTER TABLE `AnimeSeries_User` ADD `PlexContractSize` int NOT NULL DEFAULT 0"),
        new(51, 18, "ALTER TABLE `AnimeSeries_User` DROP COLUMN `PlexContractString`"),
        new(51, 19, "ALTER TABLE `AnimeGroup_User` ADD `PlexContractBlob` mediumblob NULL"),
        new(51, 20, "ALTER TABLE `AnimeGroup_User` ADD `PlexContractSize` int NOT NULL DEFAULT 0"),
        new(51, 21, "ALTER TABLE `AnimeGroup_User` DROP COLUMN `PlexContractString`"),
        new(51, 22, "ALTER TABLE `AnimeGroup` ADD `ContractBlob` mediumblob NULL"),
        new(51, 23, "ALTER TABLE `AnimeGroup` ADD `ContractSize` int NOT NULL DEFAULT 0"),
        new(51, 24, "ALTER TABLE `AnimeGroup` DROP COLUMN `ContractString`"),
        new(52, 1, "ALTER TABLE `AniDB_Anime` DROP COLUMN `AllCategories`"),
        new(53, 1, DatabaseFixes.DeleteSeriesUsersWithoutSeries),
        new(54, 1,
            "CREATE TABLE `VideoLocal_Place` ( `VideoLocal_Place_ID` INT NOT NULL AUTO_INCREMENT, `VideoLocalID` int NOT NULL, `FilePath` text character set utf8 NOT NULL, `ImportFolderID` int NOT NULL, `ImportFolderType` int NOT NULL, PRIMARY KEY (`VideoLocal_Place_ID`) ) ; "),
        new(54, 2, "ALTER TABLE `VideoLocal` ADD `FileName` text character set utf8 NOT NULL"),
        new(54, 3, "ALTER TABLE `VideoLocal` ADD `VideoCodec` varchar(100) NOT NULL DEFAULT ''"),
        new(54, 4, "ALTER TABLE `VideoLocal` ADD `VideoBitrate` varchar(100) NOT NULL DEFAULT ''"),
        new(54, 5, "ALTER TABLE `VideoLocal` ADD `VideoBitDepth` varchar(100) NOT NULL DEFAULT ''"),
        new(54, 6,
            "ALTER TABLE `VideoLocal` ADD `VideoFrameRate` varchar(100) NOT NULL DEFAULT ''"),
        new(54, 7,
            "ALTER TABLE `VideoLocal` ADD `VideoResolution` varchar(100) NOT NULL DEFAULT ''"),
        new(54, 8, "ALTER TABLE `VideoLocal` ADD `AudioCodec` varchar(100) NOT NULL DEFAULT ''"),
        new(54, 9, "ALTER TABLE `VideoLocal` ADD `AudioBitrate` varchar(100) NOT NULL DEFAULT ''"),
        new(54, 10, "ALTER TABLE `VideoLocal` ADD `Duration` bigint NOT NULL DEFAULT 0"),
        new(54, 11,
            "INSERT INTO `VideoLocal_Place` (`VideoLocalID`, `FilePath`, `ImportFolderID`, `ImportFolderType`) SELECT `VideoLocalID`, `FilePath`, `ImportFolderID`, 1 as `ImportFolderType` FROM `VideoLocal`"),
        new(54, 12, "ALTER TABLE `VideoLocal` DROP COLUMN `FilePath`"),
        new(54, 13, "ALTER TABLE `VideoLocal` DROP COLUMN `ImportFolderID`"),
        new(54, 14,
            "CREATE TABLE `CloudAccount` ( `CloudID` INT NOT NULL AUTO_INCREMENT,  `ConnectionString` text character set utf8 NOT NULL,  `Provider` varchar(100) NOT NULL DEFAULT '', `Name` varchar(256) NOT NULL DEFAULT '',  PRIMARY KEY (`CloudID`) ) ; "),
        new(54, 15, "ALTER TABLE `ImportFolder` ADD `CloudID` int NULL"),
        new(54, 16, "ALTER TABLE `VideoLocal_User` MODIFY COLUMN `WatchedDate` datetime NULL"),
        new(54, 17, "ALTER TABLE `VideoLocal_User` ADD `ResumePosition` bigint NOT NULL DEFAULT 0"),
        new(54, 17, "ALTER TABLE `VideoLocal_User` ADD `ResumePosition` bigint NOT NULL DEFAULT 0"),
        new(54, 18,
            "UPDATE `VideoLocal` INNER JOIN `VideoInfo` ON `VideoLocal`.`Hash`=`VideoInfo`.`Hash` SET `VideoLocal`.`FileName`=`VideoInfo`.`FileName`,`VideoLocal`.`VideoCodec`=`VideoInfo`.`VideoCodec`,`VideoLocal`.`VideoBitrate`=`VideoInfo`.`VideoBitrate`,`VideoLocal`.`VideoBitDepth`=`VideoInfo`.`VideoBitDepth`,`VideoLocal`.`VideoFrameRate`=`VideoInfo`.`VideoFrameRate`,`VideoLocal`.`VideoResolution`=`VideoInfo`.`VideoResolution`,`VideoLocal`.`AudioCodec`=`VideoInfo`.`AudioCodec`,`VideoLocal`.`AudioBitrate`=`VideoInfo`.`AudioBitrate`,`VideoLocal`.`Duration`=`VideoInfo`.`Duration`"),
        new(54, 19, "DROP TABLE `VideoInfo`"),
        new(55, 1, "ALTER TABLE `VideoLocal` DROP INDEX `UIX_VideoLocal_Hash` ;"),
        new(55, 2, "ALTER TABLE `VideoLocal` ADD INDEX `IX_VideoLocal_Hash` (`Hash` ASC) ;"),
        new(56, 1,
            "CREATE TABLE `AuthTokens` ( `AuthID` INT NOT NULL AUTO_INCREMENT, `UserID` int NOT NULL, `DeviceName` text character set utf8, `Token` text character set utf8, PRIMARY KEY (`AuthID`) ) ; "),
        new(57, 1,
            "CREATE TABLE `Scan` ( `ScanID` INT NOT NULL AUTO_INCREMENT, `CreationTime` datetime NOT NULL, `ImportFolders` text character set utf8, `Status` int NOT NULL, PRIMARY KEY (`ScanID`) ) ; "),
        new(57, 2,
            "CREATE TABLE `ScanFile` ( `ScanFileID` INT NOT NULL AUTO_INCREMENT, `ScanID` int NOT NULL, `ImportFolderID` int NOT NULL, `VideoLocal_Place_ID` int NOT NULL, `FullName` text character set utf8, `FileSize` bigint NOT NULL, `Status` int NOT NULL, `CheckDate` datetime NULL, `Hash` text character set utf8, `HashResult` text character set utf8 NULL, PRIMARY KEY (`ScanFileID`) ) ; "),
        new(57, 3,
            "ALTER TABLE `ScanFile` ADD  INDEX `UIX_ScanFileStatus` (`ScanID` ASC, `Status` ASC, `CheckDate` ASC) ;"),
        new(58, 1, DatabaseFixes.NoOperation),
        new(59, 1,
            "ALTER TABLE `GroupFilter` ADD INDEX `IX_groupfilter_GroupFilterName` (`GroupFilterName`(250));"),
        new(60, 1, DatabaseFixes.NoOperation),
        new(61, 1, DatabaseFixes.NoOperation),
        new(62, 1, "ALTER TABLE JMMUser ADD PlexToken text character set utf8"),
        new(63, 1, "ALTER TABLE AniDB_File ADD IsChaptered INT NOT NULL DEFAULT -1"),
        new(64, 1, "ALTER TABLE `CrossRef_File_Episode` ADD INDEX `IX_Xref_Epid` (`episodeid` ASC) ;"),
        new(64, 2, "ALTER TABLE `CrossRef_Subtitles_AniDB_File` ADD INDEX `IX_Xref_Sub_AniDBFile` (`fileid` ASC) ;"),
        new(64, 3, "ALTER TABLE `CrossRef_Languages_AniDB_File` ADD INDEX `IX_Xref_Epid` (`fileid` ASC) ;"),
        new(65, 1,
            "ALTER TABLE RenameScript ADD RenamerType varchar(255) character set utf8 NOT NULL DEFAULT 'Legacy'"),
        new(65, 2, "ALTER TABLE RenameScript ADD ExtraData TEXT character set utf8"),
        new(66, 1,
            "ALTER TABLE `AniDB_Anime_Character` ADD INDEX `IX_AniDB_Anime_Character_CharID` (`CharID` ASC) ;"),
        new(67, 1, "ALTER TABLE `TvDB_Episode` ADD `Rating` int NULL"),
        new(67, 2, "ALTER TABLE `TvDB_Episode` ADD `AirDate` datetime NULL"),
        new(67, 3, "ALTER TABLE `TvDB_Episode` DROP COLUMN `FirstAired`"),
        new(67, 4, DatabaseFixes.NoOperation),
        new(68, 1, "ALTER TABLE `AnimeSeries` ADD `AirsOn` TEXT character set utf8 NULL"),
        new(69, 1, "DROP TABLE `Trakt_ImageFanart`"),
        new(69, 2, "DROP TABLE `Trakt_ImagePoster`"),
        new(70, 1,
            "CREATE TABLE `AnimeCharacter` ( `CharacterID` INT NOT NULL AUTO_INCREMENT, `AniDBID` INT NOT NULL, `Name` text character set utf8 NOT NULL, `AlternateName` text character set utf8 NULL, `Description` text character set utf8 NULL, `ImagePath` text character set utf8 NULL, PRIMARY KEY (`CharacterID`) )"),
        new(70, 2,
            "CREATE TABLE `AnimeStaff` ( `StaffID` INT NOT NULL AUTO_INCREMENT, `AniDBID` INT NOT NULL, `Name` text character set utf8 NOT NULL, `AlternateName` text character set utf8 NULL, `Description` text character set utf8 NULL, `ImagePath` text character set utf8 NULL, PRIMARY KEY (`StaffID`) )"),
        new(70, 3,
            "CREATE TABLE `CrossRef_Anime_Staff` ( `CrossRef_Anime_StaffID` INT NOT NULL AUTO_INCREMENT, `AniDB_AnimeID` INT NOT NULL, `StaffID` INT NOT NULL, `Role` text character set utf8 NULL, `RoleID` INT, `RoleType` INT NOT NULL, `Language` text character set utf8 NOT NULL, PRIMARY KEY (`CrossRef_Anime_StaffID`) )"),
        new(70, 4, DatabaseFixes.PopulateCharactersAndStaff),
        new(71, 1, "ALTER TABLE `MovieDB_Movie` ADD `Rating` INT NOT NULL DEFAULT 0"),
        new(71, 2, "ALTER TABLE `TvDB_Series` ADD `Rating` INT NULL"),
        new(72, 1, "ALTER TABLE `AniDB_Episode` ADD `Description` text character set utf8 NOT NULL"),
        new(72, 2, DatabaseFixes.FixCharactersWithGrave),
        new(73, 1, DatabaseFixes.RefreshAniDBInfoFromXML),
        new(74, 1, DatabaseFixes.NoOperation),
        new(74, 2, DatabaseFixes.UpdateAllStats),
        new(75, 1, DatabaseFixes.RemoveBasePathsFromStaffAndCharacters),
        new(76, 1,
            "CREATE TABLE `AniDB_AnimeUpdate` ( `AniDB_AnimeUpdateID` INT NOT NULL AUTO_INCREMENT, `AnimeID` INT NOT NULL, `UpdatedAt` datetime NOT NULL, PRIMARY KEY (`AniDB_AnimeUpdateID`) );"),
        new(76, 2, "ALTER TABLE `AniDB_AnimeUpdate` ADD INDEX `UIX_AniDB_AnimeUpdate` (`AnimeID` ASC) ;"),
        new(76, 3, DatabaseFixes.MigrateAniDB_AnimeUpdates),
        new(77, 1, DatabaseFixes.RemoveBasePathsFromStaffAndCharacters),
        new(78, 1, DatabaseFixes.NoOperation),
        new(79, 1, DatabaseFixes.NoOperation),
        new(80, 1, "ALTER TABLE `CrossRef_AniDB_MAL` DROP INDEX `UIX_CrossRef_AniDB_MAL_Anime` ;"),
        new(80, 2,
            "ALTER TABLE `AniDB_Anime` ADD ( `Site_JP` text character set utf8 null, `Site_EN` text character set utf8 null, `Wikipedia_ID` text character set utf8 null, `WikipediaJP_ID` text character set utf8 null, `SyoboiID` INT NULL, `AnisonID` INT NULL, `CrunchyrollID` text character set utf8 null );"),
        new(80, 3, DatabaseFixes.NoOperation),
        new(81, 1, "ALTER TABLE `VideoLocal` ADD `MyListID` INT NOT NULL DEFAULT 0"),
        new(81, 2, DatabaseFixes.NoOperation),
        new(82, 1, MySQLFixUTF8),
        new(83, 1, "ALTER TABLE `AniDB_Episode` DROP COLUMN `EnglishName`"),
        new(83, 2, "ALTER TABLE `AniDB_Episode` DROP COLUMN `RomajiName`"),
        new(83, 3,
            "CREATE TABLE `AniDB_Episode_Title` ( `AniDB_Episode_TitleID` INT NOT NULL AUTO_INCREMENT, `AniDB_EpisodeID` int NOT NULL, `Language` varchar(50) character set utf8 NOT NULL, `Title` varchar(500) character set utf8 NOT NULL, PRIMARY KEY (`AniDB_Episode_TitleID`) ) ; "),
        new(83, 4, DatabaseFixes.NoOperation),
        new(84, 1,
            "ALTER TABLE `CrossRef_AniDB_TvDB_Episode` DROP INDEX `UIX_CrossRef_AniDB_TvDB_Episode_AniDBEpisodeID`;"),
        new(84, 2, "RENAME TABLE `CrossRef_AniDB_TvDB_Episode` TO `CrossRef_AniDB_TvDB_Episode_Override`;"),
        new(84, 3, "ALTER TABLE `CrossRef_AniDB_TvDB_Episode_Override` DROP COLUMN `AnimeID`"),
        new(84, 4,
            "ALTER TABLE `CrossRef_AniDB_TvDB_Episode_Override` CHANGE `CrossRef_AniDB_TvDB_EpisodeID` `CrossRef_AniDB_TvDB_Episode_OverrideID` INT NOT NULL AUTO_INCREMENT;"),
        new(84, 5,
            "ALTER TABLE `CrossRef_AniDB_TvDB_Episode_Override` ADD UNIQUE INDEX `UIX_AniDB_TvDB_Episode_Override_AniDBEpisodeID_TvDBEpisodeID` (`AniDBEpisodeID` ASC, `TvDBEpisodeID` ASC);"),
        // For some reason, this was never dropped
        new(84, 6, "DROP TABLE `CrossRef_AniDB_TvDB`;"),
        new(84, 7,
            "CREATE TABLE `CrossRef_AniDB_TvDB` ( `CrossRef_AniDB_TvDBID` INT NOT NULL AUTO_INCREMENT, `AniDBID` int NOT NULL, `TvDBID` int NOT NULL, `CrossRefSource` INT NOT NULL, PRIMARY KEY (`CrossRef_AniDB_TvDBID`));"),
        new(84, 8,
            "ALTER TABLE `CrossRef_AniDB_TvDB` ADD UNIQUE INDEX `UIX_AniDB_TvDB_AniDBID_TvDBID` (`AniDBID` ASC, `TvDBID` ASC);"),
        new(84, 9,
            "CREATE TABLE `CrossRef_AniDB_TvDB_Episode` ( `CrossRef_AniDB_TvDB_EpisodeID` INT NOT NULL AUTO_INCREMENT, `AniDBEpisodeID` int NOT NULL, `TvDBEpisodeID` int NOT NULL, `MatchRating` INT NOT NULL, PRIMARY KEY (`CrossRef_AniDB_TvDB_EpisodeID`) );"),
        new(84, 10,
            "ALTER TABLE `CrossRef_AniDB_TvDB_Episode` ADD UNIQUE INDEX `UIX_CrossRef_AniDB_TvDB_Episode_AniDBID_TvDBID` ( `AniDBEpisodeID` ASC, `TvDBEpisodeID` ASC);"),
        new(84, 11, DatabaseFixes.NoOperation),
        // DatabaseFixes.MigrateTvDBLinks_v2_to_V3() drops the CrossRef_AniDB_TvDBV2 table. We do it after init to migrate
        new(85, 1, DatabaseFixes.NoOperation),
        new(86, 1, DatabaseFixes.NoOperation),
        new(87, 1, "ALTER TABLE `AniDB_File` CHANGE COLUMN `File_AudioCodec` `File_AudioCodec` VARCHAR(500) NOT NULL;"),
        new(88, 1, "ALTER TABLE `AnimeSeries` ADD `UpdatedAt` datetime NOT NULL DEFAULT '2000-01-01 00:00:00';"),
        new(89, 1, DatabaseFixes.NoOperation),
        new(90, 1,
            "ALTER TABLE VideoLocal DROP COLUMN VideoCodec, DROP COLUMN VideoBitrate, DROP COLUMN VideoFrameRate, DROP COLUMN VideoResolution, DROP COLUMN AudioCodec, DROP COLUMN AudioBitrate, DROP COLUMN Duration;"),
        new(91, 1, DropMALIndex),
        new(92, 1, DropAniDBUniqueIndex),
        new(93, 1,
            "CREATE TABLE `AniDB_Anime_Staff` ( `AniDB_Anime_StaffID` INT NOT NULL AUTO_INCREMENT, `AnimeID` int NOT NULL, `CreatorID` int NOT NULL, `CreatorType` varchar(50) NOT NULL, PRIMARY KEY (`AniDB_Anime_StaffID`) );"),
        new(93, 2, DatabaseFixes.RefreshAniDBInfoFromXML),
        new(94, 1, DatabaseFixes.EnsureNoOrphanedGroupsOrSeries),
        new(95, 1, "UPDATE VideoLocal_User SET WatchedDate = NULL WHERE WatchedDate = '1970-01-01 00:00:00';"),
        new(95, 2, "ALTER TABLE VideoLocal_User ADD WatchedCount INT NOT NULL DEFAULT 0;"),
        new(95, 3,
            "ALTER TABLE VideoLocal_User ADD LastUpdated datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP;"),
        new(95, 4,
            "UPDATE VideoLocal_User SET WatchedCount = 1, LastUpdated = WatchedDate WHERE WatchedDate IS NOT NULL;"),
        new(96, 1, "ALTER TABLE AnimeSeries_User ADD LastEpisodeUpdate datetime DEFAULT NULL;"),
        new(96, 2, DatabaseFixes.FixWatchDates),
        new(97, 1, "ALTER TABLE AnimeGroup ADD MainAniDBAnimeID INT DEFAULT NULL;"),
        new(98, 1,
            "ALTER TABLE AnimeEpisode_User DROP COLUMN ContractSize, DROP COLUMN ContractBlob, DROP COLUMN ContractVersion;"),
        new(99, 1,
            "ALTER TABLE `AniDB_File` DROP `File_AudioCodec`, DROP `File_VideoCodec`, DROP `File_VideoResolution`, DROP `File_FileExtension`, DROP `File_LengthSeconds`, DROP `Anime_GroupName`, DROP `Anime_GroupNameShort`, DROP `Episode_Rating`, DROP `Episode_Votes`, DROP `IsWatched`, DROP `WatchedDate`, DROP `CRC`, DROP `MD5`, DROP `SHA1`"),
        new(99, 2, "UPDATE AniDB_File SET IsChaptered = 0 WHERE IsChaptered = -1; UPDATE AniDB_File SET IsDeprecated = 0 WHERE IsDeprecated = -1; ALTER TABLE `AniDB_File` MODIFY `IsCensored` bit NULL; ALTER TABLE `AniDB_File` MODIFY `IsDeprecated` bit not null; ALTER TABLE `AniDB_File` MODIFY `IsChaptered` bit not null"),
        new(99, 3, "ALTER TABLE `AniDB_GroupStatus` MODIFY `Rating` decimal(6,2) NULL; UPDATE `AniDB_GroupStatus` SET `Rating` = `Rating` / 100 WHERE `Rating` > 10"),
        new(99, 4, "ALTER TABLE `AniDB_Character` DROP COLUMN CreatorListRaw;"),
        new(99, 5, "ALTER TABLE `AniDB_Anime_Character` DROP COLUMN EpisodeListRaw;"),
        new(99, 6, "ALTER TABLE `AniDB_Anime` DROP COLUMN AwardList;"),
        new(99, 7, "ALTER TABLE `AniDB_File` DROP COLUMN AnimeID;"),
        new(100, 1, "ALTER TABLE CrossRef_Languages_AniDB_File ADD LanguageName nvarchar(100) NOT NULL DEFAULT '';"),
        new(100, 2,
            "UPDATE CrossRef_Languages_AniDB_File c INNER JOIN Language l ON l.LanguageID = c.LanguageID SET c.LanguageName = l.LanguageName WHERE c.LanguageName = '';"),
        new(100, 3, "ALTER TABLE CrossRef_Languages_AniDB_File DROP COLUMN LanguageID;"),
        new(100, 4, "ALTER TABLE CrossRef_Subtitles_AniDB_File ADD LanguageName nvarchar(100) NOT NULL DEFAULT '';"),
        new(100, 5,
            "UPDATE CrossRef_Subtitles_AniDB_File c INNER JOIN Language l ON l.LanguageID = c.LanguageID SET c.LanguageName = l.LanguageName WHERE c.LanguageName = '';"),
        new(100, 6, "ALTER TABLE CrossRef_Subtitles_AniDB_File DROP COLUMN LanguageID;"),
        new(100, 7, "DROP TABLE Language;"),
        new(101, 1, "DROP TABLE AniDB_Anime_Category"),
        new(101, 2, "DROP TABLE AniDB_Anime_Review"),
        new(101, 3, "DROP TABLE AniDB_Category"),
        new(101, 4, "DROP TABLE AniDB_MylistStats"),
        new(101, 5, "DROP TABLE AniDB_Review"),
        new(101, 6, "DROP TABLE CloudAccount"),
        new(101, 7, "DROP TABLE FileFfdshowPreset"),
        new(101, 8, "DROP TABLE CrossRef_AniDB_Trakt"),
        new(101, 9, "DROP TABLE Trakt_Friend"),
        new(101, 10, "ALTER TABLE VideoLocal DROP COLUMN VideoBitDepth;"),
        new(101, 11, "ALTER TABLE AniDB_File ADD INDEX IX_AniDB_File_FileID (FileID);"),
        new(101, 12,
            "ALTER TABLE CrossRef_File_Episode DROP INDEX IX_Xref_Epid; ALTER TABLE CrossRef_File_Episode ADD INDEX IX_CrossRef_File_Episode_EpisodeID (EpisodeID);"),
        new(101, 13, "ALTER TABLE CrossRef_Languages_AniDB_File DROP INDEX IX_Xref_Epid;"),
        new(101, 14, "ALTER TABLE CrossRef_Subtitles_AniDB_File DROP INDEX IX_Xref_Sub_AniDBFile;"),
        new(101, 15, "ALTER TABLE GroupFilter DROP INDEX IX_groupfilter_GroupFilterName;"),
        new(101, 16,
            "ALTER TABLE VideoLocal DROP INDEX IX_VideoLocal_Hash; ALTER TABLE VideoLocal ADD UNIQUE INDEX UIX_VideoLocal_Hash (Hash);"),
        new(102, 1, "UPDATE AniDB_File SET File_Source = 'Web' WHERE File_Source = 'www'; UPDATE AniDB_File SET File_Source = 'BluRay' WHERE File_Source = 'Blu-ray'; UPDATE AniDB_File SET File_Source = 'LaserDisc' WHERE File_Source = 'LD'; UPDATE AniDB_File SET File_Source = 'Unknown' WHERE File_Source = 'unknown';"),
        new (103, 1, "ALTER TABLE AniDB_GroupStatus MODIFY GroupName LONGTEXT NULL; ALTER TABLE AniDB_GroupStatus MODIFY EpisodeRange LONGTEXT NULL;"),
        new(104, 1, "ALTER TABLE AniDB_Episode ADD INDEX IX_AniDB_Episode_EpisodeType (EpisodeType);"),
        new(105, 1, "ALTER TABLE AniDB_Episode_Title MODIFY Title TEXT NOT NULL"),
        new(106, 1, "ALTER TABLE VideoLocal ADD DateTimeImported datetime DEFAULT NULL;"),
        new(106, 2, "UPDATE VideoLocal v INNER JOIN CrossRef_File_Episode CRFE on v.Hash = CRFE.Hash SET DateTimeImported = DateTimeCreated;"),
        new(107, 1, "ALTER TABLE AniDB_Tag ADD Verified integer NOT NULL DEFAULT 0;"),
        new(107, 2, "ALTER TABLE AniDB_Tag ADD ParentTagID integer DEFAULT NULL;"),
        new(107, 3, "ALTER TABLE AniDB_Tag ADD TagNameOverride varchar(150) DEFAULT NULL;"),
        new(107, 4, "ALTER TABLE AniDB_Tag ADD LastUpdated datetime NOT NULL DEFAULT '1970-01-01 00:00:00';"),
        new(107, 5, "ALTER TABLE AniDB_Tag DROP COLUMN Spoiler;"),
        new(107, 6, "ALTER TABLE AniDB_Tag DROP COLUMN LocalSpoiler;"),
        new(107, 7, "ALTER TABLE AniDB_Tag DROP COLUMN TagCount;"),
        new(107, 8, "ALTER TABLE AniDB_Anime_Tag ADD LocalSpoiler integer NOT NULL DEFAULT 0;"),
        new(107, 9, "ALTER TABLE AniDB_Anime_Tag DROP COLUMN Approval;"),
        new(107, 10, DatabaseFixes.FixTagParentIDsAndNameOverrides),
        new(108, 1, "ALTER TABLE AnimeEpisode ADD IsHidden integer NOT NULL DEFAULT 0;"),
        new(108, 2, "ALTER TABLE AnimeSeries_User ADD HiddenUnwatchedEpisodeCount integer NOT NULL DEFAULT 0;"),
        new(109, 1, "UPDATE VideoLocal v INNER JOIN CrossRef_File_Episode CRFE on v.Hash = CRFE.Hash SET DateTimeImported = DateTimeCreated;"),
        new(110, 1, "CREATE TABLE `AniDB_FileUpdate` ( `AniDB_FileUpdateID` INT NOT NULL AUTO_INCREMENT, `FileSize` BIGINT NOT NULL, `Hash` varchar(50) NOT NULL, `HasResponse` BIT NOT NULL, `UpdatedAt` datetime NOT NULL, PRIMARY KEY (`AniDB_FileUpdateID`) );"),
        new(110, 2, "ALTER TABLE `AniDB_FileUpdate` ADD INDEX `IX_AniDB_FileUpdate` (`FileSize` ASC, `Hash` ASC) ;"),
        new(110, 3, DatabaseFixes.MigrateAniDB_FileUpdates),
        new(111, 1, "ALTER TABLE AniDB_Anime DROP COLUMN DisableExternalLinksFlag;"),
        new(111, 2, "ALTER TABLE AnimeSeries ADD DisableAutoMatchFlags integer NOT NULL DEFAULT 0;"),
        new(111, 3, "ALTER TABLE `AniDB_Anime` ADD ( `VNDBID` INT NULL, `BangumiID` INT NULL, `LianID` INT NULL, `FunimationID` text character set utf8 null, `HiDiveID` text character set utf8 null );"),
        new(112, 1, "ALTER TABLE AniDB_Anime DROP COLUMN LianID;"),
        new(112, 2, "ALTER TABLE AniDB_Anime DROP COLUMN AnimePlanetID;"),
        new(112, 3, "ALTER TABLE AniDB_Anime DROP COLUMN AnimeNfo;"),
        new(112, 4, "ALTER TABLE AniDB_Anime ADD LainID INT NULL"),
        new(113, 1, DatabaseFixes.FixEpisodeDateTimeUpdated),
        new(114, 1, "ALTER TABLE AnimeSeries ADD HiddenMissingEpisodeCount INT NOT NULL DEFAULT 0;"),
        new(114, 2, "ALTER TABLE AnimeSeries ADD HiddenMissingEpisodeCountGroups INT NOT NULL DEFAULT 0;"),
        new(114, 3, DatabaseFixes.UpdateSeriesWithHiddenEpisodes),
        new(115, 1, "UPDATE AniDB_Anime SET AirDate = NULL, BeginYear = 0 WHERE AirDate = '1970-01-01 00:00:00';"),
        new(116, 1, "ALTER TABLE JMMUser ADD AvatarImageBlob BLOB NULL;"),
        new(116, 2, "ALTER TABLE JMMUser ADD AvatarImageMetadata VARCHAR(128) NULL;"),
        new(117, 1, "ALTER TABLE VideoLocal ADD LastAVDumped datetime;"),
        new(117, 2, "ALTER TABLE VideoLocal ADD LastAVDumpVersion nvarchar(128);"),
        new(118, 1, DatabaseFixes.FixAnimeSourceLinks),
        new(118, 2, DatabaseFixes.FixOrphanedShokoEpisodes),
        new(119, 1, "CREATE TABLE FilterPreset( FilterPresetID INT NOT NULL AUTO_INCREMENT, ParentFilterPresetID int, Name text NOT NULL, FilterType int NOT NULL, Locked bit NOT NULL, Hidden bit NOT NULL, ApplyAtSeriesLevel bit NOT NULL, Expression longtext, SortingExpression longtext, PRIMARY KEY (`FilterPresetID`) ); "),
        new(119, 2, "ALTER TABLE FilterPreset ADD INDEX IX_FilterPreset_ParentFilterPresetID (ParentFilterPresetID); ALTER TABLE FilterPreset ADD INDEX IX_FilterPreset_Name (Name(255)); ALTER TABLE FilterPreset ADD INDEX IX_FilterPreset_FilterType (FilterType); ALTER TABLE FilterPreset ADD INDEX IX_FilterPreset_LockedHidden (Locked, Hidden);"),
        new(119, 3, "DELETE FROM GroupFilter WHERE FilterType = 2; DELETE FROM GroupFilter WHERE FilterType = 16;"),
        new(119, 4, DatabaseFixes.MigrateGroupFilterToFilterPreset),
        new(119, 5, DatabaseFixes.DropGroupFilter),
        new(120, 1, "SET @exist_Check := (SELECT count(1) FROM information_schema.columns WHERE TABLE_NAME='AnimeGroup' AND COLUMN_NAME='SortName' AND TABLE_SCHEMA=database()) ; SET @sqlstmt := IF(@exist_Check>0,'ALTER TABLE AnimeGroup DROP COLUMN SortName', 'SELECT ''''') ; PREPARE stmt FROM @sqlstmt ; EXECUTE stmt ;"),
        new(121, 1, "SET @exist_Check := (SELECT count(1) FROM information_schema.columns WHERE TABLE_NAME='AnimeEpisode' AND COLUMN_NAME='PlexContractVersion' AND TABLE_SCHEMA=database()) ; SET @sqlstmt := IF(@exist_Check>0,'ALTER TABLE AnimeEpisode DROP COLUMN PlexContractVersion', 'SELECT ''''') ; PREPARE stmt FROM @sqlstmt ; EXECUTE stmt ;"),
        new(121, 2, "SET @exist_Check := (SELECT count(1) FROM information_schema.columns WHERE TABLE_NAME='AnimeEpisode' AND COLUMN_NAME='PlexContractBlob' AND TABLE_SCHEMA=database()) ; SET @sqlstmt := IF(@exist_Check>0,'ALTER TABLE AnimeEpisode DROP COLUMN PlexContractBlob', 'SELECT ''''') ; PREPARE stmt FROM @sqlstmt ; EXECUTE stmt ;"),
        new(121, 3, "SET @exist_Check := (SELECT count(1) FROM information_schema.columns WHERE TABLE_NAME='AnimeEpisode' AND COLUMN_NAME='PlexContractSize' AND TABLE_SCHEMA=database()) ; SET @sqlstmt := IF(@exist_Check>0,'ALTER TABLE AnimeEpisode DROP COLUMN PlexContractSize', 'SELECT ''''') ; PREPARE stmt FROM @sqlstmt ; EXECUTE stmt ;"),
        new(121, 4, "SET @exist_Check := (SELECT count(1) FROM information_schema.columns WHERE TABLE_NAME='AnimeGroup_User' AND COLUMN_NAME='PlexContractVersion' AND TABLE_SCHEMA=database()) ; SET @sqlstmt := IF(@exist_Check>0,'ALTER TABLE AnimeGroup_User DROP COLUMN PlexContractVersion', 'SELECT ''''') ; PREPARE stmt FROM @sqlstmt ; EXECUTE stmt ;"),
        new(121, 5, "SET @exist_Check := (SELECT count(1) FROM information_schema.columns WHERE TABLE_NAME='AnimeGroup_User' AND COLUMN_NAME='PlexContractBlob' AND TABLE_SCHEMA=database()) ; SET @sqlstmt := IF(@exist_Check>0,'ALTER TABLE AnimeGroup_User DROP COLUMN PlexContractBlob', 'SELECT ''''') ; PREPARE stmt FROM @sqlstmt ; EXECUTE stmt ;"),
        new(121, 6, "SET @exist_Check := (SELECT count(1) FROM information_schema.columns WHERE TABLE_NAME='AnimeGroup_User' AND COLUMN_NAME='PlexContractSize' AND TABLE_SCHEMA=database()) ; SET @sqlstmt := IF(@exist_Check>0,'ALTER TABLE AnimeGroup_User DROP COLUMN PlexContractSize', 'SELECT ''''') ; PREPARE stmt FROM @sqlstmt ; EXECUTE stmt ;"),
        new(121, 7, "SET @exist_Check := (SELECT count(1) FROM information_schema.columns WHERE TABLE_NAME='AnimeSeries_User' AND COLUMN_NAME='PlexContractVersion' AND TABLE_SCHEMA=database()) ; SET @sqlstmt := IF(@exist_Check>0,'ALTER TABLE AnimeSeries_User DROP COLUMN PlexContractVersion', 'SELECT ''''') ; PREPARE stmt FROM @sqlstmt ; EXECUTE stmt ;"),
        new(121, 8, "SET @exist_Check := (SELECT count(1) FROM information_schema.columns WHERE TABLE_NAME='AnimeSeries_User' AND COLUMN_NAME='PlexContractBlob' AND TABLE_SCHEMA=database()) ; SET @sqlstmt := IF(@exist_Check>0,'ALTER TABLE AnimeSeries_User DROP COLUMN PlexContractBlob', 'SELECT ''''') ; PREPARE stmt FROM @sqlstmt ; EXECUTE stmt ;"),
        new(121, 9, "SET @exist_Check := (SELECT count(1) FROM information_schema.columns WHERE TABLE_NAME='AnimeSeries_User' AND COLUMN_NAME='PlexContractSize' AND TABLE_SCHEMA=database()) ; SET @sqlstmt := IF(@exist_Check>0,'ALTER TABLE AnimeSeries_User DROP COLUMN PlexContractSize', 'SELECT ''''') ; PREPARE stmt FROM @sqlstmt ; EXECUTE stmt ;"),
        new(122, 1, "ALTER TABLE CommandRequest ADD INDEX IX_CommandRequest_CommandType (CommandType); ALTER TABLE CommandRequest ADD INDEX IX_CommandRequest_Priority_Date (Priority, DateTimeUpdated);"),
        new(123, 1, "DROP TABLE CommandRequest"),
        new(124, 1, "ALTER TABLE `AnimeEpisode` ADD `EpisodeNameOverride` text NULL;"),
        new(125, 1, "DELETE FROM FilterPreset WHERE FilterType IN (16, 24, 32, 40, 64, 72)"),
        new(126, 1, "ALTER TABLE AniDB_Anime DROP COLUMN ContractVersion;ALTER TABLE AniDB_Anime DROP COLUMN ContractBlob;ALTER TABLE AniDB_Anime DROP COLUMN ContractSize;"),
        new(126, 2, "ALTER TABLE AnimeSeries DROP COLUMN ContractVersion;ALTER TABLE AnimeSeries DROP COLUMN ContractBlob;ALTER TABLE AnimeSeries DROP COLUMN ContractSize;"),
        new(126, 3, "ALTER TABLE AnimeGroup DROP COLUMN ContractVersion;ALTER TABLE AnimeGroup DROP COLUMN ContractBlob;ALTER TABLE AnimeGroup DROP COLUMN ContractSize;"),
        new(127, 1, "ALTER TABLE VideoLocal DROP COLUMN MediaSize;"),
        new(128, 1, "CREATE TABLE `AniDB_NotifyQueue` ( `AniDB_NotifyQueueID` INT NOT NULL AUTO_INCREMENT, `Type` int NOT NULL, `ID` int NOT NULL, `AddedAt` datetime NOT NULL, PRIMARY KEY (`AniDB_NotifyQueueID`) ) ; "),
        new(128, 2, "CREATE TABLE `AniDB_Message` ( `AniDB_MessageID` INT NOT NULL AUTO_INCREMENT, `MessageID` int NOT NULL, `FromUserID` int NOT NULL, `FromUserName` varchar(100) character set utf8 NOT NULL, `SentAt` datetime NOT NULL, `FetchedAt` datetime NOT NULL, `Type` int NOT NULL, `Title` text character set utf8 NOT NULL, `Body` text character set utf8 NOT NULL, `Flags` int NOT NULL DEFAULT 0, PRIMARY KEY (`AniDB_MessageID`) ) ;"),
        new(129, 1, "CREATE TABLE `CrossRef_AniDB_TMDB_Episode` ( `CrossRef_AniDB_TMDB_EpisodeID` INT NOT NULL AUTO_INCREMENT, `AnidbAnimeID` INT NOT NULL, `AnidbEpisodeID` INT NOT NULL, `TmdbShowID` INT NOT NULL, `TmdbEpisodeID` INT NOT NULL, `Ordering` INT NOT NULL, `MatchRating` INT NOT NULL, PRIMARY KEY (`CrossRef_AniDB_TMDB_EpisodeID`) );"),
        new(129, 2, "CREATE TABLE `CrossRef_AniDB_TMDB_Movie` ( `CrossRef_AniDB_TMDB_MovieID` INT NOT NULL AUTO_INCREMENT, `AnidbAnimeID` INT NOT NULL, `AnidbEpisodeID` INT NULL, `TmdbMovieID` INT NOT NULL, `Source` INT NOT NULL, PRIMARY KEY (`CrossRef_AniDB_TMDB_MovieID`) );"),
        new(129, 3, "CREATE TABLE `CrossRef_AniDB_TMDB_Show` ( `CrossRef_AniDB_TMDB_ShowID` INT NOT NULL AUTO_INCREMENT, `AnidbAnimeID` INT NOT NULL, `TmdbShowID` INT NOT NULL, `Source` INT NOT NULL, PRIMARY KEY (`CrossRef_AniDB_TMDB_ShowID`) );"),
        new(129, 4, "CREATE TABLE `TMDB_Image` ( `TMDB_ImageID` INT NOT NULL AUTO_INCREMENT, `TmdbMovieID` INT NULL, `TmdbEpisodeID` INT NULL, `TmdbSeasonID` INT NULL, `TmdbShowID` INT NULL, `TmdbCollectionID` INT NULL, `TmdbNetworkID` INT NULL, `TmdbCompanyID` INT NULL, `TmdbPersonID` INT NULL, `ForeignType` INT NOT NULL, `ImageType` INT NOT NULL, `IsEnabled` INT NOT NULL, `Width` INT NOT NULL, `Height` INT NOT NULL, `Language` VARCHAR(32) CHARACTER SET UTF8 NOT NULL, `RemoteFileName` VARCHAR(128) CHARACTER SET UTF8 NOT NULL, `UserRating` decimal(6,2) NOT NULL, `UserVotes` INT NOT NULL, PRIMARY KEY (`TMDB_ImageID`) );"),
        new(129, 5, "CREATE TABLE `AniDB_Anime_PreferredImage` ( `AniDB_Anime_PreferredImageID` INT NOT NULL AUTO_INCREMENT, `AnidbAnimeID` INT NOT NULL, `ImageID` INT NOT NULL, `ImageType` INT NOT NULL, `ImageSource` INT NOT NULL, PRIMARY KEY (`AniDB_Anime_PreferredImageID`) );"),
        new(129, 6, "CREATE TABLE `TMDB_Title` ( `TMDB_TitleID` INT NOT NULL AUTO_INCREMENT, `ParentID` INT NOT NULL, `ParentType` INT NOT NULL, `LanguageCode` VARCHAR(5) CHARACTER SET UTF8 NOT NULL, `CountryCode` VARCHAR(5) CHARACTER SET UTF8 NOT NULL, `Value`  VARCHAR(512) CHARACTER SET UTF8 NOT NULL, PRIMARY KEY (`TMDB_TitleID`) );"),
        new(129, 7, "CREATE TABLE `TMDB_Overview` ( `TMDB_OverviewID` INT NOT NULL AUTO_INCREMENT, `ParentID` INT NOT NULL, `ParentType` INT NOT NULL, `LanguageCode` VARCHAR(5) CHARACTER SET UTF8 NOT NULL, `CountryCode` VARCHAR(5) CHARACTER SET UTF8 NOT NULL, `Value` TEXT CHARACTER SET UTF8 NOT NULL, PRIMARY KEY (`TMDB_OverviewID`) );"),
        new(129, 8, "CREATE TABLE `TMDB_Company` ( `TMDB_CompanyID` INT NOT NULL AUTO_INCREMENT, `TmdbCompanyID` INT NOT NULL, `Name` VARCHAR(512) CHARACTER SET UTF8 NOT NULL, `CountryOfOrigin` VARCHAR(3) CHARACTER SET UTF8 NOT NULL, PRIMARY KEY (`TMDB_CompanyID`) );"),
        new(129, 9, "CREATE TABLE `TMDB_Network` ( `TMDB_NetworkID` INT NOT NULL AUTO_INCREMENT, `TmdbNetworkID` INT NOT NULL, `Name` VARCHAR(512) CHARACTER SET UTF8 NOT NULL, `CountryOfOrigin` VARCHAR(3) CHARACTER SET UTF8 NOT NULL, PRIMARY KEY (`TMDB_NetworkID`) );"),
        new(129, 10, "CREATE TABLE `TMDB_Person` ( `TMDB_PersonID` INT NOT NULL AUTO_INCREMENT, `TmdbPersonID` INT NOT NULL, `EnglishName` VARCHAR(512) CHARACTER SET UTF8 NOT NULL, `EnglishBiography` TEXT CHARACTER SET UTF8 NOT NULL, `Aliases` TEXT CHARACTER SET UTF8 NOT NULL, `Gender` INT NOT NULL, `IsRestricted` BIT NOT NULL, `BirthDay` DATE NULL, `DeathDay` DATE NULL, `PlaceOfBirth` VARCHAR(128) CHARACTER SET UTF8 NULL, `CreatedAt` DATETIME NOT NULL, `LastUpdatedAt` DATETIME NOT NULL, PRIMARY KEY (`TMDB_PersonID`) );"),
        new(129, 11, "CREATE TABLE `TMDB_Movie` ( `TMDB_MovieID` INT NOT NULL AUTO_INCREMENT, `TmdbMovieID` INT NOT NULL, `TmdbCollectionID` INT NULL, `EnglishTitle` VARCHAR(512) CHARACTER SET UTF8 NOT NULL, `EnglishOverview` TEXT CHARACTER SET UTF8 NOT NULL, `OriginalTitle` VARCHAR(512) CHARACTER SET UTF8 NOT NULL, `OriginalLanguageCode` VARCHAR(5) CHARACTER SET UTF8 NOT NULL, `IsRestricted` BIT NOT NULL, `IsVideo` BIT NOT NULL, `Genres` VARCHAR(128) CHARACTER SET UTF8 NOT NULL, `ContentRatings` VARCHAR(128) CHARACTER SET UTF8 NOT NULL, `Runtime` INT NULL, `UserRating` decimal(6,2) NOT NULL, `UserVotes` INT NOT NULL, `ReleasedAt` DATE NULL, `CreatedAt` DATETIME NOT NULL, `LastUpdatedAt` DATETIME NOT NULL, PRIMARY KEY (`TMDB_MovieID`) );"),
        new(129, 12, "CREATE TABLE `TMDB_Movie_Cast` ( `TMDB_Movie_CastID` INT NOT NULL AUTO_INCREMENT, `TmdbMovieID` INT NOT NULL, `TmdbPersonID` INT NOT NULL, `TmdbCreditID` VARCHAR(64) CHARACTER SET UTF8 NOT NULL, `CharacterName` VARCHAR(512) CHARACTER SET UTF8 NOT NULL, `Ordering` INT NOT NULL, PRIMARY KEY (`TMDB_Movie_CastID`) );"),
        new(129, 13, "CREATE TABLE `TMDB_Company_Entity` ( `TMDB_Company_EntityID` INT NOT NULL AUTO_INCREMENT, `TmdbCompanyID` INT NOT NULL, `TmdbEntityType` INT NOT NULL, `TmdbEntityID` INT NOT NULL, `Ordering` INT NOT NULL, `ReleasedAt` DATE NULL, PRIMARY KEY (`TMDB_Company_EntityID`) );"),
        new(129, 14, "CREATE TABLE `TMDB_Movie_Crew` ( `TMDB_Movie_CrewID` INT NOT NULL AUTO_INCREMENT, `TmdbMovieID` INT NOT NULL, `TmdbPersonID` INT NOT NULL, `TmdbCreditID` VARCHAR(64) CHARACTER SET UTF8 NOT NULL, `Job` VARCHAR(64) CHARACTER SET UTF8 NOT NULL, `Department` VARCHAR(64) CHARACTER SET UTF8 NOT NULL, PRIMARY KEY (`TMDB_Movie_CrewID`) );"),
        new(129, 15, "CREATE TABLE `TMDB_Show` ( `TMDB_ShowID` INT NOT NULL AUTO_INCREMENT, `TmdbShowID` INT NOT NULL, `EnglishTitle` VARCHAR(512) CHARACTER SET UTF8 NOT NULL, `EnglishOverview` TEXT CHARACTER SET UTF8 NOT NULL, `OriginalTitle` VARCHAR(512) CHARACTER SET UTF8 NOT NULL, `OriginalLanguageCode` VARCHAR(5) CHARACTER SET UTF8 NOT NULL, `IsRestricted` BIT NOT NULL, `Genres` VARCHAR(128) CHARACTER SET UTF8 NOT NULL, `ContentRatings` VARCHAR(128) CHARACTER SET UTF8 NOT NULL, `EpisodeCount` INT NOT NULL, `SeasonCount` INT NOT NULL, `AlternateOrderingCount` INT NOT NULL, `UserRating` decimal(6,2) NOT NULL, `UserVotes` INT NOT NULL, `FirstAiredAt` DATE, `LastAiredAt` DATE NULL, `CreatedAt` DATETIME NOT NULL, `LastUpdatedAt` DATETIME NOT NULL, PRIMARY KEY (`TMDB_ShowID`) );"),
        new(129, 16, "CREATE TABLE `Tmdb_Show_Network` ( `TMDB_Show_NetworkID` INT NOT NULL AUTO_INCREMENT, `TmdbShowID` INT NOT NULL, `TmdbNetworkID` INT NOT NULL, `Ordering` INT NOT NULL, PRIMARY KEY (`TMDB_Show_NetworkID`) );"),
        new(129, 17, "CREATE TABLE `TMDB_Season` ( `TMDB_SeasonID` INT NOT NULL AUTO_INCREMENT, `TmdbShowID` INT NOT NULL, `TmdbSeasonID` INT NOT NULL, `EnglishTitle` VARCHAR(512) CHARACTER SET UTF8 NOT NULL, `EnglishOverview` TEXT CHARACTER SET UTF8 NOT NULL, `EpisodeCount` INT NOT NULL, `SeasonNumber` INT NOT NULL, `CreatedAt` DATETIME NOT NULL, `LastUpdatedAt` DATETIME NOT NULL, PRIMARY KEY (`TMDB_SeasonID`) );"),
        new(129, 18, "CREATE TABLE `TMDB_Episode` ( `TMDB_EpisodeID` INT NOT NULL AUTO_INCREMENT, `TmdbShowID` INT NOT NULL, `TmdbSeasonID` INT NOT NULL, `TmdbEpisodeID` INT NOT NULL, `EnglishTitle` VARCHAR(512) CHARACTER SET UTF8 NOT NULL, EnglishOverview TEXT CHARACTER SET UTF8 NOT NULL, `SeasonNumber` INT NOT NULL, `EpisodeNumber` INT NOT NULL, `Runtime` INT NULL, `UserRating` decimal(6,2) NOT NULL, `UserVotes` INT NOT NULL, `AiredAt` DATE NULL, `CreatedAt` DATETIME NOT NULL, `LastUpdatedAt` DATETIME NOT NULL, PRIMARY KEY (`TMDB_EpisodeID`) );"),
        new(129, 19, "CREATE TABLE `TMDB_Episode_Cast` ( `TMDB_Episode_CastID` INT NOT NULL AUTO_INCREMENT, `TmdbShowID` INT NOT NULL, `TmdbSeasonID` INT NOT NULL, `TmdbEpisodeID` INT NOT NULL, `TmdbPersonID` INT NOT NULL, `TmdbCreditID` VARCHAR(64) CHARACTER SET UTF8 NOT NULL, `CharacterName` VARCHAR(512) CHARACTER SET UTF8 NOT NULL, `IsGuestRole` BIT NOT NULL, `Ordering` INT NOT NULL, PRIMARY KEY (`TMDB_Episode_CastID`) );"),
        new(129, 20, "CREATE TABLE `TMDB_Episode_Crew` ( `TMDB_Episode_CrewID` INT NOT NULL AUTO_INCREMENT, `TmdbShowID` INT NOT NULL, `TmdbSeasonID` INT NOT NULL, `TmdbEpisodeID` INT NOT NULL, `TmdbPersonID` INT NOT NULL, `TmdbCreditID` VARCHAR(64) CHARACTER SET UTF8 NOT NULL, `Job` VARCHAR(512) CHARACTER SET UTF8 NOT NULL, `Department` VARCHAR(512) CHARACTER SET UTF8 NOT NULL, PRIMARY KEY (`TMDB_Episode_CrewID`) );"),
        new(129, 21, "CREATE TABLE `TMDB_AlternateOrdering` ( `TMDB_AlternateOrderingID` INT NOT NULL AUTO_INCREMENT, `TmdbShowID` INT NOT NULL, `TmdbNetworkID` INT NULL, `TmdbEpisodeGroupCollectionID` VARCHAR(64) CHARACTER SET UTF8 NOT NULL, `EnglishTitle` VARCHAR(512) CHARACTER SET UTF8 NOT NULL, `EnglishOverview` TEXT CHARACTER SET UTF8 NOT NULL, `EpisodeCount` INT NOT NULL, `SeasonCount` INT NOT NULL, `Type` INT NOT NULL, `CreatedAt` DATETIME NOT NULL, `LastUpdatedAt` DATETIME NOT NULL, PRIMARY KEY (`TMDB_AlternateOrderingID`) );"),
        new(129, 22, "CREATE TABLE `TMDB_AlternateOrdering_Season` ( `TMDB_AlternateOrdering_SeasonID` INT NOT NULL AUTO_INCREMENT, `TmdbShowID` INT NOT NULL, `TmdbEpisodeGroupCollectionID` VARCHAR(64) CHARACTER SET UTF8 NOT NULL, `TmdbEpisodeGroupID` VARCHAR(64) CHARACTER SET UTF8 NOT NULL, `EnglishTitle` VARCHAR(512) CHARACTER SET UTF8 NOT NULL, `SeasonNumber` INT NOT NULL, `EpisodeCount` INT NOT NULL, `IsLocked` BIT NOT NULL, `CreatedAt` DATETIME NOT NULL, `LastUpdatedAt` DATETIME NOT NULL, PRIMARY KEY (`TMDB_AlternateOrdering_SeasonID`) );"),
        new(129, 23, "CREATE TABLE `TMDB_AlternateOrdering_Episode` ( `TMDB_AlternateOrdering_EpisodeID` INT NOT NULL AUTO_INCREMENT, `TmdbShowID` INT NOT NULL, `TmdbEpisodeGroupCollectionID` VARCHAR(64) CHARACTER SET UTF8 NOT NULL, `TmdbEpisodeGroupID` VARCHAR(64) CHARACTER SET UTF8 NOT NULL, `TmdbEpisodeID` INT NOT NULL, `SeasonNumber` INT NOT NULL, `EpisodeNumber` INT NOT NULL, `CreatedAt` DATETIME NOT NULL, `LastUpdatedAt` DATETIME NOT NULL, PRIMARY KEY (`TMDB_AlternateOrdering_EpisodeID`) );"),
        new(129, 24, "CREATE TABLE `TMDB_Collection` ( `TMDB_CollectionID` INT NOT NULL AUTO_INCREMENT, `TmdbCollectionID` INT NOT NULL, `EnglishTitle` VARCHAR(512) CHARACTER SET UTF8 NOT NULL, `EnglishOverview` TEXT CHARACTER SET UTF8 NOT NULL, `MovieCount` INT NOT NULL, `CreatedAt` DATETIME NOT NULL, `LastUpdatedAt` DATETIME NOT NULL, PRIMARY KEY (`TMDB_CollectionID`) );"),
        new(129, 25, "CREATE TABLE `TMDB_Collection_Movie` ( `TMDB_Collection_MovieID` INT NOT NULL AUTO_INCREMENT, `TmdbCollectionID` INT NOT NULL, `TmdbMovieID` INT NOT NULL, `Ordering` INT NOT NULL, PRIMARY KEY (`TMDB_Collection_MovieID`) );"),
        new(129, 26, "INSERT INTO `CrossRef_AniDB_TMDB_Movie` ( AnidbAnimeID, TmdbMovieID, Source ) SELECT AnimeID, CrossRefID, CrossRefSource FROM `CrossRef_AniDB_Other` WHERE CrossRefType = 1;"),
        new(129, 27, "DROP TABLE `CrossRef_AniDB_Other`;"),
        new(129, 28, "DROP TABLE `MovieDB_Fanart`;"),
        new(129, 29, "DROP TABLE `MovieDB_Movie`;"),
        new(129, 30, "DROP TABLE `MovieDB_Poster`;"),
        new(129, 31, "DROP TABLE `AniDB_Anime_DefaultImage`;"),
        new(129, 32, "CREATE TABLE `AniDB_Episode_PreferredImage` ( `AniDB_Episode_PreferredImageID` INT NOT NULL AUTO_INCREMENT, `AnidbAnimeID` INT NOT NULL, `AnidbEpisodeID` INT NOT NULL, `ImageID` INT NOT NULL, `ImageType` INT NOT NULL, `ImageSource` INT NOT NULL, PRIMARY KEY (`AniDB_Episode_PreferredImageID`) );"),
        new(129, 33, DatabaseFixes.CleanupAfterAddingTMDB),
        new(129, 34, "UPDATE FilterPreset SET Expression = REPLACE(Expression, 'HasTMDbLinkExpression', 'HasTmdbLinkExpression');"),
        new(129, 35, "ALTER TABLE `TMDB_Movie` CHANGE COLUMN IF EXISTS `EnglishOvervie` `EnglishOverview` TEXT CHARACTER SET UTF8 NOT NULL;"),
        new(129, 36, "UPDATE `TMDB_Image` SET `IsEnabled` = 1;"),
        new(130, 1, MigrateRenamers),
        new(131, 1, "DELETE FROM RenamerInstance WHERE NAME = 'AAA_WORKINGFILE_TEMP_AAA';"),
        new(131, 2, DatabaseFixes.CreateDefaultRenamerConfig)
    };

    private DatabaseCommand linuxTableVersionsFix = new("RENAME TABLE versions TO Versions;");


    private List<DatabaseCommand> linuxTableFixes = new()
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
        new DatabaseCommand("RENAME TABLE videolocal_user TO VideoLocal_User;")
    };


    private List<DatabaseCommand> updateVersionTable = new()
    {
        new DatabaseCommand("ALTER TABLE `Versions` ADD `VersionRevision` varchar(100) NULL;"),
        new DatabaseCommand("ALTER TABLE `Versions` ADD `VersionCommand` text NULL;"),
        new DatabaseCommand("ALTER TABLE `Versions` ADD `VersionProgram` varchar(100) NULL;"),
        new DatabaseCommand("ALTER TABLE `Versions` DROP INDEX `UIX_Versions_VersionType` ;"),
        new DatabaseCommand(
            "ALTER TABLE `Versions` ADD INDEX `IX_Versions_VersionType` (`VersionType`,`VersionValue`,`VersionRevision`);")
    };

    public override void BackupDatabase(string fullfilename)
    {
        fullfilename += ".sql";
        using (var conn = new MySqlConnection(GetConnectionString()))
        {
            using (var cmd = new MySqlCommand())
            {
                using (var mb = new MySqlBackup(cmd))
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
                                         CREATE TABLE IF NOT EXISTS RenamerInstance (ID INT NOT NULL AUTO_INCREMENT, Name text NOT NULL, Type text NOT NULL, Settings mediumblob, PRIMARY KEY (ID));
                                         ALTER TABLE RenamerInstance ADD INDEX IX_RenamerInstance_Name (Name);
                                         ALTER TABLE RenamerInstance ADD INDEX IX_RenamerInstance_Type (Type);
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

    public static void DropMALIndex()
    {
        MySQL mysql = new();
        using MySqlConnection conn = new(mysql.GetConnectionString());
        conn.Open();
        var query =
            @"SELECT 1 FROM INFORMATION_SCHEMA.STATISTICS WHERE TABLE_NAME = 'CrossRef_AniDB_MAL' AND INDEX_NAME = 'UIX_CrossRef_AniDB_MAL_MALID';";
        MySqlCommand cmd = new(query, conn);
        var result = cmd.ExecuteScalar();
        // not exists
        if (result == null)
        {
            return;
        }

        query = "DROP INDEX `UIX_CrossRef_AniDB_MAL_MALID` ON `CrossRef_AniDB_MAL`;";
        cmd = new MySqlCommand(query, conn);
        cmd.ExecuteScalar();
    }

    public static void DropAniDBUniqueIndex()
    {
        MySQL mysql = new();
        using MySqlConnection conn = new(mysql.GetConnectionString());
        conn.Open();
        var query =
            @"SELECT 1 FROM INFORMATION_SCHEMA.STATISTICS WHERE TABLE_NAME = 'AniDB_File' AND INDEX_NAME = 'UIX_AniDB_File_FileID';";
        MySqlCommand cmd = new(query, conn);
        var result = cmd.ExecuteScalar();
        // not exists
        if (result == null)
        {
            return;
        }

        query = "DROP INDEX `UIX_AniDB_File_FileID` ON `AniDB_File`;";
        cmd = new MySqlCommand(query, conn);
        cmd.ExecuteScalar();
    }

    public override bool TestConnection()
    {
        try
        {
            using (var conn = new MySqlConnection(GetTestConnectionString()))
            {
                var query = "select 1";
                var cmd = new MySqlCommand(query, conn);
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
        using (var scommand = new MySqlCommand(command, connection))
        {
            scommand.CommandTimeout = 0;
            scommand.ExecuteNonQuery();
        }
    }

    protected override long ExecuteScalar(MySqlConnection connection, string command)
    {
        using (var cmd = new MySqlCommand(command, connection))
        {
            cmd.CommandTimeout = 0;
            var result = cmd.ExecuteScalar();
            return long.Parse(result.ToString());
        }
    }

    protected override List<object[]> ExecuteReader(MySqlConnection connection, string command)
    {
        using var cmd = new MySqlCommand(command, connection);
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

    protected override void ConnectionWrapper(string connectionstring, Action<MySqlConnection> action)
    {
        using var conn = new MySqlConnection(connectionstring);
        conn.Open();
        action(conn);
    }

    public override string GetConnectionString()
    {
        var settings = Utils.SettingsProvider.GetSettings();
        return
            $"Server={settings.Database.Hostname};Port={settings.Database.Port};Database={settings.Database.Schema};User ID={settings.Database.Username};Password={settings.Database.Password};Default Command Timeout=3600;Allow User Variables=true";
    }

    public override string GetTestConnectionString()
    {
        var settings = Utils.SettingsProvider.GetSettings();
        return
            $"Server={settings.Database.Hostname};Port={settings.Database.Port};Database=information_schema;User ID={settings.Database.Username};Password={settings.Database.Password};Default Command Timeout=3600";
    }

    public override bool HasVersionsTable()
    {
        var connStr = GetConnectionString();

        const string sql = "SELECT COUNT(*) FROM information_schema.tables WHERE table_name = 'Versions'";
        using var conn = new MySqlConnection(connStr);
        var com = new MySqlCommand(sql, conn);
        conn.Open();
        var count = (long)com.ExecuteScalar();
        return count > 0;
    }

    public override ISessionFactory CreateSessionFactory()
    {
        var settings = Utils.SettingsProvider.GetSettings();
        return Fluently.Configure()
            .Database(MySQLConfiguration.Standard
                .ConnectionString(x => x.Database(settings.Database.Schema)
                    .Server(settings.Database.Hostname)
                    .Port(settings.Database.Port)
                    .Username(settings.Database.Username)
                    .Password(settings.Database.Password))
                .Driver<MySqlConnectorDriver>())
            .Mappings(m => m.FluentMappings.AddFromAssemblyOf<ShokoServer>())
            .ExposeConfiguration(c => c.DataBaseIntegration(prop =>
            {
                // uncomment this for SQL output
                //prop.LogSqlInConsole = true;
            }).SetInterceptor(new NHibernateDependencyInjector(Utils.ServiceContainer)))
            .BuildSessionFactory();
    }

    public override bool DatabaseAlreadyExists()
    {
        var settings = Utils.SettingsProvider.GetSettings();
        try
        {
            var connStr = GetConnectionString();

            var sql =
                $"SELECT SCHEMA_NAME FROM INFORMATION_SCHEMA.SCHEMATA WHERE SCHEMA_NAME = '{settings.Database.Schema}'";
            Logger.Trace(sql);

            using var conn = new MySqlConnection(connStr);
            conn.Open();
            var rows = ExecuteReader(conn, sql);
            if (rows.Count > 0)
            {
                var db = (string)((object[])rows[0])[0];
                Logger.Trace("Found db already exists: {DB}", db);
                return true;
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, ex.ToString());
        }

        Logger.Trace("db does not exist: {0}", settings.Database.Schema);
        return false;
    }

    public override void CreateDatabase()
    {
        var settings = Utils.SettingsProvider.GetSettings();
        try
        {
            if (DatabaseAlreadyExists())
            {
                return;
            }

            var connStr =
                $"Server={settings.Database.Hostname};Port={settings.Database.Port};User ID={settings.Database.Username};Password={settings.Database.Password}";
            Logger.Trace(connStr);
            var sql =
                $"CREATE DATABASE {settings.Database.Schema} DEFAULT CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;";
            Logger.Trace(sql);

            using var conn = new MySqlConnection(connStr);
            conn.Open();
            Execute(conn, sql);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, ex.ToString());
        }
    }


    public override void CreateAndUpdateSchema()
    {
        ConnectionWrapper(GetConnectionString(), myConn =>
        {
            var settings = Utils.SettingsProvider.GetSettings();
            var create = false;
            var fixtablesforlinux = false;
            var count = ExecuteScalar(myConn,
                $"select count(*) from information_schema.tables where table_schema='{settings.Database.Schema}' and table_name = 'Versions'");
            if (count == 0)
            {
                count = ExecuteScalar(myConn,
                    $"select count(*) from information_schema.tables where table_schema='{settings.Database.Schema}' and table_name = 'versions'");
                if (count > 0)
                {
                    fixtablesforlinux = true;
                    ExecuteWithException(myConn, linuxTableVersionsFix);
                }
                else
                {
                    create = true;
                }
            }

            if (create)
            {
                ServerState.Instance.ServerStartingStatus = Resources.Database_CreateSchema;
                ExecuteWithException(myConn, createVersionTable);
            }

            count = ExecuteScalar(myConn,
                $"select count(*) from information_schema.columns where table_schema='{settings.Database.Schema}' and table_name = 'Versions' and column_name = 'VersionRevision'");
            if (count == 0)
            {
                ExecuteWithException(myConn, updateVersionTable);
                AllVersions = RepoFactory.Versions.GetAllByType(Constants.DatabaseTypeKey);
            }

            PreFillVersions(createTables.Union(patchCommands));
            if (create)
            {
                ExecuteWithException(myConn, createTables);
            }

            if (fixtablesforlinux)
            {
                ExecuteWithException(myConn, linuxTableFixes);
            }

            ServerState.Instance.ServerStartingStatus = Resources.Database_ApplySchema;

            ExecuteWithException(myConn, patchCommands);
        });
    }

    private static void MySQLFixUTF8()
    {
        var settings = Utils.SettingsProvider.GetSettings();
        var sql =
            "SELECT `TABLE_SCHEMA`, `TABLE_NAME`, `COLUMN_NAME`, `DATA_TYPE`, `CHARACTER_MAXIMUM_LENGTH` " +
            "FROM information_schema.COLUMNS " +
            $"WHERE table_schema = '{settings.Database.Schema}' " +
            "AND collation_name != 'utf8mb4_unicode_ci'";

        using (var conn = new MySqlConnection(
                   $"Server={settings.Database.Hostname};Port={settings.Database.Port};User ID={settings.Database.Username};Password={settings.Database.Password};database={settings.Database.Schema}"))
        {
            var mySQL = (MySQL)Utils.ServiceContainer.GetRequiredService<DatabaseFactory>().Instance;
            conn.Open();
            var rows = mySQL.ExecuteReader(conn, sql);
            if (rows.Count > 0)
            {
                foreach (object[] row in rows)
                {
                    var alter = "";
                    switch (row[3].ToString().ToLowerInvariant())
                    {
                        case "text":
                        case "mediumtext":
                        case "tinytext":
                        case "longtext":
                            alter =
                                $"ALTER TABLE `{row[1]}` MODIFY `{row[2]}` {row[3]} CHARACTER SET 'utf8mb4' COLLATE 'utf8mb4_unicode_ci'";
                            break;

                        default:
                            alter =
                                $"ALTER TABLE `{row[1]}` MODIFY `{row[2]}` {row[3]}({row[4]}) CHARACTER SET 'utf8mb4' COLLATE 'utf8mb4_unicode_ci'";
                            break;
                    }

                    mySQL.ExecuteCommand(conn, alter);
                }
            }
        }
    }
}
