using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Video.Release;
using Shoko.QueueProcessor.Abstractions;
using Shoko.QueueProcessor.Acquisition.Attributes;
using Shoko.QueueProcessor.Builder;
using Shoko.QueueProcessor.Chain;
using Shoko.QueueProcessor.Concurrency;
using Shoko.Server.Models.Release;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Providers.AniDB.Release;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Scheduling.Concurrency;
using Shoko.Server.Services;

namespace Shoko.Server.Scheduling.Jobs.Shoko;

[DatabaseRequired]
[LimitConcurrency(4)]
[AniDBUdpRateLimited]
[DisallowConcurrencyGroup(ConcurrencyGroups.AniDB_UDP)]
[JobKeyGroup(JobKeyGroup.Import)]
public class AnidbProcessFileJob(VideoReleaseService videoReleaseService, VideoLocalRepository videoLocals, StoredReleaseInfo_MatchAttemptRepository matchAttempts) : BaseJob, IVideoReleaseProviderJob<AnidbReleaseProvider>
{
    private VideoLocal? _vlocal;

    private StoredReleaseInfo_MatchAttempt? _matchAttempt;

    private int? _attemptNumber;

    private string? _fileName;

    public int VideoLocalID { get; set; }

    public bool SkipEvents { get; set; }

    public int MatchAttemptID { get; set; }

    public override string TypeName => "Get Release Information for Video From Provider";

    public override string Title => "Getting Release Information for Video From Provider";

    public override Dictionary<string, object> Details
    {
        get
        {
            var result = new Dictionary<string, object>
            {
                ["Provider"] = "AniDB"
            };
            if (_attemptNumber.HasValue)
            {
                result["Attempt Number"] = _attemptNumber;
                result["Attempt Chain Index"] = _matchAttempt!.AttemptedProviderNames.IndexOf("AniDB");
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
        _fileName = VideoService.GetDistinctPath(_vlocal?.FirstValidPlace?.Path);
    }

    public override async Task Execute()
    {
        _logger.LogInformation("Processing {Job}: {FileName}", nameof(AnidbProcessFileJob), _fileName ?? VideoLocalID.ToString());

        _vlocal ??= videoLocals.GetByID(VideoLocalID);
        _matchAttempt ??= matchAttempts.GetByID(MatchAttemptID);
        if (_vlocal is null || _matchAttempt is null) return;


        // Skip if the chain already has a definitive (non-deferred) result.
        if (_matchAttempt.IsCompleted)
        {
            if (_matchAttempt.ProviderID.HasValue)
                _logger.LogTrace("Release already found for {FileName}, skipping AniDB lookup.", _fileName);
            return;
        }

        try
        {
            // Get the AniDB provider instance managed by the release service (preserves memory cache)
            var providerInfo = videoReleaseService.GetProviderInfo<AnidbReleaseProvider>();
            var request = new ReleaseInfoContext { Video = _vlocal, IsAutomatic = true };
            var release = await providerInfo.Provider.GetReleaseInfoForVideo(request, CancellationToken.None);
            if (release is null || release.CrossReferences.Count < 1)
            {
                _logger.LogTrace("No AniDB release found for {FileName}.", _fileName);
                return;
            }

            var releaseInfo = new ReleaseInfoWithProvider(release, providerInfo.Name);
            _matchAttempt.ProviderID = providerInfo.ID;
            _matchAttempt.ProviderName = providerInfo.Name;
            await videoReleaseService.SaveReleaseForVideo(_vlocal, releaseInfo, matchAttempt: _matchAttempt, skipEvents: SkipEvents);
        }
        catch (Exception ex)
        {
            _matchAttempt.AttemptEndedAt = DateTime.Now;
            matchAttempts.Save(_matchAttempt);
            await videoReleaseService.FireSearchCompleted(_vlocal, _matchAttempt, null, ex);
            throw new ChainAbortException(ex);
        }
    }

}
