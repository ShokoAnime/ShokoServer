using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Core.Events;
using Shoko.Server.Scheduling;
using Shoko.Server.Server;
using Shoko.Server.Services;

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

        var systemService = new SystemService();
        systemService.StartupFailed += OnStartupFailed;
        systemService.AboutToStart += (_, args) => AddEventHandlers(args.ServiceProvider);
        var host = await systemService.StartAsync();
        if (host is null)
            return 1;

        await systemService.WaitForShutdownAsync();
        if (systemService.RestartPending)
            return 140; // Custom restart exit code.
        if (systemService.StartupFailedException is not null)
            return 1;
        return 0;
    }

    private static void AddEventHandlers(IServiceProvider provider)
    {
        var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger("Main");
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

    private static void OnStartupFailed(object? _, StartupFailedEventArgs args)
    {
        Console.WriteLine(@"Startup failed! Error message: " + args.Exception.Message);
    }
}
