using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Video.Release;
using Shoko.Abstractions.Video.Services;
using Shoko.QueueProcessor.Acquisition.Attributes;
using Shoko.QueueProcessor.Builder;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Services;

#pragma warning disable CS8618
#nullable enable
namespace Shoko.Server.Scheduling.Jobs.Shoko;

/// <summary>
/// Generic fallback job for release providers that do not register a specific
/// provider job class implementing IVideoReleaseProviderJob&lt;TProvider&gt;.
/// Runs a single enabled provider identified by <see cref="ProviderID"/>.
/// </summary>
[DatabaseRequired]
[JobKeyGroup(JobKeyGroup.Import)]
public class ProcessReleaseProviderJob : BaseJob
{
    private readonly IVideoReleaseService _videoReleaseService;
    private readonly VideoLocalRepository _videoLocals;

    private VideoLocal _vlocal;
    private string? _fileName;

    public int VideoLocalID { get; set; }

    public bool SkipMyList { get; set; }

    [JobKeyMember]
    public Guid ProviderID { get; set; }

    public override string TypeName => "Get Release Info for Video";

    public override string Title => "Getting Release Info for Video";

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
        _logger.LogInformation("Processing {Job}: {FileName} (Provider={ProviderID})", nameof(ProcessReleaseProviderJob), _fileName ?? VideoLocalID.ToString(), ProviderID);

        if (_vlocal == null)
        {
            _vlocal = _videoLocals.GetByID(VideoLocalID);
            if (_vlocal == null) return;
        }

        // Skip if a higher-priority provider already found a release
        if (_videoReleaseService.GetCurrentReleaseForVideo(_vlocal) is not null)
        {
            _logger.LogTrace("Release already found for {FileName}, skipping provider {ProviderID}.", _fileName, ProviderID);
            return;
        }

        var providerInfo = _videoReleaseService.GetProviderInfo(ProviderID);
        if (providerInfo is null)
        {
            _logger.LogWarning("Provider {ProviderID} not found, skipping.", ProviderID);
            return;
        }

        var request = new ReleaseInfoContext { Video = _vlocal, IsAutomatic = true };
        var releaseInfo = await providerInfo.Provider.GetReleaseInfoForVideo(request, CancellationToken.None);
        if (releaseInfo is null || releaseInfo.CrossReferences.Count < 1)
        {
            _logger.LogTrace("No release found for {FileName} via provider {ProviderName}.", _fileName, providerInfo.Name);
            return;
        }

        await _videoReleaseService.SaveReleaseForVideo(_vlocal, releaseInfo, providerInfo.Name, addToMylist: !SkipMyList);
    }

    public ProcessReleaseProviderJob(IVideoReleaseService videoReleaseService, VideoLocalRepository videoLocals)
    {
        _videoReleaseService = videoReleaseService;
        _videoLocals = videoLocals;
    }

    protected ProcessReleaseProviderJob() { }
}
