using Shoko.Abstractions.Metadata.Containers;

namespace Shoko.Abstractions.Metadata;

/// <summary>
/// Collection metadata.
/// </summary>
public interface ICollection : IMetadata<string>, IWithPrimaryImage, IWithBackdropImage, IWithLogoImage, IWithBannerImage, IWithDiscImage, IWithTitles, IWithDescriptions { }
