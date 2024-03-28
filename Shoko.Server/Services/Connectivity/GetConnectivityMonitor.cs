using System;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Security;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Shoko.Server.Services.Connectivity;

public abstract class GetConnectivityMonitor : IConnectivityMonitor
{
    private readonly ILogger _logger;
    private readonly string _target;
    private readonly HttpClient _client;
    private bool _lastState;
    private DateTime? _lastRunTimestamp;

    protected GetConnectivityMonitor(string target, ILogger logger)
    {
        _logger = logger;
        _target = target;
        _client = new HttpClient(new SocketsHttpHandler
        {
            SslOptions = new SslClientAuthenticationOptions
            {
                RemoteCertificateValidationCallback = delegate { return true; }
            }
        })
        {
            Timeout = TimeSpan.FromSeconds(5)
        };
    }

    public abstract string Service { get; }

    public async Task ExecuteCheckAsync(CancellationToken token)
    {
        // Only trigger every 15 minutes
        if (_lastRunTimestamp is not null && _lastRunTimestamp.Value.AddMinutes(15) < DateTime.Now)
            return;

        var lastState = _lastState;
        _logger.LogTrace("Trying to connect to {Service}", Service);
        // TODO: Use polly for retry and backoff
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(_target));
        var sw = Stopwatch.StartNew();
        try
        {
            var result = await _client.SendAsync(request, token);
            _lastState = result.IsSuccessStatusCode;
            sw.Stop();
            if (_lastState)
                _logger.LogTrace("Successfully connected to {Service} in {Time}ms", Service, sw.ElapsedMilliseconds);
            else
                _logger.LogTrace("Received a failed status code from {Service} in {Time}ms: {Code}", Service, sw.ElapsedMilliseconds,
                    (int)result.StatusCode);
        }
        catch
        {
            _lastState = false;
            sw.Stop();
            _logger.LogTrace("Failed to connect to {Service} after {Time}ms", Service, sw.ElapsedMilliseconds);
        }
        finally
        {
            _lastRunTimestamp = DateTime.Now;
        }

        if (lastState != _lastState) StateChanged?.Invoke(null, EventArgs.Empty);
    }

    public bool HasConnected => _lastState;
    public event EventHandler StateChanged;
}
