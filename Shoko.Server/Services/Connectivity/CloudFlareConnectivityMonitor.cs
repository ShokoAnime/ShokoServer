using Microsoft.Extensions.Logging;

namespace Shoko.Server.Services.Connectivity;

public class CloudFlareConnectivityMonitor : HeadConnectivityMonitor
{
    public CloudFlareConnectivityMonitor(ILogger<CloudFlareConnectivityMonitor> logger) : base("https://1.1.1.1/", logger) { }
    public override string Service => "CloudFlare";
}
