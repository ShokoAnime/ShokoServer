using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using Hardcodet.Wpf.TaskbarNotification;
using NLog;
using Shoko.Models.Interfaces;
using Shoko.Server;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Providers.AniDB.UDP.Generic;
using Shoko.Server.Providers.AniDB.UDP.Info;
using Shoko.Server.Server;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

namespace Shoko.TrayService
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static TaskbarIcon Icon { get; set; }
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private void OnStartup(object a, StartupEventArgs e)
        {
            Console.CancelKeyPress += (sender, args) => Dispatcher.Invoke(() =>
            {
                args.Cancel = true;
                Shutdown();
            });
            Icon = new TaskbarIcon();
            using (var iconStream = GetResourceStream(new Uri("pack://application:,,,/ShokoServer;component/db.ico"))
                ?.Stream)
            {
                if (iconStream != null) Icon.Icon = new Icon(iconStream);
            }
            Icon.ToolTipText = "Shoko Server";
            ContextMenu menu = new ContextMenu();
            MenuItem webui = new MenuItem();
            webui.Header = "Open WebUI";
            webui.Click += (sender, args) =>
            {
                try
                {
                    string IP = GetLocalIPv4(NetworkInterfaceType.Ethernet);
                    if (string.IsNullOrEmpty(IP))
                        IP = "127.0.0.1";

                    string url = $"http://{IP}:{ServerSettings.Instance.ServerPort}";
                    OpenUrl(url);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, ex.ToString());
                }
            };
            menu.Items.Add(webui);
            webui = new MenuItem();
            webui.Header = "Exit";
            webui.Click += (sender, args) => Dispatcher.Invoke(Shutdown);
            menu.Items.Add(webui);
            Icon.ContextMenu = menu;
            Icon.MenuActivation = PopupActivationMode.All;
            Icon.Visibility = Visibility.Visible;

            string instance = null;
            for (int x = 0; x < e.Args.Length; x++)
            {
                if (!e.Args[x].Equals("instance", StringComparison.InvariantCultureIgnoreCase)) continue;
                if (x + 1 >= e.Args.Length) continue;
                instance = e.Args[x + 1];
                break;
            }

            var arguments = new ProgramArguments {Instance = instance};
            if (!string.IsNullOrEmpty(arguments.Instance)) ServerSettings.DefaultInstance = arguments.Instance;

            ShokoServer.Instance.InitLogger();
            
            ServerSettings.LoadSettings();
            ServerState.Instance.LoadSettings();

            if (!ShokoServer.Instance.StartUpServer()) return;

            if (!ServerSettings.Instance.FirstRun)
                ShokoServer.RunWorkSetupDB();
            else Logger.Warn("The Server is NOT STARTED. It needs to be configured via webui or the settings.json");

            ShokoServer.Instance.ServerShutdown += (sender, eventArgs) => Shutdown();
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

        protected override void OnExit(ExitEventArgs e)
        {
            base.OnExit(e);
            Logger.Info("Exit was Requested. Shutting Down.");
            ShokoService.CancelAndWaitForQueues();
            Icon.Visibility = Visibility.Hidden;
        }
        
        private void OpenUrl(string url)
        {
            try
            {
                Process.Start(url);
            }
            catch
            {
                // hack because of this: https://github.com/dotnet/corefx/issues/10361
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    url = url.Replace("&", "^&");
                    Process.Start(new ProcessStartInfo("cmd", $"/c start {url}") { CreateNoWindow = true });
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    Process.Start("xdg-open", url);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    Process.Start("open", url);
                }
                else
                {
                    throw;
                }
            }
        }
        
        internal static string GetLocalIPv4(NetworkInterfaceType _type)
        {
            string output = "";
            foreach (NetworkInterface item in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (item.NetworkInterfaceType == _type && item.OperationalStatus == OperationalStatus.Up)
                {
                    IPInterfaceProperties adapterProperties = item.GetIPProperties();

                    if (adapterProperties.GatewayAddresses.FirstOrDefault() != null)
                    {
                        foreach (UnicastIPAddressInformation ip in adapterProperties.UnicastAddresses)
                        {
                            if (ip.Address.AddressFamily == AddressFamily.InterNetwork)
                            {
                                output = ip.Address.ToString();
                            }
                        }
                    }
                }
            }

            return output;
        }
    }
}