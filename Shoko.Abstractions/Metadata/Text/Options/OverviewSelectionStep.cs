using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Abstractions.Metadata.Text.Options;

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
