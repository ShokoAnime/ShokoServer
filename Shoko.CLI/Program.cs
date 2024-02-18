﻿#region
using System;
using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;
using Shoko.Server.Scheduling;
using Shoko.Server.Server;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

#endregion
namespace Shoko.CLI;

public static class Program
{
    private static ILogger _logger = null!;
    public static void Main()
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
            var settingsProvider = new SettingsProvider(logFactory.CreateLogger<SettingsProvider>());
            settingsProvider.LoadSettings();
            Utils.SettingsProvider = settingsProvider;
            var startup = new Startup(logFactory.CreateLogger<Startup>(), settingsProvider);
            startup.AboutToStart += (_, args) => AddEventHandlers(args.ServiceProvider);
            startup.Start();
            startup.WaitForShutdown();
        }
        catch (Exception e)
        {
            _logger.LogCritical(e, "The server failed to start");
        }
    }
    
    private static void AddEventHandlers(IServiceProvider provider)
    {
        Utils.YesNoRequired += OnUtilsOnYesNoRequired;
        ServerState.Instance.PropertyChanged += OnInstanceOnPropertyChanged;
        var queueStateEventHandler = provider.GetRequiredService<QueueStateEventHandler>();
        queueStateEventHandler.QueueItemAdded += QueueStateEventHandlerOnQueueItemAdded;
        queueStateEventHandler.ExecutingJobsChanged += ExecutingJobsStateEventHandlerOnExecutingJobsChanged;
    }

    private static void QueueStateEventHandlerOnQueueItemAdded(object? sender, QueueItemAddedEventArgs e)
    {
        if (e.AddedItems is not { Count: > 0 }) return;

        foreach (var addedItem in e.AddedItems)
        {
            _logger.LogTrace("Job Added: {Type} | {Details}", addedItem.JobType ?? addedItem.Key, addedItem.Description);
        }
    }

    private static void ExecutingJobsStateEventHandlerOnExecutingJobsChanged(object? sender, QueueChangedEventArgs e)
    {
        if (e.AddedItems is { Count: > 0 })
        {
            foreach (var addedItem in e.AddedItems)
            {
                _logger.LogTrace("Job Started: {Type} | {Details}", addedItem.JobType ?? addedItem.Key, addedItem.Description);
            }
        }

        if (e.RemovedItems is { Count: > 0 })
        {
            foreach (var removedItem in e.RemovedItems)
            {
                _logger.LogTrace("Job Completed: {Type} | {Details}", removedItem.JobType, removedItem.Description);
            }
        }

        _logger.LogTrace("Waiting: {Waiting} | Blocked: {Blocked} | ThreadPoolSize: {Pool} | Currently Executing: {Executing}", e.WaitingJobsCount,
            e.BlockedJobsCount, e.ThreadCount, e.ExecutingItems?.Count ?? 0);
    }

    private static void OnInstanceOnPropertyChanged(object? _, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == "StartupFailedMessage" && ServerState.Instance.StartupFailed)
            Console.WriteLine(@"Startup failed! Error message: " + ServerState.Instance.StartupFailedMessage);
    }

    private static void OnUtilsOnYesNoRequired(object? _, Utils.CancelReasonEventArgs e) => e.Cancel = true;
}
