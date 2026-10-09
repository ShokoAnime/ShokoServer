using System;
using Microsoft.Extensions.Logging.Abstractions;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.UI.Attributes;
using Shoko.Abstractions.UI.Elements;
using Shoko.Abstractions.UI.Enums;
using Shoko.Server.Services.Configuration;
using Xunit;

namespace Shoko.Tests.Services;

/// <summary>
///   Coverage for the switch a checkbox section names: carried on its
///   container, and checked when the schema is generated.
/// </summary>
[Collection(ConfigurationSchemaCollection.Name)]
public class SectionToggleTests
{
    [Fact]
    public void ACheckboxSection_CarriesItsToggleMember()
    {
        var wrapped = ShokoJsonSchemaGeneratorGoldenTests.CreateGenerator().GetSchemaForType(typeof(ToggledConfiguration));
        var root = Assert.IsType<UiSectionContainerElement>(new UiDefinitionBuilder(NullLogger<UiDefinitionBuilder>.Instance).Build(Guid.Empty, "Fixture", null, wrapped).Root);

        var section = Assert.IsType<UiSectionContainerElement>(root.Items[nameof(ToggledConfiguration.Experimental)]);
        Assert.Equal(nameof(ToggledSection.Enabled), section.ToggleMember);
        Assert.Null(root.ToggleMember);
    }

    [Theory]
    [InlineData(typeof(MissingToggleConfiguration))]
    [InlineData(typeof(TextToggleConfiguration))]
    [InlineData(typeof(StrayToggleConfiguration))]
    public void AnUnfitToggle_FailsGeneration(Type type)
        => Assert.Throws<NotSupportedException>(() => ShokoJsonSchemaGeneratorGoldenTests.CreateGenerator().GetSchemaForType(type));

    public class ToggledConfiguration : IConfiguration
    {
        public ToggledSection Experimental { get; set; } = new();
    }

    [Section(DisplaySectionType.Checkbox, ToggleMember = nameof(Enabled))]
    public class ToggledSection
    {
        public bool Enabled { get; set; }

        public string Value { get; set; } = string.Empty;
    }

    public class MissingToggleConfiguration : IConfiguration
    {
        public MissingToggleSection Section { get; set; } = new();
    }

    [Section(DisplaySectionType.Checkbox)]
    public class MissingToggleSection
    {
        public bool Enabled { get; set; }
    }

    public class TextToggleConfiguration : IConfiguration
    {
        public TextToggleSection Section { get; set; } = new();
    }

    [Section(DisplaySectionType.Checkbox, ToggleMember = nameof(Value))]
    public class TextToggleSection
    {
        public string Value { get; set; } = string.Empty;
    }

    [Section(DisplaySectionType.FieldSet, ToggleMember = nameof(Enabled))]
    public class StrayToggleConfiguration : IConfiguration
    {
        public bool Enabled { get; set; }
    }
}
