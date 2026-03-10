using System;
using Avalonia;
using Shoko.Server.Server;

namespace Shoko.TrayService;

public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
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
