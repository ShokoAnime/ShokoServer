using System;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Cached.AniDB;
using Shoko.Server.Repositories.Cached.Metadata.Text;
using Xunit;

namespace Shoko.IntegrationTests;

/// <summary>
/// Writes the core's two tag tables into the migrated database, reads the
/// caches back from it and finds each tag again by its ID, the way
/// <see cref="IMetadataService.GetEntry(MetadataGuid)"/> and the image
/// manager find them.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class MetadataEntryLookupRoundTripTests(DatabaseMigrationFixture fixture)
{
    [Fact]
    public void TheCoresTagsAreFoundByTheirIDAfterAReload()
    {
        Assert.True(fixture.Success, fixture.FailureMessage);
        var services = fixture.Services;
        var metadata = services.GetRequiredService<IMetadataService>();
        var images = services.GetRequiredService<IImageManager>();
        var anidbTags = services.GetRequiredService<AniDB_TagRepository>();
        var customTags = services.GetRequiredService<CustomTagRepository>();
        var anidbTag = new AniDB_Tag { TagID = 990_504, TagNameSource = "Lookup Tag", LastUpdated = DateTime.Now };
        var customTag = new CustomTag { TagName = "Lookup Custom Tag" };
        anidbTags.Save(anidbTag);
        customTags.Save(customTag);
        try
        {
            services.GetRequiredService<TextCache>().Populate(displayName: false, cancellationToken: TestContext.Current.CancellationToken);
            anidbTags.Populate(displayName: false, cancellationToken: TestContext.Current.CancellationToken);
            customTags.Populate(displayName: false, cancellationToken: TestContext.Current.CancellationToken);

            var tags = new MetadataGuid[]
            {
                new(MetadataSource.AniDB, MetadataEntityType.Tag, "990504"),
                new(MetadataSource.User, MetadataEntityType.Tag, customTag.CustomTagID.ToString()),
            };
            foreach (var id in tags)
            {
                Assert.Equal(id, metadata.GetEntry<ITag>(id)?.ID);
                Assert.Null(images.GetEntityForImage(id));
            }
        }
        finally
        {
            anidbTags.Delete(anidbTag);
            customTags.Delete(customTag);
        }
    }
}
