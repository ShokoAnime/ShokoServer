using System;
using Avalonia;
using Shoko.Server.Server;

namespace Shoko.TrayService;

public static class Program
{
    internal static string[] StartupArgs { get; private set; } = Array.Empty<string>();

    [STAThread]
    public static int Main(string[] args)
    {
        StartupArgs = args;
        try
        {
            UnhandledExceptionManager.AddHandler();
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
        }
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace()
            .StartWithClassicDesktopLifetime(args);
    }
}
