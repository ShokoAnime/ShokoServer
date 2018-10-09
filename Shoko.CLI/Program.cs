using Shoko.Server;
using System;
using NLog;

namespace Shoko.CLI
{
    class Program
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();
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
            ShokoServer.Instance.StartUpServer();

            // Ensure that the AniDB socket is initialized. Try to Login, then start the server if successful.
            ShokoServer.Instance.RestartAniDBSocket();
            if (ShokoService.AnidbProcessor.ValidAniDBCredentials() && ShokoService.AnidbProcessor.Login())
                ShokoServer.RunWorkSetupDB();
            else logger.Warn("The Server is NOT STARTED. It needs to be configured via webui or the settings.json");

            bool running = true;

            ShokoServer.Instance.ServerShutdown += (sender, eventArgs) => running = false;
            Utils.YesNoRequired += (sender, e) =>
            {
                e.Cancel = true;
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
