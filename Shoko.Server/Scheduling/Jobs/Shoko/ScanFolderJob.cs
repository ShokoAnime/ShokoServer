using System.Collections.Generic;
using System.Threading.Tasks;
using Shoko.Server.Repositories;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Scheduling.Attributes;
using Shoko.Server.Services;

namespace Shoko.Server.Scheduling.Jobs.Shoko;

[DatabaseRequired]
[JobKeyMember("ScanFolder")]
[JobKeyGroup(JobKeyGroup.Actions)]
internal class ScanFolderJob : BaseJob
{
    private readonly ActionService _actionService;

    private string _managedFolder;

    [JobKeyMember]
    public int ManagedFolderID { get; set; }

    public override string TypeName => "Scan Managed Folder";

    public override string Title => "Scanning Managed Folder";

    public override Dictionary<string, object> Details => new() { { "Managed Folder", _managedFolder ?? ManagedFolderID.ToString() } };

    public override void PostInit()
    {
        _managedFolder = RepoFactory.ShokoManagedFolder?.GetByID(ManagedFolderID)?.Name;
    }

    public override async Task Process()
    {
        await _actionService.RunImport_ScanFolder(ManagedFolderID);
    }

    public ScanFolderJob(ActionService actionService)
    {
        _actionService = actionService;
    }

    protected ScanFolderJob() { }
}
