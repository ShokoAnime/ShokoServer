using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using Shoko.Abstractions.Config.Attributes;
using Shoko.Abstractions.Config.Enums;
using Shoko.Abstractions.UI.Attributes;
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

    /// <summary>
    ///   Every member a client reaches by walking a container's structure,
    ///   descending into the sections it names.
    /// </summary>
    private static IEnumerable<UiStructureEntry> Reach(UiSectionContainerElement container)
        => container.Structure
            .SelectMany(entry => entry.Kind is UiStructureMemberKind.FloatingSection
                ? container.FloatingSections[entry.Name] is var section
                    ? section.Structure.Concat(section.StartActions.Concat(section.EndActions).Select(x => new UiStructureEntry { Name = x, Kind = UiStructureMemberKind.Action }))
                    : []
                : [entry])
            .Concat(container.StartActions.Concat(container.EndActions).Select(x => new UiStructureEntry { Name = x, Kind = UiStructureMemberKind.Action }));

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
        // A client indexes into `Items` by the key a section member carries, so
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
    public void SectionContainer_GathersNamedMembersAndKeepsTheRestInPlace()
    {
        var definition = BuildFor(typeof(NewtonsoftTwinConfiguration), "Twin");
        var body = Find<UiSectionContainerElement>(definition.Root, "Body");

        // `TwinBody` named its default section, so its loose members gather into
        // `General` while `Endpoints` — a list of containers, which labels
        // itself — stays an item. `AppendFloatingSectionsAtEnd` puts both
        // gathered sections after it.
        Assert.Equal(
            [("Endpoints", UiStructureMemberKind.Item), ("General", UiStructureMemberKind.FloatingSection), ("Behaviour", UiStructureMemberKind.FloatingSection)],
            body.Structure.Select(x => (x.Name, x.Kind))
        );
        Assert.Equal(["General", "Behaviour"], body.FloatingSections.Keys);
        Assert.Equal(
            ["Name", "Enabled", "Count", "Ratio", "Secret", "Note", "Script", "Weights", "Toggles", "Picked"],
            body.FloatingSections["General"].Structure.Select(x => x.Name)
        );
        Assert.Equal(["Mode", "Modes"], body.FloatingSections["Behaviour"].Structure.Select(x => x.Name));

        // Both actions are pinned rather than inline, each to the section that
        // holds it, and neither to the container itself.
        Assert.Equal(["DoTheThingAction"], body.FloatingSections["General"].StartActions);
        Assert.Equal(["DoAnotherThingAction"], body.FloatingSections["Behaviour"].EndActions);
        Assert.Empty(body.StartActions);
        Assert.Empty(body.EndActions);

        // Every item and every action is reachable from the structure exactly
        // once, whether directly or through a section.
        var reached = Reach(body).ToList();
        Assert.Equal(body.Items.Keys.Order(StringComparer.Ordinal), reached.Where(x => x.Kind is UiStructureMemberKind.Item).Select(x => x.Name).Order(StringComparer.Ordinal));
        Assert.Equal(body.Actions.Keys.Order(StringComparer.Ordinal), reached.Where(x => x.Kind is UiStructureMemberKind.Action).Select(x => x.Name).Order(StringComparer.Ordinal));

        // The twin's root is laid out as tabs and holds nothing but the body,
        // which labels its own tab.
        var root = Assert.IsType<UiSectionContainerElement>(definition.Root);
        var entry = Assert.Single(root.Structure);
        Assert.Equal(("Body", UiStructureMemberKind.Item), (entry.Name, entry.Kind));
        Assert.Empty(root.FloatingSections);
    }

    [Fact]
    public void SectionContainer_WithoutADefaultSectionName_KeepsLooseMembersInPlace()
    {
        var (definition, _) = BuildForServerSettings();
        var plugins = Find<UiSectionContainerElement>(definition.Root, "Plugins");
        var web = Find<UiSectionContainerElement>(definition.Root, "Web");

        // `PluginSettings` names no default section and is not laid out as tabs,
        // so nothing is gathered: its loose members and its two nested
        // containers all render where they stand.
        Assert.Empty(plugins.FloatingSections);
        Assert.Equal(["EnabledPlugins", "Priority", "Renamer", "Updates"], plugins.Structure.Select(x => x.Name));
        Assert.All(plugins.Structure, x => Assert.Equal(UiStructureMemberKind.Item, x.Kind));

        // `WebSettings` is nothing but loose members, and gets no section either.
        Assert.Empty(web.FloatingSections);
        Assert.Equal(web.Items.Keys, web.Structure.Select(x => x.Name));
    }

    [Fact]
    public void ServerSettings_StructureFollowsTheAuthoredTabs()
    {
        var (definition, _) = BuildForServerSettings();
        var root = Assert.IsType<UiSectionContainerElement>(definition.Root);

        // Every nested settings object stays an item and labels its own tab,
        // with the gathered sections appended after them.
        Assert.Equal(
            ["Image", "Import", "AniDb", "TMDB", "Database", "Queue", "Connectivity", "Language", "Plex", "Plugins", "ReleaseComparisonPreferences", "Logging", "Linux", "Web", "Misc.", "Web UI"],
            root.Structure.Select(x => x.Name)
        );
        Assert.Equal(["Misc.", "Web UI"], root.Structure.TakeLast(2).Select(x => x.Name));
        Assert.All(root.Structure.TakeLast(2), x => Assert.Equal(UiStructureMemberKind.FloatingSection, x.Kind));
        Assert.All(root.Structure.SkipLast(2), x => Assert.IsType<UiSectionContainerElement>(root.Items[x.Name]));
        Assert.Equal(["WebUI_Settings"], root.FloatingSections["Web UI"].Structure.Select(x => x.Name));

        // `AniDbSettings` gives every member a section name, so it is nothing
        // but gathered sections, and `Test` pins to the top of `Login`.
        var anidb = Find<UiSectionContainerElement>(definition.Root, "AniDb");
        Assert.Equal(["Login", "Download", "MyList", "Update", "URLs", "HTTP", "UDP", "AVDump"], anidb.Structure.Select(x => x.Name));
        Assert.All(anidb.Structure, x => Assert.Equal(UiStructureMemberKind.FloatingSection, x.Kind));
        Assert.Equal(["Username", "Password"], anidb.FloatingSections["Login"].Structure.Select(x => x.Name));
        Assert.Equal(["Test"], anidb.FloatingSections["Login"].StartActions);
        // A nested container filed under a section name is an item in it.
        Assert.Contains(anidb.FloatingSections["AVDump"].Structure, x => x.Name is "AVDump" && x.Kind is UiStructureMemberKind.Item);
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
        // The derived class's own section attribute wins over the base's, while
        // the inherited section name survives.
        Assert.Equal(DisplaySectionType.Tab, root.SectionType);
        Assert.Equal(["Derived", "Inherited", "Endpoints"], root.Structure.Select(x => x.Name));
        Assert.Equal(["Name", "Count", "DoTheInheritedThingAction"], root.FloatingSections["Derived"].Structure.Select(x => x.Name));
        Assert.Equal(["Mode"], root.FloatingSections["Inherited"].Structure.Select(x => x.Name));
        Assert.True(root.ShowSaveAction);
        // An inherited list still resolves its item class and primary key.
        var endpoints = Assert.IsType<UiListElement>(root.Items["Endpoints"]);
        Assert.Equal(DisplayListType.ComplexTab, endpoints.ListType);
        Assert.Equal("ID", Assert.IsType<UiSectionContainerElement>(endpoints.Item).PrimaryKey);
    }

    [Fact]
    public void Descriptions_CarryNoXmlDocWhitespace()
    {
        var (definition, _) = BuildForServerSettings();

        // An XML doc summary is indented and wrapped for the reader of the
        // source, and both used to reach the client verbatim: a trailing newline
        // and indent on every summary, and a hard break at whatever column the
        // author happened to wrap at. A blank line between paragraphs survives
        // as a single newline; nothing else does.
        var descriptions = Flatten(definition.Root)
            .Select(x => x.Description)
            .Concat(Flatten(definition.Root).OfType<UiSectionContainerElement>().SelectMany(x => x.Actions.Values.Select(y => y.Description)))
            .OfType<string>()
            .ToList();

        Assert.NotEmpty(descriptions);
        Assert.All(descriptions, x => Assert.Equal(x.Trim(), x));
        Assert.DoesNotContain(descriptions, x => x.Contains("\n ", StringComparison.Ordinal));
        Assert.DoesNotContain(descriptions, x => x.Contains("  ", StringComparison.Ordinal));
    }

    [Fact]
    public void LiveEditFlags_MarkOnlyTheBranchesThatReact()
    {
        var definition = BuildFor(typeof(ReactiveRoot), "Reactive");
        var root = Assert.IsType<UiSectionContainerElement>(definition.Root);

        // The root declares no handler of its own, but two of its branches do,
        // so a client knows to post an edit made below them and to leave the
        // quiet branch alone.
        Assert.False(root.HasLiveEdit);
        Assert.True(root.HasNestedLiveEdit);
        Assert.False(root.FloatingSections["Left"].HasNestedLiveEdit);
        Assert.True(root.FloatingSections["Right"].HasNestedLiveEdit);
        // A list of reactive containers counts the same as one of them.
        Assert.True(root.FloatingSections["Many"].HasNestedLiveEdit);

        var quiet = Assert.IsType<UiSectionContainerElement>(root.Items["Quiet"]);
        Assert.False(quiet.HasLiveEdit);
        Assert.False(quiet.HasNestedLiveEdit);

        var live = Assert.IsType<UiSectionContainerElement>(root.Items["Live"]);
        Assert.True(live.HasLiveEdit);
        Assert.False(live.HasNestedLiveEdit);
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
            var sections = Find<UiSectionContainerElement>(BuildFor(type, "Twin").Root, "Body").FloatingSections;

            // The authored position decides which of a section's three lists an
            // action lands in, so it is read at build time rather than sent on.
            // While the aliases existed, a button authored as `Start`/`Top` went
            // out as `"Left"` and one authored as `End` went out as `"Right"`,
            // on both serializer paths, and neither reached the right list.
            Assert.Equal(["DoTheThingAction"], sections.Values.SelectMany(x => x.StartActions));
            Assert.Equal(["DoAnotherThingAction"], sections.Values.SelectMany(x => x.EndActions));
            Assert.DoesNotContain(
                sections.Values.SelectMany(x => x.Structure),
                x => x.Kind is UiStructureMemberKind.Action
            );
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
    ///   A shape with one reactive branch and one quiet one.
    /// </summary>
    public class ReactiveRoot
    {
        /// <summary>A loose member.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>A branch that handles nothing.</summary>
        [SectionName("Left")]
        public QuietBranch Quiet { get; set; } = new();

        /// <summary>A branch that handles live edits.</summary>
        [SectionName("Right")]
        public LiveBranch Live { get; set; } = new();

        /// <summary>A list of branches that handle live edits.</summary>
        [SectionName("Many")]
        public List<LiveBranch> Branches { get; set; } = [];
    }

    /// <summary>A branch that handles nothing.</summary>
    public class QuietBranch
    {
        /// <summary>A member.</summary>
        public string Name { get; set; } = string.Empty;
    }

    /// <summary>A branch that handles live edits.</summary>
    public class LiveBranch
    {
        /// <summary>A member.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Reacts to an edit.</summary>
        [ConfigurationAction(ConfigurationActionType.LiveEdit)]
        public void OnEdit() { }
    }

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
