namespace Shoko.Server.Services.ConnectivityMon;

public class CloudFlareConnectivityMonitor : HeadConnectivityMonitor
{
    public CloudFlareConnectivityMonitor() : base("https://1.1.1.1/") { }
}
