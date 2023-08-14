using System;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Plugin.Abstractions.Services;
using Shoko.Server.Commands.Generic;
using Shoko.Server.Models;
using Shoko.Server.Repositories;

namespace Shoko.Server.Commands;

public class CommandProcessorImages : CommandProcessor
{
    public override string QueueType { get; } = "Image";

    private IConnectivityService ConnectivityService { get; set; }

    protected override void UpdatePause(bool pauseState)
    {
        ServerInfo.Instance.ImagesQueuePaused = pauseState;
        ServerInfo.Instance.ImagesQueueRunning = !pauseState;
    }

    public override void Init(IServiceProvider provider)
    {
        ConnectivityService = provider.GetRequiredService<IConnectivityService>();
        base.Init(provider);
        QueueState = new QueueStateStruct
        {
            message = "Starting image downloading command worker",
            queueState = QueueStateEnum.StartingImages,
            extraParams = new string[0]
        };
    }

    protected override CommandRequest GetNextCommandRequest()
        => RepoFactory.CommandRequest.GetNextDBCommandRequestImages(ConnectivityService);
}
