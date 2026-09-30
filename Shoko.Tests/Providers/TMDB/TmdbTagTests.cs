using System.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Models.TMDB;
using Xunit;

namespace Shoko.Tests.Providers.TMDB;

/// <summary>
/// TMDB's genres and keywords read as tags on its shows and movies.
/// </summary>
public class TmdbTagTests
{
    [Fact]
    public void AShowsGenresAndKeywordsAreItsTags()
    {
        var show = new TMDB_Show(5) { Genres = ["Animation", "Drama"], Keywords = ["anime", " ", "based on manga"] };

        var tags = ((ISeries)show).Tags;

        Assert.Equal(
            [("Animation", TagKind.Genre), ("Drama", TagKind.Genre), ("anime", TagKind.Keyword), ("based on manga", TagKind.Keyword)],
            tags.Select(tag => (tag.Name, tag.Kind))
        );
        Assert.Equal(MetadataGuid.Parse("tmdb://tag/genre/Animation"), tags[0].ID);
        Assert.Equal(MetadataGuid.Parse("tmdb://tag/keyword/based on manga"), tags[3].ID);
        Assert.All(tags, tag => Assert.Equal(MetadataSource.TMDB, tag.Source));
    }

    [Fact]
    public void AGenreOrKeywordTooLongForAnIDIsHashed()
    {
        var name = new string('k', MetadataGuid.MaxIDLength);

        var id = TMDB_Tag.IDFor(name, TagKind.Keyword);

        Assert.StartsWith("keyword/#", id);
        Assert.True(id.Length <= MetadataGuid.MaxIDLength);
        Assert.Equal(id, TMDB_Tag.IDFor(name, TagKind.Keyword));
        Assert.NotEqual(id, TMDB_Tag.IDFor(name, TagKind.Genre));
    }
}
