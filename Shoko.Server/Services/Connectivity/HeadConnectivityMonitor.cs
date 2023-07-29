// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;

namespace Shoko.Server.Services.ConnectivityMon;

public abstract class HeadConnectivityMonitor : IConnectivityMonitor
{
    // TODO: Maybe cache the result for a few seconds so that we dont hammer the remote site with unnecessary ping packets

    private readonly string _target;
    private readonly HttpClient _client;
    private bool _lastState;
    private DateTime? _lastRunTimestamp;

    protected HeadConnectivityMonitor(string target)
    {
        _target = target;
        _client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(5)
        };
    }

    public async Task ExecuteCheckAsync()
    {
        // Only trigger every 15 minutes
        if (_lastRunTimestamp is not null && _lastRunTimestamp.Value.AddMinutes(15) < DateTime.Now)
            return;
        
        // TODO: Use polly for retry and backoff
        var request = new HttpRequestMessage
        {
            Method = HttpMethod.Head,
            RequestUri = new Uri(_target)
        };
        try
        {
            var result = await _client.SendAsync(request);
            _lastState = result.IsSuccessStatusCode;
        }
        catch
        {
            _lastState = false;
        }
        finally
        {
            _lastRunTimestamp = DateTime.Now;
        }
    }

    public async Task<bool> IsUpAsync()
    {
        return await Task.FromResult(_lastState);
    }
}
