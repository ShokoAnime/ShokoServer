using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Video.Release;
using Shoko.Abstractions.Video.Services;
using Shoko.QueueProcessor.Acquisition.Attributes;
using Shoko.QueueProcessor.Builder;
using Shoko.QueueProcessor.Chain;
using Shoko.Server.Models.Release;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Services;

namespace Shoko.Server.Scheduling.Jobs.Shoko;

/// <summary>
/// Generic fallback job for release providers that do not register a specific
/// provider job class implementing IVideoReleaseProviderJob&lt;TProvider&gt;.
/// Runs a single enabled provider identified by <see cref="ProviderID"/>.
/// </summary>
[DatabaseRequired]
[JobKeyGroup(JobKeyGroup.Import)]
public class ProcessReleaseProviderJob(IVideoReleaseService videoReleaseService, VideoLocalRepository videoLocals, StoredReleaseInfo_MatchAttemptRepository matchAttempts) : BaseJob
{
    private readonly VideoReleaseService _videoReleaseService = (VideoReleaseService)videoReleaseService;

    private VideoLocal? _vlocal;

    private StoredReleaseInfo_MatchAttempt? _matchAttempt;

    private ReleaseProviderInfo? _providerInfo;

    private int? _attemptNumber;

    private string? _fileName;

    public int VideoLocalID { get; set; }

    public bool SkipEvents { get; set; }

    public int MatchAttemptID { get; set; }

    public Guid ProviderID { get; set; }

    public override string TypeName => "Get Release Information for Video From Provider";

    public override string Title => "Getting Release Information for Video From Provider";

    public override Dictionary<string, object> Details
    {
        get
        {
            var result = new Dictionary<string, object>();
            if (_providerInfo is not null)
                result["Provider"] = _providerInfo.Name;
            else
                result["Provider ID"] = ProviderID;
            if (_attemptNumber.HasValue)
            {
                result["Attempt Number"] = _attemptNumber;
                if (_providerInfo is not null)
                    result["Attempt Chain Index"] = _matchAttempt!.AttemptedProviderNames.IndexOf(_providerInfo.Name);
            }
            if (string.IsNullOrEmpty(_fileName))
                result["Video"] = VideoLocalID;
            else
                result["File Path"] = _fileName;
            if (!SkipEvents) result["Add to MyList"] = true;
            return result;
        }
    }

    public override void PostInit()
    {
        _vlocal = videoLocals.GetByID(VideoLocalID);
        _matchAttempt = matchAttempts.GetByID(MatchAttemptID);
        if (_vlocal is not null && _matchAttempt is not null)
            _attemptNumber = matchAttempts.GetByEd2kAndFileSize(_vlocal.Hash, _vlocal.FileSize)
                .FindIndex(m => m.StoredReleaseInfo_MatchAttemptID == MatchAttemptID) + 1;
        _providerInfo = _videoReleaseService.GetProviderInfo(ProviderID);
        _fileName = VideoService.GetDistinctPath(_vlocal?.FirstValidPlace?.Path);
    }

    public override async Task Execute()
    {
        _logger.LogInformation("Processing {Job}: {FileName} (Provider={ProviderID})", nameof(ProcessReleaseProviderJob), _fileName ?? VideoLocalID.ToString(), ProviderID);

        _vlocal ??= videoLocals.GetByID(VideoLocalID);
        _matchAttempt ??= matchAttempts.GetByID(MatchAttemptID);
        if (_vlocal is null || _matchAttempt is null) return;

        _providerInfo ??= _videoReleaseService.GetProviderInfo(ProviderID);
        if (_providerInfo is null)
        {
            _logger.LogWarning("Provider with id {ProviderID} not found, skipping.", ProviderID);
            return;
        }

        // Skip if the chain already has a definitive (non-deferred) result.
        if (_matchAttempt.IsCompleted)
        {
            if (_matchAttempt.ProviderID.HasValue)
                _logger.LogTrace("Release already found for {FileName}, skipping provider {ProviderName}.", _fileName, _providerInfo.Name);
            return;
        }

        try
        {
            var request = new ReleaseInfoContext { Video = _vlocal, IsAutomatic = true };
            var release = await _providerInfo.Provider.GetReleaseInfoForVideo(request, CancellationToken.None);
            if (release is null || release.CrossReferences.Count < 1)
            {
                _logger.LogTrace("No release found for {FileName} via provider {ProviderName}.", _fileName, _providerInfo.Name);
                return;
            }

            var releaseInfo = new ReleaseInfoWithProvider(release, _providerInfo.Name);
            _matchAttempt.ProviderID = _providerInfo.ID;
            _matchAttempt.ProviderName = _providerInfo.Name;
            await _videoReleaseService.SaveReleaseForVideo(_vlocal, releaseInfo, matchAttempt: _matchAttempt, skipEvents: SkipEvents);
        }
        catch (Exception ex)
        {
            _matchAttempt.AttemptEndedAt = DateTime.Now;
            matchAttempts.Save(_matchAttempt);
            await _videoReleaseService.FireSearchCompleted(_vlocal, _matchAttempt, null, ex);
            throw new ChainAbortException(ex);
        }
    }

}
