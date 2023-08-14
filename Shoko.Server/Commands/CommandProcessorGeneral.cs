using System;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Plugin.Abstractions.Services;
using Shoko.Server.Commands.Generic;
using Shoko.Server.Models;
using Shoko.Server.Repositories;

namespace Shoko.Server.Commands;

public class CommandProcessorGeneral : CommandProcessor
{
    public override string QueueType { get; } = "General";

    private IConnectivityService ConnectivityService { get; set; }

    protected override void UpdatePause(bool pauseState)
    {
        ServerInfo.Instance.GeneralQueuePaused = pauseState;
        ServerInfo.Instance.GeneralQueueRunning = !pauseState;
    }

    public override void Init(IServiceProvider provider)
    {
        ConnectivityService = provider.GetRequiredService<IConnectivityService>();
        base.Init(provider);
        QueueState = new QueueStateStruct
        {
            message = "Starting general command worker",
            queueState = QueueStateEnum.StartingGeneral,
            extraParams = new string[0]
        };
    }

    protected override CommandRequest GetNextCommandRequest()
        => RepoFactory.CommandRequest.GetNextDBCommandRequestGeneral(ConnectivityService);
}
