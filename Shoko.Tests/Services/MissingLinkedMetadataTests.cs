using System.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Server.Services;
using Xunit;

namespace Shoko.Tests.Services;

/// <summary>
///   What "Download Missing Linked Metadata" fetches: the series or film an
///   anime in the library links to but that is not stored, and the series of
///   a linked episode not stored, each once.
/// </summary>
public class MissingLinkedMetadataTests
{
    private static readonly MetadataGuid StoredSeries = MetadataGuid.Parse("tmdb://series/1");

    private static readonly MetadataGuid MissingSeries = MetadataGuid.Parse("tmdb://series/2");

    private static readonly MetadataGuid MissingMovie = MetadataGuid.Parse("tmdb://movie/3");

    private static readonly MetadataGuid StoredEpisode = MetadataGuid.Parse("tmdb://episode/10");

    private static readonly MetadataGuid MissingEpisode = MetadataGuid.Parse("tmdb://episode/11");

    [Fact]
    public void OnlyTheMissingEntriesOfAnimeInTheLibraryAreFetched()
    {
        var stored = new[] { StoredSeries, StoredEpisode };

        var missing = MetadataRefreshService.SelectMissingLinkedEntries(
            [(1, StoredSeries), (1, MissingSeries), (2, MissingMovie), (9, MissingSeries)],
            [(1, StoredEpisode, StoredSeries), (1, MissingEpisode, StoredSeries), (1, MissingEpisode, MissingSeries), (9, MissingEpisode, MetadataGuid.Parse("tmdb://series/9"))],
            animeID => animeID is not 9,
            stored.Contains
        );

        Assert.Equal([MissingSeries, MissingMovie, StoredSeries], missing);
    }
}
