using Shoko.Server.Databases;
using Shoko.Server.MediaInfo;
using Xunit;

namespace Shoko.Tests.Databases;

public class ExternalSubtitleLanguageRepairTests
{
    private static TextStream Stream(string? language, bool external = true, string? filename = "Show... 09 Title (1080p H264) [0CDB4BE0]_eng.ass")
        => new() { External = external, Filename = filename, Language = language, LanguageCode = "x", LanguageName = "x" };

    [Fact]
    public void ALeakedFilenameTailIsReplacedByTheLanguageInTheFilename()
    {
        var stream = Stream(" 09 Title (1080p H264) [0CDB4BE0]_eng");

        Assert.True(DatabaseFixes.RepairExternalSubtitleLanguage(stream));

        Assert.Equal("en", stream.Language);
        Assert.Equal("English", stream.LanguageName);
    }

    [Fact]
    public void ALeakedTailWithoutALanguageInTheFilenameIsCleared()
    {
        var stream = Stream(" 05 Title", filename: "Show... 05 Title.ass");

        Assert.True(DatabaseFixes.RepairExternalSubtitleLanguage(stream));

        Assert.Null(stream.Language);
        Assert.Null(stream.LanguageCode);
        Assert.Null(stream.LanguageName);
    }

    [Theory]
    [InlineData("en", true, "Show_eng.ass")]
    [InlineData("und", true, "Show_und.ass")]
    [InlineData("und", true, "Show.ass")]
    [InlineData("pt-BR", true, "Show.ass")]
    [InlineData(" 09 Title_eng", false, "Show_eng.ass")]
    [InlineData(" 09 Title_eng", true, null)]
    [InlineData(null, true, "Show_eng.ass")]
    public void AStreamThatIsFineOrCannotBeCheckedIsLeftAlone(string? language, bool external, string? filename)
    {
        var stream = Stream(language, external, filename);

        Assert.False(DatabaseFixes.RepairExternalSubtitleLanguage(stream));

        Assert.Equal(language, stream.Language);
        Assert.Equal("x", stream.LanguageCode);
    }
}
