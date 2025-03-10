using System.Collections.Generic;
using System.Threading.Tasks;
using Shoko.Server.Repositories;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Scheduling.Attributes;
using Shoko.Server.Services;

#pragma warning disable CS8618
namespace Shoko.Server.Scheduling.Jobs.Shoko;

[DatabaseRequired]
[JobKeyMember("DeleteManagedFolder")]
[JobKeyGroup(JobKeyGroup.Actions)]
internal class DeleteManagedFolderJob : BaseJob
{
    private readonly ActionService _actionService;

    private string _managedFolder;

    public int ManagedFolderID { get; set; }

    public override string TypeName => "Delete Managed Folder";

    public override string Title => "Deleting Managed Folder";

    public override void PostInit()
    {
        _managedFolder = RepoFactory.ShokoManagedFolder?.GetByID(ManagedFolderID)?.Name;
    }

    public override Dictionary<string, object> Details => new()
    {
        {
            "Managed Folder", _managedFolder ?? ManagedFolderID.ToString()
        }
    };

    public override async Task Process()
    {
        await _actionService.DeleteManagedFolder(ManagedFolderID);
    }

    public DeleteManagedFolderJob(ActionService actionService)
    {
        _actionService = actionService;
    }

    protected DeleteManagedFolderJob() { }
}
