using System;
using System.Threading;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Shoko.Abstractions.Core;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Shoko.Server.API;

/// <summary>
///   Follows the APIs served while the server runs: tells MVC to build its
///   routes again, so the controllers of an API turned on or off appear or go
///   away at once, and keeps the core's Swagger documents in step.
/// </summary>
/// <remarks>
///   Made by the web host's container, which disposes it with the host, so a
///   host that stopped no longer listens.
/// </remarks>
internal sealed class ServedApisChangeProvider : IActionDescriptorChangeProvider, IDisposable
{
    private readonly ServedApis _servedApis;

    private readonly IServiceProvider _services;

    private readonly Lock _lock = new();

    private CancellationTokenSource _source = new();

    /// <summary>
    ///   Follows the APIs served.
    /// </summary>
    /// <param name="servedApis">The APIs served.</param>
    /// <param name="services">The web host's services.</param>
    public ServedApisChangeProvider(ServedApis servedApis, IServiceProvider services)
    {
        _servedApis = servedApis;
        _services = services;
        _servedApis.Changed += OnChanged;
    }

    /// <inheritdoc/>
    public IChangeToken GetChangeToken()
    {
        lock (_lock)
            return new CancellationChangeToken(_source.Token);
    }

    /// <inheritdoc/>
    public void Dispose()
        => _servedApis.Changed -= OnChanged;

    private void OnChanged(object? sender, CoreApis before)
    {
        // The documents first, so the rebuilt routes find them.
        if (_services.GetService<IOptions<SwaggerGeneratorOptions>>() is { } swaggerOptions)
            APIExtensions.SyncCoreSwaggerDocuments(swaggerOptions.Value.SwaggerDocs, _servedApis.Running);

        CancellationTokenSource previous;
        lock (_lock)
        {
            previous = _source;
            _source = new();
        }

        // Not disposed: a listener may still read the old token once it fired.
        previous.Cancel();
    }
}
