using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Commons.Queue;

namespace Shoko.Server.CommandQueue.Commands
{
    public class FakeCommand : ICommand
    {
        public Task<CommandResult> RunAsync(IProgress<ICommandProgress> progress = null, CancellationToken token = default(CancellationToken))
        {
            throw new NotImplementedException();
        }

        public string Serialize()
        {
            throw new NotImplementedException();
        }

        public string ParallelTag { get; set; }
        public int ParallelMax { get; set; }
        public int Priority { get; set; }
        public string Id { get; set; } = string.Empty;
        public int Retries { get; set; }
        public string Batch { get; set; }
        public QueueStateStruct PrettyDescription { get; set; }
        public WorkTypes WorkType { get; set; }

        public static CommandProgress<FakeCommand> Create(QueueStateStruct st, WorkTypes wt)

        {
            CommandProgress<FakeCommand> cc=new CommandProgress<FakeCommand>();
            cc.Command=new FakeCommand();
            cc.Command.PrettyDescription = st;
            cc.Command.ParallelTag = wt.ToString();
            cc.Command.WorkType = wt;
            return cc;
        }
    }
}
