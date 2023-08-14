using Shoko.Commons.Queue;

namespace Shoko.Server.Commands.Interfaces;

public interface ICommandProcessor
{
    int QueueCount { get; }
    QueueStateStruct QueueState { get; set; }
}
