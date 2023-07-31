// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Shoko.Server.Services.Connectivity;

[Obsolete("We can maybe make use of this later, but Linux apparently handles ping stupid", error: true, UrlFormat = "https://github.com/dotnet/runtime/issues/62458")]
public abstract class PingConnectivityMonitor : IConnectivityMonitor
{
    // TODO: Maybe cache the result for a few seconds so that we dont hammer the remote site with unnecessary ping packets

    private readonly string _target;
    private bool _lastState;
    private DateTime? _lastRunTimestamp;

    protected PingConnectivityMonitor(string target)
    {
        _target = target;
    }

    public async Task ExecuteCheckAsync(CancellationToken token)
    {
        // Only trigger every 15 minutes
        if (_lastRunTimestamp is not null && _lastRunTimestamp.Value.AddMinutes(15) < DateTime.Now)
            return;
        
        // TODO: Use polly for retry and backoff
        var pingSender = new Ping();
        var pingOptions = new PingOptions(128, true); // Use the default ttl of 128 but don't fragment the packets
        var buffer = Encoding.ASCII.GetBytes(new string('a', 32));
        var reply = await pingSender.SendPingAsync(_target, 120, buffer, pingOptions).WaitAsync(token);
        _lastState = reply.Status == IPStatus.Success;
        _lastRunTimestamp = DateTime.Now;
    }

    public bool HasConnected => _lastState;
}
