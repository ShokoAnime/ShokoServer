using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Video.Hashing;
using Shoko.Server.Models.Release;
using Xunit;

namespace Shoko.Tests.Video.Release;

public class StoredReleaseInfoEmbeddedTests
{
    private const string ED2K = "0880F299DD50D50943069183F979BB8D";

    private const string CrossReferencesText =
        """[{"IDs":["anidb://episode/230182","anidb://series/15107"],"PercentageStart":0,"PercentageEnd":100},{"IDs":["anidb://episode/230183"],"PercentageStart":0,"PercentageEnd":50}]""";

    private const string HashesText =
        """[{"Type":"ED2K","Value":"0880F299DD50D50943069183F979BB8D","Metadata":null},{"Type":"MD5","Value":"F27798EA3796DB35C7409271254985BA","Metadata":null},{"Type":"CRC32","Value":"C859A673","Metadata":"x"}]""";

    #region Saving the same column values

    [Fact]
    public void StoredTextIsWrittenBackUnchanged()
    {
        var release = Stored(CrossReferencesText, HashesText);

        Assert.Equal(CrossReferencesText, release.EmbeddedCrossReferences);
        Assert.Equal(HashesText, release.EmbeddedHashes);

        // Reading the lists changes nothing that is written.
        _ = release.CrossReferences;
        _ = release.Hashes;
        Assert.Equal(CrossReferencesText, release.EmbeddedCrossReferences);
        Assert.Equal(HashesText, release.EmbeddedHashes);
    }

    [Theory]
    [InlineData("""[ {"IDs": ["anidb://episode/1"], "PercentageStart": 0, "PercentageEnd": 100} ]""")]
    [InlineData("""[{"PercentageStart":0,"PercentageEnd":100,"IDs":["anidb://episode/1"]}]""")]
    [InlineData("null")]
    public void TextTheListWouldWriteDifferentlyIsKept(string text)
    {
        var release = Stored(text, text is "null" ? "null" : """[ {"Type":"ED2K","Value":"0880F299DD50D50943069183F979BB8D"} ]""");
        var hashesText = release.EmbeddedHashes;

        _ = release.Hashes;

        Assert.Equal(text, release.EmbeddedCrossReferences);
        Assert.Equal(hashesText, release.EmbeddedHashes);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void MissingHashesStayMissing(string? text)
    {
        var release = Stored(CrossReferencesText, text);

        Assert.Null(release.Hashes);
        Assert.Equal(text, release.EmbeddedHashes);
    }

    [Fact]
    public void SetListsAreWrittenAsTheyWereBefore()
    {
        var crossReferences = JsonConvert.DeserializeObject<List<EmbeddedCrossReference>>(CrossReferencesText)!;
        var hashes = JsonConvert.DeserializeObject<List<HashDigest>>(HashesText)!;
        var release = new StoredReleaseInfo { CrossReferences = crossReferences, Hashes = hashes };

        Assert.Equal(CrossReferencesText, release.EmbeddedCrossReferences);
        Assert.Equal(HashesText, release.EmbeddedHashes);

        release.Hashes = null;
        Assert.Null(release.EmbeddedHashes);
        Assert.Equal("[]", new StoredReleaseInfo().EmbeddedCrossReferences);
    }

    #endregion

    #region Reading the lists

    [Fact]
    public void ListsReadTheStoredValues()
    {
        var release = Stored(CrossReferencesText, HashesText);

        Assert.Equal([230182, 230183], release.CrossReferences.Select(xref => xref.AnidbEpisodeID));
        Assert.Equal([15107, null], release.CrossReferences.Select(xref => xref.AnidbAnimeID));
        Assert.Equal([(0, 100), (0, 50)], release.CrossReferences.Select(xref => (xref.PercentageStart, xref.PercentageEnd)));
        Assert.Equal(["ED2K", "MD5", "CRC32"], release.Hashes!.Select(hash => hash.Type));
        Assert.Equal([null, null, "x"], release.Hashes!.Select(hash => hash.Metadata));
    }

    [Fact]
    public void TheHashesAreReadOnceAndShareTheReleaseHash()
    {
        var release = Stored(CrossReferencesText, HashesText);

        Assert.Same(release.Hashes, release.Hashes);
        Assert.Same(release.ED2K, release.Hashes!.Single(hash => hash.Type is "ED2K").Value);
    }

    [Fact]
    public void RepeatedIDsShareOneInstance()
    {
        var first = Stored(CrossReferencesText, null);
        var second = Stored(CrossReferencesText, null);

        Assert.Same(first.CrossReferences[0].IDs[1], second.CrossReferences[0].IDs[1]);
    }

    #endregion

    #region Comparing releases

    [Fact]
    public void AReleaseBuiltFromListsEqualsTheStoredOne()
    {
        var stored = Stored(CrossReferencesText, HashesText);
        var built = new StoredReleaseInfo
        {
            StoredReleaseInfoID = 7,
            ED2K = ED2K,
            CrossReferences = JsonConvert.DeserializeObject<List<EmbeddedCrossReference>>(CrossReferencesText)!,
            Hashes = JsonConvert.DeserializeObject<List<HashDigest>>(HashesText)!,
        };

        Assert.True(stored == built);
        Assert.Equal(stored.GetHashCode(), built.GetHashCode());
    }

    [Fact]
    public void AChangedFlagIsANewRelease()
    {
        var stored = Stored(CrossReferencesText, HashesText);
        var unlisted = Stored(CrossReferencesText, HashesText);
        unlisted.IsPublic = false;
        var pinned = Stored(CrossReferencesText, HashesText);
        pinned.PreventRescan = true;

        Assert.False(stored == unlisted);
        Assert.False(stored == pinned);
    }

    #endregion

    #region Helpers

    private static StoredReleaseInfo Stored(string crossReferences, string? hashes)
        => new()
        {
            ED2K = new string(ED2K.ToCharArray()),
            EmbeddedCrossReferences = crossReferences,
            EmbeddedHashes = hashes,
        };

    #endregion
}
