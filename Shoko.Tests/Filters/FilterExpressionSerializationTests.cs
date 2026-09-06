using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Shoko.Abstractions.Filtering.Expressions;
using Shoko.Abstractions.Filtering.Expressions.Info;
using Shoko.Abstractions.Filtering.Expressions.Logic.Expressions;
using Shoko.Abstractions.Filtering.Expressions.User;
using Shoko.Abstractions.Filtering.Sorting;
using Shoko.Server.Databases.NHibernate;
using Xunit;

namespace Shoko.Tests.Filters;

/// <summary>
/// Guards the persistence format of saved filters. <c>FilterPreset.Expression</c> and
/// <c>FilterPreset.SortingExpression</c> are stored as JSON written by
/// <see cref="FilterExpressionConverter"/>, which records each node by its <em>simple</em> class
/// name via <see cref="SimpleNameSerializationBinder"/>.
/// </summary>
/// <remarks>
/// That makes the format quietly fragile in two directions, and both fail silently:
/// <see cref="SimpleNameSerializationBinder.BindToType"/> returns <see langword="null"/> for a name
/// it cannot resolve, and <see cref="FilterExpressionConverter.ConvertFrom"/> swallows the
/// resulting error, so a renamed or removed expression turns a user's filter into a broken one with
/// nothing logged at the call site. Where two types share a simple name the binder picks the first
/// match it happens to find, which can bind a saved filter to the wrong expression entirely.
/// </remarks>
public class FilterExpressionSerializationTests
{
    /// <summary>Every concrete node that can legitimately appear in a stored filter.</summary>
    private static readonly Type[] s_expressionTypes =
    [
        .. typeof(FilterExpression).Assembly
            .GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false, IsPublic: true })
            .Where(t => !t.Name.Contains('<'))
            .Where(typeof(FilterExpression).IsAssignableFrom)
            .OrderBy(t => t.FullName, StringComparer.Ordinal),
    ];

    private static Type Resolve(string fullName)
        => s_expressionTypes.Single(t => t.FullName == fullName);

    public static TheoryData<string> AllExpressions()
    {
        var data = new TheoryData<string>();
        foreach (var type in s_expressionTypes)
            data.Add(type.FullName!);

        return data;
    }

    public static TheoryData<string> ConstructibleExpressions()
    {
        var data = new TheoryData<string>();
        foreach (var type in s_expressionTypes.Where(t => t.GetConstructor(Type.EmptyTypes) is not null))
            data.Add(type.FullName!);

        return data;
    }

    #region Discovery

    [Fact]
    public void TheExpressionTypesAreDiscovered()
    {
        // Guards the theories below from silently becoming empty if the assembly or hierarchy moves.
        Assert.True(s_expressionTypes.Length > 250, $"Only found {s_expressionTypes.Length} expression types.");
        Assert.Contains(s_expressionTypes, t => typeof(SortingExpression).IsAssignableFrom(t));
    }

    [Fact]
    public void TheSortingSelectorsAreIncluded()
    {
        // SortingExpression derives from FilterExpression<object>, so the sorting column shares this
        // binder. If that hierarchy changes, saved sort orders stop resolving.
        var selectors = s_expressionTypes.Where(t => typeof(SortingExpression).IsAssignableFrom(t)).ToArray();

        Assert.True(selectors.Length > 50, $"Only found {selectors.Length} sorting selectors.");
    }

    #endregion

    #region Name binding

    [Fact]
    public void NoTwoExpressionsShareASimpleName()
    {
        var collisions = s_expressionTypes
            .GroupBy(t => t.Name, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => $"{g.Key}: {string.Join(", ", g.Select(t => t.FullName))}")
            .ToArray();

        // The binder resolves by simple name and takes the first match, so a collision would bind
        // stored filters to an arbitrary one of the two types.
        Assert.Empty(collisions);
    }

    [Theory]
    [MemberData(nameof(AllExpressions))]
    public void EveryExpressionBindsBackToItsOwnType(string fullName)
    {
        var type = Resolve(fullName);
        var binder = new SimpleNameSerializationBinder(typeof(FilterExpression));

        binder.BindToName(type, out _, out var typeName);

        Assert.NotNull(typeName);
        Assert.Same(type, binder.BindToType(assemblyName: null, typeName!));
    }

    [Fact]
    public void AnUnrecognisedNameDoesNotBind()
    {
        var binder = new SimpleNameSerializationBinder(typeof(FilterExpression));

        // This is what a renamed or deleted expression looks like on load. It resolves to nothing,
        // and the converter turns that into a silently broken filter rather than an error.
        Assert.Null(binder.BindToType(assemblyName: null, "AnExpressionThatNoLongerExists"));
    }

    [Fact]
    public void ATypeOutsideTheExpressionHierarchyDoesNotBind()
    {
        var binder = new SimpleNameSerializationBinder(typeof(FilterExpression));

        Assert.Null(binder.BindToType(assemblyName: null, nameof(String)));
    }

    #endregion

    #region Round trip

    [Theory]
    [MemberData(nameof(ConstructibleExpressions))]
    public void EveryConstructibleExpressionSurvivesARoundTrip(string fullName)
    {
        var type = Resolve(fullName);
        var converter = new FilterExpressionConverter();
        var original = Activator.CreateInstance(type)!;

        var json = converter.ConvertTo(null, CultureInfo.InvariantCulture, original, typeof(string));
        Assert.NotNull(json);

        var restored = converter.ConvertFrom(null, CultureInfo.InvariantCulture, json!);

        Assert.NotNull(restored);
        Assert.IsType(type, restored);
    }

    [Fact]
    public void ANestedExpressionTreeSurvivesARoundTrip()
    {
        var converter = new FilterExpressionConverter();
        var original = new AndExpression(new HasWatchedEpisodesExpression(), new NotExpression(new HasTagExpression("comedy")));

        var json = converter.ConvertTo(null, CultureInfo.InvariantCulture, original, typeof(string));
        var restored = Assert.IsType<AndExpression>(converter.ConvertFrom(null, CultureInfo.InvariantCulture, json!));

        // The tree, not just the root, has to come back intact.
        Assert.IsType<HasWatchedEpisodesExpression>(restored.Left);
        var not = Assert.IsType<NotExpression>(restored.Right);
        Assert.Equal("comedy", Assert.IsType<HasTagExpression>(not.Left).Parameter);
    }

    [Fact]
    public void ConvertTo_WritesOnlyTheSimpleTypeName()
    {
        var converter = new FilterExpressionConverter();

        var json = (string)converter.ConvertTo(null, CultureInfo.InvariantCulture, new HasWatchedEpisodesExpression(), typeof(string))!;

        // Assembly-qualified names would tie stored filters to an assembly version.
        Assert.Contains("\"$type\": \"HasWatchedEpisodesExpression\"", json.Replace("\"$type\":\"", "\"$type\": \""));
        Assert.DoesNotContain("Shoko.Abstractions,", json);
    }

    [Fact]
    public void ConvertTo_ReturnsNullForANullExpression()
        => Assert.Null(new FilterExpressionConverter().ConvertTo(null, CultureInfo.InvariantCulture, null, typeof(string)));

    #endregion
}
