using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Newtonsoft.Json.Linq;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.UI.Elements;
using Shoko.Server.Services;
using Shoko.Server.Services.Configuration;
using Xunit;

namespace Shoko.Tests.Services;

/// <summary>
///   Coverage for a parameter the action marks required, which the definition
///   marks and an invocation has to send. Every other parameter stays optional.
/// </summary>
[Collection(ConfigurationSchemaCollection.Name)]
public class ActionRequiredParameterTests
{
    private static (ActionService Service, Guid ActionId) CreateService()
    {
        var services = new ServiceCollection().AddTransient<RequiringAction>().BuildServiceProvider();
        var service = new ActionService(
            NullLogger<ActionService>.Instance,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            Mock.Of<IPluginManager>(),
            services,
            new ActionUiDefinitionBuilder(NullLoggerFactory.Instance),
            new ConfigurationService(NullLoggerFactory.Instance, Mock.Of<IApplicationPaths>(), Mock.Of<IPluginManager>()),
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!
        );
        service.AddParts([(Guid.NewGuid(), typeof(RequiringAction))]);
        return (service, service.GetActions().Single().ID);
    }

    [Fact]
    public void TheDefinition_MarksOnlyTheExplicitlyRequired()
    {
        var (service, actionId) = CreateService();
        var root = Assert.IsType<UiSectionContainerElement>(service.GetActionInfo(actionId)!.ParameterDefinition!.Root);

        Assert.True(root.Items[nameof(RequiringAction.Annotated)].IsRequired);
        Assert.True(root.Items[nameof(RequiringAction.Modifier)].IsRequired);
        Assert.False(root.Items[nameof(RequiringAction.Optional)].IsRequired);
    }

    [Fact]
    public void AnInvocation_HasToSendTheRequired()
    {
        var (service, actionId) = CreateService();

        Assert.Equal(
            [nameof(RequiringAction.Annotated), nameof(RequiringAction.Modifier)],
            service.ValidateParameters(actionId, null).Keys.Order()
        );
        Assert.Empty(service.ValidateParameters(actionId, JObject.Parse("""{"Annotated":"a","Modifier":1}""")));
    }

    public class RequiringAction : IExecutableAction
    {
        public string Name => "Requiring";

        public ActionPermission Permission => ActionPermission.Admin;

        [Required]
        public string Annotated { get; set; } = string.Empty;

        public required int Modifier { get; set; }

        public string Optional { get; set; } = string.Empty;

        public Task Execute(CancellationToken token = default)
            => Task.CompletedTask;
    }
}
