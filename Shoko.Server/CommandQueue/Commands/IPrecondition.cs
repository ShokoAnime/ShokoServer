using Newtonsoft.Json;

namespace Shoko.Server.CommandQueue.Commands
{
    public interface IPrecondition
    {
        bool CanExecute();

        [JsonIgnore]
        int PreconditionRetryFutureInSeconds { get; }
    }
}