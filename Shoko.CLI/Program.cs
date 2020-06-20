using System;
using System.Threading;
using NLog;
using Shoko.Server;
using Shoko.Server.Settings;

namespace Shoko.CLI
{
    class Program
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();
        private static bool _running;

        static Program()
        {
            _running = true;
        }

        static void Main(string[] args)
        {
            for (int x = 0; x < args.Length; x++)
            {
                if (!args[x].Equals("instance", StringComparison.InvariantCultureIgnoreCase)) continue;
                if (x + 1 < args.Length)
                {
                    ServerSettings.DefaultInstance = args[x + 1];
                }
            }

            ShokoServer.Instance.InitLogger();
            
            ServerSettings.LoadSettings();
            ServerState.Instance.LoadSettings();
            if (!ShokoServer.Instance.StartUpServer()) return;

            // Ensure that the AniDB socket is initialized. Try to Login, then start the server if successful.
            ShokoServer.Instance.RestartAniDBSocket();
            if (!ServerSettings.Instance.FirstRun)
                ShokoServer.RunWorkSetupDB();
            else logger.Warn("The Server is NOT STARTED. It needs to be configured via webui or the settings.json");

            ShokoServer.Instance.ServerShutdown += (sender, eventArgs) => _running = false;
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
                ev => Console.WriteLine($"General Queue state change: {ev.QueueState.formatMessage()}");

            while (_running)
            {
                Thread.Sleep(TimeSpan.FromSeconds(60));
            }
        }
    }
}
