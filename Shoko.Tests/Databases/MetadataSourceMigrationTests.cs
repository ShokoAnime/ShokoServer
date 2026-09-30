using Shoko.Server.Databases;
using Xunit;

namespace Shoko.Tests.Databases;

/// <summary>
/// The old enum spellings the database migrations turn into source values,
/// ignoring case and spacing, next to values and aliases read as they are.
/// </summary>
public class MetadataSourceMigrationTests
{
    #region Old Spellings

    [Theory]
    [InlineData("tmdb", "tmdb")]
    [InlineData("trakttv", "trakt")]
    [InlineData(" locallygenerated ", "generated")]
    [InlineData("themoviedb", "tmdb")]
    [InlineData("None", null)]
    [InlineData("Locally Generated", null)]
    [InlineData("", null)]
    [InlineData("not a source", null)]
    public void AnOldName_ConvertsToItsValue(string text, string? expected)
        => Assert.Equal(expected, DatabaseFixes.ConvertOldSourceName(text));

    #endregion
}
