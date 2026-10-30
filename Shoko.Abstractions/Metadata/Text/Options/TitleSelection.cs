using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Services;

namespace Shoko.Abstractions.Metadata.Text.Options;

/// <summary>
///   How <see cref="IMetadataTextManager.ChooseTitle"/> picks a title out of
///   candidates: steps walked in order, the first one a title answers wins.
///   <see cref="IMetadataTextManager.GetTitleSelection"/> builds one from the
///   settings, to use as is or to change.
/// </summary>
public sealed record TitleSelection
{
    /// <summary>
    ///   The steps, best first.
    /// </summary>
    public required IReadOnlyList<TitleSelectionStep> Steps { get; init; }

    /// <summary>
    ///   Whether every step is first walked for real titles only, and again
    ///   for generic ones such as <c>Episode 5</c>, as for episodes, so a real
    ///   title in a later step beats a generic one in an earlier step.
    /// </summary>
    public bool RankGenericTitlesLast { get; init; }

    /// <summary>
    ///   Whether a user's picks count: a title a user prefers overall wins
    ///   outright, and one a user prefers for a language wins before the
    ///   first step in that language. Defaults to <c>true</c>.
    /// </summary>
    public bool UsePreferences { get; init; } = true;
}
