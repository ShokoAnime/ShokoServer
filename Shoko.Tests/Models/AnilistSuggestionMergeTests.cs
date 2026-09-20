using System.Linq;
using Shoko.Server.Models.Anilist;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.Cached.Anilist;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Models;

/// <summary>
/// Covers <see cref="Anilist_Anime.Suggestions"/> merging both stored directions
/// into one list.
/// </summary>
/// <remarks>
/// AniList holds one undirected recommendation and serves it from both sides
/// with the same score, so an edge stored while fetching one anime belongs to
/// the other as well. Measured against the live API on 2026-09-20: the top five
/// recommendations on anime 1 each named it back with an identical rating. These
/// tests pin the consequences of relying on that, including which copy wins when
/// both were stored.
/// </remarks>
[Collection(nameof(RepoFactoryCollection))]
public class AnilistSuggestionMergeTests
{
    private const int Subject = 100;

    private const int Outgoing = 200;

    private const int Incoming = 300;

    private static Anilist_Anime Anime(int id)
        => new() { AnilistAnimeID = id, Anilist_AnimeID = id };

    private static Anilist_Anime_Suggestion Suggestion(int id, int from, int to, int rating, int ordering)
        => new(from, to, rating, ordering) { Anilist_Anime_SuggestionID = id };

    private static RepoFactoryScope Scope(params Anilist_Anime_Suggestion[] suggestions)
        => new RepoFactoryScope()
            .With<Anilist_AnimeRepository, int, Anilist_Anime>(a => a.Anilist_AnimeID,
                [Anime(Subject), Anime(Outgoing), Anime(Incoming)])
            .With<Anilist_Anime_SuggestionRepository, int, Anilist_Anime_Suggestion>(s => s.Anilist_Anime_SuggestionID, suggestions);

    [Fact]
    public void AnEdgeStoredFromTheOtherSideIsStillASuggestion()
    {
        using var scope = Scope(Suggestion(1, Incoming, Subject, rating: 50, ordering: 7));

        var suggestions = RepoFactory.Anilist_Anime.GetByAnilistAnimeID(Subject)!.Suggestions;

        var only = Assert.Single(suggestions);
        Assert.Equal(Subject, only.AnilistAnimeID);
        Assert.Equal(Incoming, only.SuggestedAnilistAnimeID);
        Assert.Equal(50, only.Rating);
    }

    [Fact]
    public void BothDirectionsAreMergedIntoOneListOrderedByRating()
    {
        using var scope = Scope(
            Suggestion(1, Subject, Outgoing, rating: 10, ordering: 0),
            Suggestion(2, Incoming, Subject, rating: 90, ordering: 3));

        var suggestions = RepoFactory.Anilist_Anime.GetByAnilistAnimeID(Subject)!.Suggestions;

        Assert.Equal([Incoming, Outgoing], suggestions.Select(s => s.SuggestedAnilistAnimeID));
        Assert.All(suggestions, suggestion => Assert.Equal(Subject, suggestion.AnilistAnimeID));
    }

    [Fact]
    public void TheDirectCopyWinsWhenBothWereStored()
    {
        // Both ends fetched, so the same edge is on disk twice. Only the direct
        // row carries this side's own position, so it is the one to survive.
        using var scope = Scope(
            Suggestion(1, Subject, Outgoing, rating: 40, ordering: 1),
            Suggestion(2, Outgoing, Subject, rating: 40, ordering: 9));

        var suggestions = RepoFactory.Anilist_Anime.GetByAnilistAnimeID(Subject)!.Suggestions;

        var only = Assert.Single(suggestions);
        Assert.Equal(Outgoing, only.SuggestedAnilistAnimeID);
        Assert.Equal(1, only.Ordering);
    }

    [Fact]
    public void SuggestedByIsTheSameSetWithTheEndsSwapped()
    {
        using var scope = Scope(
            Suggestion(1, Subject, Outgoing, rating: 10, ordering: 0),
            Suggestion(2, Incoming, Subject, rating: 90, ordering: 3));

        var anime = RepoFactory.Anilist_Anime.GetByAnilistAnimeID(Subject)!;

        Assert.Equal(anime.Suggestions.Count, anime.SuggestedBy.Count);
        Assert.All(anime.SuggestedBy, suggestion => Assert.Equal(Subject, suggestion.SuggestedAnilistAnimeID));
        Assert.Equal(
            anime.Suggestions.Select(s => s.SuggestedAnilistAnimeID).Order(),
            anime.SuggestedBy.Select(s => s.AnilistAnimeID).Order());
    }

    [Fact]
    public void ReversingTwiceGivesBackTheSameEdge()
    {
        var suggestion = Suggestion(1, Subject, Outgoing, rating: 25, ordering: 4);

        var roundTripped = suggestion.Reversed.Reversed;

        Assert.Equal(suggestion.AnilistAnimeID, roundTripped.AnilistAnimeID);
        Assert.Equal(suggestion.SuggestedAnilistAnimeID, roundTripped.SuggestedAnilistAnimeID);
        Assert.Equal(suggestion.Rating, roundTripped.Rating);
        Assert.Equal(suggestion.Ordering, roundTripped.Ordering);
    }
}
