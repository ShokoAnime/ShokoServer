using System;
using System.Collections.Generic;

namespace Shoko.Server.CommandQueue.Commands
{
    public interface ICommandProvider
    {
        List<ICommand> Get(int qnty, Dictionary<string,int> tagLimits, List<string> batchLimits, List<WorkTypes> workLimits);
        void Put(ICommand cmd, string batch = "Server", int secondsInFuture = 0, string error = null, int retries = 0);
        void PutRange(IEnumerable<ICommand> cmds, string batch = "Server", int secondsInFuture = 0);
        void ClearBatch(string batch);
        void ClearWorkTypes(params WorkTypes[] worktypes);
        void Clear();
        int GetQueuedCommandCount(params WorkTypes[] wt);
        int GetQueuedCommandCount();

        int GetQueuedCommandCount(string batch);
    }
}
