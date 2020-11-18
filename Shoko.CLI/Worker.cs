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
    using Microsoft.Extensions.Logging;
    using Properties;
    using Server.Server;
    using Server.Settings;
    using Server.Utilities;

    public sealed class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;

        public Worker(ProgramArguments args, IHostApplicationLifetime lifetime, ILogger<Worker> logger)
        {
            // Make sure everything is set.
            Args = args ?? throw new ArgumentNullException(nameof(args));
            AppLifetime = lifetime ?? throw new ArgumentNullException(nameof(lifetime));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        private IHostApplicationLifetime AppLifetime { get; }
        private ProgramArguments Args { get; }

        //async without await is useless. We return Task instead.
        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!string.IsNullOrEmpty(Args.Instance))
            {
                ServerSettings.DefaultInstance = Args.Instance;
            }

            ShokoServer.Instance.InitLogger();

            ServerSettings.LoadSettings();
            ServerState.Instance.LoadSettings();
            if (!ShokoServer.Instance.StartUpServer())
            {
                return Task.CompletedTask;
                //TODO: Maybe it is more fitting to return an Task With Exception that represents the case that the instance in not started.
                //return Task.FromException()
            }

            // Ensure that the AniDB socket is initialized. Try to Login, then start the server if successful.
            ShokoServer.Instance.RestartAniDBSocket();
            if (!ServerSettings.Instance.FirstRun)
            {
                ShokoServer.RunWorkSetupDB();
            }
            else
            {
                _logger.LogWarning(Resources.Worker_ExecuteAsync_ServerNotStarted_LogMessage);
            }

            ShokoServer.Instance.ServerShutdown += (sender, eventArgs) => AppLifetime.StopApplication();
            Utils.YesNoRequired += (sender, e) => e.Cancel = true;

            ServerState.Instance.PropertyChanged += (sender, e) =>
                                                    {
                                                        if (e.PropertyName == "StartupFailedMessage" && ServerState.Instance.StartupFailed)
                                                        {
                                                            // Changed from Console to logger output.
                                                            // Console output can configured in the logger if a console is present.
                                                            _logger.LogError(Resources.Worker_ExecuteAsync_StartupFailed_LogMessage, ServerState.Instance.StartupFailedMessage);
                                                        }
                                                    };
            // Changed from Console to logger output.
            // Console output can configured in the logger if a console is present.
            ShokoService.CmdProcessorGeneral.OnQueueStateChangedEvent += ev => _logger.LogInformation(Resources.Worker_ExecuteAsync_GeneralQueueStateChange_LogMessage, ev.QueueState.formatMessage());

            //Everything is fine so we return a completed task.
            return Task.CompletedTask;
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            ShokoService.CancelAndWaitForQueues();
            await base.StopAsync(cancellationToken);
        }
    }
}
