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
/// Covers the three converters that turn a <see cref="MetadataEntityType"/>
/// into text and back: System.Text.Json, Newtonsoft.Json and the type
/// converter that Newtonsoft's dictionary keys go through.
/// </summary>
/// <remarks>
/// Each writes the value and reads a value or alias, ignoring case, or any
/// other valid value as an unregistered kind, so saved settings and stored
/// JSON keep reading back whatever spelling the old enum wrote.
/// </remarks>
public class MetadataEntityTypeConverterTests
{
    private sealed class Holder
    {
        public MetadataEntityType? EntityType { get; set; }
    }

    #region System.Text.Json

    [Fact]
    public void Stj_HandlesNullAndRoundTripsAProperty()
    {
        Assert.Null(StjJsonSerializer.Deserialize<MetadataEntityType>("null"));
        Assert.Equal("{\"EntityType\":null}", StjJsonSerializer.Serialize(new Holder()));

        var json = StjJsonSerializer.Serialize(new Holder { EntityType = MetadataEntityType.Movie });

        Assert.Equal("{\"EntityType\":\"movie\"}", json);
        Assert.Same(MetadataEntityType.Movie, StjJsonSerializer.Deserialize<Holder>(json)!.EntityType);
    }

    [Fact]
    public void Stj_RoundTripsDictionaryKeysAndReadsOldSpellings()
    {
        var dictionary = new Dictionary<MetadataEntityType, int> { [MetadataEntityType.Series] = 1, [MetadataEntityType.Movie] = 2 };

        var json = StjJsonSerializer.Serialize(dictionary);

        Assert.Equal("{\"series\":1,\"movie\":2}", json);
        Assert.Equal(dictionary, StjJsonSerializer.Deserialize<Dictionary<MetadataEntityType, int>>(json));
        var restored = StjJsonSerializer.Deserialize<Dictionary<MetadataEntityType, int>>("{\"Show\":1,\"BoxSet\":2,\"metc-stj-key\":3}")!;
        Assert.Equal(1, restored[MetadataEntityType.Series]);
        Assert.Equal(2, restored[MetadataEntityType.Collection]);
        Assert.Equal(3, restored[MetadataEntityType.Parse("metc-stj-key")]);
    }

    [Theory]
    [InlineData("\"not valid\"")]
    [InlineData("2")]
    public void Stj_RefusesInvalidText(string json)
        => Assert.Throws<StjJsonException>(() => StjJsonSerializer.Deserialize<MetadataEntityType>(json));

    [Fact]
    public void Stj_RefusesAnInvalidDictionaryKey()
        => Assert.Throws<StjJsonException>(() => StjJsonSerializer.Deserialize<Dictionary<MetadataEntityType, int>>("{\"not valid\":1}"));

    #endregion

    #region Newtonsoft.Json

    [Fact]
    public void Newtonsoft_HandlesNullAndRoundTripsAProperty()
    {
        Assert.Null(JsonConvert.DeserializeObject<MetadataEntityType>("null"));
        Assert.Equal("{\"EntityType\":null}", JsonConvert.SerializeObject(new Holder()));

        var json = JsonConvert.SerializeObject(new Holder { EntityType = MetadataEntityType.Episode });

        Assert.Equal("{\"EntityType\":\"episode\"}", json);
        Assert.Same(MetadataEntityType.Episode, JsonConvert.DeserializeObject<Holder>(json)!.EntityType);
    }

    [Fact]
    public void Newtonsoft_RoundTripsDictionaryKeysAndReadsOldSpellings()
    {
        var dictionary = new Dictionary<MetadataEntityType, int> { [MetadataEntityType.Series] = 1, [MetadataEntityType.Movie] = 2 };

        var json = JsonConvert.SerializeObject(dictionary);

        Assert.Equal("{\"series\":1,\"movie\":2}", json);
        Assert.Equal(dictionary, JsonConvert.DeserializeObject<Dictionary<MetadataEntityType, int>>(json));

        // Newtonsoft reads dictionary keys through the type converter, so settings keep kinds whose plugin is gone.
        var restored = JsonConvert.DeserializeObject<Dictionary<MetadataEntityType, int>>("{\"Show\":1,\"Company\":2,\"Library\":3}")!;
        Assert.Equal(1, restored[MetadataEntityType.Series]);
        Assert.Equal(2, restored[MetadataEntityType.Studio]);
        Assert.Equal(3, restored[MetadataEntityType.Parse("library")]);
    }

    [Theory]
    [InlineData("\"not valid\"")]
    [InlineData("2")]
    public void Newtonsoft_RefusesInvalidText(string json)
        => Assert.Throws<JsonSerializationException>(() => JsonConvert.DeserializeObject<MetadataEntityType>(json));

    [Fact]
    public void Newtonsoft_RefusesAnInvalidDictionaryKey()
        => Assert.Throws<JsonSerializationException>(() => JsonConvert.DeserializeObject<Dictionary<MetadataEntityType, int>>("{\"not valid\":1}"));

    #endregion

    #region Type Converter

    [Fact]
    public void TheTypeConverterIsTheOneTheTypeDescriptorFinds()
        => Assert.IsType<MetadataEntityTypeTypeConverter>(TypeDescriptor.GetConverter(typeof(MetadataEntityType)));

    [Fact]
    public void TheTypeConverterWritesTheValueAndReadsItBack()
    {
        var converter = new MetadataEntityTypeTypeConverter();

        Assert.Equal("movie", converter.ConvertToInvariantString(MetadataEntityType.Movie));
        Assert.Same(MetadataEntityType.Movie, converter.ConvertFromInvariantString("film"));
    }

    [Fact]
    public void TheTypeConverterRefusesInvalidText()
        => Assert.Throws<FormatException>(() => new MetadataEntityTypeTypeConverter().ConvertFromInvariantString("not valid"));

    #endregion
}
