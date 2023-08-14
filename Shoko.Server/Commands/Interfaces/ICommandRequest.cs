#nullable enable
using Shoko.Commons.Queue;
using Shoko.Server.Server;

namespace Shoko.Server.Commands.Interfaces;

public interface ICommandRequest
{
    void PostInit();
    void ProcessCommand();
    bool LoadFromCommandDetails();
    CommandRequestPriority DefaultPriority { get; }
    QueueStateStruct PrettyDescription { get; }
    CommandConflict ConflictBehavior { get; }
    ICommandProcessor? Processor { get; set; }
    void GenerateCommandID();
}
