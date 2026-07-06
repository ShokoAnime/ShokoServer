using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Shoko.Abstractions.Video.Services;
using Shoko.QueueProcessor.Acquisition.Attributes;
using Shoko.QueueProcessor.Builder;
using Shoko.QueueProcessor.Concurrency;
using Shoko.Server.Repositories.Cached;

namespace Shoko.Server.Scheduling.Jobs.Actions;

[DatabaseRequired]
[DisallowConcurrentExecution]
[JobKeyGroup(JobKeyGroup.Actions)]
public class DeleteRedundantReleasesJob(IVideoService videoService, VideoLocal_PlaceRepository places) : BaseJob
{

    public List<int> PlaceIDs { get; set; } = [];

    /// <summary>
    /// Hash key representing the places to be deleted.
    /// </summary>
    [JobKeyMember]
    public string Key
    {
        get => PlaceIDs is not null
            ? Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(PlaceIDs.Order().ToArray()))))
            : string.Empty;
        set { }
    }

    public override string TypeName => "Delete Redundant Files";

    public override string Title => $"Deleting {PlaceIDs.Count} Redundant File{(PlaceIDs.Count != 1 ? "s" : "")}";

    public override Dictionary<string, object> Details => new()
    {
        ["File Count"] = PlaceIDs.Count,
    };

    public override async Task Execute()
    {
        _logger.LogInformation("Deleting {Count} redundant release file(s)", PlaceIDs.Count);

        var deleted = 0;
        foreach (var placeID in PlaceIDs)
        {
            var place = places.GetByID(placeID);
            if (place is null)
            {
                _logger.LogDebug("Skipping place {PlaceID}: not found", placeID);
                continue;
            }

            _logger.LogDebug("Deleting redundant place {PlaceID}: {Path}", placeID, place.Path ?? place.RelativePath);
            await videoService.DeleteVideoFile(place, removeFile: true);
            deleted++;
        }

        _logger.LogInformation("Deleted {Deleted}/{Total} redundant release file(s)", deleted, PlaceIDs.Count);
    }
}
