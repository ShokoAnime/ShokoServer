using Shoko.Server.Databases;
using Shoko.Server.MediaInfo;
using Xunit;

namespace Shoko.Tests.Databases;

public class RegionTaggedStreamLanguageRepairTests
{
    [Theory]
    [InlineData("ja-JP", "jpn", "Japanese")]
    [InlineData("es-419", "spa", "Spanish")]
    [InlineData("pt_BR", "por", "Portuguese")]
    public void ARegionTaggedLanguageWithoutACodeGetsOne(string language, string code, string name)
    {
        var stream = new AudioStream { Language = language };

        Assert.True(DatabaseFixes.RepairRegionTaggedStreamLanguage(stream));

        Assert.Equal(language, stream.Language);
        Assert.Equal(code, stream.LanguageCode);
        Assert.Equal(name, stream.LanguageName);
    }

    [Fact]
    public void AVideoStreamIsRepairedToo()
    {
        var stream = new VideoStream { Language = "ja-JP" };

        Assert.True(DatabaseFixes.RepairRegionTaggedStreamLanguage(stream));

        Assert.Equal("jpn", stream.LanguageCode);
    }

    [Fact]
    public void AStreamWithACodeIsLeftAlone()
    {
        var stream = new TextStream { Language = "ja-JP", LanguageCode = "jpn", LanguageName = "Japanese" };

        Assert.False(DatabaseFixes.RepairRegionTaggedStreamLanguage(stream));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("zz-ZZ")]
    public void AStreamWithoutAKnownLanguageIsLeftAlone(string? language)
    {
        var stream = new AudioStream { Language = language };

        Assert.False(DatabaseFixes.RepairRegionTaggedStreamLanguage(stream));
        Assert.Null(stream.LanguageCode);
    }
}
