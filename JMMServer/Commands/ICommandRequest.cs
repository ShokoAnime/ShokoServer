using JMMServer.Entities;

namespace JMMServer.Commands
{
    public interface ICommandRequest
    {
        CommandRequestPriority DefaultPriority { get; }
        string PrettyDescription { get; }
        void ProcessCommand();
        bool LoadFromDBCommand(CommandRequest cq);
    }
}