using Moq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Image.CrossReferences;
using Shoko.Abstractions.Video;
using Shoko.Server.API.v3.Models.ImageManagement;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.API;

/// <summary>
/// Pins the entity ID APIv3 hands out for an image cross-reference, which
/// kept its old shape when the abstractions started naming entities by
/// <see cref="MetadataGuid"/>.
/// </summary>
public class ImageCrossReferenceOutputTests
{
    private static Mock<IImageCrossReference> CrossReference(MetadataGuid entityID, IVideo? video = null)
    {
        var xref = new Mock<IImageCrossReference>(MockBehavior.Loose);
        xref.SetupGet(x => x.Source).Returns(MetadataSource.User);
        xref.SetupGet(x => x.ImageSource).Returns(MetadataSource.User);
        xref.SetupGet(x => x.EntityID).Returns(entityID);
        xref.Setup(x => x.GetEntity()).Returns(video);
        return xref;
    }

    [Fact]
    public void AVideoIsNamedByItsFileID()
    {
        var video = new Mock<IVideo>();
        video.SetupGet(v => v.LocalID).Returns(4711);
        var entityID = new MetadataGuid(MetadataSource.Shoko, MetadataEntityType.Video, "0123456789ABCDEF0123456789ABCDEF+734003200");

        var model = new ImageCrossReference(CrossReference(entityID, video.Object).Object);

        Assert.Equal("4711", model.EntityID);
        Assert.Equal(MetadataSource.Shoko, model.EntitySource);
        Assert.Equal(MetadataEntityType.Video, model.EntityType);
    }

    [Fact]
    public void AVideoThatIsGoneKeepsTheIDPartOfItsName()
    {
        var entityID = new MetadataGuid(MetadataSource.Shoko, MetadataEntityType.Video, "0123456789ABCDEF0123456789ABCDEF+734003200");

        var model = new ImageCrossReference(CrossReference(entityID).Object);

        Assert.Equal("0123456789ABCDEF0123456789ABCDEF+734003200", model.EntityID);
    }

    [Fact]
    public void AnyOtherEntityIsNamedByTheIDPartOfItsName()
    {
        var xref = CrossReference(new MetadataGuid(TestSources.Plugin, MetadataEntityType.Series, "s-1"));

        var model = new ImageCrossReference(xref.Object);

        Assert.Equal("s-1", model.EntityID);
        xref.Verify(x => x.GetEntity(), Times.Never);
    }
}
