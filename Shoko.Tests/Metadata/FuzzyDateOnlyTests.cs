using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Shoko.Abstractions.Metadata;
using Xunit;

using StjJsonException = System.Text.Json.JsonException;
using StjJsonSerializer = System.Text.Json.JsonSerializer;

namespace Shoko.Tests.Metadata;

/// <summary>
/// Covers <see cref="FuzzyDateOnly"/>: which parts it takes, its ISO 8601
/// text form, its order, its conversions and its three converters.
/// </summary>
public class FuzzyDateOnlyTests
{
    private sealed class Holder
    {
        public FuzzyDateOnly? Date { get; set; }
    }

    #region Construction

    [Theory]
    [InlineData(1990, 4, 1)]
    [InlineData(1990, 4, null)]
    [InlineData(1990, null, null)]
    [InlineData(null, 4, 1)]
    [InlineData(null, 4, null)]
    [InlineData(2024, 2, 29)]
    [InlineData(null, 2, 29)]
    public void ValidPartsMakeADate(int? year, int? month, int? day)
    {
        var date = new FuzzyDateOnly(year, month, day);

        Assert.Equal((year, month, day), (date.Year, date.Month, date.Day));
        Assert.True(FuzzyDateOnly.IsValid(year, month, day));
    }

    [Theory]
    [InlineData(null, null, null)]
    [InlineData(1990, null, 1)]
    [InlineData(null, null, 1)]
    public void MissingPartsAreRejected(int? year, int? month, int? day)
    {
        Assert.Throws<ArgumentException>(() => new FuzzyDateOnly(year, month, day));
        Assert.False(FuzzyDateOnly.IsValid(year, month, day));
    }

    [Theory]
    [InlineData(0, null, null)]
    [InlineData(10000, null, null)]
    [InlineData(1990, 13, null)]
    [InlineData(null, 0, null)]
    [InlineData(2023, 2, 29)]
    [InlineData(null, 2, 30)]
    [InlineData(null, 4, 31)]
    [InlineData(1990, 1, 0)]
    public void OutOfRangePartsAreRejected(int? year, int? month, int? day)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new FuzzyDateOnly(year, month, day));
        Assert.False(FuzzyDateOnly.IsValid(year, month, day));
    }

    #endregion

    #region Text Form

    public static TheoryData<string, int?, int?, int?> TextForms() => new()
    {
        { "1990-04-01", 1990, 4, 1 },
        { "1990-04", 1990, 4, null },
        { "1990", 1990, null, null },
        { "0042", 42, null, null },
        { "--04-01", null, 4, 1 },
        { "--04", null, 4, null },
        { "--02-29", null, 2, 29 },
        { "2024-02-29", 2024, 2, 29 },
    };

    [Theory]
    [MemberData(nameof(TextForms))]
    public void TheTextFormIsIso8601(string text, int? year, int? month, int? day)
    {
        var date = new FuzzyDateOnly(year, month, day);

        Assert.Equal(text, date.ToString());
        Assert.Equal(text, date.ToString(null, CultureInfo.InvariantCulture));
        Assert.Equal(text, $"{date}");
    }

    [Theory]
    [MemberData(nameof(TextForms))]
    public void TheTextFormParsesBack(string text, int? year, int? month, int? day)
    {
        var expected = new FuzzyDateOnly(year, month, day);

        Assert.Equal(expected, FuzzyDateOnly.Parse(text));
        Assert.Equal(expected, FuzzyDateOnly.Parse(text, CultureInfo.InvariantCulture));
        Assert.Equal(expected, FuzzyDateOnly.Parse(text.AsSpan(), CultureInfo.InvariantCulture));
        Assert.True(FuzzyDateOnly.TryParse($"  {text} ", out var trimmed));
        Assert.Equal(expected, trimmed);
    }

    [Theory]
    [MemberData(nameof(TextForms))]
    public void TheSpanFormattersWriteTheTextForm(string text, int? year, int? month, int? day)
    {
        var date = new FuzzyDateOnly(year, month, day);

        Span<char> chars = stackalloc char[FuzzyDateOnly.MaxLength];
        Assert.True(date.TryFormat(chars, out var charsWritten, default, null));
        Assert.Equal(text, new string(chars[..charsWritten]));

        Span<byte> bytes = stackalloc byte[FuzzyDateOnly.MaxLength];
        Assert.True(date.TryFormat(bytes, out var bytesWritten, default, null));
        Assert.Equal(text, Encoding.UTF8.GetString(bytes[..bytesWritten]));

        Assert.False(date.TryFormat(new char[text.Length - 1], out charsWritten, default, null));
        Assert.Equal(0, charsWritten);
        Assert.False(date.TryFormat(new byte[text.Length - 1], out bytesWritten, default, null));
        Assert.Equal(0, bytesWritten);
    }

    [Fact]
    public void ALongerTextIsReadAsADateAndTime()
        => Assert.Equal(new FuzzyDateOnly(1990, 4, 1), FuzzyDateOnly.Parse("1990-04-01 12:34:56"));

    // The two ends of the offset range: no local time zone can take both
    // to the same day without moving one of them.
    [Theory]
    [InlineData("1990-05-01T00:00:00+14:00")]
    [InlineData("1990-05-01T23:59:59-12:00")]
    [InlineData("1990-05-01T00:00:00Z")]
    [InlineData("1990-05-01T23:59:59Z")]
    public void ALongerTextKeepsTheDateAsWrittenWhateverItsOffset(string text)
        => Assert.Equal(new FuzzyDateOnly(1990, 5, 1), FuzzyDateOnly.Parse(text));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("90")]
    [InlineData("199")]
    [InlineData("1990-4")]
    [InlineData("1990/04/01")]
    [InlineData("1990-04-1")]
    [InlineData("--4")]
    [InlineData("--04-1")]
    [InlineData("---04")]
    [InlineData("--13")]
    [InlineData("--02-30")]
    [InlineData("2023-02-29")]
    [InlineData("0000")]
    [InlineData("+990")]
    [InlineData("-1990")]
    [InlineData("1990-13")]
    [InlineData("someday")]
    public void InvalidTextIsRejected(string text)
    {
        Assert.False(FuzzyDateOnly.TryParse(text, out var date));
        Assert.Equal(default, date);
        Assert.Throws<FormatException>(() => FuzzyDateOnly.Parse(text));
    }

    [Fact]
    public void TheDefaultValueWritesAsNothing()
        => Assert.Equal(string.Empty, default(FuzzyDateOnly).ToString());

    #endregion

    #region Order

    [Fact]
    public void DatesWithAYearSortFirstAndYearlessOnesByMonthAndDay()
    {
        FuzzyDateOnly[] expected =
        [
            new(1985),
            new(1985, 7),
            new(1985, 7, 15),
            new(1990, 1, 1),
            new(2001),
            new(null, 1),
            new(null, 1, 31),
            new(null, 2, 29),
            new(null, 12, 1),
        ];

        var shuffled = expected.Reverse().ToList();
        shuffled.Sort();

        Assert.Equal(expected, shuffled);
        Assert.Equal(expected, expected.Reverse().Order());
    }

    [Fact]
    public void TheOperatorsFollowTheOrder()
    {
        FuzzyDateOnly dated = new(2020, 5, 5);
        FuzzyDateOnly yearless = new(null, 1, 1);
        FuzzyDateOnly? missing = null;

        Assert.True(dated < yearless);
        Assert.True(yearless > dated);
        Assert.True(dated <= new FuzzyDateOnly(2020, 5, 5));
        Assert.True(yearless >= new FuzzyDateOnly(null, 1, 1));
        Assert.True(missing < dated);
        Assert.False(missing > yearless);
        Assert.True(dated == new FuzzyDateOnly(2020, 5, 5));
        Assert.True(dated != yearless);
        Assert.True(missing != dated);
        Assert.True(missing == (FuzzyDateOnly?)null);
    }

    [Fact]
    public void EqualDatesHashTheSame()
    {
        var set = new HashSet<FuzzyDateOnly> { new(null, 2, 29), new(null, 2, 29), new(2024, 2, 29) };

        Assert.Equal(2, set.Count);
        Assert.True(new FuzzyDateOnly(null, 2, 29).Equals((object)new FuzzyDateOnly(null, 2, 29)));
        Assert.False(new FuzzyDateOnly(null, 2, 29).Equals((object)new PartialDateOnly(2024, 2, 29)));
    }

    #endregion

    #region Conversions

    [Theory]
    [InlineData(1990, null, null)]
    [InlineData(1990, 4, null)]
    [InlineData(1990, 4, 1)]
    public void APartialDateConvertsImplicitlyAndBack(int year, int? month, int? day)
    {
        var partial = new PartialDateOnly(year, month, day);
        FuzzyDateOnly fuzzy = partial;

        Assert.Equal((year, month, day), (fuzzy.Year!.Value, fuzzy.Month, fuzzy.Day));
        Assert.Equal(partial.ToString(), fuzzy.ToString());
        Assert.True(fuzzy.TryGetPartialDate(out var back));
        Assert.Equal(partial, back);
    }

    [Fact]
    public void ANullablePartialDateConvertsToo()
    {
        PartialDateOnly? partial = new PartialDateOnly(1990, 4);
        PartialDateOnly? none = null;
        FuzzyDateOnly? fuzzy = partial;
        FuzzyDateOnly? missing = none;

        Assert.Equal(new FuzzyDateOnly(1990, 4), fuzzy);
        Assert.Null(missing);
    }

    [Fact]
    public void AYearlessDateHasNoPartialDate()
    {
        Assert.False(new FuzzyDateOnly(null, 2, 29).TryGetPartialDate(out var date));
        Assert.Equal(default, date);
    }

    [Fact]
    public void OnlyACompleteDateHasADateOnly()
    {
        Assert.True(new FuzzyDateOnly(new DateOnly(2024, 2, 29)).TryGetDateOnly(out var date));
        Assert.Equal(new DateOnly(2024, 2, 29), date);
        Assert.False(new FuzzyDateOnly(2024, 2).TryGetDateOnly(out _));
        Assert.False(new FuzzyDateOnly(null, 2, 29).TryGetDateOnly(out _));
    }

    #endregion

    #region Converters

    [Fact]
    public void Stj_WritesAndReadsTheTextForm()
    {
        Assert.Equal("\"--02-29\"", StjJsonSerializer.Serialize(new FuzzyDateOnly(null, 2, 29)));
        Assert.Equal("{\"Date\":\"1990-04\"}", StjJsonSerializer.Serialize(new Holder { Date = new(1990, 4) }));
        Assert.Equal("{\"Date\":null}", StjJsonSerializer.Serialize(new Holder()));
        Assert.Equal(new FuzzyDateOnly(null, 12), StjJsonSerializer.Deserialize<FuzzyDateOnly>("\"--12\""));
        Assert.Equal(new FuzzyDateOnly(1990), StjJsonSerializer.Deserialize<Holder>("{\"Date\":\"1990\"}")!.Date);
        Assert.Null(StjJsonSerializer.Deserialize<Holder>("{\"Date\":null}")!.Date);
    }

    [Fact]
    public void Stj_HandlesDictionaryKeys()
    {
        var json = StjJsonSerializer.Serialize(new Dictionary<FuzzyDateOnly, int> { [new(null, 7, 4)] = 1 });

        Assert.Equal("{\"--07-04\":1}", json);
        Assert.Equal(1, StjJsonSerializer.Deserialize<Dictionary<FuzzyDateOnly, int>>(json)![new(null, 7, 4)]);
    }

    [Theory]
    [InlineData("\"someday\"")]
    [InlineData("1990")]
    public void Stj_RejectsInvalidValues(string json)
        => Assert.Throws<StjJsonException>(() => StjJsonSerializer.Deserialize<FuzzyDateOnly>(json));

    [Fact]
    public void Newtonsoft_WritesAndReadsTheTextForm()
    {
        Assert.Equal("\"--02-29\"", JsonConvert.SerializeObject(new FuzzyDateOnly(null, 2, 29)));
        Assert.Equal("{\"Date\":\"1990-04\"}", JsonConvert.SerializeObject(new Holder { Date = new(1990, 4) }));
        Assert.Equal("{\"Date\":null}", JsonConvert.SerializeObject(new Holder()));
        Assert.Equal(new FuzzyDateOnly(null, 12), JsonConvert.DeserializeObject<FuzzyDateOnly>("\"--12\""));
        Assert.Equal(new FuzzyDateOnly(1990), JsonConvert.DeserializeObject<Holder>("{\"Date\":\"1990\"}")!.Date);
        Assert.Null(JsonConvert.DeserializeObject<Holder>("{\"Date\":null}")!.Date);
    }

    [Fact]
    public void Newtonsoft_HandlesDictionaryKeys()
    {
        var json = JsonConvert.SerializeObject(new Dictionary<FuzzyDateOnly, int> { [new(null, 7, 4)] = 1 });

        Assert.Equal("{\"--07-04\":1}", json);
        Assert.Equal(1, JsonConvert.DeserializeObject<Dictionary<FuzzyDateOnly, int>>(json)![new(null, 7, 4)]);
    }

    [Theory]
    [InlineData("\"someday\"")]
    [InlineData("1990")]
    [InlineData("null")]
    public void Newtonsoft_RejectsInvalidValues(string json)
        => Assert.Throws<JsonSerializationException>(() => JsonConvert.DeserializeObject<FuzzyDateOnly>(json));

    [Fact]
    public void TheTypeConverterReadsTextAndOtherDates()
    {
        var converter = TypeDescriptor.GetConverter(typeof(FuzzyDateOnly));

        Assert.Equal(new FuzzyDateOnly(null, 2, 29), converter.ConvertFromInvariantString("--02-29"));
        Assert.Equal(new FuzzyDateOnly(1990, 4), converter.ConvertFrom(new PartialDateOnly(1990, 4)));
        Assert.Equal(new FuzzyDateOnly(1990, 4, 1), converter.ConvertFrom(new DateOnly(1990, 4, 1)));
        Assert.Equal("--02-29", converter.ConvertToInvariantString(new FuzzyDateOnly(null, 2, 29)));
        Assert.Throws<FormatException>(() => converter.ConvertFromInvariantString("someday"));
    }

    #endregion
}
