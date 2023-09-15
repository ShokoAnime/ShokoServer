using Microsoft.Extensions.Logging;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Server.Commands.Attributes;
using Shoko.Server.Commands.Generic;
using Shoko.Server.Repositories;
using Shoko.Server.Server;

namespace Shoko.Server.Commands;

[Command(CommandRequestType.Refresh_GroupFilter)]
public class CommandRequest_RefreshGroupFilter : CommandRequestImplementation
{
    public virtual int GroupFilterID { get; set; }

    public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority9;

    public override QueueStateStruct PrettyDescription => new()
    {
        message = "Refreshing Group Filter: {0}",
        queueState = QueueStateEnum.RefreshGroupFilter,
        extraParams = new[] { GroupFilterID.ToString() }
    };

    protected override void Process()
    {
        if (GroupFilterID == 0)
        {
            RepoFactory.GroupFilter.CreateOrVerifyLockedFilters();
            RepoFactory.FilterPreset.CreateOrVerifyLockedFilters();
            return;
        }

        var gf = RepoFactory.GroupFilter.GetByID(GroupFilterID);
        if (gf == null)
        {
            return;
        }

        gf.CalculateGroupsAndSeries();
        RepoFactory.GroupFilter.Save(gf);
    }

    public override void GenerateCommandID()
    {
        CommandID = $"CommandRequest_RefreshGroupFilter_{GroupFilterID}";
    }

    protected override bool Load()
    {
        GroupFilterID = int.Parse(CommandDetails);
        return true;
    }


    protected override string GetCommandDetails()
    {
        return GroupFilterID.ToString();
    }

    public CommandRequest_RefreshGroupFilter(ILoggerFactory loggerFactory) : base(loggerFactory)
    {
    }

    protected CommandRequest_RefreshGroupFilter()
    {
    }
}
