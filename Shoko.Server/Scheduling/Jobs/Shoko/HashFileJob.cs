using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Video.Services;
using Shoko.QueueProcessor.Acquisition.Attributes;
using Shoko.QueueProcessor.Builder;
using Shoko.QueueProcessor.Concurrency;
using Shoko.Server.Services;

namespace Shoko.Server.Scheduling.Jobs.Shoko;

[DatabaseRequired]
[LimitConcurrency(2)]
[LongRunning]
[JobKeyGroup(JobKeyGroup.Import)]
public class HashFileJob(IVideoHashingService videoHashingService) : BaseJob
{
    public string FilePath { get; set; }

    public bool ForceHash { get; set; }

    public bool SkipEvents { get; set; }

    public bool SkipFindRelease { get; set; }

    public override string TypeName => "Hash File";

    public override string Title => "Hashing File";

    public override Dictionary<string, object> Details
    {
        get
        {
            var result = new Dictionary<string, object> { { "File Path", VideoService.GetDistinctPath(FilePath) } };
            if (ForceHash) result["Force"] = true;
            if (!SkipEvents) result["Add to MyList"] = true;
            if (!SkipFindRelease) result["Find Release"] = true;
            return result;
        }
    }

    public override async Task Execute()
    {
        try
        {
            _logger.LogInformation("Processing {Job}: {FileName}", nameof(HashFileJob), VideoService.GetDistinctPath(FilePath));
            await videoHashingService.GetHashesForPath(FilePath, useExistingHashes: !ForceHash, skipFindRelease: SkipFindRelease, skipEvents: SkipEvents);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing {Job}: {FileName}", nameof(HashFileJob), VideoService.GetDistinctPath(FilePath));
        }
    }
}
