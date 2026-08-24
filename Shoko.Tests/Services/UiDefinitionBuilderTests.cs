using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using Shoko.Abstractions.UI;
using Shoko.Abstractions.UI.Elements;
using Shoko.Abstractions.UI.Enums;
using Shoko.Server.Services.Configuration;
using Shoko.Server.Settings;
using Xunit;

namespace Shoko.Tests.Services;

/// <summary>
///   Coverage for <see cref="UiDefinitionBuilder"/>, the joiner that zips a
///   finished schema with the typed builders that produced it. The dump test
///   also writes the produced documents to <c>poc-output/</c> so the shape can
///   be eyeballed and the payload sizes compared.
/// </summary>
[Collection(ConfigurationSchemaCollection.Name)]
public class UiDefinitionBuilderTests
{
    private static UiDefinition BuildFor(Type type, string name)
    {
        var wrapped = ShokoJsonSchemaGeneratorGoldenTests.CreateGenerator().GetSchemaForType(type);
        var builder = new UiDefinitionBuilder(NullLogger<UiDefinitionBuilder>.Instance);
        return builder.Build(Guid.Empty, name, null, wrapped);
    }

    private static (UiDefinition Definition, string SchemaJson) BuildForServerSettings()
    {
        var wrapped = ShokoJsonSchemaGeneratorGoldenTests.CreateGenerator().GetSchemaForType(typeof(ServerSettings));
        var builder = new UiDefinitionBuilder(NullLogger<UiDefinitionBuilder>.Instance);
        var definition = builder.Build(Guid.Empty, wrapped.Schema.Title ?? "Core Settings", null, wrapped);
        return (definition, wrapped.Schema.ToJson());
    }

    /// <summary>
    ///   Every element reachable by key, paired with the key its container
    ///   files it under. A list's item and a record's key and value elements
    ///   are not filed under a key, so they do not appear here.
    /// </summary>
    private static IEnumerable<(string Key, UiElement Element)> Keyed(UiElement root)
        => Flatten(root)
            .OfType<UiSectionContainerElement>()
            .SelectMany(container => container.Items.Select(entry => (entry.Key, entry.Value)));

    private static T Find<T>(UiElement root, string key) where T : UiElement
        => Assert.IsType<T>(Assert.Single(Keyed(root), x => x.Key == key).Element);

    private static IEnumerable<UiElement> Flatten(UiElement element)
    {
        yield return element;
        switch (element)
        {
            case UiSectionContainerElement container:
                foreach (var item in container.Items.Values.SelectMany(Flatten))
                    yield return item;
                break;
            case UiListElement list:
                foreach (var item in Flatten(list.Item))
                    yield return item;
                break;
            case UiRecordElement record:
                foreach (var item in Flatten(record.KeyItem).Concat(Flatten(record.Item)))
                    yield return item;
                break;
        }
    }

    [Fact]
    public void ServerSettings_ProducesATreeWithoutUnknownOrAutoElements()
    {
        var (definition, _) = BuildForServerSettings();
        var elements = Flatten(definition.Root).ToList();

        Assert.IsType<UiSectionContainerElement>(definition.Root);
        Assert.DoesNotContain(elements, x => x is UiUnknownElement);
        Assert.All(elements, x => Assert.NotEqual(UiElementKind.Unknown, x.Kind));
        // A client indexes into `Items` by the key it sees in `Structure`, so
        // the map's key and the element's own key have to agree.
        Assert.NotEmpty(Keyed(definition.Root));
        Assert.All(
            elements.OfType<UiSectionContainerElement>(),
            container => Assert.All(container.Items, entry => Assert.Equal(entry.Key, entry.Value.Key))
        );
    }

    [Fact]
    public void ServerSettings_CarriesLabelsAndConstraints()
    {
        var (definition, _) = BuildForServerSettings();
        var elements = Flatten(definition.Root).ToList();

        // The code editor on `ServerSettings.WebUI_Settings` has to survive as a
        // concrete element, not as a generic string with a hint attached.
        Assert.Contains(elements, x => x is UiCodeEditorElement);
        // `PluginSettings.EnabledPlugins` is a `Dictionary<string, bool>`.
        var record = Find<UiRecordElement>(definition.Root, "EnabledPlugins");
        Assert.IsType<UiBooleanElement>(record.Item);
        Assert.IsType<UiStringElement>(record.KeyItem);
        // Every leaf has a non-empty label; the client should never fall back to
        // the property name itself.
        Assert.All(Keyed(definition.Root), x => Assert.NotEqual(string.Empty, x.Element.Label));
    }

    [Fact]
    public void EnumKeyedRecord_TypesTheKeyElementFromTheKeyType()
    {
        var definition = BuildFor(typeof(NewtonsoftTwinConfiguration), "Twin");
        var elements = Flatten(definition.Root).ToList();

        // `Dictionary<TwinMode, int>` — the schema says nothing about the key,
        // so this can only come off the builder's `KeyType`.
        var weights = Find<UiRecordElement>(definition.Root, "Weights");
        var key = Assert.IsType<UiEnumElement>(weights.KeyItem);
        Assert.Equal(["slow-and-steady", "balanced", "fast"], key.Values.Select(x => x.Value));
        Assert.Equal(["Slow", "Balanced", "Very Fast"], key.Values.Select(x => x.Title));
        Assert.IsType<UiIntegerElement>(weights.Item);

        // `Dictionary<string, bool>` still gets a free-text key.
        var toggles = Find<UiRecordElement>(definition.Root, "Toggles");
        Assert.IsType<UiStringElement>(toggles.KeyItem);
        Assert.IsType<UiBooleanElement>(toggles.Item);
    }

    [Fact]
    public void SectionContainer_InterleavesChildrenAndActionsInTheAuthoredOrder()
    {
        var definition = BuildFor(typeof(NewtonsoftTwinConfiguration), "Twin");
        var body = Find<UiSectionContainerElement>(definition.Root, "Body");

        // The structure lists every item and every action exactly once, in the
        // authored order, with the actions after the last property.
        Assert.Equal(
            body.Items.Keys.Concat(body.Actions.Keys),
            body.Structure.Select(x => x.Name)
        );
        Assert.Equal(body.Items.Count, body.Structure.Count(x => x.Kind is UiStructureMemberKind.Item));
        Assert.Equal(body.Actions.Count, body.Structure.Count(x => x.Kind is UiStructureMemberKind.Action));
        Assert.Equal(["Name", "Enabled", "Count", "Ratio", "Mode"], body.Items.Keys.Take(5));
        Assert.Equal(["DoTheThingAction", "DoAnotherThingAction"], body.Actions.Keys);
    }

    [Fact]
    public void InheritedMembers_KeepTheirDefinition()
    {
        var definition = BuildFor(typeof(InheritingConfiguration), "Inheriting");
        var root = Assert.IsType<UiSectionContainerElement>(definition.Root);

        // Every one of these but `Count` is declared on the base class, and the
        // generator files an inherited property under the type that declares
        // it, not under the one being generated.
        Assert.Equal(["Name", "Mode", "Endpoints", "Count"], root.Items.Keys);
        Assert.Equal(["Inherited Name", "Mode", "Endpoints", "Derived Count"], root.Items.Values.Select(x => x.Label));
        Assert.Equal(["DoTheInheritedThingAction"], root.Actions.Keys);

        var name = root.Items["Name"];
        Assert.True(name.RequiresRestart);
        Assert.Equal("INHERITED_NAME", name.EnvironmentVariable?.Name);
        Assert.Equal("Inherited", root.Items["Mode"].SectionName);
        // The derived class's own section attribute wins over the base's.
        Assert.Equal(DisplaySectionType.Tab, root.SectionType);
        Assert.Equal("Derived", root.DefaultSectionName);
        Assert.True(root.ShowSaveAction);
        // An inherited list still resolves its item class and primary key.
        var endpoints = Assert.IsType<UiListElement>(root.Items["Endpoints"]);
        Assert.Equal(DisplayListType.ComplexTab, endpoints.ListType);
        Assert.Equal("ID", Assert.IsType<UiSectionContainerElement>(endpoints.Item).PrimaryKey);
    }

    [Fact]
    public void BothSerializerPaths_ProduceTheSameDefinition()
    {
        var newtonsoft = Serialize(BuildFor(typeof(NewtonsoftTwinConfiguration), "Twin"))
            .Replace("Newtonsoft Twin", "Twin", StringComparison.Ordinal);
        var systemTextJson = Serialize(BuildFor(typeof(SystemTextJsonTwinConfiguration), "Twin"))
            .Replace("System Text Json Twin", "Twin", StringComparison.Ordinal);

        // The only authored difference between the two twins is which
        // serializer interface they implement. `[EnumMember]` (Newtonsoft) and
        // `[JsonStringEnumMemberName]` (System.Text.Json) have to agree for
        // this to hold.
        //
        // `DeniedValues` is dropped before comparing: literal values are
        // rendered by the configuration's own serializer, and the two disagree
        // on how to write a whole-numbered double. See
        // `BothSerializerPaths_DisagreeOnWholeNumberedDoubleLiterals`.
        Assert.Equal(WithoutDeniedValues(newtonsoft), WithoutDeniedValues(systemTextJson));
    }

    [Fact]
    public void BothSerializerPaths_DisagreeOnWholeNumberedDoubleLiterals()
    {
        var newtonsoft = Find<UiFloatElement>(BuildFor(typeof(NewtonsoftTwinConfiguration), "Twin").Root, "Ratio");
        var systemTextJson = Find<UiFloatElement>(BuildFor(typeof(SystemTextJsonTwinConfiguration), "Twin").Root, "Ratio");

        // `[DeniedValues(0.0, 1.0)]` on a `double`. The values are rendered by
        // the configuration's own serializer so they line up with the values in
        // the configuration document, and Newtonsoft keeps the decimal point
        // where System.Text.Json drops it. They compare equal numerically, so
        // this only bites a client doing a textual comparison.
        Assert.Equal(["0.0", "1.0"], newtonsoft.DeniedValues!.Select(x => x!.ToString(Formatting.None)));
        Assert.Equal(["0", "1"], systemTextJson.DeniedValues!.Select(x => x!.ToString(Formatting.None)));
    }

    [Fact]
    public void ServerSettings_DumpsDefinitionAndReportsPayloadSize()
    {
        var (definition, schemaJson) = BuildForServerSettings();

        // Mirrors the MVC pipeline, `MaxDepth` included: the produced tree is
        // deeper than 10 levels, so this doubles as a check that the pipeline
        // can actually emit it.
        var mvcSettings = new JsonSerializerSettings
        {
            MaxDepth = 10,
            ContractResolver = new DefaultContractResolver { NamingStrategy = new DefaultNamingStrategy() },
            NullValueHandling = NullValueHandling.Include,
        };
        var leanSettings = new JsonSerializerSettings
        {
            ContractResolver = new DefaultContractResolver { NamingStrategy = new DefaultNamingStrategy() },
            NullValueHandling = NullValueHandling.Ignore,
        };

        var definitionJson = JsonConvert.SerializeObject(definition, Formatting.Indented, mvcSettings);
        var definitionMinified = JsonConvert.SerializeObject(definition, Formatting.None, mvcSettings);
        var definitionLean = JsonConvert.SerializeObject(definition, Formatting.None, leanSettings);
        var schemaMinified = JToken.Parse(schemaJson).ToString(Formatting.None);
        var elements = Flatten(definition.Root).ToList();

        var outputDirectory = TestPaths.OutputDirectory;
        Directory.CreateDirectory(outputDirectory);
        File.WriteAllText(Path.Combine(outputDirectory, "ServerSettings.ui-definition.json"), definitionJson);
        File.WriteAllText(Path.Combine(outputDirectory, "ServerSettings.schema.json"), schemaJson);
        File.WriteAllText(
            Path.Combine(outputDirectory, "payload-sizes.txt"),
            string.Join(
                Environment.NewLine,
                "Payload comparison for ServerSettings (bytes, UTF-8, minified unless noted)",
                $"  /Schema                            : {schemaMinified.Length,8}",
                $"  /UiDefinition (NullValueHandling.Include, as MVC would emit it): {definitionMinified.Length,8}",
                $"  /UiDefinition (NullValueHandling.Ignore)                       : {definitionLean.Length,8}",
                $"  ratio vs schema (Include)          : {(double)definitionMinified.Length / schemaMinified.Length:0.00}x",
                $"  ratio vs schema (Ignore)           : {(double)definitionLean.Length / schemaMinified.Length:0.00}x",
                string.Empty,
                "Element census",
                $"  elements                           : {elements.Count,8}",
                $"  with a default                     : {elements.Count(x => x.Default is not null),8}",
                $"  hoisted definitions                : {definition.Definitions.Count,8}",
                string.Empty
            )
        );

        Assert.True(definitionMinified.Length > 0);
        Assert.True(schemaMinified.Length > 0);
    }

    [Fact]
    public void DisplayButtonPosition_SerializesToItsAuthoredNameOnBothPaths()
    {
        foreach (var type in new[] { typeof(NewtonsoftTwinConfiguration), typeof(SystemTextJsonTwinConfiguration) })
        {
            var actions = Flatten(BuildFor(type, "Twin").Root).OfType<UiSectionContainerElement>()
                .SelectMany(x => x.Actions)
                .ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);

            // Every member of the enum now carries a distinct value, so name
            // resolution is deterministic and reaches the attribute. While the
            // aliases existed, a button authored as `Start`/`Top` went out as
            // `"Left"` and one authored as `End` went out as `"Right"`, on both
            // serializer paths.
            Assert.Equal(DisplayButtonPosition.Start, actions["DoTheThingAction"].Position);
            Assert.Equal(DisplayButtonPosition.End, actions["DoAnotherThingAction"].Position);
            Assert.Equal("\"start\"", JsonConvert.SerializeObject(DisplayButtonPosition.Start));
            Assert.Equal("\"end\"", JsonConvert.SerializeObject(DisplayButtonPosition.End));
            Assert.Equal("\"auto\"", JsonConvert.SerializeObject(DisplayButtonPosition.Auto));
            Assert.Equal("\"start\"", System.Text.Json.JsonSerializer.Serialize(DisplayButtonPosition.Start));
            Assert.Equal("\"end\"", System.Text.Json.JsonSerializer.Serialize(DisplayButtonPosition.End));
            Assert.Equal("\"auto\"", System.Text.Json.JsonSerializer.Serialize(DisplayButtonPosition.Auto));
        }
    }

    [Fact]
    public void RecursiveConfiguration_HoistsTheCycleIntoDefinitions()
    {
        var definition = BuildFor(typeof(RecursiveNode), "Recursive");

        var elements = Flatten(definition.Root).ToList();
        var reference = Assert.Single(elements.OfType<UiReferenceElement>());
        Assert.True(definition.Definitions.ContainsKey(reference.Reference));
        // The hoisted definition is a real container, not a self-reference.
        Assert.IsType<UiSectionContainerElement>(definition.Definitions[reference.Reference]);
    }

    private static string WithoutDeniedValues(string json)
    {
        var token = JToken.Parse(json);
        foreach (var denied in token.SelectTokens("$..DeniedValues").ToList())
            denied.Replace(JValue.CreateNull());
        return token.ToString(Formatting.Indented);
    }

    private static string Serialize(UiDefinition definition)
        => JsonConvert.SerializeObject(definition, Formatting.Indented, new JsonSerializerSettings
        {
            MaxDepth = 10,
            ContractResolver = new DefaultContractResolver { NamingStrategy = new DefaultNamingStrategy() },
            NullValueHandling = NullValueHandling.Include,
        });

    /// <summary>
    ///   A deliberately self-recursive shape; nothing in-tree currently
    ///   recurses, so the cycle handling would otherwise go untested.
    /// </summary>
    public class RecursiveNode
    {
        /// <summary>The node's name.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>The node's items.</summary>
        public List<RecursiveNode> Items { get; set; } = [];
    }
}
