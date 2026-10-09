using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Namotion.Reflection;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Config.Attributes;
using Shoko.Abstractions.Config.Enums;
using Shoko.Abstractions.Config.Exceptions;
using Shoko.Abstractions.Plugin;
using Shoko.Server.Services.Configuration;
using Xunit;

namespace Shoko.Tests.Services;

/// <summary>
///   Coverage for the Validate hook running before every save, so a Save hook
///   or a plain save is never handed a document the hook refuses.
/// </summary>
[Collection(ConfigurationSchemaCollection.Name)]
public class ConfigurationValidateHookTests
{
    private static (ConfigurationService Service, ConfigurationInfo Info) Create()
    {
        var applicationPaths = new Mock<IApplicationPaths>();
        applicationPaths.SetupGet(x => x.DataPath).Returns(Path.GetTempPath());
        applicationPaths.SetupGet(x => x.ConfigurationsPath).Returns(Path.GetTempPath());
        var service = new ConfigurationService(NullLoggerFactory.Instance, applicationPaths.Object, Mock.Of<IPluginManager>());
        var type = typeof(HookedConfiguration);
        var info = new ConfigurationInfo(service)
        {
            ID = Guid.NewGuid(),
            Path = null,
            Name = "Fixture",
            Description = string.Empty,
            HasCustomActions = false,
            HasCustomNewFactory = false,
            HasCustomValidation = true,
            HasCustomSave = true,
            HasCustomLoad = false,
            HasLiveEdit = false,
            Type = type,
            ContextualType = type.ToContextualType(),
            Schema = service.GenerateSchema(type),
            PluginInfo = null!,
        };
        return (service, info);
    }

    [Fact]
    public void Validate_RunsTheHook()
    {
        var (service, info) = Create();

        var (path, messages) = Assert.Single(service.Validate(info, """{"Count":13}"""));

        Assert.Equal(nameof(HookedConfiguration.Count), path);
        Assert.Equal(["13 is refused."], messages);
    }

    [Fact]
    public void Save_IsRefusedByTheHook()
    {
        var (service, info) = Create();

        var ex = Assert.Throws<ConfigurationValidationException>(() => service.Save(info, """{"Count":13}"""));

        Assert.Contains(nameof(HookedConfiguration.Count), ex.ValidationErrors.Keys);
    }

    [Fact]
    public void Validate_PassesWhatTheHookAccepts()
    {
        var (service, info) = Create();

        Assert.Empty(service.Validate(info, """{"Count":12}"""));
    }

    /// <summary>A configuration whose Validate hook refuses 13.</summary>
    public class HookedConfiguration : IConfiguration
    {
        /// <summary>Refused when it is 13.</summary>
        public int Count { get; set; }

        /// <summary>Refuses 13.</summary>
        [ConfigurationAction(ConfigurationActionType.Validate)]
        public static ConfigurationActionResult OnValidate(HookedConfiguration configuration)
            => new()
            {
                ValidationErrors = configuration.Count is 13
                    ? new Dictionary<string, IReadOnlyList<string>> { [nameof(Count)] = ["13 is refused."] }
                    : new Dictionary<string, IReadOnlyList<string>>(),
            };
    }
}
