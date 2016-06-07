using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NLog;

namespace JMMServer
{
    public class TimeUpdater<T, U>
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        private readonly Action<T, U> action;
        private readonly string name;
        private readonly object stateLock = new object();
        private readonly int timeout;
        private readonly Timer timer;
        private readonly Func<U, U, U> updaction;

        private readonly Dictionary<T, DateJoinU> updates = new Dictionary<T, DateJoinU>();

        public TimeUpdater(int seconds, string name, Action<T, U> exec_routine, Func<U, U, U> update_routine = null)
        {
            this.name = name;
            action = exec_routine;
            updaction = update_routine;
            timeout = seconds;
            timer = new Timer(UpdateStateWorker, null, seconds * 1000, seconds * 1000);
        }

        public void Update(T t, U u)
        {
            lock (stateLock)
            {
                if (updates.Keys.Contains(t))
                {
                    updates[t].Parameters = updaction != null ? updaction(updates[t].Parameters, u) : u;
                }
                else
                {
                    var dju = new DateJoinU();
                    dju.Parameters = u;
                    updates.Add(t, dju);
                }
                updates[t].TimeStamp = DateTime.Now.AddSeconds(timeout);
            }
        }

        private void UpdateStateWorker(object o)
        {
            var dt = DateTime.Now;
            var par_updates = new List<DateJoinU>();
            lock (stateLock)
            {
                par_updates = updates.Values.Where(a => a.TimeStamp < dt).ToList();
            }
            logger.Trace("TimeUpdater " + name + " State Worker Count: {0} ", par_updates.Count);
            if (par_updates.Count > 0)
            {
                var procdic = new Dictionary<T, DateJoinU>();
                lock (stateLock)
                {
                    foreach (var ser in updates.Keys.ToList())
                    {
                        if (par_updates.Contains(updates[ser]))
                        {
                            procdic.Add(ser, updates[ser]);
                            updates.Remove(ser);
                        }
                    }
                }
                timer.Change(Timeout.Infinite, Timeout.Infinite);
                Task.Run(() =>
                {
                    foreach (var ser in procdic.Keys)
                    {
                        var p = procdic[ser];
                        action(ser, p.Parameters);
                    }
                    timer.Change(timeout * 1000, timeout * 1000);
                });
            }
        }

        public class DateJoinU
        {
            public U Parameters;
            public DateTime TimeStamp;
        }
    }
}