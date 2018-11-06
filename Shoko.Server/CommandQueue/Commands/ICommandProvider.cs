using System.Collections.Generic;

namespace Shoko.Server.CommandQueue.Commands
{
    public interface ICommandProvider
    {
        List<ICommand> Get(int qnty, Dictionary<string,int> tagLimits);
        void Put(ICommand cmd, string batch = "Server", int secondsInFuture = 0, string error = null, int retries = 0);
        void PutRange(IEnumerable<ICommand> cmds, string batch = "Server", int secondsInFuture = 0);

    }
}
