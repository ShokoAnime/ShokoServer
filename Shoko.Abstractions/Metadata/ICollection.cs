using System;
using Shoko.Abstractions.Metadata.Containers;

namespace Shoko.Abstractions.Metadata;

/// <summary>
/// Collection metadata.
/// </summary>
public interface ICollection : IMetadata, IWithPrimaryImage, IWithBackdropImage, IWithLogoImage, IWithBannerImage, IWithDiscImage, IWithTitles, IWithOverviews, IWithCreationDate, IWithUpdateDate
{
    /// <summary>
    ///   When the core last refreshed the collection in full from its source
    ///   without failing, whether or not anything changed, in UTC. Set by the
    ///   core alone; <see langword="null"/> when it never was.
    /// </summary>
    DateTime? LastRefreshedAt { get; }
}
