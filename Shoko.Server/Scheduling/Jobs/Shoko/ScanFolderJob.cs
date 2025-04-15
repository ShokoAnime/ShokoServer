using System.Collections.Generic;
using System.Threading.Tasks;
using Shoko.Plugin.Abstractions.Services;
using Shoko.Server.Repositories;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Scheduling.Attributes;

namespace Shoko.Server.Scheduling.Jobs.Shoko;

[DatabaseRequired]
[JobKeyMember("ScanFolder")]
[JobKeyGroup(JobKeyGroup.Actions)]
internal class ScanFolderJob : BaseJob
{
    private readonly IVideoService _videoService;

    private string _managedFolder;

    [JobKeyMember]
    public int ManagedFolderID { get; set; }

    public bool OnlyNewFiles { get; set; }

    public bool SkipMyList { get; set; }

    public override string TypeName => "Scan Managed Folder";

    public override string Title => "Scanning Managed Folder";

    public override Dictionary<string, object> Details
    {
        get
        {
            var details = new Dictionary<string, object>();
            if (!string.IsNullOrEmpty(_managedFolder))
                details["Managed Folder"] = _managedFolder;
            details["Managed Folder ID"] = ManagedFolderID;
            if (OnlyNewFiles) details["Only New Files"] = true;
            if (!SkipMyList) details["Add to MyList"] = true;
            return details;
        }
    }

    public override void PostInit()
    {
        _managedFolder = RepoFactory.ShokoManagedFolder?.GetByID(ManagedFolderID)?.Name;
    }

    public override async Task Process()
    {
        var managedFolder = _videoService.GetManagedFolderByID(ManagedFolderID);
        if (managedFolder == null)
            return;

        await _videoService.ScanManagedFolder(managedFolder, onlyNewFiles: OnlyNewFiles, skipMylist: SkipMyList);
    }

    public ScanFolderJob(IVideoService videoService)
    {
        _videoService = videoService;
    }

    protected ScanFolderJob() { }
}
