using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shoko.Abstractions.Core.Services;
using Shoko.Server.API.FileProviders;
using Shoko.Server.Services;

namespace Shoko.Server.API;

/// <summary>
///   Serves the active web UI, for every request no route took: the files
///   from its mount path, a redirect from <c>/</c> to it, a redirect from
///   the Shoko Web UI's path when another web UI is served elsewhere, and a
///   redirect into it for a page outside it that nothing else answered.
///   Follows <see cref="WebUIService"/> as it changes, without a restart.
/// </summary>
/// <param name="_next">The next middleware.</param>
/// <param name="_environment">The host environment, for the static file middleware.</param>
/// <param name="_loggerFactory">The logger factory, for the static file middleware.</param>
/// <param name="_webUIService">The web UI service.</param>
/// <param name="_systemService">The system service, for whether the server has started.</param>
public sealed class WebUIMiddleware(
    RequestDelegate _next,
    IWebHostEnvironment _environment,
    ILoggerFactory _loggerFactory,
    WebUIService _webUIService,
    ISystemService _systemService
)
{
    /// <summary>
    ///   The static file middleware for one serving state, swapped whole when
    ///   the state changes, so a request never mixes two of them.
    /// </summary>
    /// <param name="State">The serving state it was made for.</param>
    /// <param name="ServeFile">Serves a file of the active web UI, or passes the request on.</param>
    private sealed record Snapshot(WebUIService.ServingState State, RequestDelegate ServeFile);

    private Snapshot? _snapshot;

    /// <summary>
    ///   Answers the request with the active web UI, or passes it on.
    /// </summary>
    /// <param name="context">The request.</param>
    /// <returns>A task that completes when the request is answered.</returns>
    public Task Invoke(HttpContext context)
    {
        var request = context.Request;
        if (context.GetEndpoint() is not null || !(HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method)) || IsReservedPath(request.Path))
            return _next(context);

        var state = _webUIService.GetState();
        if (!state.IsEnabled)
            return _next(context);

        var snapshot = _snapshot;
        if (snapshot is null || !ReferenceEquals(snapshot.State, state))
            _snapshot = snapshot = new(state, CreateFileServer(state));

        if (state.MountPath is not "/" && request.Path.Value is null or "" or "/")
        {
            context.Response.Redirect(state.MountPath);
            return Task.CompletedTask;
        }

        // Old links to the Shoko Web UI's path lead to the web UI served elsewhere.
        if (state.MountPath != CoreWebUIProvider.WebUIPath &&
            !state.MountPath.StartsWith(CoreWebUIProvider.WebUIPath + "/", StringComparison.OrdinalIgnoreCase) &&
            request.Path.StartsWithSegments(CoreWebUIProvider.WebUIPath, StringComparison.OrdinalIgnoreCase))
        {
            context.Response.Redirect(state.MountPath);
            return Task.CompletedTask;
        }

        if (state.MountPath is "/" || IsUnderMountPath(request.Path, state.MountPath) || !AcceptsHtml(request))
            return snapshot.ServeFile(context);

        // Until the server has started, nothing past this answers a page, and the web UI is where
        // setup and a failed start are dealt with.
        if (!_systemService.IsStarted)
        {
            context.Response.Redirect(IntoMountPath(request, state.MountPath));
            return Task.CompletedTask;
        }

        return ServeOrRedirectAsync(context, snapshot, state.MountPath);
    }

    /// <summary>
    ///   Lets the rest of the pipeline answer a page outside the mount path,
    ///   plugin middleware included, and sends a browser that got nothing but
    ///   a <c>404</c> into the web UI at the same path, where its own routes
    ///   decide.
    /// </summary>
    /// <param name="context">The request.</param>
    /// <param name="snapshot">The current snapshot.</param>
    /// <param name="mountPath">The web UI's mount path.</param>
    /// <returns>A task that completes when the request is answered.</returns>
    private static async Task ServeOrRedirectAsync(HttpContext context, Snapshot snapshot, string mountPath)
    {
        await snapshot.ServeFile(context);
        if (context.Response.StatusCode is not StatusCodes.Status404NotFound || context.Response.HasStarted)
            return;

        context.Response.Redirect(IntoMountPath(context.Request, mountPath));
    }

    /// <summary>
    ///   The same path and query under the mount path.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="mountPath">The web UI's mount path.</param>
    /// <returns>The location to redirect to.</returns>
    private static string IntoMountPath(HttpRequest request, string mountPath)
        => new PathString(mountPath).Add(request.Path) + request.QueryString;

    /// <summary>
    ///   Makes the static file middleware serving a state's web UI from its
    ///   mount path. A file it does not have passes the request on.
    /// </summary>
    /// <param name="state">The serving state.</param>
    /// <returns>The middleware, as a request delegate.</returns>
    private RequestDelegate CreateFileServer(WebUIService.ServingState state)
    {
        var options = new StaticFileOptions
        {
            FileProvider = state.Active.Provider,
            RequestPath = state.MountPath is "/" ? PathString.Empty : new PathString(state.MountPath),
            ServeUnknownFileTypes = true,
            DefaultContentType = "text/html",
            OnPrepareResponse = ctx =>
            {
                // The index has no hash in its name, so it is never cached.
                if (string.Equals(ctx.File.Name, "index.html", StringComparison.OrdinalIgnoreCase))
                {
                    ctx.Context.Response.Headers.Append("Cache-Control", "no-cache, no-store, must-revalidate");
                    ctx.Context.Response.Headers.Append("Expires", "0");
                }
            },
        };
        return new StaticFileMiddleware(_next, _environment, Options.Create(options), _loggerFactory).Invoke;
    }

    /// <summary>
    ///   Checks if a path is the mount path or under it.
    /// </summary>
    /// <param name="path">The request's path.</param>
    /// <param name="mountPath">The web UI's mount path.</param>
    /// <returns><c>true</c> if the web UI's files answer the path.</returns>
    private static bool IsUnderMountPath(PathString path, string mountPath)
        => path.StartsWithSegments(mountPath, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    ///   Checks if a request is a browser's navigation, which asks for HTML,
    ///   so a script or an API client keeps getting its plain <c>404</c>.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <returns><c>true</c> if the request accepts <c>text/html</c>.</returns>
    private static bool AcceptsHtml(HttpRequest request)
        => request.Headers.Accept.Any(value => value?.Contains("text/html", StringComparison.OrdinalIgnoreCase) is true);

    /// <summary>
    ///   Checks if a request is for a path no web UI may answer, which stays
    ///   clear even for a web UI at the root, for the routes only the legacy
    ///   MVC router knows.
    /// </summary>
    /// <param name="path">The request's path.</param>
    /// <returns><c>true</c> if the path is under <c>/api</c>, <c>/plugin</c> or <c>/signalr</c>.</returns>
    private static bool IsReservedPath(PathString path)
        => path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWithSegments("/plugin", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWithSegments("/signalr", StringComparison.OrdinalIgnoreCase);
}
