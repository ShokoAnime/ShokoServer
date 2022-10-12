﻿using System;
using System.ComponentModel;
using System.IO;
using System.Threading;
using LeanWork.IO.FileSystem.Watcher.LeanWork.IO.FileSystem;
using NLog;

namespace LeanWork.IO.FileSystem;

public class RecoveringFileSystemWatcher : BufferingFileSystemWatcher
{
    public TimeSpan DirectoryMonitorInterval = TimeSpan.FromMinutes(5);
    public TimeSpan DirectoryRetryInterval = TimeSpan.FromSeconds(5);

    private Timer _monitorTimer;

    private bool _isRecovering;
    private static Logger _trace = LogManager.GetCurrentClassLogger();


    public RecoveringFileSystemWatcher()
    {
    }

    public RecoveringFileSystemWatcher(string path)
        : base(path, "*.*")
    {
    }

    public RecoveringFileSystemWatcher(string path, string filter)
        : base(path, filter)
    {
    }

    //To allow consumer to cancel default error handling
    private EventHandler<FileWatcherErrorEventArgs> _onErrorHandler;

    public new event EventHandler<FileWatcherErrorEventArgs> Error
    {
        add => _onErrorHandler += value;
        remove => _onErrorHandler -= value;
    }

    public new bool EnableRaisingEvents
    {
        get => base.EnableRaisingEvents;
        set
        {
            //Static _firstCall = True
            //If _firstCall And value Then
            //    _firstCall = False
            //    File.Create(System.IO.Path.Combine(Path, "~~monitored by RecoveringFileSystemWatcher~~"), 1024, FileOptions.DeleteOnClose)
            //End If

            if (value == EnableRaisingEvents)
            {
                return;
            }

            base.EnableRaisingEvents = value;
            if (EnableRaisingEvents)
            {
                base.Error += BufferingFileSystemWatcher_Error;
                Start();
            }
            else
            {
                base.Error -= BufferingFileSystemWatcher_Error;
            }
        }
    }

    private void Start()
    {
        try
        {
            _monitorTimer = new Timer(_monitorTimer_Elapsed);

            Disposed += (_, __) => { _monitorTimer.Dispose(); };

            ReStartIfNeccessary(TimeSpan.Zero);
        }
        catch (Exception ex)
        {
            _trace.Error($"Unexpected error: {ex}");
            throw;
        }
    }

    private void _monitorTimer_Elapsed(object state)
    {
        try
        {
            if (!Directory.Exists(Path))
            {
                throw new DirectoryNotFoundException($"Directory not found {Path}");
            }

            if (!EnableRaisingEvents)
            {
                EnableRaisingEvents = true;
                if (_isRecovering)
                {
                    _trace.Warn("<= Watcher recovered");
                }
            }

            ReStartIfNeccessary(DirectoryMonitorInterval);
        }
        catch (Exception ex) when (ex is FileNotFoundException || ex is DirectoryNotFoundException)
        {
            //Handles race condition too: Path loses accessiblity between .Exists() and .EnableRaisingEvents 
            if (ExceptionWasHandledByCaller(ex))
            {
                return;
            }

            if (_isRecovering)
            {
                _trace.Warn("...retrying");
            }
            else
            {
                _trace.Warn($@"=> Directory {Path} Is Not accessible.
                                 - Will try to recover automatically in {DirectoryRetryInterval}!");
                _isRecovering = true;
            }

            EnableRaisingEvents = false;
            _isRecovering = true;
            ReStartIfNeccessary(DirectoryRetryInterval);
        }
        catch (Exception ex)
        {
            _trace.Error($"Unexpected error: {ex}");
            throw;
        }
    }

    private void ReStartIfNeccessary(TimeSpan delay)
    {
        try
        {
            _monitorTimer.Change(delay, Timeout.InfiniteTimeSpan);
        }
        catch (ObjectDisposedException)
        {
        } //ignore timer disposed     
    }

    private void BufferingFileSystemWatcher_Error(object sender, ErrorEventArgs e)
    {
        //These exceptions have the same HResult
        var NetworkNameNoLongerAvailable = -2147467259; //occurs on network outage
        var AccessIsDenied = -2147467259; //occurs after directory was deleted


        var ex = e.GetException();
        if (ExceptionWasHandledByCaller(e.GetException()))
        {
            return;
        }

        //The base FSW does set .EnableRaisingEvents=False AFTER raising OnError()
        EnableRaisingEvents = false;

        if (ex is InternalBufferOverflowException || ex is EventQueueOverflowException)
        {
            _trace.Warn(ex.Message);
            _trace.Error(@"This should Not happen with short event handlers!
                             - Will recover automatically.");
            ReStartIfNeccessary(DirectoryRetryInterval);
        }
        else if (ex is Win32Exception && (ex.HResult == NetworkNameNoLongerAvailable) |
                 (ex.HResult == AccessIsDenied))
        {
            _trace.Debug(ex.Message);
            _trace.Debug("Will try to recover automatically!");
            ReStartIfNeccessary(DirectoryRetryInterval);
        }
        else
        {
            _trace.Error($@"Unexpected error: {ex}
                             - Watcher is disabled!");
            throw ex;
        }
    }

    //Function GetMaxInternalBuffersize() As Integer
    //    'NOTE: Only increase FSW InternalBuffersize after evaluation other options:
    //    '  http://msdn.microsoft.com/en-us/library/ded0dc5s.aspx
    //    '  http://msdn.microsoft.com/en-us/library/aa366778(VS.85).aspx
    //    Dim maxInternalBufferSize64BitOS = ByteSize.ByteSize.FromKiloBytes(16 * 4)
    //    Dim maxInternalBufferSize32BitOS = ByteSize.ByteSize.FromKiloBytes(2 * 4)
    //    If Environment.Is64BitOperatingSystem Then
    //        Return maxInternalBufferSize64BitOS.Bytes
    //    Else
    //        Return maxInternalBufferSize32BitOS.Bytes
    //    End If
    //End Function

    private bool ExceptionWasHandledByCaller(Exception ex)
    {
        //Allow consumer to handle error
        if (_onErrorHandler != null)
        {
            var e = new FileWatcherErrorEventArgs(ex);
            InvokeHandler(_onErrorHandler, e);
            return e.Handled;
        }

        return false;
    }

    private void InvokeHandler(EventHandler<FileWatcherErrorEventArgs> eventHandler, FileWatcherErrorEventArgs e)
    {
        if (eventHandler != null)
        {
            if (SynchronizingObject != null && SynchronizingObject.InvokeRequired)
            {
                SynchronizingObject.BeginInvoke(eventHandler, new object[] { this, e });
            }
            else
            {
                eventHandler(this, e);
            }
        }
    }
}
