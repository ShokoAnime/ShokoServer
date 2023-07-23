using System;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Server.Commands.Generic;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Repositories;

namespace Shoko.Server.Commands;

public class CommandProcessorGeneral : CommandProcessor
{
    public override string QueueType { get; } = "General";

    protected override void UpdatePause(bool pauseState)
    {
        ServerInfo.Instance.GeneralQueuePaused = pauseState;
        ServerInfo.Instance.GeneralQueueRunning = !pauseState;
    }

    public override void Init(IServiceProvider provider)
    {
        base.Init(provider);
        QueueState = new QueueStateStruct
        {
            message = "Starting general command worker",
            queueState = QueueStateEnum.StartingGeneral,
            extraParams = new string[0]
        };
    }

    protected override Shoko.Models.Server.CommandRequest GetNextCommandRequest()
    {
        var udpHandler = ServiceProvider.GetRequiredService<IUDPConnectionHandler>();
        var httpHandler = ServiceProvider.GetRequiredService<IHttpConnectionHandler>();
        return RepoFactory.CommandRequest.GetNextDBCommandRequestGeneral(udpHandler, httpHandler);
    }
}
