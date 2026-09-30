using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Airing;
using Shoko.Server.Models.Airing;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.Shoko;
using Xunit;

namespace Shoko.Tests.Metadata;

/// <summary>
/// Covers the <see cref="MetadataGuid"/> each implementer names itself by: the
/// source's own ID for the entry, with the source and kind read off it.
/// </summary>
public class MetadataIdentityTests
{
    [Fact]
    public void EachEntryNamesItsSourceAndKind()
    {
        (IMetadata Entry, MetadataSource Source, MetadataEntityType EntityType)[] entries =
        [
            // A group is a collection of series, and a custom tag is the user's.
            (new AnimeGroup { AnimeGroupID = 5 }, MetadataSource.Shoko, MetadataEntityType.Collection),
            (new CustomTag { CustomTagID = 3 }, MetadataSource.User, MetadataEntityType.Tag),
            (new FilterPreset { FilterPresetID = 7 }, MetadataSource.Shoko, MetadataEntityType.Filter),
            (new AniDB_Tag { TagID = 3 }, MetadataSource.AniDB, MetadataEntityType.Tag),
            (new AiringChannel("Tokyo MX", AiringChannelType.Television), MetadataSource.Shoko, MetadataEntityType.Channel),
        ];

        Assert.All(entries, entry =>
        {
            Assert.Equal((entry.Source, entry.EntityType), (entry.Entry.ID.Source, entry.Entry.ID.EntityType));
            Assert.Equal((entry.Source, entry.EntityType), (entry.Entry.Source, entry.Entry.EntityType));
        });
    }

    [Fact]
    public void AChannelIsNoNetwork()
        => Assert.False(new AiringChannel("Tokyo MX", AiringChannelType.Television) is INetwork);
}
