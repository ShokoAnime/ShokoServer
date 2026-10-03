using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Namotion.Reflection;
using NJsonSchema;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Config.Services;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.UI;
using Shoko.Abstractions.UI.Elements;
using Shoko.Server.Services.Configuration;
using Shoko.Server.Settings;
using Xunit;

namespace Shoko.Tests.Services;

/// <summary>
///   Coverage for <see cref="IConfigurationService.GenerateUiDefinition"/> and
///   for <see cref="ConfigurationInfo.UiDefinition"/>, the property that
///   replaced the old <c>GetUiDefinition(ConfigurationInfo)</c> accessor so a
///   configuration and an executable action are both described by a property on
///   their info object.
/// </summary>
[Collection(ConfigurationSchemaCollection.Name)]
public class ConfigurationInfoUiDefinitionTests
{
    private static ConfigurationService CreateService()
    {
        var applicationPaths = new Mock<IApplicationPaths>();
        applicationPaths.SetupGet(x => x.DataPath).Returns(Path.GetTempPath());
        applicationPaths.SetupGet(x => x.ConfigurationsPath).Returns(Path.GetTempPath());
        return new ConfigurationService(NullLoggerFactory.Instance, applicationPaths.Object, Mock.Of<IPluginManager>());
    }

    private static ConfigurationInfo CreateInfo(IConfigurationService service, Type type)
        => new(service)
        {
            ID = Guid.NewGuid(),
            Path = null,
            Name = "Fixture",
            Description = string.Empty,
            HasCustomActions = false,
            HasCustomNewFactory = false,
            HasCustomValidation = false,
            HasCustomSave = false,
            HasCustomLoad = false,
            HasLiveEdit = false,
            Type = type,
            ContextualType = type.ToContextualType(),
            Schema = new JsonSchema(),
            PluginInfo = null!,
        };

    [Fact]
    public void TheDefinition_IsNotBuiltUntilItIsRead()
    {
        var service = new Mock<IConfigurationService>();
        service.Setup(x => x.GenerateUiDefinition(It.IsAny<Type>())).Returns(new UiDefinition());
        var info = CreateInfo(service.Object, typeof(ServerSettings));

        // Constructing the info must not materialise the tree; `ServerSettings`
        // alone runs to ~120 KB and every `GetConfigurationInfo` call would pay
        // for it.
        service.Verify(x => x.GenerateUiDefinition(It.IsAny<Type>()), Times.Never);

        _ = info.UiDefinition;

        service.Verify(x => x.GenerateUiDefinition(typeof(ServerSettings)), Times.Once);
    }

    [Fact]
    public void TheDefinition_IsBuiltOnceAndHeld()
    {
        var service = new Mock<IConfigurationService>();
        service.Setup(x => x.GenerateUiDefinition(It.IsAny<Type>())).Returns(() => new UiDefinition());
        var info = CreateInfo(service.Object, typeof(ServerSettings));

        var first = info.UiDefinition;
        var second = info.UiDefinition;

        // The cache lives on the info, not in the service, because the
        // generator is type-general and has no configuration identity to key on.
        Assert.Same(first, second);
        service.Verify(x => x.GenerateUiDefinition(It.IsAny<Type>()), Times.Once);
    }

    [Fact]
    public void TheGenerator_AcceptsATypeThatIsNotAConfiguration()
    {
        // The whole point of mirroring `GenerateSchema(Type)`: a plugin can
        // describe a parameter POCO or a form model of its own, not only a
        // registered configuration.
        Assert.False(typeof(PlainFormModel).IsAssignableTo(typeof(IConfiguration)));

        var definition = CreateService().GenerateUiDefinition(typeof(PlainFormModel));

        var root = Assert.IsType<UiSectionContainerElement>(definition.Root);
        Assert.Equal(["Query", "Limit"], root.Items.Keys);
        Assert.IsType<UiStringElement>(root.Items["Query"]);
        Assert.IsType<UiIntegerElement>(root.Items["Limit"]);
        Assert.Equal("Plain Form Model", definition.Name);
        Assert.Equal("A shape a plugin might want a form for.", definition.Description);
    }

    [Fact]
    public void TheGenerator_FallsBackToAnEmptyIdForATypeOutsideAnyPlugin()
    {
        // Same behaviour `GenerateSchema` already has for `Schema.Id`: an id is
        // derived from the owning plugin, and a type with no owning plugin has
        // none to derive from. Reported rather than thrown, so the plugin case
        // this method exists for still works.
        Assert.Equal(Guid.Empty, CreateService().GenerateUiDefinition(typeof(PlainFormModel)).ID);
    }

    [Fact]
    public void ARegisteredConfiguration_IsNamedTheWayItsInfoIs()
    {
        var service = CreateService();
        var definition = service.GenerateUiDefinition(typeof(ServerSettings));

        // `AddParts` sets `ConfigurationInfo.Name` from the schema title, so the
        // type-general generator has to land on the same string or a
        // configuration would be labelled differently depending on how it was
        // reached.
        Assert.Equal(service.GenerateSchema(typeof(ServerSettings)).Title, definition.Name);
    }

    [Fact]
    public void TheDefinitionReachedThroughTheInfo_MatchesTheGeneratorsOutput()
    {
        var service = CreateService();
        var info = CreateInfo(service, typeof(ServerSettings));

        var throughInfo = info.UiDefinition;
        var direct = service.GenerateUiDefinition(typeof(ServerSettings));

        Assert.Equal(direct.ID, throughInfo.ID);
        Assert.Equal(direct.Name, throughInfo.Name);
        Assert.Equal(Flatten(direct.Root).Count(), Flatten(throughInfo.Root).Count());
    }

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

    /// <summary>
    ///   A shape a plugin might want a form for. The description is on the
    ///   attribute rather than the doc comment because the test assembly emits
    ///   no XML documentation file for the reflection reader to find.
    /// </summary>
    [Display(Description = "A shape a plugin might want a form for.")]
    public class PlainFormModel
    {
        /// <summary>The text to match against.</summary>
        public string Query { get; set; } = string.Empty;

        /// <summary>How many entries to touch at most.</summary>
        public int Limit { get; set; } = 25;
    }
}
