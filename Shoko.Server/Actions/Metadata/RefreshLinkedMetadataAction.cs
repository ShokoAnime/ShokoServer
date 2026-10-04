using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Providers;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Utilities;

namespace Shoko.Server.Actions;

/// <summary>
///   Refresh every series and film linked from one metadata source, or from
///   every source with an enabled provider, and the stored collections.
/// </summary>
/// <remarks>
///   Only queues the refreshes, which run on their own; the progress covers
///   the queuing. A source named must be known and have an enabled provider.
///   <see cref="RefreshAllLinkedMetadataAction"/> is the scheduled form.
/// </remarks>
/// <param name="providerManager">Lists the metadata providers.</param>
/// <param name="refreshService">Queues the refreshes.</param>
public sealed class RefreshLinkedMetadataAction(
    IMetadataProviderManager providerManager,
    IMetadataRefreshService refreshService
) : IExecutableAction, IProgressReportingAction
{
    private IProgress<decimal>? _progress;

    /// <summary>
    ///   The source to refresh, or <c>null</c> for every source
    ///   with an enabled provider.
    /// </summary>
    public MetadataSource? Source { get; set; }

    /// <summary>
    ///   Refresh only series, only movies or only collections, or
    ///   <c>null</c> for all three.
    /// </summary>
    public MetadataEntityType? EntityType { get; set; }

    /// <summary>
    ///   Refresh every entry however recently it was refreshed. Left off, an
    ///   entry refreshed within the last hour is skipped.
    /// </summary>
    public bool Force { get; set; }

    /// <summary>
    ///   Download the images of the refreshed entries too.
    /// </summary>
    public bool DownloadImages { get; set; } = true;

    public string Name => "Refresh All Linked Metadata";

    public string? Description
        => "Refreshes every series and film linked from one metadata source, or from every source, and the stored collections, "
            + "with or without their images.";

    public ActionCategory Category => ActionCategory.Maintenance;

    public ActionPermission Permission => ActionPermission.Admin;

    void IProgressReportingAction.SetProgress(IProgress<decimal> progress)
        => _progress = progress;

    public Task<ActionValidationResult?> Validate(CancellationToken token = default)
        => Task.FromResult(MetadataPurges.CheckKind(EntityType) ?? CheckSource());

    public async Task Execute(CancellationToken token = default)
    {
        var sources = Source is { } source ? [source] : EnabledSources();
        var options = new MetadataRefreshOptions { DownloadImages = DownloadImages, Reason = MetadataRefreshReason.Requested };
        var stages = new StagedProgress(_progress, Math.Max(sources.Count, 1));
        stages.Report(0);
        foreach (var each in sources)
        {
            await refreshService.RefreshAllLinked(each, Force, options, EntityType, stages, token).ConfigureAwait(false);
            stages.NextStage();
        }

        stages.Complete();
    }

    /// <summary>
    ///   The sources with an enabled metadata provider.
    /// </summary>
    /// <returns>The sources.</returns>
    private IReadOnlyList<MetadataSource> EnabledSources()
        => providerManager.MetadataProviders.Where(info => info.Enabled).Select(info => info.Source).Distinct().ToList();

    /// <summary>
    ///   Refuses a source that is not known or has no enabled provider.
    /// </summary>
    /// <returns>Why the source is refused, or <c>null</c> to allow it.</returns>
    private ActionValidationResult? CheckSource()
        => Source switch
        {
            null => null,
            { IsRegistered: false } => new($"\"{Source}\" is not a known metadata source."),
            _ when !EnabledSources().Contains(Source) => new($"{Source.Name} has no enabled metadata provider."),
            _ => null,
        };
}
