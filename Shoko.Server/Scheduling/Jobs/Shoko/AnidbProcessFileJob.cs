using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Video.Release;
using Shoko.Abstractions.Video.Services;
using Shoko.QueueProcessor.Abstractions;
using Shoko.QueueProcessor.Acquisition.Attributes;
using Shoko.QueueProcessor.Builder;
using Shoko.QueueProcessor.Concurrency;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Providers.AniDB.Release;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Scheduling.Concurrency;
using Shoko.Server.Services;

#pragma warning disable CS8618
#nullable enable
namespace Shoko.Server.Scheduling.Jobs.Shoko;

[DatabaseRequired]
[LimitConcurrency(4)]
[AniDBUdpRateLimited]
[DisallowConcurrencyGroup(ConcurrencyGroups.AniDB_UDP)]
[JobKeyGroup(JobKeyGroup.Import)]
public class AnidbProcessFileJob : BaseJob, IVideoReleaseProviderJob<AnidbReleaseProvider>
{
    private readonly IVideoReleaseService _videoReleaseService;
    private readonly VideoLocalRepository _videoLocals;

    private VideoLocal _vlocal;
    private string? _fileName;

    public int VideoLocalID { get; set; }

    public bool SkipMyList { get; set; }

    public override string TypeName => "Get AniDB Release Info for Video";

    public override string Title => "Getting AniDB Release Info for Video";

    public override Dictionary<string, object> Details
    {
        get
        {
            var result = new Dictionary<string, object>();
            if (string.IsNullOrEmpty(_fileName))
                result["Video"] = VideoLocalID;
            else
                result["File Path"] = _fileName;
            if (!SkipMyList) result["Add to MyList"] = true;
            return result;
        }
    }

    public override void PostInit()
    {
        _vlocal = _videoLocals.GetByID(VideoLocalID);
        if (_vlocal == null) throw new Exception($"VideoLocal not found: {VideoLocalID}");
        _fileName = VideoService.GetDistinctPath(_vlocal?.FirstValidPlace?.Path);
    }

    public override async Task Execute()
    {
        _logger.LogInformation("Processing {Job}: {FileName}", nameof(AnidbProcessFileJob), _fileName ?? VideoLocalID.ToString());

        if (_vlocal == null)
        {
            _vlocal = _videoLocals.GetByID(VideoLocalID);
            if (_vlocal == null) return;
        }

        // Skip if a higher-priority provider already found a release for this file
        if (_videoReleaseService.GetCurrentReleaseForVideo(_vlocal) is not null)
        {
            _logger.LogTrace("Release already found for {FileName}, skipping AniDB lookup.", _fileName);
            return;
        }

        // Get the AniDB provider instance managed by the release service (preserves memory cache)
        var providerInfo = _videoReleaseService.GetProviderInfo<AnidbReleaseProvider>();
        var request = new ReleaseInfoContext { Video = _vlocal, IsAutomatic = true };
        var releaseInfo = await providerInfo.Provider.GetReleaseInfoForVideo(request, CancellationToken.None);
        if (releaseInfo is null || releaseInfo.CrossReferences.Count < 1)
        {
            _logger.LogTrace("No AniDB release found for {FileName}.", _fileName);
            return;
        }

        await _videoReleaseService.SaveReleaseForVideo(_vlocal, releaseInfo, providerInfo.Name, addToMylist: !SkipMyList);
    }

    public AnidbProcessFileJob(IVideoReleaseService videoReleaseService, VideoLocalRepository videoLocals)
    {
        _videoReleaseService = videoReleaseService;
        _videoLocals = videoLocals;
    }

    protected AnidbProcessFileJob() { }
}
