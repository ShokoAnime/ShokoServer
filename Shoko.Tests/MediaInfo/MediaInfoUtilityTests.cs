using Shoko.Server.MediaInfo;
using Xunit;

namespace Shoko.Tests.MediaInfo;

public class MediaInfoUtilityTests
{
    [Theory]
    [InlineData("ja", "jpn", "Japanese")]
    [InlineData("ja-JP", "jpn", "Japanese")]
    [InlineData("en-US", "eng", "English")]
    [InlineData("es-419", "spa", "Spanish")]
    [InlineData("zh-Hans", "chi", "Chinese")]
    [InlineData("JA-JP", "jpn", "Japanese")]
    [InlineData("Es-419", "spa", "Spanish")]
    [InlineData("pt_BR", "por", "Portuguese")]
    [InlineData("EN", "eng", "English")]
    [InlineData("zh-Hant-HK", "chi", "Chinese")]
    [InlineData("ar-001", "ara", "Arabic")]
    [InlineData("pt-PT", "por", "Portuguese")]
    [InlineData("ms-MY", "may", "Malay")]
    [InlineData("id-ID", "ind", "Indonesian")]
    [InlineData("vi-VN", "vie", "Vietnamese")]
    public void GetLanguageMapping_ResolvesBareAndRegionTaggedCodes(string language, string code, string name)
    {
        var mapping = MediaInfoUtility.GetLanguageMapping(language);

        Assert.NotNull(mapping);
        Assert.Equal(code, mapping.Item1);
        Assert.Equal(name, mapping.Item2);
    }

    [Theory]
    [InlineData("zz")]
    [InlineData("zz-YY")]
    [InlineData("-US")]
    [InlineData("")]
    public void GetLanguageMapping_UnknownCodes_ReturnNull(string language)
        => Assert.Null(MediaInfoUtility.GetLanguageMapping(language));
}
