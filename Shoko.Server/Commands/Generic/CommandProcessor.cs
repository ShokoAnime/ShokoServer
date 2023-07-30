using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Force.DeepCloner;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Commands.Interfaces;
using Shoko.Server.Repositories;

namespace Shoko.Server.Commands.Generic;

public abstract class CommandProcessor : IDisposable, ICommandProcessor
{
    protected ILogger Logger;
    protected readonly BackgroundWorker WorkerCommands = new();
    protected IServiceProvider ServiceProvider;
    private bool _processingCommands;
    private bool _cancelled = false;

    public abstract string QueueType { get; }

    private readonly object _lockQueueState = new();

    public delegate void QueueStateChangedHandler(QueueStateEventArgs ev);

    public event QueueStateChangedHandler OnQueueStateChangedEvent;

    private bool _paused;

    public bool Paused
    {
        get => _paused;
        set
        {
            var unpausing = !value && _paused;
            _paused = value;

            UpdatePause(_paused);
            UpdateQueueCount(force: true);

            if (unpausing)
            {
                _cancelled = false;
                StartWorker();
            }
        }
    }

    private int _queueCount;

    public int QueueCount => _queueCount;

    private QueueStateStruct _queueState = new() { queueState = QueueStateEnum.Idle, extraParams = new string[0] };

    public QueueStateStruct QueueState
    {
        // use copies and never return the object in use
        get
        {
            lock (_lockQueueState) return _queueState.DeepClone();
        }
        set
        {
            lock (_lockQueueState) _queueState = value.DeepClone();

            Task.Factory.StartNew(() => OnQueueStateChangedEvent?.Invoke(new QueueStateEventArgs(value, CurrentCommand?.CommandRequestID, _queueCount, _paused)));
        }
    }

    public CommandRequest CurrentCommand { get; protected set; }

    public bool ProcessingCommands => _processingCommands;

    public bool IsWorkerBusy => WorkerCommands.IsBusy;

    public CommandProcessor()
    {
        WorkerCommands.WorkerReportsProgress = true;
        WorkerCommands.WorkerSupportsCancellation = true;
        WorkerCommands.DoWork += WorkerCommands_DoWork;
        WorkerCommands.RunWorkerCompleted += WorkerCommands_RunWorkerCompleted;
    }

    protected void WorkerCommands_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
    {
        CurrentCommand = null;
        _processingCommands = false;

        if (e.Cancelled)
        {
            Logger.LogWarning("The {QueueType} Queue was cancelled with {QueueCount} commands left", QueueType, QueueCount);
        }

        UpdateQueueCount(_paused ? (
            new() { message = "Paused", queueState = QueueStateEnum.Paused, extraParams = new string[0] }
        ) : (
            new() { message = "Idle", queueState = QueueStateEnum.Idle, extraParams = new string[0] }
        ));
    }

    public virtual void Init(IServiceProvider provider)
    {
        ServiceProvider = provider;
        var logFactory = provider.GetRequiredService<ILoggerFactory>();
        Logger = logFactory.CreateLogger(GetType());

        // Start Paused. We'll unpause after setup is complete
        Paused = true;
        _cancelled = false;
        StartWorker();
    }

    public void Stop()
    {
        Logger?.LogInformation("{QueueType} Queue has been stopped, {QueueCount} commands left", QueueType, QueueCount);
        _cancelled = true;
        WorkerCommands.CancelAsync();
    }

    /// <summary>
    /// This is simply used to tell the command processor that a new command has been added to the database
    /// </summary>
    public void NotifyOfNewCommand()
    {
        UpdateQueueCount();
        StartWorker();
    }

    public void Clear()
    {
        Stop();

        RepoFactory.CommandRequest.ClearByQueueType(QueueType);

        NotifyOfNewCommand();
    }

    protected void StartWorker()
    {
        // if the worker is busy, it will pick up the next command from the DB
        // do not pick new command if cancellation is requested
        if (_processingCommands || _cancelled) return;

        _processingCommands = true;
        if (!WorkerCommands.IsBusy) WorkerCommands.RunWorkerAsync();
    }

    protected abstract void UpdatePause(bool pauseState);

    protected void UpdateQueueCount(QueueStateStruct? queueState = null, bool force = false)
    {
        var currentCount = _queueCount;
        _queueCount = RepoFactory.CommandRequest.GetQueuedCommandCountByType(QueueType);

        // If the count changed, we provided a new state, or if we want to force
        // update the state for other reasons, then update the state.
        if (_queueCount != currentCount || queueState.HasValue || force)
        {
            if (queueState.HasValue)
                _queueState = queueState.Value;
            Task.Factory.StartNew(() => OnQueueStateChangedEvent?.Invoke(new QueueStateEventArgs(_queueState, CurrentCommand?.CommandRequestID, _queueCount, _paused)));
        }
    }

    protected abstract CommandRequest GetNextCommandRequest();

    protected virtual void WorkerCommands_DoWork(object sender, DoWorkEventArgs e)
    {
        while (true)
        {
            try
            {
                if (WorkerCommands.CancellationPending) return;

                // if paused we will sleep for 200 milliseconds, and the try again
                if (_paused)
                {
                    try
                    {
                        if (QueueState.queueState != QueueStateEnum.Paused)
                            QueueState = new() { message = "Paused", queueState = QueueStateEnum.Paused, extraParams = new string[0] };

                        if (WorkerCommands.CancellationPending) return;
                    }
                    catch
                    {
                        // ignore
                    }

                    Thread.Sleep(200);
                    continue;
                }

                if (WorkerCommands.CancellationPending) return;

                var crdb = GetNextCommandRequest();
                if (crdb == null)
                {
                    if (QueueCount > 0)
                        Logger.LogWarning("No command returned from repo, but there are {QueueCount} commands left", QueueCount);

                    return;
                }

                var icr = CommandHelper.GetCommand(ServiceProvider, crdb);
                if (icr == null)
                {
                    Logger.LogWarning("No implementation found for command: {CommandType}-{CommandID}", crdb.CommandType,
                        crdb.CommandID);
                }
                // Only continue with running the command if a cancellation is
                // not pending.
                else if (!WorkerCommands.CancellationPending)
                {
                    Logger.LogTrace("Processing command request: {CommandID}", crdb.CommandID);
                    try
                    {
                        CurrentCommand = crdb;

                        QueueState = icr.PrettyDescription;

                        icr.Processor = this;
                        icr.ProcessCommand();
                        icr.Processor = null;
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex, "ProcessCommand exception: {CommandID}", crdb.CommandID);
                    }
                    finally
                    {
                        CurrentCommand = null;
                    }
                }

                Logger.LogTrace("Deleting command request: {Command}", crdb.CommandID);
                RepoFactory.CommandRequest.Delete(crdb.CommandRequestID);
                UpdateQueueCount();
            }
            catch (Exception exception)
            {
                Logger.LogError(exception, "Error Processing Commands");
            }
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing) WorkerCommands?.Dispose();
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
