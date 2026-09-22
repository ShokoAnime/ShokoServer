using System.Linq;
using Shoko.Abstractions.Metadata.Anilist;
using Shoko.Server.Models.Anilist;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.Cached.Anilist;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Models;

/// <summary>
/// Covers <see cref="IAnilistTag.AllAnilistAnime"/>, which lists the anime an
/// AniList tag is set on through the tag's anime cross-references.
/// </summary>
[Collection(nameof(RepoFactoryCollection))]
public class AnilistTagAnimeTests
{
    private const int TagID = 10;

    private const int OtherTagID = 20;

    private static Anilist_Anime Anime(int id)
        => new() { AnilistAnimeID = id, Anilist_AnimeID = id };

    private static Anilist_Anime_Tag AnimeTag(int id, int animeID, int tagID)
        => new(animeID, tagID) { Anilist_Anime_TagID = id };

    private static RepoFactoryScope Scope(params Anilist_Anime_Tag[] animeTags)
        => new RepoFactoryScope()
            .With<Anilist_AnimeRepository, int, Anilist_Anime>(a => a.Anilist_AnimeID, [Anime(100), Anime(200), Anime(300)])
            .With<Anilist_TagRepository, int, Anilist_Tag>(t => t.Anilist_TagID,
                [new(TagID) { Anilist_TagID = 1 }, new(OtherTagID) { Anilist_TagID = 2 }])
            .With<Anilist_Anime_TagRepository, int, Anilist_Anime_Tag>(t => t.Anilist_Anime_TagID, animeTags);

    [Fact]
    public void ListsEveryAnimeTheTagIsSetOn()
    {
        using var scope = Scope(AnimeTag(1, 100, TagID), AnimeTag(2, 200, TagID), AnimeTag(3, 300, OtherTagID));

        IAnilistTag tag = RepoFactory.Anilist_Tag.GetByAnilistTagID(TagID)!;

        Assert.Equal([100, 200], tag.AllAnilistAnime.Select(anime => anime.ID).Order());
    }

    [Fact]
    public void SkipsAnimeThatAreNotStoredLocally()
    {
        using var scope = Scope(AnimeTag(1, 100, TagID), AnimeTag(2, 999, TagID));

        IAnilistTag tag = RepoFactory.Anilist_Tag.GetByAnilistTagID(TagID)!;

        var only = Assert.Single(tag.AllAnilistAnime);
        Assert.Equal(100, only.ID);
    }

    [Fact]
    public void IsEmptyForATagSetOnNothing()
    {
        using var scope = Scope(AnimeTag(1, 100, OtherTagID));

        IAnilistTag tag = RepoFactory.Anilist_Tag.GetByAnilistTagID(TagID)!;

        Assert.Empty(tag.AllAnilistAnime);
    }
}
