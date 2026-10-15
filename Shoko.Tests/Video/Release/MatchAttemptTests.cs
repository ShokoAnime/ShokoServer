using System;
using Shoko.Server.Models.Release;
using Xunit;

namespace Shoko.Tests.Video.Release;

/// <summary>
/// Unit tests for <see cref="StoredReleaseInfo_MatchAttempt"/> covering the
/// <see cref="StoredReleaseInfo_MatchAttempt.IsSuccessful"/> property and the
/// <see cref="StoredReleaseInfo_MatchAttempt.AttemptedProviderNames"/> list.
/// </summary>
public class MatchAttemptTests
{
    // ── IsSuccessful ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("AniDB", true)]
    public void IsSuccessful_IsTrueOnlyWhenTheProviderNameIsSet(string? providerName, bool expected)
    {
        var attempt = new StoredReleaseInfo_MatchAttempt { ProviderName = providerName };
        Assert.Equal(expected, attempt.IsSuccessful);
    }

    // ── AttemptedProviderNames ────────────────────────────────────────────────

    [Fact]
    public void AttemptedProviderNames_EmptyEmbedded_ReturnsEmpty()
    {
        var attempt = new StoredReleaseInfo_MatchAttempt { EmbeddedAttemptProviderNames = string.Empty };
        Assert.DoesNotContain(attempt.AttemptedProviderNames, n => !string.IsNullOrEmpty(n));
    }

    [Fact]
    public void AttemptedProviderNames_CommaSeparated_SplitsCorrectly()
    {
        var attempt = new StoredReleaseInfo_MatchAttempt { EmbeddedAttemptProviderNames = "AniDB,TMDB,MyPlugin" };
        Assert.Equal(["AniDB", "TMDB", "MyPlugin"], attempt.AttemptedProviderNames);
    }

    [Fact]
    public void AttemptedProviderNames_EqualNamesShareOneInstance()
    {
        var first = new StoredReleaseInfo_MatchAttempt { EmbeddedAttemptProviderNames = new string("Release Importer,AniDB".AsSpan()) };
        var second = new StoredReleaseInfo_MatchAttempt { EmbeddedAttemptProviderNames = new string("AniDB,Release Importer".AsSpan()) };

        var firstNames = first.AttemptedProviderNames;
        var secondNames = second.AttemptedProviderNames;

        Assert.Equal(["AniDB", "Release Importer"], secondNames);
        Assert.Same(firstNames[0], secondNames[1]);
        Assert.Same(firstNames[1], secondNames[0]);
    }
}
