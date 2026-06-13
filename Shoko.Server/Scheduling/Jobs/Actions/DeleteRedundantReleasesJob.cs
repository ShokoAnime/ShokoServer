using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Video.Services;
using Shoko.QueueProcessor.Acquisition.Attributes;
using Shoko.QueueProcessor.Builder;
using Shoko.QueueProcessor.Concurrency;
using Shoko.Server.Repositories.Cached;

#pragma warning disable CS8618
namespace Shoko.Server.Scheduling.Jobs.Actions;

[DatabaseRequired]
[DisallowConcurrentExecution]
[JobKeyGroup(JobKeyGroup.Actions)]
public class DeleteRedundantReleasesJob : BaseJob
{
    private readonly IVideoService _videoService;

    private readonly VideoLocal_PlaceRepository _places;

    public List<int> PlaceIDs { get; set; } = [];

    public override string TypeName => "Delete Redundant Releases";

    public override string Title => $"Deleting {PlaceIDs.Count} redundant file(s)";

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
            var place = _places.GetByID(placeID);
            if (place is null)
            {
                _logger.LogDebug("Skipping place {PlaceID}: not found", placeID);
                continue;
            }

            _logger.LogDebug("Deleting redundant place {PlaceID}: {Path}", placeID, place.Path ?? place.RelativePath);
            await _videoService.DeleteVideoFile(place, removeFile: true, removeFolders: false);
            deleted++;
        }

        _logger.LogInformation("Deleted {Deleted}/{Total} redundant release file(s)", deleted, PlaceIDs.Count);
    }

    public DeleteRedundantReleasesJob(IVideoService videoService, VideoLocal_PlaceRepository places)
    {
        _videoService = videoService;
        _places = places;
    }

    protected DeleteRedundantReleasesJob() { }
}
