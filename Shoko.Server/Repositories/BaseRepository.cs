using System;
using System.Threading;
using Shoko.Server.Utilities;

namespace Shoko.Server.Repositories;

public class BaseRepository
{
    private static readonly ReaderWriterLockSlim s_lock = new(LockRecursionPolicy.SupportsRecursion);

    public static void Lock(Action action)
    {
        WriteLock(action);
    }

    public static void Lock<T>(T arg, Action<T> action)
    {
        WriteLock(() => action(arg));
    }

    public static void Lock<T, T1>(T arg, T1 arg2, Action<T, T1> action)
    {
        WriteLock(() => action(arg, arg2));
    }

    public static void Lock<T, T1, T2>(T arg, T1 arg2, T2 arg3, Action<T, T1, T2> action)
    {
        WriteLock(() => action(arg, arg2, arg3));
    }

    public static T Lock<T>(Func<T> action)
    {
        return WriteLock(action);
    }

    public static T Lock<T, T1>(T1 arg, Func<T1, T> action)
    {
        return WriteLock(() => action(arg));
    }

    public static T Lock<T, T1, T2>(T1 arg, T2 arg2, Func<T1, T2, T> action)
    {
        return WriteLock(() => action(arg, arg2));
    }

    public static T Lock<T, T1, T2, T3>(T1 arg, T2 arg2, T3 arg3, Func<T1, T2, T3, T> action)
    {
        return WriteLock(() => action(arg, arg2, arg3));
    }

    public static void ReadLock(Action action)
    {
        var useLock = Utils.SettingsProvider.GetSettings().Database.UseDatabaseLock;
        if (useLock)
        {
            s_lock.EnterReadLock();
            try
            {
                action.Invoke();
            }
            finally
            {
                s_lock.ExitReadLock();
            }
        }
        else
        {
            action();
        }
    }

    public static T ReadLock<T>(Func<T> action)
    {
        var useLock = Utils.SettingsProvider.GetSettings().Database.UseDatabaseLock;
        T result;
        if (useLock)
        {
            s_lock.EnterReadLock();
            try
            {
                result = action();
            }
            finally
            {
                s_lock.ExitReadLock();
            }
        }
        else
        {
            result = action();
        }

        return result;
    }

    public static void WriteLock(Action action)
    {
        var useLock = Utils.SettingsProvider.GetSettings().Database.UseDatabaseLock;
        if (useLock)
        {
            s_lock.EnterWriteLock();
            try
            {
                action.Invoke();
            }
            finally
            {
                s_lock.ExitWriteLock();
            }
        }
        else
        {
            action();
        }
    }

    public static T WriteLock<T>(Func<T> action)
    {
        var useLock = Utils.SettingsProvider.GetSettings().Database.UseDatabaseLock;
        T result;
        if (useLock)
        {
            s_lock.EnterWriteLock();
            try
            {
                result = action();
            }
            finally
            {
                s_lock.ExitWriteLock();
            }
        }
        else
        {
            result = action();
        }

        return result;
    }
}
