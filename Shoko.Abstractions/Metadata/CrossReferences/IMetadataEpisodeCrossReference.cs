using Shoko.Abstractions.Metadata.Shoko;

namespace Shoko.Abstractions.Metadata.CrossReferences;

/// <summary>
/// A cross-reference keyed on a Shoko episode: one episode is the same work as
/// an episode of the provider's show. An episode standing for a whole film is
/// linked through <see cref="IMetadataMovieCrossReference"/> instead.
/// </summary>
public interface IMetadataEpisodeCrossReference : IMetadataCrossReference
{
    /// <summary>
    /// AniDB episode id of the linked entry.
    /// </summary>
    int AnidbEpisodeID { get; }

    /// <summary>
    ///   The provider series the linked episode belongs to, or <c>null</c>
    ///   when it is not known or the episode is linked to nothing.
    /// </summary>
    MetadataGuid? ProviderParentID { get; }

    /// <summary>
    ///   The provider season the linked episode sits in, or <c>null</c> when
    ///   it is not known or the episode is linked to nothing.
    /// </summary>
    /// <remarks>
    ///   Kept on the link, so it reads without the source cached; season links
    ///   are worked out from it.
    /// </remarks>
    MetadataGuid? SeasonID { get; }

    /// <summary>
    ///   The number of the season the linked episode sits in, or <c>null</c>
    ///   when it is not known.
    /// </summary>
    int? SeasonNumber { get; }

    /// <summary>
    ///   The linked episode's number within its season, or <c>null</c> when
    ///   it is not known.
    /// </summary>
    int? EpisodeNumber { get; }

    /// <summary>
    /// The linked episode, if it is in the collection.
    /// </summary>
    IShokoEpisode? ShokoEpisode { get; }
}

/// <summary>
/// An episode cross-reference with its provider entry typed.
/// </summary>
/// <typeparam name="TProvider">The provider entry's type.</typeparam>
public interface IMetadataEpisodeCrossReference<out TProvider> : IMetadataEpisodeCrossReference, IMetadataCrossReference<TProvider>
    where TProvider : IMetadata;
