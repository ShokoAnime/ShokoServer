using Moq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Image.Options;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Storage;
using Shoko.Server.Services;

namespace Shoko.Tests.Infrastructure;

/// <summary>
/// A <see cref="MetadataEntityCleanup"/> over stores that hold nothing, for tests of a store whose
/// removals need one but are not about what the other stores hold.
/// </summary>
public static class NoCleanup
{
    /// <summary>
    /// Builds the clean-up.
    /// </summary>
    /// <returns>A clean-up that finds nothing to remove.</returns>
    public static MetadataEntityCleanup Build()
    {
        var people = new Mock<IMetadataPeopleStore>();
        people.Setup(s => s.GetCast(It.IsAny<MetadataGuid>())).Returns([]);
        people.Setup(s => s.GetCrew(It.IsAny<MetadataGuid>())).Returns([]);
        var images = new Mock<IImageManager>();
        images.Setup(i => i.GetImageCrossReferencesForEntity(It.IsAny<IWithImages>(), It.IsAny<ImageCrossReferenceFilteringOptions?>())).Returns([]);
        return new(
            people.Object,
            Mock.Of<IMetadataTagStore>(),
            Mock.Of<IMetadataStudioStore>(),
            Mock.Of<IMetadataRelationStore>(),
            Mock.Of<IMetadataSuggestionStore>(),
            images.Object
        );
    }
}
