using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Databases;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories.Cached;
using Xunit;
using static Shoko.IntegrationTests.Sql;

#pragma warning disable CS0618
namespace Shoko.IntegrationTests;

/// <summary>
/// Seeds the rows the metadata identity steps rewrite, runs the steps again
/// against the migrated database, and reads the rows back, so the SQL of each
/// backend and the data fix are checked on real rows.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class MetadataIdentityDataFixTests(DatabaseMigrationFixture fixture)
{
    #region Helpers

    private static ShokoImage_Entity CrossReference(MetadataSource entitySource, MetadataEntityType entityType, string entityID)
        => new()
        {
            ImageID = Guid.NewGuid(),
            PrimaryImageID = Guid.NewGuid(),
            ImageType = ImageEntityType.Primary,
            ImageSource = MetadataSource.User,
            EntitySource = entitySource,
            EntityType = entityType,
            EntityID = entityID,
            IsEnabled = true,
            Source = MetadataSource.User,
            CreatedAt = DateTime.UtcNow,
            LastUpdatedAt = DateTime.UtcNow,
        };

    #endregion

    #region Link Steps

    #endregion

    #region Season Image Steps

    #endregion

    #region Image Cross-Reference Fix

    [Fact]
    public void TheImageFixKeysAVideoByItsHash_AndASecondRunChangesNothing()
    {
        Assert.True(fixture.Success, fixture.FailureMessage);
        var xrefs = fixture.Services.GetRequiredService<ShokoImage_EntityRepository>();
        var videos = fixture.Services.GetRequiredService<VideoLocalRepository>();

        var video = new VideoLocal
        {
            Hash = $"{Guid.NewGuid():N}".ToUpperInvariant(),
            FileSize = 734003200,
            FileName = "video.mkv",
            DateTimeCreated = DateTime.Now,
            DateTimeUpdated = DateTime.Now,
        };
        videos.Save(video, false);

        var videoXref = CrossReference(MetadataSource.Shoko, MetadataEntityType.Video, video.VideoLocalID.ToString());
        var seeded = new[] { videoXref };
        xrefs.Save(seeded);
        try
        {
            for (var run = 0; run < 2; run++)
            {
                DatabaseFixes.MoveImageCrossReferencesToEntityIDs();
                xrefs.Populate(displayName: false, cancellationToken: TestContext.Current.CancellationToken);

                Assert.Equal($"{video.Hash}+{video.FileSize}", xrefs.GetByID(videoXref.ID)?.EntityID);
            }
        }
        finally
        {
            xrefs.Delete(seeded.Select(xref => xrefs.GetByID(xref.ID)).OfType<ShokoImage_Entity>().ToList());
            videos.Delete(video);
        }
    }

    #endregion
}
