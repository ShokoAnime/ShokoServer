using Microsoft.Extensions.Logging;

namespace Shoko.Server.Services.Connectivity;

public class WeChatConnectivityMonitor : GetConnectivityMonitor
{
    public WeChatConnectivityMonitor(ILogger<WeChatConnectivityMonitor> logger) : base("https://www.wechat.com/", logger) { }
    public override string Service => "WeChat";
}
