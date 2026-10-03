using Moq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Stub;
using Shoko.Server.Models.Metadata;
using Shoko.Server.Repositories.Cached.Metadata.Text;
using Shoko.Server.Services;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Models;

/// <summary>
/// Covers the titles of a stored season: one with no title of its own is called by its generic
/// name, made up on the spot rather than stored.
/// </summary>
[Collection(nameof(RepoFactoryCollection))]
public class StoredSeasonTitleTests
{
    private static RepoFactoryScope ScopeWith(Metadata_Season season, out MetadataTextManager manager)
    {
        var service = new Mock<IMetadataService>();
        service.Setup(s => s.GetEntry(((IMetadata)season).ID)).Returns(season);
        manager = TestTextManager.Build(new MetadataTextStore(new TextCache(), new CacheOnlyRowWriter()), service.Object);
        return new RepoFactoryScope().Set(manager);
    }

    private static Metadata_Season Season(int number)
        => new() { Source = TestSources.Plugin, ProviderID = $"s{number}", SeriesID = "1", SeasonNumber = number };

    [Theory]
    [InlineData(2, "Season 2")]
    [InlineData(0, "Specials")]
    public void ASeasonWithNoTitlesIsCalledByItsGenericName(int number, string expected)
    {
        var season = Season(number);
        using var scope = ScopeWith(season, out var manager);
        var titled = (IWithTitles)season;

        Assert.Empty(titled.Titles);
        Assert.Equal(expected, titled.Title);
        Assert.True(titled.PreferredTitle?.IsSynthesized);
        Assert.Equal(expected, titled.DefaultTitle.Value);
        Assert.True(titled.DefaultTitle.IsSynthesized);
        var byID = manager.GetPreferredTitle(((IMetadata)season).ID);
        Assert.Equal(expected, byID?.Value);
        Assert.True(byID?.IsSynthesized);
    }

    [Fact]
    public void ASeasonWithATitleIsCalledByIt()
    {
        var season = Season(2);
        using var scope = ScopeWith(season, out var manager);
        manager.SetTitles(((IMetadata)season).ID, TestSources.Plugin,
            [new TitleStub { Source = TestSources.Plugin, Value = "Staffel", Language = TitleLanguage.German, LanguageCode = "de", Type = TitleType.Main }]);
        var titled = (IWithTitles)season;

        Assert.Equal("Staffel", titled.Title);
        Assert.False(titled.DefaultTitle.IsSynthesized);
    }
}
