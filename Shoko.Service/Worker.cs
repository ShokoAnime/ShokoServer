using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using NLog;
using Shoko.Server;
using Shoko.Server.Settings;

namespace Shoko.Service
{
    public class Worker : BackgroundService
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        
        private string[] Args { get; set; }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (Args != null)
            {
                for (int x = 0; x < Args.Length; x++)
                {
                    if (!Args[x].Equals("instance", StringComparison.InvariantCultureIgnoreCase)) continue;
                    if (x + 1 < Args.Length)
                    {
                        ServerSettings.DefaultInstance = Args[x + 1];
                    }
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
            else Logger.Warn("The Server is NOT STARTED. It needs to be configured via webui or the settings.json");

            ShokoServer.Instance.ServerShutdown += (sender, eventArgs) => StopAsync(stoppingToken);
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
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(1000, stoppingToken);
            }

            ShokoService.CancelAndWaitForQueues();
        }
    }
}