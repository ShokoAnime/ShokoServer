using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Quartz;
using Shoko.Models.Client;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Extensions;
using Shoko.Server.Extensions;
using Shoko.Server.Models;
using Shoko.Server.Providers.AniDB.Release;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Cached.AniDB;
using Shoko.Server.Scheduling;
using Shoko.Server.Scheduling.Jobs.AniDB;
using Media = Shoko.Models.PlexAndKodi.Media;

#pragma warning disable CS0618
namespace Shoko.Server.Services;

public class VideoLocalService
{
    private readonly ConcurrentDictionary<int, object> _userLock = new();

    private readonly ISchedulerFactory _schedulerFactory;

    private readonly StoredReleaseInfoRepository _storedReleaseInfo;

    private readonly AniDB_EpisodeRepository _aniDBEpisode;

    private readonly VideoLocal_UserRepository _vlUsers;

    private readonly ILogger<VideoLocalService> _logger;

    public VideoLocalService(VideoLocal_UserRepository vlUsers, ISchedulerFactory schedulerFactory, ILogger<VideoLocalService> logger, StoredReleaseInfoRepository storedReleaseInfo, AniDB_EpisodeRepository aniDBEpisode)
    {
        _vlUsers = vlUsers;
        _schedulerFactory = schedulerFactory;
        _logger = logger;
        _storedReleaseInfo = storedReleaseInfo;
        _aniDBEpisode = aniDBEpisode;
    }

    public SVR_VideoLocal_User GetOrCreateUserRecord(SVR_VideoLocal vl, int userID)
    {
        SVR_VideoLocal_User userRecord;
        var lockObj = _userLock.GetOrAdd(vl.VideoLocalID, _ => new object());
        try
        {
            Monitor.Enter(lockObj);
            userRecord = _vlUsers.GetByUserIDAndVideoLocalID(userID, vl.VideoLocalID);
            if (userRecord != null)
                return userRecord;
            userRecord = new SVR_VideoLocal_User(userID, vl.VideoLocalID);
            _vlUsers.Save(userRecord);
        }
        finally
        {
            _userLock.TryRemove(new KeyValuePair<int, object>(vl.VideoLocalID, lockObj));
        }

        return userRecord;
    }

    public CL_VideoLocal GetV1Contract(SVR_VideoLocal vl, int userID)
    {
        var cl = new CL_VideoLocal
        {
            CRC32 = vl.CRC32,
            DateTimeUpdated = vl.DateTimeUpdated,
            FileName = vl.FileName,
            FileSize = vl.FileSize,
            Hash = vl.Hash,
            HashSource = vl.HashSource,
            IsIgnored = vl.IsIgnored ? 1 : 0,
            IsVariation = vl.IsVariation ? 1 : 0,
            Duration = (long)(vl.MediaInfo?.GeneralStream.Duration ?? 0),
            MD5 = vl.MD5,
            SHA1 = vl.SHA1,
            VideoLocalID = vl.VideoLocalID,
            Places = vl.Places.Select(a => a.ToClient()).ToList()
        };

        var userRecord = _vlUsers.GetByUserIDAndVideoLocalID(userID, vl.VideoLocalID);
        if (userRecord?.WatchedDate == null)
        {
            cl.IsWatched = 0;
            cl.WatchedDate = null;
        }
        else
        {
            cl.IsWatched = 1;
            cl.WatchedDate = userRecord.WatchedDate;
        }
        cl.ResumePosition = userRecord?.ResumePosition ?? 0;

        try
        {

            if (vl.MediaInfo?.GeneralStream != null) cl.Media = new Media(vl.VideoLocalID, vl.MediaInfo);
        }
        catch (Exception e)
        {
            _logger.LogError("There was an error generating a Desktop client contract: {Ex}", e);
        }

        return cl;
    }

    public async Task ScheduleRemovalFromMyList(SVR_VideoLocal video)
    {
        var scheduler = await _schedulerFactory.GetScheduler();
        if (_storedReleaseInfo.GetByEd2kAndFileSize(video.Hash, video.FileSize) is { ReleaseURI: not null } releaseInfo && releaseInfo.ReleaseURI.StartsWith(AnidbReleaseProvider.ReleasePrefix))
        {
            await scheduler.StartJob<DeleteFileFromMyListJob>(c =>
                {
                    c.Hash = video.Hash;
                    c.FileSize = video.FileSize;
                }
            );
        }
        else
        {
            var xrefs = video.EpisodeCrossReferences;
            foreach (var xref in xrefs)
            {
                if (xref.AnimeID is 0)
                    continue;

                var ep = _aniDBEpisode.GetByEpisodeID(xref.EpisodeID);
                if (ep is null)
                    continue;

                await scheduler.StartJob<DeleteFileFromMyListJob>(c =>
                    {
                        c.AnimeID = xref.AnimeID;
                        c.EpisodeType = ep.EpisodeTypeEnum;
                        c.EpisodeNumber = ep.EpisodeNumber;
                    }
                );
            }
        }
    }

    public CL_VideoDetailed GetV1DetailedContract(SVR_VideoLocal vl, int userID)
    {
        // get the cross ref episode
        var xrefs = vl.EpisodeCrossReferences;
        if (xrefs.Count == 0) return null;

        var userRecord = _vlUsers.GetByUserIDAndVideoLocalID(userID, vl.VideoLocalID);
        var aniFile = vl.ReleaseInfo is { ReleaseURI: not null } r && r.ReleaseURI.StartsWith(AnidbReleaseProvider.ReleasePrefix) ? r : null; // to prevent multiple db calls
        var relGroup = vl.ReleaseGroup?.ToClient(); // to prevent multiple db calls
        var mediaInfo = vl.MediaInfo as IMediaInfo; // to prevent multiple db calls
        var audioStream = mediaInfo.AudioStreams is { Count: > 0 } ? mediaInfo.AudioStreams[0] : null;
        var videoStream = mediaInfo.VideoStream;
        var cl = new CL_VideoDetailed
        {
            Percentage = xrefs[0].Percentage,
            EpisodeOrder = xrefs[0].EpisodeOrder,
            CrossRefSource = 0,
            AnimeEpisodeID = xrefs[0].EpisodeID,
            VideoLocal_FileName = vl.FileName,
            VideoLocal_Hash = vl.Hash,
            VideoLocal_FileSize = vl.FileSize,
            VideoLocalID = vl.VideoLocalID,
            VideoLocal_IsIgnored = vl.IsIgnored ? 1 : 0,
            VideoLocal_IsVariation = vl.IsVariation ? 1 : 0,
            Places = vl.Places.Select(a => a.ToClient()).ToList(),
            VideoLocal_MD5 = vl.MD5,
            VideoLocal_SHA1 = vl.SHA1,
            VideoLocal_CRC32 = vl.CRC32,
            VideoLocal_HashSource = vl.HashSource,
            VideoLocal_IsWatched = userRecord?.WatchedDate == null ? 0 : 1,
            VideoLocal_WatchedDate = userRecord?.WatchedDate,
            VideoLocal_ResumePosition = userRecord?.ResumePosition ?? 0,
            VideoInfo_AudioBitrate = audioStream?.BitRate.ToString(),
            VideoInfo_AudioCodec = audioStream?.Codec.Simplified,
            VideoInfo_Duration = (long)(mediaInfo?.Duration.TotalMilliseconds ?? 0),
            VideoInfo_VideoBitrate = videoStream?.BitRate.ToString() ?? "0",
            VideoInfo_VideoBitDepth = videoStream?.BitDepth.ToString() ?? "0",
            VideoInfo_VideoCodec = videoStream?.Codec.Simplified,
            VideoInfo_VideoFrameRate = videoStream?.FrameRate.ToString(),
            VideoInfo_VideoResolution = videoStream?.Resolution,
            AniDB_File_FileExtension = Path.GetExtension(aniFile?.OriginalFilename) ?? string.Empty,
            AniDB_File_LengthSeconds = (int?)mediaInfo?.Duration.TotalSeconds ?? 0,
            AniDB_AnimeID = xrefs.FirstOrDefault(xref => xref.AnimeID > 0)?.AnimeID,
            AniDB_CRC = vl.CRC32,
            AniDB_MD5 = vl.MD5,
            AniDB_SHA1 = vl.SHA1,
            AniDB_Episode_Rating = 0,
            AniDB_Episode_Votes = 0,
            AniDB_File_AudioCodec = audioStream?.Codec.Simplified ?? string.Empty,
            AniDB_File_VideoCodec = videoStream?.Codec.Simplified ?? string.Empty,
            AniDB_File_VideoResolution = vl.VideoResolution,
            AniDB_Anime_GroupName = aniFile?.GroupName ?? string.Empty,
            AniDB_Anime_GroupNameShort = aniFile?.GroupShortName ?? string.Empty,
            AniDB_File_Description = aniFile?.Comment ?? string.Empty,
            AniDB_File_ReleaseDate = aniFile?.ReleasedAt is { } releasedAt ? (int)(releasedAt.ToDateTime(TimeOnly.MinValue).ToUniversalTime() - DateTime.UnixEpoch).TotalSeconds : 0,
            AniDB_File_Source = aniFile?.LegacySource ?? string.Empty,
            AniDB_FileID = aniFile is not null ? int.Parse(aniFile?.ReleaseURI[AnidbReleaseProvider.ReleasePrefix.Length..]) : 0,
            AniDB_GroupID = relGroup.GroupID,
            AniDB_File_FileVersion = aniFile?.Revision ?? 1,
            AniDB_File_IsCensored = aniFile?.IsCensored ?? false ? 1 : 0,
            AniDB_File_IsChaptered = aniFile?.IsChaptered ?? false ? 1 : 0,
            AniDB_File_IsDeprecated = aniFile?.IsCorrupted ?? false ? 1 : 0,
            AniDB_File_InternalVersion = 4,
            LanguagesAudio = aniFile?.AudioLanguages?.Select(a => a.GetString()).Join(',') ?? string.Empty,
            LanguagesSubtitle = aniFile?.SubtitleLanguages?.Select(a => a.GetString()).Join(',') ?? string.Empty,
            ReleaseGroup = relGroup,
            Media = mediaInfo is null ? null : new Media(vl.VideoLocalID, mediaInfo),
        };

        return cl;
    }
}
