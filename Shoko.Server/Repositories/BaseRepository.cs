using System;
using Shoko.Server.Utilities;

namespace Shoko.Server.Repositories;

public class BaseRepository
{
    private static readonly object s_lock = new();

    public static void Lock(Action action)
    {
        var useLock = Utils.SettingsProvider.GetSettings().Database.UseDatabaseLock;
        if (useLock)
        {
            lock (s_lock)
            {
                action.Invoke();
            }
        }
        else
        {
            action();
        }
    }

    public static void Lock<T>(T arg, Action<T> action)
    {
        var useLock = Utils.SettingsProvider.GetSettings().Database.UseDatabaseLock;
        if (useLock)
        {
            lock (s_lock)
            {
                action.Invoke(arg);
            }
        }
        else
        {
            action(arg);
        }
    }

    public static void Lock<T, T1>(T arg, T1 arg2, Action<T, T1> action)
    {
        var useLock = Utils.SettingsProvider.GetSettings().Database.UseDatabaseLock;
        if (useLock)
        {
            lock (s_lock)
            {
                action.Invoke(arg, arg2);
            }
        }
        else
        {
            action(arg, arg2);
        }
    }

    public static void Lock<T, T1, T2>(T arg, T1 arg2, T2 arg3, Action<T, T1, T2> action)
    {
        var useLock = Utils.SettingsProvider.GetSettings().Database.UseDatabaseLock;
        if (useLock)
        {
            lock (s_lock)
            {
                action.Invoke(arg, arg2, arg3);
            }
        }
        else
        {
            action(arg, arg2, arg3);
        }
    }

    public static T Lock<T>(Func<T> action)
    {
        var useLock = Utils.SettingsProvider.GetSettings().Database.UseDatabaseLock;
        T result;
        if (useLock)
        {
            lock (s_lock)
            {
                result = action();
            }
        }
        else
        {
            result = action();
        }

        return result;
    }

    public static T Lock<T, T1>(T1 arg, Func<T1, T> action)
    {
        var useLock = Utils.SettingsProvider.GetSettings().Database.UseDatabaseLock;
        T result;
        if (useLock)
        {
            lock (s_lock)
            {
                result = action(arg);
            }
        }
        else
        {
            result = action(arg);
        }

        return result;
    }

    public static T Lock<T, T1, T2>(T1 arg, T2 arg2, Func<T1, T2, T> action)
    {
        var useLock = Utils.SettingsProvider.GetSettings().Database.UseDatabaseLock;
        T result;
        if (useLock)
        {
            lock (s_lock)
            {
                result = action(arg, arg2);
            }
        }
        else
        {
            result = action(arg, arg2);
        }

        return result;
    }

    public static T Lock<T, T1, T2, T3>(T1 arg, T2 arg2, T3 arg3, Func<T1, T2, T3, T> action)
    {
        var useLock = Utils.SettingsProvider.GetSettings().Database.UseDatabaseLock;
        T result;
        if (useLock)
        {
            lock (s_lock)
            {
                result = action(arg, arg2, arg3);
            }
        }
        else
        {
            result = action(arg, arg2, arg3);
        }

        return result;
    }
}
