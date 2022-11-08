using System;
using System.ComponentModel;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Force.DeepCloner;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Settings;

namespace Shoko.Server.Commands.Generic;

public abstract class CommandProcessor : IDisposable
{
    protected ILogger Logger;
    protected readonly BackgroundWorker WorkerCommands = new();
    protected IServiceProvider ServiceProvider;
    private bool _processingCommands;

    protected abstract string QueueType { get; }

    private readonly object _lockQueueState = new();

    public delegate void QueueCountChangedHandler(QueueCountEventArgs ev);

    public event QueueCountChangedHandler OnQueueCountChangedEvent;

    public delegate void QueueStateChangedHandler(QueueStateEventArgs ev);

    public event QueueStateChangedHandler OnQueueStateChangedEvent;

    private bool _paused;

    public bool Paused
    {
        get => _paused;
        set
        {
            _paused = value;
            lock (_lockQueueState)
            {
                if (_paused)
                {
                    QueueState = new QueueStateStruct
                    {
                        message = "Paused", queueState = QueueStateEnum.Paused, extraParams = new string[0]
                    };
                }
                else
                {
                    QueueState = new QueueStateStruct
                    {
                        message = "Unpaused", queueState = QueueStateEnum.Idle, extraParams = new string[0]
                    };
                }
            }

            UpdatePause(_paused);
        }
    }

    protected abstract void UpdatePause(bool pauseState);

    private int _queueCount;

    public int QueueCount
    {
        get => _queueCount;
        set
        {
            _queueCount = value;
            Task.Factory.StartNew(() => OnQueueCountChangedEvent?.Invoke(new QueueCountEventArgs(value)));
        }
    }

    private QueueStateStruct _queueState = new() { queueState = QueueStateEnum.Idle, extraParams = new string[0] };

    public QueueStateStruct QueueState
    {
        // use copies and never return the object in use
        get
        {
            lock (_lockQueueState)
            {
                return _queueState.DeepClone();
            }
        }
        set
        {
            lock (_lockQueueState)
            {
                _queueState = value.DeepClone();
            }

            Task.Factory.StartNew(() => OnQueueStateChangedEvent?.Invoke(new QueueStateEventArgs(value)));
        }
    }

    public CommandRequest CurrentCommand { get; protected set; }

    public bool ProcessingCommands => _processingCommands;

    public bool IsWorkerBusy => WorkerCommands.IsBusy;

    public CommandProcessor()
    {
        Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(ServerSettings.Instance.Culture);
        WorkerCommands.WorkerReportsProgress = true;
        WorkerCommands.WorkerSupportsCancellation = true;
        WorkerCommands.DoWork += WorkerCommands_DoWork;
        WorkerCommands.RunWorkerCompleted += WorkerCommands_RunWorkerCompleted;
    }

    protected void WorkerCommands_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
    {
        CurrentCommand = null;
        _processingCommands = false;
        _paused = false;

        if (e.Cancelled)
        {
            Logger.LogWarning("The {QueueType} Queue was cancelled with {QueueCount} commands left", QueueType,
                QueueCount);
        }

        QueueState = new QueueStateStruct
        {
            message = "Idle", queueState = QueueStateEnum.Idle, extraParams = new string[0]
        };

        try
        {
            UpdateQueueCount();
        }
        catch
        {
            // ignore
        }
    }

    public virtual void Init(IServiceProvider provider)
    {
        ServiceProvider = provider;
        Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(ServerSettings.Instance.Culture);
        var logFactory = provider.GetRequiredService<ILoggerFactory>();
        Logger = logFactory.CreateLogger(GetType());

        // Start Paused. We'll unpause after setup is complete
        Paused = true;
        _processingCommands = true;
        if (!WorkerCommands.IsBusy)
        {
            WorkerCommands.RunWorkerAsync();
        }
    }

    public void Stop()
    {
        Logger?.LogInformation("{QueueType} Queue has been stopped, {QueueCount} commands left", QueueType, QueueCount);
        WorkerCommands.CancelAsync();
    }

    /// <summary>
    /// This is simply used to tell the command processor that a new command has been added to the database
    /// </summary>
    public void NotifyOfNewCommand()
    {
        UpdateQueueCount();
        // if the worker is busy, it will pick up the next command from the DB
        // do not pick new command if cancellation is requested
        if (_processingCommands || WorkerCommands.CancellationPending)
        {
            return;
        }

        _processingCommands = true;
        if (!WorkerCommands.IsBusy)
        {
            WorkerCommands.RunWorkerAsync();
        }
    }

    protected abstract void WorkerCommands_DoWork(object sender, DoWorkEventArgs e);

    protected abstract void UpdateQueueCount();

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            WorkerCommands?.Dispose();
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
