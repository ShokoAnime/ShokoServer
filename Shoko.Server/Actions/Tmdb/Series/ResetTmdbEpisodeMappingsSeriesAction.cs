using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Services;

namespace Shoko.Server.Actions;

/// <summary>
///   Reset all TMDB episode mappings for the series.
/// </summary>
public sealed class ResetTmdbEpisodeMappingsSeriesAction(IMetadataLinkingService linkingService) : SeriesAction
{
    public override string Name => "Reset TMDB Episode Mappings";

    public override string? Description => "Reset all TMDB episode mappings for the series.";

    public override ActionCategory Category => ActionCategory.TMDB;

    public override ActionPermission Permission => ActionPermission.Admin;

    public override bool RequiresConfirmation => true;

    public override string? ConfirmationMessage => "Are you sure you want to reset all TMDB episode mappings for this series?";

    public override async Task Execute(CancellationToken token = default)
        => await linkingService.ResetEpisodeLinks(MetadataSource.TMDB, Series.AnidbAnimeID, allowAutoMatch: true, token).ConfigureAwait(false);
}
