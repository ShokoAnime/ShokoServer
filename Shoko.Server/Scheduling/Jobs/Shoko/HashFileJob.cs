using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Plugin.Abstractions.Services;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Scheduling.Attributes;
using Shoko.Server.Scheduling.Concurrency;
using Shoko.Server.Utilities;

#pragma warning disable CS8618
#nullable enable
namespace Shoko.Server.Scheduling.Jobs.Shoko;

[DatabaseRequired]
[LimitConcurrency]
[JobKeyGroup(JobKeyGroup.Import)]
public class HashFileJob : BaseJob
{

    private readonly IVideoHashingService _videoHashingService;

    public string FilePath { get; set; }

    public bool ForceHash { get; set; }

    public override string TypeName => "Hash File";

    public override string Title => "Hashing File";

    public override Dictionary<string, object> Details
    {
        get
        {
            var result = new Dictionary<string, object> { { "File Path", Utils.GetDistinctPath(FilePath) } };
            if (ForceHash) result["Force"] = true;
            return result;
        }
    }

    protected HashFileJob() { }

    public HashFileJob(IVideoHashingService videoHashingService)
    {
        _videoHashingService = videoHashingService;
    }

    public override async Task Process()
    {
        try
        {
            _logger.LogInformation("Processing {Job}: {FileName}", nameof(HashFileJob), Utils.GetDistinctPath(FilePath));
            await _videoHashingService.GetHashesForPath(FilePath, !ForceHash);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing {Job}: {FileName}", nameof(HashFileJob), Utils.GetDistinctPath(FilePath));
        }
    }

}
