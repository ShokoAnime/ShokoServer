using Shoko.Server.Utilities;
using Xunit;

namespace Shoko.Tests.Utilities;

/// <summary>
/// Covers <see cref="TitleVariants.WithoutSequelSuffix"/>, one of the shorter forms of a title both
/// the searches and the matching engine try, and <see cref="TitleVariants.SequelNumber"/>.
/// </summary>
public class TitleVariantsTests
{
    #region Sequel suffix

    [Theory]
    [InlineData("Kaguya-sama Season 2", "Kaguya-sama")]
    [InlineData("Kaguya-sama S2", "Kaguya-sama")]
    [InlineData("Kaguya-sama 2nd Season", "Kaguya-sama")]
    [InlineData("Kaguya-sama Season IV", "Kaguya-sama")]
    [InlineData("Kaguya-sama (2019)", "Kaguya-sama")]
    [InlineData("進撃の巨人 第2期", "進撃の巨人")]
    [InlineData("進撃の巨人 第２期", "進撃の巨人")]
    [InlineData("進撃の巨人 第二期", "進撃の巨人")]
    [InlineData("進撃の巨人 第十二期", "進撃の巨人")]
    [InlineData("ゆるキャン△ 第3季", "ゆるキャン△")]
    [InlineData("進撃の巨人第2期", "進撃の巨人")]
    [InlineData("第2期", "")]
    public void WithoutSequelSuffix_CutsTheSuffix(string title, string expected)
        => Assert.Equal(expected, TitleVariants.WithoutSequelSuffix(title));

    [Theory]
    [InlineData("Kaguya-sama")]
    [InlineData("進撃の巨人")]
    [InlineData("進撃の巨人 第期")]
    [InlineData("進撃の巨人 第2話")]
    [InlineData("進撃の巨人 第2期 後編")]
    public void WithoutSequelSuffix_LeavesATitleWithoutOne(string title)
        => Assert.Null(TitleVariants.WithoutSequelSuffix(title));

    #endregion

    #region Sequel number

    [Theory]
    [InlineData("Gachiakuta", 1)]
    [InlineData("Gachiakuta 2", 2)]
    [InlineData("Gachiakuta Season 2", 2)]
    [InlineData("Gachiakuta S2", 2)]
    [InlineData("Gachiakuta 2nd Season", 2)]
    [InlineData("Gachiakuta Second Season", 2)]
    [InlineData("Gachiakuta Part 2", 2)]
    [InlineData("Gachiakuta Season 3 Part 2", 3)]
    [InlineData("Gachiakuta III", 3)]
    [InlineData("Gachiakuta Season IV", 4)]
    [InlineData("Gachiakuta II: The Subtitle", 2)]
    [InlineData("ガチアクタ 第2期", 2)]
    [InlineData("ガチアクタ 第二期", 2)]
    [InlineData("ガチアクタ 2期", 2)]
    [InlineData("からかい上手の高木さん2", 2)]
    [InlineData("Mob Psycho 100", 1)]
    [InlineData("Steins;Gate 0", 0)]
    public void SequelNumber_ReadsTheCommonForms(string title, int expected)
        => Assert.Equal(expected, TitleVariants.SequelNumber(title));

    [Fact]
    public void SequelNumber_TellsNothingFromAYear()
        => Assert.Null(TitleVariants.SequelNumber("Gachiakuta (2026)"));

    #endregion
}
