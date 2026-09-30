using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using Newtonsoft.Json;
using Shoko.Abstractions.Metadata;
using Shoko.Server.API.Resolvers;
using Xunit;

using StjJsonException = System.Text.Json.JsonException;
using StjJsonSerializer = System.Text.Json.JsonSerializer;

namespace Shoko.Tests.Metadata;

/// <summary>
/// Tests for how a <see cref="PartialDateOnly"/> is built, and how it is
/// written and read as text by both JSON libraries and its type converter.
/// </summary>
public class PartialDateOnlyTests
{
    private sealed class Holder
    {
        public PartialDateOnly Date { get; set; } = new(2000);

        public PartialDateOnly? Maybe { get; set; }
    }

    // The settings APIv3 serializes with.
    private static readonly JsonSerializerSettings _apiSettings = new()
    {
        MaxDepth = 10,
        ContractResolver = new ApiContractResolver(),
        NullValueHandling = NullValueHandling.Include,
        DefaultValueHandling = DefaultValueHandling.Populate,
    };

    public static TheoryData<string, int, int?, int?> TextForms() => new()
    {
        { "2024", 2024, null, null },
        { "2024-03", 2024, 3, null },
        { "2024-03-28", 2024, 3, 28 },
        { "0999-12-31", 999, 12, 31 },
    };

    #region Constructors

    [Fact]
    public void ADateOnlyOrDateTimeGivesTheCompleteDate()
    {
        var expected = new PartialDateOnly(2024, 3, 28);

        Assert.Equal(expected, new PartialDateOnly(new DateOnly(2024, 3, 28)));
        Assert.Equal(expected, new PartialDateOnly(new DateTime(2024, 3, 28, 23, 59, 59)));
        Assert.Equal(DayOfWeek.Thursday, new PartialDateOnly(new DateOnly(2024, 3, 28)).DayOfWeek);
    }

    #endregion

    #region Newtonsoft.Json

    [Theory]
    [MemberData(nameof(TextForms))]
    public void Newtonsoft_RoundTrips(string text, int year, int? month, int? day)
    {
        var date = new PartialDateOnly(year, month, day);

        var json = JsonConvert.SerializeObject(new Holder { Date = date, Maybe = date }, _apiSettings);
        Assert.Equal($"{{\"Date\":\"{text}\",\"Maybe\":\"{text}\"}}", json);

        var read = JsonConvert.DeserializeObject<Holder>(json, _apiSettings)!;
        Assert.Equal(date, read.Date);
        Assert.Equal(date, read.Maybe);
        Assert.Equal(date, JsonConvert.DeserializeObject<PartialDateOnly>($"\"{text}\""));
        Assert.Equal(date, JsonConvert.DeserializeObject<PartialDateOnly?>($"\"{text}\""));
    }

    [Fact]
    public void Newtonsoft_RoundTripsNull()
    {
        var json = JsonConvert.SerializeObject(new Holder(), _apiSettings);
        Assert.Equal("{\"Date\":\"2000\",\"Maybe\":null}", json);
        Assert.Null(JsonConvert.DeserializeObject<Holder>(json, _apiSettings)!.Maybe);
        Assert.Null(JsonConvert.DeserializeObject<PartialDateOnly?>("null"));
    }

    [Theory]
    [InlineData("\"2024-03-28T12:30:00\"")]
    [InlineData("\"2024-03-28 12:30:00\"")]
    public void Newtonsoft_ReadsADateWithATime(string json)
    {
        Assert.Equal(new PartialDateOnly(2024, 3, 28), JsonConvert.DeserializeObject<PartialDateOnly>(json));
        Assert.Equal(
            new PartialDateOnly(2024, 3, 28),
            JsonConvert.DeserializeObject<PartialDateOnly>(json, new JsonSerializerSettings { DateParseHandling = DateParseHandling.None })
        );
    }

    // Reading a key back is not possible: the struct is IConvertible, so Newtonsoft.Json tries
    // Convert.ChangeType on the key before any type converter, and that throws.
    [Fact]
    public void Newtonsoft_WritesDictionaryKeysAsText()
        => Assert.Equal("{\"2024-03\":1}", JsonConvert.SerializeObject(new Dictionary<PartialDateOnly, int> { [new(2024, 3)] = 1 }));

    [Theory]
    [InlineData("\"2024-13\"")]
    [InlineData("\"not a date\"")]
    [InlineData("\"\"")]
    [InlineData("2024")]
    [InlineData("null")]
    public void Newtonsoft_RefusesWhatIsNotADate(string json)
        => Assert.Throws<JsonSerializationException>(() => JsonConvert.DeserializeObject<PartialDateOnly>(json));

    #endregion

    #region System.Text.Json

    [Theory]
    [MemberData(nameof(TextForms))]
    public void Stj_RoundTrips(string text, int year, int? month, int? day)
    {
        var date = new PartialDateOnly(year, month, day);

        var json = StjJsonSerializer.Serialize(new Holder { Date = date, Maybe = date });
        Assert.Equal($"{{\"Date\":\"{text}\",\"Maybe\":\"{text}\"}}", json);

        var read = StjJsonSerializer.Deserialize<Holder>(json)!;
        Assert.Equal(date, read.Date);
        Assert.Equal(date, read.Maybe);
        Assert.Equal(date, StjJsonSerializer.Deserialize<PartialDateOnly>($"\"{text}\""));
        Assert.Equal(date, StjJsonSerializer.Deserialize<PartialDateOnly?>($"\"{text}\""));
    }

    [Fact]
    public void Stj_RoundTripsNull()
    {
        var json = StjJsonSerializer.Serialize(new Holder());
        Assert.Equal("{\"Date\":\"2000\",\"Maybe\":null}", json);
        Assert.Null(StjJsonSerializer.Deserialize<Holder>(json)!.Maybe);
        Assert.Null(StjJsonSerializer.Deserialize<PartialDateOnly?>("null"));
    }

    [Fact]
    public void Stj_ReadsADateWithATime()
        => Assert.Equal(new PartialDateOnly(2024, 3, 28), StjJsonSerializer.Deserialize<PartialDateOnly>("\"2024-03-28T12:30:00\""));

    [Fact]
    public void Stj_RoundTripsDictionaryKeys()
    {
        var json = StjJsonSerializer.Serialize(new Dictionary<PartialDateOnly, int> { [new(2024, 3)] = 1 });

        Assert.Equal("{\"2024-03\":1}", json);
        Assert.Equal(1, StjJsonSerializer.Deserialize<Dictionary<PartialDateOnly, int>>(json)![new(2024, 3)]);
    }

    [Theory]
    [InlineData("\"2024-13\"")]
    [InlineData("\"not a date\"")]
    [InlineData("\"\"")]
    [InlineData("2024")]
    [InlineData("{}")]
    public void Stj_RefusesWhatIsNotADate(string json)
        => Assert.Throws<StjJsonException>(() => StjJsonSerializer.Deserialize<PartialDateOnly>(json));

    #endregion

    #region Type converter

    [Theory]
    [MemberData(nameof(TextForms))]
    public void TypeConverter_RoundTripsText(string text, int year, int? month, int? day)
    {
        var converter = TypeDescriptor.GetConverter(typeof(PartialDateOnly));
        var date = new PartialDateOnly(year, month, day);

        Assert.True(converter.CanConvertFrom(typeof(string)));
        Assert.True(converter.CanConvertTo(typeof(string)));
        Assert.Equal(text, converter.ConvertToInvariantString(date));
        Assert.Equal(date, converter.ConvertFromInvariantString(text));
    }

    [Fact]
    public void TypeConverter_ConvertsFromDates()
    {
        var converter = TypeDescriptor.GetConverter(typeof(PartialDateOnly));

        Assert.Equal(new PartialDateOnly(2024, 3, 28), converter.ConvertFrom(null, CultureInfo.InvariantCulture, new DateOnly(2024, 3, 28)));
        Assert.Equal(new PartialDateOnly(2024, 3, 28), converter.ConvertFrom(null, CultureInfo.InvariantCulture, new DateTime(2024, 3, 28, 8, 0, 0)));
    }

    [Fact]
    public void TypeConverter_RefusesWhatIsNotADate()
        => Assert.Throws<FormatException>(() => TypeDescriptor.GetConverter(typeof(PartialDateOnly)).ConvertFromInvariantString("2024-13"));

    #endregion
}
