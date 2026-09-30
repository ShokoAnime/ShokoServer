using System;
using Moq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Image;
using Shoko.Abstractions.Metadata.Image.CrossReferences;
using Shoko.Abstractions.Metadata.Image.Exceptions;
using Shoko.Abstractions.Metadata.Image.Options;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Server.API.v3.Helpers;
using Xunit;

namespace Shoko.Tests.API;

/// <summary>
/// Covers the image routes of the Shoko entities: an upload can make its image the preferred one, and an
/// image can be enabled or disabled for the entity it is shown for.
/// </summary>
public class EntityImageRouteTests
{
    #region Helpers

    private static readonly Guid _imageID = Guid.Parse("0a000000-0000-0000-0000-000000000000");

    private static readonly Guid _otherImageID = Guid.Parse("0b000000-0000-0000-0000-000000000000");

    private static IImage Image(Guid id)
        => Mock.Of<IImage>(image => image.ID == id);

    private static IImageCrossReference Xref(Guid imageID, bool enabled = true)
        => Mock.Of<IImageCrossReference>(xref => xref.ImageID == imageID && xref.IsEnabled == enabled && xref.ImageType == ImageEntityType.Primary);

    #endregion

    #region Upload

    [Fact]
    public void AnUploadIsLinkedAsTheUsersOwnEnabledImage()
    {
        var entity = Mock.Of<IWithImages>();
        var image = Image(_imageID);
        var xref = Xref(_imageID);
        var manager = new Mock<IImageManager>(MockBehavior.Strict);
        manager.Setup(m => m.AddImageCrossReference(entity, image, It.Is<ImageCrossReferenceData>(data =>
            data.ImageType == ImageEntityType.Primary && data.IsEnabled && data.IsDesired && !data.IsPreferred && data.Source == MetadataSource.User
        ))).Returns(xref);

        var (linkedImage, linked, created) = manager.Object.LinkUploadedImage(entity, image, ImageEntityType.Primary, preferred: false);

        Assert.Same(image, linkedImage);
        Assert.Same(xref, linked);
        Assert.True(created);
    }

    [Fact]
    public void AnUploadCanBecomeThePreferredImage()
    {
        var entity = Mock.Of<IWithImages>();
        var image = Image(_imageID);
        var xref = Xref(_imageID);
        var preferred = Xref(_imageID);
        var manager = new Mock<IImageManager>(MockBehavior.Strict);
        manager.Setup(m => m.AddImageCrossReference(entity, image, It.IsAny<ImageCrossReferenceData>())).Returns(xref);
        manager.Setup(m => m.SetPreferredImageForEntity(xref)).Returns(preferred);

        var (_, linked, created) = manager.Object.LinkUploadedImage(entity, image, ImageEntityType.Primary, preferred: true);

        Assert.Same(preferred, linked);
        Assert.True(created);
        manager.Verify(m => m.SetPreferredImageForEntity(xref), Times.Once);
    }

    [Fact]
    public void AnUploadAlreadyLinkedKeepsItsLinkAndCanStillBecomePreferred()
    {
        var entity = Mock.Of<IWithImages>();
        var uploaded = Image(_imageID);
        var stored = Image(_imageID);
        var existing = Xref(_imageID);
        var preferred = Xref(_imageID);
        var manager = new Mock<IImageManager>(MockBehavior.Strict);
        manager.Setup(m => m.AddImageCrossReference(entity, uploaded, It.IsAny<ImageCrossReferenceData>()))
            .Throws(new ImageCrossReferenceExistsException { CrossReference = existing, Image = stored, Entity = entity });
        manager.Setup(m => m.SetPreferredImageForEntity(existing)).Returns(preferred);

        var (linkedImage, linked, created) = manager.Object.LinkUploadedImage(entity, uploaded, ImageEntityType.Primary, preferred: true);

        Assert.Same(stored, linkedImage);
        Assert.Same(preferred, linked);
        Assert.False(created);
    }

    #endregion

    #region Enabled

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EveryLinkTheEntitySeesTheImageThroughIsChanged(bool enabled)
    {
        var entity = Mock.Of<IWithImages>();
        var image = Image(_imageID);
        var own = Xref(_imageID, !enabled);
        var linked = Xref(_imageID, !enabled);
        var alreadySet = Xref(_imageID, enabled);
        var other = Xref(_otherImageID, !enabled);
        var manager = new Mock<IImageManager>(MockBehavior.Strict);
        manager.Setup(m => m.GetImageCrossReferencesForEntity(entity, It.Is<ImageCrossReferenceFilteringOptions>(options => options.ImageType == ImageEntityType.Primary)))
            .Returns([own, other, linked, alreadySet]);
        manager.Setup(m => m.UpdateImageCrossReference(It.IsAny<IImageCrossReference>(), It.Is<ImageCrossReferenceUpdateData>(data => data.IsEnabled == enabled)))
            .Returns<IImageCrossReference, ImageCrossReferenceUpdateData>((xref, _) => xref);

        var updated = manager.Object.SetImageEnabledForEntity(entity, ImageEntityType.Primary, image, enabled);

        Assert.Equal([own, linked, alreadySet], updated);
        manager.Verify(m => m.UpdateImageCrossReference(own, It.IsAny<ImageCrossReferenceUpdateData>()), Times.Once);
        manager.Verify(m => m.UpdateImageCrossReference(linked, It.IsAny<ImageCrossReferenceUpdateData>()), Times.Once);
        manager.Verify(m => m.UpdateImageCrossReference(alreadySet, It.IsAny<ImageCrossReferenceUpdateData>()), Times.Never);
        manager.Verify(m => m.UpdateImageCrossReference(other, It.IsAny<ImageCrossReferenceUpdateData>()), Times.Never);
    }

    [Fact]
    public void AnImageTheEntityDoesNotShowChangesNothing()
    {
        var entity = Mock.Of<IWithImages>();
        var manager = new Mock<IImageManager>(MockBehavior.Strict);
        manager.Setup(m => m.GetImageCrossReferencesForEntity(entity, It.IsAny<ImageCrossReferenceFilteringOptions>())).Returns([Xref(_otherImageID)]);

        Assert.Empty(manager.Object.SetImageEnabledForEntity(entity, ImageEntityType.Primary, Image(_imageID), false));
    }

    #endregion
}
