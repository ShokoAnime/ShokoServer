using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Cached.AniDB;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Providers.AniDB;

/// <summary>
/// Covers how an AniDB anime's source material is picked from its tags, by
/// <see cref="TagFilter.GetSourceMaterial"/> and through the cached
/// <see cref="AniDB_Anime.SourceMaterial"/>.
/// </summary>
[Collection(nameof(RepoFactoryCollection))]
public class AnidbSourceMaterialTests
{
    #region Mapping

    private const int OriginalWork = 2797;
    private const int Manga = 2798;
    private const int Novel = 2799;
    private const int Game = 2800;
    private const int Rpg = 2801;
    private const int VisualNovel = 2804;
    private const int EroticGame = 2803;
    private const int Manhua = 6493;
    private const int WesternComics = 3430;
    private const int AmericanDerived = 4424;

    [Fact]
    public void NoSourceTag_IsUnknown()
    {
        Assert.Equal(SourceMaterial.Unknown, TagFilter.GetSourceMaterial([]));
        Assert.Equal(SourceMaterial.Unknown, TagFilter.GetSourceMaterial([(1, 600), (TagFilter.SourceMaterialParentTagID, 0)]));
    }

    [Theory]
    [InlineData(new[] { Game, EroticGame }, SourceMaterial.Eroge)]
    [InlineData(new[] { Game, VisualNovel, EroticGame }, SourceMaterial.Eroge)]
    [InlineData(new[] { Game, EroticGame, Rpg }, SourceMaterial.Eroge)]
    [InlineData(new[] { Game, VisualNovel }, SourceMaterial.VisualNovel)]
    [InlineData(new[] { Game, Rpg }, SourceMaterial.VideoGame)]
    public void EroticGame_MakesAnyGameAnEroge(int[] tagIDs, SourceMaterial expected)
        => Assert.Equal(expected, TagFilter.GetSourceMaterial(tagIDs.Select(id => (id, 0))));

    [Fact]
    public void EroticGame_WinsOverAHeavierVisualNovel()
        => Assert.Equal(SourceMaterial.Eroge, TagFilter.GetSourceMaterial([(VisualNovel, 600), (EroticGame, 100)]));

    [Fact]
    public void EroticGame_DoesNotOverrideAHeavierNonGameSource()
        => Assert.Equal(SourceMaterial.Manga, TagFilter.GetSourceMaterial([(EroticGame, 100), (Manga, 300)]));

    [Theory]
    [InlineData(new[] { Manga, Novel }, new[] { 0, 0 }, SourceMaterial.Manga)]
    [InlineData(new[] { Novel, Manga }, new[] { 0, 0 }, SourceMaterial.Manga)]
    [InlineData(new[] { Manga, OriginalWork }, new[] { 0, 0 }, SourceMaterial.Original)]
    [InlineData(new[] { AmericanDerived, WesternComics }, new[] { 0, 0 }, SourceMaterial.Comic)]
    [InlineData(new[] { Novel, Manhua }, new[] { 0, 0 }, SourceMaterial.Novel)]
    [InlineData(new[] { Manga, Novel }, new[] { 200, 400 }, SourceMaterial.Novel)]
    [InlineData(new[] { AmericanDerived, WesternComics }, new[] { 300, 0 }, SourceMaterial.Other)]
    public void SeveralTags_PickByWeightThenPrecedence(int[] tagIDs, int[] weights, SourceMaterial expected)
        => Assert.Equal(expected, TagFilter.GetSourceMaterial(tagIDs.Zip(weights)));

    [Fact]
    public void UnmappedChild_MapsLikeItsParent()
    {
        var parents = new Dictionary<int, int?> { [9001] = Manga, [9002] = TagFilter.SourceMaterialParentTagID, [9003] = 9001, [1] = null };
        int? Parent(int tagID) => parents.TryGetValue(tagID, out var parent) ? parent : null;

        Assert.Equal(SourceMaterial.Manga, TagFilter.GetSourceMaterial([(9001, 0)], Parent));
        Assert.Equal(SourceMaterial.Manga, TagFilter.GetSourceMaterial([(9003, 0)], Parent));
        Assert.Equal(SourceMaterial.Other, TagFilter.GetSourceMaterial([(9002, 0)], Parent));
        Assert.Equal(SourceMaterial.Unknown, TagFilter.GetSourceMaterial([(1, 0)], Parent));

        // A known tag keeps its precedence over a new one.
        Assert.Equal(SourceMaterial.Novel, TagFilter.GetSourceMaterial([(9002, 0), (Novel, 0)], Parent));
    }

    #endregion

    #region Model

    private const int AnimeID = 42;

    private static AniDB_Anime_Tag Xref(int id, int tagID, int weight = 0, bool spoiler = false)
        => new() { AniDB_Anime_TagID = id, AnimeID = AnimeID, TagID = tagID, Weight = weight, LocalSpoiler = spoiler };

    private static RepoFactoryScope Scope(AniDB_Anime anime, params AniDB_Anime_Tag[] xrefs)
        => new RepoFactoryScope()
            .With<AniDB_AnimeRepository, int, AniDB_Anime>(a => a.AniDB_AnimeID, [anime])
            .With<AnimeSeriesRepository, int, AnimeSeries>(s => s.AnimeSeriesID, [new() { AnimeSeriesID = 7, AniDB_ID = AnimeID }])
            .With<AniDB_TagRepository, int, AniDB_Tag>(t => t.AniDB_TagID,
            [
                new() { AniDB_TagID = 1, TagID = Manga, ParentTagID = TagFilter.SourceMaterialParentTagID, TagNameSource = "manga" },
                new() { AniDB_TagID = 2, TagID = Novel, ParentTagID = TagFilter.SourceMaterialParentTagID, TagNameSource = "novel" },
            ])
            .With<AniDB_Anime_TagRepository, int, AniDB_Anime_Tag>(x => x.AniDB_Anime_TagID, xrefs);

    [Fact]
    public void Anime_ReadsItsTags_AndTheShokoSeriesReportsIt()
    {
        var anime = new AniDB_Anime { AniDB_AnimeID = 1, AnimeID = AnimeID };
        using var scope = Scope(anime, Xref(1, Manga, spoiler: true));

        Assert.Equal(SourceMaterial.Manga, ((ISeries)anime).SourceMaterial);
        Assert.Equal(SourceMaterial.Manga, ((ISeries)RepoFactory.AnimeSeries.GetByID(7)!).SourceMaterial);
    }

    [Fact]
    public void Anime_KeepsItsValueUntilTheTagsAreImportedAgain()
    {
        var anime = new AniDB_Anime { AniDB_AnimeID = 1, AnimeID = AnimeID };
        using (Scope(anime, Xref(1, Manga)))
            Assert.Equal(SourceMaterial.Manga, anime.SourceMaterial);

        using (Scope(anime, Xref(2, Novel)))
        {
            Assert.Equal(SourceMaterial.Manga, anime.SourceMaterial);

            anime.ResetSourceMaterial();
            Assert.Equal(SourceMaterial.Novel, anime.SourceMaterial);
        }

        using (Scope(anime, Xref(1, Manga)))
        {
            // Setting the tag names, as a re-import does, also clears it.
            anime.AllTags = "manga";
            Assert.Equal(SourceMaterial.Manga, anime.SourceMaterial);
        }
    }

    #endregion
}
