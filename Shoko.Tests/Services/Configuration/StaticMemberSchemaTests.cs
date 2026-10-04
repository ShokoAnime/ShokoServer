using System;
using System.Linq;
using System.Text.Json;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Shoko.Abstractions.Config;
using Shoko.Server.Services.Configuration;
using Shoko.Server.Settings;
using Xunit;

namespace Shoko.Tests.Services.Configuration;

/// <summary>
/// Unit tests for how <see cref="ShokoJsonSchemaGenerator"/> treats the
/// constants and static members of a configuration class, which are never
/// settings, and the limits of its numbers.
/// </summary>
public class StaticMemberSchemaTests
{
    public class ConfigurationWithStatics : INewtonsoftJsonConfiguration
    {
        public const int Limit = 3;

        public static readonly TimeSpan Floor = TimeSpan.FromMinutes(15);

        public static int Shared { get; set; }

        public int Value { get; set; }
    }

    private static JObject Schema(Type type)
        => JObject.Parse(
            new ShokoJsonSchemaGenerator(new JsonSerializerSettings { Converters = [new StringEnumConverter()] }, new JsonSerializerOptions())
                .GetSchemaForType(type)
                .Schema
                .ToJson()
        );

    private static string[] Structure(JObject schema)
        => [.. ((JObject)schema["x-uiDefinition"]!["structure"]!).Properties().Select(property => property.Name)];

    [Fact]
    public void TheStructureListsOnlyTheInstanceSettings()
        => Assert.Equal([nameof(ConfigurationWithStatics.Value)], Structure(Schema(typeof(ConfigurationWithStatics))));

    [Fact]
    public void TheAiringScheduleSettingsListOnlyTheirSettingsWithTheirLimits()
    {
        var schema = Schema(typeof(AiringScheduleServiceSettings));
        var properties = ((JObject)schema["properties"]!).Properties().Select(property => property.Name).ToHashSet();

        Assert.All(Structure(schema), name => Assert.Contains(name, properties));
        Assert.DoesNotContain(Structure(schema), name => name.StartsWith("Minimum") || name.StartsWith("Maximum") || name.StartsWith("Default"));
        Assert.Equal(
            (AiringScheduleServiceSettings.MinimumSweepBudgetSeconds, AiringScheduleServiceSettings.MaximumSweepBudgetSeconds),
            (schema["properties"]!["SweepBudgetSeconds"]!["minimum"]!.Value<int>(), schema["properties"]!["SweepBudgetSeconds"]!["maximum"]!.Value<int>())
        );
        Assert.Equal(
            (AiringScheduleServiceSettings.MinimumRetentionMonths, AiringScheduleServiceSettings.MaximumRetentionMonths),
            (schema["properties"]!["RetentionMonths"]!["minimum"]!.Value<int>(), schema["properties"]!["RetentionMonths"]!["maximum"]!.Value<int>())
        );
    }
}
