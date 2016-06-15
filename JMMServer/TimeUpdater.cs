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
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public class DateJoinU
        {
            public DateTime TimeStamp;
            public U Parameters;
        }

        private Dictionary<T, DateJoinU> updates = new Dictionary<T, DateJoinU>();
        private string name;
        private Action<T, U> action;
        private Func<U, U, U> updaction;
        private int timeout;
        private object stateLock = new object();
        private Timer timer;

        public TimeUpdater(int seconds, string name, Action<T, U> exec_routine, Func<U, U, U> update_routine = null)
        {
            this.name = name;
            action = exec_routine;
            updaction = update_routine;
            timeout = seconds;
            timer = new Timer(UpdateStateWorker, null, seconds*1000, seconds*1000);
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
                    DateJoinU dju = new DateJoinU();
                    dju.Parameters = u;
                    updates.Add(t, dju);
                }
                updates[t].TimeStamp = DateTime.Now.AddSeconds(timeout);
            }
        }

        private void UpdateStateWorker(object o)
        {
            DateTime dt = DateTime.Now;
            List<DateJoinU> par_updates = new List<DateJoinU>();
            lock (stateLock)
            {
                par_updates = updates.Values.Where(a => a.TimeStamp < dt).ToList();
            }
            logger.Trace("TimeUpdater " + name + " State Worker Count: {0} ", par_updates.Count);
            if (par_updates.Count > 0)
            {
                Dictionary<T, DateJoinU> procdic = new Dictionary<T, DateJoinU>();
                lock (stateLock)
                {
                    foreach (T ser in updates.Keys.ToList())
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
                    foreach (T ser in procdic.Keys)
                    {
                        DateJoinU p = procdic[ser];
                        action(ser, p.Parameters);
                    }
                    timer.Change(timeout*1000, timeout*1000);
                });
            }
        }
    }
}