using System.Collections.Generic;
using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Shoko.Abstractions.Config;
using Shoko.Server.Services.Configuration;
using Xunit;

namespace Shoko.Tests.Services.Configuration;

/// <summary>
///   Coverage for <see cref="ReadOnlyConfigurationProperties"/>: what a
///   stored configuration leaves out, for both serializers.
/// </summary>
public class ReadOnlyConfigurationPropertiesTests
{
    private static readonly ReadOnlyConfigurationProperties _properties = new(
        ShokoJsonSerializers.CreateNewtonsoftSettings(),
        ShokoJsonSerializers.CreateSystemTextJsonOptions()
    );

    [Fact]
    public void Newtonsoft_RemovesReadOnlyPropertiesAtEveryLevel()
    {
        var token = JToken.Parse(JsonConvert.SerializeObject(NewtonsoftConfiguration.Sample(), ShokoJsonSerializers.CreateNewtonsoftSettings()));
        ((JObject)token)["$schema"] = "./sample.schema.json";

        _properties.Strip(token, typeof(NewtonsoftConfiguration), useNewtonsoft: true);

        Assert.Null(token["Display"]);
        Assert.Equal("root", token["Name"]!.Value<string>());
        Assert.Equal("./sample.schema.json", token["$schema"]!.Value<string>());
        Assert.Equal("kept", token["PrivateSetter"]!.Value<string>());
        Assert.All((JArray)token["Rules"]!, rule =>
        {
            Assert.Null(rule["Key"]);
            Assert.NotNull(rule["Name"]);
        });
        Assert.Null(token["ByName"]!["first"]!["Key"]);
        Assert.Equal("first", token["ByName"]!["first"]!["Name"]!.Value<string>());
        Assert.Null(token["Nested"]!["Key"]);
    }

    [Fact]
    public void Newtonsoft_StrippedDocumentStillReadsBack()
    {
        var token = JToken.Parse(JsonConvert.SerializeObject(NewtonsoftConfiguration.Sample(), ShokoJsonSerializers.CreateNewtonsoftSettings()));
        _properties.Strip(token, typeof(NewtonsoftConfiguration), useNewtonsoft: true);

        var read = token.ToObject<NewtonsoftConfiguration>(JsonSerializer.Create(ShokoJsonSerializers.CreateNewtonsoftSettings()))!;

        Assert.Equal("root", read.Name);
        Assert.Equal(["a", "b"], read.Rules.ConvertAll(rule => rule.Name));
        Assert.Equal("Rule: a", read.Rules[0].Key);
    }

    [Fact]
    public void SystemTextJson_RemovesReadOnlyPropertiesAtEveryLevel()
    {
        var token = JToken.Parse(System.Text.Json.JsonSerializer.Serialize(SystemTextJsonConfiguration.Sample(), ShokoJsonSerializers.CreateSystemTextJsonOptions()));
        ((JObject)token)["$schema"] = "./sample.schema.json";

        _properties.Strip(token, typeof(SystemTextJsonConfiguration), useNewtonsoft: false);

        Assert.Null(token["Display"]);
        Assert.Equal("root", token["Name"]!.Value<string>());
        Assert.Equal("./sample.schema.json", token["$schema"]!.Value<string>());
        Assert.Equal("kept", token["Included"]!.Value<string>());
        Assert.All((JArray)token["Rules"]!, rule =>
        {
            Assert.Null(rule["Key"]);
            Assert.NotNull(rule["Name"]);
        });
        Assert.Null(token["ByName"]!["first"]!["Key"]);
    }

    public class Rule
    {
        public string Name { get; set; } = string.Empty;

        public string Key
        {
            get => $"Rule: {Name}";
        }
    }

    public class NewtonsoftConfiguration : INewtonsoftJsonConfiguration
    {
        public string Name { get; set; } = string.Empty;

        public string Display => $"Display: {Name}";

        [JsonProperty]
        public string PrivateSetter { get; private set; } = "kept";

        public List<Rule> Rules { get; set; } = [];

        public Dictionary<string, Rule> ByName { get; set; } = [];

        public Rule Nested { get; set; } = new();

        public static NewtonsoftConfiguration Sample()
            => new()
            {
                Name = "root",
                Rules = [new() { Name = "a" }, new() { Name = "b" }],
                ByName = new() { ["first"] = new() { Name = "first" } },
                Nested = new() { Name = "nested" },
            };
    }

    public class SystemTextJsonConfiguration : IConfiguration
    {
        public string Name { get; set; } = string.Empty;

        public string Display => $"Display: {Name}";

        [JsonInclude]
        public string Included { get; private set; } = "kept";

        public List<Rule> Rules { get; set; } = [];

        public Dictionary<string, Rule> ByName { get; set; } = [];

        public static SystemTextJsonConfiguration Sample()
            => new()
            {
                Name = "root",
                Rules = [new() { Name = "a" }],
                ByName = new() { ["first"] = new() { Name = "first" } },
            };
    }
}
