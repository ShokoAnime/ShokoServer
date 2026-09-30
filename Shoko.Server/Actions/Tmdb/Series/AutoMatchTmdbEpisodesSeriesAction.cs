using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Services;

namespace Shoko.Server.Actions;

/// <summary>
///   Automatically match TMDB episodes for the series.
/// </summary>
public sealed class AutoMatchTmdbEpisodesSeriesAction(IMetadataLinkingService linkingService) : SeriesAction
{
    public override string Name => "Auto-Match TMDB Episodes";

    public override string? Description => "Automatically matches Shoko episodes with TMDB episodes.";

    public override ActionCategory Category => ActionCategory.TMDB;

    public override ActionPermission Permission => ActionPermission.Admin;

    public override async Task Execute(CancellationToken token = default)
    {
        var showID = Series.GetSeriesCrossReferences(MetadataSource.TMDB)
            .Select(xref => xref.ProviderID)
            .FirstOrDefault(providerID => providerID?.EntityType == MetadataEntityType.Series);
        if (showID is null)
            return;

        await linkingService.MatchEpisodes(Series.AnidbAnimeID, showID, useExisting: true, save: true, cancellationToken: token).ConfigureAwait(false);
    }
}
