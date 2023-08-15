#region
using System;
using System.ComponentModel;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;
using Shoko.Server.Commands;
using Shoko.Server.Server;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

#endregion
namespace Shoko.CLI;

public static class Program
{
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
        var logger = logFactory.CreateLogger("Main");

        try
        {
            var settingsProvider = new SettingsProvider(logFactory.CreateLogger<SettingsProvider>());
            settingsProvider.LoadSettings();
            Utils.SettingsProvider = settingsProvider;
            var startup = new Startup(logFactory.CreateLogger<Startup>(), settingsProvider);
            startup.Start();
            AddEventHandlers();
            startup.WaitForShutdown();
        }
        catch (Exception e)
        {
            logger.LogCritical(e, "The server failed to start");
        }
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
