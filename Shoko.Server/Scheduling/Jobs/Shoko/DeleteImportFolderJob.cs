using System.Collections.Generic;
using System.Threading.Tasks;
using Shoko.Server.Repositories;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Scheduling.Attributes;
using Shoko.Server.Services;

namespace Shoko.Server.Scheduling.Jobs.Shoko;

[DatabaseRequired]
[JobKeyMember("DeleteImportFolder")]
[JobKeyGroup(JobKeyGroup.Actions)]
internal class DeleteImportFolderJob : BaseJob
{
    private readonly ActionService _actionService;

    public int ImportFolderID { get; set; }
    public override string TypeName => "Delete Import Folder";
    public override string Title => "Deleting Import Folder";
    public override Dictionary<string, object> Details => new()
    {
        {
            "Import Folder", RepoFactory.ImportFolder?.GetByID(ImportFolderID)?.ImportFolderName ?? ImportFolderID.ToString()
        }
    };

    public override async Task Process()
    {
        await _actionService.DeleteImportFolder(ImportFolderID);
    }

    public DeleteImportFolderJob(ActionService actionService)
    {
        _actionService = actionService;
    }

    protected DeleteImportFolderJob() { }
}
