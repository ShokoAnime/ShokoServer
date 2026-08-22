using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Shoko.Abstractions.UI;
using Shoko.Abstractions.UI.Elements;
using Shoko.Abstractions.UI.Enums;
using Shoko.Server.Actions;
using Shoko.Server.Services.Configuration;
using Xunit;

namespace Shoko.Tests.Services;

/// <summary>
///   Coverage for the action-parameter entrypoint: an executable action's
///   settable, serialized properties are described by the very same
///   <see cref="UiDefinition"/> a configuration is, and the action's own
///   metadata surface is not part of it.
/// </summary>
[Collection(ConfigurationSchemaCollection.Name)]
public class ActionUiDefinitionBuilderTests
{
    private static readonly ActionUiDefinitionBuilder _builder = new(NullLoggerFactory.Instance);

    private static UiDefinition BuildFor(Type actionType)
    {
        var described = _builder.Build(Guid.Empty, "Action", null, actionType);
        Assert.NotNull(described);
        return described.Definition;
    }

    private static UiSectionContainerElement RootOf(Type actionType)
        => Assert.IsType<UiSectionContainerElement>(BuildFor(actionType).Root);

    /// <summary>
    ///   Every member of the action's metadata surface, derived the same way
    ///   the exclusion rule derives it rather than written out here.
    /// </summary>
    public static TheoryData<string> MetadataMemberNames()
    {
        var data = new TheoryData<string>();
        foreach (var name in ActionMetadataContractResolver.MetadataMembers.Keys)
            data.Add(name);
        return data;
    }

    [Fact]
    public void MetadataSurface_IsExactlyTheKnownSeven()
    {
        // Pins what the mechanical rule actually resolves to, so a member added
        // to `IExecutableAction` or to a scoped base shows up here rather than
        // silently becoming a parameter.
        Assert.Equal(
            ["Category", "ConfirmationMessage", "Description", "Name", "Permission", "RequiresConfirmation", "Scope"],
            ActionMetadataContractResolver.MetadataMembers.Keys.Order(StringComparer.Ordinal)
        );
    }

    [Theory]
    [MemberData(nameof(MetadataMemberNames))]
    public void MetadataSurface_IsNeverAParameter(string memberName)
    {
        foreach (var actionType in new[] { typeof(ParameterisedGlobalAction), typeof(ParameterisedSeriesAction) })
        {
            var root = RootOf(actionType);
            Assert.DoesNotContain(root.Items, x => string.Equals(x.Key, memberName, StringComparison.Ordinal));
            Assert.DoesNotContain(root.Structure, x => string.Equals(x.Name, memberName, StringComparison.Ordinal));
        }
    }

    [Fact]
    public void Parameters_AreDescribedInTheAuthoredOrder()
    {
        var root = RootOf(typeof(ParameterisedGlobalAction));

        Assert.Equal(["Query", "Mode", "MaxResults", "Tags", "DryRun"], root.Items.Keys);
        Assert.Equal(root.Items.Keys, root.Structure.Select(x => x.Name));
        Assert.All(root.Structure, x => Assert.Equal(UiStructureMemberKind.Item, x.Kind));
    }

    [Fact]
    public void Parameters_CarryTheSameElementKindsAConfigurationWould()
    {
        var root = RootOf(typeof(ParameterisedGlobalAction));

        var query = Assert.IsType<UiStringElement>(root.Items["Query"]);
        Assert.Equal("Search Query", query.Label);
        Assert.Equal("New", query.Badge?.Name);
        Assert.Equal(DisplayColorTheme.Primary, query.Badge?.Theme);

        var mode = Assert.IsType<UiEnumElement>(root.Items["Mode"]);
        Assert.Equal(["slow-and-steady", "balanced", "fast"], mode.Values.Select(x => x.Value));
        Assert.Equal("Behaviour", mode.SectionName);

        var maxResults = Assert.IsType<UiIntegerElement>(root.Items["MaxResults"]);
        Assert.Equal(1L, maxResults.Minimum);
        Assert.Equal(100L, maxResults.Maximum);
        Assert.Equal(DisplayElementSize.Small, maxResults.Size);
        Assert.True(maxResults.Visibility.Advanced);
        Assert.Equal("DryRun", maxResults.Visibility.Toggle?.Path);
        Assert.Equal(DisplayVisibility.ReadOnly, maxResults.Visibility.Toggle?.Visibility);

        var tags = Assert.IsType<UiListElement>(root.Items["Tags"]);
        Assert.True(tags.UniqueItems);
        Assert.IsType<UiStringElement>(tags.Item);

        Assert.IsType<UiBooleanElement>(root.Items["DryRun"]);
    }

    [Fact]
    public void Parameters_AreDescribedByTheNewtonsoftPath()
    {
        // An action never implements `INewtonsoftJsonConfiguration`, yet
        // `JsonConvert.PopulateObject` is what fills it in, so the entrypoint
        // has to take the Newtonsoft path unconditionally. `TwinMode` carries
        // both serialisers' naming attributes in agreement, so the tell is the
        // aliases: only the Newtonsoft path resolves `[EnumMember]`.
        var mode = Assert.IsType<UiEnumElement>(RootOf(typeof(ParameterisedGlobalAction)).Items["Mode"]);

        Assert.Equal(["Slow", "Balanced", "Very Fast"], mode.Values.Select(x => x.Title));
        Assert.Equal("\"balanced\"", JsonConvert.SerializeObject(TwinMode.Balanced));
    }

    [Fact]
    public void ScopedAction_DropsScopeButKeepsItsOwnParameters()
    {
        var root = RootOf(typeof(ParameterisedSeriesAction));

        // `Scope` is declared on the base class and no interface names it, and
        // the entity context is a protected property Newtonsoft never sees.
        Assert.Equal(["Force"], root.Items.Keys);
        Assert.DoesNotContain(root.Items, x => x.Key is "Series");
    }

    [Fact]
    public void ScopedContext_IsNotAPublicPropertyToBeginWith()
    {
        // The exclusion rule leans on this: if the context were public, it
        // would need naming, and naming it would clobber a legitimate `Series`
        // parameter.
        Assert.DoesNotContain(
            typeof(ParameterisedSeriesAction).GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance),
            x => x.Name is "Series"
        );
        Assert.DoesNotContain(ActionMetadataContractResolver.MetadataMembers.Keys, x => x is "Series" or "Group" or "Episode" or "Video");
    }

    [Fact]
    public void AParameterThatMerelySharesAMetadataName_IsKept()
    {
        var root = RootOf(typeof(ShadowedNameAction));

        // `Name` here is an `int` parameter; `IExecutableAction.Name` is
        // implemented explicitly and is a `string`. The rule matches on name
        // and type, so only the latter is dropped.
        var (key, element) = Assert.Single(root.Items);
        Assert.Equal("Name", key);
        Assert.IsType<UiIntegerElement>(element);
    }

    [Fact]
    public void AnActionWhoseParametersCannotBeDescribed_FailsRatherThanDegrading()
    {
        // An action that declares parameters has to be describable. Swallowing
        // this would leave it listed but uninvokable from a UI, which is worse
        // than the startup failure the equivalent configuration would cause.
        Assert.ThrowsAny<Exception>(() => _builder.Build(Guid.Empty, "Undescribable", null, typeof(UndescribableGlobalAction)));
    }

    [Fact]
    public void AnActionWithoutParameters_HasNoDefinitionAtAll()
    {
        Assert.Null(_builder.Build(Guid.Empty, "Run Import", null, typeof(ParameterlessGlobalAction)));
    }

    [Fact]
    public void TheRoot_CarriesTheActionsIdentityAndNoSaveAction()
    {
        var id = Guid.NewGuid();
        var described = _builder.Build(id, "Reindex Library", "Rebuilds the search index.", typeof(ParameterisedGlobalAction));

        Assert.NotNull(described);
        var definition = described.Definition;
        Assert.Equal(id, definition.ID);
        Assert.Equal("Reindex Library", definition.Name);
        Assert.Equal("Rebuilds the search index.", definition.Description);
        // There is nothing to save on an invocation form.
        Assert.False(Assert.IsType<UiSectionContainerElement>(definition.Root).ShowSaveAction);
    }

    [Fact]
    public void TheDefinition_IsShapedExactlyLikeAConfigurations()
    {
        // The binding requirement: a client cannot tell which entrypoint
        // produced the document. Compare the serialised key sets rather than
        // the values.
        var action = Serialize(BuildFor(typeof(ParameterisedGlobalAction)));
        var configuration = Serialize(
            new UiDefinitionBuilder(NullLogger<UiDefinitionBuilder>.Instance)
                .Build(Guid.Empty, "Twin", null, ShokoJsonSchemaGeneratorGoldenTests.CreateGenerator().GetSchemaForType(typeof(NewtonsoftTwinConfiguration)))
        );

        Assert.Equal(TopLevelKeys(configuration), TopLevelKeys(action));
        Assert.DoesNotContain("ConfigurationID", action, StringComparison.Ordinal);
    }

    [Fact]
    public void NoElement_ComesOutUnknownOrPathless()
    {
        var definition = BuildFor(typeof(ParameterisedGlobalAction));
        var elements = Flatten(definition.Root).ToList();

        Assert.DoesNotContain(elements, x => x is UiUnknownElement);
        Assert.All(elements, x => Assert.NotEqual(UiElementKind.Unknown, x.Kind));
        // A client indexes into `Items` by the key it sees in `Structure`, so
        // the map's key and the element's own key have to agree.
        Assert.NotEmpty(elements.OfType<UiSectionContainerElement>().SelectMany(x => x.Items));
        Assert.All(
            elements.OfType<UiSectionContainerElement>(),
            container => Assert.All(container.Items, entry => Assert.Equal(entry.Key, entry.Value.Key))
        );
    }

    [Fact]
    public void RealInTreeActions_AreDescribedWithoutTheirMetadata()
    {
        // `DownloadAllImagesAction` and `PurgeAllTmdbLinksAction` are the only
        // two in-tree actions that declare parameters, and between them they
        // cover nullable enums, plain bools and a nullable bool.
        var images = RootOf(typeof(DownloadAllImagesAction));
        Assert.Equal(["ImageSource", "ImageType", "XrefSource", "Force"], images.Items.Keys);
        Assert.All(images.Items.Values.Take(3), x => Assert.True(Assert.IsType<UiEnumElement>(x).IsNullable));
        Assert.IsType<UiBooleanElement>(images.Items["Force"]);

        var links = RootOf(typeof(PurgeAllTmdbLinksAction));
        Assert.Equal(["RemoveShowLinks", "RemoveMovieLinks", "ResetAutoLinkingState"], links.Items.Keys);
        Assert.All(links.Items.Values, x => Assert.IsType<UiBooleanElement>(x));
        // Exactly as for a configuration, a default comes off `[DefaultValue]`
        // and not off a property initialiser, so these two carry none even
        // though both initialise to `true`.
        Assert.All(links.Items.Values, x => Assert.Null(x.Default));
        Assert.True(links.Items["ResetAutoLinkingState"].IsNullable);
    }

    [Fact]
    public void ActionDefinitions_AreDumpedNextToTheConfigurationOnes()
    {
        var outputDirectory = TestPaths.OutputDirectory;
        Directory.CreateDirectory(outputDirectory);

        // The fixture covers one of every decorated element; the in-tree action
        // shows what an undecorated, real one comes out as.
        var fixture = _builder.Build(Guid.Empty, "Reindex Library", "Rebuilds the search index.", typeof(ParameterisedGlobalAction));
        Assert.NotNull(fixture);
        File.WriteAllText(Path.Combine(outputDirectory, "ExampleAction.ui-definition.json"), Serialize(fixture.Definition));

        var inTree = _builder.Build(Guid.Empty, "Download All Images", null, typeof(DownloadAllImagesAction));
        Assert.NotNull(inTree);
        File.WriteAllText(Path.Combine(outputDirectory, "DownloadAllImagesAction.ui-definition.json"), Serialize(inTree.Definition));
    }

    private static IEnumerable<string> TopLevelKeys(string json)
        => Newtonsoft.Json.Linq.JObject.Parse(json).Properties().Select(x => x.Name).Order(StringComparer.Ordinal);

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

    private static string Serialize(UiDefinition definition)
        => JsonConvert.SerializeObject(definition, Formatting.Indented, new JsonSerializerSettings
        {
            MaxDepth = 10,
            ContractResolver = new DefaultContractResolver { NamingStrategy = new DefaultNamingStrategy() },
            NullValueHandling = NullValueHandling.Include,
        });
}
