using System.Collections.Generic;
using System.Threading.Tasks;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.QueueProcessor.Builder;
using Shoko.Server.Services;
using Shoko.QueueProcessor.Concurrency;

namespace Shoko.Server.Scheduling.Jobs.Actions;

[DatabaseRequired]
[JobKeyMember("RemoveMissingFiles")]
[JobKeyGroup(JobKeyGroup.Legacy)]
[DisallowConcurrentExecution]
internal class RemoveMissingFilesJob : BaseJob
{
    private readonly ActionService _actionService;

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
        await _actionService.RemoveRecordsWithoutPhysicalFiles(RemoveMyList);
    }

    public RemoveMissingFilesJob(ActionService actionService)
    {
        _actionService = actionService;
    }

    protected RemoveMissingFilesJob() { }
}
