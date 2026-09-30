using System;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Models.TMDB;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Cached.AniDB;
using Shoko.Server.Repositories.Cached.Metadata.Text;
using Shoko.Server.Repositories.Direct.TMDB;
using Shoko.Server.Repositories.Direct.TMDB.Optional;
using Xunit;

namespace Shoko.IntegrationTests;

/// <summary>
/// Writes the core's entries that no cache holds, and the core's two tag
/// tables, into the migrated database, reads the caches back from it and
/// finds each entry again by its ID, the way
/// <see cref="IMetadataService.GetEntry(MetadataGuid)"/> and the image
/// manager find them.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class MetadataEntryLookupRoundTripTests(DatabaseMigrationFixture fixture)
{
    [Fact]
    public void TheCoresUncachedEntriesAndTagsAreFoundByTheirIDAfterAReload()
    {
        Assert.True(fixture.Success, fixture.FailureMessage);
        var services = fixture.Services;
        var metadata = services.GetRequiredService<IMetadataService>();
        var images = services.GetRequiredService<IImageManager>();
        var persons = services.GetRequiredService<TMDB_PersonRepository>();
        var companies = services.GetRequiredService<TMDB_CompanyRepository>();
        var networks = services.GetRequiredService<TMDB_NetworkRepository>();
        var anidbTags = services.GetRequiredService<AniDB_TagRepository>();
        var customTags = services.GetRequiredService<CustomTagRepository>();
        var person = new TMDB_Person(990_501) { EnglishName = "Lookup Person" };
        var company = new TMDB_Company(990_502) { Name = "Lookup Company" };
        var network = new TMDB_Network { TmdbNetworkID = 990_503, Name = "Lookup Network" };
        var anidbTag = new AniDB_Tag { TagID = 990_504, TagNameSource = "Lookup Tag", LastUpdated = DateTime.Now };
        var customTag = new CustomTag { TagName = "Lookup Custom Tag" };
        persons.Save(person);
        companies.Save(company);
        networks.Save(network);
        anidbTags.Save(anidbTag);
        customTags.Save(customTag);
        try
        {
            services.GetRequiredService<TextCache>().Populate(displayName: false, cancellationToken: TestContext.Current.CancellationToken);
            anidbTags.Populate(displayName: false, cancellationToken: TestContext.Current.CancellationToken);
            customTags.Populate(displayName: false, cancellationToken: TestContext.Current.CancellationToken);

            var withImages = new MetadataGuid[]
            {
                new(MetadataSource.TMDB, MetadataEntityType.Creator, "990501"),
                new(MetadataSource.TMDB, MetadataEntityType.Studio, "990502"),
                new(MetadataSource.TMDB, MetadataEntityType.Network, "990503"),
            };
            foreach (var id in withImages)
            {
                Assert.Equal(id, metadata.GetEntry(id)?.ID);
                Assert.Equal(id, metadata.GetEntry<IWithImages>(id)?.ID);
                Assert.Equal(id, images.GetEntityForImage(id)?.ID);
            }

            var withoutImages = new MetadataGuid[]
            {
                new(MetadataSource.AniDB, MetadataEntityType.Tag, "990504"),
                new(MetadataSource.User, MetadataEntityType.Tag, customTag.CustomTagID.ToString()),
            };
            foreach (var id in withoutImages)
            {
                Assert.Equal(id, metadata.GetEntry<ITag>(id)?.ID);
                Assert.Null(images.GetEntityForImage(id));
            }

            Assert.Equal("Lookup Person", metadata.GetEntry<ICreator>(new(MetadataSource.TMDB, MetadataEntityType.Creator, "990501"))?.Name);
            Assert.Null(metadata.GetEntry<ICharacter>(new(MetadataSource.TMDB, MetadataEntityType.Creator, "990501")));
            Assert.Null(metadata.GetEntry(new(MetadataSource.TMDB, MetadataEntityType.Creator, "990599")));
        }
        finally
        {
            persons.Delete(person);
            companies.Delete(company);
            networks.Delete(network);
            anidbTags.Delete(anidbTag);
            customTags.Delete(customTag);
        }
    }
}
