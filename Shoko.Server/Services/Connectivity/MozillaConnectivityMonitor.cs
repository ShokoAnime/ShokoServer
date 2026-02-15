using Microsoft.Extensions.Logging;

namespace Shoko.Server.Services.Connectivity;

public class MozillaConnectivityMonitor : HeadConnectivityMonitor
{
    public MozillaConnectivityMonitor(ILogger<MozillaConnectivityMonitor> logger) : base("https://detectportal.firefox.com/success.txt", logger) { }
    public override string Service => "Mozilla";
}
