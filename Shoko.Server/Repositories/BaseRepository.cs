using System;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Server.Utilities;

namespace Shoko.Server.Repositories;

public class BaseRepository
{
    private static readonly SemaphoreSlim s_globalDBLock = new(1, 1);

    public static void Lock(Action action)
    {
        var useLock = Utils.SettingsProvider.GetSettings().Database.UseDatabaseLock;
        if (useLock)
        {
            s_globalDBLock.Wait();
            action.Invoke();
            s_globalDBLock.Release();
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
            s_globalDBLock.Wait();
            action.Invoke(arg);
            s_globalDBLock.Release();
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
            s_globalDBLock.Wait();
            action.Invoke(arg, arg2);
            s_globalDBLock.Release();
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
            s_globalDBLock.Wait();
            action.Invoke(arg, arg2, arg3);
            s_globalDBLock.Release();
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
            s_globalDBLock.WaitAsync();
            result = action();
            s_globalDBLock.Release();
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
            s_globalDBLock.WaitAsync();
            result = action(arg);
            s_globalDBLock.Release();
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
            s_globalDBLock.WaitAsync();
            result = action(arg, arg2);
            s_globalDBLock.Release();
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
            s_globalDBLock.WaitAsync();
            result = action(arg, arg2, arg3);
            s_globalDBLock.Release();
        }
        else
        {
            result = action(arg, arg2, arg3);
        }

        return result;
    }

    public static async Task<T> LockAsync<T>(Func<Task<T>> action)
    {
        var useLock = Utils.SettingsProvider.GetSettings().Database.UseDatabaseLock;
        T result;
        if (useLock)
        {
            await s_globalDBLock.WaitAsync();
            result = await action();
            s_globalDBLock.Release();
        }
        else
        {
            result = await action();
        }

        return result;
    }
}
