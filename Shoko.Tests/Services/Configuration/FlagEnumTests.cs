using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.UI.Attributes;
using Shoko.Abstractions.UI.Elements;
using Shoko.Abstractions.UI.Enums;
using Shoko.Server.Services.Configuration;
using Xunit;

using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Shoko.Tests.Services.Configuration;

/// <summary>
///   Coverage for a <c>[Flags]</c> enum treated as a list of its single-bit
///   members: the converters, the schema and UI definition, legacy values in
///   validation, and options providers.
/// </summary>
[Collection(ConfigurationSchemaCollection.Name)]
public class FlagEnumTests
{
    #region Converters

    [Theory]
    [InlineData(Access.None, "[]")]
    [InlineData(Access.Read, """["Read"]""")]
    [InlineData(Access.Read | Access.Execute, """["Read","run"]""")]
    [InlineData(Access.All, """["Read","Write","run"]""")]
    public void Newtonsoft_WritesTheSingleBitNames(Access value, string expected)
    {
        var json = JsonConvert.SerializeObject(value, Formatting.None, ShokoJsonSerializers.CreateNewtonsoftSettings());

        Assert.Equal(expected, json);
        Assert.Equal(value, JsonConvert.DeserializeObject<Access>(json, ShokoJsonSerializers.CreateNewtonsoftSettings()));
    }

    [Theory]
    [InlineData(Access.None, "[]")]
    [InlineData(Access.Read | Access.Execute, """["Read","Execute"]""")]
    public void SystemTextJson_WritesTheSingleBitNames(Access value, string expected)
    {
        var options = ShokoJsonSerializers.CreateSystemTextJsonOptions();
        options.WriteIndented = false;
        var json = JsonSerializer.Serialize(value, options);

        Assert.Equal(expected, json);
        Assert.Equal(value, JsonSerializer.Deserialize<Access>(json, options));
    }

    [Theory]
    [InlineData("""["write","Read"]""", Access.Read | Access.Write)]
    [InlineData("""["All"]""", Access.All)]
    [InlineData("""["Execute"]""", Access.Execute)]
    [InlineData("\"Read, Write\"", Access.Read | Access.Write)]
    [InlineData("\"All\"", Access.All)]
    [InlineData("\"\"", Access.None)]
    [InlineData("5", Access.Read | Access.Execute)]
    public void Both_ReadEveryAcceptedForm(string json, Access expected)
    {
        Assert.Equal(expected, JsonConvert.DeserializeObject<Access>(json, ShokoJsonSerializers.CreateNewtonsoftSettings()));
        Assert.Equal(expected, JsonSerializer.Deserialize<Access>(json, ShokoJsonSerializers.CreateSystemTextJsonOptions()));
    }

    [Theory]
    [InlineData("8")]
    [InlineData("""["Delete"]""")]
    [InlineData("\"Read, Delete\"")]
    public void Both_RefuseBitsNoMemberNames(string json)
    {
        Assert.Throws<JsonSerializationException>(() => JsonConvert.DeserializeObject<Access>(json, ShokoJsonSerializers.CreateNewtonsoftSettings()));
        Assert.Throws<System.Text.Json.JsonException>(() => JsonSerializer.Deserialize<Access>(json, ShokoJsonSerializers.CreateSystemTextJsonOptions()));
    }

    [Fact]
    public void Newtonsoft_RefusesToWriteBitsNoMemberNames()
        => Assert.Throws<JsonSerializationException>(() => JsonConvert.SerializeObject((Access)8, ShokoJsonSerializers.CreateNewtonsoftSettings()));

    [Fact]
    public void Newtonsoft_KeepsANullableNull()
    {
        var value = JsonConvert.DeserializeObject<FlagConfiguration>("""{"Optional": null}""", ShokoJsonSerializers.CreateNewtonsoftSettings())!;

        Assert.Null(value.Optional);
    }

    #endregion

    #region Schema

    [Fact]
    public void Schema_DescribesTheMemberAsAUniqueArrayOfTheSingleBitNames()
    {
        var schema = JObject.Parse(CreateWrapped().Schema.ToJson());
        var access = schema["properties"]!["Access"]!;
        var entry = schema["definitions"]![access["items"]!["$ref"]!.Value<string>()!.Split('/')[^1]]!;

        Assert.Equal("array", access["type"]!.Value<string>());
        Assert.True(access["uniqueItems"]!.Value<bool>());
        Assert.Equal(["Read", "Write", "run"], entry["enum"]!.Values<string>());
        Assert.Null(entry["x-enumFlags"]);
        Assert.Equal(["Read", "Write"], access["default"]!.Values<string>());
    }

    [Fact]
    public void Definition_IsTheElementOfAListOfTheEnum()
    {
        var definition = new UiDefinitionBuilder(NullLogger<UiDefinitionBuilder>.Instance).Build(Guid.Empty, "Flags", null, CreateWrapped(), listsOptions: true);
        var root = Assert.IsType<UiSectionContainerElement>(definition.Root);
        var access = Assert.IsType<UiListElement>(root.Items["Access"]);
        var entry = Assert.IsType<UiEnumElement>(access.Item);

        Assert.Equal(DisplayListType.EnumCheckbox, access.ListType);
        Assert.True(access.UniqueItems);
        Assert.False(access.Sortable);
        Assert.Equal(["Read", "Write", "run"], entry.Values.Select(x => x.Value));
        Assert.Equal("Options", entry.OptionsRoute);
    }

    [Theory]
    [InlineData(typeof(ListOfFlagsConfiguration), "A collection cannot hold another collection directly")]
    [InlineData(typeof(EmptyFlagsConfiguration), "has no member with a single bit set")]
    public void Generation_RefusesAShapeItCannotRender(Type type, string fault)
    {
        var exception = Assert.Throws<NotSupportedException>(() => ShokoJsonSchemaGeneratorGoldenTests.CreateGenerator().GetSchemaForType(type));

        Assert.Contains(fault, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Generation_RefusesAFlagsEnumAsADictionaryKey()
    {
        var exception = Assert.Throws<ArgumentException>(() => ShokoJsonSchemaGeneratorGoldenTests.CreateGenerator().GetSchemaForType(typeof(FlagKeyConfiguration)));

        Assert.Contains("is a flags enum", exception.Message, StringComparison.Ordinal);
    }

    #endregion

    #region Validation

    [Theory]
    [InlineData("\"Read, Write\"", "Read,Write")]
    [InlineData("3", "Read,Write")]
    [InlineData("""["write","READ"]""", "Read,Write")]
    [InlineData("""["All"]""", "Read,Write,run")]
    [InlineData("\"\"", "")]
    public void Validate_ReadsAnyAcceptedFormIntoTheList(string value, string expected)
    {
        var (token, errors) = Validate($$"""{"Access": {{value}}}""");

        Assert.Empty(errors);
        Assert.Equal(expected.Split(',', StringSplitOptions.RemoveEmptyEntries), token["Access"]!.Values<string>());
    }

    [Theory]
    [InlineData("8")]
    [InlineData("\"Delete\"")]
    [InlineData("""["Delete"]""")]
    public void Validate_RefusesWhatNoEntryNames(string value)
    {
        var (_, errors) = Validate($$"""{"Access": {{value}}}""");

        Assert.NotEmpty(errors);
    }

    #endregion

    #region Options

    [Fact]
    public async Task Options_ListTheEntriesTheProviderNarrowedTo()
    {
        var configuration = new FlagConfiguration();
        var (owner, method) = UiOptionsProvider.Resolve(configuration, "Access", OptionsTarget.Values, isNewtonsoftJson: true);
        var settings = ShokoJsonSerializers.CreateNewtonsoftSettings();
        var options = await UiOptionsProvider.InvokeAsync(
            method,
            Mock.Of<IPluginManager>(),
            owner,
            [configuration],
            value => value is null ? null : JToken.FromObject(value, Newtonsoft.Json.JsonSerializer.Create(settings))
        );

        Assert.Equal([("Read", "Read"), ("run", "Execute")], options.Select(x => (x.Value!.Value<string>(), x.Label)));
    }

    [Fact]
    public void Options_OfAnotherTypeAreRefused()
    {
        var exception = Assert.Throws<NotSupportedException>(() => ShokoJsonSchemaGeneratorGoldenTests.CreateGenerator().GetSchemaForType(typeof(MismatchedOptionsConfiguration)));

        Assert.Contains("rather than a collection of Access", exception.Message, StringComparison.Ordinal);
    }

    #endregion

    #region Helpers

    private static WrappedJsonSchema CreateWrapped()
        => ShokoJsonSchemaGeneratorGoldenTests.CreateGenerator().GetSchemaForType(typeof(FlagConfiguration));

    private static (JToken Token, ICollection<NJsonSchema.Validation.ValidationError> Errors) Validate(string json)
        => new ShokoJsonSchemaValidator<FlagConfiguration>(NullLogger.Instance, null!, null!, null, saveValidation: false, loadValidation: true)
            .Validate(json, CreateWrapped().Schema);

    #endregion

    #region Types

    [Flags]
    public enum Access
    {
        None = 0,
        Read = 1,
        Write = 2,
        [EnumMember(Value = "run")]
        Execute = 4,
        All = Read | Write | Execute,
    }

    [Flags]
    public enum Empty
    {
        None = 0,
        Both = 3,
    }

    public class FlagConfiguration : INewtonsoftJsonConfiguration
    {
        [List(ListType = DisplayListType.EnumCheckbox)]
        [System.ComponentModel.DefaultValue(Access.Read | Access.Write)]
        public Access Access { get; set; } = Access.Read | Access.Write;

        public Access? Optional { get; set; }

        [OptionsProvider(nameof(Access))]
        public Access[] ListAccess() => [Access.Read, Access.All, Access.None, Access.Execute];
    }

    public class ListOfFlagsConfiguration : INewtonsoftJsonConfiguration
    {
        public List<Access> Accesses { get; set; } = [];
    }

    public class EmptyFlagsConfiguration : INewtonsoftJsonConfiguration
    {
        public Empty Empty { get; set; }
    }

    public class FlagKeyConfiguration : INewtonsoftJsonConfiguration
    {
        public Dictionary<Access, string> Names { get; set; } = [];
    }

    public class MismatchedOptionsConfiguration : INewtonsoftJsonConfiguration
    {
        public Access Access { get; set; }

        [OptionsProvider(nameof(Access))]
        public int[] ListAccess() => [1];
    }

    #endregion
}
