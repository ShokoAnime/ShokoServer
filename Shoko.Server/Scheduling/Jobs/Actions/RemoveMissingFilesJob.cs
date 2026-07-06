using System.Collections.Generic;
using System.Threading.Tasks;
using Shoko.QueueProcessor.Acquisition.Attributes;
using Shoko.QueueProcessor.Builder;
using Shoko.QueueProcessor.Concurrency;
using Shoko.Server.Services;

namespace Shoko.Server.Scheduling.Jobs.Actions;

[DatabaseRequired]
[JobKeyMember("RemoveMissingFiles")]
[JobKeyGroup(JobKeyGroup.Legacy)]
[DisallowConcurrentExecution]
internal class RemoveMissingFilesJob(ActionService actionService) : BaseJob
{
    [JobKeyMember]
    public bool RemoveMyList { get; set; }

    public override string TypeName => "Remove Missing Files";

    public override string Title => "Removing Missing Files";

    public override Dictionary<string, object> Details => new()
    {
        {
            "Remove From MyList", RemoveMyList
        }
    };

    public override async Task Execute()
    {
        await actionService.RemoveRecordsWithoutPhysicalFiles(RemoveMyList);
    }
}
