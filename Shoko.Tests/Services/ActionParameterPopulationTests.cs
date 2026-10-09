using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.Exceptions;
using Shoko.Abstractions.Plugin;
using Shoko.Server.Services;
using Shoko.Server.Services.Configuration;
using Shoko.Tests.Services.Configuration;
using Xunit;

namespace Shoko.Tests.Services;

/// <summary>
///   Coverage for a parameter value that passes the schema but cannot be read
///   into its parameter, which is reported as a validation error.
/// </summary>
[Collection(ConfigurationSchemaCollection.Name)]
public class ActionParameterPopulationTests
{
    private static readonly Guid _pluginId = Guid.Parse("6e3f3c1a-8f1e-4b1c-9d55-3b1d2a4c5e6f");

    private static (ActionService Service, Guid ActionId) CreateService()
    {
        var services = new ServiceCollection().AddTransient<ConvertedParameterAction>().BuildServiceProvider();
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
            null!,
            null!
        );
        service.AddParts([(_pluginId, typeof(ConvertedParameterAction))]);
        return (service, service.GetActions().Single().ID);
    }

    [Theory]
    [InlineData("Version", "not-a-version", "Version")]
    [InlineData("Access", "Read, Nope", "Access")]
    public async Task AnUnreadableValue_IsAValidationError(string name, string text, string key)
    {
        var (service, actionId) = CreateService();
        var parameters = new Dictionary<string, object?> { [name] = text };

        var populated = Assert.Throws<GenericValidationException>(() => ActionService.PopulateParameters(new ConvertedParameterAction(), parameters));
        var invoked = await Assert.ThrowsAsync<GenericValidationException>(() => service.InvokeAsync(actionId, parameters, token: TestContext.Current.CancellationToken));
        var options = await Assert.ThrowsAsync<GenericValidationException>(() => service.GetParameterOptionsAsync(actionId, name, parameters, token: TestContext.Current.CancellationToken));

        Assert.All(
            new[] { populated, invoked, options },
            ex => Assert.Equal([key], ex.ValidationErrors.Keys)
        );
    }

    [Fact]
    public async Task ReadableValues_StillPopulate()
    {
        var (service, actionId) = CreateService();
        var action = new ConvertedParameterAction();
        var parameters = new Dictionary<string, object?> { ["Version"] = "3.4", ["Access"] = new[] { "Read", "run" } };

        ActionService.PopulateParameters(action, parameters);

        Assert.Equal(new ParsableTypeTests.TextVersion(3, 4), action.Version);
        Assert.Equal(FlagEnumTests.Access.Read | FlagEnumTests.Access.Execute, action.Access);
        Assert.Null(await service.ValidateAsync(actionId, parameters, token: TestContext.Current.CancellationToken));
    }

    public class ConvertedParameterAction : IExecutableAction
    {
        public string Name => "Converted Parameters";

        public ActionPermission Permission => ActionPermission.Admin;

        public ParsableTypeTests.TextVersion Version { get; set; } = new(1, 0);

        public FlagEnumTests.Access Access { get; set; }

        public Task Execute(CancellationToken token = default)
            => Task.CompletedTask;
    }
}
