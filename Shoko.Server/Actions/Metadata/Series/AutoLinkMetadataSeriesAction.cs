using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Services;

namespace Shoko.Server.Actions;

/// <summary>
///   Ask a source's auto-linker to work out what the series is and link it,
///   for one source or for every plugin source with an auto-linker.
/// </summary>
public sealed class AutoLinkMetadataSeriesAction(IMetadataProviderManager providerManager, IMetadataRefreshService refreshService) : SeriesAction
{
    /// <summary>
    ///   The source to search, or <see langword="null"/> for every plugin
    ///   source with an auto-linker.
    /// </summary>
    public MetadataSource? Source { get; set; }

    public override string Name => "Auto-Search Metadata Links";

    public override string? Description => "Searches a metadata source for the series and links what it finds, even where the series is left alone.";

    public override ActionPermission Permission => ActionPermission.User;

    public override async Task Execute(CancellationToken token = default)
    {
        var sources = Source is { } source
            ? [source]
            : providerManager.MetadataProviders
                .Where(info => !info.Source.IsCore)
                .Where(info => info.IsAutoLinker)
                .Select(info => info.Source)
                .Distinct()
                .ToList();
        foreach (var each in sources)
            await refreshService.AutoSearch(each, Series.AnidbAnimeID, force: true, token).ConfigureAwait(false);
    }
}
