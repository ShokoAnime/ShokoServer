using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Namotion.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using NJsonSchema;
using NJsonSchema.NewtonsoftJson.Generation;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Config.Services;
using Shoko.Abstractions.Exceptions;
using Shoko.Abstractions.Plugin;
using Shoko.Server.Models.Internal;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Plugin;
using Shoko.Server.Repositories.Direct;
using Shoko.Server.Services;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Services;

public class UserConfigurationServiceTests : IDisposable
{
    #region Fixtures

    private readonly string _dataPath = Path.Join(Path.GetTempPath(), "shoko-user-configuration-" + Guid.NewGuid().ToString("N"));

    private static readonly JMMUser User = new() { JMMUserID = 7, Username = "someone" };

    public UserConfigurationServiceTests()
        => Directory.CreateDirectory(_dataPath);

    public void Dispose()
    {
        if (Directory.Exists(_dataPath))
            Directory.Delete(_dataPath, true);
        GC.SuppressFinalize(this);
    }

    /// <summary>A typed user configuration, read and written through the mocked configuration system.</summary>
    public sealed class ViewerConfiguration : IUserConfiguration
    {
        public string Theme { get; set; } = "dark";

        public DayOfWeek FirstDay { get; set; } = DayOfWeek.Monday;
    }

    private static readonly JsonSerializerSettings _serializerSettings = new() { Converters = [new StringEnumConverter()] };

    private UserConfigurationService Service(params UserConfigurationRegistration[] registrations)
    {
        var nextID = 1;
        var rows = new List<UserConfiguration>();
        var repository = new Mock<UserConfigurationRepository>(new object[] { null! });
        repository.Setup(r => r.GetByUserID(It.IsAny<int>())).Returns((int userID) => [.. rows.Where(row => row.JMMUserID == userID)]);
        repository.Setup(r => r.GetByUserIDAndConfigurationID(It.IsAny<int>(), It.IsAny<string>()))
            .Returns((int userID, string configurationID) => rows.FirstOrDefault(row => row.JMMUserID == userID && row.ConfigurationID == configurationID));
        repository.Setup(r => r.Save(It.IsAny<UserConfiguration>())).Callback<UserConfiguration>(row =>
        {
            if (row.UserConfigurationID is 0)
            {
                row.UserConfigurationID = nextID++;
                rows.Add(row);
            }
        });
        repository.Setup(r => r.Delete(It.IsAny<UserConfiguration>())).Callback<UserConfiguration>(row => rows.Remove(row));

        var pluginManager = new Mock<IPluginManager>();
        var configurationService = new Mock<IConfigurationService>();
        var viewerSchema = JsonSchema.FromType<ViewerConfiguration>(new NewtonsoftJsonSchemaGeneratorSettings { SerializerSettings = _serializerSettings });
        var viewerInfo = new ConfigurationInfo(configurationService.Object)
        {
            ID = Guid.NewGuid(),
            Path = null,
            Name = "Viewer",
            Description = string.Empty,
            HasCustomActions = false,
            HasCustomNewFactory = false,
            HasCustomValidation = false,
            HasCustomSave = false,
            HasCustomLoad = false,
            HasLiveEdit = false,
            Type = typeof(ViewerConfiguration),
            ContextualType = typeof(ViewerConfiguration).ToContextualType(),
            Schema = viewerSchema,
            PluginInfo = PluginTestDoubles.InstalledPluginInfo(typeof(PluginTestDoubles.TestPlugin), Guid.NewGuid()),
        };
        configurationService.Setup(c => c.GetConfigurationInfo(typeof(ViewerConfiguration))).Returns(viewerInfo);
        configurationService.Setup(c => c.GetSchema(viewerInfo)).Returns(viewerSchema.ToJson());
        configurationService.Setup(c => c.Validate(It.IsAny<string>(), It.IsAny<JsonSchema>()))
            .Returns((string json, JsonSchema schema) => Validate(schema, json));
        configurationService.Setup(c => c.Validate(viewerInfo, It.IsAny<string>()))
            .Returns((ConfigurationInfo info, string json) => Validate(info.Schema, json));
        configurationService.Setup(c => c.New(viewerInfo)).Returns(() => new ViewerConfiguration());
        configurationService.Setup(c => c.Deserialize(viewerInfo, It.IsAny<string>()))
            .Returns((ConfigurationInfo _, string json) => JsonConvert.DeserializeObject<ViewerConfiguration>(json, _serializerSettings)!);
        configurationService.Setup(c => c.Serialize(It.IsAny<IConfiguration>()))
            .Returns((IConfiguration configuration) => JsonConvert.SerializeObject(configuration, _serializerSettings));
        configurationService.Setup(c => c.MaskSecrets(viewerInfo, It.IsAny<string>())).Returns((ConfigurationInfo _, string json) => json);
        var paths = new Mock<IApplicationPaths>();
        paths.SetupGet(p => p.DataPath).Returns(_dataPath);

        return new(
            repository.Object,
            UserConfigurationRegistry.Build(registrations),
            pluginManager.Object,
            configurationService.Object,
            paths.Object,
            NullLogger<UserConfigurationService>.Instance
        );
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> Validate(JsonSchema schema, string json)
        => schema.Validate(json)
            .GroupBy(error => error.Path ?? string.Empty)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<string>)[.. group.Select(error => error.Kind.ToString())]);

    private static UserConfigurationRegistration Registration(
        string id,
        string? schemaPath = null,
        string? schemaJson = null,
        bool strict = true,
        Type? configurationType = null,
        string? name = null
    )
        => new(typeof(CorePlugin), id, name, null, configurationType, schemaPath, schemaJson, strict);

    private static string? Theme(string? json)
        => json is null ? null : JObject.Parse(json)["theme"]?.Value<string>();

    private const string ThemeSchema = """{ "type": "object", "properties": { "theme": { "type": "string" } } }""";

    #endregion

    #region Registration

    [Theory]
    [InlineData("")]
    [InlineData("-webui")]
    [InlineData("web--ui")]
    [InlineData("WebUI")]
    [InlineData("web_ui")]
    [InlineData("web.ui")]
    [InlineData("web ui")]
    public void AConfigurationIDThatIsNotKebabCaseIsRefused(string configurationID)
        => Assert.Throws<ArgumentException>(() => new ServiceCollection().AddUserConfiguration<CorePlugin>(configurationID));

    [Fact]
    public void ABlankNameIsRefused()
        => Assert.Throws<ArgumentException>(() => new ServiceCollection().AddUserConfiguration<CorePlugin>("webui", options => options.Name = " "));

    [Fact]
    public void ATypedConfigurationTakesNoOtherSchema()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentException>(
            () => services.AddUserConfiguration<CorePlugin, ViewerConfiguration>("viewer", options => options.SchemaJson = ThemeSchema)
        );
    }

    [Theory]
    [InlineData("../schema.json")]
    [InlineData("webui/../../schema.json")]
    [InlineData("/srv/shoko/../schema.json")]
    public void ASchemaPathWithAParentSegmentIsRefused(string path)
        => Assert.Throws<ArgumentException>(() => new ServiceCollection().AddUserConfiguration<CorePlugin>("webui", options => options.SchemaPath = path));

    [Fact]
    public void ASchemaPathIsNormalized()
        => Assert.Equal(Path.Join("webui", "schema.json"), UserConfigurationExtensions.NormalizeSchemaPath(@"./webui//.\schema.json"));

    [Fact]
    public void ASchemaPathAndASchemaTogetherAreRefused()
        => Assert.Throws<ArgumentException>(() => new ServiceCollection().AddUserConfiguration<CorePlugin>("webui", options =>
        {
            options.SchemaPath = "schema.json";
            options.SchemaJson = ThemeSchema;
        }));

    [Fact]
    public void AConfigurationIDRegisteredTwiceIsRefused()
    {
        UserConfigurationRegistration[] registrations = [Registration("webui", name: "Web UI"), Registration("webui")];

        var error = Assert.Throws<InvalidOperationException>(() => UserConfigurationRegistry.Build(registrations));

        Assert.Contains("Web UI (webui)", error.Message);
    }

    #endregion

    #region Documents

    [Fact]
    public void AnUnregisteredConfigurationCanBeReadAndDeletedButNotSaved()
    {
        var service = Service(Registration("webui"));
        service.Save(User, "webui", """{ "theme": "dark" }""");

        var other = Service();
        Assert.Throws<GenericValidationException>(() => other.Save(User, "webui", "{}"));
        Assert.Equal("dark", Theme(service.Load(User, "WEBUI")));
        Assert.True(service.Delete(User, "webui"));
        Assert.Null(service.Load(User, "webui"));
    }

    [Fact]
    public void TextThatIsNotAJsonObjectIsRefused()
    {
        var service = Service(Registration("webui"));

        Assert.Throws<GenericValidationException>(() => service.Save(User, "webui", "[1, 2]"));
        Assert.Throws<GenericValidationException>(() => service.Save(User, "webui", "not json"));
    }

    [Fact]
    public void TheListingHoldsTheRegisteredAndTheStoredConfigurations()
    {
        var service = Service(Registration("webui", name: "Web UI"), Registration("viewer", configurationType: typeof(ViewerConfiguration)));
        service.Save(User, "viewer", "{}");

        var infos = service.GetInfoForUser(User);

        Assert.Equal(["viewer", "webui"], infos.Select(info => info.ID));
        Assert.Equal(["viewer", "Web UI"], infos.Select(info => info.Name));
        Assert.All(infos, info => Assert.True(info.IsRegistered));
        Assert.NotNull(infos[0].ConfigurationInfo);
        Assert.Null(infos[1].ConfigurationInfo);
        Assert.NotNull(infos[0].UpdatedAt);
        Assert.Null(infos[1].UpdatedAt);
        Assert.All(service.GetAllInfos(), info => Assert.Null(info.UpdatedAt));
    }

    [Fact]
    public void ATypedConfigurationGoesThroughTheConfigurationSystem()
    {
        var service = Service(Registration("viewer", configurationType: typeof(ViewerConfiguration)));

        Assert.Equal("dark", service.Load<ViewerConfiguration>(User, "viewer").Theme);
        service.Save(User, "viewer", new ViewerConfiguration { Theme = "light", FirstDay = DayOfWeek.Sunday });
        Assert.Throws<GenericValidationException>(() => service.Save(User, "viewer", """{ "FirstDay": "Someday" }"""));

        var configuration = service.Load<ViewerConfiguration>(User, "viewer");
        Assert.Equal("light", configuration.Theme);
        Assert.Equal(DayOfWeek.Sunday, configuration.FirstDay);
        Assert.Equal("Sunday", JObject.Parse(service.Load(User, "viewer")!)["FirstDay"]?.Value<string>());
    }

    [Fact]
    public void ATypedConfigurationIsHandedOutWithItsDefaultsWhenNoneIsStored()
    {
        var service = Service(Registration("viewer", configurationType: typeof(ViewerConfiguration)));

        Assert.Equal("dark", JObject.Parse(service.LoadMasked(User, "viewer")!)["Theme"]?.Value<string>());
        Assert.Null(service.LoadMasked(User, "other"));
    }

    [Fact]
    public void ADocumentTooLargeIsRefused()
    {
        var service = Service(Registration("webui"));
        var json = new JObject { ["blob"] = new string('x', UserConfigurationService.MaxDocumentLength) }.ToString();

        Assert.Throws<GenericValidationException>(() => service.Save(User, "webui", json));
    }

    #endregion

    #region Imports

    [Fact]
    public void OldSettingsAreCopiedForAUser_OnlyWhileNoneIsStored()
    {
        var service = Service(Registration("webui"));

        Assert.True(service.ImportIfMissing(User, "webui", """{ "theme": "dark" }"""));
        Assert.False(service.ImportIfMissing(User, "webui", """{ "theme": "light" }"""));
        Assert.True(service.ImportIfMissing(User, "other", "not json"));
        Assert.Equal("dark", Theme(service.Load(User, "webui")));
    }

    #endregion

    #region Schemas

    [Fact]
    public void ADocumentThatDoesNotFitTheSchemaIsRefused()
    {
        var service = Service(Registration("webui", schemaJson: ThemeSchema));

        Assert.Throws<GenericValidationException>(() => service.Save(User, "webui", """{ "theme": 5 }"""));
        service.Save(User, "webui", """{ "theme": "dark" }""");
    }

    [Fact]
    public void AStrictMissingSchemaFileRefusesSaves_UntilItExists()
    {
        var service = Service(Registration("webui", schemaPath: Path.Join("webui", "schema.json")));

        Assert.Throws<GenericValidationException>(() => service.Save(User, "webui", """{ "theme": "dark" }"""));

        Directory.CreateDirectory(Path.Join(_dataPath, "webui"));
        File.WriteAllText(Path.Join(_dataPath, "webui", "schema.json"), ThemeSchema);
        service.Save(User, "webui", """{ "theme": "dark" }""");
        Assert.Throws<GenericValidationException>(() => service.Save(User, "webui", """{ "theme": 5 }"""));
        Assert.Equal(ThemeSchema, service.GetSchema("webui"));
    }

    [Fact]
    public void ALenientMissingSchemaFileSavesUnvalidated()
    {
        var service = Service(Registration("webui", schemaPath: "schema.json", strict: false));

        service.Save(User, "webui", """{ "theme": 5 }""");

        Assert.Null(service.GetSchema("webui"));
    }

    [Fact]
    public void ABrokenStrictSchemaFileRefusesSaves()
    {
        File.WriteAllText(Path.Join(_dataPath, "schema.json"), "{ not json");
        var service = Service(Registration("webui", schemaPath: "schema.json"));

        Assert.Throws<GenericValidationException>(() => service.Save(User, "webui", "{}"));
    }

    #endregion
}
