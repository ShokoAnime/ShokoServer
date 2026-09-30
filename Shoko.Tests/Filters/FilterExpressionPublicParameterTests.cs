using System.Collections.Generic;
using System.Globalization;
using Shoko.Abstractions.Filtering.Expressions;
using Shoko.Abstractions.Filtering.Expressions.Info;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Databases.NHibernate;
using Xunit;

namespace Shoko.Tests.Filters;

/// <summary>
/// The five expressions that kept their parameters behind explicit interface members only, so a
/// saved preset using one came back without them: <see cref="InSeasonExpression"/>,
/// <see cref="HasCharacterExpression"/>, <see cref="HasCharacterWithAppearanceExpression"/>,
/// <see cref="HasCreatorExpression"/> and <see cref="HasCreatorWithRoleExpression"/>. Their stored
/// JSON uses the property names they had before, so filters saved by older versions load too.
/// </summary>
public class FilterExpressionPublicParameterTests
{
    /// <summary>
    /// A series that aired in summer 2019, with character 101 as a main character and creator 202
    /// as its director.
    /// </summary>
    private static readonly TestFilterable s_filterable = new()
    {
        Seasons = new HashSet<(int, YearlySeason)> { (2019, YearlySeason.Summer) },
        CharacterIDs = new HashSet<string> { "101" },
        CharacterAppearances = new Dictionary<CastRoleType, IReadOnlySet<string>> { [CastRoleType.MainCharacter] = new HashSet<string> { "101" } },
        CreatorIDs = new HashSet<string> { "202" },
        CreatorRoles = new Dictionary<CrewRoleType, IReadOnlySet<string>> { [CrewRoleType.Director] = new HashSet<string> { "202" } },
    };

    #region Stored JSON

    public static TheoryData<FilterExpression<bool>, string[], FilterExpression<bool>?> StoredExpressions() => new()
    {
        { new InSeasonExpression(2019, YearlySeason.Summer), ["\"Year\":2019", "\"Season\":\"Summer\""], new InSeasonExpression(2019, YearlySeason.Fall) },
        { new HasCharacterExpression("101"), ["\"CharacterID\":\"101\""], null },
        {
            new HasCharacterWithAppearanceExpression("101", CastRoleType.MainCharacter),
            ["\"CharacterID\":\"101\"", "\"Appearance\":\"MainCharacter\""],
            new HasCharacterWithAppearanceExpression("101", CastRoleType.Cameo)
        },
        { new HasCreatorExpression("202"), ["\"CreatorID\":\"202\""], null },
        {
            new HasCreatorWithRoleExpression("202", CrewRoleType.Director),
            ["\"CreatorID\":\"202\"", "\"Role\":\"Director\""],
            new HasCreatorWithRoleExpression("202", CrewRoleType.Music)
        },
    };

    [Theory]
    [MemberData(nameof(StoredExpressions))]
    public void TheParametersAreStoredUnderTheirOldNamesAndStillMatchOnceLoaded(FilterExpression<bool> expression, string[] storedParameters, FilterExpression<bool>? other)
    {
        var (json, restored) = RoundTrip(expression);

        Assert.All(storedParameters, parameter => Assert.Contains(parameter, json));
        Assert.True(restored.Evaluate(s_filterable, null, null));
        if (other is not null)
            Assert.False(RoundTrip(other).Restored.Evaluate(s_filterable, null, null));
    }

    #endregion

    #region Older stored JSON

    [Fact]
    public void InSeason_LoadsTheShapeVersion5Saved()
    {
        // Version 5 kept the season as its number.
        var restored = Load<InSeasonExpression>("{\"$type\":\"InSeasonExpression\",\"Year\":2019,\"Season\":2}");

        Assert.Equal(2019, restored.Year);
        Assert.Equal(YearlySeason.Summer, restored.Season);
        Assert.True(restored.Evaluate(s_filterable, null, null));
    }

    [Fact]
    public void HasCharacterWithAppearance_LoadsTheNamesVersion5Saved()
    {
        // Version 5 wrote the appearance with underscores.
        var restored = Load<HasCharacterWithAppearanceExpression>(
            "{\"$type\":\"HasCharacterWithAppearanceExpression\",\"CharacterID\":\"101\",\"Appearance\":\"Main_Character\"}");

        Assert.Equal("101", restored.CharacterID);
        Assert.Equal(CastRoleType.MainCharacter, restored.Appearance);
        Assert.True(restored.Evaluate(s_filterable, null, null));
    }

    [Theory]
    [InlineData("Staff")]
    [InlineData("Studio")]
    [InlineData("NotARole")]
    public void HasCreatorWithRole_LoadsARoleThatNoLongerExistsAsNone(string role)
    {
        var restored = Load<HasCreatorWithRoleExpression>(
            $"{{\"$type\":\"HasCreatorWithRoleExpression\",\"CreatorID\":\"202\",\"Role\":\"{role}\"}}");

        Assert.Equal("202", restored.CreatorID);
        Assert.Equal(CrewRoleType.None, restored.Role);

        // None still matches the creators a series files under it, staff and studios among them; this
        // series only credits creator 202 in other roles.
        Assert.False(restored.Evaluate(s_filterable, null, null));
    }

    [Theory]
    [InlineData(nameof(InSeasonExpression))]
    [InlineData(nameof(HasCharacterExpression))]
    [InlineData(nameof(HasCharacterWithAppearanceExpression))]
    [InlineData(nameof(HasCreatorExpression))]
    [InlineData(nameof(HasCreatorWithRoleExpression))]
    public void OneSavedWithoutItsParametersLoadsAndMatchesNothing(string typeName)
    {
        // What the broken versions wrote. The parameters are gone for good; the filter still loads.
        var restored = Load<FilterExpression<bool>>($"{{\"$type\":\"{typeName}\"}}");

        Assert.Equal(typeName, restored.GetType().Name);
        Assert.False(restored.Evaluate(s_filterable, null, null));
    }

    #endregion

    #region Helpers

    private static (string Json, T Restored) RoundTrip<T>(T expression) where T : FilterExpression
    {
        var json = (string)new FilterExpressionConverter().ConvertTo(null, CultureInfo.InvariantCulture, expression, typeof(string))!;
        return (json, Load<T>(json));
    }

    private static T Load<T>(string json) where T : FilterExpression
        => Assert.IsAssignableFrom<T>(new FilterExpressionConverter().ConvertFrom(null, CultureInfo.InvariantCulture, json));

    #endregion
}
