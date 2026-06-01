using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Shoko.Server.Repositories;

public class ChangeTracker<T>
{
    private readonly Dictionary<T, DateTime> _changes = new();
    private readonly Dictionary<T, DateTime> _removals = new();
    private readonly ReaderWriterLockSlim _lock = new();

    public void AddOrUpdate(T item)
    {
        try
        {
            _lock.EnterWriteLock();
            _removals.Remove(item);
            _changes[item] = DateTime.Now;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public void AddOrUpdateRange(IEnumerable<T> range)
    {
        try
        {
            _lock.EnterWriteLock();
            var dt = DateTime.Now;
            foreach (var item in range)
            {
                _removals.Remove(item);
                _changes[item] = dt;
            }
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public void Remove(T item)
    {
        try
        {
            _lock.EnterWriteLock();
            if (_changes.Remove(item))
            {
                _removals[item] = DateTime.Now;
            }
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public void RemoveRange(IEnumerable<T> range)
    {
        try
        {
            _lock.EnterWriteLock();
            var dt = DateTime.Now;

            foreach (var item in range)
            {
                if (_changes.Remove(item))
                {
                    _removals[item] = dt;
                }
            }
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    private Changes<T> GetChanges(DateTime date)
    {
        try
        {
            _lock.EnterReadLock();
            var changes = new Changes<T> { LastChange = DateTime.MinValue };
            if (_changes.Values.Count > 0)
            {
                changes.LastChange = _changes.Values.Max();
            }

            if (_removals.Values.Count > 0)
            {
                var remmax = _removals.Values.Max();
                if (remmax > changes.LastChange)
                {
                    changes.LastChange = remmax;
                }
            }

            changes.ChangedItems = new HashSet<T>(_changes.Where(a => a.Value.ToUniversalTime() > date.ToUniversalTime())
                .Select(a => a.Key));
            changes.RemovedItems = new HashSet<T>(_removals.Where(a => a.Value.ToUniversalTime() > date.ToUniversalTime())
                .Select(a => a.Key));
            return changes;
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public static List<Changes<T>> GetChainedChanges(List<ChangeTracker<T>> trackers, DateTime dt)
    {
        var list = new List<Changes<T>>();

        foreach (var n in trackers)
        {
            try
            {
                n._lock.EnterReadLock();
                list.Add(n.GetChanges(dt));
            }
            finally
            {
                n._lock.ExitReadLock();
            }
        }

        return list;
    }
}

public class Changes<T>
{
    public HashSet<T> ChangedItems { get; set; } = new();
    public HashSet<T> RemovedItems { get; set; } = new();
    public DateTime LastChange { get; set; }
}
