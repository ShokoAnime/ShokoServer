using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Shoko.Abstractions.Filtering;
using Shoko.Abstractions.Filtering.Expressions;
using Shoko.Abstractions.Filtering.Expressions.Containers;
using Shoko.Abstractions.Filtering.Expressions.Info;
using Shoko.Abstractions.Filtering.Expressions.Logic.Expressions;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Databases.NHibernate;
using Xunit;

namespace Shoko.Tests.Filters;

/// <summary>
/// The expressions that name the values behind their containers themselves: a saved preset
/// stores every one of them as <c>Parameter</c> and <c>SecondParameter</c>, like any other
/// expression, and gets them back.
/// </summary>
public class FilterExpressionStoredParameterTests
{
    /// <summary>
    /// A series that aired in summer 2019, with character 101 as a main character and creator 202
    /// as its director.
    /// </summary>
    private static readonly TestFilterable s_filterable = new()
    {
        Years = new HashSet<int> { 2019 },
        Seasons = new HashSet<(int, YearlySeason)> { (2019, YearlySeason.Summer) },
        CharacterIDs = new HashSet<string> { "101" },
        CharacterAppearances = new Dictionary<CastRoleType, IReadOnlySet<string>> { [CastRoleType.MainCharacter] = new HashSet<string> { "101" } },
        CreatorIDs = new HashSet<string> { "202" },
        CreatorRoles = new Dictionary<CrewRoleType, IReadOnlySet<string>> { [CrewRoleType.Director] = new HashSet<string> { "202" } },
    };

    #region Stored JSON

    public static TheoryData<FilterExpression<bool>, string[], string[], FilterExpression<bool>?> StoredExpressions() => new()
    {
        { new InSeasonExpression(2019, YearlySeason.Summer), ["\"Parameter\":2019.0", "\"SecondParameter\":\"Summer\""], ["Year", "Season"], new InSeasonExpression(2019, YearlySeason.Fall) },
        { new InYearExpression(2019), ["\"Parameter\":2019.0"], [], new InYearExpression(2020) },
        {
            new SwitchExpression(new HasCharacterExpression("101"), InclusionFilter.Only, "Mine"),
            ["\"Parameter\":\"Mine\"", "\"SecondParameter\":\"Only\""],
            ["Label", "Inclusion"],
            new SwitchExpression(new HasCharacterExpression("101"), InclusionFilter.False)
        },
        { new HasCharacterExpression("101"), ["\"Parameter\":\"101\""], ["CharacterID"], new HasCharacterExpression("102") },
        {
            new HasCharacterWithAppearanceExpression("101", CastRoleType.MainCharacter),
            ["\"Parameter\":\"101\"", "\"SecondParameter\":\"MainCharacter\""],
            ["CharacterID", "Appearance"],
            new HasCharacterWithAppearanceExpression("101", CastRoleType.Cameo)
        },
        { new HasCreatorExpression("202"), ["\"Parameter\":\"202\""], ["CreatorID"], new HasCreatorExpression("203") },
        {
            new HasCreatorWithRoleExpression("202", CrewRoleType.Director),
            ["\"Parameter\":\"202\"", "\"SecondParameter\":\"Director\""],
            ["CreatorID", "Role"],
            new HasCreatorWithRoleExpression("202", CrewRoleType.Music)
        },
    };

    [Theory]
    [MemberData(nameof(StoredExpressions))]
    public void TheValuesAreStoredAsParameterAndSecondParameterAndStillMatchOnceLoaded(
        FilterExpression<bool> expression,
        string[] storedValues,
        string[] ownNames,
        FilterExpression<bool>? other
    )
    {
        var (json, restored) = RoundTrip(expression);

        Assert.All(storedValues, value => Assert.Contains(value, json));
        Assert.All(ownNames, name => Assert.DoesNotContain($"\"{name}\"", json));
        Assert.Equal(json, RoundTrip(restored).Json);
        Assert.True(restored.Evaluate(s_filterable, null, null));
        if (other is not null)
            Assert.False(RoundTrip(other).Restored.Evaluate(s_filterable, null, null));
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

    #region Stored members

    public static TheoryData<string> AllExpressions()
    {
        var data = new TheoryData<string>();
        foreach (var type in typeof(FilterExpression).Assembly.GetTypes().Where(t => !t.IsAbstract && typeof(FilterExpression).IsAssignableFrom(t)))
            data.Add(type.FullName!);
        return data;
    }

    [Theory]
    [MemberData(nameof(AllExpressions))]
    public void EveryPublicValueIsAContainerMemberOrBacksOne(string fullName)
    {
        // Only container members are stored (and a sorting expression's place in its chain), so a
        // public value of its own must stand behind a container the class implements explicitly.
        var type = typeof(FilterExpression).Assembly.GetType(fullName)!;
        string[] stored = ["Left", "Right", "Parameter", "SecondParameter", "Descending", "Next"];
        var ownValues = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.CanWrite && property.SetMethod!.IsPublic && !stored.Contains(property.Name))
            .Select(property => property.Name)
            .ToList();
        var implementsAContainerExplicitly = type.GetInterfaces()
            .Where(container => container.Namespace == typeof(IWithBoolParameter).Namespace)
            .SelectMany(container => type.GetInterfaceMap(container).TargetMethods)
            .Any(method => method.IsPrivate);

        Assert.True(ownValues.Count is 0 || implementsAContainerExplicitly, $"{type.Name} has public values that are not stored: {string.Join(", ", ownValues)}");
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
