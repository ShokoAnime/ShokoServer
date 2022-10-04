using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Shoko.Server.Repositories;

public class ChangeTracker<T>
{
    private Dictionary<T, DateTime> _changes = new();
    private Dictionary<T, DateTime> _removals = new();
    private ReaderWriterLockSlim _lock = new();

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
            var dt = DateTime.Now;
            foreach (var item in range)
            {
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
            if (_changes.Remove(item))
            {
                _removals[item] = DateTime.Now;
            }
        }
        finally
        {
            Unlock();
        }
    }

    public void RemoveRange(IEnumerable<T> range)
    {
        try
        {
            Lock();
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
            Unlock();
        }
    }

    private Changes<T> InternalGetChanges(DateTime date)
    {
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
            var list = new List<Changes<T>>();
            foreach (var n in trackers)
            {
                n.Lock();
            }

            foreach (var n in trackers)
            {
                list.Add(n.InternalGetChanges(dt));
            }

            return list;
        }
        finally
        {
            foreach (var n in trackers)
            {
                n.Unlock();
            }
        }
    }
}

public class Changes<T>
{
    public HashSet<T> ChangedItems { get; set; } = new();
    public HashSet<T> RemovedItems { get; set; } = new();
    public DateTime LastChange { get; set; }
}
