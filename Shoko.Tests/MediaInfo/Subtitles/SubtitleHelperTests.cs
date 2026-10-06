using Shoko.Server.MediaInfo.Subtitles;
using Xunit;

namespace Shoko.Tests.MediaInfo.Subtitles;

public class SubtitleHelperTests
{
    [Theory]
    [InlineData("[Group] Show - 01.eng.srt", "en")]
    [InlineData("[Group] Show - 01_eng.ass", "en")]
    [InlineData("[Group] Show - 01_eng_1.ass", "en")]
    // A literal "..." in the title must not be mistaken for the dot-separated
    // "filename.eng.srt" convention and leak the raw trailing segment.
    [InlineData("[AnoZu] Show to... 05 ---- Is All You Need (1080p Web) [DUAL-AUDIO] [77D3B345]_por.ass", "pt")]
    [InlineData("[AnoZu] Show to... 05 ---- Is All You Need (1080p Web) [DUAL-AUDIO] [77D3B345]_por_1.ass", "pt")]
    // Region-tagged names keep their region, with a three-letter language folded to two letters.
    [InlineData("[Group] Show - 01_pt-BR.ass", "pt-BR")]
    [InlineData("[Group] Show - 01_es-419_1.ass", "es-419")]
    [InlineData("[Group] Show - 01_spa-ES.ass", "es-ES")]
    [InlineData("[Group] Show - 01_spa-419.ass", "es-419")]
    // A segment shaped like a region tag is only a language when its first part is one.
    [InlineData("[Group] Show - 01_OP-NC.ass", null)]
    [InlineData("[Group] Show - 01_Do-Re.srt", null)]
    [InlineData("no language here.srt", null)]
    public void GetLanguageFromFilename_ExtractsExpectedLanguage(string filename, string? expected)
        => Assert.Equal(expected, SubtitleHelper.GetLanguageFromFilename(filename));
}
