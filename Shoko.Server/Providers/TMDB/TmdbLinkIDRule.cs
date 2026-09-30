using Shoko.Abstractions.Metadata;
using Shoko.Server.Services;

namespace Shoko.Server.Providers.TMDB;

/// <summary>
///   TMDB numbers its shows, movies and episodes from 1, so a link to any
///   other ID, a 0 above all, names nothing on TMDB.
/// </summary>
/// <remarks>
///   A season is left alone: an alternate ordering's season is an episode
///   group, whose ID is not a number.
/// </remarks>
public sealed class TmdbLinkIDRule : IMetadataLinkIDRule
{
    /// <inheritdoc />
    public MetadataSource Source => MetadataSource.TMDB;

    /// <inheritdoc />
    public bool IsValid(MetadataGuid entry)
        => entry.EntityType == MetadataEntityType.Season || (entry.TryGetNumericID<int>(out var id) && id > 0);
}
