using NLog;

namespace Shoko.Server.CommandQueue.Commands
{
    public class CommandResult
    {
        public CommandResultStatus Status { get; }
        public string Error { get; }

        public CommandResult()
        {
            Status = CommandResultStatus.Ok;
            Error = null;
        }

        public CommandResult(CommandResultStatus status, string error)
        {
            Status = status;
            Error = error;
        }
        public CommandResult(CommandResultStatus status, Logger logger, string error)
        {
            Status = status;
            Error = error;
            logger.Error(Error);
        }
    }
  
}