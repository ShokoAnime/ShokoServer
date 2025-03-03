using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Force.DeepCloner;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using NJsonSchema;
using NJsonSchema.Generation;
using NJsonSchema.NewtonsoftJson.Generation;
using NJsonSchema.Validation;
using Shoko.Plugin.Abstractions;
using Shoko.Plugin.Abstractions.Config;
using Shoko.Plugin.Abstractions.Config.Exceptions;
using Shoko.Plugin.Abstractions.Events;
using Shoko.Plugin.Abstractions.Services;
using Shoko.Server.Extensions;
using Shoko.Server.Plugin;
using Shoko.Server.Settings;

#nullable enable
namespace Shoko.Server.Services;

public partial class ConfigurationService : IConfigurationService
{
    private readonly ILogger<ConfigurationService> _logger;

    private readonly IApplicationPaths _applicationPaths;

    private readonly JsonSerializerSettings _newtonsoftJsonSerializerSettings;

    private readonly System.Text.Json.JsonSerializerOptions _systemTextJsonSerializerOptions;

    private Dictionary<Guid, ConfigurationInfo> _configurationTypes;

    private Dictionary<Guid, IConfigurationDefinition> _configurationDefinitions;

    private Dictionary<ConfigurationInfo, string> _serializedSchemas;

    private readonly ConcurrentDictionary<Guid, IConfiguration> _loadedConfigurations = [];

    private bool _loaded = false;

    public event EventHandler<ConfigurationSavedEventArgs>? Saved;

    public ConfigurationService(ILogger<ConfigurationService> logger, IApplicationPaths applicationPaths)
    {
        var serverSettingsDefinition = new ServerSettingsDefinition(new(this));
        _logger = logger;
        _applicationPaths = applicationPaths;

        _newtonsoftJsonSerializerSettings = new()
        {
            Formatting = Formatting.Indented,
            DefaultValueHandling = DefaultValueHandling.Include,
            ObjectCreationHandling = ObjectCreationHandling.Replace,
            MissingMemberHandling = MissingMemberHandling.Ignore,
            Converters = [new StringEnumConverter()]
        };

        _systemTextJsonSerializerOptions = new()
        {
            AllowTrailingCommas = true,
            WriteIndented = true,
            PreferredObjectCreationHandling = System.Text.Json.Serialization.JsonObjectCreationHandling.Replace,
            ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles,
        };
        _systemTextJsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());

        _configurationTypes = new()
        {
            {
                GetID(typeof(ServerSettings)),
                new ConfigurationInfo()
                {
                    ID = GetID(typeof(ServerSettings)),
                    Name = "Shoko Core",
                    Path = Path.Join(_applicationPaths.ProgramDataPath, serverSettingsDefinition.RelativePath),
                    Type = typeof(ServerSettings),
                    // We're not going to be using this before .AddParts is called.
                    Plugin = null!,
                    Schema = GetSchemaForType(typeof(ServerSettings)),
                }
            },
        };

        _configurationDefinitions = new()
        {
            { GetID(typeof(ServerSettings)),  serverSettingsDefinition },
        };

        _serializedSchemas = _configurationTypes.Values
            .ToDictionary(info => info, info => AddSchemaPropertyToSchema(info.Schema.ToJson()));
    }

    #region Configuration Info

    public void AddParts(IEnumerable<Type> configurationTypes, IEnumerable<IConfigurationDefinition> configurationDefinitions)
    {
        if (_loaded) return;
        _loaded = true;

        ArgumentNullException.ThrowIfNull(configurationTypes);
        ArgumentNullException.ThrowIfNull(configurationDefinitions);

        var configurationDefinitionDict = configurationDefinitions.ToDictionary(GetID);
        _configurationTypes = configurationTypes
            .Select(configurationType =>
            {
                var configName = GetDisplayName(configurationType);
                if (configName.EndsWith(" Settings"))
                    configName = configName[..^9];
                if (configName.EndsWith(" Config"))
                    configName = configName[..^7];
                if (configName.EndsWith(" Configuration"))
                    configName = configName[..^14];

                var pluginTypes = Loader.GetTypes<IPlugin>(configurationType.Assembly);
                foreach (var pluginType in pluginTypes)
                {
                    var plugin = Loader.GetFromType(pluginType);
                    if (plugin is null)
                        continue;

                    var configurationId = GetID(configurationType);
                    var provider = configurationDefinitionDict.GetValueOrDefault(configurationId);
                    var path = provider is IConfigurationDefinitionWithCustomSaveLocation { } p
                        ? Path.Join(_applicationPaths.ProgramDataPath, p.RelativePath)
                        : Path.Join(_applicationPaths.PluginConfigurationsPath, plugin.ID.ToString(), configurationType.FullName! + ".json");
                    var schema = GetSchemaForType(configurationType);
                    return new ConfigurationInfo()
                    {
                        ID = configurationId,
                        Name = configName,
                        Path = path,
                        Type = configurationType,
                        Schema = schema,
                        Plugin = plugin,
                    };
                }

                return null;
            })
            .WhereNotNull()
            .ToDictionary(info => info.ID);

        // Dispose of older definitions.
        foreach (var definition in _configurationDefinitions.Values.OfType<IDisposable>())
            definition.Dispose();

        _configurationDefinitions = configurationDefinitionDict
            .Where(kvp => _configurationTypes.ContainsKey(kvp.Key))
            .ToDictionary(kvp => kvp.Key, kvp => (IConfigurationDefinition)kvp.Value);

        _serializedSchemas = _configurationTypes.Values
            .ToDictionary(info => info, info => AddSchemaPropertyToSchema(info.Schema.ToJson()));

        _logger.LogTrace("Loaded {ConfigurationCount} configurations & {ProviderCount} configuration providers.", _configurationTypes.Count, _configurationDefinitions.Count);
    }

    public ConfigurationProvider<TConfig> CreateProvider<TConfig>() where TConfig : class, IConfiguration, new()
        => new(this);

    public IEnumerable<ConfigurationInfo> GetAllConfigurationInfos()
        => _configurationTypes.Values
            .OrderByDescending(p => p.Plugin.GetType() == typeof(CorePlugin))
            .ThenBy(p => p.Plugin.Name)
            .ThenBy(p => p.Name);

    public ConfigurationInfo GetConfigurationInfo<TConfig>() where TConfig : class, IConfiguration, new()
        => GetConfigurationInfo(GetID(typeof(TConfig)))!;

    public ConfigurationInfo? GetConfigurationInfo(Guid configurationId)
        => _configurationTypes.TryGetValue(configurationId, out var configInfo) ? configInfo : null;

    #endregion

    #region Validation

    public IReadOnlyDictionary<string, IReadOnlyList<string>> Validate(ConfigurationInfo info, string json)
    {
        var evalRes = info.Schema.Validate(json);
        var errors = new Dictionary<string, List<string>>();
        if (evalRes.Count > 0)
        {
            foreach (var error in evalRes)
            {
                // Ignore the "$schema" property at the root of the document if it is present.
                if (error.Path is "#/$schema" && error.Property is "$schema")
                    continue;

                var path = string.IsNullOrEmpty(error.Path) ? string.Empty : error.Path.StartsWith("#/") ? error.Path[2..] : error.Path;
                if (!errors.TryGetValue(path, out var errorList))
                    errors[path] = errorList = [];

                var message = GetErrorMessage(error);
                errorList.Add(message);
            }

            // Log validation errors if the server hasn't started yet.
            if (errors.Count > 0)
            {
                _logger.LogTrace("Configuration validation failed for {Type} with {ErrorCount} errors.", info.Name, errors.Sum(a => a.Value.Count));
                foreach (var (path, messages) in errors)
                    _logger.LogError("Configuration validation failed for {Type} at \"{Path}\": {Message}", info.Name, path, string.Join(", ", messages));
            }
        }

        return errors
            .Select(a => KeyValuePair.Create(a.Key, (IReadOnlyList<string>)a.Value))
            .ToDictionary();
    }

    public IReadOnlyDictionary<string, IReadOnlyList<string>> Validate<TConfig>(TConfig config) where TConfig : class, IConfiguration, new()
        => Validate(GetConfigurationInfo<TConfig>(), Serialize(config));

    #endregion

    #region New

    public IConfiguration New(ConfigurationInfo info)
    {
        try
        {
            return (IConfiguration)typeof(ConfigurationService)
                .GetMethod(nameof(NewInternal), BindingFlags.NonPublic | BindingFlags.Instance)!
                .MakeGenericMethod(info.Type)
                .Invoke(this, [])!;
        }
        catch (TargetInvocationException ex)
        {
            if (ex.InnerException is null)
                throw;
            throw ex.InnerException;
        }
    }

    public TConfig New<TConfig>() where TConfig : class, IConfiguration, new()
        => NewInternal<TConfig>();

    private TConfig NewInternal<TConfig>() where TConfig : class, IConfiguration, new()
    {
        var info = GetConfigurationInfo<TConfig>();
        if (_configurationDefinitions!.GetValueOrDefault(info.ID) is IConfigurationNewFactory<TConfig> provider)
            return provider.New();

        return new TConfig();
    }

    #endregion

    #region Load

    public IConfiguration Load(ConfigurationInfo info, bool copy = false)
    {
        try
        {
            return (IConfiguration)typeof(ConfigurationService)
                .GetMethod(nameof(LoadInternal), BindingFlags.NonPublic | BindingFlags.Instance)!
                .MakeGenericMethod(info.Type)
                .Invoke(this, [copy])!;
        }
        catch (TargetInvocationException ex)
        {
            if (ex.InnerException is null)
                throw;
            throw ex.InnerException;
        }
    }

    public TConfig Load<TConfig>(bool copy = false) where TConfig : class, IConfiguration, new()
        => LoadInternal<TConfig>(copy);

    private TConfig LoadInternal<TConfig>(bool copy) where TConfig : class, IConfiguration, new()
    {
        var info = GetConfigurationInfo<TConfig>();
        if (_loadedConfigurations.GetValueOrDefault(info.ID) is TConfig config)
            return copy ? config.DeepClone() : config;

        if (!File.Exists(info.Path))
        {
            config = New<TConfig>();
            Save(config);
            return copy ? config.DeepClone() : config;
        }

        lock (info)
        {

            var json = File.ReadAllText(info.Path);
            if (_configurationDefinitions!.GetValueOrDefault(info.ID) is IConfigurationDefinitionsWithMigrations { } provider)
                json = provider.ApplyMigrations(json);

            EnsureSchemaExists(info);

            var errors = Validate(info, json);
            if (errors.Count > 0)
                throw new ConfigurationValidationException(info, errors);

            _logger?.LogTrace("Loading configuration for {Name}.", info.Name);
            config = Deserialize<TConfig>(json);
            _logger?.LogTrace("Loaded configuration for {Name}.", info.Name);

            _loadedConfigurations[info.ID] = config;
            return copy ? config.DeepClone() : config;
        }
    }

    #endregion

    #region Save

    public void Save(ConfigurationInfo info, IConfiguration config)
    {
        try
        {
            typeof(ConfigurationService)
                .GetMethod(nameof(SaveInternal), BindingFlags.NonPublic | BindingFlags.Instance)!
                .MakeGenericMethod(info.Type)
                .Invoke(this, [info, Serialize(config), config]);
        }
        catch (TargetInvocationException ex)
        {
            if (ex.InnerException is null)
                throw;
            throw ex.InnerException;
        }
    }

    public void Save(ConfigurationInfo info, string json)
    {
        try
        {
            typeof(ConfigurationService)
                .GetMethod(nameof(SaveInternal), BindingFlags.NonPublic | BindingFlags.Instance)!
                .MakeGenericMethod(info.Type)
                .Invoke(this, [info, json, null]);
        }
        catch (TargetInvocationException ex)
        {
            if (ex.InnerException is null)
                throw;
            throw ex.InnerException;
        }
    }

    public void Save<TConfig>() where TConfig : class, IConfiguration, new()
        => Save(Load<TConfig>());

    public void Save<TConfig>(TConfig config) where TConfig : class, IConfiguration, new()
        => SaveInternal(GetConfigurationInfo<TConfig>(), Serialize(config), config);

    public void Save<TConfig>(string json) where TConfig : class, IConfiguration, new()
        => SaveInternal<TConfig>(GetConfigurationInfo<TConfig>(), json);

    private void SaveInternal<TConfig>(ConfigurationInfo info, string json, TConfig? config = null) where TConfig : class, IConfiguration, new()
    {
        json = AddSchemaProperty(info, json);
        var errors = Validate(info, json);
        if (errors.Count > 0)
            throw new ConfigurationValidationException(info, errors);

        lock (info)
        {
            var parentDir = Path.GetDirectoryName(info.Path)!;
            if (!Directory.Exists(parentDir))
                Directory.CreateDirectory(parentDir);

            EnsureSchemaExists(info);

            if (File.Exists(info.Path))
            {
                var oldJson = File.ReadAllText(info.Path);
                if (oldJson.Equals(json, StringComparison.Ordinal))
                {
                    _logger.LogTrace("Configuration for {Name} is unchanged. Skipping save.", info.Name);
                    return;
                }
            }

            _logger.LogTrace("Saving configuration for {Name}.", info.Name);
            File.WriteAllText(info.Path, json);
            _logger.LogTrace("Saved configuration for {Name}.", info.Name);

            _loadedConfigurations[info.ID] = config ?? Deserialize<TConfig>(json);
        }

        Saved?.Invoke(this, new ConfigurationSavedEventArgs() { ConfigurationInfo = info });
    }

    #endregion

    #region Schema

    public string GetSchema(ConfigurationInfo info)
        => _serializedSchemas[info];

    private void EnsureSchemaExists(ConfigurationInfo info)
    {
        var schemaJson = _serializedSchemas[info];
        var schemaPath = Path.ChangeExtension(info.Path, ".schema.json");
        if (!File.Exists(schemaPath))
        {
            _logger.LogTrace("Saving schema for {Name}.", info.Name);
            File.WriteAllText(schemaPath, schemaJson);
            _logger.LogTrace("Saved schema for {Name}.", info.Name);
            return;
        }

        var oldSchemaJson = File.ReadAllText(schemaPath);
        if (!oldSchemaJson.Equals(schemaJson, StringComparison.Ordinal))
        {
            _logger.LogTrace("Schema for {Name} changed. Saving new schema.", info.Name);
            File.WriteAllText(schemaPath, schemaJson);
            _logger.LogTrace("Saved schema for {Name}.", info.Name);
        }
    }

    private static string AddSchemaProperty(ConfigurationInfo info, string json)
    {
        if (json.Contains("$schema"))
            return json;

        if (json[0] == '{')
        {
            var baseUri = $"file://{Path.ChangeExtension(info.Path, ".schema.json").Replace("\"", "\\\"")}";
            if (json[1] == '\n')
            {
                if (json[2] == ' ')
                {
                    var spaceLength = 0;
                    for (var i = 2; i < json.Length; i++)
                    {
                        if (json[i] != ' ')
                            break;
                        spaceLength++;
                    }
                    return "{\n" + new string(' ', spaceLength) + "\"$schema\": \"" + baseUri + "\"," + json[1..];
                }

                if (json[2] == '\t')
                {
                    var tabLength = 0;
                    for (var i = 2; i < json.Length; i++)
                    {
                        if (json[i] != '\t')
                            break;
                        tabLength++;
                    }
                    return "{\n" + new string('\t', tabLength) + "\"$schema\": \"" + baseUri + "\"," + json[1..];
                }
            }

            return "{\"$schema\":\"" + baseUri + "\"," + json[1..];
        }
        return json;
    }

    private static string AddSchemaPropertyToSchema(string schema)
    {
        var propertiesIndex = schema.IndexOf("\"properties\": {");
        if (propertiesIndex > 0)
        {
            var newLineIndex = schema[..propertiesIndex].LastIndexOf('\n');
            if (newLineIndex > 0)
            {
                var spacing = schema[(newLineIndex + 1)..propertiesIndex];
                return $"{schema[0..(propertiesIndex + 15)]}\n{spacing}{spacing}\"$schema\": {{\n{spacing}{spacing}{spacing}\"type\": \"string\"\n{spacing}{spacing}}},{schema[(propertiesIndex + 15)..]}";
            }
        }
        return schema;
    }

    #endregion

    #region Serialization/Deserialization

    private TConfig Deserialize<TConfig>(string json) where TConfig : class, IConfiguration, new()
        => typeof(TConfig).IsAssignableTo(typeof(INewtonsoftJsonConfiguration))
            ? JsonConvert.DeserializeObject<TConfig>(json, _newtonsoftJsonSerializerSettings)!
            : System.Text.Json.JsonSerializer.Deserialize<TConfig>(json, _systemTextJsonSerializerOptions)!;

    public string Serialize(IConfiguration config)
        => config.GetType().IsAssignableTo(typeof(INewtonsoftJsonConfiguration))
            ? JsonConvert.SerializeObject(config, _newtonsoftJsonSerializerSettings)
            : System.Text.Json.JsonSerializer.Serialize(config, _systemTextJsonSerializerOptions)!;

    private string Serialize<TConfig>(TConfig config) where TConfig : class, IConfiguration, new()
        => typeof(TConfig).IsAssignableTo(typeof(INewtonsoftJsonConfiguration))
            ? JsonConvert.SerializeObject(config, _newtonsoftJsonSerializerSettings)
            : System.Text.Json.JsonSerializer.Serialize(config, _systemTextJsonSerializerOptions)!;

    private JsonSchema GetSchemaForType(Type type)
        => type.IsAssignableTo(typeof(INewtonsoftJsonConfiguration))
            ? new JsonSchemaGenerator(new NewtonsoftJsonSchemaGeneratorSettings { SerializerSettings = _newtonsoftJsonSerializerSettings }).Generate(type)
            : new JsonSchemaGenerator(new SystemTextJsonSchemaGeneratorSettings() { SerializerOptions = _systemTextJsonSerializerOptions }).Generate(type);

    #endregion

    #region Static Helper Methods

    #endregion

    /// <summary>
    /// Gets a unique ID for a configuration generated from its class name.
    /// </summary>
    /// <param name="provider">The provider.</param>
    /// <returns><see cref="Guid" />.</returns>
    private static Guid GetID(IConfigurationDefinition provider) => GetID(provider.ConfigurationType);

    /// <summary>
    /// Gets a unique ID for a configuration generated from its class name.
    /// </summary>
    /// <param name="providerType">The provider type.</param>
    /// <returns><see cref="Guid" />.</returns>
    private static Guid GetID(Type providerType)
        => new(MD5.HashData(Encoding.Unicode.GetBytes(providerType.FullName!)));

    /// <summary>
    /// Gets the display name for a configuration inferred from it's type.
    /// </summary>
    /// <param name="type">The type.</param>
    private static string GetDisplayName(Type type)
    {
        var displayAttribute = type.GetCustomAttribute<DisplayAttribute>();
        if (displayAttribute != null && !string.IsNullOrEmpty(displayAttribute.Name))
        {
            return displayAttribute.Name;
        }

        // If no attributes, auto-infer from class name.
        return DisplayNameRegex().Replace(type.Name, " $1");
    }

    /// <summary>
    /// Simple regex to auto-infer display name from PascalCase class names.
    /// </summary>
    [GeneratedRegex(@"(\B[A-Z](?![A-Z]))")]
    private static partial Regex DisplayNameRegex();

    private static string GetErrorMessage(ValidationError validationError)
    {
        return validationError.Kind switch
        {
            _ => DisplayNameRegex().Replace(validationError.Kind.ToString(), " $1"),
        };
    }
}
