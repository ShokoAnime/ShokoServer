using System;
using System.ComponentModel;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json.Linq;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.UI;
using Shoko.Abstractions.UI.Elements;
using Shoko.Server.Services.Configuration;
using Xunit;

namespace Shoko.Tests.Services;

/// <summary>
///   Coverage for the defaults a definition carries: what a freshly
///   constructed instance holds, initialisers included.
/// </summary>
[Collection(ConfigurationSchemaCollection.Name)]
public class UiDefaultsTests
{
    private static UiSectionContainerElement RootOf(UiDefinition definition)
        => Assert.IsType<UiSectionContainerElement>(definition.Root);

    [Fact]
    public void Configuration_TakesItsDefaultsFromTheInitialisers()
    {
        var wrapped = ShokoJsonSchemaGeneratorGoldenTests.CreateGenerator().GetSchemaForType(typeof(InitialisedConfiguration));
        var root = RootOf(new UiDefinitionBuilder(NullLogger<UiDefinitionBuilder>.Instance).Build(Guid.Empty, "Fixture", null, wrapped));

        Assert.Equal(JToken.FromObject("Hello"), root.Items[nameof(InitialisedConfiguration.Greeting)].Default);
        Assert.Equal(JToken.FromObject(3), root.Items[nameof(InitialisedConfiguration.Count)].Default);
        Assert.Equal(JToken.FromObject("declared"), root.Items[nameof(InitialisedConfiguration.Declared)].Default);
        Assert.Null(root.Items[nameof(InitialisedConfiguration.Password)].Default);
        Assert.Null(root.Items[nameof(InitialisedConfiguration.Nested)].Default);
    }

    [Fact]
    public void Action_TakesItsDefaultsFromTheInstanceItWasBuiltWith()
    {
        var builder = new ActionUiDefinitionBuilder(NullLoggerFactory.Instance);
        var probe = new ParameterisedGlobalAction { MaxResults = 7 };

        var root = RootOf(builder.Build(Guid.Empty, "Action", null, typeof(ParameterisedGlobalAction), instance: probe)!.Definition);

        Assert.Equal(JToken.FromObject(7), root.Items[nameof(ParameterisedGlobalAction.MaxResults)].Default);
        Assert.Equal(JToken.FromObject("balanced"), root.Items[nameof(ParameterisedGlobalAction.Mode)].Default);
    }

    /// <summary>A configuration with initialised members.</summary>
    public class InitialisedConfiguration : IConfiguration
    {
        /// <summary>Initialised.</summary>
        public string Greeting { get; set; } = "Hello";

        /// <summary>Initialised.</summary>
        public int Count { get; set; } = 3;

        /// <summary>Declared, which wins.</summary>
        [DefaultValue("declared")]
        public string Declared { get; set; } = "initialised";

        /// <summary>A secret, never offered as a default.</summary>
        [PasswordPropertyText]
        public string Password { get; set; } = "hunter2";

        /// <summary>A nested class, whose own members carry their defaults.</summary>
        public NestedSection Nested { get; set; } = new();
    }

    /// <summary>A nested class.</summary>
    public class NestedSection
    {
        /// <summary>Initialised.</summary>
        public bool Enabled { get; set; } = true;
    }
}
