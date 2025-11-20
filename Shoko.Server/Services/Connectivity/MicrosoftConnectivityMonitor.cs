using Microsoft.Extensions.Logging;

namespace Shoko.Server.Services.Connectivity;

public class MicrosoftConnectivityMonitor : HeadConnectivityMonitor
{
    public MicrosoftConnectivityMonitor(ILogger<MicrosoftConnectivityMonitor> logger) : base("https://www.msftconnecttest.com/connecttest.txt", logger) { }
    public override string Service => "Microsoft";
}
