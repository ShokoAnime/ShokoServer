using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Config.Services;
using Shoko.Server.Providers.AniDB.HTTP;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.AniDB.UDP;
using Shoko.Server.Settings;

namespace Shoko.Tests.Infrastructure;

/// <summary>
/// Test doubles for the AniDB connection layer.
/// </summary>
/// <remarks>
/// Nothing here opens a socket or resolves a host. The HTTP side is driven through a stub
/// <see cref="HttpMessageHandler"/> and the UDP side through <see cref="IAniDBSocketHandlerFactory"/>,
/// so the protocol handling can be exercised with no network of any kind.
/// </remarks>
public static class AniDBTestDoubles
{
    /// <summary>
    /// Builds a <see cref="ConfigurationProvider{TConfig}"/> over the given settings, with the rate
    /// limits zeroed so tests are not paced by the real AniDB throttle.
    /// </summary>
    public static ConfigurationProvider<ServerSettings> Configuration(ServerSettings? settings = null)
    {
        settings ??= new ServerSettings();
        settings.AniDb.HTTPRateLimit.BaseRateInSeconds = 0;
        settings.AniDb.UDPRateLimit.BaseRateInSeconds = 0;

        var info = (ConfigurationInfo)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(ConfigurationInfo));
        var service = new Mock<IConfigurationService>();
        service.Setup(s => s.GetConfigurationInfo<ServerSettings>()).Returns(info);
        service.Setup(s => s.Load(It.IsAny<ConfigurationInfo>(), It.IsAny<bool>())).Returns(settings);

        return new ConfigurationProvider<ServerSettings>(service.Object);
    }

    public static HttpRateLimiter HttpRateLimiter(ServerSettings? settings = null)
        => new(NullLogger<HttpRateLimiter>.Instance, Configuration(settings));

    public static UDPRateLimiter UdpRateLimiter(ServerSettings? settings = null)
        => new(NullLogger<UDPRateLimiter>.Instance, Configuration(settings));

    /// <summary>
    /// An <see cref="HttpMessageHandler"/> that answers from a queue instead of the network, and
    /// records what it was asked for.
    /// </summary>
    public sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _responses = new();

        public List<HttpRequestMessage> Requests { get; } = [];

        public int CallCount => Requests.Count;

        public StubHttpMessageHandler Respond(HttpStatusCode status, string body)
        {
            _responses.Enqueue(_ => new HttpResponseMessage(status) { Content = new StringContent(body) });
            return this;
        }

        public StubHttpMessageHandler Throw(Exception exception)
        {
            _responses.Enqueue(_ => throw exception);
            return this;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            if (_responses.Count == 0)
                throw new InvalidOperationException($"No canned response for {request.RequestUri}.");

            return Task.FromResult(_responses.Dequeue()(request));
        }
    }

    /// <summary>
    /// Builds an <see cref="IHttpClientFactory"/> whose clients are backed by
    /// <paramref name="handler"/>.
    /// </summary>
    public static IHttpClientFactory HttpClientFactory(HttpMessageHandler handler)
    {
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(() => new HttpClient(handler, disposeHandler: false));
        return factory.Object;
    }

    /// <summary>
    /// A socket handler that replays canned payloads instead of talking to AniDB.
    /// </summary>
    public sealed class StubSocketHandler : IAniDBSocketHandler
    {
        private readonly Queue<byte[]> _responses = new();

        public bool IsConnected { get; set; } = true;

        public bool ConnectionAttempted { get; private set; }

        public List<byte[]> Sent { get; } = [];

        public StubSocketHandler Respond(byte[] payload)
        {
            _responses.Enqueue(payload);
            return this;
        }

        public byte[] Send(byte[] payload)
        {
            Sent.Add(payload);
            if (_responses.Count == 0)
                throw new InvalidOperationException("No canned response for this UDP call.");

            return _responses.Dequeue();
        }

        public bool TryConnection()
        {
            ConnectionAttempted = true;
            return IsConnected;
        }

        public void Dispose() { }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    public static IAniDBSocketHandlerFactory SocketHandlerFactory(IAniDBSocketHandler handler)
    {
        var factory = new Mock<IAniDBSocketHandlerFactory>();
        factory.Setup(f => f.Create(It.IsAny<string>(), It.IsAny<ushort>(), It.IsAny<ushort>())).Returns(handler);
        return factory.Object;
    }
}
