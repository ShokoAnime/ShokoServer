using System;
using Shoko.Server.Settings;
using NLog;
namespace Shoko.Server.Server;
#nullable enable
public static class StartServer
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    public static void StartupServer(Action? addEventHandlers, Func<bool> serverStart)
    {
        SetInstanceIfNeeded();
        InitInstanceLogger();
        LoadSettings();
        if (serverStart?.Invoke() == false)
            return;
        EnsureAniDBSocketInitialized();
        addEventHandlers?.Invoke();
    }

    public static string? GetInstanceFromCommandLineArguments()
    {
        const int notFound = -1;
        var       args     = Environment.GetCommandLineArgs();
        var       idx      = Array.FindIndex(args, x => string.Equals(x, "instance", StringComparison.InvariantCultureIgnoreCase));
        if (idx is notFound)
            return null;
        if (idx >= args.Length - 1)
            return null;
        return args[idx + 1];
    }
    
    ///Ensure that the AniDB socket is initialized. Try to Login, then start the server if successful.
    private static void EnsureAniDBSocketInitialized()
    {
        if (ServerSettings.Instance.FirstRun is false)
            ShokoServer.RunWorkSetupDB();
        else
            Logger.Warn("The Server is NOT STARTED. It needs to be configured via webui or the settings.json");
    }

    private static void InitInstanceLogger() => ShokoServer.Instance.InitLogger();

    private static void SetInstanceIfNeeded()
    {
        var instance = GetInstanceFromCommandLineArguments();
        if (string.IsNullOrWhiteSpace(instance) is false)
            ServerSettings.DefaultInstance = instance;
    }

    private static void LoadSettings()
    {
        ServerSettings.LoadSettings();
        ServerState.Instance.LoadSettings();
    }
}
#nullable disable