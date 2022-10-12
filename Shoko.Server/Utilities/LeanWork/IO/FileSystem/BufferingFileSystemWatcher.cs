﻿using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LeanWork.IO.FileSystem.Watcher.LeanWork.IO.FileSystem;

namespace LeanWork.IO.FileSystem;

/// <devdoc>
/// Features:
/// - Buffers FileSystemWatcher events in a BlockinCollection to prevent InternalBufferOverflowExceptions.
/// - Does not break the original FileSystemWatcher API.
/// - Supports reporting existing files via a new Existed event.
/// - Supports sorting events by oldest (existing) file first.
/// - Supports an new event Any reporting any FSW change.
/// - Offers the Error event in Win Forms designer (via [Browsable[true)]
/// - Does not prevent duplicate files occuring.
/// Notes:
///   We contain FilSystemWatcher to follow the prinicple composition over inheritance
///   and because System.IO.FileSystemWatcher is not designed to be inherited from:
///   Event handlers and Dispose(disposing) are not virtual.
/// </devdoc>
public class BufferingFileSystemWatcher : Component
{
    private FileSystemWatcher _containedFSW;

    private FileSystemEventHandler _onExistedHandler;
    private FileSystemEventHandler _onAllChangesHandler;

    private FileSystemEventHandler _onCreatedHandler;
    private FileSystemEventHandler _onChangedHandler;
    private FileSystemEventHandler _onDeletedHandler;
    private RenamedEventHandler _onRenamedHandler;

    private ErrorEventHandler _onErrorHandler;

    //We use a single buffer for all change types. Alternatively we could use one buffer per event type, costing additional enumerate tasks.
    private BlockingCollection<FileSystemEventArgs> _fileSystemEventBuffer;

    private CancellationTokenSource _cancellationTokenSource;

    #region Contained FileSystemWatcher

    public BufferingFileSystemWatcher()
    {
        _containedFSW = new FileSystemWatcher();
    }

    public BufferingFileSystemWatcher(string path)
    {
        _containedFSW = new FileSystemWatcher(path, "*.*");
    }

    public BufferingFileSystemWatcher(string path, string filter)
    {
        _containedFSW = new FileSystemWatcher(path, filter);
    }

    public bool EnableRaisingEvents
    {
        get => _containedFSW.EnableRaisingEvents;
        set
        {
            if (_containedFSW.EnableRaisingEvents == value)
            {
                return;
            }

            //always triggers cancel
            StopRaisingBufferedEvents();
            //We EnableRaisingEvents, before NotifyExistingFiles
            //  to prevent missing any events
            //  accepting more duplicates (which may occure anyway).
            if (value)
            {
                _fileSystemEventBuffer = new BlockingCollection<FileSystemEventArgs>(_eventQueueSize);
            }

            _containedFSW.EnableRaisingEvents = value;
            if (value)
            {
                RaiseBufferedEventsUntilCancelled();
            }
        }
    }


    public string Filter
    {
        get => _containedFSW.Filter;
        set => _containedFSW.Filter = value;
    }

    public bool IncludeSubdirectories
    {
        get => _containedFSW.IncludeSubdirectories;
        set => _containedFSW.IncludeSubdirectories = value;
    }

    public int InternalBufferSize
    {
        get => _containedFSW.InternalBufferSize;
        set => _containedFSW.InternalBufferSize = value;
    }

    public NotifyFilters NotifyFilter
    {
        get => _containedFSW.NotifyFilter;
        set => _containedFSW.NotifyFilter = value;
    }

    public string Path
    {
        get => _containedFSW.Path;
        set => _containedFSW.Path = value;
    }

    public ISynchronizeInvoke SynchronizingObject
    {
        get => _containedFSW.SynchronizingObject;
        set => _containedFSW.SynchronizingObject = value;
    }

    public override ISite Site
    {
        get => _containedFSW.Site;
        set => _containedFSW.Site = value;
    }

    #endregion

    [DefaultValue(false)] public bool OrderByOldestFirst { get; set; } = false;

    private int _eventQueueSize = int.MaxValue;

    public int EventQueueCapacity
    {
        get => _eventQueueSize;
        set => _eventQueueSize = value;
    }

    public bool DisableEvents { get; set; }

    #region New BufferingFileSystemWatcher specific events

    public event FileSystemEventHandler Existed
    {
        add => _onExistedHandler += value;
        remove => _onExistedHandler -= value;
    }

    public event FileSystemEventHandler All
    {
        add
        {
            if (_onAllChangesHandler == null)
            {
                _containedFSW.Created += BufferEvent;
                _containedFSW.Changed += BufferEvent;
                _containedFSW.Renamed += BufferEvent;
                _containedFSW.Deleted += BufferEvent;
            }

            _onAllChangesHandler += value;
        }
        remove
        {
            _containedFSW.Created -= BufferEvent;
            _containedFSW.Changed -= BufferEvent;
            _containedFSW.Renamed -= BufferEvent;
            _containedFSW.Deleted -= BufferEvent;
            _onAllChangesHandler -= value;
        }
    }

    #endregion

    #region Standard FSW events

    //- The _fsw events add to the buffer.
    //- The public events raise from the buffer to the consumer.
    public event FileSystemEventHandler Created
    {
        add
        {
            if (_onCreatedHandler == null)
            {
                _containedFSW.Created += BufferEvent;
            }

            _onCreatedHandler += value;
        }
        remove
        {
            _containedFSW.Created -= BufferEvent;
            _onCreatedHandler -= value;
        }
    }

    public event FileSystemEventHandler Changed
    {
        add
        {
            if (_onChangedHandler == null)
            {
                _containedFSW.Changed += BufferEvent;
            }

            _onChangedHandler += value;
        }
        remove
        {
            _containedFSW.Changed -= BufferEvent;
            _onChangedHandler -= value;
        }
    }

    public event FileSystemEventHandler Deleted
    {
        add
        {
            if (_onDeletedHandler == null)
            {
                _containedFSW.Deleted += BufferEvent;
            }
        }
        remove
        {
            _containedFSW.Deleted -= BufferEvent;
            _onDeletedHandler -= value;
        }
    }

    public event RenamedEventHandler Renamed
    {
        add
        {
            if (_onRenamedHandler == null)
            {
                _containedFSW.Renamed += BufferEvent;
            }

            _onRenamedHandler += value;
        }
        remove
        {
            _containedFSW.Renamed -= BufferRenameEvent;
            _onRenamedHandler -= value;
        }
    }

    private void BufferEvent(object _, FileSystemEventArgs e)
    {
        if (!_fileSystemEventBuffer.TryAdd(e))
        {
            var ex = new EventQueueOverflowException(
                $"Event queue size {_fileSystemEventBuffer.BoundedCapacity} events exceeded.");
            InvokeHandler(_onErrorHandler, new ErrorEventArgs(ex));
        }
    }

    private void BufferRenameEvent(object _, RenamedEventArgs e)
    {
        if (!_fileSystemEventBuffer.TryAdd(e))
        {
            var ex = new EventQueueOverflowException(
                $"Event queue size {_fileSystemEventBuffer.BoundedCapacity} events exceeded.");
            InvokeHandler(_onErrorHandler, new ErrorEventArgs(ex));
        }
    }

    private void StopRaisingBufferedEvents(object _ = null, EventArgs __ = null)
    {
        _cancellationTokenSource?.Cancel();
    }

    public event ErrorEventHandler Error
    {
        add
        {
            if (_onErrorHandler == null)
            {
                _containedFSW.Error += BufferingFileSystemWatcher_Error;
            }

            _onErrorHandler += value;
        }
        remove
        {
            if (_onErrorHandler == null)
            {
                _containedFSW.Error -= BufferingFileSystemWatcher_Error;
            }

            _onErrorHandler -= value;
        }
    }

    private void BufferingFileSystemWatcher_Error(object sender, ErrorEventArgs e)
    {
        InvokeHandler(_onErrorHandler, e);
    }

    #endregion

    private void RaiseBufferedEventsUntilCancelled()
    {
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = new CancellationTokenSource();

        Task.Run(() =>
        {
            try
            {
                if (_onExistedHandler != null || _onAllChangesHandler != null)
                {
                    NotifyExistingFiles();
                }

                foreach (var e in _fileSystemEventBuffer.GetConsumingEnumerable(_cancellationTokenSource.Token))
                {
                    if (DisableEvents)
                    {
                        continue;
                    }

                    if (_onAllChangesHandler != null)
                    {
                        InvokeHandler(_onAllChangesHandler, e);
                    }
                    else
                    {
                        switch (e.ChangeType)
                        {
                            case WatcherChangeTypes.Created:
                                InvokeHandler(_onCreatedHandler, e);
                                break;
                            case WatcherChangeTypes.Changed:
                                InvokeHandler(_onChangedHandler, e);
                                break;
                            case WatcherChangeTypes.Deleted:
                                InvokeHandler(_onDeletedHandler, e);
                                break;
                            case WatcherChangeTypes.Renamed:
                                InvokeHandler(_onRenamedHandler, e as RenamedEventArgs);
                                break;
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
            } //ignore
            catch (Exception ex)
            {
                BufferingFileSystemWatcher_Error(this, new ErrorEventArgs(ex));
            }
        });
    }

    private void NotifyExistingFiles()
    {
        if (OrderByOldestFirst)
        {
            var searchSubDirectoriesOption = IncludeSubdirectories
                ? SearchOption.AllDirectories
                : SearchOption.TopDirectoryOnly;
            var sortedFileNames = from fi in new DirectoryInfo(Path).GetFiles(Filter, searchSubDirectoriesOption)
                orderby fi.LastWriteTime
                select fi.Name;
            foreach (var fileName in sortedFileNames)
            {
                InvokeHandler(_onExistedHandler, new FileSystemEventArgs(WatcherChangeTypes.All, Path, fileName));
                InvokeHandler(_onAllChangesHandler, new FileSystemEventArgs(WatcherChangeTypes.All, Path, fileName));
            }
        }
        else
        {
            foreach (var fileName in Directory.EnumerateFiles(Path))
            {
                InvokeHandler(_onExistedHandler, new FileSystemEventArgs(WatcherChangeTypes.All, Path, fileName));
                InvokeHandler(_onAllChangesHandler, new FileSystemEventArgs(WatcherChangeTypes.All, Path, fileName));
            }
        }
    }

    #region InvokeHandlers

    //Automatically raise event in calling thread when _fsw.SynchronizingObject is set. Ex: When used as a component in Win Forms.
    //TODO: remove redundancy. I don't understand how to cast the specific *EventHandler to a generic Delegate, EventHandler, Action or whatever.
    private void InvokeHandler(FileSystemEventHandler eventHandler, FileSystemEventArgs e)
    {
        if (eventHandler != null)
        {
            if (_containedFSW.SynchronizingObject != null && _containedFSW.SynchronizingObject.InvokeRequired)
            {
                _containedFSW.SynchronizingObject.BeginInvoke(eventHandler, new object[] { this, e });
            }
            else
            {
                eventHandler(this, e);
            }
        }
    }

    private void InvokeHandler(RenamedEventHandler eventHandler, RenamedEventArgs e)
    {
        if (eventHandler != null)
        {
            if (_containedFSW.SynchronizingObject != null && _containedFSW.SynchronizingObject.InvokeRequired)
            {
                _containedFSW.SynchronizingObject.BeginInvoke(eventHandler, new object[] { this, e });
            }
            else
            {
                eventHandler(this, e);
            }
        }
    }

    private void InvokeHandler(ErrorEventHandler eventHandler, ErrorEventArgs e)
    {
        if (eventHandler != null)
        {
            if (_containedFSW.SynchronizingObject != null && _containedFSW.SynchronizingObject.InvokeRequired)
            {
                _containedFSW.SynchronizingObject.BeginInvoke(eventHandler, new object[] { this, e });
            }
            else
            {
                eventHandler(this, e);
            }
        }
    }

    #endregion

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _containedFSW?.Dispose();

            //_onExistedHandler = null;
            //_onAllChangesHandler = null;
            //_onCreatedHandler = null;
            //_onChangedHandler = null;
            //_onDeletedHandler = null;
            //_onRenamedHandler = null;
            //_onErrorHandler = null;
        }

        base.Dispose(disposing);
    }
}
