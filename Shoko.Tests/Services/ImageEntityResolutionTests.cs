using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using Moq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Image;
using Shoko.Abstractions.Metadata.Image.CrossReferences;
using Shoko.Abstractions.Metadata.Providers;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Services;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Services;

/// <summary>
/// Covers how <see cref="ImageManager"/> names and resolves the entity of an image cross-reference: through
/// the metadata service's lookup, plugin resolvers included, the entries it leaves out, and the ID a
/// cross-reference records for its entity.
/// </summary>
[Collection(nameof(RepoFactoryCollection))]
public class ImageEntityResolutionTests
{
    #region Harness

    private sealed class Harness
    {
        public Mock<ILogger<ImageManager>> Logger { get; } = new();

        public Mock<IMetadataService> MetadataService { get; } = new(MockBehavior.Loose);

        public ImageManager Manager { get; }

        /// <summary>
        /// Builds the image manager over a mocked metadata service, or over
        /// the real one of <paramref name="tables"/>.
        /// </summary>
        /// <param name="tables">The tables whose metadata service to find entries through, if any.</param>
        public Harness(MetadataLookupTables? tables = null)
        {
            var metadataService = tables?.Service ?? MetadataService.Object;

            // Every other dependency is left out, since resolving an entity never reaches it.
            var constructor = typeof(ImageManager).GetConstructors().Single();
            var arguments = constructor.GetParameters()
                .Select(parameter => parameter.ParameterType switch
                {
                    var type when type == typeof(ILogger<ImageManager>) => Logger.Object,
                    var type when type == typeof(Lazy<IMetadataService>) => new Lazy<IMetadataService>(() => metadataService),
                    _ => (object?)null,
                })
                .ToArray();
            Manager = (ImageManager)constructor.Invoke(arguments);
        }
    }

    private static Mock<IMetadataResolver> Resolver(string name, MetadataSource source, MetadataEntityType entityType, IMetadata? entry = null)
        => Resolver(name, MetadataEntityScope.Single(source, entityType), entry);

    private static Mock<IMetadataResolver> Resolver(string name, MetadataEntityScope scope, IMetadata? entry = null)
    {
        var resolver = new Mock<IMetadataResolver>();
        resolver.SetupGet(r => r.Name).Returns(name);
        resolver.SetupGet(r => r.Scope).Returns(scope);
        resolver.Setup(r => r.GetEntry(It.IsAny<MetadataGuid>())).Returns(entry);
        return resolver;
    }

    /// <summary>
    /// Builds the image manager over the real metadata service of fresh
    /// tables, with <paramref name="resolvers"/> given to that service.
    /// </summary>
    /// <param name="resolvers">The metadata resolvers the service takes.</param>
    /// <returns>The harness.</returns>
    private static Harness WithResolvers(params IMetadataResolver[] resolvers)
    {
        var tables = new MetadataLookupTables();
        tables.Service.AddParts([], resolvers);
        return new Harness(tables);
    }

    private static IWithImages Entity(MetadataGuid id)
    {
        var entity = new Mock<IWithImages>();
        entity.SetupGet(e => e.ID).Returns(id);
        return entity.Object;
    }

    #endregion

    #region Entries

    [Fact]
    public void AnEntryWithoutImagesIsNotFoundForImages()
    {
        var id = new MetadataGuid(TestSources.Plugin, TestEntityTypes.Library, "plain");
        var entry = new Mock<IMetadata>();
        entry.SetupGet(e => e.ID).Returns(id);
        var harness = WithResolvers(Resolver("Library", TestSources.Plugin, TestEntityTypes.Library, entry.Object).Object);

        Assert.Null(harness.Manager.GetEntityForImage(id));
    }

    [Fact]
    public void AnOrderingAndAGroupOfAUsersOrderingAreFoundThroughTheOrderingService()
    {
        var harness = new Harness();
        var orderingID = new MetadataGuid(MetadataSource.User, MetadataEntityType.Ordering, "mine");
        var groupID = new MetadataGuid(MetadataSource.User, MetadataEntityType.Season, "part");
        var tmdbOrderingID = new MetadataGuid(MetadataSource.TMDB, MetadataEntityType.Ordering, "5f0c1a2b3c4d5e6f7a8b9c0d");
        var ordering = new Mock<IOrdering>();
        var group = new Mock<ISeason>();
        var tmdbOrdering = new Mock<IOrdering>();
        harness.MetadataService.Setup(s => s.GetEntry(orderingID)).Returns(ordering.Object);
        harness.MetadataService.Setup(s => s.GetEntry(groupID)).Returns(group.Object);
        harness.MetadataService.Setup(s => s.GetEntry(tmdbOrderingID)).Returns(tmdbOrdering.Object);

        Assert.Same(ordering.Object, harness.Manager.GetEntityForImage(orderingID));
        Assert.Same(group.Object, harness.Manager.GetEntityForImage(groupID));
        Assert.Null(harness.Manager.GetEntityForImage(new(MetadataSource.User, MetadataEntityType.Ordering, "gone")));

        // The orderings of the core's other sources keep what their source
        // gives them.
        Assert.Null(harness.Manager.GetEntityForImage(tmdbOrderingID));
    }

    [Fact]
    public void ADefaultOrderingIsNotFoundForImages()
    {
        // It is never stored, so nothing would remove its links once its
        // series is gone.
        var harness = new Harness();
        var pluginDefaultID = new MetadataGuid(TestSources.Plugin, MetadataEntityType.Ordering, "show");
        var pluginStoredID = new MetadataGuid(TestSources.Plugin, MetadataEntityType.Ordering, "arcs");
        var pluginDefault = new Mock<IOrdering>();
        pluginDefault.SetupGet(o => o.IsDefault).Returns(true);
        var pluginStored = new Mock<IOrdering>();
        harness.MetadataService.Setup(s => s.GetEntry(pluginDefaultID)).Returns(pluginDefault.Object);
        harness.MetadataService.Setup(s => s.GetEntry(pluginStoredID)).Returns(pluginStored.Object);

        Assert.Null(harness.Manager.GetEntityForImage(pluginDefaultID));
        Assert.Same(pluginStored.Object, harness.Manager.GetEntityForImage(pluginStoredID));
        Assert.Null(harness.Manager.GetEntityForImage(new(MetadataSource.Shoko, MetadataEntityType.Ordering, "1")));
        Assert.Null(harness.Manager.GetEntityForImage(new(MetadataSource.AniDB, MetadataEntityType.Ordering, "100")));
    }

    #endregion

    #region Kinds

    /// <summary>
    /// Entries of <see cref="MetadataServiceLookupTests.EveryKind"/> and
    /// whether images link to them: not when they have no images.
    /// </summary>
    public static TheoryData<string, bool> Kinds => new()
    {
        { "shoko://series/3", true },
        { "shoko://filter/7", false },
        { "user://tag/8", false },
        { "test-plugin://tag/1", false },
    };

    [Theory]
    [MemberData(nameof(Kinds))]
    public void AnEntryIsFoundForImagesOnlyWhenItHasImages(string idText, bool found)
    {
        var tables = new MetadataLookupTables();
        using var scope = tables.Scope();
        tables.StorePluginEntries();
        var harness = new Harness(tables);
        var id = MetadataGuid.Parse(idText);

        var entity = harness.Manager.GetEntityForImage(id);

        if (!found)
        {
            Assert.Null(entity);
            return;
        }

        Assert.Equal(id, entity?.ID);
    }

    #endregion

    #region Cross-References

    [Fact]
    public void ACrossReferenceRecordsItsEntitysIDAndTheNumbersOfAnEpisode()
    {
        var id = new MetadataGuid(TestSources.Plugin, MetadataEntityType.Episode, "e-9");
        var episode = new Mock<IEpisode>();
        episode.SetupGet(e => e.ID).Returns(id);
        episode.SetupGet(e => e.SeasonNumber).Returns(2);
        episode.SetupGet(e => e.EpisodeNumber).Returns(5);
        episode.SetupGet(e => e.AirDate).Returns(new DateOnly(2020, 1, 2));
        var image = new Mock<IImage>();
        image.SetupGet(i => i.ID).Returns(Guid.NewGuid());
        image.SetupGet(i => i.PrimaryID).Returns(Guid.NewGuid());
        image.SetupGet(i => i.Source).Returns(TestSources.Plugin);

        var xref = new ShokoImage_Entity(image.Object, episode.Object, new() { ImageType = ImageEntityType.Primary }, 0);

        Assert.Equal(id, ((IImageCrossReference)xref).EntityID);
        Assert.Equal(2, xref.EntitySeasonNumber);
        Assert.Equal(5, xref.EntityEpisodeNumber);
        Assert.Equal(new DateOnly(2020, 1, 2), xref.EntityReleasedAt);
    }

    [Fact]
    public void AnUpdateRefusesADifferentEntity()
    {
        var image = new Mock<IImage>();
        image.SetupGet(i => i.Source).Returns(TestSources.Plugin);
        var entity = Entity(new(TestSources.Plugin, TestEntityTypes.Library, "a"));
        var xref = new ShokoImage_Entity(image.Object, entity, new() { ImageType = ImageEntityType.Primary }, 0);

        Assert.Throws<ArgumentException>(() => xref.Update(null, Entity(new(TestSources.Plugin, TestEntityTypes.Library, "b"))));
        Assert.False(xref.Update(null, Entity(new(TestSources.Plugin, TestEntityTypes.Library, "a"))));
    }

    #endregion
}
