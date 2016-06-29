using JMMServer.Entities;

namespace JMMServer.Commands
{
    public interface ICommandRequest
    {
        void ProcessCommand();
        bool LoadFromDBCommand(CommandRequest cq);
        CommandRequestPriority DefaultPriority { get; }
        QueueStateStruct PrettyDescription { get; }
    }
}