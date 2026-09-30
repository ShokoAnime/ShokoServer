using System;
using System.Collections.Generic;
using Moq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Image.CrossReferences;
using Shoko.Abstractions.Metadata.Image.Options;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Storage;
using Shoko.Server.Services;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Services;

/// <summary>
/// Covers <see cref="MetadataEntityCleanup"/>: what the other stores hold for
/// removed entities goes while the people stay, and the image links of the
/// people a purge removed go with them.
/// </summary>
public class MetadataEntityCleanupTests
{
    private static readonly MetadataSource _source = TestSources.Plugin;

    private static MetadataGuid ID(MetadataEntityType entityType, string id)
        => new(_source, entityType, id);

    private sealed class Harness
    {
        public Mock<IMetadataPeopleStore> People { get; } = new();

        public Mock<IMetadataTagStore> Tags { get; } = new();

        public Mock<IMetadataStudioStore> Studios { get; } = new();

        public Mock<IMetadataRelationStore> Relations { get; } = new();

        public Mock<IMetadataSuggestionStore> Suggestions { get; } = new();

        public Mock<IImageManager> Images { get; } = new();

        private readonly Dictionary<MetadataGuid, IImageCrossReference> _imageLinks = [];

        public Harness()
        {
            People.Setup(s => s.GetCast(It.IsAny<MetadataGuid>())).Returns([]);
            People.Setup(s => s.GetCrew(It.IsAny<MetadataGuid>())).Returns([]);
            Images.Setup(i => i.GetImageCrossReferencesForEntity(It.IsAny<IWithImages>(), It.IsAny<ImageCrossReferenceFilteringOptions?>()))
                .Returns((IWithImages entity, ImageCrossReferenceFilteringOptions? _) => _imageLinks.TryGetValue(entity.ID, out var xref) ? [xref] : []);
        }

        public IImageCrossReference Linked(MetadataGuid id)
            => _imageLinks[id] = new Mock<IImageCrossReference>().Object;

        public MetadataEntityCleanup Build()
            => new(People.Object, Tags.Object, Studios.Object, Relations.Object, Suggestions.Object, Images.Object);
    }

    [Fact]
    public void EachEntityLosesItsImageLinksAndWhatTheOtherStoresHold()
    {
        var harness = new Harness();
        var season = ID(MetadataEntityType.Season, "1-1");
        var episode = ID(MetadataEntityType.Episode, "11");
        var seasonImage = harness.Linked(season);
        harness.People.Setup(s => s.GetCrew(episode)).Returns([Mock.Of<ICrew>(crew => crew.CreatorID == ID(MetadataEntityType.Creator, "c"))]);

        harness.Build().Remove([season, episode, episode]);

        foreach (var id in new[] { season, episode })
        {
            harness.Tags.Verify(s => s.RemoveTags(id), Times.Once);
            harness.Studios.Verify(s => s.RemoveStudios(id), Times.Once);
            harness.People.Verify(s => s.RemoveCast(id), Times.Once);
            harness.People.Verify(s => s.RemoveCrew(id), Times.Once);
            harness.Relations.Verify(s => s.RemoveRelations(id), Times.Once);
            harness.Suggestions.Verify(s => s.RemoveSuggestions(id), Times.Once);
        }

        harness.Images.Verify(i => i.RemoveImageCrossReference(seasonImage), Times.Once);

        // The people they credited are left to the purge.
        harness.People.Verify(s => s.RemoveOrphaned(It.IsAny<MetadataSource>(), It.IsAny<DateTime>()), Times.Never);
        harness.People.Verify(s => s.GetCreator(It.IsAny<MetadataGuid>()), Times.Never);
    }

    [Fact]
    public void ThePeopleAPurgeRemovedLoseTheirImageLinks()
    {
        var harness = new Harness();
        var goneID = ID(MetadataEntityType.Creator, "gone");
        var characterID = ID(MetadataEntityType.Character, "gone");
        var keptID = ID(MetadataEntityType.Creator, "kept");
        var goneImage = harness.Linked(goneID);
        var characterImage = harness.Linked(characterID);
        var keptImage = harness.Linked(keptID);

        harness.Build().RemoveImageLinks([goneID, characterID, goneID]);

        harness.Images.Verify(i => i.RemoveImageCrossReference(goneImage), Times.Once);
        harness.Images.Verify(i => i.RemoveImageCrossReference(characterImage), Times.Once);
        harness.Images.Verify(i => i.RemoveImageCrossReference(keptImage), Times.Never);
    }
}
