using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;
using Shoko.Server.Scheduling;
using Shoko.Server.Server;
using Shoko.Server.Utilities;

namespace Shoko.CLI;

public static class Program
{
    private static ILogger _logger = null!;
    public static async Task<int> Main()
    {
        try
        {
            UnhandledExceptionManager.AddHandler();
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
        }
        Utils.SetInstance();
        Utils.InitLogger();
        var logFactory = LoggerFactory.Create(o => o.AddNLog());
        _logger = logFactory.CreateLogger("Main");

        try
        {
            var startup = new Startup(logFactory);
            startup.AboutToStart += (_, args) => AddEventHandlers(args.ServiceProvider);
            await startup.Start();
            await startup.WaitForShutdown();
            return 0;
        }
        catch (Exception e)
        {
            _logger.LogCritical(e, "The server failed to start");
            return -1;
        }
    }

    private static void AddEventHandlers(IServiceProvider provider)
    {
        ServerState.Instance.PropertyChanged += OnInstanceOnPropertyChanged;
        var queueStateEventHandler = provider.GetRequiredService<QueueStateEventHandler>();
        queueStateEventHandler.QueueItemsAdded += QueueStateEventHandlerOnQueueItemAdded;
        queueStateEventHandler.ExecutingJobsChanged += ExecutingJobsStateEventHandlerOnExecutingJobsChanged;
    }

    private static string GetDetails(Dictionary<string, object>? map)
    {
        return map == null ? string.Intern("No Details") : string.Join(", ", map.Select(a => a.Key + ": " + a.Value));
    }

    private static void QueueStateEventHandlerOnQueueItemAdded(object? sender, QueueItemsAddedEventArgs e)
    {
        if (e.AddedItems is not { Count: > 0 }) return;

        foreach (var addedItem in e.AddedItems)
        {
            _logger.LogTrace("Job Added: {Type} | {Details}", addedItem.Title, GetDetails(addedItem.Details));
        }

        _logger.LogTrace("Waiting: {Waiting} | Blocked: {Blocked} | Executing: {Executing}/{Pool} | Total: {Total}", e.WaitingJobsCount,
            e.BlockedJobsCount, e.ExecutingJobsCount, e.ThreadCount, e.TotalJobsCount);
    }

    private static void ExecutingJobsStateEventHandlerOnExecutingJobsChanged(object? sender, QueueChangedEventArgs e)
    {
        if (e.AddedItems is { Count: > 0 })
        {
            foreach (var addedItem in e.AddedItems)
            {
                _logger.LogTrace("Job Started: {Type} | {Details}", addedItem.Title, GetDetails(addedItem.Details));
            }
        }

        if (e.RemovedItems is { Count: > 0 })
        {
            foreach (var removedItem in e.RemovedItems)
            {
                _logger.LogTrace("Job Completed: {Type} | {Details}", removedItem.Title, GetDetails(removedItem.Details));
            }
        }

        _logger.LogTrace("Waiting: {Waiting} | Blocked: {Blocked} | Executing: {Executing}/{Pool} | Total: {Total}", e.WaitingJobsCount,
            e.BlockedJobsCount, e.ExecutingItems?.Count ?? 0, e.ThreadCount, e.TotalJobsCount);
    }

    private static void OnInstanceOnPropertyChanged(object? _, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == "StartupFailedMessage" && ServerState.Instance.StartupFailed)
            Console.WriteLine(@"Startup failed! Error message: " + ServerState.Instance.StartupFailedMessage);
    }
}
