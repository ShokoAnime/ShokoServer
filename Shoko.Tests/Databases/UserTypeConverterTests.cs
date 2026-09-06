using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;
using NHibernate.UserTypes;
using Shoko.Abstractions.Metadata;
using Shoko.Server.Databases.NHibernate;
using Shoko.Server.MediaInfo;
using Xunit;

namespace Shoko.Tests.Databases;

/// <summary>
/// Covers the <see cref="IUserType"/> converters, which sit between the entity properties and the
/// database columns.
/// </summary>
/// <remarks>
/// Nothing else stands between a stored column and the value handed back to the application, so a
/// converter that loses information corrupts data with no error anywhere. The existing MediaInfo
/// test covers raw MessagePack, but not the converter NHibernate actually calls, and not the
/// <c>Equals</c> that decides whether a change is written back at all.
/// </remarks>
public class UserTypeConverterTests
{
    private static readonly Type[] s_converterTypes =
    [
        .. typeof(StringListConverter).Assembly
            .GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false, IsPublic: true })
            .Where(t => typeof(IUserType).IsAssignableFrom(t))
            .Where(t => t.GetConstructor(Type.EmptyTypes) is not null)
            .OrderBy(t => t.FullName, StringComparer.Ordinal),
    ];

    private static IUserType Resolve(string fullName)
        => (IUserType)Activator.CreateInstance(s_converterTypes.Single(t => t.FullName == fullName))!;

    public static TheoryData<string> AllConverters()
    {
        var data = new TheoryData<string>();
        foreach (var type in s_converterTypes)
            data.Add(type.FullName!);

        return data;
    }

    #region The contract every converter owes NHibernate

    [Fact]
    public void TheConvertersAreDiscovered()
        => Assert.True(s_converterTypes.Length >= 10, $"Only found {s_converterTypes.Length} converters.");

    [Theory]
    [MemberData(nameof(AllConverters))]
    public void EveryConverterDeclaresItsColumnTypes(string fullName)
    {
        var converter = Resolve(fullName);

        // NHibernate needs at least one column type to build the mapping at all.
        Assert.NotEmpty(converter.SqlTypes);
        Assert.All(converter.SqlTypes, Assert.NotNull);
    }

    [Theory]
    [MemberData(nameof(AllConverters))]
    public void EveryConverterDeclaresTheTypeItReturns(string fullName)
        => Assert.NotNull(Resolve(fullName).ReturnedType);

    [Theory]
    [MemberData(nameof(AllConverters))]
    public void EveryConverterTreatsTwoNullsAsEqual(string fullName)
    {
        // Dirty-checking runs this on every flush; saying two nulls differ would rewrite untouched
        // rows forever.
        Assert.True(Resolve(fullName).Equals(null, null));
    }

    [Theory]
    [MemberData(nameof(AllConverters))]
    public void EveryConverterTreatsAValueAsEqualToItself(string fullName)
    {
        var converter = Resolve(fullName);
        var value = new object();

        Assert.True(converter.Equals(value, value));
    }

    [Theory]
    [MemberData(nameof(AllConverters))]
    public void EveryConverterHashesNullWithoutThrowing(string fullName)
        => Resolve(fullName).GetHashCode(null!);

    #endregion

    #region MessagePack

    [Fact]
    public void MediaInfoSurvivesAConverterRoundTrip()
    {
        var converter = new MessagePackConverter<MediaContainer>();
        var original = new MediaContainer();

        var stored = converter.ConvertTo(null, CultureInfo.InvariantCulture, original, typeof(byte[]));

        var bytes = Assert.IsType<byte[]>(stored);
        Assert.NotEmpty(bytes);
        Assert.IsType<MediaContainer>(converter.ConvertFrom(null, CultureInfo.InvariantCulture, bytes));
    }

    [Fact]
    public void MessagePackConverter_StoresNullAsNull()
        => Assert.Null(new MessagePackConverter<MediaContainer>().ConvertTo(null, CultureInfo.InvariantCulture, null, typeof(byte[])));

    [Fact]
    public void MessagePackConverter_OnlyAcceptsBytesBack()
        => Assert.Throws<ArgumentException>(
            () => new MessagePackConverter<MediaContainer>().ConvertFrom(null, CultureInfo.InvariantCulture, "not bytes"));

    #endregion

    #region String lists

    [Fact]
    public void AStringListSurvivesARoundTrip()
    {
        var converter = new StringListConverter();
        var original = new List<string> { "alpha", "beta", "gamma" };

        var stored = converter.ConvertTo(null, CultureInfo.InvariantCulture, original, typeof(string));
        var restored = converter.ConvertFrom(null, CultureInfo.InvariantCulture, stored);

        Assert.Equal(original, Assert.IsType<List<string>>(restored));
    }

    [Fact]
    public void AnEmptyStringListStoresAsAnEmptyString()
        => Assert.Equal(string.Empty, new StringListConverter().ConvertTo(null, CultureInfo.InvariantCulture, new List<string>(), typeof(string)));

    [Fact]
    public void ANullStringListReadsBackAsEmpty()
        => Assert.Empty(Assert.IsType<List<string>>(new StringListConverter().ConvertFrom(null, CultureInfo.InvariantCulture, null)));

    [Fact]
    public void AStringListEntryContainingTheSeparatorIsSplitApart()
    {
        var converter = new StringListConverter();

        var stored = converter.ConvertTo(null, CultureInfo.InvariantCulture, new List<string> { "a|||b" }, typeof(string));
        var restored = Assert.IsType<List<string>>(converter.ConvertFrom(null, CultureInfo.InvariantCulture, stored));

        // The list is delimited by "|||" with no escaping, so a value containing it comes back as
        // two entries. Pinned so the limitation is visible rather than discovered in a user's data.
        Assert.Equal(["a", "b"], restored);
    }

    #endregion

    #region Dates

    [Fact]
    public void APartialDateSurvivesARoundTrip()
    {
        var converter = new PartialDateOnlyConverter();
        var original = new PartialDateOnly(2024, 5, 1);

        var stored = converter.ConvertTo(null, CultureInfo.InvariantCulture, original, typeof(string));
        var restored = converter.ConvertFrom(null, CultureInfo.InvariantCulture, stored);

        Assert.Equal(original, Assert.IsType<PartialDateOnly>(restored));
    }

    [Fact]
    public void AYearOnlyPartialDateKeepsItsMissingParts()
    {
        var converter = new PartialDateOnlyConverter();
        var original = new PartialDateOnly(2024);

        var stored = converter.ConvertTo(null, CultureInfo.InvariantCulture, original, typeof(string));
        var restored = Assert.IsType<PartialDateOnly>(converter.ConvertFrom(null, CultureInfo.InvariantCulture, stored));

        // AniDB supplies plenty of year-only dates; filling in a month and day would invent data.
        Assert.Equal(2024, restored.Year);
        Assert.Null(restored.Month);
        Assert.Null(restored.Day);
    }

    [Fact]
    public void ANullPartialDateStaysNull()
        => Assert.Null(new PartialDateOnlyConverter().ConvertFrom(null, CultureInfo.InvariantCulture, null));

    [Fact]
    public void ADateOnlySurvivesARoundTripThroughADateTime()
    {
        var converter = new DateOnlyConverter();
        var original = new DateOnly(2024, 5, 1);

        var restored = converter.ConvertFrom(null, CultureInfo.InvariantCulture, original.ToDateTime(TimeOnly.MinValue));

        Assert.Equal(original, Assert.IsType<DateOnly>(restored));
    }

    #endregion

    #region Types and JSON

    [Fact]
    public void ATypeSurvivesARoundTrip()
    {
        var converter = new TypeStringConverter();

        var stored = converter.ConvertTo(null, CultureInfo.InvariantCulture, typeof(StringListConverter), typeof(string));
        var restored = converter.ConvertFrom(null, CultureInfo.InvariantCulture, stored!);

        Assert.Same(typeof(StringListConverter), restored);
    }

    [Fact]
    public void AnUnknownTypeNameReadsBackAsNull()
        => Assert.Null(new TypeStringConverter().ConvertFrom(null, CultureInfo.InvariantCulture, "Nothing.Called.This"));

    [Fact]
    public void AJTokenDictionarySurvivesARoundTrip()
    {
        var converter = new JTokenDictionaryConverter();
        var original = new Dictionary<string, JToken?> { ["a"] = JToken.FromObject(1), ["b"] = JToken.FromObject("two") };

        var stored = converter.ConvertTo(null, CultureInfo.InvariantCulture, original, typeof(string));
        var restored = converter.ConvertFrom(null, CultureInfo.InvariantCulture, stored!);

        var dictionary = Assert.IsAssignableFrom<IDictionary<string, JToken?>>(restored);
        Assert.Equal(2, dictionary.Count);
        Assert.True(JToken.DeepEquals(original["a"], dictionary["a"]));
        Assert.True(JToken.DeepEquals(original["b"], dictionary["b"]));
    }

    #endregion
}
