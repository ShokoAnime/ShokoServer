using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Services;

namespace Shoko.Server.Actions;

/// <summary>
///   Link and download again the images of everything the series is linked
///   to, from every plugin provider that supplies images or from one source.
/// </summary>
/// <remarks>
///   TMDB has image actions of its own.
/// </remarks>
public sealed class DownloadLinkedMetadataImagesSeriesAction(IMetadataRefreshService refreshService) : SeriesAction
{
    /// <summary>
    ///   The source to download from, or <see langword="null"/> for every
    ///   enabled plugin provider that supplies images.
    /// </summary>
    public MetadataSource? Source { get; set; }

    public override string Name => "Download Linked Metadata Images - Force";

    public override string? Description => "Links and downloads again the images of everything the series is linked to, from the plugin metadata providers.";

    public override ActionCategory Category => ActionCategory.Images;

    public override ActionPermission Permission => ActionPermission.Admin;

    public override Task Execute(CancellationToken token = default)
        => refreshService.DownloadImagesForAnime(Series.AnidbAnimeID, Source, force: true, token);
}
