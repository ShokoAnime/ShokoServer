using System;
using System.Threading;
using System.Threading.Tasks;

namespace Shoko.Server.Services.Connectivity;

public interface IConnectivityMonitor
{
    public string Service { get; }
    public Task ExecuteCheckAsync(CancellationToken token);
    public bool HasConnected { get; }
    event EventHandler StateChanged;
}
