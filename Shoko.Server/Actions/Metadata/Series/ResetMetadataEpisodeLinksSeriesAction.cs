using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Services;

namespace Shoko.Server.Actions;

/// <summary>
///   Remove every episode link of the series to one metadata source,
///   leaving them for matching to fill in again.
/// </summary>
/// <remarks>
///   Links made by hand go too. The series links are kept. A source must be
///   named, and be known.
/// </remarks>
/// <param name="linkingService">Removes the links.</param>
public sealed class ResetMetadataEpisodeLinksSeriesAction(IMetadataLinkingService linkingService) : SeriesAction
{
    /// <summary>
    ///   The source whose episode links to remove.
    /// </summary>
    public MetadataSource? Source { get; set; }

    public override string Name => "Reset Metadata Episode Links";

    public override string? Description
        => "Removes every episode link of the series to one metadata source, links made by hand included, for matching to fill in again.";

    public override ActionCategory Category => ActionCategory.Destructive;

    public override ActionPermission Permission => ActionPermission.Admin;

    public override bool RequiresConfirmation => true;

    public override string? ConfirmationMessage => "Are you sure you want to reset every episode link of this series to the chosen metadata source?";

    public override Task<ActionValidationResult?> Validate(CancellationToken token = default)
        => Task.FromResult<ActionValidationResult?>(Source switch
        {
            null => new("Choose the metadata source whose episode links to reset."),
            { IsRegistered: false } => new($"\"{Source}\" is not a known metadata source."),
            _ => null,
        });

    public override Task Execute(CancellationToken token = default)
        => Source is { } source
            ? linkingService.ResetEpisodeLinks(source, Series.AnidbAnimeID, allowAutoMatch: true, token)
            : Task.CompletedTask;
}
