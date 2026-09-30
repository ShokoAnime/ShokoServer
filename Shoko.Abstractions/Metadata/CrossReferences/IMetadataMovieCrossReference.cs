using Shoko.Abstractions.Metadata.Shoko;

namespace Shoko.Abstractions.Metadata.CrossReferences;

/// <summary>
/// A cross-reference keyed on a Shoko episode that stands for a whole film: the
/// anime is the film, and the episode is where the link is kept.
/// </summary>
/// <remarks>
/// Names both AniDB ends, since a film is claimed at both, and no
/// provider-side parent, since a film sits in nothing.
/// </remarks>
public interface IMetadataMovieCrossReference : IMetadataCrossReference
{
    /// <summary>
    /// AniDB episode id of the linked entry.
    /// </summary>
    int AnidbEpisodeID { get; }

    /// <summary>
    /// The linked episode, if it is in the collection.
    /// </summary>
    IShokoEpisode? ShokoEpisode { get; }
}

/// <summary>
/// A film cross-reference with its provider entry typed.
/// </summary>
/// <typeparam name="TProvider">The provider entry's type.</typeparam>
public interface IMetadataMovieCrossReference<out TProvider> : IMetadataMovieCrossReference, IMetadataCrossReference<TProvider>
    where TProvider : IMetadata;
