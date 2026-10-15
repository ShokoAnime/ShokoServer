using System;
using System.Linq;
using System.Text.Json;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Abstractions.Config;
using Shoko.Server.Services.Configuration;
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

    private static WrappedJsonSchema Generate(Type type)
        => new ShokoJsonSchemaGenerator(new JsonSerializerSettings { Converters = [new StringEnumConverter()] }, new JsonSerializerOptions())
            .GetSchemaForType(type);

    private static string[] Structure(Type type)
    {
        // The authored member order lives on the typed builders now; the
        // `x-uiDefinition` bag only carries what the validator reads back.
        var wrapped = Generate(type);
        return [.. wrapped.UiBuilders[wrapped.Schema].Structure.Select(entry => entry.Name)];
    }

    [Fact]
    public void TheStructureListsOnlyTheInstanceSettings()
        => Assert.Equal([nameof(ConfigurationWithStatics.Value)], Structure(typeof(ConfigurationWithStatics)));
}
