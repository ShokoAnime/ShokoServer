using System.Collections.Generic;
using Moq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Image.CrossReferences;
using Shoko.Abstractions.Metadata.Image.Options;
using Xunit;

namespace Shoko.Tests.Metadata;

/// <summary>
/// Covers how an entry lists the images its source names as its defaults.
/// </summary>
public class DefaultImageTests
{
    private sealed class WithImages : IWithImages
    {
        public MetadataGuid ID { get; } = new(MetadataSource.AniDB, MetadataEntityType.Series, "1");

        public IImageCrossReference? DefaultPrimaryImageCrossReference { get; init; }

        public IImageCrossReference? DefaultBackdropImageCrossReference { get; init; }

        // No image is preferred, so only the defaults can be listed.
        public IReadOnlyList<IImageCrossReference> GetImageCrossReferences(ImageCrossReferenceFilteringOptions? options = null) => [];
    }

    [Fact]
    public void TheDefaultImagesAreListedEvenWhenNoneIsPreferred()
    {
        var primary = Mock.Of<IImageCrossReference>();
        var backdrop = Mock.Of<IImageCrossReference>();
        IWithImages entry = new WithImages { DefaultPrimaryImageCrossReference = primary, DefaultBackdropImageCrossReference = backdrop };

        Assert.Equal([primary, backdrop], entry.GetDefaultImageCrossReferences());
    }
}
