using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Quartz;
using Shoko.Server.Models;
using Shoko.Server.Repositories;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Scheduling.Attributes;

namespace Shoko.Server.Scheduling.Jobs.Shoko;

[DatabaseRequired]
[JobKeyGroup(JobKeyGroup.Actions)]
public class ProcessFileMovedMessageJob : BaseJob
{
    private readonly ISchedulerFactory _schedulerFactory;
    public override string TypeName => "Handle Moved File Message";
    public override string Title => "Handling Moved File Message";
    public AniDB_Message Message { get; set; }

    public override async Task Process()
    {
        _logger.LogInformation("Processing {Job}: {MessageId}", nameof(ProcessFileMovedMessageJob), Message.MessageID);
        // title should be in the format "file moved: <fileID>"
        if (!int.TryParse(Message.Title[12..].Trim(), out var fileId))
        {
            throw new Exception("Could not parse file ID from message title");
        }

        var file = RepoFactory.AniDB_File.GetByFileID(fileId);
        if (file == null)
        {
            _logger.LogWarning("Could not find file with AniDB ID: {ID}", fileId);
            return;
        }

        var vlocal = RepoFactory.VideoLocal.GetByHash(file.Hash);
        if (vlocal == null)
        {
            _logger.LogWarning("Could not find VideoLocal for file with AniDB ID and Hash: {ID} {Hash}", fileId, file.Hash);
            return;
        }

        var scheduler = await _schedulerFactory.GetScheduler();
        await scheduler.StartJob<ProcessFileJob>(
            c =>
            {
                c.VideoLocalID = vlocal.VideoLocalID;
                c.ForceAniDB = true;
            }
        ).ContinueWith(t =>
        {
            if (!t.IsFaulted)
            {
                Message.IsFileMoveHandled = true;
                RepoFactory.AniDB_Message.Save(Message);
            }
        });
    }

    public ProcessFileMovedMessageJob(ISchedulerFactory schedulerFactory)
    {
        _schedulerFactory = schedulerFactory;
    }

    protected ProcessFileMovedMessageJob()
    {
    }
}
