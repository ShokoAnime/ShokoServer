using Shoko.Server;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Shoko.Server.Commands;

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
            
            ShokoServer.Instance.StartUpServer();

            ShokoServer.RunWorkSetupDB();

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
