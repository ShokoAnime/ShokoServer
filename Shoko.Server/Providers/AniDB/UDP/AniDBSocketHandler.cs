using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using ICSharpCode.SharpZipLib.Zip.Compression;
using Microsoft.Extensions.Logging;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Utilities;

namespace Shoko.Server.Providers.AniDB.UDP;

public class AniDBSocketHandler : IAniDBSocketHandler
{
    private IPEndPoint _localIpEndPoint;
    private IPEndPoint _remoteIpEndPoint;
    private readonly Socket _aniDBSocket;
    private readonly string _serverHost;
    private readonly ushort _serverPort;
    private readonly ushort _clientPort;
    private readonly ILogger<AniDBSocketHandler> _logger;
    private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
    private bool Locked { get; set; }
    public bool IsLocked => Locked;
    private static int SendTimeoutMs => 30000; // 30 seconds
    private static int ReceiveTimeoutMs => 30000; // 30 seconds
    public bool IsConnected { get; private set; }

    public AniDBSocketHandler(ILoggerFactory loggerFactory, string host, ushort serverPort, ushort clientPort)
    {
        _logger = loggerFactory.CreateLogger<AniDBSocketHandler>();
        _aniDBSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        _serverHost = host;
        _serverPort = serverPort;
        _clientPort = clientPort;
    }

    public async Task<byte[]> Send(byte[] payload)
    {
        if (!IsConnected) return [0];
        await _semaphore.WaitAsync();
        // this doesn't need to be bigger than 1400, but meh, better safe than sorry
        var result = new byte[1600];
        Locked = true;

        try
        {
            result = await SendUnsafe(payload, result);
        }
        finally
        {
            _semaphore.Release();
        }

        Locked = false;
        return result;
    }

    private async Task<byte[]> SendUnsafe(byte[] payload, byte[] result)
    {
        EmptyBuffer();

        using CancellationTokenSource sendCts = new(SendTimeoutMs);
        await _aniDBSocket.SendToAsync(payload, SocketFlags.None, _remoteIpEndPoint, sendCts.Token);

        using CancellationTokenSource receiveCts = new(ReceiveTimeoutMs);
        var receivedResult = await _aniDBSocket.ReceiveFromAsync(result, SocketFlags.None, _remoteIpEndPoint, receiveCts.Token);

        var received = receivedResult.ReceivedBytes;

        if (received > 2 && result[0] == 0 && result[1] == 0)
        {
            //deflate
            var buff = new byte[65536];
            var input = new byte[received - 2];
            Array.Copy(result, 2, input, 0, received - 2);
            var inf = new Inflater(false);
            inf.SetInput(input);
            inf.Inflate(buff);
            result = buff;
            received = (int)inf.TotalOut;
        }

        Array.Resize(ref result, received);

        EmptyBuffer();
        return result;
    }

    private void EmptyBuffer()
    {
        if (_aniDBSocket.Available == 0) return;
        var result = new byte[1600];
        try
        {
            _aniDBSocket.Receive(result);
            var decodedString = Utils.GetEncoding(result).GetString(result, 0, result.Length);
            if (decodedString[0] == 0xFEFF) // remove BOM
            {
                decodedString = decodedString[1..];
            }
            _logger.LogWarning("Unexpected data in the UDP stream: {Result}", decodedString);
        }
        catch
        {
            // ignore
        }
    }

    public async Task<bool> TryConnection()
    {
        if (IsConnected) return true;
        await _semaphore.WaitAsync();
        // Don't send Expect 100 requests. These requests aren't always supported by remote internet devices, in which case can cause failure.
        ServicePointManager.Expect100Continue = false;

        try
        {
            _localIpEndPoint = new IPEndPoint(IPAddress.Any, _clientPort);

            // we use bind() here (normally only for servers, not clients) instead of connect() because of this:
            /*
             * Local Port
             *  A client should select a fixed local port >1024 at install time and reuse it for local UDP Sockets. If the API sees too many different UDP Ports from one IP within ~1 hour it will ban the IP. (So make sure you're reusing your UDP ports also for testing/debugging!)
             *  The local port may be hardcoded, however, an option to manually specify another port should be offered.
             */
            _aniDBSocket.Bind(_localIpEndPoint);
            _aniDBSocket.ReceiveTimeout = ReceiveTimeoutMs;

            _logger.LogInformation("Bound to local address: {Local} - Port: {ClientPort} ({Family})", _localIpEndPoint,
                _clientPort, _localIpEndPoint.AddressFamily);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not bind to local port");
            _semaphore.Release();
            IsConnected = false;
            return false;
        }

        try
        {
            var remoteHostEntry = await Dns.GetHostEntryAsync(_serverHost).WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            _remoteIpEndPoint = new IPEndPoint(remoteHostEntry.AddressList[0], _serverPort);

            _logger.LogInformation("Bound to remote address: {Address} : {Port}", _remoteIpEndPoint.Address,
                _remoteIpEndPoint.Port);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not bind to remote port");
            _semaphore.Release();
            IsConnected = false;
            return false;
        }

        _semaphore.Release();
        IsConnected = true;
        return true;
    }

    public async ValueTask DisposeAsync()
    {
        await _semaphore.WaitAsync();
        GC.SuppressFinalize(this);
        if (_aniDBSocket == null)
        {
            IsConnected = false;
            return;
        }

        try
        {
            if (_aniDBSocket.Connected)
            {
                _aniDBSocket.Shutdown(SocketShutdown.Both);
            }

            // should not be the case
            if (_aniDBSocket.Connected)
            {
                await _aniDBSocket.DisconnectAsync(false);
            }
        }
        catch (SocketException ex)
        {
            _logger.LogError(ex, "Failed to Shutdown and Disconnect the connection to AniDB");
        }
        finally
        {
            _aniDBSocket.Close();
            _logger.LogInformation("Closed AniDB Connection");
            _semaphore.Release();
            _semaphore.Dispose();
            IsConnected = false;
        }
    }

    public void Dispose()
    {
        _semaphore.Wait();
        GC.SuppressFinalize(this);
        if (_aniDBSocket == null)
        {
            IsConnected = false;
            return;
        }

        try
        {
            if (_aniDBSocket.Connected)
            {
                _aniDBSocket.Shutdown(SocketShutdown.Both);
            }

            // should not be the case
            if (_aniDBSocket.Connected)
            {
                _aniDBSocket.Disconnect(false);
            }
        }
        catch (SocketException ex)
        {
            _logger.LogError(ex, "Failed to Shutdown and Disconnect the connection to AniDB");
        }
        finally
        {
            _aniDBSocket.Close();
            _logger.LogInformation("Closed AniDB Connection");
            _semaphore.Release();
            _semaphore.Dispose();
            IsConnected = false;
        }
    }
}
