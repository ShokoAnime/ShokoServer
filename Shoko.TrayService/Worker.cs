#region
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Shoko.Server.Server;
using Shoko.Server.Utilities;
#endregion
namespace Shoko.TrayService;

public class Worker : BackgroundService
{
    private readonly ShokoServer _server;
    private readonly StartServer _startup;

    public Worker() { }
    public Worker(ShokoServer server, StartServer startup)
    {
        _server = server;
        _startup = startup;
        Utils.ShokoServer = server;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _startup.StartupServer(AddEventHandlers, () => _server.StartUpServer());
        return Task.CompletedTask;
    }

    private void AddEventHandlers()
    {
        Utils.ShokoServer.ServerShutdown += App.OnInstanceOnServerShutdown;
        Utils.YesNoRequired += (_, args) => args.Cancel = true;
    }
}
