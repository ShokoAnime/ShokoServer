using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nito.AsyncEx;
using Shoko.Commons.Extensions;
using Shoko.Server.CommandQueue.Commands;
using Shoko.Server.Utilities;

namespace Shoko.Server.CommandQueue
{
    public class Queue : ObservableProgress<ICommand>
    {
        private static Queue _instance;
        public static Queue Instance => _instance ?? (_instance = new Queue());
        public int MaxThreads { get; set; } = 8;
        public int DefaultCheckDelayInMilliseconds { get; set; } = 1000;
        public int NoWorkDelayInMilliseconds { get; set; } = 5000;
        public int RetryFutureSeconds { get; set; } = 60;

        public int BatchSize { get; set; } = 20;

        public ICommandProvider Provider => Repositories.Repo.Instance.CommandRequest;

        private CancellationTokenSource _src;

        public bool Running { get; private set; }

        public void Start()
        {
            if (Running)
                return;
            Stop();
            _src=new CancellationTokenSource();
            CancellationToken token = _src.Token;
            Running = true;
            Task.Run(() => QueueWorker(token), token);
        }

        private AsyncLock _lock=new AsyncLock();
        private List<ICommand> workingtasks=new List<ICommand>();

        private async Task QueueWorker(CancellationToken token)
        {
            do
            {
                Dictionary<string, int> usedtags=new Dictionary<string, int>();
                int count;
                using (await _lock.LockAsync(token))
                {
                    count = MaxThreads-workingtasks.Count;
                    if (count > 0)
                    {
                        foreach (string s in workingtasks.Select(a => a.ParallelTag).Distinct())
                        {
                            int max = workingtasks.First(a => a.ParallelTag == s).ParallelMax;
                            int cr = workingtasks.Count(a => a.ParallelTag == s);
                            int qty = max - cr;
                            if (qty < 0)
                                qty = 0;
                            usedtags.Add(s, qty);
                        }
                    }
                }
                bool nowork = false;
                if (count > 0)
                {
                    List<ICommand> cmds=Provider.Get(count, usedtags);
                    nowork = cmds.Count == 0;
                    foreach (ICommand c in cmds)
                    {
                        Task t = new Task(async () =>
                        {
                            await c.RunAsync(this, token);
                            if (c.Status == CommandStatus.Error && c.Retries < c.MaxRetries)
                            {
                                c.Retries++;
                                Provider.Put(c, c.Batch, RetryFutureSeconds, c.Error, c.Retries);
                            }
                            using (await _lock.LockAsync(token))
                            {
                                workingtasks.Remove(c);
                            }
                        }, token);
                        using (await _lock.LockAsync(token))
                        {
                            workingtasks.Add(c);
                        }
                        t.Start();
                    }
                }
                await Task.Delay(nowork ? NoWorkDelayInMilliseconds : DefaultCheckDelayInMilliseconds,token);
            } while (!token.IsCancellationRequested);
        }

        public void Stop()
        {
            if (!Running)
                return;
            _src?.Cancel();
            _src = null;
            while (workingtasks.Count > 0)
            {
                Thread.Sleep(1000);
            }

            Running = false;

        }

        public void Add(ICommand command, string batch="Server")
        {
            Provider.Put(command,batch);
        }

        public void AddRange(IEnumerable<ICommand> command, string batch = "Server")
        {
            command.Batch(BatchSize).ForEach(a=>Provider.PutRange(a,batch));
        }
    }
}
