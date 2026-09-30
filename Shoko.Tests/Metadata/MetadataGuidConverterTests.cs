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
/// Covers the three converters that turn a <see cref="MetadataGuid"/> into
/// text and back: System.Text.Json, Newtonsoft.Json and the type converter
/// that Newtonsoft's dictionary keys go through.
/// </summary>
/// <remarks>
/// Each writes the canonical text form and reads it as
/// <see cref="MetadataGuid.Parse(string, IFormatProvider?)"/> does: aliases,
/// any case and unregistered values included.
/// </remarks>
public class MetadataGuidConverterTests
{
    private sealed class Holder
    {
        public MetadataGuid? Guid { get; set; }
    }

    private static readonly MetadataGuid _series = new(MetadataSource.AniDB, MetadataEntityType.Series, "1");

    private static readonly MetadataGuid _movie = new(MetadataSource.TMDB, MetadataEntityType.Movie, "603");

    #region System.Text.Json

    [Fact]
    public void Stj_HandlesNullAndRoundTripsAProperty()
    {
        Assert.Null(StjJsonSerializer.Deserialize<MetadataGuid>("null"));
        Assert.Equal("{\"Guid\":null}", StjJsonSerializer.Serialize(new Holder()));

        var json = StjJsonSerializer.Serialize(new Holder { Guid = _movie });

        Assert.Equal("{\"Guid\":\"tmdb://movie/603\"}", json);
        Assert.Equal(_movie, StjJsonSerializer.Deserialize<Holder>(json)!.Guid);
    }

    [Fact]
    public void Stj_RoundTripsDictionaryKeys()
    {
        var dictionary = new Dictionary<MetadataGuid, int> { [_series] = 1, [_movie] = 2 };

        var json = StjJsonSerializer.Serialize(dictionary);

        Assert.Equal("{\"anidb://series/1\":1,\"tmdb://movie/603\":2}", json);
        Assert.Equal(dictionary, StjJsonSerializer.Deserialize<Dictionary<MetadataGuid, int>>(json));
        Assert.Equal(1, StjJsonSerializer.Deserialize<Dictionary<MetadataGuid, int>>("{\"AniDB://Show/1\":1}")![_series]);
    }

    [Theory]
    [InlineData("\"anidb\"")]
    [InlineData("1")]
    public void Stj_RefusesInvalidText(string json)
        => Assert.Throws<StjJsonException>(() => StjJsonSerializer.Deserialize<MetadataGuid>(json));

    [Fact]
    public void Stj_RefusesAnInvalidDictionaryKey()
        => Assert.Throws<StjJsonException>(() => StjJsonSerializer.Deserialize<Dictionary<MetadataGuid, int>>("{\"anidb\":1}"));

    #endregion

    #region Newtonsoft.Json

    [Fact]
    public void Newtonsoft_HandlesNullAndRoundTripsAProperty()
    {
        Assert.Null(JsonConvert.DeserializeObject<MetadataGuid>("null"));
        Assert.Equal("{\"Guid\":null}", JsonConvert.SerializeObject(new Holder()));

        var json = JsonConvert.SerializeObject(new Holder { Guid = _series });

        Assert.Equal("{\"Guid\":\"anidb://series/1\"}", json);
        Assert.Equal(_series, JsonConvert.DeserializeObject<Holder>(json)!.Guid);
    }

    [Fact]
    public void Newtonsoft_RoundTripsDictionaryKeys()
    {
        var dictionary = new Dictionary<MetadataGuid, int> { [_series] = 1, [_movie] = 2 };

        var json = JsonConvert.SerializeObject(dictionary);

        Assert.Equal("{\"anidb://series/1\":1,\"tmdb://movie/603\":2}", json);
        Assert.Equal(dictionary, JsonConvert.DeserializeObject<Dictionary<MetadataGuid, int>>(json));
        Assert.Equal(2, JsonConvert.DeserializeObject<Dictionary<MetadataGuid, int>>("{\"themoviedb://Movie/603\":2}")![_movie]);
    }

    [Theory]
    [InlineData("\"anidb\"")]
    [InlineData("1")]
    public void Newtonsoft_RefusesInvalidText(string json)
        => Assert.Throws<JsonSerializationException>(() => JsonConvert.DeserializeObject<MetadataGuid>(json));

    [Fact]
    public void Newtonsoft_RefusesAnInvalidDictionaryKey()
        => Assert.Throws<JsonSerializationException>(() => JsonConvert.DeserializeObject<Dictionary<MetadataGuid, int>>("{\"anidb\":1}"));

    #endregion

    #region Type Converter

    [Fact]
    public void TheTypeConverterIsTheOneTheTypeDescriptorFinds()
        => Assert.IsType<MetadataGuidTypeConverter>(TypeDescriptor.GetConverter(typeof(MetadataGuid)));

    [Fact]
    public void TheTypeConverterWritesTheTextFormAndReadsItBack()
    {
        var converter = new MetadataGuidTypeConverter();

        Assert.Equal("tmdb://movie/603", converter.ConvertToInvariantString(_movie));
        Assert.Equal(_movie, converter.ConvertFromInvariantString("tmdb://movie/603"));
    }

    [Fact]
    public void TheTypeConverterRefusesInvalidText()
        => Assert.Throws<FormatException>(() => new MetadataGuidTypeConverter().ConvertFromInvariantString("anidb"));

    #endregion
}
