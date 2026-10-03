using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Providers;
using Shoko.Abstractions.Metadata.Services;

namespace Shoko.Server.Actions;

/// <summary>
///   Match the series' episodes against the first series it is linked to on
///   one metadata source, or on every source that links episodes, and save
///   the result.
/// </summary>
/// <remarks>
///   A source named must be known, link episodes and have a series linked to
///   this one.
/// </remarks>
/// <param name="providerManager">Lists the metadata providers.</param>
/// <param name="linkingService">Matches the episodes.</param>
public sealed class AutoMatchMetadataEpisodesSeriesAction(IMetadataProviderManager providerManager, IMetadataLinkingService linkingService) : SeriesAction
{
    /// <summary>
    ///   The source to match against, or <see langword="null"/> for every
    ///   source that links episodes and has a series linked to this one.
    /// </summary>
    public MetadataSource? Source { get; set; }

    /// <summary>
    ///   Keep the episode links already there and only fill the gaps. Left
    ///   off, the match replaces the links of each episode it decides on.
    /// </summary>
    public bool KeepExisting { get; set; } = true;

    public override string Name => "Auto-Match Metadata Episodes";

    public override string? Description
        => "Matches the series' episodes against the series it is linked to on one metadata source, or on every source that links episodes.";

    public override ActionPermission Permission => ActionPermission.Admin;

    public override Task<ActionValidationResult?> Validate(CancellationToken token = default)
        => Task.FromResult<ActionValidationResult?>(Source switch
        {
            null => null,
            { IsRegistered: false } => new($"\"{Source}\" is not a known metadata source."),
            _ when !LinksEpisodes(Source) => new($"{Source.Name} does not link episodes."),
            _ when LinkedSeries(Source) is null => new($"The series is not linked to a {Source.Name} series."),
            _ => null,
        });

    public override async Task Execute(CancellationToken token = default)
    {
        var sources = Source is { } source
            ? [source]
            : Series.MetadataSeriesCrossReferences.Select(link => link.Source).Distinct().Where(LinksEpisodes).ToList();
        foreach (var each in sources)
        {
            if (LinkedSeries(each) is not { } seriesID)
                continue;

            await linkingService.MatchEpisodes(Series.AnidbAnimeID, seriesID, useExisting: KeepExisting, save: true, cancellationToken: token)
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    ///   Whether an enabled provider links episodes on a source.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <returns><see langword="true"/> when one does.</returns>
    private bool LinksEpisodes(MetadataSource source)
        => providerManager.GetAvailableProviders(MetadataEntityType.Episode, source)
            .Select(info => info.Provider)
            .OfType<IMetadataSeriesLinkingProvider>()
            .Any(provider => provider.LinkableEntityTypes.Contains(MetadataEntityType.Episode));

    /// <summary>
    ///   The first series of a source the series is linked to.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <returns>The series, or <see langword="null"/> when none is linked.</returns>
    private MetadataGuid? LinkedSeries(MetadataSource source)
        => Series.GetSeriesCrossReferences(source)
            .Select(link => link.ProviderID)
            .FirstOrDefault(providerID => providerID?.EntityType == MetadataEntityType.Series);
}
