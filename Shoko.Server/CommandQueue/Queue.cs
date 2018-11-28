using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Nito.AsyncEx;
using NLog;
using Shoko.Commons.Extensions;
using Shoko.Server.CommandQueue.Commands;
using Shoko.Server.Repositories;
using Shoko.Server.Utilities;
using TMDbLib.Objects.Lists;

namespace Shoko.Server.CommandQueue
{
    public class Queue : ObservableProgress<ICommand>
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();
        public static string[] GeneralWorkTypesExceptSchedule => new[] { WorkTypes.MovieDB, WorkTypes.Hashing, WorkTypes.Plex, WorkTypes.Server, WorkTypes.Trakt, WorkTypes.TvDB, WorkTypes.WebCache };
        public static string[] GeneralWorkTypes => new[] { WorkTypes.Schedule, WorkTypes.MovieDB, WorkTypes.Hashing, WorkTypes.Plex, WorkTypes.Server, WorkTypes.Trakt, WorkTypes.TvDB, WorkTypes.WebCache };
        public static string[] AllWorkTypes => new[] { WorkTypes.Image,WorkTypes.AniDB, WorkTypes.Schedule, WorkTypes.MovieDB, WorkTypes.Hashing, WorkTypes.Plex, WorkTypes.Server, WorkTypes.Trakt, WorkTypes.TvDB, WorkTypes.WebCache };

        private readonly AsyncLock _lock = new AsyncLock();
        private CancellationTokenSource _src;
        private readonly List<string> PausedBatches = new List<string>();
        private readonly List<string> PausedTags = new List<string>();
        private readonly List<string> PausedWorkTypes = new List<string>();
        private readonly List<ICommand> workingtasks = new List<ICommand>();

        private static readonly Lazy<Queue> _instance = new Lazy<Queue>(() => new Queue());
        public static Queue Instance => _instance.Value;

        public int MaxThreads { get; set; } = 8;
        public int DefaultCheckDelayInMilliseconds { get; set; } = 1000;
        public int NoWorkDelayInMilliseconds { get; set; } = 5000;

        public int BatchSize { get; set; } = 20;

        public ICommandProvider Provider { get; set; }= Repo.Instance.CommandRequest;

        private Dictionary<IPrecondition, PreConditionState> _genericPreconditions = new Dictionary<IPrecondition, PreConditionState>();

        public bool Running { get; private set; }

        internal class PreConditionState
        {
            public string Name { get; set; }
            public bool CanExecute { get; set; }
            public DateTime CheckTime { get; set; }
        }
        public Queue()
        {
            foreach (Type t in Resolver.Instance.GenericPreconditionToString.Keys)
            {
                ConstructorInfo ctor = t.GetConstructor(new Type[0]);
                if (ctor == null)
                    continue;
                IPrecondition prec = (IPrecondition) ctor.Invoke(new object[0]);
                if (prec==null)
                    continue;
                _genericPreconditions.Add(prec, new PreConditionState{Name=Resolver.Instance.GenericPreconditionToString[t], CanExecute = true, CheckTime = DateTime.Now});
            }
        }
        public void Start()
        {
            if (Running)
                return;
            Stop();
            _src = new CancellationTokenSource();
            CancellationToken token = _src.Token;
            Running = true;
            Task.Run(() => QueueWorker(token), token);
        }

        public bool IsTagPaused(string tag)
        {
            using (_lock.Lock())
            {
                return PausedTags.Contains(tag);
            }
        }

        public int GetCommandCount()
        {
            using (_lock.Lock())
            {
                if (PausedWorkTypes.Count > 0)
                    return GetCommandCountInternal(AllWorkTypes);
                return Provider.GetQueuedCommandCount()+workingtasks.Count;
            }

        }

        private int GetCommandCountInternal(string[] worktypes)
        {

            List<string> wk = worktypes.ToList();
            foreach (string w in worktypes)
            {
                if (PausedWorkTypes.Contains(w))
                    wk.Remove(w);
            }
            return Provider.GetQueuedCommandCount(wk.ToArray());
        }
        public int GetCommandCount(params string[] worktypes)
        {
            using (_lock.Lock())
            {
                return GetCommandCountInternal(worktypes)+workingtasks.Count(a=>worktypes.Contains(a.WorkType));
            }
        }
        public int GetCommandCount(string batch)
        {
            using (_lock.Lock())
            {
                return Provider.GetQueuedCommandCount(batch)+ workingtasks.Count(a => a.Batch == batch);
            }
        }
        public bool AreWorkTypesPaused(params string[] worktypes)
        {
            using (_lock.Lock())
            {
                foreach (string w in worktypes)
                {
                    if (!PausedWorkTypes.Contains(w))
                        return false;
                }

                return true;
            }
        }

        public bool IsBatchPaused(string batch)
        {
            using (_lock.Lock())
            {
                return PausedBatches.Contains(batch);
            }
        }

        public void PauseTag(string tag)
        {
            using (_lock.Lock())
            {
                if (!PausedTags.Contains(tag))
                    PausedTags.Add(tag);
            }
        }

        public void ResumeTag(string tag)
        {
            using (_lock.Lock())
            {
                if (PausedTags.Contains(tag))
                    PausedTags.Remove(tag);
            }
        }

        public void PauseWorkTypes(params string[] types)
        {
            using (_lock.Lock())
            {
                foreach (string w in types)
                {
                    if (!PausedWorkTypes.Contains(w))
                        PausedWorkTypes.Add(w);
                }
            }
        }

        public void ResumeWorkTypes(params string[] types)
        {
            using (_lock.Lock())
            {
                foreach (string w in types)
                {
                    if (PausedWorkTypes.Contains(w))
                        PausedWorkTypes.Remove(w);
                }
            }
        }

        public void PauseBatch(string batch)
        {
            using (_lock.Lock())
            {
                if (!PausedBatches.Contains(batch))
                    PausedBatches.Add(batch);
            }
        }

        public void ResumeBatch(string batch)
        {
            using (_lock.Lock())
            {
                if (PausedBatches.Contains(batch))
                    PausedBatches.Remove(batch);
            }
        }

        public void ClearBatch(string batch)
        {
            using (_lock.Lock())
            {
                Provider.ClearBatch(batch);
            }
        }
        public void ClearWorkTypes(params string[] wk)
        {
            using (_lock.Lock())
            {
                Provider.ClearWorkTypes(wk);
            }
        }
        public void Clear()
        {
            using (_lock.Lock())
            {
                Provider.Clear();
            }
        }
        private async Task QueueWorker(CancellationToken token)
        {
            do
            {
                Dictionary<string, int> usedtags = new Dictionary<string, int>();
                List<string> batchlimits;
                List<string> workLimits;

                int count;
                //Execute Generic preconditions
                foreach (IPrecondition p in _genericPreconditions.Keys)
                {
                    PreConditionState state = _genericPreconditions[p];
                    if (state.CheckTime < DateTime.Now)
                    {
                        try
                        {
                            var res = p.CanExecute();
                            state.CheckTime = DateTime.Now.Add(res.RetryIn);
                            state.CanExecute = res.CanRun;
                        }
                        catch (Exception e)
                        {
                            //Ignore, log and recheck again
                            logger.Error(e,$"Error in Queue: Generic IPrecondition {p.GetType().Name} failed on CanExecute Error: {e.Message}");
                        }
                    }
                }
                using (await _lock.LockAsync(token))
                {
                    count = MaxThreads - workingtasks.Count;
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

                    foreach (string s in PausedTags)
                        usedtags[s] = 0;
                    batchlimits = PausedBatches.ToList();
                    workLimits = PausedWorkTypes.ToList();
                }

                bool nowork = false;
                if (count > 0)
                {
                    List<ICommand> cmds = Provider.Get(count, usedtags, batchlimits, workLimits, _genericPreconditions.Values.Where(a => !a.CanExecute).Select(a => a.Name).ToList());
                    nowork = cmds.Count == 0;
                    foreach (ICommand c in cmds)
                    {
                        if (c is IPrecondition d)
                        {
                            try
                            {
                                try
                                {
                                    var res = d.CanExecute();
                                    if (!res.CanRun)
                                    {
                                        if (res.RetryIn != null && res.RetryIn.Milliseconds > 0)
                                            Provider.Put(c, c.Batch, (int)res.RetryIn.TotalSeconds);
                                        continue;
                                    }
                                }
                                catch (Exception e)
                                {
                                    //Ignore, asume execution and log
                                    logger.Error(e, $"Error in Queue: Command IPrecondition {c.GetType().Name} failed on CanExecute Error: {e.Message}");
                                }

                            }
                            catch (Exception e)
                            {

                            }
                        }
                        Task t = new Task(async () =>
                        {
                            try
                            {
                                await c.RunAsync(this, token);
                                if (c.Status == CommandStatus.Error && c.Retries < c.MaxRetries)
                                {
                                    c.Retries++;
                                    Provider.Put(c, c.Batch, c.RetryFutureInSeconds, c.Error, c.Retries);
                                }
                            }
                            catch (Exception e)
                            {
                                logger.Error(e, $"Error Executing Command: {c.GetType().Name} Retries: {c.Retries} failed on with Error: {e.Message}");
                                if (c.Retries < c.MaxRetries)
                                {
                                    c.Retries++;
                                    Provider.Put(c, c.Batch, c.RetryFutureInSeconds, e.Message, c.Retries);

                                }
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

                await Task.Delay(nowork ? NoWorkDelayInMilliseconds : DefaultCheckDelayInMilliseconds, token);
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

        public void Add(ICommand command, string batch = "Server", int secondsinfuture = 0)
        {
            Provider.Put(command, batch,secondsinfuture);
        }
        public void Add(ICommand command, DateTime future, string batch = "Server")
        {
            Provider.Put(command, batch, (int)(future-DateTime.UtcNow).TotalSeconds);
        }
        public void AddRange(IEnumerable<ICommand> command, string batch = "Server", int secondsinfuture = 0)
        {
            command.Batch(BatchSize).ForEach(a => Provider.PutRange(a, batch,secondsinfuture));
        }
    }
}