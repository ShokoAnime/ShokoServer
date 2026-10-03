using Shoko.Abstractions.Metadata.CrossReferences;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.API.v3.Models.TMDB.Input;
using Xunit;
using static Shoko.Server.API.v3.Controllers.TmdbController;

namespace Shoko.Tests.API;

/// <summary>
/// Covers how the TMDB export body is handed to the cross-reference transfer
/// service, so the endpoint writes what it wrote before.
/// </summary>
public class TmdbExportBodyTests
{
    [Fact]
    public void NoSectionsWriteNothing()
        => Assert.Equal(MetadataCrossReferenceSections.None, new TmdbExportBody().ToOptions().Sections);

    [Fact]
    public void TheSectionsAndFiltersCarryOver()
    {
        var options = new TmdbExportBody
        {
            SectionSet = [CrossReferenceExportType.Show, CrossReferenceExportType.Episode],
            TmdbEpisodeID = 0,
        }.ToOptions();

        Assert.Equal(MetadataCrossReferenceSections.Series | MetadataCrossReferenceSections.Episode, options.Sections);
        // A TMDB ID is handed on as the provider's ID, zero as a link to nothing.
        Assert.Equal(string.Empty, options.ProviderEpisodeID);
    }

    [Theory]
    [InlineData(IncludeOnlyFilter.True, null)]
    [InlineData(IncludeOnlyFilter.Only, true)]
    [InlineData(IncludeOnlyFilter.False, false)]
    public void TheIncludeFiltersBecomeThreeWaySwitches(IncludeOnlyFilter filter, bool? expected)
    {
        var options = new TmdbExportBody { Automatic = filter, WithEpisodes = filter }.ToOptions();

        Assert.Equal(expected, options.Automatic);
        Assert.Equal(expected, options.WithEpisodes);
    }
}
