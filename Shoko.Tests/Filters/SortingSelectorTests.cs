using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Filtering.Sorting;
using Shoko.Abstractions.Filtering.Sorting.Selectors;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Filters;

/// <summary>
/// Covers the sorting selectors, which had no tests of any kind. Each one projects a filterable to
/// the value the collection is ordered by, so a selector that throws, returns null, or quietly
/// disagrees with its own <c>TimeDependent</c>/<c>UserDependent</c> flags produces a wrong or
/// broken sort order for the user.
/// </summary>
public class SortingSelectorTests
{
    private static readonly Type[] s_selectorTypes =
    [
        .. typeof(SortingExpression).Assembly
            .GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false, IsPublic: true })
            .Where(typeof(SortingExpression).IsAssignableFrom)
            .OrderBy(t => t.FullName, StringComparer.Ordinal),
    ];

    private static readonly TestFilterable s_filterable = FilterableFactory.CreatePopulated<TestFilterable>();

    private static readonly TestFilterableUserInfo s_userInfo = FilterableFactory.CreatePopulated<TestFilterableUserInfo>();

    private static SortingExpression Create(string fullName)
        => (SortingExpression)Activator.CreateInstance(s_selectorTypes.Single(t => t.FullName == fullName))!;

    public static TheoryData<string> AllSelectors()
    {
        var data = new TheoryData<string>();
        foreach (var type in s_selectorTypes.Where(t => t.GetConstructor(Type.EmptyTypes) is not null))
            data.Add(type.FullName!);

        return data;
    }

    #region Discovery

    [Fact]
    public void TheSelectorsAreDiscovered()
    {
        // Guards the theories below from silently emptying out.
        Assert.True(s_selectorTypes.Length > 50, $"Only found {s_selectorTypes.Length} sorting selectors.");
    }

    [Fact]
    public void EverySelectorCanBeConstructedWithoutArguments()
    {
        // Stored sort orders are rebuilt by the JSON deserialiser, which needs a parameterless ctor.
        var missing = s_selectorTypes.Where(t => t.GetConstructor(Type.EmptyTypes) is null).Select(t => t.FullName).ToArray();

        Assert.Empty(missing);
    }

    #endregion

    #region Contract held by every selector

    [Theory]
    [MemberData(nameof(AllSelectors))]
    public void EverySelectorProducesAValue(string fullName)
    {
        var selector = Create(fullName);

        var result = selector.Evaluate(s_filterable, s_userInfo, s_date);

        Assert.NotNull(result);
    }

    [Theory]
    [MemberData(nameof(AllSelectors))]
    public void EverySelectorProducesSomethingComparable(string fullName)
    {
        var selector = Create(fullName);

        // The result is what the collection is ordered by, so it has to be comparable.
        Assert.IsAssignableFrom<IComparable>(selector.Evaluate(s_filterable, s_userInfo, s_date));
    }

    [Theory]
    [MemberData(nameof(AllSelectors))]
    public void EverySelectorIsDeterministic(string fullName)
    {
        var selector = Create(fullName);

        Assert.Equal(
            selector.Evaluate(s_filterable, s_userInfo, s_date),
            selector.Evaluate(s_filterable, s_userInfo, s_date));
    }

    [Theory]
    [MemberData(nameof(AllSelectors))]
    public void ASelectorThatIsNotUserDependentDoesNotNeedUserInfo(string fullName)
    {
        var selector = Create(fullName);
        if (selector.UserDependent)
            return;

        // Filters are evaluated without user info for user-independent expressions, so these must
        // cope with a null userInfo rather than throwing.
        Assert.Equal(
            selector.Evaluate(s_filterable, s_userInfo, s_date),
            selector.Evaluate(s_filterable, null, s_date));
    }

    [Theory]
    [MemberData(nameof(AllSelectors))]
    public void ASelectorThatIsNotTimeDependentIgnoresTheTime(string fullName)
    {
        var selector = Create(fullName);
        if (selector.TimeDependent)
            return;

        // A selector whose value moves with the clock while claiming otherwise defeats the caching
        // that the filtering engine does on the strength of that flag.
        Assert.Equal(
            selector.Evaluate(s_filterable, s_userInfo, s_date),
            selector.Evaluate(s_filterable, s_userInfo, s_date.AddYears(5)));
    }

    #endregion

    #region Representative values

    private static readonly DateTime s_date = new(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc);

    [Fact]
    public void AddedDateSelector_ReturnsTheAddedDate()
        => Assert.Equal(s_filterable.AddedDate, new AddedDateSortingSelector().Evaluate(s_filterable, s_userInfo, s_date));

    [Fact]
    public void MissingEpisodeCountSelector_ReturnsTheMissingEpisodeCount()
        => Assert.Equal(s_filterable.MissingEpisodes, new MissingEpisodeCountSortingSelector().Evaluate(s_filterable, s_userInfo, s_date));

    [Fact]
    public void Descending_DefaultsToAscending()
        => Assert.False(new AddedDateSortingSelector().Descending);

    [Fact]
    public void Next_DefaultsToNoFurtherSort()
        => Assert.Null(new AddedDateSortingSelector().Next);

    #endregion
}
