using System.Reflection;
using FluentNHibernate.Mapping;
using Shoko.Server.Commands;
using Shoko.Server.Commands.AniDB;
using Shoko.Server.Commands.Attributes;
using Shoko.Server.Commands.Import;
using Shoko.Server.Commands.Plex;
using Shoko.Server.Commands.TvDB;
using Shoko.Server.Models;

namespace Shoko.Server.Mappings;

public class CommandRequestMap : ClassMap<CommandRequest>
{
    public CommandRequestMap()
    {
        Not.LazyLoad();
        Id(x => x.CommandRequestID);
        Map(x => x.CommandDetails).Not.Nullable();
        Map(x => x.CommandID).Not.Nullable();
        DiscriminateSubClassesOnColumn("CommandType", 98).Not.Nullable();
        Map(x => x.CommandType, "CommandType").ReadOnly().Not.Nullable();
        Map(x => x.DateTimeUpdated).Not.Nullable();
        Map(x => x.Priority).Not.Nullable();
    }

    public class CommandRequestSubclass<T> : SubclassMap<T>
    {
        public CommandRequestSubclass()
        {
            var attr = typeof(T).GetCustomAttribute<CommandAttribute>();
            if (attr == null || attr.RequestType == 0) return;
            DiscriminatorValue((int)attr.RequestType);
        }
    }

    public class CommandRequest_NullMap : CommandRequestSubclass<CommandRequest_Null> {}
    public class CommandRequest_RefreshAnimeMap : CommandRequestSubclass<CommandRequest_RefreshAnime> {}
    public class CommandRequest_RefreshGroupFilterMap : CommandRequestSubclass<CommandRequest_RefreshGroupFilter> { }
    public class CommandRequest_AddFileToMyListMap : CommandRequestSubclass<CommandRequest_AddFileToMyList> { }
    public class CommandRequest_DeleteFileFromMyListMap : CommandRequestSubclass<CommandRequest_DeleteFileFromMyList> { }
    public class CommandRequest_GetAniDBTitlesMap : CommandRequestSubclass<CommandRequest_GetAniDBTitles> { }
    public class CommandRequest_GetAnimeHTTPMap : CommandRequestSubclass<CommandRequest_GetAnimeHTTP> { }
    public class CommandRequest_GetAnimeHTTP_ForceMap : CommandRequestSubclass<CommandRequest_GetAnimeHTTP_Force> { }
    public class CommandRequest_GetCalendarMap : CommandRequestSubclass<CommandRequest_GetCalendar> { }
    public class CommandRequest_GetFileMap : CommandRequestSubclass<CommandRequest_GetFile> { }
    public class CommandRequest_GetReleaseGroupMap : CommandRequestSubclass<CommandRequest_GetReleaseGroup> { }
    public class CommandRequest_GetReleaseGroupStatusMap : CommandRequestSubclass<CommandRequest_GetReleaseGroupStatus> { }
    public class CommandRequest_GetUpdatedMap : CommandRequestSubclass<CommandRequest_GetUpdated> { }
    public class CommandRequest_SyncMyListMap : CommandRequestSubclass<CommandRequest_SyncMyList> { }
    public class CommandRequest_SyncMyVotesMap : CommandRequestSubclass<CommandRequest_SyncMyVotes> { }
    public class CommandRequest_UpdateMyListFileStatusMap : CommandRequestSubclass<CommandRequest_UpdateMyListFileStatus> { }
    public class CommandRequest_UpdateMyListStatsMap : CommandRequestSubclass<CommandRequest_UpdateMyListStats> { }
    public class CommandRequest_VoteAnimeMap : CommandRequestSubclass<CommandRequest_VoteAnime> { }
    public class CommandRequest_AVDumpFileMap : CommandRequestSubclass<CommandRequest_AVDumpFile> { }
    public class CommandRequest_DownloadAniDBImagesMap : CommandRequestSubclass<CommandRequest_DownloadAniDBImages> { }
    public class CommandRequest_DownloadImageMap : CommandRequestSubclass<CommandRequest_DownloadImage> { }
    public class CommandRequest_HashFileMap : CommandRequestSubclass<CommandRequest_HashFile> { }
    public class CommandRequest_LinkFileManuallyMap : CommandRequestSubclass<CommandRequest_LinkFileManually> { }
    public class CommandRequest_ProcessFileMap : CommandRequestSubclass<CommandRequest_ProcessFile> { }
    public class CommandRequest_ReadMediaInfoMap : CommandRequestSubclass<CommandRequest_ReadMediaInfo> { }
    public class CommandRequest_ValidateAllImagesMap : CommandRequestSubclass<CommandRequest_ValidateAllImages> { }
    public class CommandRequest_MovieDBSearchAnimeMap : CommandRequestSubclass<CommandRequest_MovieDBSearchAnime> { }
    public class CommandRequest_PlexSyncWatchedMap : CommandRequestSubclass<CommandRequest_PlexSyncWatched> { }
    public class CommandRequest_TraktCollectionEpisodeMap : CommandRequestSubclass<CommandRequest_TraktCollectionEpisode> { }
    public class CommandRequest_TraktHistoryEpisodeMap : CommandRequestSubclass<CommandRequest_TraktHistoryEpisode> { }
    public class CommandRequest_TraktSearchAnimeMap : CommandRequestSubclass<CommandRequest_TraktSearchAnime> { }
    public class CommandRequest_TraktSyncCollectionMap : CommandRequestSubclass<CommandRequest_TraktSyncCollection> { }
    public class CommandRequest_TraktSyncCollectionSeriesMap : CommandRequestSubclass<CommandRequest_TraktSyncCollectionSeries> { }
    public class CommandRequest_TraktUpdateAllSeriesMap : CommandRequestSubclass<CommandRequest_TraktUpdateAllSeries> { }
    public class CommandRequest_TraktUpdateInfoMap : CommandRequestSubclass<CommandRequest_TraktUpdateInfo> { }
    public class CommandRequest_LinkAniDBTvDBMap : CommandRequestSubclass<CommandRequest_LinkAniDBTvDB> { }
    public class CommandRequest_TvDBDownloadImagesMap : CommandRequestSubclass<CommandRequest_TvDBDownloadImages> { }
    public class CommandRequest_TvDBSearchAnimeMap : CommandRequestSubclass<CommandRequest_TvDBSearchAnime> { }
    public class CommandRequest_TvDBUpdateSeriesMap : CommandRequestSubclass<CommandRequest_TvDBUpdateSeries> { }
}
