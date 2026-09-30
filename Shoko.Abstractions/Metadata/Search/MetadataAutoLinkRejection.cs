using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Abstractions.Metadata.Search;

/// <summary>
///   Why an auto-link candidate was not linked.
/// </summary>
/// <remarks>
///   Set by the source for a candidate it scored and turned down, or by the
///   core for one it may not link.
/// </remarks>
public sealed record MetadataAutoLinkRejection
{
    /// <summary>
    ///   The reason that decided it. Never
    ///   <see cref="MatchRejectionReason.None"/>.
    /// </summary>
    public required MatchRejectionReason Reason { get; init; }

    /// <summary>
    ///   Anything more worth showing about it, such as the query that found
    ///   it, or <see langword="null"/>.
    /// </summary>
    public string? Details { get; init; }
}
