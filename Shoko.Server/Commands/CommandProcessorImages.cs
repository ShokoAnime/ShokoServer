using System;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Server.Commands.Generic;
using Shoko.Server.Repositories;

namespace Shoko.Server.Commands;

public class CommandProcessorImages : CommandProcessor
{
    public override string QueueType { get; } = "Image";

    protected override void UpdatePause(bool pauseState)
    {
        ServerInfo.Instance.ImagesQueuePaused = pauseState;
        ServerInfo.Instance.ImagesQueueRunning = !pauseState;
    }

    public override void Init(IServiceProvider provider)
    {
        base.Init(provider);
        QueueState = new QueueStateStruct
        {
            message = "Starting image downloading command worker",
            queueState = QueueStateEnum.StartingImages,
            extraParams = new string[0]
        };
    }

    protected override Shoko.Models.Server.CommandRequest GetNextCommandRequest()
        => RepoFactory.CommandRequest.GetNextDBCommandRequestImages();
}
