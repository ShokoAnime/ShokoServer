using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Utilities;
using Shoko.Server.Databases;
using Xunit;

namespace Shoko.Tests.Services;

/// <summary>
/// Covers the image migration's map from an image's stored ID, hashed from the source's old name, to
/// the ID the image manager gives the same source and resource now.
/// </summary>
public class ImageIdentityTests
{
    [Theory]
    [InlineData("anidb", "AniDB")]
    [InlineData("tmdb", "TMDB")]
    [InlineData("generated", "LocallyGenerated")]
    public void TheImageMigrationMapsTheOldIDToTheValueID(string value, string oldName)
    {
        var source = MetadataSource.Get(value);
        var oldID = UuidUtility.GetV5($"ImageSource={oldName},ResourceID=/abc.jpg", IImageManager.ImageIdentifierNamespace);
        var image = new ImageIdentity(oldID, oldID, source, "/abc.jpg");

        Assert.Equal(IImageManager.GetIDForImageSourceAndResourceID(source, "/abc.jpg"), ImageIdentityMigrator.BuildIDMap([image])[oldID]);
    }
}
