using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Force.DeepCloner;
using Microsoft.Extensions.Logging;
using Namotion.Reflection;
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
using Shoko.Plugin.Abstractions.Plugin;
using Shoko.Plugin.Abstractions.Services;
using Shoko.Server.Extensions;
using Shoko.Server.Plugin;
using Shoko.Server.Settings;

#nullable enable
namespace Shoko.Server.Services;

public class ConfigurationService : IConfigurationService
{
    private readonly ILogger<ConfigurationService> _logger;

    private readonly IApplicationPaths _applicationPaths;

    private readonly IPluginManager _pluginManager;

    private readonly JsonSerializerSettings _newtonsoftJsonSerializerSettings;

    private readonly System.Text.Json.JsonSerializerOptions _systemTextJsonSerializerOptions;

    private Dictionary<Guid, ConfigurationInfo> _configurationTypes;

    private Dictionary<Guid, IConfigurationDefinition> _configurationDefinitions;

    private Dictionary<ConfigurationInfo, string> _serializedSchemas;

    private readonly ConcurrentDictionary<Guid, IConfiguration> _loadedConfigurations = [];

    private bool _loaded = false;

    public event EventHandler<ConfigurationSavedEventArgs>? Saved;

    public ConfigurationService(ILogger<ConfigurationService> logger, IApplicationPaths applicationPaths, IPluginManager pluginManager)
    {
        var serverSettingsDefinition = new ServerSettingsDefinition(new(this));

        _logger = logger;
        _applicationPaths = applicationPaths;
        _pluginManager = pluginManager;

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
                Guid.Empty,
                new ConfigurationInfo()
                {
                    ID = Guid.Empty,
                    Name = PluginManager.GetDisplayName(typeof(ServerSettings)),
                    Description = string.Empty,
                    Path = Path.Join(_applicationPaths.ProgramDataPath, serverSettingsDefinition.RelativePath),
                    Type = typeof(ServerSettings),
                    // We're not going to be using this before .AddParts is called.
                    PluginInfo = null!,
                    Schema = GetSchemaForType(typeof(ServerSettings)),
                }
            },
        };

        _configurationDefinitions = new()
        {
            { Guid.Empty,  serverSettingsDefinition },
        };

        _serializedSchemas = _configurationTypes.Values
            .ToDictionary(info => info, info => AddSchemaPropertyToSchema(info.Schema.ToJson()));
    }

    #region Configuration Info

    private static readonly HashSet<string> _configurationSuffixSet = ["Setting", "Settings", "Conf", "Config", "Configuration"];

    public void AddParts(IEnumerable<Type> configurationTypes, IEnumerable<IConfigurationDefinition> configurationDefinitions)
    {
        if (_loaded) return;
        _loaded = true;

        ArgumentNullException.ThrowIfNull(configurationTypes);
        ArgumentNullException.ThrowIfNull(configurationDefinitions);

        _logger.LogInformation("Initializing service.");

        var configurationDefinitionDict = configurationDefinitions.ToDictionary(GetID);
        _configurationTypes = configurationTypes
            .Select(configurationType =>
            {
                var pluginInfo = _pluginManager.GetPluginInfo(
                    Loader.GetTypes<IPlugin>(configurationType.Assembly)
                        .First(t => _pluginManager.GetPluginInfo(t) is not null)
                )!;
                var contextualType = configurationType.ToContextualType();
                var name = PluginManager.GetDisplayName(contextualType);
                foreach (var suffix in _configurationSuffixSet)
                {
                    var endWith = $" {suffix}";
                    if (name.EndsWith(endWith, StringComparison.OrdinalIgnoreCase))
                        name = name[..^endWith.Length];
                }
                var description = PluginManager.GetDescription(contextualType);
                var id = GetID(configurationType, pluginInfo);
                var definition = configurationDefinitionDict.GetValueOrDefault(id);
                string path;
                if (definition is IConfigurationDefinitionWithCustomSaveLocation { } p0)
                {
                    var relativePath = p0.RelativePath;
                    if (!relativePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                        relativePath += ".json";
                    path = Path.Join(_applicationPaths.ProgramDataPath, relativePath);
                }
                else if (definition is IConfigurationDefinitionWithCustomSaveName { } p1)
                {
                    var fileName = p1.Name;
                    if (!fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                        fileName += ".json";
                    path = Path.Join(_applicationPaths.PluginConfigurationsPath, pluginInfo.ID.ToString(), fileName);
                }
                else
                {
                    var fileName = name
                        .RemoveInvalidPathCharacters()
                        .Replace(' ', '-')
                        .ToLower();
                    path = Path.Join(_applicationPaths.PluginConfigurationsPath, pluginInfo.ID.ToString(), fileName + ".json");
                }
                var schema = GetSchemaForType(configurationType);
                return new ConfigurationInfo()
                {
                    ID = id,
                    Name = name,
                    Description = description,
                    Path = path,
                    Type = configurationType,
                    Schema = schema,
                    PluginInfo = pluginInfo,
                };
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
            .OrderByDescending(p => p.PluginInfo.PluginType == typeof(CorePlugin))
            .ThenBy(p => p.PluginInfo.Name)
            .ThenBy(p => p.Name);

    public IReadOnlyList<ConfigurationInfo> GetConfigurationInfo(IPlugin plugin)
        => _configurationTypes.Values
            .Where(info => info.PluginInfo.ID == plugin.ID)
            .OrderBy(info => info.Name)
            .ThenBy(info => info.ID)
            .ToList();

    public ConfigurationInfo GetConfigurationInfo<TConfig>() where TConfig : class, IConfiguration, new()
        => GetConfigurationInfo(GetID(typeof(TConfig)))!;

    public ConfigurationInfo? GetConfigurationInfo(Guid configurationId)
        => _configurationTypes.TryGetValue(configurationId, out var configInfo) ? configInfo : null;

    #endregion

    #region Validation

    public IReadOnlyDictionary<string, IReadOnlyList<string>> Validate(ConfigurationInfo info, string json)
    {
        var results = info.Schema.Validate(json);
        var errorDict = new Dictionary<string, List<string>>();
        foreach (var error in results)
        {
            // Ignore the "$schema" property at the root of the document if it is present.
            if (error is { Path: "#/$schema", Property: "$schema", Kind: ValidationErrorKind.NoAdditionalPropertiesAllowed })
                continue;

            var path = string.IsNullOrEmpty(error.Path) ? string.Empty : error.Path.StartsWith("#/") ? error.Path[2..] : error.Path;
            if (!errorDict.TryGetValue(path, out var errorList))
                errorDict[path] = errorList = [];

            errorList.Add(GetErrorMessage(error));
        }

        if (errorDict.Count > 0)
        {
            _logger.LogTrace("Configuration validation failed for {Type} with {ErrorCount} errors.", info.Name, errorDict.Sum(a => a.Value.Count));
            foreach (var (path, messages) in errorDict)
                _logger.LogError("Configuration validation failed for {Type} at \"{Path}\": {Message}", info.Name, path, string.Join(", ", messages));
        }

        return errorDict
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
                throw new ConfigurationValidationException("load", info, errors);

            config = Deserialize<TConfig>(json);
            _loadedConfigurations[info.ID] = config;
            return copy ? config.DeepClone() : config;
        }
    }

    #endregion

    #region Save

    public bool Save(ConfigurationInfo info, IConfiguration config)
    {
        try
        {
            return (bool)typeof(ConfigurationService)
                .GetMethod(nameof(SaveInternal), BindingFlags.NonPublic | BindingFlags.Instance)!
                .MakeGenericMethod(info.Type)
                .Invoke(this, [info, Serialize(config), config])!;
        }
        catch (TargetInvocationException ex)
        {
            if (ex.InnerException is null)
                throw;
            throw ex.InnerException;
        }
    }

    public bool Save(ConfigurationInfo info, string json)
    {
        try
        {
            return (bool)typeof(ConfigurationService)
                .GetMethod(nameof(SaveInternal), BindingFlags.NonPublic | BindingFlags.Instance)!
                .MakeGenericMethod(info.Type)
                .Invoke(this, [info, json, null])!;
        }
        catch (TargetInvocationException ex)
        {
            if (ex.InnerException is null)
                throw;
            throw ex.InnerException;
        }
    }

    public bool Save<TConfig>() where TConfig : class, IConfiguration, new()
        => Save(Load<TConfig>());

    public bool Save<TConfig>(TConfig config) where TConfig : class, IConfiguration, new()
        => SaveInternal(GetConfigurationInfo<TConfig>(), Serialize(config), config);

    public bool Save<TConfig>(string json) where TConfig : class, IConfiguration, new()
        => SaveInternal<TConfig>(GetConfigurationInfo<TConfig>(), json);

    private bool SaveInternal<TConfig>(ConfigurationInfo info, string json, TConfig? config = null) where TConfig : class, IConfiguration, new()
    {
        json = AddSchemaProperty(info, json);
        var errors = Validate(info, json);
        if (errors.Count > 0)
            throw new ConfigurationValidationException("save", info, errors);

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
                    return false;
                }
            }

            _logger.LogTrace("Saving configuration for {Name}.", info.Name);
            File.WriteAllText(info.Path, json);
            _logger.LogTrace("Saved configuration for {Name}.", info.Name);

            _loadedConfigurations[info.ID] = config ?? Deserialize<TConfig>(json);
        }

        Saved?.Invoke(this, new ConfigurationSavedEventArgs() { ConfigurationInfo = info });

        return true;
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
    private Guid GetID(IConfigurationDefinition provider) => GetID(provider.ConfigurationType);

    private Guid GetID(Type type)
        => _loaded && Loader.GetTypes<IPlugin>(type.Assembly).FirstOrDefault(t => _pluginManager.GetPluginInfo(t) is not null) is { } pluginType
            ? GetID(type, _pluginManager.GetPluginInfo(pluginType)!)
            : Guid.Empty;

    private static Guid GetID(Type type, PluginInfo pluginInfo)
        => UuidUtility.GetV5($"Configuration={type.FullName!}", pluginInfo.ID);

    private static string GetErrorMessage(ValidationError validationError)
    {
        return validationError.Kind switch
        {
            _ => PluginManager.GetDisplayName(validationError.Kind.ToString()),
        };
    }
}
