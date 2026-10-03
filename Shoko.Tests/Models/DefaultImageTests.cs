using System;
using Moq;
using Shoko.Abstractions.Metadata.Image.CrossReferences;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Server.Models.Metadata;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Models;

/// <summary>
/// Covers how a stored entry's default image of a type is picked from its
/// links: the image its source pinned, else the first link.
/// </summary>
public class DefaultImageTests
{
    private static IImageCrossReference Link(string resourceID, int ordering)
    {
        var xref = new Mock<IImageCrossReference>();
        xref.SetupGet(x => x.ImageID).Returns(IImageManager.GetIDForImageSourceAndResourceID(TestSources.Plugin, resourceID));
        xref.SetupGet(x => x.Ordering).Returns(ordering);
        return xref.Object;
    }

    [Fact]
    public void ThePinnedImageIsTheDefaultWhereverItIsLinked()
    {
        var pinned = Link("pinned.jpg", 2);

        Assert.Same(pinned, MetadataStoredEntry.DefaultOf([Link("first.jpg", 0), pinned, Link("second.jpg", 1)], TestSources.Plugin, "pinned.jpg"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("gone.jpg")]
    public void WithoutAPinnedLinkTheFirstOneIsTheDefault(string? resourceID)
    {
        var first = Link("first.jpg", 0);

        Assert.Same(first, MetadataStoredEntry.DefaultOf([Link("second.jpg", 1), first], TestSources.Plugin, resourceID));
        Assert.Null(MetadataStoredEntry.DefaultOf(Array.Empty<IImageCrossReference>(), TestSources.Plugin, resourceID));
    }
}
