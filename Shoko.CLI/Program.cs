#region
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;
using Shoko.Server.Commands;
using Shoko.Server.Filters;
using Shoko.Server.Repositories;
using Shoko.Server.Server;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

#endregion
namespace Shoko.CLI;

public static class Program
{
    private static ILogger _logger;
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
        var logFactory = new LoggerFactory().AddNLog();
        _logger = logFactory.CreateLogger("Main");

        try
        {
            var settingsProvider = new SettingsProvider(logFactory.CreateLogger<SettingsProvider>());
            settingsProvider.LoadSettings();
            Utils.SettingsProvider = settingsProvider;
            var startup = new Startup(logFactory.CreateLogger<Startup>(), settingsProvider);
            startup.Start();
            AddEventHandlers();
            // TODO Remove this after filter merge
            Utils.ShokoServer.DBSetupCompleted += OnShokoServerOnDBSetupCompleted;
            startup.WaitForShutdown();
        }
        catch (Exception e)
        {
            _logger.LogCritical(e, "The server failed to start");
        }
    }
    
    private static void OnShokoServerOnDBSetupCompleted(object? o, EventArgs eventArgs)
    {
        var comedyFilter = RepoFactory.Filter.GetAll().FirstOrDefault(a => a.Name.Equals("comedy", StringComparison.InvariantCultureIgnoreCase));
        if (comedyFilter == null) return;
        var filterEvaluator = Utils.ServiceContainer.GetRequiredService<FilterEvaluator>();
        var s = Stopwatch.StartNew();
        var result = filterEvaluator.EvaluateFilter(comedyFilter, null);
        s.Stop();
        _logger.LogInformation("Filtering took {Time}ms", s.ElapsedMilliseconds);
        s.Restart();
        var groups = result.SelectMany(a => a.Select(b => new
            {
                Group = RepoFactory.AnimeGroup.GetByID(a.Key), Series = RepoFactory.AnimeSeries.GetByID(b)
            }))
            .GroupBy(a => a.Group, a => a.Series)
            .ToDictionary(a => a.Key, a => a.ToList());
        s.Stop();
        _logger.LogInformation("Projecting results took {Time}ms", s.ElapsedMilliseconds);
        _logger.LogInformation("Finished");
    }

    
    private static void AddEventHandlers()
    {
        Utils.YesNoRequired += OnUtilsOnYesNoRequired;
        ServerState.Instance.PropertyChanged += OnInstanceOnPropertyChanged;
        ShokoService.CmdProcessorGeneral.OnQueueStateChangedEvent += OnCmdProcessorGeneralOnQueueStateChangedEvent;
        ShokoService.CmdProcessorImages.OnQueueStateChangedEvent += OnCmdProcessorImagesOnQueueStateChangedEvent;
        ShokoService.CmdProcessorHasher.OnQueueStateChangedEvent += OnCmdProcessorHasherOnQueueStateChangedEvent;
    }

    private static string LastGeneralQueueMessage = string.Empty;

    private static void OnCmdProcessorGeneralOnQueueStateChangedEvent(QueueStateEventArgs ev) 
    {
        var message = ev.QueueState.formatMessage();
        if (!string.Equals(LastGeneralQueueMessage, message))
        {
            LastGeneralQueueMessage = message;
            Console.WriteLine($@"General Queue state change: {message}");
        }
    }

    private static string LastImagesQueueMessage = string.Empty;

    private static void OnCmdProcessorImagesOnQueueStateChangedEvent(QueueStateEventArgs ev) 
    {
        var message = ev.QueueState.formatMessage();
        if (!string.Equals(LastImagesQueueMessage, message))
        {
            LastImagesQueueMessage = message;
            Console.WriteLine($@"Images Queue state change: {message}");
        }
    }

    private static string LastHasherQueueMessage = string.Empty;

    private static void OnCmdProcessorHasherOnQueueStateChangedEvent(QueueStateEventArgs ev) 
    {
        var message = ev.QueueState.formatMessage();
        if (!string.Equals(LastHasherQueueMessage, message))
        {
            LastHasherQueueMessage = message; 
            Console.WriteLine($@"Hasher Queue state change: {message}");
        }
    }

    private static void OnInstanceOnPropertyChanged(object? _, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == "StartupFailedMessage" && ServerState.Instance.StartupFailed)
            Console.WriteLine("Startup failed! Error message: " + ServerState.Instance.StartupFailedMessage);
    }

    private static void OnUtilsOnYesNoRequired(object? _, Utils.CancelReasonEventArgs e) => e.Cancel = true;
}
