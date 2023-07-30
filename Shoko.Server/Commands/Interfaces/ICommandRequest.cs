using Shoko.Commons.Queue;
using Shoko.Models.Server;
using Shoko.Server.Server;

#nullable enable
namespace Shoko.Server.Commands.Interfaces;

public interface ICommandRequest
{
    void PostInit();
    void ProcessCommand();
    bool LoadFromDBCommand(CommandRequest cq);
    CommandRequestPriority DefaultPriority { get; }
    QueueStateStruct PrettyDescription { get; }
    CommandConflict ConflictBehavior { get; }
    ICommandProcessor? Processor { get; set; }
    void GenerateCommandID();
}
