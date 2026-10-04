using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Core.Services;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Plugin.Tmdb.Api;
using Shoko.Plugin.Tmdb.Mapping;

namespace Shoko.Plugin.Tmdb.Services;

/// <summary>
///   The little the plugin does on its own at start-up: registering the
///   image template, and once the server has started, writing TMDb's genres
///   into the tag store.
/// </summary>
/// <param name="systemService">The core's system service, for when the server has started.</param>
/// <param name="imageManager">The core's image manager, which takes the template.</param>
/// <param name="apiClient">The TMDb client, asked whether a key is configured.</param>
/// <param name="tags">Writes the genres.</param>
/// <param name="logger">The logger.</param>
public sealed class TmdbBackgroundService(
    ISystemService systemService,
    IImageManager imageManager,
    TmdbApiClient apiClient,
    TmdbTagService tags,
    ILogger<TmdbBackgroundService> logger
) : BackgroundService
{
    /// <summary>
    ///   The environment variable that points the default template at another
    ///   image server, as the core's TMDB settings could.
    /// </summary>
    internal const string ImageCdnVariable = "TMDB_IMAGE_CDN_URL";

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        RegisterTemplateUrl();
        await WaitForStart(stoppingToken).ConfigureAwait(false);
        if (!apiClient.HasApiKey)
            return;

        try
        {
            await tags.FillGenres(cancellationToken: stoppingToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Unable to store TMDb's genres; they are stored on the next search or refresh instead.");
        }
    }

    /// <summary>
    ///   Registers the default image template: TMDb's image server in the
    ///   original size, or the one the environment names. A template the
    ///   user set in the server's settings goes before it.
    /// </summary>
    /// <returns>The template registered, or <c>null</c> when the core refused it.</returns>
    public string? RegisterTemplateUrl()
    {
        var template = DefaultTemplate(Environment.GetEnvironmentVariable(ImageCdnVariable));
        try
        {
            imageManager.RegisterTemplateUrl(MetadataSource.TMDB, template);
            return template;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            logger.LogWarning(ex, "Unable to register the TMDb image template URL {Template}.", template);
            return null;
        }
    }

    /// <summary>
    ///   The default image template for a configured image server.
    /// </summary>
    /// <param name="imageCdnUrl">An image server's base URL, or a template with a <c>{0}</c>, or <c>null</c> for TMDb's own.</param>
    /// <returns>The template.</returns>
    internal static string DefaultTemplate(string? imageCdnUrl)
    {
        if (TmdbTexts.Clean(imageCdnUrl) is not { } url || !Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            return TmdbImages.Template(TmdbApiClient.DefaultImageServerUrl);

        return url.Contains("{0}", StringComparison.Ordinal) ? url : TmdbImages.Template(url);
    }

    private async Task WaitForStart(CancellationToken cancellationToken)
    {
        if (systemService.IsStarted)
            return;

        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnStarted(object? sender, EventArgs eventArgs) => started.TrySetResult();
        systemService.Started += OnStarted;
        try
        {
            if (!systemService.IsStarted)
                await started.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            systemService.Started -= OnStarted;
        }
    }
}
