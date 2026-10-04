using System;
using System.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories.Cached;
using Shoko.Tests.Infrastructure;
using Xunit;

#pragma warning disable CS0618
namespace Shoko.Tests.Repositories;

/// <summary>
///   Covers the image lookups answered by a filter or a scan instead of an
///   index of their own.
/// </summary>
public class ImageRepositoryLookupTests
{
    #region Fields

    private static readonly Guid _primary = Guid.NewGuid();

    private static readonly Guid _linked = Guid.NewGuid();

    private static readonly Guid _single = Guid.NewGuid();

    #endregion

    #region Cross-References

    [Fact]
    public void ForTypeFindsOnlyThatTypeOfTheEntity()
    {
        var repository = Xrefs();

        var backdrops = repository.GetByEntityForType(MetadataSource.AniDB, MetadataEntityType.Series, "1", ImageEntityType.Backdrop);

        Assert.Equal([2, 4], backdrops.Select(xref => xref.ID).Order());
        Assert.Empty(repository.GetByEntityForType(MetadataSource.AniDB, MetadataEntityType.Series, "2", ImageEntityType.Backdrop));
        Assert.Empty(repository.GetByEntityForType(MetadataSource.AniDB, MetadataEntityType.Series, "3", ImageEntityType.Primary));
    }

    [Fact]
    public void BySourceAndKindFindsEveryEntityOfThem()
    {
        var repository = Xrefs();

        Assert.Equal([1, 2, 3, 4], repository.GetByEntity(MetadataSource.AniDB, MetadataEntityType.Series).Select(xref => xref.ID).Order());
        Assert.Equal([5], repository.GetByEntity(MetadataSource.AniDB, MetadataEntityType.Character).Select(xref => xref.ID));
        Assert.Empty(repository.GetByEntity(MetadataSource.TMDB, MetadataEntityType.Series));
    }

    [Fact]
    public void ByPrimaryImageFindsTheLinkedAndOwnCrossReferences()
    {
        var repository = Xrefs();

        Assert.Equal([1, 2, 3], repository.GetByPrimaryImageID(_primary).Select(xref => xref.ID).Order());
        Assert.Equal([4, 5], repository.GetByPrimaryImageID(_single).Select(xref => xref.ID).Order());
        Assert.Empty(repository.GetByPrimaryImageID(_linked));
        Assert.Empty(repository.GetByPrimaryImageID(Guid.NewGuid()));
    }

    [Fact]
    public void ByPrimaryImageFollowsAChangedPrimary()
    {
        var mock = CachedRepo.BuildWritable<ShokoImage_EntityRepository, int, ShokoImage_Entity>(xref => xref.ID, Xrefs().GetAll());
        var repository = mock.Object;
        var xref = repository.GetByID(3)!;

        xref.PrimaryImageID = _linked;
        repository.Save(xref);

        Assert.Equal([1, 2], repository.GetByPrimaryImageID(_primary).Select(x => x.ID).Order());
        Assert.Equal([3], repository.GetByPrimaryImageID(_linked).Select(x => x.ID));
    }

    #endregion

    #region Images

    [Fact]
    public void ByPrimaryImageFindsThePrimaryFirstAndItsLinkedImages()
    {
        var repository = Images();

        Assert.Equal([_primary, _linked], repository.GetByPrimaryImageID(_primary).Select(image => image.ID));
        Assert.Equal([_single], repository.GetByPrimaryImageID(_single).Select(image => image.ID));
        Assert.Empty(repository.GetByPrimaryImageID(_linked));
        Assert.Empty(repository.GetByPrimaryImageID(Guid.Empty));
    }

    [Fact]
    public void ByPrimaryImageFollowsAnUnlinkedImage()
    {
        var mock = CachedRepo.BuildWritable<ShokoImageRepository, Guid, ShokoImage>(image => image.ID, Images().GetAll());
        var repository = mock.Object;
        var image = repository.GetByID(_linked)!;

        image.PrimaryID = _linked;
        repository.Save(image);

        Assert.Equal([_primary], repository.GetByPrimaryImageID(_primary).Select(i => i.ID));
        Assert.Equal([_linked], repository.GetByPrimaryImageID(_linked).Select(i => i.ID));
    }

    #endregion

    #region Helpers

    private static ShokoImage_EntityRepository Xrefs()
        => CachedRepo.Build<ShokoImage_EntityRepository, int, ShokoImage_Entity>(
            xref => xref.ID,
            Xref(1, _primary, _primary, MetadataEntityType.Series, "1", ImageEntityType.Primary),
            Xref(2, _primary, _primary, MetadataEntityType.Series, "1", ImageEntityType.Backdrop),
            Xref(3, _linked, _primary, MetadataEntityType.Series, "2", ImageEntityType.Primary),
            Xref(4, _single, _single, MetadataEntityType.Series, "1", ImageEntityType.Backdrop),
            Xref(5, _single, _single, MetadataEntityType.Character, "1", ImageEntityType.Primary)
        );

    private static ShokoImage_Entity Xref(int id, Guid imageID, Guid primaryImageID, MetadataEntityType entityType, string entityID, ImageEntityType imageType)
        => new()
        {
            ID = id,
            ImageID = imageID,
            PrimaryImageID = primaryImageID,
            ImageSource = MetadataSource.AniDB,
            EntitySource = MetadataSource.AniDB,
            EntityType = entityType,
            EntityID = entityID,
            ImageType = imageType,
            Source = MetadataSource.AniDB,
        };

    private static ShokoImageRepository Images()
        => CachedRepo.Build<ShokoImageRepository, Guid, ShokoImage>(
            image => image.ID,
            new ShokoImage { ID = _primary, PrimaryID = _primary, LocalID = 1 },
            new ShokoImage { ID = _linked, PrimaryID = _primary, LocalID = 2 },
            new ShokoImage { ID = _single, PrimaryID = _single, LocalID = 3 }
        );

    #endregion
}
