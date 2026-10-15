using System;
using System.Net;
using System.Net.Http;
using Shoko.Abstractions.Extensions;
using Shoko.Server.Plugin;

namespace Shoko.Server.Utilities;

/// <summary>
///   Hands out the core's <c>"Default"</c> HTTP client before the web host
///   exists, over one handler shared for the life of the process. The web
///   host's own <c>"Default"</c> client is set up by the same methods.
/// </summary>
internal sealed class CoreHttpClientFactory : IHttpClientFactory
{
    /// <summary>
    ///   The name of the core's default client.
    /// </summary>
    internal const string DefaultClientName = "Default";

    private static readonly Lazy<CoreHttpClientFactory> _instance = new(() => new());

    private readonly SocketsHttpHandler _handler;

    private CoreHttpClientFactory()
    {
        _handler = new();
        ConfigureDefaultHandler(_handler);
    }

    /// <summary>
    ///   The one instance, built on first use.
    /// </summary>
    internal static CoreHttpClientFactory Instance => _instance.Value;

    /// <summary>
    ///   Sets the headers every request of the default client carries.
    /// </summary>
    /// <param name="client">The client to set up.</param>
    internal static void ConfigureDefaultClient(HttpClient client)
    {
        client.DefaultRequestHeaders.Add("Accept", "application/json, text/plain");
        client.DefaultRequestHeaders.Add("User-Agent", $"ShokoServer/{PluginManager.GetVersionInformation().Version.ToSemanticVersioningString()}");
        client.DefaultRequestHeaders.AcceptEncoding.ParseAdd("gzip");
        client.DefaultRequestHeaders.AcceptEncoding.ParseAdd("deflate");
        client.DefaultRequestHeaders.AcceptEncoding.ParseAdd("br");
    }

    /// <summary>
    ///   Sets up the handler behind the default client.
    /// </summary>
    /// <param name="handler">The handler to set up.</param>
    internal static void ConfigureDefaultHandler(SocketsHttpHandler handler)
    {
        handler.AllowAutoRedirect = true;
        handler.AutomaticDecompression = DecompressionMethods.All;
        handler.PooledConnectionLifetime = TimeSpan.FromMinutes(2);
    }

    /// <summary>
    ///   Creates a default client over the shared handler.
    /// </summary>
    /// <param name="name">The client's name; only <c>"Default"</c> is known here.</param>
    /// <returns>The client.</returns>
    /// <exception cref="InvalidOperationException">
    ///   Thrown for any other name, as the clients plugins register live in
    ///   the web host only.
    /// </exception>
    public HttpClient CreateClient(string name)
    {
        if (!string.Equals(name, DefaultClientName, StringComparison.Ordinal))
            throw new InvalidOperationException($"Only the \"{DefaultClientName}\" client is available before the web host is built, not \"{name}\".");

        var client = new HttpClient(_handler, disposeHandler: false);
        ConfigureDefaultClient(client);
        return client;
    }
}
