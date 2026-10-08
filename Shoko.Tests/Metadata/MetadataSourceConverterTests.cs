using System;
using System.Collections.Generic;
using System.ComponentModel;
using Newtonsoft.Json;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Converters;
using Xunit;

using StjJsonException = System.Text.Json.JsonException;
using StjJsonSerializer = System.Text.Json.JsonSerializer;

namespace Shoko.Tests.Metadata;

/// <summary>
/// Covers the three converters that turn a <see cref="MetadataSource"/> into
/// text and back: System.Text.Json, Newtonsoft.Json and the type converter
/// that API binding and Newtonsoft's dictionary keys go through.
/// </summary>
/// <remarks>
/// Each writes the value and reads a value or alias, ignoring case, or any
/// other valid value as an unregistered source, so saved settings and stored
/// JSON keep reading back whatever spelling they were written in.
/// </remarks>
public class MetadataSourceConverterTests
{
    private sealed class Holder
    {
        public MetadataSource? Source { get; set; }
    }

    private sealed class NameHolder
    {
        [JsonConverter(typeof(NameConverter))]
        public MetadataSource? Source { get; set; }
    }

    private sealed class NameConverter : MetadataSourceNewtonsoftJsonConverter
    {
        protected override string GetText(MetadataSource source)
            => source.Name;
    }

    #region System.Text.Json

    [Fact]
    public void Stj_RoundTripsASourceAPropertyAndNull()
    {
        var source = MetadataSource.Register("MscStjRoundTrip", "msc-stj-round-trip", ["msc-stj-round-trip-alias"]);
        var json = StjJsonSerializer.Serialize(new Holder { Source = MetadataSource.TMDB });

        Assert.Same(source, StjJsonSerializer.Deserialize<MetadataSource>(StjJsonSerializer.Serialize(source)));
        Assert.Same(source, StjJsonSerializer.Deserialize<MetadataSource>("\"MSC-STJ-ROUND-TRIP-ALIAS\""));
        Assert.Equal("{\"Source\":\"tmdb\"}", json);
        Assert.Same(MetadataSource.TMDB, StjJsonSerializer.Deserialize<Holder>(json)!.Source);
        Assert.Null(StjJsonSerializer.Deserialize<MetadataSource>("null"));
        Assert.Equal("{\"Source\":null}", StjJsonSerializer.Serialize(new Holder()));
    }

    [Fact]
    public void Stj_RoundTripsDictionaryKeys()
    {
        var dictionary = new Dictionary<MetadataSource, int> { [MetadataSource.AniDB] = 1, [MetadataSource.TMDB] = 2 };

        var json = StjJsonSerializer.Serialize(dictionary);
        var restored = StjJsonSerializer.Deserialize<Dictionary<MetadataSource, int>>(json)!;

        Assert.Equal("{\"anidb\":1,\"tmdb\":2}", json);
        Assert.Equal(dictionary, restored);
    }

    [Theory]
    [InlineData("\"not valid\"")]
    [InlineData("1")]
    public void Stj_RefusesInvalidText(string json)
        => Assert.Throws<StjJsonException>(() => StjJsonSerializer.Deserialize<MetadataSource>(json));

    [Fact]
    public void Stj_RefusesAnInvalidDictionaryKey()
        => Assert.Throws<StjJsonException>(() => StjJsonSerializer.Deserialize<Dictionary<MetadataSource, int>>("{\"not valid\":1}"));

    #endregion

    #region Newtonsoft.Json

    [Fact]
    public void Newtonsoft_RoundTripsASourceAPropertyAndNull()
    {
        var source = MetadataSource.Register("MscNewtonsoftRoundTrip", "msc-newtonsoft-round-trip", ["msc-newtonsoft-round-trip-alias"]);
        var json = JsonConvert.SerializeObject(new Holder { Source = MetadataSource.TMDB });

        Assert.Same(source, JsonConvert.DeserializeObject<MetadataSource>(JsonConvert.SerializeObject(source)));
        Assert.Same(source, JsonConvert.DeserializeObject<MetadataSource>("\"MSC-NEWTONSOFT-ROUND-TRIP-ALIAS\""));
        Assert.Equal("{\"Source\":\"tmdb\"}", json);
        Assert.Same(MetadataSource.TMDB, JsonConvert.DeserializeObject<Holder>(json)!.Source);
        Assert.Null(JsonConvert.DeserializeObject<MetadataSource>("null"));
        Assert.Equal("{\"Source\":null}", JsonConvert.SerializeObject(new Holder()));
    }

    [Fact]
    public void Newtonsoft_RoundTripsDictionaryKeys()
    {
        var dictionary = new Dictionary<MetadataSource, int> { [MetadataSource.AniDB] = 1, [MetadataSource.TMDB] = 2 };

        var json = JsonConvert.SerializeObject(dictionary);
        var restored = JsonConvert.DeserializeObject<Dictionary<MetadataSource, int>>(json)!;

        Assert.Equal("{\"anidb\":1,\"tmdb\":2}", json);
        Assert.Equal(dictionary, restored);
    }

    [Fact]
    public void Newtonsoft_ReadsAnUnregisteredDictionaryKey_SoSettingsKeepSourcesWhosePluginIsGone()
    {
        // Newtonsoft reads dictionary keys through the type converter, so it must stay lenient.
        var restored = JsonConvert.DeserializeObject<Dictionary<MetadataSource, int>>("{\"msc-settings-key\":1,\"trakt\":2}")!;

        Assert.Equal(1, restored[MetadataSource.Parse("msc-settings-key")]);
        Assert.Equal(2, restored[MetadataSource.Parse("trakt")]);
    }

    [Theory]
    [InlineData("\"not valid\"")]
    [InlineData("1")]
    public void Newtonsoft_RefusesInvalidText(string json)
        => Assert.Throws<JsonSerializationException>(() => JsonConvert.DeserializeObject<MetadataSource>(json));

    [Fact]
    public void Newtonsoft_RefusesAnInvalidDictionaryKey()
        => Assert.Throws<JsonSerializationException>(() => JsonConvert.DeserializeObject<Dictionary<MetadataSource, int>>("{\"not valid\":1}"));

    [Fact]
    public void Newtonsoft_ASubclassOnAPropertyCanChooseTheTextItWrites()
    {
        var json = JsonConvert.SerializeObject(new NameHolder { Source = MetadataSource.TMDB });

        Assert.Equal("{\"Source\":\"TMDB\"}", json);
        Assert.Same(MetadataSource.TMDB, JsonConvert.DeserializeObject<NameHolder>(json)!.Source);
        Assert.Equal("{\"Source\":null}", JsonConvert.SerializeObject(new NameHolder()));
    }

    #endregion

    #region Type Converter

    [Fact]
    public void TheTypeConverterIsTheOneTheTypeDescriptorFinds()
        => Assert.IsType<MetadataSourceTypeConverter>(TypeDescriptor.GetConverter(typeof(MetadataSource)));

    [Fact]
    public void TheTypeConverterWritesTheValueAndReadsItBack()
    {
        var converter = new MetadataSourceTypeConverter();

        Assert.Equal("tmdb", converter.ConvertToInvariantString(MetadataSource.TMDB));
        Assert.Same(MetadataSource.TMDB, converter.ConvertFromInvariantString("themoviedb"));
    }

    [Fact]
    public void TheTypeConverterRefusesInvalidText()
        => Assert.Throws<FormatException>(() => new MetadataSourceTypeConverter().ConvertFromInvariantString("not valid"));

    #endregion
}
