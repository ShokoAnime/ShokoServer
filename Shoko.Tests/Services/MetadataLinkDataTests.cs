using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.CrossReferences;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Models.CrossReference;
using Shoko.Server.Models.CrossReference.Embedded;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Services;

/// <summary>
/// Covers how a stored link reads back: the entries it names, and the shape
/// the store would write it in.
/// </summary>
public class MetadataLinkDataTests
{
    [Fact]
    public void AStoredEpisodeLinkReadsBackAsTheLinkItWouldBeWritten()
    {
        var stored = new CrossRef_AniDB_Metadata_Episode
        {
            Source = TestSources.AniList,
            AnidbAnimeID = 100,
            AnidbEpisodeID = 1001,
            ProviderID = "70001",
            ProviderParentID = "9001",
            ProviderSeasonID = "9001-s1",
            SeasonNumber = 1,
            EpisodeNumber = 4,
            MatchRating = MatchRating.UserVerified,
            Ordering = 2,
        };

        var link = ((IMetadataEpisodeCrossReference)stored).ToLinkData();

        Assert.Equal(new MetadataGuid(TestSources.AniList, MetadataEntityType.Episode, "70001"), link.ProviderID);
        Assert.Equal(new MetadataGuid(TestSources.AniList, MetadataEntityType.Series, "9001"), link.ProviderParentID);
        Assert.Equal(new MetadataGuid(TestSources.AniList, MetadataEntityType.Season, "9001-s1"), link.SeasonID);
    }

    [Fact]
    public void AnEmptyParentIsNoParentRatherThanAnEmptyOne()
    {
        var stored = new CrossRef_AniDB_Metadata_Episode
        {
            Source = TestSources.AniList,
            AnidbAnimeID = 100,
            AnidbEpisodeID = 1002,
            ProviderID = "10",
        };

        Assert.Null(((IMetadataEpisodeCrossReference)stored).ToLinkData().ProviderParentID);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void AnEmptyOrBlankProviderIDIsALinkToNoEntry(string providerID)
    {
        var stored = new CrossRef_AniDB_Metadata_Episode
        {
            Source = MetadataSource.TMDB,
            AnidbAnimeID = 100,
            AnidbEpisodeID = 1002,
            ProviderID = providerID,
        };

        Assert.Null(((IMetadataCrossReference)stored).ProviderID);
        Assert.True(stored.IsUnlinked);
        Assert.Null(((IMetadataEpisodeCrossReference)stored).ToLinkData().ProviderID);
    }

    [Fact]
    public void AZeroIsAnEntryLikeAnyOther()
    {
        // At rest no entry is an empty string; the old "0" is rewritten on
        // upgrade, so a "0" left is a source's own ID.
        var stored = new CrossRef_AniDB_Metadata_Series { Source = TestSources.AniList, AnidbAnimeID = 100, ProviderID = "0" };

        Assert.Equal(new MetadataGuid(TestSources.AniList, MetadataEntityType.Series, "0"), ((IMetadataCrossReference)stored).ProviderID);
    }

    [Fact]
    public void ATmdbLinkToNothingIsStoredEmpty()
    {
        var xref = new CrossRef_AniDB_TMDB_Episode(1001, 100, 0, 0);

        Assert.Equal(string.Empty, xref.Row.ProviderID);
        Assert.Null(((IMetadataCrossReference)xref).ProviderID);
        Assert.Null(((IMetadataEpisodeCrossReference)xref).ProviderParentID);
        Assert.Equal(0, xref.TmdbEpisodeID);
    }
}
