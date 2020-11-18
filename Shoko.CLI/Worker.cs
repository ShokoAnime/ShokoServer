// ******************************* Module Header *******************************
// Module Name:   Worker.cs
// Project:       Shoko.Cli
// 
// MIT License
// 
// Copyright © 2020 Shoko
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.
// *****************************************************************************

namespace Shoko.Cli
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Hosting;
    using NLog;
    using Server.Server;
    using Server.Settings;
    using Server.Utilities;

    public class Worker : BackgroundService
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public Worker() => Args = null;

        public Worker(ProgramArguments args, IHostApplicationLifetime lifetime)
        {
            Args = args;
            AppLifetime = lifetime;
        }

        private IHostApplicationLifetime AppLifetime { get; }

        public ProgramArguments Args { get; set; }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!string.IsNullOrEmpty(Args?.Instance))
            {
                ServerSettings.DefaultInstance = Args.Instance;
            }

            ShokoServer.Instance.InitLogger();

            ServerSettings.LoadSettings();
            ServerState.Instance.LoadSettings();
            if (!ShokoServer.Instance.StartUpServer())
            {
                return;
            }

            // Ensure that the AniDB socket is initialized. Try to Login, then start the server if successful.
            ShokoServer.Instance.RestartAniDBSocket();
            if (!ServerSettings.Instance.FirstRun)
            {
                ShokoServer.RunWorkSetupDB();
            }
            else
            {
                Logger.Warn("The Server is NOT STARTED. It needs to be configured via webui or the settings.json");
            }

            ShokoServer.Instance.ServerShutdown += (sender, eventArgs) => AppLifetime.StopApplication();
            Utils.YesNoRequired += (sender, e) => { e.Cancel = true; };

            ServerState.Instance.PropertyChanged += (sender, e) =>
                                                    {
                                                        if (e.PropertyName == "StartupFailedMessage" && ServerState.Instance.StartupFailed)
                                                        {
                                                            Console.WriteLine("Startup failed! Error message: " + ServerState.Instance.StartupFailedMessage);
                                                        }
                                                    };
            ShokoService.CmdProcessorGeneral.OnQueueStateChangedEvent += ev => Console.WriteLine($"General Queue state change: {ev.QueueState.formatMessage()}");
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            ShokoService.CancelAndWaitForQueues();
            await base.StopAsync(cancellationToken);
        }
    }
}
