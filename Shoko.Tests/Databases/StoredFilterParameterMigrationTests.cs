using System.Globalization;
using Shoko.Abstractions.Filtering.Expressions;
using Shoko.Abstractions.Filtering.Expressions.Info;
using Shoko.Abstractions.Filtering.Expressions.Logic.Expressions;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Databases;
using Shoko.Server.Databases.NHibernate;
using Xunit;

namespace Shoko.Tests.Databases;

/// <summary>
/// The rewrite that moves the values stored filters kept under names of their own to
/// <c>Parameter</c> and <c>SecondParameter</c>.
/// </summary>
public class StoredFilterParameterMigrationTests
{
    #region Rewrite

    [Fact]
    public void InSeason_TheShapeVersion5Saved()
    {
        // Version 5 kept the season as its number.
        var restored = Migrate<InSeasonExpression>("{\"$type\":\"InSeasonExpression\",\"Year\":2019,\"Season\":2}");

        Assert.Equal(2019, restored.Year);
        Assert.Equal(YearlySeason.Summer, restored.Season);
    }

    [Fact]
    public void HasCharacterWithAppearance_TheNamesVersion5Saved()
    {
        // Version 5 wrote the appearance with underscores.
        var restored = Migrate<HasCharacterWithAppearanceExpression>(
            "{\"$type\":\"HasCharacterWithAppearanceExpression\",\"CharacterID\":\"101\",\"Appearance\":\"Main_Character\"}");

        Assert.Equal("101", restored.CharacterID);
        Assert.Equal(CastRoleType.MainCharacter, restored.Appearance);
    }

    [Theory]
    [InlineData("Staff")]
    [InlineData("Studio")]
    [InlineData("NotARole")]
    public void HasCreatorWithRole_ARoleThatNoLongerExistsBecomesUnset(string role)
    {
        var json = DatabaseFixes.RewriteStoredFilterParameters(
            $"{{\"$type\":\"HasCreatorWithRoleExpression\",\"CreatorID\":\"202\",\"Role\":\"{role}\"}}");
        var restored = Load<HasCreatorWithRoleExpression>(json!);

        Assert.Contains("\"SecondParameter\":null", json);
        Assert.Equal("202", restored.CreatorID);
        Assert.Null(restored.Role);
        Assert.False(restored.IsApplicable);
    }

    [Fact]
    public void EveryNodeOfATreeIsRewritten()
    {
        const string Stored = "{\"$type\":\"AndExpression\","
            + "\"Left\":{\"$type\":\"ScopeExpression\",\"Left\":{\"$type\":\"HasCreatorExpression\",\"CreatorID\":\"202\"},\"Parameter\":\"Mine\"},"
            + "\"Right\":{\"$type\":\"NotExpression\",\"Left\":{\"$type\":\"InYearExpression\",\"Parameter\":2019}}}";

        var json = DatabaseFixes.RewriteStoredFilterParameters(Stored);
        var restored = Load<AndExpression>(json!);

        var frame = Assert.IsType<ScopeExpression>(restored.Left);
        Assert.Equal("Mine", frame.Parameter);
        Assert.Equal("202", Assert.IsType<HasCreatorExpression>(frame.Left).CreatorID);
        Assert.Equal(2019, Assert.IsType<InYearExpression>(Assert.IsType<NotExpression>(restored.Right).Left).Parameter);
        Assert.DoesNotContain("\"CreatorID\"", json);
        Assert.Null(DatabaseFixes.RewriteStoredFilterParameters(json!));
    }

    [Theory]
    [InlineData("{\"$type\":\"HasTagExpression\",\"Parameter\":\"comedy\"}")]
    [InlineData("{\"$type\":\"NameSortingSelector\",\"Descending\":true,\"Next\":{\"$type\":\"AirDateSortingSelector\",\"Descending\":false,\"Next\":null}}")]
    [InlineData("{\"$type\":\"InSeasonExpression\",\"Parameter\":2019.0,\"SecondParameter\":\"Summer\"}")]
    [InlineData("not json")]
    public void JsonWithNothingToMoveIsLeftAlone(string json)
        => Assert.Null(DatabaseFixes.RewriteStoredFilterParameters(json));

    [Fact]
    public void ATypeNameWithItsNamespaceIsRecognised()
    {
        var json = DatabaseFixes.RewriteStoredFilterParameters(
            "{\"$type\":\"Shoko.Server.Filters.Info.HasCharacterExpression, Shoko.Server\",\"CharacterID\":\"101\"}");

        Assert.Contains("\"Parameter\":\"101\"", json);
    }

    #endregion

    #region Helpers

    private static T Migrate<T>(string stored) where T : FilterExpression
        => Load<T>(DatabaseFixes.RewriteStoredFilterParameters(stored)!);

    private static T Load<T>(string json) where T : FilterExpression
        => Assert.IsAssignableFrom<T>(new FilterExpressionConverter().ConvertFrom(null, CultureInfo.InvariantCulture, json));

    #endregion
}
