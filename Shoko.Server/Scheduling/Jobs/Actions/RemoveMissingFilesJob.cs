using System.Collections.Generic;
using System.Threading.Tasks;
using Shoko.Abstractions.Video.Services;
using Shoko.QueueProcessor.Acquisition.Attributes;
using Shoko.QueueProcessor.Builder;
using Shoko.QueueProcessor.Concurrency;

namespace Shoko.Server.Scheduling.Jobs.Actions;

[DatabaseRequired]
[JobKeyMember("RemoveMissingFiles")]
[JobKeyGroup(JobKeyGroup.Legacy)]
[DisallowConcurrentExecution]
internal class RemoveMissingFilesJob(IVideoService videoService) : BaseJob
{
    [JobKeyMember]
    public bool RemoveMylist { get; set; }

    public override string TypeName => "Remove Missing Files";

    public override string Title => "Removing Missing Files";

    public override Dictionary<string, object> Details => new()
    {
        {
            "Remove From MyList", RemoveMylist
        }
    };

    public override async Task Execute()
    {
        await videoService.RemoveMissingVideoFiles(RemoveMylist);
    }
}
