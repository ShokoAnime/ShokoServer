using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Services;

namespace Shoko.Abstractions.Metadata.Text.Options;

/// <summary>
///   How <see cref="IMetadataTextManager.ChooseOverview"/> picks an overview
///   out of candidates: steps walked in order, the first one an overview
///   answers wins. <see cref="IMetadataTextManager.GetOverviewSelection"/>
///   builds one from the settings, to use as is or to change.
/// </summary>
public sealed record OverviewSelection
{
    /// <summary>
    ///   The steps, best first.
    /// </summary>
    public required IReadOnlyList<OverviewSelectionStep> Steps { get; init; }

    /// <summary>
    ///   Whether a user's picks count: an overview a user prefers overall wins
    ///   outright, and one a user prefers for a language wins before the
    ///   first step in that language. Defaults to <c>true</c>.
    /// </summary>
    public bool UsePreferences { get; init; } = true;
}

/// <summary>
///   One step of an <see cref="OverviewSelection"/>: a source's first
///   overview in a language.
/// </summary>
public sealed record OverviewSelectionStep
{
    /// <summary>
    ///   The source to read, or <c>null</c> for any source.
    /// </summary>
    public MetadataSource? Source { get; init; }

    /// <summary>
    ///   The language wanted.
    /// </summary>
    public required TitleLanguage Language { get; init; }
}
