using Shoko.Server;
using System;

namespace Shoko.CLI
{
    class Program
    {
        static void Main(string[] args)
        {
            for (int x = 0; x < args.Length; x++)
            {
                if (args[x].Equals("instance", StringComparison.InvariantCultureIgnoreCase))
                {
                    if (x + 1 < args.Length)
                    {
                        ServerSettings.DefaultInstance = args[x + 1];
                    }
                }
            }

            ServerSettings.LoadSettings();
            ServerState.Instance.LoadSettings();
            ShokoServer.Instance.SetupNetHosts();

            if (!ServerSettings.Instance.FirstRun)
            {
                ShokoServer.Instance.StartUpServer();
                ShokoServer.RunWorkSetupDB();
            }

            bool running = true;

            ShokoServer.Instance.ServerShutdown += (sender, eventArgs) => running = false;
            Utils.YesNoRequired += (sender, e) =>
            {
                e.Cancel = true;
            };

            ServerState.Instance.PropertyChanged += (sender, e) =>
            {
                if (e.PropertyName == "StartupFailedMessage" && ServerState.Instance.StartupFailed)
                {
                    Console.WriteLine("Startup failed! Error message: " + ServerState.Instance.StartupFailedMessage);
                }
            };

            ShokoService.CmdProcessorGeneral.OnQueueStateChangedEvent +=
                ev => Console.WriteLine($"Queue state change: {ev.QueueState.formatMessage()}");

            while (running)
            {
                System.Threading.Thread.Sleep(TimeSpan.FromSeconds(60));
            }
        }
    }
}
