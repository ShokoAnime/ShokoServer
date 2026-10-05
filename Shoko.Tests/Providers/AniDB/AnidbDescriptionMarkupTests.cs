using Shoko.Server.Providers.AniDB;
using Xunit;

namespace Shoko.Tests.Providers.AniDB;

/// <summary>
/// Covers which AniDB descriptions hold only notes: a short synopsis must never count as one.
/// </summary>
public class AnidbDescriptionMarkupTests
{
    [Theory]
    [InlineData("* Based on a fantasy isekai light novel series written by http://anidb.net/cr64951 [Tanaka Yuu] and illustrated by http://anidb.net/cr9167 [Llo].")]
    [InlineData("Note: Bundled with the 27th manga volume.")]
    [InlineData("[i]Note 1: Shown at an event.[/i]\n\nNote 2: Streamed later.")]
    [InlineData("* Based on a manga.\nSource: ANN")]
    [InlineData("* Based on a game.\nSources: the official site")]
    [InlineData("* Based on a novel.\nSummary written by someone")]
    [InlineData("Summary: none yet")]
    public void ADescriptionOfNotesAlone_IsNoteOnly(string description)
        => Assert.True(AnidbDescriptionMarkup.IsNoteOnly(description));

    [Theory]
    [InlineData("* Based on a manga.\nA boy finds a sword in his garden.")]
    [InlineData("A boy finds a sword.\nNote: The first episode was screened early.")]
    [InlineData("A short story about the source of a river.")]
    [InlineData("Sources say the hero will return.")]
    [InlineData("The heroine reads the note: it says to run.")]
    [InlineData("Summary of the TV series with an alternate ending")]
    [InlineData("Notes from a school trip.")]
    [InlineData("")]
    [InlineData(null)]
    public void ADescriptionWithAnySynopsis_IsNotNoteOnly(string? description)
        => Assert.False(AnidbDescriptionMarkup.IsNoteOnly(description));

    [Fact]
    public void LinksAndMarkupBecomePlainText()
        => Assert.Equal("Based on Tanaka Yuu's novel.", AnidbDescriptionMarkup.ToPlainText("[i]Based on http://anidb.net/cr64951 [Tanaka Yuu]'s novel.[/i]"));
}
