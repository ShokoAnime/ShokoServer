using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Server.Databases;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories.Cached;
using Xunit;

namespace Shoko.IntegrationTests;

/// <summary>
///   A new location is the primary of its own group once saved, and a removed
///   primary hands its group to the next location, a file before a link.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class VideoFileGroupTests(DatabaseMigrationFixture fixture)
{
    [Fact]
    public void ALocation_StartsAsItsOwnPrimary_AndARemovedPrimaryHandsItsCopyOn()
    {
        Assert.True(fixture.Success, fixture.FailureMessage);
        var locations = fixture.Services.GetRequiredService<VideoLocal_PlaceRepository>();
        var database = fixture.Services.GetRequiredService<DatabaseFactory>();
        int Stored(VideoLocal_Place location)
        {
            using var session = database.SessionFactory.OpenSession();
            return session.CreateSQLQuery("SELECT PrimaryLocationID FROM VideoLocal_Place WHERE ID = :id")
                .SetParameter("id", location.ID)
                .UniqueResult<object>() is { } value ? Convert.ToInt32(value) : 0;
        }

        var name = $"copy-test-{Guid.NewGuid():N}";
        VideoLocal_Place Make(string suffix)
        {
            var location = new VideoLocal_Place { VideoID = int.MaxValue, ManagedFolderID = int.MaxValue, RelativePath = $"{name}/{suffix}.mkv" };
            locations.Save(location);
            return location;
        }

        var link = Make("link");
        var file = Make("file");
        var hardLink = Make("hard-link");
        try
        {
            // Stored, not only read back as its own ID.
            Assert.All([link, file, hardLink], location => Assert.Equal(location.ID, Stored(location)));
            Assert.Equal([link.ID], locations.GetByPrimaryLocationID(link.ID).Select(location => location.ID));

            link.IsSymbolicLink = true;
            link.SymbolicLinkTarget = $"/{name}/file.mkv";
            foreach (var location in new[] { link, file, hardLink })
                location.PrimaryLocationID = link.ID;
            locations.Save([link, file, hardLink]);
            Assert.Equal([link.ID, file.ID, hardLink.ID], locations.GetByPrimaryLocationID(link.ID).Select(location => location.ID).Order());

            locations.Delete(link);

            Assert.Empty(locations.GetByPrimaryLocationID(link.ID));
            Assert.Equal([file.ID, hardLink.ID], locations.GetByPrimaryLocationID(file.ID).Select(location => location.ID).Order());
            Assert.Equal(file.ID, Stored(hardLink));
        }
        finally
        {
            locations.Delete([.. new[] { link, file, hardLink }.Where(location => locations.GetByID(location.ID) is not null)]);
        }
    }
}
