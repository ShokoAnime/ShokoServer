using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Server.Providers.AniDB.Interfaces;

namespace Shoko.Server.Providers.AniDB.HTTP;

public abstract class HttpRequest<T> : IRequest, IRequest<HttpResponse<T>, T> where T : class
{
    protected readonly ILogger Logger;
    private IHttpConnectionHandler _handler;

    protected HttpRequest(IHttpConnectionHandler handler, ILoggerFactory loggerFactory)
    {
        _handler = handler;
        Logger = loggerFactory.CreateLogger(GetType());
    }

    protected string Command { get; set; } = string.Empty;

    /// <summary>
    /// Various Parameters to add to the base command
    /// </summary>
    protected abstract string BaseCommand { get; }

    protected abstract Task<HttpResponse<T>> ParseResponse(HttpResponse<string> receivedData);

    public virtual HttpResponse<T> Send()
    {
        Command = BaseCommand.Trim();
        var rawResponse = _handler.GetHttp(Command).Result;
        var response = ParseResponse(rawResponse).Result;
        PostExecute(response);
        return response;
    }

    protected virtual void PostExecute(HttpResponse<T> response)
    {
    }

    object IRequest.Send()
    {
        return Send();
    }
}
