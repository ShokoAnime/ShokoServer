using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Server.Providers.AniDB.Interfaces;

namespace Shoko.Server.Providers.AniDB.HTTP;

public abstract class HttpRequest<T> : IRequest, IRequest<HttpResponse<T>> where T : class
{
    protected readonly ILogger Logger;

    private readonly IHttpConnectionHandler _handler;

    protected HttpRequest(IHttpConnectionHandler handler, ILoggerFactory loggerFactory)
    {
        _handler = handler;
        Logger = loggerFactory.CreateLogger(GetType());
    }

    protected string Command { get; set; } = string.Empty;

    public virtual bool Force { get; set; } = false;

    /// <summary>
    /// Various Parameters to add to the base command
    /// </summary>
    protected abstract string BaseCommand { get; }

    protected abstract Task<HttpResponse<T>> ParseResponse(HttpResponse<string> receivedData);

    public virtual async Task<HttpResponse<T>> SendAsync(CancellationToken cancellationToken = default)
    {
        Command = BaseCommand.Trim();
        var rawResponse = await _handler.GetHttp(Command, Force, cancellationToken);
        var response = await ParseResponse(rawResponse);
        PostExecute(response);
        return response;
    }

    protected virtual void PostExecute(HttpResponse<T> response)
    {
    }
}
