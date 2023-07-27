using System;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Server.Commands.Generic;
using Shoko.Server.Repositories;

namespace Shoko.Server.Commands;

public class CommandProcessorHasher : CommandProcessor
{
    public override string QueueType { get; } = "Hasher";

    protected override void UpdatePause(bool pauseState)
    {
        ServerInfo.Instance.HasherQueuePaused = pauseState;
        ServerInfo.Instance.HasherQueueRunning = !pauseState;
    }

    public override void Init(IServiceProvider provider)
    {
        base.Init(provider);
        QueueState = new QueueStateStruct
        {
            message = "Starting hasher command worker",
            queueState = QueueStateEnum.StartingHasher,
            extraParams = new string[0]
        };
    }

    protected override Shoko.Models.Server.CommandRequest GetNextCommandRequest()
        => RepoFactory.CommandRequest.GetNextDBCommandRequestHasher();
}
