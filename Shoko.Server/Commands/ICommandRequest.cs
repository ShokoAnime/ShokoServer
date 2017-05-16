using Shoko.Commons.Queue;
using Shoko.Models.Server;

namespace Shoko.Server.Commands
{
    public interface ICommandRequest
    {
        void ProcessCommand();
        bool LoadFromDBCommand(CommandRequest cq);
        CommandRequestPriority DefaultPriority { get; }
        QueueStateStruct PrettyDescription { get; }
    }
}