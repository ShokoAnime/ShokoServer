using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using Shoko.Abstractions.UI.Elements;
using Shoko.Server.Services.Configuration;
using Xunit;

namespace Shoko.Tests.Services;

/// <summary>
///   Coverage for where denied values land: on the element that can actually
///   act on them, which for a collection is the item rather than the collection
///   itself.
/// </summary>
[Collection(ConfigurationSchemaCollection.Name)]
public class UiDeniedValuesTests
{
    private static UiSectionContainerElement RootOf(Type type)
    {
        var wrapped = ShokoJsonSchemaGeneratorGoldenTests.CreateGenerator().GetSchemaForType(type);
        var definition = new UiDefinitionBuilder(NullLogger<UiDefinitionBuilder>.Instance).Build(Guid.Empty, "Denied", null, wrapped);
        return Assert.IsType<UiSectionContainerElement>(definition.Root);
    }

    [Fact]
    public void AScalarCarriesItsOwnDeniedValues()
    {
        var root = RootOf(typeof(DeniedValuesConfiguration));

        Assert.Equal(["Auto", "None"], root.Items["Mode"].DeniedValues!.Select(x => x!.ToString()));
    }

    [Fact]
    public void ACollectionHoistsThemOntoTheItem()
    {
        var root = RootOf(typeof(DeniedValuesConfiguration));

        // `[DeniedValues]` on a list says which values an entry may not hold;
        // the list itself is never equal to one of them.
        var list = Assert.IsType<UiListElement>(root.Items["Modes"]);
        Assert.Null(list.DeniedValues);
        Assert.Equal(["Auto", "None"], list.Item.DeniedValues!.Select(x => x!.ToString()));

        var record = Assert.IsType<UiRecordElement>(root.Items["Weights"]);
        Assert.Null(record.DeniedValues);
        Assert.Equal(["0", "1"], record.Item.DeniedValues!.Select(x => x!.ToString()));
    }

    [Fact]
    public void AnElementThatCannotActOnThemCarriesNone()
    {
        var root = RootOf(typeof(DeniedValuesConfiguration));

        // A container has no value of its own to deny, and a select's options
        // live in the configuration value rather than in the definition, so
        // neither can act on a denied value.
        Assert.Null(root.Items["Nested"].DeniedValues);
        Assert.Null(root.Items["Picked"].DeniedValues);
    }

    /// <summary>A shape denying values in every position that allows it.</summary>
    public class DeniedValuesConfiguration
    {
        /// <summary>A scalar.</summary>
        [DeniedValues(DeniedMode.Auto, DeniedMode.None)]
        public DeniedMode Mode { get; set; }

        /// <summary>A list of scalars.</summary>
        [DeniedValues(DeniedMode.Auto, DeniedMode.None)]
        public List<DeniedMode> Modes { get; set; } = [];

        /// <summary>A record of scalars.</summary>
        [DeniedValues(0, 1)]
        public Dictionary<string, int> Weights { get; set; } = [];

        /// <summary>A nested class.</summary>
        [DeniedValues("nope")]
        public DeniedNested Nested { get; set; } = new();

        /// <summary>A server-populated selection.</summary>
        [DeniedValues("nope")]
        [Shoko.Abstractions.UI.Attributes.Select]
        public Shoko.Abstractions.UI.Components.SelectComponent<string> Picked { get; set; } = new();
    }

    /// <summary>A nested class.</summary>
    public class DeniedNested
    {
        /// <summary>A member.</summary>
        public string Name { get; set; } = string.Empty;
    }

    /// <summary>The modes denied above.</summary>
    public enum DeniedMode
    {
        /// <summary>Denied.</summary>
        Auto = 0,

        /// <summary>Allowed.</summary>
        Default = 1,

        /// <summary>Denied.</summary>
        None = 2,
    }
}
