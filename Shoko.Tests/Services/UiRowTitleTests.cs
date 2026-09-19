using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Logging.Abstractions;
using Shoko.Abstractions.UI.Components;
using Shoko.Abstractions.UI.Elements;
using Shoko.Abstractions.UI.Enums;
using Shoko.Server.Services.Configuration;
using Xunit;

namespace Shoko.Tests.Services;

/// <summary>
///   Coverage for what a row calls itself, and for the layout a collection
///   settles on when the class did not ask for one.
/// </summary>
[Collection(ConfigurationSchemaCollection.Name)]
public class UiRowTitleTests
{
    private static UiSectionContainerElement RootOf(Type type)
    {
        var wrapped = ShokoJsonSchemaGeneratorGoldenTests.CreateGenerator().GetSchemaForType(type);
        var definition = new UiDefinitionBuilder(NullLogger<UiDefinitionBuilder>.Instance).Build(Guid.Empty, "Rows", null, wrapped);
        return Assert.IsType<UiSectionContainerElement>(definition.Root);
    }

    [Fact]
    public void AClassSayingWhatItIsCalled_IsTakenAtItsWord()
    {
        var root = RootOf(typeof(RowConfiguration));

        var titled = Assert.IsType<UiListElement>(root.Items["Titled"]);
        Assert.Equal("Name.Title", titled.ItemTitlePath);
        Assert.Equal("Name.SubTitle", titled.ItemCategoryPath);

        // A record's values are labelled the same way.
        var keyed = Assert.IsType<UiRecordElement>(root.Items["Keyed"]);
        Assert.Equal("Name.Title", keyed.ItemTitlePath);
        Assert.Equal("Name.SubTitle", keyed.ItemCategoryPath);
    }

    [Fact]
    public void AClassWithoutOne_FallsBackToItsKey()
    {
        var root = RootOf(typeof(RowConfiguration));

        var keyed = Assert.IsType<UiListElement>(root.Items["KeyedOnly"]);
        Assert.Equal("ID", keyed.ItemTitlePath);
        Assert.Null(keyed.ItemCategoryPath);
    }

    [Fact]
    public void AClassWithNeither_LeavesTheKeyToLabelIt()
    {
        var root = RootOf(typeof(RowConfiguration));

        // A record entry with nothing to go on is labelled by the key it is
        // stored under, which only the document has.
        var plain = Assert.IsType<UiRecordElement>(root.Items["Plain"]);
        Assert.Null(plain.ItemTitlePath);
        Assert.Null(plain.ItemCategoryPath);
    }

    [Fact]
    public void AnUnauthoredLayout_IsSettledBeforeItIsServed()
    {
        var root = RootOf(typeof(RowConfiguration));

        // A class of its own is laid out one per entry; anything else is a row.
        Assert.Equal(DisplayListType.ComplexInline, Assert.IsType<UiListElement>(root.Items["Titled"]).ListType);
        Assert.Equal(DisplayListType.Flat, Assert.IsType<UiListElement>(root.Items["Names"]).ListType);
        Assert.Equal(DisplayRecordType.ComplexDropdown, Assert.IsType<UiRecordElement>(root.Items["Keyed"]).RecordType);
        Assert.Equal(DisplayRecordType.Flat, Assert.IsType<UiRecordElement>(root.Items["Plain"]).RecordType);

        // A list of enums stays a row per entry: checkboxes are a choice, not an
        // inference.
        Assert.Equal(DisplayListType.Flat, Assert.IsType<UiListElement>(root.Items["Modes"]).ListType);

        // An authored layout is never overridden.
        Assert.Equal(DisplayListType.EnumCheckbox, Assert.IsType<UiListElement>(root.Items["CheckedModes"]).ListType);
    }

    /// <summary>A shape with a row of every sort.</summary>
    public class RowConfiguration
    {
        /// <summary>Rows that say what they are called.</summary>
        public List<TitledRow> Titled { get; set; } = [];

        /// <summary>Values that say what they are called.</summary>
        public Dictionary<string, TitledRow> Keyed { get; set; } = [];

        /// <summary>Rows with only a key.</summary>
        public List<KeyedRow> KeyedOnly { get; set; } = [];

        /// <summary>Values with neither.</summary>
        public Dictionary<string, int> Plain { get; set; } = [];

        /// <summary>Plain rows.</summary>
        public List<string> Names { get; set; } = [];

        /// <summary>Enum rows.</summary>
        public List<RowMode> Modes { get; set; } = [];

        /// <summary>Enum rows the class asked to see as checkboxes.</summary>
        [Shoko.Abstractions.UI.Attributes.List(ListType = DisplayListType.EnumCheckbox)]
        public List<RowMode> CheckedModes { get; set; } = [];
    }

    /// <summary>A row saying what it is called.</summary>
    public class TitledRow
    {
        /// <summary>What it is called.</summary>
        public TitleComponent Name { get; set; } = new();

        /// <summary>A member.</summary>
        public string Url { get; set; } = string.Empty;
    }

    /// <summary>A row with only a key.</summary>
    public class KeyedRow
    {
        /// <summary>The key.</summary>
        [Key]
        public string ID { get; set; } = string.Empty;

        /// <summary>A member.</summary>
        public string Url { get; set; } = string.Empty;
    }

    /// <summary>The modes the rows hold.</summary>
    public enum RowMode
    {
        /// <summary>One.</summary>
        First = 0,

        /// <summary>Another.</summary>
        Second = 1,
    }
}
