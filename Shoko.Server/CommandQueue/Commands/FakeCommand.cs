using System;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Commons.Queue;

namespace Shoko.Server.CommandQueue.Commands
{
    public class FakeCommand : ICommand
    {
        public Task RunAsync(IProgress<ICommand> progress = null, CancellationToken token = default(CancellationToken))
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
        public int MaxRetries { get; set; }
        public string Batch { get; set; }
        public QueueStateStruct PrettyDescription { get; set; }
        public WorkTypes WorkType { get; set; }
        public double Progress { get; } = 0;
        public CommandStatus Status { get; } = CommandStatus.Working;
        public string Error { get; } = null;

        public static FakeCommand Create(QueueStateStruct st, WorkTypes wt)

        {
            FakeCommand cc=new FakeCommand();
            cc.PrettyDescription = st;
            cc.ParallelTag = wt.ToString();
            cc.WorkType = wt;
            return cc;
        }
    }
}
