using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.ScheduledActions;

namespace Shoko.Server.Actions;

/// <summary>
///   Purge the stored series and films of the plugin sources that nothing
///   links to any more, and the collections none of whose members anything
///   links to.
/// </summary>
/// <remarks>
///   TMDB has purge actions of its own.
/// </remarks>
public sealed class PurgeAllUnusedMetadataAction(IMetadataProviderManager providerManager, IMetadataPurgeService purgeService) : IScheduledAction
{
    public string Name => "Purge Unused Metadata";

    public string? Description => "Removes the stored series, films and collections of the plugin metadata sources that are not linked to any AniDB anime.";

    public ActionCategory Category => ActionCategory.Destructive;

    public bool RequiresConfirmation => true;

    public string? ConfirmationMessage => "Are you sure you want to remove all unlinked series, films and collections of the plugin metadata sources?";

    public async Task Execute(IProgress<decimal> progress, CancellationToken token)
    {
        var sources = providerManager.MetadataProviders
            .Where(info => !info.Source.IsCore)
            .Select(info => info.Source)
            .Distinct()
            .ToList();
        foreach (var each in sources)
            await purgeService.PurgeUnused(each, cancellationToken: token).ConfigureAwait(false);
    }
}
