using Shoko.Abstractions.Metadata;
using Shoko.Server.Models.AniDB;
using Xunit;

namespace Shoko.Tests.Models;

/// <summary>
/// Covers the votes of <see cref="AniDB_Anime_Similar"/>, whose approval
/// rating is the one <see cref="ISuggestedMetadata"/> works out from them.
/// </summary>
public class AnidbSimilarSuggestionTests
{
    [Theory]
    [InlineData(1, 3)]
    [InlineData(7, 9)]
    [InlineData(0, 5)]
    [InlineData(12, 12)]
    public void TheApprovalRatingIsWorkedOutFromTheVotes(int approval, int total)
    {
        ISuggestedMetadata suggestion = new AniDB_Anime_Similar { Approval = approval, Total = total };

        Assert.True(suggestion.HasVotes && suggestion.HasApprovalRating);
        Assert.Equal((approval, total), (suggestion.ApprovalVotes.Value, suggestion.Votes.Value));
        Assert.Equal(approval / (double)total * 100, suggestion.ApprovalRating.Value);
    }

    [Fact]
    public void WithNoVotesThereIsNoApprovalRating()
    {
        ISuggestedMetadata suggestion = new AniDB_Anime_Similar();

        Assert.True(suggestion.HasVotes);
        Assert.False(suggestion.HasApprovalRating);
    }
}
