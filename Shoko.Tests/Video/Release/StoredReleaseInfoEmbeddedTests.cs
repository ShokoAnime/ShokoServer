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
        """[{"ProviderIDs":{"AniDB_Episode":"230182","AniDB_Anime":"15107"},"PercentageStart":0,"PercentageEnd":100},{"ProviderIDs":{"AniDB_Episode":"230183"},"PercentageStart":0,"PercentageEnd":50}]""";

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
    [InlineData("""[ {"ProviderIDs": {"AniDB_Episode": "1"}, "PercentageStart": 0, "PercentageEnd": 100} ]""")]
    [InlineData("""[{"PercentageStart":0,"PercentageEnd":100,"ProviderIDs":{"AniDB_Episode":"1"}}]""")]
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
    public void RepeatedProviderIDsShareOneInstance()
    {
        var first = Stored(CrossReferencesText, null);
        var second = Stored(CrossReferencesText, null);

        Assert.Same(first.CrossReferences[0].ProviderIDs.Keys.First(), second.CrossReferences[0].ProviderIDs.Keys.First());
        Assert.Same(first.CrossReferences[0].ProviderIDs["AniDB_Anime"], second.CrossReferences[0].ProviderIDs["AniDB_Anime"]);
    }

    #endregion

    #region Provider IDs

    [Fact]
    public void ProviderIDsBehaveAsADictionary()
    {
        var ids = new ProviderIDDictionary { ["b"] = "1", ["a"] = "2" };
        ids["b"] = "3";
        ids.Add("c", "4");

        Assert.Equal(["b", "a", "c"], ids.Keys);
        Assert.Equal("3", ids["b"]);
        Assert.Throws<System.ArgumentException>(() => ids.Add("a", "5"));
        Assert.Throws<KeyNotFoundException>(() => ids["z"]);
        Assert.True(ids.Remove("a"));
        Assert.False(ids.Remove("a"));
        Assert.Equal(new Dictionary<string, string> { ["b"] = "3", ["c"] = "4" }, ids);
        Assert.Equal("""{"b":"3","c":"4"}""", JsonConvert.SerializeObject(ids));
    }

    [Fact]
    public void ProviderIDsReadFromJsonInOrder()
    {
        var ids = JsonConvert.DeserializeObject<ProviderIDDictionary>("""{"x":"1","AniDB_Episode":"2"}""")!;

        Assert.Equal(["x", "AniDB_Episode"], ids.Keys);
        Assert.Equal("2", ((IReadOnlyDictionary<string, string>)ids)["AniDB_Episode"]);
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
