using System;
using System.Collections.Generic;

namespace Shoko.Server.CommandQueue.Commands
{
    public interface ICommandProvider
    {
        List<ICommand> Get(int qnty, Dictionary<string,int> tagLimits, List<string> batchLimits, List<string> workLimits, List<string> preconditionLimits);
        void Put(ICommand cmd, string batch = "Server", int secondsInFuture = 0, string error = null, int retries = 0);
        void PutRange(IEnumerable<ICommand> cmds, string batch = "Server", int secondsInFuture = 0);
        void ClearBatch(string batch);
        void ClearWorkTypes(params string[] worktypes);
        void Clear();
        int GetQueuedCommandCount(params string[] wt);
        int GetQueuedCommandCount();

        int GetQueuedCommandCount(string batch);
    }
}
