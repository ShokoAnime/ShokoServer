using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Http;
using Moq;
using Newtonsoft.Json;
using Shoko.Abstractions.Filtering.Expressions;
using Shoko.Abstractions.Filtering.Expressions.Containers;
using Shoko.Abstractions.Filtering.Services;
using Shoko.Abstractions.Filtering.Sorting;
using Shoko.Server.API.v3.Helpers;
using Shoko.Server.API.v3.Models.Shoko;
using Shoko.Server.Databases.NHibernate;
using Xunit;

namespace Shoko.Tests.Filters;

/// <summary>
/// Every expression has to keep all of its parameters through the shapes a filter is kept in:
/// the JSON <see cref="FilterExpressionConverter"/> stores for a preset, and APIv3's
/// <see cref="Filter.FilterCondition"/> and <see cref="Filter.SortingCriteria"/> trees. Each
/// expression is filled and read back through its parameter containers, so a parameter kept only
/// behind an explicit interface member, which the stored JSON never sees, fails here.
/// </summary>
public class FilterExpressionParameterRoundTripTests
{
    private static readonly DateTime s_date = new(2021, 3, 4, 5, 6, 7, DateTimeKind.Utc);

    private static readonly Type[] s_expressionTypes =
    [
        .. typeof(FilterExpression).Assembly
            .GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false, IsPublic: true })
            .Where(t => !t.Name.Contains('<'))
            .Where(typeof(FilterExpression).IsAssignableFrom)
            .OrderBy(t => t.FullName, StringComparer.Ordinal),
    ];

    private static readonly FilterFactory s_factory = new(Mock.Of<IHttpContextAccessor>(), Mock.Of<IFilteringEngine>());

    private static readonly MethodInfo s_conditionToExpression = typeof(FilterFactory)
        .GetMethods()
        .Single(m => m is { Name: nameof(FilterFactory.GetExpressionTree), IsGenericMethodDefinition: true });

    public static TheoryData<string> Expressions()
    {
        var data = new TheoryData<string>();
        foreach (var type in s_expressionTypes.Where(t => t != typeof(FilterExpression) && !typeof(SortingExpression).IsAssignableFrom(t)))
            data.Add(type.FullName!);

        return data;
    }

    public static TheoryData<string> SortingExpressions()
    {
        var data = new TheoryData<string>();
        foreach (var type in s_expressionTypes.Where(typeof(SortingExpression).IsAssignableFrom))
            data.Add(type.FullName!);

        return data;
    }

    #region Round trips

    [Theory]
    [MemberData(nameof(Expressions))]
    [MemberData(nameof(SortingExpressions))]
    public void EveryParameterSurvivesTheStoredJson(string fullName)
    {
        var original = Fill(Resolve(fullName));

        var restored = ThroughStoredJson(original);

        Assert.Equal(Describe(original), Describe(restored));
    }

    [Theory]
    [MemberData(nameof(Expressions))]
    public void EveryParameterSurvivesTheApiFilterCondition(string fullName)
    {
        var type = Resolve(fullName);
        var original = Fill(type);

        var condition = s_factory.GetExpressionTree(original);
        var wire = JsonConvert.DeserializeObject<Filter.FilterCondition>(JsonConvert.SerializeObject(condition));
        var restored = (FilterExpression?)s_conditionToExpression
            .MakeGenericMethod(ResultType(type))
            .Invoke(s_factory, [wire]);

        Assert.Equal(Describe(original), Describe(restored));
    }

    [Theory]
    [MemberData(nameof(SortingExpressions))]
    public void EveryParameterSurvivesTheApiSortingCriteria(string fullName)
    {
        var original = (SortingExpression)Fill(Resolve(fullName));

        var criteria = s_factory.GetSortingCriteria(original);
        var wire = JsonConvert.DeserializeObject<Filter.SortingCriteria>(JsonConvert.SerializeObject(criteria))!;
        var restored = s_factory.GetSortingCriteria(wire);

        Assert.Equal(Describe(original), Describe(restored));
    }

    #endregion

    #region Helpers

    private static Type Resolve(string fullName)
        => s_expressionTypes.Single(t => t.FullName == fullName);

    private static FilterExpression? ThroughStoredJson(FilterExpression expression)
    {
        var converter = new FilterExpressionConverter();
        var json = converter.ConvertTo(null, CultureInfo.InvariantCulture, expression, typeof(string));
        return (FilterExpression?)converter.ConvertFrom(null, CultureInfo.InvariantCulture, json!);
    }

    /// <summary>
    /// The <c>T</c> of the <see cref="FilterExpression{T}"/> the type derives from.
    /// </summary>
    private static Type ResultType(Type type)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(FilterExpression<>))
                return current.GetGenericArguments()[0];
        }

        throw new InvalidOperationException($"{type.Name} is not a typed filter expression.");
    }

    /// <summary>
    /// Whether the type takes no parameter or child of any kind.
    /// </summary>
    private static bool IsLeaf(Type type)
        => !type.GetInterfaces().Any(i => i.Namespace == typeof(IWithStringParameter).Namespace);

    /// <summary>
    /// A leaf expression giving <typeparamref name="T"/>; the first or the last one found, so the
    /// left and right children differ and a swap shows.
    /// </summary>
    private static FilterExpression<T> Leaf<T>(bool last)
    {
        var leaves = s_expressionTypes
            .Where(t => typeof(FilterExpression<T>).IsAssignableFrom(t) && !typeof(SortingExpression).IsAssignableFrom(t) && IsLeaf(t))
            .ToArray();
        return (FilterExpression<T>)Activator.CreateInstance(last ? leaves[^1] : leaves[0])!;
    }

    /// <summary>
    /// Creates the expression and sets every parameter and child it takes to a value other than
    /// its default, so a parameter that comes back empty shows.
    /// </summary>
    private static FilterExpression Fill(Type type)
    {
        var expression = (FilterExpression)Activator.CreateInstance(type)!;

        if (expression is IWithExpressionParameter expressionLeft)
            expressionLeft.Left = Leaf<bool>(last: false);
        if (expression is IWithDateSelectorParameter dateLeft)
            dateLeft.Left = Leaf<DateTime?>(last: false);
        if (expression is IWithNumberSelectorParameter numberLeft)
            numberLeft.Left = Leaf<double>(last: false);
        if (expression is IWithStringSelectorParameter stringLeft)
            stringLeft.Left = Leaf<string>(last: false);
        if (expression is IWithStringSetSelectorParameter stringSetLeft)
            stringSetLeft.Left = Leaf<IReadOnlySet<string>>(last: false);

        if (expression is IWithSecondExpressionParameter expressionRight)
            expressionRight.Right = Leaf<bool>(last: true);
        if (expression is IWithSecondDateSelectorParameter dateRight)
            dateRight.Right = Leaf<DateTime?>(last: true);
        if (expression is IWithSecondNumberSelectorParameter numberRight)
            numberRight.Right = Leaf<double>(last: true);
        if (expression is IWithSecondStringSelectorParameter stringRight)
            stringRight.Right = Leaf<string>(last: true);

        if (expression is IWithBoolParameter boolParameter)
            boolParameter.Parameter = true;
        if (expression is IWithDateParameter dateParameter)
            dateParameter.Parameter = s_date;
        if (expression is IWithNumberParameter numberParameter)
            numberParameter.Parameter = 2021;
        if (expression is IWithTimeSpanParameter timeSpanParameter)
            timeSpanParameter.Parameter = new TimeSpan(3, 4, 5, 6);
        if (expression is IWithStringSetParameter stringSetParameter)
            stringSetParameter.Parameter = new HashSet<string> { "alpha", "beta" };
        if (expression is IWithStringParameter stringParameter)
            SetString(expression, "first-parameter", value => stringParameter.Parameter = value);
        if (expression is IWithSecondStringParameter secondStringParameter)
            SetString(expression, "second-parameter", value => secondStringParameter.SecondParameter = value);

        if (expression is SortingExpression sorting)
        {
            sorting.Descending = true;
            var next = s_expressionTypes.Where(t => typeof(SortingExpression).IsAssignableFrom(t) && t != type && IsLeaf(t)).First();
            sorting.Next = (SortingExpression)Activator.CreateInstance(next)!;
        }

        return expression;
    }

    /// <summary>
    /// Sets a string parameter, falling back to the last name of an enum the expression keeps when
    /// the parameter only takes one of those names.
    /// </summary>
    private static void SetString(FilterExpression expression, string value, Action<string> set)
    {
        try
        {
            set(value);
            return;
        }
        catch (ArgumentException)
        {
        }

        var enumNames = expression.GetType()
            .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(p => p.PropertyType.IsEnum)
            .Select(p => Enum.GetNames(p.PropertyType)[^1]);
        foreach (var name in enumNames)
        {
            try
            {
                set(name);
                return;
            }
            catch (ArgumentException)
            {
            }
        }

        throw new InvalidOperationException($"No value found for a string parameter of {expression.GetType().Name}.");
    }

    /// <summary>
    /// Reads the expression back through its parameter containers, recursively.
    /// </summary>
    private static string Describe(FilterExpression? expression)
    {
        if (expression is null)
            return "null";

        var parts = new List<string> { expression.GetType().Name };

        if (expression is IWithExpressionParameter expressionLeft)
            parts.Add($"Left={Describe(expressionLeft.Left)}");
        if (expression is IWithDateSelectorParameter dateLeft)
            parts.Add($"Left={Describe(dateLeft.Left)}");
        if (expression is IWithNumberSelectorParameter numberLeft)
            parts.Add($"Left={Describe(numberLeft.Left)}");
        if (expression is IWithStringSelectorParameter stringLeft)
            parts.Add($"Left={Describe(stringLeft.Left)}");
        if (expression is IWithStringSetSelectorParameter stringSetLeft)
            parts.Add($"Left={Describe(stringSetLeft.Left)}");

        if (expression is IWithSecondExpressionParameter expressionRight)
            parts.Add($"Right={Describe(expressionRight.Right)}");
        if (expression is IWithSecondDateSelectorParameter dateRight)
            parts.Add($"Right={Describe(dateRight.Right)}");
        if (expression is IWithSecondNumberSelectorParameter numberRight)
            parts.Add($"Right={Describe(numberRight.Right)}");
        if (expression is IWithSecondStringSelectorParameter stringRight)
            parts.Add($"Right={Describe(stringRight.Right)}");

        if (expression is IWithBoolParameter boolParameter)
            parts.Add($"Bool={boolParameter.Parameter}");
        if (expression is IWithDateParameter dateParameter)
            parts.Add($"Date={dateParameter.Parameter.ToUniversalTime():O}");
        if (expression is IWithNumberParameter numberParameter)
            parts.Add($"Number={numberParameter.Parameter.ToString(CultureInfo.InvariantCulture)}");
        if (expression is IWithTimeSpanParameter timeSpanParameter)
            parts.Add($"TimeSpan={timeSpanParameter.Parameter:c}");
        if (expression is IWithStringSetParameter stringSetParameter)
            parts.Add($"StringSet={string.Join('|', (stringSetParameter.Parameter ?? new HashSet<string>()).Order(StringComparer.Ordinal))}");
        if (expression is IWithStringParameter stringParameter)
            parts.Add($"String={stringParameter.Parameter ?? "null"}");
        if (expression is IWithSecondStringParameter secondStringParameter)
            parts.Add($"SecondString={secondStringParameter.SecondParameter ?? "null"}");

        if (expression is SortingExpression sorting)
        {
            parts.Add($"Descending={sorting.Descending}");
            parts.Add($"Next={Describe(sorting.Next)}");
        }

        return $"({string.Join(", ", parts)})";
    }

    #endregion
}
