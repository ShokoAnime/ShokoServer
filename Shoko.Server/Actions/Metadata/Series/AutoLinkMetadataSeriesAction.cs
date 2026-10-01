using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Server.Services;

namespace Shoko.Server.Actions;

/// <summary>
///   Ask a source's auto-linker to work out what the series is and link it,
///   for one source or for every source with an enabled auto-linker, TMDB
///   included.
/// </summary>
/// <remarks>
///   A person asks for it, so an auto-linker counts even while it does not
///   auto-link by itself. An unconfigured source is skipped.
/// </remarks>
/// <param name="providerManager">Lists the metadata providers.</param>
/// <param name="providerScheduler">Schedules the searches.</param>
public sealed class AutoLinkMetadataSeriesAction(IMetadataProviderManager providerManager, MetadataProviderScheduler providerScheduler) : SeriesAction
{
    /// <summary>
    ///   The source to search, or <see langword="null"/> for every source
    ///   with an enabled auto-linker.
    /// </summary>
    public MetadataSource? Source { get; set; }

    /// <summary>
    ///   Also search the sources the series is left alone on or already
    ///   linked on, and replace every link it has there, verified ones and
    ///   episode links included, with what is found. Left off, those
    ///   sources are skipped and no link is replaced.
    /// </summary>
    public bool Force { get; set; }

    public override string Name => "Auto-Search Metadata Links";

    public override string? Description => "Searches one metadata source, or every source with an auto-linker, TMDB included, for the series and links what it finds. "
        + "Skips the sources the series is left alone on or already linked on; forced, searches those too and replaces every link the series has there, verified ones included.";

    public override ActionPermission Permission => ActionPermission.User;

    public override async Task Execute(CancellationToken token = default)
    {
        var sources = Source is { }
            ? [Source]
            : providerManager.GetAutoLinkers(onRequest: true).Select(info => info.Source).ToList();
        foreach (var source in sources)
        {
            // The job is forced so a source that does not auto-link is still searched, which skips its
            // left-alone check, so that is made here; unless replacing, the job still skips a linked source.
            if (!Force && Series.IsAutoLinkingDisabled(source))
                continue;

            await providerScheduler.ScheduleSearch(source, Series.AnidbAnimeID, force: true, replace: Force, token).ConfigureAwait(false);
        }
    }
}
