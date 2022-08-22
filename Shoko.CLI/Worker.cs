using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using NLog;
using Shoko.Server;
using Shoko.Server.Server;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

namespace Shoko.CLI
{
    public class Worker : BackgroundService
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public ProgramArguments Args { get; set; }
        private IHostApplicationLifetime AppLifetime { get; set; }
        
        public Worker()
        {
            Args = null;
        }

        public Worker(ProgramArguments args, IHostApplicationLifetime lifetime)
        {
            Args = args;
            AppLifetime = lifetime;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!string.IsNullOrEmpty(Args?.Instance)) ServerSettings.DefaultInstance = Args.Instance;

            ShokoServer.Instance.InitLogger();
            
            ServerSettings.LoadSettings();
            ServerState.Instance.LoadSettings();
            if (!ShokoServer.Instance.StartUpServer()) return;

            // Ensure that the AniDB socket is initialized. Try to Login, then start the server if successful.
            if (!ServerSettings.Instance.FirstRun)
                ShokoServer.RunWorkSetupDB();
            else Logger.Warn("The Server is NOT STARTED. It needs to be configured via webui or the settings.json");

            ShokoServer.Instance.ServerShutdown += (sender, eventArgs) => AppLifetime.StopApplication();
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
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            ShokoService.CancelAndWaitForQueues();
            await base.StopAsync(cancellationToken);
        }
    }
}