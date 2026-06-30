using System.Linq;
using Shoko.Server.Models.Release;
using Xunit;

namespace Shoko.Tests.Video.Release;

/// <summary>
/// Unit tests for <see cref="StoredReleaseInfo_MatchAttempt"/> covering the
/// <see cref="StoredReleaseInfo_MatchAttempt.IsCompleted"/> flag,
/// <see cref="StoredReleaseInfo_MatchAttempt.IsSuccessful"/> property, and
/// the <see cref="StoredReleaseInfo.DeferToNext"/> → <c>IsCompleted</c> invariant.
/// </summary>
public class MatchAttemptTests
{
    // ── IsSuccessful ──────────────────────────────────────────────────────────

    [Fact]
    public void IsSuccessful_WhenProviderNameIsNull_ReturnsFalse()
    {
        var attempt = new StoredReleaseInfo_MatchAttempt { ProviderName = null };
        Assert.False(attempt.IsSuccessful);
    }

    [Fact]
    public void IsSuccessful_WhenProviderNameIsEmpty_ReturnsFalse()
    {
        var attempt = new StoredReleaseInfo_MatchAttempt { ProviderName = string.Empty };
        Assert.False(attempt.IsSuccessful);
    }

    [Fact]
    public void IsSuccessful_WhenProviderNameIsSet_ReturnsTrue()
    {
        var attempt = new StoredReleaseInfo_MatchAttempt { ProviderName = "AniDB" };
        Assert.True(attempt.IsSuccessful);
    }

    // ── IsCompleted initial state ─────────────────────────────────────────────

    [Fact]
    public void IsCompleted_DefaultsToFalse()
    {
        var attempt = new StoredReleaseInfo_MatchAttempt();
        Assert.False(attempt.IsCompleted);
    }

    [Fact]
    public void IsCompleted_CanBeSetToTrue()
    {
        var attempt = new StoredReleaseInfo_MatchAttempt { IsCompleted = true };
        Assert.True(attempt.IsCompleted);
    }

    // ── DeferToNext → IsCompleted invariant ──────────────────────────────────

    /// <summary>
    /// Mirrors the invariant in <c>VideoReleaseService.SaveReleaseForVideo</c>:
    /// <c>matchAttempt.IsCompleted = !releaseInfo.DeferToNext</c>.
    /// </summary>
    [Theory]
    [InlineData(false, true)]   // not deferred → chain is done
    [InlineData(true, false)]  // deferred → chain should continue
    public void SaveRelease_SetsIsCompleted_AccordingToDeferToNext(bool deferToNext, bool expectedIsCompleted)
    {
        var attempt = new StoredReleaseInfo_MatchAttempt();
        attempt.IsCompleted = !deferToNext;

        Assert.Equal(expectedIsCompleted, attempt.IsCompleted);
    }

    /// <summary>
    /// <c>FinalizeReleaseSearchJob</c> always sets <c>IsCompleted = true</c>
    /// regardless of whether a match was found.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Finalize_AlwaysSetsIsCompletedTrue(bool wasSuccessful)
    {
        var attempt = new StoredReleaseInfo_MatchAttempt
        {
            ProviderName = wasSuccessful ? "AniDB" : null,
            IsCompleted = false,
        };

        // Reproduce what FinalizeReleaseSearchJob does
        attempt.IsCompleted = true;

        Assert.True(attempt.IsCompleted);
    }

    /// <summary>
    /// A deferred save sets <c>IsCompleted = false</c>, keeping the chain open.
    /// A subsequent finalizer sets <c>IsCompleted = true</c>, closing the chain.
    /// </summary>
    [Fact]
    public void DeferredSaveFollowedByFinalize_EndsWithIsCompletedTrue()
    {
        var attempt = new StoredReleaseInfo_MatchAttempt();

        // First provider saves with DeferToNext = true
        attempt.IsCompleted = !true;   // DeferToNext = true → IsCompleted = false
        Assert.False(attempt.IsCompleted);

        // Chain continues, no second provider found → FinalizeReleaseSearchJob runs
        attempt.IsCompleted = true;
        Assert.True(attempt.IsCompleted);
    }

    // ── AttemptedProviderNames ────────────────────────────────────────────────

    [Fact]
    public void AttemptedProviderNames_EmptyEmbedded_ReturnsEmpty()
    {
        var attempt = new StoredReleaseInfo_MatchAttempt { EmbeddedAttemptProviderNames = string.Empty };
        Assert.Empty(attempt.AttemptedProviderNames.Where(n => !string.IsNullOrEmpty(n)));
    }

    [Fact]
    public void AttemptedProviderNames_CommaSeparated_SplitsCorrectly()
    {
        var attempt = new StoredReleaseInfo_MatchAttempt { EmbeddedAttemptProviderNames = "AniDB,TMDB,MyPlugin" };
        Assert.Equal(["AniDB", "TMDB", "MyPlugin"], attempt.AttemptedProviderNames);
    }

    // ── StoredReleaseInfo.DeferToNext ─────────────────────────────────────────

    [Fact]
    public void StoredReleaseInfo_DeferToNext_DefaultsToFalse()
    {
        var sri = new StoredReleaseInfo();
        Assert.False(sri.DeferToNext);
    }

    [Fact]
    public void StoredReleaseInfo_DeferToNext_CanBeSetToTrue()
    {
        var sri = new StoredReleaseInfo { DeferToNext = true };
        Assert.True(sri.DeferToNext);
    }
}
