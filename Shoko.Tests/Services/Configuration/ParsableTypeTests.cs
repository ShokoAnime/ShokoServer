using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Namotion.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Plugin;
using Shoko.Server.Services.Configuration;
using Shoko.Server.Utilities;
using Xunit;

using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Shoko.Tests.Services.Configuration;

/// <summary>
///   Coverage for a type parsable from text written as that text by the
///   configuration serializers, without a JSON converter of its own.
/// </summary>
[Collection(ConfigurationSchemaCollection.Name)]
public class ParsableTypeTests
{
    #region Converters

    [Fact]
    public void Newtonsoft_RoundTripsTheText()
    {
        var settings = ShokoJsonSerializers.CreateNewtonsoftSettings();
        var json = JObject.Parse(JsonConvert.SerializeObject(new NewtonsoftParsableConfiguration(), settings));

        Assert.Equal("1.2", json["Version"]!.Value<string>());
        Assert.Equal(JTokenType.Null, json["Optional"]!.Type);
        Assert.Equal(["2.0", "3.1"], json["Versions"]!.Values<string>());
        Assert.Equal("v1.0", json["Own"]!.Value<string>());

        var read = JsonConvert.DeserializeObject<NewtonsoftParsableConfiguration>("""{"Version":"4.5","Optional":"6.7","Own":"v8.9"}""", settings)!;
        Assert.Equal((new(4, 5), new(6, 7), new(8, 9)), (read.Version, read.Optional, read.Own.Value));
    }

    [Fact]
    public void SystemTextJson_RoundTripsTheText()
    {
        var options = ShokoJsonSerializers.CreateSystemTextJsonOptions();
        var json = JObject.Parse(JsonSerializer.Serialize(new SystemTextJsonParsableConfiguration(), options));

        Assert.Equal("1.2", json["Version"]!.Value<string>());
        Assert.Equal(JTokenType.Null, json["Optional"]!.Type);
        Assert.Equal(["2.0", "3.1"], json["Versions"]!.Values<string>());
        Assert.Equal("v1.0", json["Own"]!.Value<string>());

        var read = JsonSerializer.Deserialize<SystemTextJsonParsableConfiguration>("""{"Version":"4.5","Optional":"6.7","Own":"v8.9"}""", options)!;
        Assert.Equal((new(4, 5), new(6, 7), new(8, 9)), (read.Version, read.Optional, read.Own.Value));
    }

    [Theory]
    [InlineData("\"nope\"")]
    [InlineData("12")]
    public void Both_RefuseWhatDoesNotParse(string value)
    {
        var json = $$"""{"Version":{{value}}}""";

        Assert.Throws<JsonSerializationException>(() => JsonConvert.DeserializeObject<NewtonsoftParsableConfiguration>(json, ShokoJsonSerializers.CreateNewtonsoftSettings()));
        Assert.Throws<System.Text.Json.JsonException>(() => JsonSerializer.Deserialize<SystemTextJsonParsableConfiguration>(json, ShokoJsonSerializers.CreateSystemTextJsonOptions()));
    }

    [Fact]
    public void Both_LeaveTheFrameworkTypesAsTheyWere()
    {
        var value = new { Id = Guid.Parse("8d3a6a3c-6f1e-4bd6-a0e9-1c2d3e4f5a6b"), Span = TimeSpan.FromMinutes(90), Version = new Version(1, 2, 3) };
        var settings = ShokoJsonSerializers.CreateNewtonsoftSettings();
        var plainSettings = ShokoJsonSerializers.CreateNewtonsoftSettings();
        plainSettings.Converters.Remove(ParsableNewtonsoftConverter.Instance);
        var options = ShokoJsonSerializers.CreateSystemTextJsonOptions();
        var plainOptions = ShokoJsonSerializers.CreateSystemTextJsonOptions();
        plainOptions.Converters.Remove(plainOptions.Converters.OfType<ParsableSystemTextJsonConverter>().Single());

        Assert.Equal(JsonConvert.SerializeObject(value, plainSettings), JsonConvert.SerializeObject(value, settings));
        Assert.Equal(JsonSerializer.Serialize(value, plainOptions), JsonSerializer.Serialize(value, options));
    }

    #endregion

    #region Schema And Validation

    [Fact]
    public void Schema_WritesATypedDefaultAsItsText()
    {
        var schema = JObject.Parse(ShokoJsonSchemaGeneratorGoldenTests.CreateGenerator().GetSchemaForType(typeof(NewtonsoftParsableConfiguration)).Schema.ToJson());

        Assert.Equal("string", schema["properties"]!["Version"]!["type"]!.Value<string>());
        Assert.Equal("1.2", schema["properties"]!["Version"]!["default"]!.Value<string>());
    }

    [Theory]
    [InlineData(typeof(NewtonsoftParsableConfiguration))]
    [InlineData(typeof(SystemTextJsonParsableConfiguration))]
    public void Validate_ReportsTextThatDoesNotParseAtItsPath(Type type)
    {
        var applicationPaths = new Mock<IApplicationPaths>();
        applicationPaths.SetupGet(x => x.DataPath).Returns(Path.GetTempPath());
        applicationPaths.SetupGet(x => x.ConfigurationsPath).Returns(Path.GetTempPath());
        var service = new ConfigurationService(NullLoggerFactory.Instance, applicationPaths.Object, Mock.Of<IPluginManager>());
        var info = new ConfigurationInfo(service)
        {
            ID = Guid.NewGuid(),
            Path = null,
            Name = "Parsable",
            Description = string.Empty,
            HasCustomActions = false,
            HasCustomNewFactory = false,
            HasCustomValidation = false,
            HasCustomSave = false,
            HasCustomLoad = false,
            HasLiveEdit = false,
            Type = type,
            ContextualType = type.ToContextualType(),
            Schema = ShokoJsonSchemaGeneratorGoldenTests.CreateGenerator().GetSchemaForType(type).Schema,
            PluginInfo = null!,
        };

        var errors = service.Validate(info, """{"Version":"nope"}""");

        var (path, messages) = Assert.Single(errors);
        Assert.Equal("Version", path);
        Assert.Contains("\"nope\" is not a valid TextVersion", Assert.Single(messages));
    }

    #endregion

    #region Types

    public class NewtonsoftParsableConfiguration : INewtonsoftJsonConfiguration
    {
        [VersionDefault]
        public TextVersion Version { get; set; } = new(1, 2);

        public TextVersion? Optional { get; set; }

        public List<TextVersion> Versions { get; set; } = [new(2, 0), new(3, 1)];

        public OwnVersion Own { get; set; } = new(new(1, 0));
    }

    public class SystemTextJsonParsableConfiguration : IConfiguration
    {
        public TextVersion Version { get; set; } = new(1, 2);

        public TextVersion? Optional { get; set; }

        public List<TextVersion> Versions { get; set; } = [new(2, 0), new(3, 1)];

        public OwnVersion Own { get; set; } = new(new(1, 0));
    }

    /// <summary>A typed default, as the attribute can not take a struct.</summary>
    public sealed class VersionDefaultAttribute() : DefaultValueAttribute(new TextVersion(1, 2));

    /// <summary>A version parsable from <c>major.minor</c>, with no converter.</summary>
    public readonly record struct TextVersion(int Major, int Minor) : IParsable<TextVersion>
    {
        public static TextVersion Parse(string s, IFormatProvider? provider)
            => TryParse(s, provider, out var result) ? result : throw new FormatException(s);

        public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out TextVersion result)
        {
            result = default;
            var parts = s?.Split('.');
            if (parts is not { Length: 2 } || !int.TryParse(parts[0], provider, out var major) || !int.TryParse(parts[1], provider, out var minor))
                return false;

            result = new(major, minor);
            return true;
        }

        public override string ToString()
            => $"{Major}.{Minor}";
    }

    /// <summary>A parsable type with JSON converters of its own, writing a <c>v</c> prefix.</summary>
    [Newtonsoft.Json.JsonConverter(typeof(OwnVersionNewtonsoftConverter))]
    [System.Text.Json.Serialization.JsonConverter(typeof(OwnVersionSystemTextJsonConverter))]
    public sealed record OwnVersion(TextVersion Value) : IParsable<OwnVersion>
    {
        public static OwnVersion Parse(string s, IFormatProvider? provider)
            => new(TextVersion.Parse(s, provider));

        public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, [NotNullWhen(true)] out OwnVersion? result)
        {
            result = TextVersion.TryParse(s, provider, out var value) ? new(value) : null;
            return result is not null;
        }
    }

    public sealed class OwnVersionNewtonsoftConverter : Newtonsoft.Json.JsonConverter<OwnVersion>
    {
        public override void WriteJson(JsonWriter writer, OwnVersion? value, Newtonsoft.Json.JsonSerializer serializer)
            => writer.WriteValue($"v{value!.Value}");

        public override OwnVersion ReadJson(JsonReader reader, Type objectType, OwnVersion? existingValue, bool hasExistingValue, Newtonsoft.Json.JsonSerializer serializer)
            => OwnVersion.Parse(((string)reader.Value!)[1..], null);
    }

    public sealed class OwnVersionSystemTextJsonConverter : System.Text.Json.Serialization.JsonConverter<OwnVersion>
    {
        public override OwnVersion Read(ref System.Text.Json.Utf8JsonReader reader, Type typeToConvert, System.Text.Json.JsonSerializerOptions options)
            => OwnVersion.Parse(reader.GetString()![1..], null);

        public override void Write(System.Text.Json.Utf8JsonWriter writer, OwnVersion value, System.Text.Json.JsonSerializerOptions options)
            => writer.WriteStringValue($"v{value.Value}");
    }

    #endregion
}
