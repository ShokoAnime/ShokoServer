using System.Collections.Generic;
using System.Threading.Tasks;
using Shoko.Abstractions.Video.Services;
using Shoko.QueueProcessor.Acquisition.Attributes;
using Shoko.QueueProcessor.Builder;
using Shoko.QueueProcessor.Concurrency;

namespace Shoko.Server.Scheduling.Jobs.Shoko;

[DatabaseRequired]
[JobKeyMember("ScanFolder")]
[LongRunning]
[JobKeyGroup(JobKeyGroup.Import)]
internal class ScanFolderJob(IVideoService videoService) : BaseJob
{
    private string? _managedFolder;

    [JobKeyMember]
    public int ManagedFolderID { get; set; }

    [JobKeyMember]
    public string RelativePath { get; set; } = string.Empty;

    public bool OnlyNewFiles { get; set; }

    public bool SkipEvents { get; set; }

    public bool CleanUpStructure { get; set; }

    public bool CheckFileSize { get; set; }

    public bool ForceScan { get; set; }

    public override string TypeName => "Scan Managed Folder";

    public override string Title => "Scanning Managed Folder";

    public override Dictionary<string, object> Details
    {
        get
        {
            var details = new Dictionary<string, object>();
            if (!string.IsNullOrEmpty(_managedFolder))
                details["Managed Folder"] = _managedFolder;
            else
                details["Managed Folder ID"] = ManagedFolderID;
            if (!string.IsNullOrEmpty(RelativePath))
                details["Relative Path"] = RelativePath;
            if (OnlyNewFiles)
                details["Only New Files"] = true;
            if (!SkipEvents)
                details["Add to MyList"] = true;
            if (CleanUpStructure)
                details["Clean Up"] = true;
            if (CheckFileSize)
                details["Check File Size"] = true;
            if (ForceScan)
                details["Force Scan"] = true;
            return details;
        }
    }

    public override void PostInit()
    {
        _managedFolder = videoService.GetManagedFolderByID(ManagedFolderID)?.Name;
    }

    public override async Task Execute()
    {
        var managedFolder = videoService.GetManagedFolderByID(ManagedFolderID);
        if (managedFolder == null)
            return;

        await videoService.ScanManagedFolder(
            managedFolder,
            relativePath: RelativePath,
            onlyNewFiles: OnlyNewFiles,
            skipEvents: SkipEvents,
            cleanUpStructure: CleanUpStructure,
            checkFileSize: CheckFileSize,
            forceScan: ForceScan
        );
    }
}
