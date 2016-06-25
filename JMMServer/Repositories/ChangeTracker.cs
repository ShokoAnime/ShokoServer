using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TMDbLib.Objects.Movies;

namespace JMMServer.Repositories
{
    public class ChangeTracker<T>
    {
        private Dictionary<T,DateTime> _changes=new Dictionary<T, DateTime>();
        private Dictionary<T,DateTime> _removals=new Dictionary<T, DateTime>();
        private ReaderWriterLockSlim _lock=new ReaderWriterLockSlim();

        private void Lock()
        {
            _lock.EnterWriteLock();
        }

        private void Unlock()
        {
            _lock.ExitWriteLock();
        }
       
        public void AddOrUpdate(T item)
        {
            try
            {
                Lock();
                if (_removals.ContainsKey(item))
                    _removals.Remove(item);
                _changes[item] = DateTime.Now;
            }
            finally
            {
                Unlock();
            }
        }

        public void AddOrUpdateRange(IEnumerable<T> range)
        {
            try
            {
                Lock();
                DateTime dt = DateTime.Now;
                foreach (T item in range)
                {
                    if (_removals.ContainsKey(item))
                        _removals.Remove(item);
                    _changes[item] = dt;
                }
            }
            finally
            {
                Unlock();
            }

        }

        public void Remove(T item)
        {
            try
            {
                Lock();
                if (_changes.ContainsKey(item))
                {
                    _changes.Remove(item);
                    _removals[item] = DateTime.Now;
                }
            }
            finally
            {
                Unlock();
            }
        }

        private Changes<T> InternalGetChanges(DateTime date)
        {
            Changes<T> changes = new Changes<T>();
            changes.LastChange = DateTime.MinValue;
            if (_changes.Values.Count>0)
               changes.LastChange = _changes.Values.Max();
            if (_removals.Values.Count > 0)
            {
                DateTime remmax = _removals.Values.Max();
                if (remmax > changes.LastChange)
                    changes.LastChange = remmax;
            }
            changes.ChangedItems = new HashSet<T>(_changes.Where(a => a.Value > date).Select(a => a.Key));
            changes.RemovedItems = new HashSet<T>(_removals.Where(a => a.Value > date).Select(a => a.Key));
            return changes;
        }

        public Changes<T> GetChanges(DateTime date)
        {
            try
            {
                Lock();
                return InternalGetChanges(date);
            }
            finally
            {
                Unlock();
            }
        }

        public static List<Changes<T>> GetChainedChanges(List<ChangeTracker<T>> trackers, DateTime dt)
        {
            try
            {
                List<Changes<T>> list=new List<Changes<T>>();
                foreach (ChangeTracker<T> n in trackers)
                    n.Lock();
                foreach (ChangeTracker<T> n in trackers)
                    list.Add(n.InternalGetChanges(dt));
                return list;
            }
            finally
            {
                foreach (ChangeTracker<T> n in trackers)
                    n.Unlock();
            }
        }
    }

    public class Changes<T>
    {
        public HashSet<T> ChangedItems { get; set; }=new HashSet<T>();
        public HashSet<T> RemovedItems { get; set; }=new HashSet<T>();
        public DateTime LastChange { get; set; }
    }
}
