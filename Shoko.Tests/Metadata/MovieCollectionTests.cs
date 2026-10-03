using System.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Metadata;

/// <summary>
/// Covers how a stored film reads the stored collection it is a member of.
/// </summary>
[Collection(nameof(RepoFactoryCollection))]
public class MovieCollectionTests
{
    [Fact]
    public void AStoredFilmReadsTheCollectionItIsAMemberOf()
    {
        var tables = new MetadataLookupTables();
        using var scope = tables.Scope();
        IMovie member = tables.Movies.GetByProviderID(MetadataSource.TMDB, "600")!;
        IMovie alone = tables.Movies.GetByProviderID(MetadataSource.TMDB, "601")!;
        var collectionID = new MetadataGuid(MetadataSource.TMDB, MetadataEntityType.Collection, "700");

        Assert.Equal(collectionID, member.CollectionID);
        Assert.Equal(collectionID, member.Collection?.ID);
        Assert.Equal([member.ID], member.Collection!.Movies.Select(movie => movie.ID));
        Assert.Null(alone.CollectionID);
        Assert.Null(alone.Collection);
    }
}
