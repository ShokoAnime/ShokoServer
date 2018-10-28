using Shoko.Server.Models;
using Shoko.Models.Server;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Shoko.Models.Client;

namespace Shoko.Server.Databases
{
    public static class Mappings
    {
        public static void Map(ModelBuilder builder)
        {
            {
                //builder.Ignore<CL_AniDB_AnimeDetailed>();
                //builder.Ignore<CL_AnimeGroup_User>();
                //builder.Ignore<CL_AnimeSeries_User>();
            }
            {
                var model = builder.Entity<SVR_AniDB_Anime>();
                model.ToTable("AniDB_Anime").HasKey(x => x.AniDB_AnimeID);
//                //model.HasIndex(x => x.AnimeID).IsUnique().ForSqlServerIsClustered().HasName("PK_AniDB_AnimeID");
                model.Property(x => x.AnimeID).IsRequired();
                model.Property(x => x.EpisodeCount).IsRequired();
                model.Property(x => x.BeginYear).IsRequired();
                model.Property(x => x.EndYear).IsRequired();
                model.Property(x => x.AnimeType).IsRequired();
                model.Property(x => x.MainTitle).IsRequired().HasMaxLength(500);
                model.Property(x => x.AllTitles).IsRequired().HasMaxLength(1500);
                model.Property(x => x.AllTags).IsRequired();
                model.Property(x => x.Description).IsRequired();
                model.Property(x => x.EpisodeCountNormal).IsRequired();
                model.Property(x => x.EpisodeCountSpecial).IsRequired();
                model.Property(x => x.Rating).IsRequired();
                model.Property(x => x.VoteCount).IsRequired();
                model.Property(x => x.TempRating).IsRequired();
                model.Property(x => x.TempVoteCount).IsRequired();
                model.Property(x => x.AvgReviewRating).IsRequired();
                model.Property(x => x.ReviewCount).IsRequired();
                model.Property(x => x.DateTimeUpdated).IsRequired();
                model.Property(x => x.DateTimeDescUpdated).IsRequired();
                model.Property(x => x.ImageEnabled).IsRequired();
                model.Property(x => x.AwardList).IsRequired();
                model.Property(x => x.Restricted).IsRequired();
                model.Property(x => x.DisableExternalLinksFlag).IsRequired();
                model.Property(x => x.ContractVersion).IsRequired().HasDefaultValue(0);
                model.Property(x => x.ContractSize).IsRequired().HasDefaultValue(0);
                model.Property(x => x.ContractBlob);
                //model.Ignore("SVR_AnimeGroupAnimeGroupID"); //
                model.Ignore(x => x.Contract);
            }
            {
                var model = builder.Entity<AniDB_AnimeUpdate>();
                model.ToTable("AniDB_AnimeUpdate").HasKey(x => x.AniDB_AnimeUpdateID);
//                model.HasIndex(x => x.AnimeID).IsUnique().ForSqlServerIsClustered().HasName("PK_AniDB_AnimeUpdate");
                model.Property(x => x.AnimeID).IsRequired();
                model.Property(x => x.UpdatedAt).IsRequired();
            }
            {
                var model = builder.Entity<AniDB_Episode_Title>();
                model.ToTable("AniDB_Episode_Title").HasKey(x => x.AniDB_Episode_TitleID);
                model.Property(x => x.AniDB_EpisodeID);
                model.Property(x => x.Language);
                model.Property(x => x.Title);
            }
            {
                var model = builder.Entity<AniDB_Anime_Character>();
                model.ToTable("AniDB_Anime_Character").HasKey(x => x.AniDB_Anime_CharacterID);
//                //model.HasIndex(x => x.AniDB_Anime_CharacterID).IsUnique().ForSqlServerIsClustered().HasName("PK_AniDB_Anime_Character");
                model.HasIndex(x => x.AnimeID).HasName("IX_AniDB_Anime_Character_AnimeID");
                model.HasIndex(x => x.CharID).HasName("IX_AniDB_Anime_Character_CharID");
                model.HasIndex(x => new {x.AnimeID, x.CharID}).IsUnique().HasName("UIX_AniDB_Anime_Character_AnimeID_CharID");
                model.Property(x => x.AniDB_Anime_CharacterID).IsRequired().SetLocalValueGenerated();
                model.Property(x => x.AnimeID).IsRequired();
                model.Property(x => x.CharID).IsRequired();
                model.Property(x => x.CharType).IsRequired().HasMaxLength(100);
            }
            {
                var model = builder.Entity<AniDB_Anime_DefaultImage>();
                model.ToTable("AniDB_Anime_DefaultImage").HasKey(x => x.AniDB_Anime_DefaultImageID);
//                //model.HasIndex(x => x.AniDB_Anime_DefaultImageID).IsUnique().ForSqlServerIsClustered().HasName("PK_AniDB_Anime_DefaultImage");
                model.HasIndex(x => new { x.AnimeID, x.ImageType }).IsUnique().HasName("UIX_AniDB_Anime_DefaultImage_ImageType");
                model.Property(x => x.AniDB_Anime_DefaultImageID).IsRequired().SetLocalValueGenerated();
                model.Property(x => x.AnimeID).IsRequired();
                model.Property(x => x.ImageParentID).IsRequired();
                model.Property(x => x.ImageParentType).IsRequired();
                model.Property(x => x.ImageType).IsRequired();
            }
            {
                var model = builder.Entity<AniDB_Anime_Relation>();
                model.ToTable("AniDB_Anime_Relation").HasKey(x => x.AniDB_Anime_RelationID);
//                //model.HasIndex(x => x.AniDB_Anime_RelationID).IsUnique().ForSqlServerIsClustered().HasName("PK_AniDB_Anime_Relation");
                model.HasIndex(x => x.AnimeID).HasName("IX_AniDB_Anime_Relation_AnimeID");
                model.HasIndex(x => new { x.AnimeID, x.RelatedAnimeID }).IsUnique().HasName("UIX_AniDB_Anime_Relation_AnimeID_RelatedAnimeID");
                model.Property(x => x.AniDB_Anime_RelationID).IsRequired().SetLocalValueGenerated();
                model.Property(x => x.AnimeID).IsRequired();
                model.Property(x => x.RelatedAnimeID).IsRequired();
                model.Property(x => x.RelationType).IsRequired().HasMaxLength(100);
            }
            {
                var model = builder.Entity<AniDB_Anime_Review>();
                model.ToTable("AniDB_Anime_Review").HasKey(x => x.AniDB_Anime_ReviewID);
//                //model.HasIndex(x => x.AniDB_Anime_ReviewID).IsUnique().ForSqlServerIsClustered().HasName("PK_AniDB_Anime_Review");
                model.HasIndex(x => x.AnimeID).HasName("IX_AniDB_Anime_Review_AnimeID");
                model.HasIndex(x => new { x.AnimeID, x.ReviewID }).IsUnique().HasName("UIX_AniDB_Anime_Review_AnimeID_ReviewID");
                model.Property(x => x.AniDB_Anime_ReviewID).IsRequired().SetLocalValueGenerated();
                model.Property(x => x.AnimeID).IsRequired();
                model.Property(x => x.ReviewID).IsRequired();
            }
            {
                var model = builder.Entity<AniDB_Anime_Similar>();
                model.ToTable("AniDB_Anime_Similar").HasKey(x => x.AniDB_Anime_SimilarID);
//                //model.HasIndex(x => x.AniDB_Anime_SimilarID).IsUnique().ForSqlServerIsClustered().HasName("PK_AniDB_Anime_Similar");
                model.HasIndex(x => x.AnimeID).HasName("IX_AniDB_Anime_Similar_AnimeID");
                model.HasIndex(x => new { x.AnimeID, x.SimilarAnimeID }).IsUnique().HasName("UIX_AniDB_Anime_Similar_AnimeID_SimilarAnimeID");
                model.Property(x => x.AniDB_Anime_SimilarID).IsRequired().SetLocalValueGenerated();
                model.Property(x => x.AnimeID).IsRequired();
                model.Property(x => x.SimilarAnimeID).IsRequired();
                model.Property(x => x.Approval).IsRequired();
                model.Property(x => x.Total).IsRequired();
            }
            {
                var model = builder.Entity<AniDB_Anime_Tag>();
                model.ToTable("AniDB_Anime_Tag").HasKey(x => x.AniDB_Anime_TagID);
//                //model.HasIndex(x => x.AniDB_Anime_TagID).IsUnique().ForSqlServerIsClustered().HasName("PK_AniDB_Anime_Tag");
                model.HasIndex(x => x.AnimeID).HasName("IX_AniDB_Anime_Tag_AnimeID");
                model.HasIndex(x => new { x.AnimeID, x.TagID }).IsUnique().HasName("UIX_AniDB_Anime_Tag_AnimeID_TagID");
                model.Property(x => x.AniDB_Anime_TagID).IsRequired().SetLocalValueGenerated();
                model.Property(x => x.AnimeID).IsRequired();
                model.Property(x => x.TagID).IsRequired();
                model.Property(x => x.Approval).IsRequired();
            }
            {
                var model = builder.Entity<AniDB_Anime_Title>();
                model.ToTable("AniDB_Anime_Title").HasKey(x => x.AniDB_Anime_TitleID);
//                //model.HasIndex(x => x.AniDB_Anime_TitleID).IsUnique().ForSqlServerIsClustered().HasName("PK_AniDB_Anime_Title");
                model.HasIndex(x => x.AnimeID).HasName("IX_AniDB_Anime_Title_AnimeID");
                model.Property(x => x.AniDB_Anime_TitleID).IsRequired().SetLocalValueGenerated();
                model.Property(x => x.AnimeID).IsRequired();
                model.Property(x => x.TitleType).IsRequired().HasMaxLength(50);
                model.Property(x => x.Language).IsRequired().HasMaxLength(50);
                model.Property(x => x.Title).IsRequired().HasMaxLength(500);
            }
            {
                var model = builder.Entity<AniDB_Character_Seiyuu>();
                model.ToTable("AniDB_Character_Seiyuu").HasKey(x => x.AniDB_Character_SeiyuuID);
//                //model.HasIndex(x => x.AniDB_Character_SeiyuuID).IsUnique().ForSqlServerIsClustered().HasName("PK_AniDB_Character_Seiyuu");
                model.HasIndex(x => x.CharID).HasName("IX_AniDB_Character_Seiyuu_CharID");
                model.HasIndex(x => new {x.CharID, x.SeiyuuID}).IsUnique().HasName("UIX_AniDB_Character_Seiyuu_CharID_SeiyuuID");
                model.Property(x => x.AniDB_Character_SeiyuuID).IsRequired().SetLocalValueGenerated();
                model.Property(x => x.CharID).IsRequired();
                model.Property(x => x.SeiyuuID).IsRequired();
            }
            {
                var model = builder.Entity<AniDB_Character>();
                model.ToTable("AniDB_Character").HasKey(x => x.AniDB_CharacterID);
//                //model.HasIndex(x => x.AniDB_CharacterID).IsUnique().ForSqlServerIsClustered().HasName("PK_AniDB_CharacterID");
                model.Property(x => x.AniDB_CharacterID).IsRequired();
                model.Property(x => x.CharID).IsRequired();
                model.Property(x => x.CharName).IsRequired().HasMaxLength(200);
                model.Property(x => x.PicName).IsRequired().HasMaxLength(100);
                model.Property(x => x.CharKanjiName).IsRequired();
                model.Property(x => x.CharDescription).IsRequired();
                model.Property(x => x.CreatorListRaw).IsRequired();
            }
            {
                var model = builder.Entity<AniDB_Episode>();
                model.ToTable("AniDB_Episode").HasKey(x => x.AniDB_EpisodeID);
//                //model.HasIndex(x => x.AniDB_EpisodeID).IsUnique().ForSqlServerIsClustered().HasName("PK_AniDB_Episode");
                model.HasIndex(x => x.AnimeID).HasName("IX_AniDB_Episode_AnimeID");
                model.HasIndex(x => x.EpisodeID).IsUnique().HasName("UIX_AniDB_Episode_EpisodeID");
                model.Property(x => x.AniDB_EpisodeID).IsRequired().SetLocalValueGenerated();
                model.Property(x => x.EpisodeID).IsRequired();
                model.Property(x => x.AnimeID).IsRequired();
                model.Property(x => x.LengthSeconds).IsRequired();
                model.Property(x => x.Rating).IsRequired();
                model.Property(x => x.Votes).IsRequired();
                model.Property(x => x.EpisodeNumber).IsRequired();
                model.Property(x => x.EpisodeType).IsRequired();
                //model.Property(x => x.RomajiName).IsRequired();
                //model.Property(x => x.EnglishName).IsRequired();
                model.Property(x => x.AirDate).IsRequired();
                model.Property(x => x.DateTimeUpdated).IsRequired();
            }
            {
                var model = builder.Entity<SVR_AniDB_File>();
                model.ToTable("AniDB_File").HasKey(x => x.AniDB_FileID);
//                //model.HasIndex(x => x.FileID).IsUnique().ForSqlServerIsClustered().HasName("PK_AniDB_FileID");
                model.HasIndex(x => x.Hash).IsUnique().HasName("UIX_AniDB_File_Hash");
                model.Property(x => x.FileID).IsRequired();
                model.Property(x => x.Hash).IsRequired().HasMaxLength(50);
                model.Property(x => x.AnimeID).IsRequired();
                model.Property(x => x.GroupID).IsRequired();
                model.Property(x => x.File_Source).IsRequired();
                model.Property(x => x.File_AudioCodec).IsRequired();
                model.Property(x => x.File_VideoCodec).IsRequired();
                model.Property(x => x.File_VideoResolution).IsRequired();
                model.Property(x => x.File_FileExtension).IsRequired();
                model.Property(x => x.File_LengthSeconds).IsRequired();
                model.Property(x => x.File_Description).IsRequired();
                model.Property(x => x.File_ReleaseDate).IsRequired();
                model.Property(x => x.Anime_GroupName).IsRequired();
                model.Property(x => x.Anime_GroupNameShort).IsRequired();
                model.Property(x => x.Episode_Rating).IsRequired();
                model.Property(x => x.Episode_Votes).IsRequired();
                model.Property(x => x.DateTimeUpdated).IsRequired();
                model.Property(x => x.IsWatched).IsRequired();
                model.Property(x => x.CRC).IsRequired();
                model.Property(x => x.MD5).IsRequired();
                model.Property(x => x.SHA1).IsRequired();
                model.Property(x => x.FileName).IsRequired();
                model.Property(x => x.FileSize).IsRequired();
                model.Property(x => x.FileVersion).IsRequired();
                model.Property(x => x.IsCensored).IsRequired();
                model.Property(x => x.IsDeprecated).IsRequired();
                model.Property(x => x.InternalVersion).IsRequired();
                model.Property(x => x.IsChaptered).IsRequired().HasDefaultValue(-1);

            }
            {
                var model = builder.Entity<AniDB_GroupStatus>();
                model.ToTable("AniDB_GroupStatus").HasKey(x => x.AniDB_GroupStatusID);
//                //model.HasIndex(x => x.AniDB_GroupStatusID).IsUnique().ForSqlServerIsClustered().HasName("PK_AniDB_GroupStatus");
                model.HasIndex(x => x.AnimeID).HasName("IX_AniDB_GroupStatus_AnimeID");
                model.HasIndex(x => new { x.AnimeID, x.GroupID}).IsUnique().HasName("UIX_AniDB_GroupStatus_AnimeID_GroupID");
                model.Property(x => x.AniDB_GroupStatusID).IsRequired().SetLocalValueGenerated();
                model.Property(x => x.AnimeID).IsRequired();
                model.Property(x => x.GroupID).IsRequired();
                model.Property(x => x.GroupName).IsRequired().HasMaxLength(200);
                model.Property(x => x.CompletionState).IsRequired();
                model.Property(x => x.LastEpisodeNumber).IsRequired();
                model.Property(x => x.Rating).IsRequired();
                model.Property(x => x.Votes).IsRequired();
                model.Property(x => x.EpisodeRange).IsRequired().HasMaxLength(200);
            }
       
            {
                var model = builder.Entity<AniDB_MylistStats>();
                model.ToTable("AniDB_MylistStats").HasKey(x => x.AniDB_MylistStatsID);
//                //model.HasIndex(x => x.AniDB_MylistStatsID).IsUnique().ForSqlServerIsClustered().HasName("PK_AniDB_MylistStats");
                model.Property(x => x.AniDB_MylistStatsID).IsRequired().SetLocalValueGenerated();
                model.Property(x => x.Animes).IsRequired();
                model.Property(x => x.Episodes).IsRequired();
                model.Property(x => x.Files).IsRequired();
                model.Property(x => x.SizeOfFiles).IsRequired();
                model.Property(x => x.AddedAnimes).IsRequired();
                model.Property(x => x.AddedEpisodes).IsRequired();
                model.Property(x => x.AddedFiles).IsRequired();
                model.Property(x => x.AddedGroups).IsRequired();
                model.Property(x => x.LeechPct).IsRequired();
                model.Property(x => x.GloryPct).IsRequired();
                model.Property(x => x.ViewedPct).IsRequired();
                model.Property(x => x.MylistPct).IsRequired();
                model.Property(x => x.ViewedMylistPct).IsRequired();
                model.Property(x => x.EpisodesViewed).IsRequired();
                model.Property(x => x.Votes).IsRequired();
                model.Property(x => x.Reviews).IsRequired();
                model.Property(x => x.ViewiedLength).IsRequired();
            }
   
            {
                var model = builder.Entity<AniDB_Recommendation>();
                model.ToTable("AniDB_Recommendation").HasKey(x => x.AniDB_RecommendationID);
//                //model.HasIndex(x => x.AniDB_RecommendationID).IsUnique().ForSqlServerIsClustered().HasName("PK_AniDB_Recommendation");
                model.HasIndex(x => new { x.AnimeID, x.UserID}).IsUnique().HasName("UIX_AniDB_Recommendation");
                model.Property(x => x.AniDB_RecommendationID).IsRequired().SetLocalValueGenerated();
                model.Property(x => x.AnimeID).IsRequired();
                model.Property(x => x.UserID).IsRequired();
                model.Property(x => x.RecommendationType).IsRequired();
            }
            {
                var model = builder.Entity<AniDB_ReleaseGroup>();
                model.ToTable("AniDB_ReleaseGroup").HasKey(x => x.AniDB_ReleaseGroupID);
//                //model.HasIndex(x => x.GroupID).IsUnique().ForSqlServerIsClustered().HasName("PK_AniDB_ReleaseGroup_GroupID");
                model.Property(x => x.GroupID).IsRequired();
                model.Property(x => x.Rating).IsRequired();
                model.Property(x => x.Votes).IsRequired();
                model.Property(x => x.AnimeCount).IsRequired();
                model.Property(x => x.FileCount).IsRequired();
                model.Property(x => x.GroupName).IsRequired();
                model.Property(x => x.GroupNameShort).IsRequired().HasMaxLength(200);
                model.Property(x => x.IRCChannel).IsRequired().HasMaxLength(200);
                model.Property(x => x.IRCServer).IsRequired().HasMaxLength(200);
                model.Property(x => x.URL).IsRequired().HasMaxLength(200);
                model.Property(x => x.Picname).IsRequired().HasMaxLength(200);
            }
            {
                var model = builder.Entity<AniDB_Review>();
                model.ToTable("AniDB_Review").HasKey(x => x.AniDB_ReviewID);
//                //model.HasIndex(x => x.ReviewID).IsUnique().ForSqlServerIsClustered().HasName("PK_AniDB_ReviewID");
                model.Property(x => x.ReviewID).IsRequired();
                model.Property(x => x.AuthorID).IsRequired();
                model.Property(x => x.RatingAnimation).IsRequired();
                model.Property(x => x.RatingSound).IsRequired();
                model.Property(x => x.RatingStory).IsRequired();
                model.Property(x => x.RatingCharacter).IsRequired();
                model.Property(x => x.RatingValue).IsRequired();
                model.Property(x => x.RatingEnjoyment).IsRequired();
                model.Property(x => x.ReviewText).IsRequired();
            }
            {
                var model = builder.Entity<AniDB_Seiyuu>();
                model.ToTable("AniDB_Seiyuu").HasKey(x => x.AniDB_SeiyuuID);
//                //model.HasIndex(x => x.SeiyuuID).IsUnique().ForSqlServerIsClustered().HasName("PK_AniDB_SeiyuuID");
                model.Property(x => x.SeiyuuID).IsRequired();
                model.Property(x => x.SeiyuuName).IsRequired().HasMaxLength(200);
                model.Property(x => x.PicName).IsRequired().HasMaxLength(100);

            }
            {
                var model = builder.Entity<AniDB_Tag>();
                model.ToTable("AniDB_Tag").HasKey(x => x.AniDB_TagID);
//                //model.HasIndex(x => x.TagID).IsUnique().ForSqlServerIsClustered().HasName("PK_AniDB_TagID");
                model.Property(x => x.TagID).IsRequired();
                model.Property(x => x.Spoiler).IsRequired();
                model.Property(x => x.LocalSpoiler).IsRequired();
                model.Property(x => x.GlobalSpoiler).IsRequired();
                model.Property(x => x.TagName).IsRequired().HasMaxLength(150);
                model.Property(x => x.TagCount).IsRequired();
                model.Property(x => x.TagDescription).IsRequired();
            }
            {
                var model = builder.Entity<AniDB_Vote>();
                model.ToTable("AniDB_Vote").HasKey(x => x.AniDB_VoteID);
//                //model.HasIndex(x => x.AniDB_VoteID).IsUnique().ForSqlServerIsClustered().HasName("PK_AniDB_Vote");
                model.Property(x => x.AniDB_VoteID).IsRequired().SetLocalValueGenerated();
                model.Property(x => x.EntityID).IsRequired();
                model.Property(x => x.VoteValue).IsRequired();
                model.Property(x => x.VoteType).IsRequired();
            }
            {
                var model = builder.Entity<AnimeCharacter>();
                model.ToTable("AnimeCharacter").HasKey(x => x.CharacterID);
                model.Property(x => x.CharacterID).IsRequired();
                model.Property(x => x.AniDBID).IsRequired();
                model.Property(x => x.Name).IsRequired();
                model.Property(x => x.AlternateName).HasDefaultValue(null);
                model.Property(x => x.Description).HasDefaultValue(string.Empty);
                model.Property(x => x.ImagePath);
            }
            {
                var model = builder.Entity<SVR_AnimeEpisode_User>();
                model.ToTable("AnimeEpisode_User").HasKey(x => x.AnimeEpisode_UserID);
//                //model.HasIndex(x => x.AnimeEpisode_UserID).IsUnique().ForSqlServerIsClustered().HasName("PK_AnimeEpisode_User");
                model.HasIndex(x => new { x.JMMUserID, x.AnimeSeriesID}).HasName("IX_AnimeEpisode_User_User_AnimeSeriesID");
                model.HasIndex(x => new { x.JMMUserID, x.AnimeEpisodeID}).IsUnique().HasName("UIX_AnimeEpisode_User_User_EpisodeID");
                model.Property(x => x.AnimeEpisode_UserID).IsRequired().SetLocalValueGenerated();
                model.Property(x => x.JMMUserID).IsRequired();
                model.Property(x => x.AnimeEpisodeID).IsRequired();
                model.Property(x => x.AnimeSeriesID).IsRequired();
                model.Property(x => x.PlayedCount).IsRequired();
                model.Property(x => x.WatchedCount).IsRequired();
                model.Property(x => x.StoppedCount).IsRequired();
                model.Property(x => x.ContractVersion).IsRequired().HasDefaultValue(0);
                model.Property(x => x.ContractSize).IsRequired().HasDefaultValue(0);
            }
            {
                var model = builder.Entity<SVR_AnimeEpisode>();
                model.ToTable("AnimeEpisode").HasKey(x => x.AnimeEpisodeID);
//                //model.HasIndex(x => x.AnimeEpisodeID).IsUnique().ForSqlServerIsClustered().HasName("PK_AnimeEpisode");
                model.HasIndex(x => x.AnimeSeriesID).HasName("IX_AnimeEpisode_AnimeSeriesID");
                model.HasIndex(x => x.AniDB_EpisodeID).IsUnique().HasName("UIX_AnimeEpisode_AniDB_EpisodeID");
                model.Property(x => x.AnimeEpisodeID).IsRequired().SetLocalValueGenerated();
                model.Property(x => x.AnimeSeriesID).IsRequired();
                model.Property(x => x.AniDB_EpisodeID).IsRequired();
                model.Property(x => x.DateTimeUpdated).IsRequired();
                model.Property(x => x.DateTimeCreated).IsRequired();
                model.Property(x => x.DateTimeCreated).IsRequired();

                //plex contracts are in-memory only now
                //model.Property(x => x.PlexContractVersion).IsRequired().HasDefaultValue(0);
                //model.Property(x => x.PlexContractSize).IsRequired().HasDefaultValue(0);
            }
            {
                var model = builder.Entity<SVR_AnimeGroup_User>();
//                //model.HasIndex(x => x.AnimeGroup_UserID).IsUnique().ForSqlServerIsClustered().HasName("PK_AnimeGroup_User");
                model.HasIndex(x => new { x.JMMUserID, x.AnimeGroupID}).IsUnique().HasName("UIX_AnimeGroup_User_User_GroupID");
                model.ToTable("AnimeGroup_User").HasKey(x => x.AnimeGroup_UserID);
                model.Property(x => x.AnimeGroup_UserID).IsRequired().SetLocalValueGenerated();
                model.Property(x => x.JMMUserID).IsRequired();
                model.Property(x => x.AnimeGroupID).IsRequired();
                model.Property(x => x.IsFave).IsRequired();
                model.Property(x => x.UnwatchedEpisodeCount).IsRequired();
                model.Property(x => x.WatchedEpisodeCount).IsRequired();
                model.Property(x => x.PlayedCount).IsRequired();
                model.Property(x => x.WatchedCount).IsRequired();
                model.Property(x => x.StoppedCount).IsRequired();

                //plex contracts are in-memory only now
                //model.Property(x => x.PlexContractVersion).IsRequired().HasDefaultValue(0); 
                //model.Property(x => x.PlexContractSize).IsRequired().HasDefaultValue(0);
            }
            {
                var model = builder.Entity<SVR_AnimeGroup>();
                model.ToTable("AnimeGroup");
                model.HasKey(x => x.AnimeGroupID);
//                model.HasIndex(x => x.AnimeGroupID).IsUnique().ForSqlServerIsClustered().HasName("PK_AnimeGroup");
                model.HasIndex(x => x.AnimeGroupParentID).HasName("IX_AnimeGroup_AnimeGroupParentID");
                model.Property(x => x.AnimeGroupID).IsRequired().SetLocalValueGenerated();
                model.Property(x => x.GroupName).IsRequired();
                model.Property(x => x.IsManuallyNamed).IsRequired();
                model.Property(x => x.DateTimeUpdated).IsRequired();
                model.Property(x => x.DateTimeCreated).IsRequired();
                model.Property(x => x.SortName).IsRequired();
                model.Property(x => x.MissingEpisodeCount).IsRequired();
                model.Property(x => x.MissingEpisodeCountGroups).IsRequired();
                model.Property(x => x.OverrideDescription).IsRequired();
                model.Property(x => x.ContractVersion).IsRequired().HasDefaultValue(0);
                model.Property(x => x.ContractSize).IsRequired().HasDefaultValue(0);
            }
            {
                var model = builder.Entity<SVR_AnimeSeries_User>();
                model.ToTable("AnimeSeries_User").HasKey(x => x.AnimeSeries_UserID);
//                //model.HasIndex(x => x.AnimeSeries_UserID).IsUnique().ForSqlServerIsClustered().HasName("PK_AnimeSeries_User");
                model.HasIndex(x => new { x.JMMUserID, x.AnimeSeriesID}).IsUnique().HasName("UIX_AnimeSeries_User_User_SeriesID");
                model.Property(x => x.AnimeSeries_UserID).IsRequired().SetLocalValueGenerated();
                model.Property(x => x.JMMUserID).IsRequired();
                model.Property(x => x.AnimeSeriesID).IsRequired();
                model.Property(x => x.UnwatchedEpisodeCount).IsRequired();
                model.Property(x => x.WatchedEpisodeCount).IsRequired();
                model.Property(x => x.PlayedCount).IsRequired();
                model.Property(x => x.WatchedCount).IsRequired();
                model.Property(x => x.StoppedCount).IsRequired();

                //plex contracts are in-memory only now
                //model.Property(x => x.PlexContractVersion).IsRequired().HasDefaultValue(0);
                //model.Property(x => x.PlexContractSize).IsRequired().HasDefaultValue(0);
            }
            {
                var model = builder.Entity<SVR_AnimeSeries>();
                model.ToTable("AnimeSeries").HasKey(x => x.AnimeSeriesID);
//                //model.HasIndex(x => x.AnimeSeriesID).IsUnique().ForSqlServerIsClustered().HasName("PK_AnimeSeries");
                model.HasIndex(x => x.AnimeGroupID).HasName("IX_AnimeSeries_AnimeGroupID");
                model.HasIndex(x => x.AniDB_ID).IsUnique().HasName("UIX_AnimeSeries_AniDB_ID");
                model.Property(x => x.AnimeSeriesID).IsRequired().SetLocalValueGenerated();
                model.Property(x => x.AnimeGroupID).IsRequired();
                model.Property(x => x.AniDB_ID).IsRequired();
                model.Property(x => x.DateTimeUpdated).IsRequired();
                model.Property(x => x.DateTimeCreated).IsRequired();
                model.Property(x => x.MissingEpisodeCount).IsRequired();
                model.Property(x => x.MissingEpisodeCountGroups).IsRequired();
                model.Property(x => x.LatestLocalEpisodeNumber).IsRequired();
                model.Property(x => x.EpisodeAddedDate);
                model.Property(x => x.SeriesNameOverride).HasMaxLength(500);
                model.Property(x => x.ContractVersion).IsRequired().HasDefaultValue(0);
                model.Property(x => x.ContractSize).IsRequired().HasDefaultValue(0);
                model.Property(x => x.AirsOn).HasConversion(new EnumToStringConverter<System.DayOfWeek>());
            }
            {
                var model = builder.Entity<AnimeStaff>();
                model.ToTable("AnimeStaff").HasKey(x => x.StaffID);
//                //model.HasIndex(x => x.StaffID).IsUnique().HasName("PK_AnimeStaff_StaffID");
                model.HasIndex(x => x.AniDBID).HasName("UIX_AnimeStaff_AniDBID").IsUnique();
                model.Property(x => x.AniDBID).IsRequired();
                model.Property(x => x.Name).IsRequired();
                model.Property(x => x.AlternateName).HasDefaultValue(null);
                model.Property(x => x.Description).HasDefaultValue(null);
                model.Property(x => x.ImagePath);
            }
            {
                var model = builder.Entity<AuthTokens>();
                //model.ToTable("AuthTokens").HasKey(x => new { x.AuthID, x.UserID, x.DeviceName, x.Token });
                model.ToTable("AuthTokens").HasKey(x => x.AuthID);
//                //model.HasIndex(x => new { x.AuthID, x.UserID, x.DeviceName, x.Token}).IsUnique().ForSqlServerIsClustered().HasName("PK_AuthTokens");
                model.HasIndex(x => x.Token).HasName("IX_AuthTokens_Token");
                model.Property(x => x.AuthID).IsRequired().SetLocalValueGenerated();
                model.Property(x => x.UserID).IsRequired();
                model.Property(x => x.DeviceName).IsRequired();
                model.Property(x => x.Token).IsRequired();

            }
            {
                var model = builder.Entity<BookmarkedAnime>();
                model.ToTable("BookmarkedAnime").HasKey(x => x.AnimeID);
//                //model.HasIndex(x => x.AnimeID).IsUnique().ForSqlServerIsClustered().HasName("PK_BookmarkedAnime");
                model.Property(x => x.Priority).IsRequired();
                model.Property(x => x.Downloading).IsRequired();

            }
            {
                var model = builder.Entity<SVR_CloudAccount>();
                model.ToTable("CloudAccount").HasKey(x => x.CloudID);
//                //model.HasIndex(x => x.CloudID).IsUnique().ForSqlServerIsClustered().HasName("PK_CloudAccount");
                model.Property(x => x.CloudID).IsRequired().SetLocalValueGenerated();
                model.Property(x => x.ConnectionString).IsRequired();
                model.Property(x => x.Provider).IsRequired();
                model.Property(x => x.Name).IsRequired();
            }
            {
                var model = builder.Entity<CommandRequest>();
                model.ToTable("CommandRequest").HasKey(x => x.CommandRequestID);
//                //model.HasIndex(x => x.CommandRequestID).IsUnique().ForSqlServerIsClustered().HasName("PK_CommandRequest");
                model.Property(x => x.CommandRequestID).IsRequired().SetLocalValueGenerated();
                model.Property(x => x.Priority).IsRequired();
                model.Property(x => x.CommandType).IsRequired();
                model.Property(x => x.CommandID).IsRequired();
                model.Property(x => x.CommandDetails).IsRequired();
                model.Property(x => x.DateTimeUpdated).IsRequired();
            }
            {
                var model = builder.Entity<CrossRef_AniDB_MAL>();
                model.ToTable("CrossRef_AniDB_MAL").HasKey(x => x.CrossRef_AniDB_MALID);
//                //model.HasIndex(x => x.CrossRef_AniDB_MALID).IsUnique().ForSqlServerIsClustered().HasName("PK_CrossRef_AniDB_MAL");
                model.HasIndex(x => new { x.AnimeID, x.StartEpisodeType, x.StartEpisodeNumber }).IsUnique().HasName("UIX_CrossRef_AniDB_MAL_Anime");
                model.HasIndex(x => x.MALID).IsUnique().HasName("UIX_CrossRef_AniDB_MAL_MALID");
                model.Property(x => x.CrossRef_AniDB_MALID).IsRequired().SetLocalValueGenerated();
                model.Property(x => x.AnimeID).IsRequired();
                model.Property(x => x.MALID).IsRequired();
                model.Property(x => x.MALTitle).HasMaxLength(500);
                model.Property(x => x.StartEpisodeType).IsRequired();
                model.Property(x => x.StartEpisodeNumber).IsRequired();
                model.Property(x => x.CrossRefSource).IsRequired();
            }
            {
                var model = builder.Entity<CrossRef_AniDB_Other>();
                model.ToTable("CrossRef_AniDB_Other").HasKey(x => x.CrossRef_AniDB_OtherID);
//                //model.HasIndex(x => x.CrossRef_AniDB_OtherID).IsUnique().ForSqlServerIsClustered().HasName("PK_CrossRef_AniDB_Other");
                model.HasIndex(x => new { x.AnimeID, x.CrossRefID, x.CrossRefSource, x.CrossRefType}).IsUnique().HasName("UIX_CrossRef_AniDB_Other");
                model.Property(x => x.CrossRef_AniDB_OtherID).IsRequired().SetLocalValueGenerated();
                model.Property(x => x.AnimeID).IsRequired();
                model.Property(x => x.CrossRefID).IsRequired().HasMaxLength(500);
                model.Property(x => x.CrossRefSource).IsRequired();
                model.Property(x => x.CrossRefType).IsRequired();
            }
            {
                var model = builder.Entity<CrossRef_AniDB_Trakt_Episode>();
                model.ToTable("CrossRef_AniDB_Trakt_Episode").HasKey(x => x.CrossRef_AniDB_Trakt_EpisodeID);
//                //model.HasIndex(x => x.CrossRef_AniDB_Trakt_EpisodeID).IsUnique().ForSqlServerIsClustered().HasName("PK_CrossRef_AniDB_Trakt_Episode");
                model.HasIndex(x => x.AniDBEpisodeID).IsUnique().HasName("UIX_CrossRef_AniDB_Trakt_Episode_AniDBEpisodeID");
                model.Property(x => x.CrossRef_AniDB_Trakt_EpisodeID).IsRequired().SetLocalValueGenerated();
                model.Property(x => x.AnimeID).IsRequired();
                model.Property(x => x.AniDBEpisodeID).IsRequired();
                model.Property(x => x.TraktID).HasMaxLength(500);
                model.Property(x => x.Season).IsRequired();
                model.Property(x => x.EpisodeNumber).IsRequired();
            }
            {
                var model = builder.Entity<CrossRef_AniDB_TraktV2>();
                model.ToTable("CrossRef_AniDB_TraktV2").HasKey(x => x.CrossRef_AniDB_TraktV2ID);
//                //model.HasIndex(x => x.CrossRef_AniDB_TraktV2ID).IsUnique().ForSqlServerIsClustered().HasName("PK_CrossRef_AniDB_TraktV2");
                model.HasIndex(x => new { x.AnimeID, x.TraktSeasonNumber, x.TraktStartEpisodeNumber, x.AniDBStartEpisodeType, x.AniDBStartEpisodeNumber}).IsUnique().HasName("UIX_CrossRef_AniDB_TraktV2");
                model.Property(x => x.CrossRef_AniDB_TraktV2ID).IsRequired().SetLocalValueGenerated();
                model.Property(x => x.AnimeID).IsRequired();
                model.Property(x => x.AniDBStartEpisodeType).IsRequired();
                model.Property(x => x.AniDBStartEpisodeNumber).IsRequired();
                model.Property(x => x.TraktID).HasMaxLength(500);
                model.Property(x => x.TraktSeasonNumber).IsRequired();
                model.Property(x => x.TraktStartEpisodeNumber).IsRequired();
                model.Property(x => x.CrossRefSource).IsRequired();
            }
            {
                var model = builder.Entity<CrossRef_AniDB_TvDB_Episode>();
                model.ToTable("CrossRef_AniDB_TvDB_Episode").HasKey(x => x.CrossRef_AniDB_TvDB_EpisodeID);
//                //model.HasIndex(x => x.CrossRef_AniDB_TvDB_EpisodeID).IsUnique().ForSqlServerIsClustered().HasName("PK_CrossRef_AniDB_TvDB_Episode");
                model.HasIndex(x => x.AniDBEpisodeID).IsUnique().HasName("UIX_CrossRef_AniDB_TvDB_Episode_AniDBEpisodeID");
                model.Property(x => x.CrossRef_AniDB_TvDB_EpisodeID).IsRequired().SetLocalValueGenerated();
                model.Property(x => x.AniDBEpisodeID).IsRequired();
                model.Property(x => x.TvDBEpisodeID).IsRequired();
            }
            {
                //todo, this shouldn't be v2...?
                var model = builder.Entity<CrossRef_AniDB_TvDBV2>();
                model.ToTable("CrossRef_AniDB_TvDBV2").HasKey(x => x.CrossRef_AniDB_TvDBV2ID);
//                //model.HasIndex(x => x.CrossRef_AniDB_TvDBV2ID).IsUnique().ForSqlServerIsClustered().HasName("PK_CrossRef_AniDB_TvDBV2");
                model.HasIndex(x => new { x.AnimeID, x.TvDBID, x.TvDBSeasonNumber, x.TvDBStartEpisodeNumber, x.AniDBStartEpisodeType, x.AniDBStartEpisodeNumber}).IsUnique().HasName("UIX_CrossRef_AniDB_TvDBV2");
                model.Property(x => x.CrossRef_AniDB_TvDBV2ID).IsRequired().SetLocalValueGenerated();
                model.Property(x => x.AnimeID).IsRequired();
                model.Property(x => x.AniDBStartEpisodeType).IsRequired();
                model.Property(x => x.AniDBStartEpisodeNumber).IsRequired();
                model.Property(x => x.TvDBID).IsRequired();
                model.Property(x => x.TvDBSeasonNumber).IsRequired();
                model.Property(x => x.TvDBStartEpisodeNumber).IsRequired();
                model.Property(x => x.CrossRefSource).IsRequired();
            }
            {
                var model = builder.Entity<CrossRef_AniDB_TvDB_Episode_Override>();
                model.ToTable("CrossRef_AniDB_TvDB_Episode_Override").HasKey(x => x.CrossRef_AniDB_TvDB_Episode_OverrideID);
//                //model.HasIndex(x => x.CrossRef_AniDB_TvDB_Episode_OverrideID).IsUnique().HasName("PK_CrossRef_AniDB_TvDB_Episode_Override");
                model.Property(x => x.CrossRef_AniDB_TvDB_Episode_OverrideID).IsRequired();
                model.Property(x => x.AniDBEpisodeID).IsRequired();
                model.Property(x => x.TvDBEpisodeID).IsRequired();
            }
            {
                var model = builder.Entity<CrossRef_Anime_Staff>();
                model.ToTable("CrossRef_Anime_Staff").HasKey(x => x.CrossRef_Anime_StaffID);
//                //model.HasIndex(x => x.CrossRef_Anime_StaffID).IsUnique().HasName("PK_CrossRef_Anime_Staff");
                model.Property(x => x.CrossRef_Anime_StaffID).IsRequired();
                model.Property(x => x.AniDB_AnimeID).IsRequired();
                model.Property(x => x.StaffID).IsRequired();
                model.Property(x => x.Role).IsRequired();
                model.Property(x => x.RoleID).IsRequired();
                model.Property(x => x.RoleType).IsRequired();
                model.Property(x => x.Language).IsRequired();
            }
            {
                var model = builder.Entity<CrossRef_CustomTag>();
                model.ToTable("CrossRef_CustomTag").HasKey(x => x.CrossRef_CustomTagID);
//                model.HasIndex(x => x.CrossRef_CustomTagID).IsUnique().ForSqlServerIsClustered().HasName("PK_CrossRef_CustomTag");
                model.HasIndex(x => x.CustomTagID).HasName("IX_CrossRef_CustomTag");
                model.Property(x => x.CrossRef_CustomTagID).IsRequired().SetLocalValueGenerated();
                model.Property(x => x.CustomTagID).IsRequired();
                model.Property(x => x.CrossRefID).IsRequired();
                model.Property(x => x.CrossRefType).IsRequired();
            }
            {
                var model = builder.Entity<CrossRef_File_Episode>();
                model.ToTable("CrossRef_File_Episode").HasKey(x => x.CrossRef_File_EpisodeID);
//                model.HasIndex(x => x.CrossRef_File_EpisodeID).IsUnique().ForSqlServerIsClustered().HasName("PK_CrossRef_File_Episode");
                model.HasIndex(x => new { x.Hash, x.EpisodeID}).IsUnique().HasName("UIX_CrossRef_File_Episode_Hash_EpisodeID");
                model.Property(x => x.CrossRef_File_EpisodeID).IsRequired().SetLocalValueGenerated();
                model.Property(x => x.Hash).HasMaxLength(50);
                model.Property(x => x.FileName).IsRequired().HasMaxLength(500);
                model.Property(x => x.FileSize).IsRequired();
                model.Property(x => x.CrossRefSource).IsRequired();
                model.Property(x => x.AnimeID).IsRequired();
                model.Property(x => x.EpisodeID).IsRequired();
                model.Property(x => x.Percentage).IsRequired();
                model.Property(x => x.EpisodeOrder).IsRequired();
            }
            {
                var model = builder.Entity<CrossRef_Languages_AniDB_File>();
                model.ToTable("CrossRef_Languages_AniDB_File").HasKey(x => x.CrossRef_Languages_AniDB_FileID);
//                model.HasIndex(x => x.CrossRef_Languages_AniDB_FileID).IsUnique().ForSqlServerIsClustered().HasName("PK_CrossRef_Languages_AniDB_File");
                model.HasIndex(x => x.FileID).HasName("IX_CrossRef_Languages_AniDB_File_FileID");
                model.HasIndex(x => x.LanguageID).HasName("IX_CrossRef_Languages_AniDB_File_LanguageID");
                model.Property(x => x.CrossRef_Languages_AniDB_FileID).IsRequired().SetLocalValueGenerated();
                model.Property(x => x.FileID).IsRequired();
                model.Property(x => x.LanguageID).IsRequired();
            }
            {
                var model = builder.Entity<CrossRef_Subtitles_AniDB_File>();
                model.ToTable("CrossRef_Subtitles_AniDB_File").HasKey(x => x.CrossRef_Subtitles_AniDB_FileID);
//                model.HasIndex(x => x.CrossRef_Subtitles_AniDB_FileID).IsUnique().ForSqlServerIsClustered().HasName("PK_CrossRef_Subtitles_AniDB_File");
                model.HasIndex(x => x.FileID).HasName("IX_CrossRef_Subtitles_AniDB_File_FileID");
                model.HasIndex(x => x.LanguageID).HasName("IX_CrossRef_Subtitles_AniDB_File_LanguageID");
                model.Property(x => x.CrossRef_Subtitles_AniDB_FileID).IsRequired().SetLocalValueGenerated();
                model.Property(x => x.FileID).IsRequired();
                model.Property(x => x.LanguageID).IsRequired();
            }
            {
                var model = builder.Entity<CustomTag>();
                model.ToTable("CustomTag").HasKey(x => x.CustomTagID);
//                model.HasIndex(x => x.CustomTagID).IsUnique().ForSqlServerIsClustered().HasName("PK_CustomTag");
                model.Property(x => x.CustomTagID).IsRequired().SetLocalValueGenerated();
                model.Property(x => x.TagName).HasMaxLength(500);
            }
            {
                var model = builder.Entity<DuplicateFile>();
                model.ToTable("DuplicateFile").HasKey(x => x.DuplicateFileID);
//                model.HasIndex(x => x.DuplicateFileID).IsUnique().ForSqlServerIsClustered().HasName("PK_DuplicateFile");
                model.Property(x => x.DuplicateFileID).IsRequired().SetLocalValueGenerated();
                model.Property(x => x.FilePathFile1).IsRequired();
                model.Property(x => x.FilePathFile2).IsRequired();
                model.Property(x => x.ImportFolderIDFile1).IsRequired();
                model.Property(x => x.ImportFolderIDFile2).IsRequired();
                model.Property(x => x.Hash).IsRequired().HasMaxLength(50);
                model.Property(x => x.DateTimeUpdated).IsRequired();

            }
            {
                var model = builder.Entity<FileFfdshowPreset>();
                model.ToTable("FileFfdshowPreset").HasKey(x => x.FileFfdshowPresetID);
//                model.HasIndex(x => x.FileFfdshowPresetID).IsUnique().ForSqlServerIsClustered().HasName("PK_FileFfdshowPreset");
                model.HasIndex(x => new { x.Hash, x.FileSize}).IsUnique().HasName("UIX_FileFfdshowPreset_Hash");
                model.Property(x => x.FileFfdshowPresetID).IsRequired().SetLocalValueGenerated();
                model.Property(x => x.Hash).IsRequired().HasMaxLength(50);
                model.Property(x => x.FileSize).IsRequired();

            }
            {
                var model = builder.Entity<FileNameHash>();
                model.ToTable("FileNameHash").HasKey(x => x.FileNameHashID);
//                model.HasIndex(x => x.FileNameHashID).IsUnique().ForSqlServerIsClustered().HasName("PK_FileNameHash");
                model.HasIndex(x => new { x.FileName, x.FileSize, x.Hash}).IsUnique().HasName("UIX_FileNameHash");
                model.Property(x => x.FileNameHashID).IsRequired().SetLocalValueGenerated();
                model.Property(x => x.FileName).IsRequired().HasMaxLength(500);
                model.Property(x => x.FileSize).IsRequired();
                model.Property(x => x.Hash).IsRequired().HasMaxLength(50);
                model.Property(x => x.DateTimeUpdated).IsRequired();
            }
            {
                var model = builder.Entity<GroupFilterCondition>();
                model.ToTable("GroupFilterCondition");
//                model.HasIndex(x => x.GroupFilterConditionID).IsUnique().HasName("PK_GroupFilterCondition");
                model.Property(x => x.GroupFilterID).IsRequired();
                model.Property(x => x.ConditionType).IsRequired();
                model.Property(x => x.ConditionOperator).IsRequired();
                model.Property(x => x.ConditionParameter).IsRequired();
            }
            {
                var model = builder.Entity<SVR_GroupFilter>();
                model.ToTable("GroupFilter").HasKey(x => x.GroupFilterID);
//                model.HasIndex(x => x.GroupFilterID).IsUnique().ForSqlServerIsClustered().HasName("PK_GroupFilter");
                model.HasIndex(x => x.ParentGroupFilterID).HasName("IX_GroupFilter_ParentGroupFilterID");
                model.Property(x => x.GroupFilterID).IsRequired().SetLocalValueGenerated();
                model.Property(x => x.GroupFilterName).IsRequired();
                model.Property(x => x.ApplyToSeries).IsRequired();
                model.Property(x => x.BaseCondition).IsRequired();
                model.Property(x => x.FilterType).IsRequired();
                model.Property(x => x.GroupsIdsVersion).IsRequired().HasDefaultValue(0);
                model.Property(x => x.GroupConditionsVersion).IsRequired().HasDefaultValue(0);
                model.Property(x => x.InvisibleInClients).IsRequired().HasDefaultValue(0);
                model.Property(x => x.SeriesIdsVersion).IsRequired().HasDefaultValue(0);
            }
            {
                var model = builder.Entity<IgnoreAnime>();
                model.ToTable("IgnoreAnime").HasKey(x => x.IgnoreAnimeID);
//                model.HasIndex(x => x.IgnoreAnimeID).IsUnique().ForSqlServerIsClustered().HasName("PK_IgnoreAnime");
                model.HasIndex(x => new { x.JMMUserID, x.AnimeID, x.IgnoreType}).IsUnique().HasName("UIX_IgnoreAnime_User_AnimeID");
                model.Property(x => x.IgnoreAnimeID).IsRequired().SetLocalValueGenerated();
                model.Property(x => x.JMMUserID).IsRequired();
                model.Property(x => x.AnimeID).IsRequired();
                model.Property(x => x.IgnoreType).IsRequired();

            }
            {
                var model = builder.Entity<SVR_ImportFolder>();
                model.ToTable("ImportFolder").HasKey(x => x.ImportFolderID);
//                model.HasIndex(x => x.ImportFolderID).IsUnique().ForSqlServerIsClustered().HasName("PK_ImportFolder");
                model.Property(x => x.ImportFolderID).IsRequired().SetLocalValueGenerated();
                model.Property(x => x.ImportFolderType).IsRequired();
                model.Property(x => x.ImportFolderName).IsRequired();
                model.Property(x => x.ImportFolderLocation).IsRequired();
                model.Property(x => x.IsDropSource).IsRequired();
                model.Property(x => x.IsDropDestination).IsRequired();
                model.Property(x => x.IsWatched).IsRequired();

            }
            {
                var model = builder.Entity<SVR_JMMUser>();
                model.ToTable("JMMUser").HasKey(x => x.JMMUserID);
//                model.HasIndex(x => x.JMMUserID).IsUnique().ForSqlServerIsClustered().HasName("PK_JMMUser");
                model.Property(x => x.JMMUserID).IsRequired().SetLocalValueGenerated();
                model.Property(x => x.Username).HasMaxLength(100);
                model.Property(x => x.Password).HasMaxLength(150);
                model.Property(x => x.IsAdmin).IsRequired();
                model.Property(x => x.IsAniDBUser).IsRequired();
                model.Property(x => x.IsTraktUser).IsRequired();
            }
            {
                var model = builder.Entity<Language>();
                model.ToTable("Language").HasKey(x => x.LanguageID);
//                model.HasIndex(x => x.LanguageID).IsUnique().ForSqlServerIsClustered().HasName("PK_Language");
                model.HasIndex(x => x.LanguageName).IsUnique().HasName("UIX_Language_LanguageName");
                model.Property(x => x.LanguageID).IsRequired().SetLocalValueGenerated();
                model.Property(x => x.LanguageName).IsRequired().HasMaxLength(100);

            }
            {
                var model = builder.Entity<MovieDB_Fanart>();
                model.ToTable("MovieDB_Fanart").HasKey(x => x.MovieDB_FanartID);
//                //model.HasIndex(x => x.MovieDB_FanartID).IsUnique().ForSqlServerIsClustered().HasName("PK_MovieDB_Fanart");
                model.Property(x => x.MovieDB_FanartID).IsRequired().SetLocalValueGenerated();
                model.Property(x => x.MovieId).IsRequired();
                model.Property(x => x.ImageType).HasMaxLength(100);
                model.Property(x => x.ImageSize).HasMaxLength(100);
                model.Property(x => x.ImageWidth).IsRequired();
                model.Property(x => x.ImageHeight).IsRequired();
                model.Property(x => x.Enabled).IsRequired();

            }
            {
                var model = builder.Entity<MovieDB_Movie>();
                model.ToTable("MovieDB_Movie").HasKey(x => x.MovieDB_MovieID);
//                //model.HasIndex(x => x.MovieDB_MovieID).IsUnique().ForSqlServerIsClustered().HasName("PK_MovieDB_Movie");
                model.HasIndex(x => x.MovieId).IsUnique().HasName("UIX_MovieDB_Movie_Id");
                model.Property(x => x.MovieDB_MovieID).IsRequired().SetLocalValueGenerated();
                model.Property(x => x.MovieId).IsRequired();
            }
            {
                var model = builder.Entity<MovieDB_Poster>();
                model.ToTable("MovieDB_Poster").HasKey(x => x.MovieDB_PosterID);
//                //model.HasIndex(x => x.MovieDB_PosterID).IsUnique().ForSqlServerIsClustered().HasName("PK_MovieDB_Poster");
                model.Property(x => x.MovieDB_PosterID).IsRequired().SetLocalValueGenerated();
                model.Property(x => x.ImageID).HasMaxLength(100);
                model.Property(x => x.MovieId).IsRequired();
                model.Property(x => x.ImageType).HasMaxLength(100);
                model.Property(x => x.ImageSize).HasMaxLength(100);
                model.Property(x => x.ImageWidth).IsRequired();
                model.Property(x => x.ImageHeight).IsRequired();
                model.Property(x => x.Enabled).IsRequired();
            }
            {
                var model = builder.Entity<Playlist>();
                model.ToTable("Playlist").HasKey(x => x.PlaylistID);
//                //model.HasIndex(x => x.PlaylistID).IsUnique().ForSqlServerIsClustered().HasName("PK_Playlist");
                model.Property(x => x.PlaylistID).IsRequired().SetLocalValueGenerated();
                model.Property(x => x.DefaultPlayOrder).IsRequired();
                model.Property(x => x.PlayWatched).IsRequired();
                model.Property(x => x.PlayUnwatched).IsRequired();
            }
            {
                var model = builder.Entity<RenameScript>();
                model.ToTable("RenameScript").HasKey(x => x.RenameScriptID);
//                //model.HasIndex(x => x.RenameScriptID).IsUnique().ForSqlServerIsClustered().HasName("PK_RenameScript");
                model.Property(x => x.RenameScriptID).IsRequired().SetLocalValueGenerated();
                model.Property(x => x.IsEnabledOnImport).IsRequired();
                model.Property(x => x.RenamerType).IsRequired().HasDefaultValue("Legacy");
            }
            {
                var model = builder.Entity<ScanFile>();
                //model.ToTable("ScanFile").HasKey(x => new { x.ScanFileID, x.ScanID, x.ImportFolderID, x.VideoLocal_Place_ID, x.FullName, x.FileSize, x.Status, x.Hash });
                model.ToTable("ScanFile").HasKey(x => x.ScanFileID);
//                //model.HasIndex(x => x.ScanFileID).IsUnique().ForSqlServerIsClustered().HasName("PK_ScanFile");
                model.HasIndex(x => new { x.ScanID,  x.Status}).HasName("IX_ScanFileStatus");
                model.Property(x => x.ScanFileID).IsRequired().SetLocalValueGenerated();
                model.Property(x => x.ScanID).IsRequired();
                model.Property(x => x.ImportFolderID).IsRequired();
                model.Property(x => x.VideoLocal_Place_ID).IsRequired();
                model.Property(x => x.FullName).IsRequired();
                model.Property(x => x.FileSize).IsRequired();
                model.Property(x => x.Status).IsRequired();
                model.Property(x => x.Hash).IsRequired().HasMaxLength(100);
                model.Property(x => x.HashResult).HasMaxLength(100);

            }
            {
                var model = builder.Entity<SVR_Scan>();
                //model.ToTable("Scan").HasKey(x => new { x.ScanID, x.CreationTime, x.ImportFolders, x.Status });
                model.ToTable("Scan").HasKey(x => x.ScanID);
//                //model.HasIndex(x => new { x.ScanID, x.CreationTime , x.ImportFolders, x.Status }).IsUnique().ForSqlServerIsClustered().HasName("PK_Scan");
                model.Property(x => x.ScanID).IsRequired().SetLocalValueGenerated();
                model.Property(x => x.CreationTime).IsRequired();
                model.Property(x => x.ImportFolders).IsRequired();
                model.Property(x => x.Status).IsRequired();
            }
            {
                var model = builder.Entity<ScheduledUpdate>();
                model.ToTable("ScheduledUpdate").HasKey(x => x.ScheduledUpdateID);
//                //model.HasIndex(x => x.ScheduledUpdateID).IsUnique().ForSqlServerIsClustered().HasName("PK_ScheduledUpdate");
                model.HasIndex(x => x.UpdateType).IsUnique().HasName("UIX_ScheduledUpdate_Type");
                model.Property(x => x.ScheduledUpdateID).IsRequired().SetLocalValueGenerated();
                model.Property(x => x.UpdateType).IsRequired();
                model.Property(x => x.LastUpdate).IsRequired();
                model.Property(x => x.UpdateDetails).IsRequired();

            }
            {
                var model = builder.Entity<Trakt_Episode>();
                model.ToTable("Trakt_Episode").HasKey(x => x.Trakt_EpisodeID);
//                //model.HasIndex(x => x.Trakt_EpisodeID).IsUnique().ForSqlServerIsClustered().HasName("PK_Trakt_Episode");
                model.Property(x => x.Trakt_EpisodeID).IsRequired().SetLocalValueGenerated();
                model.Property(x => x.Trakt_ShowID).IsRequired();
                model.Property(x => x.Season).IsRequired();
                model.Property(x => x.EpisodeNumber).IsRequired();
                model.Property(x => x.URL).HasMaxLength(500);
            }
            {
                var model = builder.Entity<Trakt_Friend>();
                model.ToTable("Trakt_Friend").HasKey(x => x.Trakt_FriendID);
//                //model.HasIndex(x => x.Trakt_FriendID).IsUnique().ForSqlServerIsClustered().HasName("PK_Trakt_Friend");
                model.HasIndex(x => x.Username).IsUnique().HasName("UIX_Trakt_Friend_Username");
                model.Property(x => x.Trakt_FriendID).IsRequired().SetLocalValueGenerated();
                model.Property(x => x.Username).IsRequired().HasMaxLength(100);
                model.Property(x => x.FullName).HasMaxLength(100);
                model.Property(x => x.Gender).HasMaxLength(100);
                model.Property(x => x.Age).HasMaxLength(100);
                model.Property(x => x.Location).HasMaxLength(100);
                model.Property(x => x.Joined).IsRequired();
                model.Property(x => x.LastAvatarUpdate).IsRequired();

            }
            {
                var model = builder.Entity<Trakt_Season>();
                model.ToTable("Trakt_Season").HasKey(x => x.Trakt_SeasonID);
//                //model.HasIndex(x => x.Trakt_SeasonID).IsUnique().ForSqlServerIsClustered().HasName("PK_Trakt_Season");
                model.Property(x => x.Trakt_SeasonID).IsRequired().SetLocalValueGenerated();
                model.Property(x => x.Trakt_ShowID).IsRequired();
                model.Property(x => x.Season).IsRequired();
                model.Property(x => x.URL).HasMaxLength(500);
            }
            {
                var model = builder.Entity<Trakt_Show>();
                model.ToTable("Trakt_Show").HasKey(x => x.Trakt_ShowID);
//                //model.HasIndex(x => x.Trakt_ShowID).IsUnique().ForSqlServerIsClustered().HasName("PK_Trakt_Show");
                model.Property(x => x.Trakt_ShowID).IsRequired().SetLocalValueGenerated();
                model.Property(x => x.TraktID).HasMaxLength(500);
                model.Property(x => x.Year).HasMaxLength(500);
                model.Property(x => x.URL).HasMaxLength(500);
            }
            {
                var model = builder.Entity<TvDB_Episode>();
                model.ToTable("TvDB_Episode").HasKey(x => x.TvDB_EpisodeID);
//                //model.HasIndex(x => x.TvDB_EpisodeID).IsUnique().ForSqlServerIsClustered().HasName("PK_TvDB_Episode");
                model.HasIndex(x => x.Id).IsUnique().HasName("UIX_TvDB_Episode_Id");
                model.Property(x => x.TvDB_EpisodeID).IsRequired().SetLocalValueGenerated();
                model.Property(x => x.Id).IsRequired();
                model.Property(x => x.SeriesID).IsRequired();
                model.Property(x => x.SeasonID).IsRequired();
                model.Property(x => x.SeasonNumber).IsRequired();
                model.Property(x => x.EpisodeNumber).IsRequired();
                model.Property(x => x.EpImgFlag).IsRequired();

            }
            {
                var model = builder.Entity<TvDB_ImageFanart>();
                model.ToTable("TvDB_ImageFanart").HasKey(x => x.TvDB_ImageFanartID);
//                //model.HasIndex(x => x.TvDB_ImageFanartID).IsUnique().ForSqlServerIsClustered().HasName("PK_TvDB_ImageFanart");
                model.HasIndex(x => x.Id).IsUnique().HasName("UIX_TvDB_ImageFanart_Id");
                model.Property(x => x.TvDB_ImageFanartID).IsRequired().SetLocalValueGenerated();
                model.Property(x => x.Id).IsRequired();
                model.Property(x => x.SeriesID).IsRequired();
                model.Property(x => x.Enabled).IsRequired();
                model.Property(x => x.Chosen).IsRequired();
            }
            {
                var model = builder.Entity<TvDB_ImagePoster>();
                model.ToTable("TvDB_ImagePoster").HasKey(x => x.TvDB_ImagePosterID);
//                //model.HasIndex(x => x.TvDB_ImagePosterID).IsUnique().ForSqlServerIsClustered().HasName("PK_TvDB_ImagePoster");
                model.HasIndex(x => x.Id).IsUnique().HasName("UIX_TvDB_ImagePoster_Id");

                model.Property(x => x.TvDB_ImagePosterID).IsRequired().SetLocalValueGenerated();
                model.Property(x => x.Id).IsRequired();
                model.Property(x => x.SeriesID).IsRequired();
                model.Property(x => x.Enabled).IsRequired();

            }
            {
                var model = builder.Entity<TvDB_ImageWideBanner>();
                model.ToTable("TvDB_ImageWideBanner").HasKey(x => x.TvDB_ImageWideBannerID);
//                //model.HasIndex(x => x.TvDB_ImageWideBannerID).IsUnique().ForSqlServerIsClustered().HasName("PK_TvDB_ImageWideBanner");
                model.HasIndex(x => x.Id).IsUnique().HasName("UIX_TvDB_ImageWideBanner_Id");

                model.Property(x => x.TvDB_ImageWideBannerID).IsRequired().SetLocalValueGenerated();
                model.Property(x => x.Id).IsRequired();
                model.Property(x => x.SeriesID).IsRequired();
                model.Property(x => x.Enabled).IsRequired();

            }
            {
                var model = builder.Entity<TvDB_Series>();
                model.ToTable("TvDB_Series").HasKey(x => x.TvDB_SeriesID);
//                //model.HasIndex(x => x.TvDB_SeriesID).IsUnique().ForSqlServerIsClustered().HasName("PK_TvDB_Series");
                model.HasIndex(x => x.SeriesID).IsUnique().HasName("UIX_TvDB_Series_SeriesID");

                model.Property(x => x.TvDB_SeriesID).IsRequired().SetLocalValueGenerated();
                model.Property(x => x.SeriesID).IsRequired();
                model.Property(x => x.Status).HasMaxLength(100);
                model.Property(x => x.Banner).HasMaxLength(100);
                model.Property(x => x.Fanart).HasMaxLength(100);
                model.Property(x => x.Poster).HasMaxLength(100);
                model.Property(x => x.Lastupdated).HasMaxLength(100);
            }
            {
                var model = builder.Entity<SVR_VideoLocal_Place>();
                model.ToTable("VideoLocal_Place").HasKey(x => x.VideoLocal_Place_ID);
//                //model.HasIndex(x => x.VideoLocal_Place_ID).IsUnique().ForSqlServerIsClustered().HasName("PK_VideoLocal_Place");
                model.Property(x => x.VideoLocal_Place_ID).IsRequired().SetLocalValueGenerated();
                model.Property(x => x.VideoLocalID).IsRequired();
                model.Property(x => x.FilePath).IsRequired();
                model.Property(x => x.ImportFolderID).IsRequired();
                model.Property(x => x.ImportFolderType).IsRequired();
            }
            {
                var model = builder.Entity<VideoLocal_User>();
                model.ToTable("VideoLocal_User").HasKey(x => x.VideoLocal_UserID);
//                //model.HasIndex(x => x.VideoLocal_UserID).IsUnique().ForSqlServerIsClustered().HasName("PK_VideoLocal_User");
                model.HasIndex(x => new { x.JMMUserID, x.VideoLocalID}).IsUnique().HasName("UIX_VideoLocal_User_User_VideoLocalID");
                model.Property(x => x.VideoLocal_UserID).IsRequired().SetLocalValueGenerated();
                model.Property(x => x.JMMUserID).IsRequired();
                model.Property(x => x.VideoLocalID).IsRequired();
                model.Property(x => x.ResumePosition).IsRequired().HasDefaultValue(0);
            }
            {
                var model = builder.Entity<SVR_VideoLocal>();
                model.ToTable("VideoLocal").HasKey(x => x.VideoLocalID);
//                //model.HasIndex(x => x.VideoLocalID).IsUnique().ForSqlServerIsClustered().HasName("PK_VideoLocal");
                model.HasIndex(x => x.Hash).IsUnique().HasName("UIX_IX_VideoLocal_Hash");
                model.Property(x => x.VideoLocalID).IsRequired().SetLocalValueGenerated();
                model.Property(x => x.Hash).IsRequired().HasMaxLength(50);
                model.Property(x => x.CRC32).HasMaxLength(50).HasDefaultValue(null);
                model.Property(x => x.MD5).HasMaxLength(50).HasDefaultValue(null);
                model.Property(x => x.SHA1).HasMaxLength(50).HasDefaultValue(null);
                model.Property(x => x.HashSource).IsRequired();
                model.Property(x => x.FileSize).IsRequired();
                model.Property(x => x.FileName).HasDefaultValue(null);
                model.Property(x => x.IsIgnored).IsRequired();
                model.Property(x => x.DateTimeUpdated).IsRequired();
                model.Property(x => x.DateTimeCreated).IsRequired();
                model.Property(x => x.IsVariation).IsRequired();
                model.Property(x => x.MediaVersion).IsRequired().HasDefaultValue(0);
                model.Property(x => x.MediaSize).IsRequired().HasDefaultValue(0);
                model.Property(x => x.VideoCodec).IsRequired().HasDefaultValue("");
                model.Property(x => x.VideoBitrate).IsRequired().HasDefaultValue("");
                model.Property(x => x.VideoBitDepth).IsRequired().HasDefaultValue("");
                model.Property(x => x.VideoFrameRate).IsRequired().HasDefaultValue("");
                model.Property(x => x.VideoResolution).IsRequired().HasDefaultValue("");
                model.Property(x => x.AudioCodec).IsRequired().HasDefaultValue("");
                model.Property(x => x.AudioBitrate).IsRequired().HasDefaultValue("");
                model.Property(x => x.Duration).IsRequired().HasDefaultValue(0);
            }
        }


    }
}
