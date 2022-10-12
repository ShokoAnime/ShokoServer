﻿using System;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using Shoko.Server.Commands;
using Shoko.Server.Commands.Generic;

namespace Shoko.Server.Server;

public class ShokoService
{
    private static Logger logger = LogManager.GetCurrentClassLogger();

    private static readonly object cmdLockGeneral = new();
    private static readonly object cmdLockHasher = new();
    private static readonly object cmdLockImages = new();
    private static readonly object lockLastAniDBMessage = new();
    private static readonly object lockLastAniDBUDPMessage = new();
    private static readonly object lockLastAniDBHTTPMessage = new();
    private static readonly object lockLastAniDBMessageNonPing = new();
    private static readonly object lockLastAniDBPing = new();

    public static bool DebugFlag = false;

    public static void LogToSystem(string logType, string logMessage)
    {
        logger.Trace($"{logType} - {logMessage}");
    }

    private static DateTime lastAniDBMessage = DateTime.Now;

    public static DateTime LastAniDBMessage
    {
        get
        {
            lock (lockLastAniDBMessage)
            {
                return lastAniDBMessage;
            }
        }
        set => lastAniDBMessage = value;
    }

    private static DateTime lastAniDBUDPMessage = DateTime.Now;

    public static DateTime LastAniDBUDPMessage
    {
        get
        {
            lock (lockLastAniDBUDPMessage)
            {
                return lastAniDBUDPMessage;
            }
        }
        set => lastAniDBUDPMessage = value;
    }

    private static DateTime lastAniDBHTTPMessage = DateTime.Now;

    public static DateTime LastAniDBHTTPMessage
    {
        get
        {
            lock (lockLastAniDBHTTPMessage)
            {
                return lastAniDBHTTPMessage;
            }
        }
        set => lastAniDBHTTPMessage = value;
    }


    private static DateTime lastAniDBMessageNonPing = DateTime.Now;

    public static DateTime LastAniDBMessageNonPing
    {
        get
        {
            lock (lockLastAniDBMessageNonPing)
            {
                return lastAniDBMessageNonPing;
            }
        }
        set => lastAniDBMessageNonPing = value;
    }

    private static DateTime lastAniDBPing = DateTime.Now;

    public static DateTime LastAniDBPing
    {
        get
        {
            lock (lockLastAniDBPing)
            {
                return lastAniDBPing;
            }
        }
        set => lastAniDBPing = value;
    }

    private static readonly CommandProcessorGeneral _cmdProcessorGeneral = new();

    public static CommandProcessorGeneral CmdProcessorGeneral
    {
        get
        {
            lock (cmdLockGeneral)
            {
                return _cmdProcessorGeneral;
            }
        }
    }

    private static readonly CommandProcessorImages _cmdProcessorImages = new();

    public static CommandProcessorImages CmdProcessorImages
    {
        get
        {
            lock (cmdLockImages)
            {
                return _cmdProcessorImages;
            }
        }
    }

    private static readonly CommandProcessorHasher _cmdProcessorHasher = new();

    public static CommandProcessorHasher CmdProcessorHasher
    {
        get
        {
            lock (cmdLockHasher)
            {
                return _cmdProcessorHasher;
            }
        }
    }

    public static void CancelAndWaitForQueues()
    {
        CmdProcessorGeneral.Stop();
        CmdProcessorHasher.Stop();
        CmdProcessorImages.Stop();
        var queues = new[]
        {
            WaitForQueue(CmdProcessorGeneral), WaitForQueue(CmdProcessorHasher), WaitForQueue(CmdProcessorImages)
        };
        var done = Task.WhenAll(queues);
        done.Wait();
    }

    private static Task WaitForQueue(CommandProcessor queue)
    {
        return Task.Run(() =>
        {
            while (queue.IsWorkerBusy)
            {
                Thread.Sleep(250);
            }
        });
    }
}
