using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Newtonsoft.Json.Linq;
using Shoko.Abstractions.Exceptions;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.UI;
using Shoko.Abstractions.UI.Attributes;
using Shoko.Abstractions.UI.Components;
using Shoko.Abstractions.UI.Elements;
using Shoko.Abstractions.UI.Enums;
using Shoko.Server.Services.Configuration;
using Xunit;

namespace Shoko.Tests.Services;

/// <summary>
///   Coverage for server-listed options: the startup check on the method an
///   <see cref="OptionsProviderAttribute"/> names, where the definition points
///   a client, and how a member path is resolved and its options listed.
/// </summary>
[Collection(ConfigurationSchemaCollection.Name)]
public class UiOptionsProviderTests
{
    #region Definition

    [Fact]
    public void EveryProvidedMemberHasOptions()
    {
        var wrapped = ShokoJsonSchemaGeneratorGoldenTests.CreateGenerator().GetSchemaForType(typeof(OptionsConfiguration));
        var definition = new UiDefinitionBuilder(NullLogger<UiDefinitionBuilder>.Instance).Build(Guid.Empty, "Options", null, wrapped, listsOptions: true);
        var root = Assert.IsType<UiSectionContainerElement>(definition.Root);
        var rows = Assert.IsType<UiListElement>(root.Items["Rows"]);
        var row = Assert.IsType<UiSectionContainerElement>(rows.Item);
        var tags = Assert.IsType<UiListElement>(root.Items["Tags"]);
        var weights = Assert.IsType<UiRecordElement>(root.Items["Weights"]);
        var groups = Assert.IsType<UiRecordElement>(root.Items["Groups"]);

        // The flag sits on the element that renders the choice: a list's
        // entry, a dictionary's key or value, or the member itself.
        Assert.All(
            new[] { root.Items["Port"], root.Items["Mode"], tags.Item, root.Items["Tag"], row.Items["Name"], weights.Item, Assert.IsType<UiListElement>(groups.Item).Item },
            element => Assert.True(element.HasOptions)
        );
        Assert.True(weights.KeyItem.HasOptions);
        Assert.All(
            new[] { root.Items["Plain"], row.Items["Prefix"], tags, rows, weights, groups.KeyItem },
            element => Assert.False(element.HasOptions)
        );
    }

    [Fact]
    public void AParsableTypeIsDescribedAsText()
    {
        var wrapped = ShokoJsonSchemaGeneratorGoldenTests.CreateGenerator().GetSchemaForType(typeof(ParsableConfiguration));
        var definition = new UiDefinitionBuilder(NullLogger<UiDefinitionBuilder>.Instance).Build(Guid.Empty, "Parsable", null, wrapped, listsOptions: true);
        var root = Assert.IsType<UiSectionContainerElement>(definition.Root);

        Assert.IsType<UiStringElement>(root.Items["Version"]);
        Assert.IsType<UiStringElement>(Assert.IsType<UiListElement>(root.Items["Versions"]).Item);
        Assert.IsType<UiSectionContainerElement>(root.Items["Row"]);
    }

    [Theory]
    [InlineData(typeof(MissingMemberConfiguration), "does not have")]
    [InlineData(typeof(MixedMembersConfiguration), "whose options are Int32 and String")]
    [InlineData(typeof(ClaimedTwiceConfiguration), "whose values Value already provides for")]
    [InlineData(typeof(WrongElementConfiguration), "rather than a collection of Int32")]
    [InlineData(typeof(KeysOfAScalarConfiguration), "is not a dictionary, so it has no keys")]
    [InlineData(typeof(WrongKeyTypeConfiguration), "rather than a collection of String")]
    [InlineData(typeof(WrongOptionsKeyConfiguration), "takes a key of String, but \"Weights\" is keyed by Int32")]
    [InlineData(typeof(ComplexOptionConfiguration), "neither a primitive nor convertible to and from one")]
    [InlineData(typeof(ConvertedForTextConfiguration), "returns TestColour[] rather than a collection of String")]
    [InlineData(typeof(TextForConvertedConfiguration), "returns String[] rather than a collection of TestColour")]
    public void AProviderThatDoesNotFitFailsGeneration(Type type, string fault)
    {
        var exception = Assert.Throws<NotSupportedException>(() => ShokoJsonSchemaGeneratorGoldenTests.CreateGenerator().GetSchemaForType(type));

        Assert.Contains($"The options provider '{type.Name}.", exception.Message, StringComparison.Ordinal);
        Assert.Contains(fault, exception.Message, StringComparison.Ordinal);
    }

    #endregion

    #region Lookup

    [Fact]
    public async Task TheProviderSeesTheEditedValues()
    {
        var options = await ListAsync(new OptionsConfiguration { Port = 8 }, "Port");

        Assert.Equal([8, 9], options.Select(x => x.Value!.Value<int>()));
    }

    [Fact]
    public async Task ATaskIsAwaitedForANullableMember()
    {
        var options = await ListAsync(new OptionsConfiguration(), "Mode");

        Assert.Equal([1, 2], options.Select(x => x.Value!.Value<int>()));
    }

    [Theory]
    [InlineData("Tags")]
    [InlineData("Tag")]
    public async Task ALabelledOptionKeepsItsLabelForEveryMemberItListsFor(string path)
    {
        var options = await ListAsync(new OptionsConfiguration(), path);

        Assert.Equal([("a", "Alpha"), ("b", "b")], options.Select(x => (x.Value!.Value<string>(), x.Label)));
    }

    [Fact]
    public async Task ADictionaryListsItsKeysAndValuesApartWithoutNulls()
    {
        var keys = await ListAsync(new OptionsConfiguration(), "Weights");
        var values = await ListAsync(new OptionsConfiguration(), "Weights[\"a\"]");

        // A value without a label is labelled with its own text.
        Assert.Equal([("first", "first"), ("first", "first")], keys.Select(x => (x.Value!.Value<string>(), x.Label)));
        Assert.Equal([("1", "1"), ("2", "2")], values.Select(x => (x.Value!.ToString(), x.Label)));
    }

    [Fact]
    public async Task AnEntryPathHandsItsKeyToTheValuesProviderEvenWhenNotInTheDraft()
    {
        var options = await ListAsync(new OptionsConfiguration(), "Limits[\"3\"]");

        Assert.Equal([3, 30], options.Select(x => x.Value!.Value<int>()));
    }

    [Fact]
    public void AKeyTheKeyTypeCannotHoldIsRejected()
        => Assert.ThrowsAny<ArgumentException>(() => UiOptionsProvider.Resolve(new OptionsConfiguration(), "Limits[\"x\"]", isNewtonsoftJson: true));

    [Theory]
    [InlineData(nameof(RefusingConfiguration.Sync))]
    [InlineData(nameof(RefusingConfiguration.Async))]
    public async Task AProviderRefusalSurfacesAsThrown(string path)
    {
        var configuration = new RefusingConfiguration();
        var (owner, method, _) = UiOptionsProvider.Resolve(configuration, path, isNewtonsoftJson: true);

        var exception = await Assert.ThrowsAsync<GenericValidationException>(
            () => UiOptionsProvider.InvokeAsync(method, Mock.Of<IPluginManager>(), owner, [], value => JToken.FromObject(value!))
        );

        Assert.Equal([path], exception.ValidationErrors.Keys);
    }

    [Fact]
    public async Task APluginTypeWithAConverterIsLabelledThroughIt()
    {
        var options = await ListAsync(new OptionsConfiguration(), "Colour");

        Assert.Equal(["#ff0000"], options.Select(x => x.Label));
    }

    [Fact]
    public async Task AnEnumIsLabelledAsTheFormNamesItsMembersUnlessTheProviderSaysOtherwise()
    {
        var options = await ListAsync(new OptionsConfiguration(), "Speed");

        Assert.Equal(["Go Fast", "Very Slow", "Mine"], options.Select(x => x.Label));
    }

    [Fact]
    public async Task APathIntoAListEntryListsForThatEntry()
    {
        var configuration = new OptionsConfiguration { Rows = [new() { Prefix = "x" }, new() { Prefix = "y" }] };

        var options = await ListAsync(configuration, "Rows[1].Name");

        Assert.Equal(["y1"], options.Select(x => x.Value!.Value<string>()));
    }

    [Theory]
    [InlineData("Plain")]
    [InlineData("Missing")]
    [InlineData("Rows[3].Name")]
    [InlineData("Rows[0]")]
    [InlineData("Tags[\"a\"]")]
    public void APathNotEndingInAProvidedMemberIsRejected(string path)
        => Assert.ThrowsAny<ArgumentException>(() => UiOptionsProvider.Resolve(new OptionsConfiguration { Rows = [new()] }, path, isNewtonsoftJson: true));

    private static async Task<IReadOnlyList<UiOption>> ListAsync(OptionsConfiguration configuration, string path)
    {
        var (owner, method, key) = UiOptionsProvider.Resolve(configuration, path, isNewtonsoftJson: true);
        return await UiOptionsProvider.InvokeAsync(
            method,
            Mock.Of<IPluginManager>(),
            owner,
            [configuration],
            value => value is null ? null : JToken.FromObject(value),
            key
        );
    }

    #endregion

    #region Types

    /// <summary>Options listed in every shape a provider may return them.</summary>
    public class OptionsConfiguration
    {
        /// <summary>A scalar, listed from its own value.</summary>
        public int Port { get; set; }

        /// <summary>A nullable scalar, listed through a task.</summary>
        public int? Mode { get; set; }

        /// <summary>A list sharing its provider with another member.</summary>
        public List<string> Tags { get; set; } = [];

        /// <summary>A scalar sharing its provider with the list.</summary>
        public string Tag { get; set; } = string.Empty;

        /// <summary>A member without options.</summary>
        public string Plain { get; set; } = string.Empty;

        /// <summary>Entries with options of their own.</summary>
        public List<OptionsRow> Rows { get; set; } = [];

        /// <summary>A plugin type converted to and from text.</summary>
        public TestColour Colour { get; set; } = new(0, 0, 0);

        /// <summary>An enum narrowed by its provider.</summary>
        public TestSpeed Speed { get; set; }

        /// <summary>A dictionary whose keys and values have providers of their own.</summary>
        public Dictionary<string, int?> Weights { get; set; } = [];

        /// <summary>A dictionary of lists, whose entries share the values' provider.</summary>
        public Dictionary<string, List<int>> Groups { get; set; } = [];

        /// <summary>A dictionary whose values are listed per key.</summary>
        public Dictionary<int, int> Limits { get; set; } = [];

        /// <summary>Lists limits for the slot asked for.</summary>
        [OptionsProvider(nameof(Limits))]
        public int[] ListLimits([OptionsKey] int slot)
            => [slot, slot * 10];

        /// <summary>Lists ports around the edited one.</summary>
        [OptionsProvider(nameof(Port))]
        public int[] ListPorts()
            => [Port, Port + 1];

        /// <summary>Lists modes.</summary>
        [OptionsProvider(nameof(Mode))]
        public Task<IReadOnlyList<int>> ListModesAsync()
            => Task.FromResult<IReadOnlyList<int>>([1, 2]);

        /// <summary>Lists colours.</summary>
        [OptionsProvider(nameof(Colour))]
        public static TestColour[] ListColours()
            => [new(255, 0, 0)];

        /// <summary>Lists speeds, one with a label of its own.</summary>
        [OptionsProvider(nameof(Speed))]
        public static SelectOption<TestSpeed>[] ListSpeeds()
            => [new(TestSpeed.Fast), new(TestSpeed.VerySlow), new(TestSpeed.Medium, "Mine")];

        /// <summary>Lists dictionary keys, duplicates kept.</summary>
        [OptionsProvider(nameof(Weights), Target = OptionsTarget.Keys)]
        public string[] ListWeightKeys()
            => ["first", "first"];

        /// <summary>Lists dictionary values, a null among them.</summary>
        [OptionsProvider(nameof(Weights), nameof(Groups))]
        public int?[] ListWeightValues()
            => [1, null, 2];

        /// <summary>A method of the same name, which is not registered and so not checked.</summary>
        public void ListWeightValues(int unrelated) { }

        /// <summary>Lists tags.</summary>
        [OptionsProvider(nameof(Tags), nameof(Tag))]
        public static SelectOption<string>[] ListTags()
            => [new("a", "Alpha"), new("b")];
    }

    /// <summary>A list entry listing options from its own values.</summary>
    public class OptionsRow
    {
        /// <summary>A name, listed from the prefix.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>The prefix.</summary>
        public string Prefix { get; set; } = string.Empty;

        /// <summary>Lists names.</summary>
        [OptionsProvider(nameof(Name))]
        public IEnumerable<string> ListNames()
            => [Prefix + "1"];
    }

    /// <summary>Names a member that is not there.</summary>
    public class MissingMemberConfiguration
    {
        /// <summary>A member, named wrong below.</summary>
        public int Number { get; set; }

        /// <summary>Lists values.</summary>
        [OptionsProvider("Nope")]
        public int[] Value()
            => [];
    }

    /// <summary>Names two members of different option types.</summary>
    public class MixedMembersConfiguration
    {
        /// <summary>A number.</summary>
        public int Number { get; set; }

        /// <summary>A text.</summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>Lists values.</summary>
        [OptionsProvider(nameof(Number), nameof(Text))]
        public int[] Value()
            => [];
    }

    /// <summary>Claims one member from two providers.</summary>
    public class ClaimedTwiceConfiguration
    {
        /// <summary>The member.</summary>
        public int Number { get; set; }

        /// <summary>Lists values.</summary>
        [OptionsProvider(nameof(Number))]
        public int[] Value()
            => [];

        /// <summary>Lists them again.</summary>
        [OptionsProvider(nameof(Number))]
        public int[] Again()
            => [];
    }

    /// <summary>Lists values of another type.</summary>
    public class WrongElementConfiguration
    {
        /// <summary>The member.</summary>
        public int Number { get; set; }

        /// <summary>Lists longs.</summary>
        [OptionsProvider(nameof(Number))]
        public long[] Value()
            => [];
    }

    /// <summary>Asks for the keys of a scalar.</summary>
    public class KeysOfAScalarConfiguration
    {
        /// <summary>The member.</summary>
        public int Number { get; set; }

        /// <summary>Lists keys.</summary>
        [OptionsProvider(nameof(Number), Target = OptionsTarget.Keys)]
        public int[] Value()
            => [];
    }

    /// <summary>Lists keys of the value type.</summary>
    public class WrongKeyTypeConfiguration
    {
        /// <summary>The member.</summary>
        public Dictionary<string, int> Weights { get; set; } = [];

        /// <summary>Lists numbers for string keys.</summary>
        [OptionsProvider(nameof(Weights), Target = OptionsTarget.Keys)]
        public int[] Value()
            => [];
    }

    /// <summary>Takes a key of another type than the dictionary's.</summary>
    public class WrongOptionsKeyConfiguration
    {
        /// <summary>The member.</summary>
        public Dictionary<int, int> Weights { get; set; } = [];

        /// <summary>Lists numbers for a text key.</summary>
        [OptionsProvider(nameof(Weights))]
        public int[] Value([OptionsKey] string key)
            => [];
    }

    /// <summary>Lists options of a type that cannot be told apart as text.</summary>
    public class ComplexOptionConfiguration
    {
        /// <summary>The member.</summary>
        public OptionsRow Row { get; set; } = new();

        /// <summary>Lists rows.</summary>
        [OptionsProvider(nameof(Row))]
        public OptionsRow[] Value()
            => [];
    }

    /// <summary>Lists a convertible type for a text member, which a conversion does not bridge.</summary>
    public class ConvertedForTextConfiguration
    {
        /// <summary>The member.</summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>Lists colours.</summary>
        [OptionsProvider(nameof(Text))]
        public TestColour[] Value()
            => [];
    }

    /// <summary>Lists text for a convertible member, which a conversion does not bridge.</summary>
    public class TextForConvertedConfiguration
    {
        /// <summary>The member.</summary>
        public TestColour Colour { get; set; } = new(0, 0, 0);

        /// <summary>Lists text.</summary>
        [OptionsProvider(nameof(Colour))]
        public string[] Value()
            => [];
    }

    /// <summary>Members of a parsable type next to a plain one.</summary>
    public class ParsableConfiguration
    {
        /// <summary>A parsable value.</summary>
        public TestVersion Version { get; set; } = new(1, 0);

        /// <summary>A list of parsable values.</summary>
        public List<TestVersion> Versions { get; set; } = [];

        /// <summary>A plain record, still an object.</summary>
        public OptionsRow Row { get; set; } = new();
    }

    /// <summary>Providers refusing the draft, one sync and one async.</summary>
    public class RefusingConfiguration
    {
        /// <summary>Refused synchronously.</summary>
        public int Sync { get; set; }

        /// <summary>Refused through a task.</summary>
        public int Async { get; set; }

        /// <summary>Refuses at once.</summary>
        [OptionsProvider(nameof(Sync))]
        public int[] ListSync()
            => throw Refusal(nameof(Sync));

        /// <summary>Refuses after awaiting.</summary>
        [OptionsProvider(nameof(Async))]
        public async Task<int[]> ListAsync()
        {
            await Task.Yield();
            throw Refusal(nameof(Async));
        }

        private static GenericValidationException Refusal(string member)
            => new("Refused.", new Dictionary<string, IReadOnlyList<string>> { [member] = ["Refused."] });
    }

    #endregion
}

/// <summary>
///   A plugin-defined colour, converted to and from <c>#rrggbb</c>.
/// </summary>
/// <param name="Red">The red channel.</param>
/// <param name="Green">The green channel.</param>
/// <param name="Blue">The blue channel.</param>
[TypeConverter(typeof(TestColourConverter))]
public sealed record TestColour(byte Red, byte Green, byte Blue);

/// <summary>
///   Converts a <see cref="TestColour"/> to and from <c>#rrggbb</c>.
/// </summary>
public sealed class TestColourConverter : TypeConverter
{
    /// <inheritdoc />
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
        => sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

    /// <inheritdoc />
    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType)
        => destinationType == typeof(string) || base.CanConvertTo(context, destinationType);

    /// <inheritdoc />
    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
        => value is string text
            ? new TestColour(Convert.ToByte(text[1..3], 16), Convert.ToByte(text[3..5], 16), Convert.ToByte(text[5..7], 16))
            : base.ConvertFrom(context, culture, value);

    /// <inheritdoc />
    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
        => value is TestColour colour && destinationType == typeof(string)
            ? $"#{colour.Red:x2}{colour.Green:x2}{colour.Blue:x2}"
            : base.ConvertTo(context, culture, value, destinationType);
}

/// <summary>
///   A version parsable from <c>major.minor</c>, without a type converter.
/// </summary>
/// <param name="Major">The major part.</param>
/// <param name="Minor">The minor part.</param>
public readonly record struct TestVersion(int Major, int Minor) : IParsable<TestVersion>
{
    /// <inheritdoc />
    public static TestVersion Parse(string s, IFormatProvider? provider)
        => TryParse(s, provider, out var result) ? result : throw new FormatException(s);

    /// <inheritdoc />
    public static bool TryParse(string? s, IFormatProvider? provider, out TestVersion result)
    {
        result = default;
        var parts = s?.Split('.');
        if (parts is not { Length: 2 } || !int.TryParse(parts[0], provider, out var major) || !int.TryParse(parts[1], provider, out var minor))
            return false;

        result = new(major, minor);
        return true;
    }
}

/// <summary>
///   Speeds, one renamed for the form.
/// </summary>
public enum TestSpeed
{
    /// <summary>Slow.</summary>
    VerySlow = 0,

    /// <summary>Medium.</summary>
    Medium = 1,

    /// <summary>Fast.</summary>
    [System.ComponentModel.DataAnnotations.Display(Name = "Go Fast")]
    Fast = 2,
}
