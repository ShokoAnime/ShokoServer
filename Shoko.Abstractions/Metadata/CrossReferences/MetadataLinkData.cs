using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Abstractions.Metadata.CrossReferences;

/// <summary>
///   What every link to store carries, whichever level it is made at. Where it
///   sits among an entry's links is the store's to assign.
/// </summary>
public abstract record MetadataLinkData
{
    /// <summary>
    ///   The source the linked entry belongs to.
    /// </summary>
    public required MetadataSource Source { get; init; }

    /// <summary>
    ///   The AniDB anime, which every link belongs to whatever its level.
    /// </summary>
    public required int AnidbAnimeID { get; init; }

    /// <summary>
    ///   The entry on <see cref="Source"/>, of the kind the link's level
    ///   names. Only an episode link may leave it <c>null</c>, to mark the
    ///   AniDB episode as deliberately not on your source, so matching leaves
    ///   it alone; a series or film link must name an entry.
    /// </summary>
    public required MetadataGuid? ProviderID { get; init; }

    /// <summary>
    ///   How the link was arrived at. A manual one is
    ///   <see cref="Enums.MatchRating.UserVerified"/>.
    /// </summary>
    public MatchRating MatchRating { get; init; } = MatchRating.UserVerified;
}
