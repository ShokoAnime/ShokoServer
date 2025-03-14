using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using Force.DeepCloner;
using Microsoft.Extensions.Logging;
using Namotion.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using NJsonSchema;
using NJsonSchema.Generation;
using NJsonSchema.Generation.TypeMappers;
using NJsonSchema.NewtonsoftJson.Generation;
using NJsonSchema.Validation;
using Shoko.Plugin.Abstractions;
using Shoko.Plugin.Abstractions.Config;
using Shoko.Plugin.Abstractions.Config.Attributes;
using Shoko.Plugin.Abstractions.Config.Exceptions;
using Shoko.Plugin.Abstractions.Events;
using Shoko.Plugin.Abstractions.Plugin;
using Shoko.Plugin.Abstractions.Services;
using Shoko.Server.Extensions;
using Shoko.Server.Plugin;
using Shoko.Server.Settings;

#nullable enable
namespace Shoko.Server.Services;

public partial class ConfigurationService : IConfigurationService, ISchemaProcessor, ISchemaNameGenerator
{
    private readonly ILogger<ConfigurationService> _logger;

    private readonly IApplicationPaths _applicationPaths;

    private readonly IPluginManager _pluginManager;

    private readonly JsonSerializerSettings _newtonsoftJsonSerializerSettings;

    private readonly System.Text.Json.JsonSerializerOptions _systemTextJsonSerializerOptions;

    private Dictionary<Guid, ConfigurationInfo> _configurationTypes;

    private Dictionary<ConfigurationInfo, string> _serializedSchemas;

    private readonly ConcurrentDictionary<Guid, IConfiguration> _loadedConfigurations = [];

    private readonly ConcurrentDictionary<Guid, string> _savedMemoryConfigurations = [];

    private bool _loaded = false;

    public event EventHandler<ConfigurationSavedEventArgs>? Saved;

    public ConfigurationService(ILogger<ConfigurationService> logger, IApplicationPaths applicationPaths, IPluginManager pluginManager)
    {

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

        var serverSettingsDefinition = new ServerSettingsDefinition(new(this));
        var serverSettingsName = TypeReflectionExtensions.GetDisplayName(typeof(ServerSettings));
        var serverSettingsSchema = GetSchemaForType(typeof(ServerSettings));
        _configurationTypes = new()
        {
            {
                Guid.Empty,
                new ConfigurationInfo()
                {
                    ID = Guid.Empty,
                    Path = Path.Join(_applicationPaths.ProgramDataPath, serverSettingsDefinition.RelativePath),
                    Name = serverSettingsName,
                    Description = string.Empty,
                    Definition = serverSettingsDefinition,
                    Type = typeof(ServerSettings),
                    ContextualType = typeof(ServerSettings).ToContextualType(),
                    Schema = serverSettingsSchema,
                    // We're not going to be using this before .AddParts is called.
                    PluginInfo = null!,
                }
            },
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

        // Set the server settings to the proper ID before trying to enumerate the config type and definitions.
        var serverSettingsID = GetID(typeof(ServerSettings));
        var serverSettingsInfo = _configurationTypes[Guid.Empty];
        _configurationTypes = new()
        {
            {
                serverSettingsID,
                new ConfigurationInfo()
                {
                    ID = serverSettingsID,
                    Path = serverSettingsInfo.Path,
                    Name = serverSettingsInfo.Name,
                    Description = serverSettingsInfo.Description,
                    Definition = serverSettingsInfo.Definition,
                    Type = serverSettingsInfo.Type,
                    ContextualType = serverSettingsInfo.ContextualType,
                    Schema = serverSettingsInfo.Schema,
                    PluginInfo = _pluginManager.GetPluginInfo<CorePlugin>()!,
                }
            },
        };
        _serializedSchemas = new() { { _configurationTypes[serverSettingsID], _serializedSchemas[serverSettingsInfo] } };

        // Dispose of older definitions.
        foreach (var definition in _configurationTypes.Values.Select(info => info.Definition).OfType<IDisposable>())
            definition.Dispose();

        var configurationDefinitionDict = configurationDefinitions.ToDictionary(GetID);
        _configurationTypes = configurationTypes
            .Select(configurationType =>
            {
                var pluginInfo = _pluginManager.GetPluginInfo(Loader.GetTypes<IPlugin>(configurationType.Assembly).First(t => _pluginManager.GetPluginInfo(t) is not null))!;
                var id = GetID(configurationType, pluginInfo);
                var definition = configurationDefinitionDict.GetValueOrDefault(id);
                var contextualType = configurationType.ToContextualType();
                var description = TypeReflectionExtensions.GetDescription(contextualType);
                var name = TypeReflectionExtensions.GetDisplayName(contextualType);
                foreach (var suffix in _configurationSuffixSet)
                {
                    var endWith = $" {suffix}";
                    if (name.EndsWith(endWith, StringComparison.OrdinalIgnoreCase))
                        name = name[..^endWith.Length];
                }

                string? path = null;
                if (definition is IConfigurationDefinitionWithCustomSaveLocation { } p0)
                {
                    var relativePath = p0.RelativePath;
                    if (!relativePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                        relativePath += ".json";
                    path = Path.Join(_applicationPaths.ProgramDataPath, relativePath);
                }
                else if (definition is IConfigurationDefinitionWithCustomSaveName { } p1)
                {
                    // If name is empty or null, then treat it as an in-memory configuration.
                    if (!string.IsNullOrEmpty(p1.Name))
                    {
                        var fileName = p1.Name;
                        if (!fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                            fileName += ".json";
                        path = Path.Join(_applicationPaths.PluginConfigurationsPath, pluginInfo.ID.ToString(), fileName);
                    }
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
                    Path = path,
                    Name = name,
                    Description = description,
                    Type = configurationType,
                    ContextualType = contextualType,
                    Definition = definition,
                    Schema = schema,
                    PluginInfo = pluginInfo,
                };
            })
            .WhereNotNull()
            .ToDictionary(info => info.ID);

        _serializedSchemas = _configurationTypes.Values
            .ToDictionary(info => info, info => AddSchemaPropertyToSchema(info.Schema.ToJson()));

        _logger.LogTrace("Loaded {ConfigurationCount} configurations.", _configurationTypes.Count);
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
        try
        {
            return (IReadOnlyDictionary<string, IReadOnlyList<string>>)typeof(ConfigurationService)
                .GetMethod(nameof(ValidateInternal), BindingFlags.NonPublic | BindingFlags.Instance)!
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

    public IReadOnlyDictionary<string, IReadOnlyList<string>> Validate<TConfig>(TConfig config) where TConfig : class, IConfiguration, new()
        => ValidateInternal(GetConfigurationInfo<TConfig>(), SerializeInternal(config), config);

    private Dictionary<string, IReadOnlyList<string>> ValidateInternal<TConfig>(ConfigurationInfo info, string json, TConfig? config = null) where TConfig : class, IConfiguration, new()
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

        if (errorDict.Count is 0 && info.Definition is IConfigurationDefinitionWithCustomValidation<TConfig> { } provider)
        {
            config ??= DeserializeInternal<TConfig>(json);

            _logger.LogTrace("Calling custom validation for {Type}.", info.Name);
            errorDict = provider.Validate(config).ToDictionary(a => a.Key, a => a.Value.ToList());
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

    private static string GetErrorMessage(ValidationError validationError)
    {
        return validationError.Kind switch
        {
            _ => TypeReflectionExtensions.GetDisplayName(validationError.Kind.ToString()),
        };
    }

    #endregion

    #region Custom Actions

    public ConfigurationActionResult PerformAction(ConfigurationInfo info, IConfiguration configuration, string path, string action)
    {
        try
        {
            return (ConfigurationActionResult)typeof(ConfigurationService)
                .GetMethod(nameof(PerformActionInternal), BindingFlags.NonPublic | BindingFlags.Static)!
                .MakeGenericMethod(configuration.GetType())
                .Invoke(this, [info, configuration, path, action])!;
        }
        catch (TargetInvocationException ex)
        {
            if (ex.InnerException is null)
                throw;
            throw ex.InnerException;
        }
    }

    public ConfigurationActionResult PerformAction<TConfig>(TConfig configuration, string path, string action) where TConfig : class, IConfiguration, new()
        => PerformActionInternal(GetConfigurationInfo<TConfig>(), configuration, path, action);

    [GeneratedRegex(@"(?<!\\)\.")]
    private static partial Regex SplitPathToPartsRegex();

    [GeneratedRegex(@"(?<!\\)""")]
    private static partial Regex InvalidQuoteRegex();

    private static ConfigurationActionResult PerformActionInternal<TConfig>(ConfigurationInfo info, TConfig configuration, string path, string action) where TConfig : class, IConfiguration, new()
    {
        var schema = info.Schema;
        var type = info.ContextualType;
        var parts = path.Split(SplitPathToPartsRegex(), StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        while (parts.Length > 0)
        {
            // `part` can be in the format 'Part', '[0]', or '["Part"]', where
            // the first is a property name, the second is an array index, and
            // the third is a dictionary key.
            var part = parts[0];
            parts = parts[1..];
            if (part[0] == '[')
            {
                if (part[^1] != ']' || part.Length < 3 || part[^2] != '\\')
                    throw new InvalidConfigurationActionException($"Invalid path \"{path}\"", nameof(path));

                // Dictionary
                if (part[1] == '"')
                {
                    // Make sure the dictionary key is a valid string
                    if (part[^2] != '"' || path.Length < 5 || path[^3] != '\\' || InvalidQuoteRegex().IsMatch(part[2..^2]))
                        throw new InvalidConfigurationActionException($"Invalid path \"{path}\"", nameof(path));

                    if (schema.PatternProperties.Count != 1 || !type.IsAssignableToTypeName("IReadOnlyDictionary", TypeNameStyle.Name))
                        throw new InvalidConfigurationActionException($"Invalid path \"{path}\"", nameof(path));

                    schema = schema.PatternProperties.Values.First() is { } patternSchema
                        ? patternSchema.Reference is { } referencedSchema2
                            ? referencedSchema2 : patternSchema
                        : throw new InvalidConfigurationActionException($"Invalid path \"{path}\"", nameof(path));
                    type = type.GenericArguments[1];
                }
                // Array
                else
                {
                    if (
                        schema.Item is null ||
                        type.GenericArguments.Length == 0 ||
                        !type.IsAssignableToTypeName("IEnumerable", TypeNameStyle.Name)
                    )
                        throw new InvalidConfigurationActionException($"Invalid path \"{path}\"", nameof(path));

                    schema = schema.Item.Reference is { } referencedSchema1 ? referencedSchema1 : schema.Item;
                    type = type.GenericArguments[0];
                }
            }
            // Classes
            else
            {
                schema = schema.Properties.TryGetValue(part, out var propertySchema)
                    ? propertySchema.Reference is { } referencedSchema0 ? referencedSchema0 : propertySchema
                    : throw new InvalidConfigurationActionException($"Invalid path \"{path}\"", nameof(path));
                type = type.Properties.FirstOrDefault(x => x.Name == part)?.PropertyType ??
                    throw new InvalidConfigurationActionException($"Invalid path \"{path}\"", nameof(path));
            }
        }

        var attributes = type.GetAttributes<CustomActionAttribute>(false).ToList();
        if (attributes.Count == 0)
            throw new InvalidConfigurationActionException($"No actions attribute found for path \"{path}\"", nameof(path));
        if (!attributes.Any(x => x.Name == action))
            throw new InvalidConfigurationActionException($"Invalid action \"{action}\" for path \"{path}\"", nameof(action));

        if (info.Definition is IConfigurationDefinitionWithCustomActions<TConfig> provider)
            return provider.PerformAction(configuration, type, path, action);
        return new();
    }

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
        if (info.Definition is IConfigurationDefinitionWithNewFactory<TConfig> provider)
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

        if (info.Path is null || !File.Exists(info.Path))
        {
            config = New<TConfig>();
            Save(config);
            return copy ? config.DeepClone() : config;
        }

        lock (info)
        {
            var json = File.ReadAllText(info.Path);
            if (info.Definition is IConfigurationDefinitionWithMigrations { } provider)
                json = provider.ApplyMigrations(json);

            EnsureSchemaExists(info);

            var errors = Validate(info, json);
            if (errors.Count > 0)
                throw new ConfigurationValidationException("load", info, errors);

            config = DeserializeInternal<TConfig>(json);
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
        => SaveInternal(GetConfigurationInfo<TConfig>(), SerializeInternal(config), config);

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

            if (info.Path is null)
            {
                if (_savedMemoryConfigurations.TryGetValue(info.ID, out var oldJson) && oldJson is not null && oldJson.Equals(json, StringComparison.Ordinal))
                {
                    _logger.LogTrace("In-memory configuration for {Name} is unchanged. Skipping save.", info.Name);
                    return false;
                }

                _logger.LogTrace("Saving in-memory configuration for {Name}.", info.Name);
                _savedMemoryConfigurations[info.ID] = json;
                _logger.LogTrace("Saved in-memory configuration for {Name}.", info.Name);
            }
            else
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
            }

            _loadedConfigurations[info.ID] = config ?? DeserializeInternal<TConfig>(json);
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
        if (info.Path is null)
            return;

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
        if (json.Contains("$schema") || info.Path is null)
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

    private Type? _currentType = null;

    private readonly Dictionary<string, (Dictionary<string, object?> ClassUIDefinition, Dictionary<string, Dictionary<string, object?>> PropertyUIDefinitions)> _schemaCache = [];

    private readonly Dictionary<JsonSchema, string> _schemaKeys = [];

    private JsonSchema GetSchemaForType(Type type)
    {
        var generator = type.IsAssignableTo(typeof(INewtonsoftJsonConfiguration))
            ? GetNewtonsoftSchemaForType()
            : GetSystemTextJsonSchemaForType();
        generator.Settings.SchemaProcessors.Add(this);

        // Handle built-in types because apparently NJsonSchema doesn't.
        generator.Settings.TypeMappers.Add(new PrimitiveTypeMapper(typeof(Version), s =>
        {
            s.Type = JsonObjectType.String;
            s.Format = "version";
        }));
        generator.Settings.TypeMappers.Add(new PrimitiveTypeMapper(typeof(DateTime), s =>
        {
            s.Type = JsonObjectType.String;
            s.Format = "date-time";
        }));
        generator.Settings.TypeMappers.Add(new PrimitiveTypeMapper(typeof(DateOnly), s =>
        {
            s.Type = JsonObjectType.String;
            s.Format = "date";
        }));
        generator.Settings.TypeMappers.Add(new PrimitiveTypeMapper(typeof(TimeSpan), s =>
        {
            s.Type = JsonObjectType.String;
            s.Format = "date-span";
        }));
        generator.Settings.TypeMappers.Add(new PrimitiveTypeMapper(typeof(TimeOnly), s =>
        {
            s.Type = JsonObjectType.String;
            s.Format = "time";
        }));

        _schemaCache.Clear();
        _schemaKeys.Clear();
        _currentType = type;
        var schema = generator.Generate(type);
        var schemaDefinitions = schema.Definitions.Values.Where(s => !s.IsEnumeration).Prepend(schema).ToList();
        // Post-process the schema; add the UI definitions at the correct locations.
        foreach (var subSchema in schemaDefinitions)
        {
            if (!_schemaKeys.TryGetValue(subSchema, out var schemaKey))
                continue;

            if (!_schemaCache.TryGetValue(schemaKey, out var schemaTuple))
                continue;
            var (classDefinition, propertyDict) = schemaTuple;
            if (classDefinition.Count > 0)
            {
                if (classDefinition.TryGetValue("label", out var classLabel))
                {
                    classDefinition.Remove("label");
                    subSchema.Title = (string)classLabel!;
                }
                subSchema.ExtensionData ??= new Dictionary<string, object?>();
                subSchema.ExtensionData.Add("x-uiDefinition", classDefinition);
            }
            foreach (var tuple in subSchema.Properties)
            {
                var (propertyKey, schemaValue) = tuple;
                if (schemaValue.Item is not null)
                    propertyKey += "+List";
                if (!propertyDict.TryGetValue(propertyKey, out var propertyDefinition))
                    continue;

                if (propertyDefinition.TryGetValue("label", out var propertyLabel
                ))
                {
                    propertyDefinition.Remove("label");
                    schemaValue.Title = (string)propertyLabel!;
                }
                schemaValue.ExtensionData ??= new Dictionary<string, object?>();
                schemaValue.ExtensionData.Add("x-uiDefinition", propertyDefinition);
            }
        }
        _schemaCache.Clear();
        _schemaKeys.Clear();
        _currentType = null;
        return schema;
    }

    private JsonSchemaGenerator GetNewtonsoftSchemaForType()
    {
        var generator = new JsonSchemaGenerator(new NewtonsoftJsonSchemaGeneratorSettings
        {
            SerializerSettings = _newtonsoftJsonSerializerSettings,
            SchemaType = SchemaType.JsonSchema,
            GenerateEnumMappingDescription = true,
            FlattenInheritanceHierarchy = true,
            AlwaysAllowAdditionalObjectProperties = true,
            AllowReferencesWithProperties = true,
            SchemaNameGenerator = this,
        });
        return generator;
    }

    private JsonSchemaGenerator GetSystemTextJsonSchemaForType()
    {
        var generator = new JsonSchemaGenerator(new SystemTextJsonSchemaGeneratorSettings
        {
            SerializerOptions = _systemTextJsonSerializerOptions,
            SchemaType = SchemaType.JsonSchema,
            GenerateEnumMappingDescription = true,
            FlattenInheritanceHierarchy = true,
            AlwaysAllowAdditionalObjectProperties = true,
            AllowReferencesWithProperties = true,
            SchemaNameGenerator = this,
        });
        return generator;
    }

    #region Schema | ISchemaProcessor Implementation

    void ISchemaProcessor.Process(SchemaProcessorContext context)
    {
        var schema = context.Schema.ActualSchema;
        var contextualType = context.ContextualType;
        if (contextualType.Context is ContextualPropertyInfo info)
        {
            var uiDict = new Dictionary<string, object?>();
            var schemaKey = info.MemberInfo.ReflectedType!.FullName!;
            _schemaCache.TryAdd(schemaKey, ([], []));
            var propertyKey = GetPropertyKey(info);
            if (schema.Item is not null)
                propertyKey += "+List";
            if (!_schemaCache[schemaKey].PropertyUIDefinitions.TryAdd(propertyKey, uiDict))
                uiDict = _schemaCache[schemaKey].PropertyUIDefinitions[propertyKey];

            if (info.GetAttribute<KeyAttribute>(false) is { })
                uiDict.Add("primaryKey", true);

            if (info.GetAttribute<DisplayAttribute>(false) is { } displayAttribute && !string.IsNullOrWhiteSpace(displayAttribute.Name))
                uiDict.TryAdd("label", displayAttribute.Name);
            else
                uiDict.TryAdd("label", TypeReflectionExtensions.GetDisplayName(info.Name));

            if (info.GetAttribute<VisibilityAttribute>(false) is { } visibilityAttribute)
            {
                var visibilityDict = new Dictionary<string, object?>()
                {
                    { "default", visibilityAttribute.Visibility.ToString().ToLower() switch { "readonly" => "read-only", string def => def } },
                };
                if (visibilityAttribute.HasToggle)
                {
                    var toggleDict = new Dictionary<string, object?>()
                    {
                        { "path", visibilityAttribute.ToggleWhenMemberIsSet },
                        { "value", Convert(_currentType!, visibilityAttribute.ToggleWhenSetTo) },
                        { "visibility", visibilityAttribute.ToggleVisibilityTo.ToString().ToLower() switch { "readonly" => "read-only", string def => def } }
                    };
                    visibilityDict.Add("toggle", toggleDict);
                }
                uiDict.Add("visibility", visibilityDict);
            }

            if (info.GetAttribute<BadgeAttribute>(false) is { } badgeAttribute && !string.IsNullOrWhiteSpace(badgeAttribute.Name))
            {
                var badgeDict = new Dictionary<string, object?>
                {
                    { "name", badgeAttribute.Name },
                    { "theme", JsonConvert.DeserializeObject<string?>(JsonConvert.SerializeObject(badgeAttribute.Theme, Formatting.None, _newtonsoftJsonSerializerSettings)) }
                };
                uiDict.Add("badge", badgeDict);
            }

            if (schema.IsEnumeration)
            {
                schema.Enumeration.Clear();
                schema.EnumerationNames.Clear();

                var enumList = new List<Dictionary<string, object?>>();
                var enumValueConverter = context.Settings.ReflectionService.GetEnumValueConverter(context.Settings);
                foreach (var enumName in Enum.GetNames(contextualType.Type))
                {
                    string? value = null;
                    var field = contextualType.GetField(enumName)!;
                    var title = TypeReflectionExtensions.GetDisplayName(field);
                    var description = TypeReflectionExtensions.GetDescription(field);
                    if (field.GetAttribute<EnumMemberAttribute>(false) is { } enumMemberAttribute && !string.IsNullOrEmpty(enumMemberAttribute.Value))
                        value = enumMemberAttribute.Value;
                    else
                        value = enumValueConverter(Enum.Parse(contextualType.Type, enumName));

                    schema.Enumeration.Add(value);
                    schema.EnumerationNames.Add(enumName);
                    enumList.Add(new()
                    {
                        { "title", title },
                        { "description", description },
                        { "value", value },
                    });
                }
                uiDict.Add("elementType", "enum");
                uiDict.Add("enumDefinitions", enumList);
                uiDict.Add("enumIsFlag", schema.IsFlagEnumerable);
            }
            else if (schema.Item is { } itemSchema)
            {
                uiDict.Add("elementType", "list");
                uiDict.Add("listElementType", "auto");
                if (info.GetAttribute<ListAttribute>(false) is { } listAttribute)
                {
                    uiDict.Add("listType", listAttribute.ListType.ToString().ToLower());
                    uiDict.Add("listSortable", listAttribute.Sortable);
                    uiDict.Add("listUniqueItems", listAttribute.UniqueItems);
                }
                else
                {
                    uiDict.Add("listType", "auto");
                    uiDict.Add("listSortable", true);
                    uiDict.Add("listUniqueItems", false);
                }

                // Only set if the referenced schema is a class definition
                if (itemSchema.Reference is not null && _schemaKeys.TryGetValue(itemSchema.Reference.ActualSchema, out var referencedSchemaKey))
                {
                    var referencedDict = _schemaCache[referencedSchemaKey].ClassUIDefinition;
                    foreach (var (key, value) in referencedDict)
                    {
                        if (key is "elementType" && value is not "auto")
                            uiDict["listElementType"] = value;
                        else if (key is "sectionType" or "primaryKey")
                            uiDict.TryAdd(key, value);
                    }
                }

                var innerDict = _schemaCache[schemaKey].PropertyUIDefinitions[propertyKey[..^5]];
                foreach (var (key, value) in innerDict)
                {
                    if (key is "elementType" && value is not "auto")
                        uiDict["listElementType"] = value;
                    else if (key is not "elementType")
                        uiDict.TryAdd(key, value);
                }
                if (uiDict["listType"] is "dropdown")
                {
                    if (uiDict["listElementType"] is not "section-container")
                        throw new NotSupportedException("Dropdown lists are not supported for non-class list items.");
                    if (!uiDict.ContainsKey("primaryKey"))
                        throw new NotSupportedException("Dropdown lists must have a primary key set.");
                }
                if (uiDict["listType"] is "checkbox" && uiDict["listElementType"] is not "enum")
                    throw new NotSupportedException("Dropdown lists are not supported for non-class list items.");
            }
            else if (info.GetAttribute<CodeEditorAttribute>(false) is { } codeBlockAttribute)
            {
                uiDict.Add("elementType", "code-block");
                uiDict.Add("codeLanguage", codeBlockAttribute.Language.ToString());
            }
            else if (info.GetAttribute<TextAreaAttribute>(false) is not null)
            {
                uiDict.Add("elementType", "text-area");
            }
            else if (info.GetAttribute<PasswordPropertyTextAttribute>(false) is not null)
            {
                uiDict.Add("elementType", "password");
            }
            else
            {
                uiDict.TryAdd("elementType", "auto");
            }
        }

        // Add a reference to the class schema and generate the schema if it's not done yet.
        if (schema.Properties.Count > 0 && (!contextualType.Type.FullName?.StartsWith("System.") ?? false))
        {
            var schemaKey = contextualType.Type.FullName!;
            _schemaKeys.TryAdd(schema, schemaKey);
            _schemaCache.TryAdd(schemaKey, ([], []));

            // Only generate the class schema once per type.
            if (_schemaCache[schemaKey].ClassUIDefinition is { Count: 0 } uiDict)
            {
                if (contextualType.GetAttribute<DisplayAttribute>(false) is { } displayAttribute && !string.IsNullOrWhiteSpace(displayAttribute.Name))
                    uiDict.Add("label", displayAttribute.Name);

                var propertyDefinitions = _schemaCache[schemaKey].PropertyUIDefinitions;
                var primaryKey = propertyDefinitions.FirstOrDefault(x => x.Value.ContainsKey("primaryKey") && x.Value["primaryKey"] is true).Key;
                if (!string.IsNullOrEmpty(primaryKey))
                    uiDict.Add("primaryKey", primaryKey);

                var actions = contextualType.GetAttributes<CustomActionAttribute>(false).ToList();
                var hideSaveAction = contextualType.GetAttribute<HideDefaultSaveActionAttribute>(false) is not null;
                if (hideSaveAction || actions.Count > 0)
                {
                    var actionList = new List<Dictionary<string, object?>>();
                    var actionsDict = new Dictionary<string, object?>
                    {
                        { "hideSaveAction", hideSaveAction },
                        { "customActions", actionList },
                    };
                    foreach (var action in actions)
                    {
                        var actionDict = new Dictionary<string, object?>
                        {
                            { "title", action.Name },
                            { "description", action.Description ?? string.Empty },
                            { "theme", JsonConvert.DeserializeObject<string?>(JsonConvert.SerializeObject(action.Theme, Formatting.None, _newtonsoftJsonSerializerSettings)) },
                        };
                        if (action.HasToggle)
                        {
                            actionDict.Add("toggle", new Dictionary<string, object?>
                            {
                                { "path", action.ToggleWhenMemberIsSet },
                                { "value", Convert(_currentType!, action.ToggleWhenSetTo) },
                            });
                            actionDict.Add("hideByDefault", action.HideByDefault);
                        }
                        else
                        {
                            actionDict.Add("toggle", null);
                            actionDict.Add("hideByDefault", false);
                        }
                        actionDict.Add("disableIfNoChanges", action.DisableIfNoChanges);
                        actionList.Add(actionDict);
                    }
                    uiDict.Add("actions", actionsDict);
                }

                uiDict.Add("elementType", "section-container");
                if (contextualType.GetAttribute<SectionAttribute>(false) is { } sectionTypeAttribute)
                    uiDict.Add("sectionType", sectionTypeAttribute.SectionType.ToString().ToLower() switch { "fieldset" => "field-set", string def => def });
                else
                    uiDict.Add("sectionType", "field-set");
            }
        }
    }

    private static string GetPropertyKey(ContextualPropertyInfo info)
    {
        if (info.GetAttribute<JsonPropertyAttribute>(false) is { } jsonPropertyAttribute)
            return jsonPropertyAttribute.PropertyName ?? info.Name;
        if (info.GetAttribute<System.Text.Json.Serialization.JsonPropertyNameAttribute>(false) is { } jsonPropertyNameAttribute)
            return jsonPropertyNameAttribute.Name ?? info.Name;
        return info.Name;
    }

    #endregion

    #region Schema | ISchemaNameGenerator Implementation

    string ISchemaNameGenerator.Generate(Type type)
    {
        var contextualType = type.ToContextualType();
        if (contextualType.GetAttribute<DisplayAttribute>(false) is { } displayAttribute && !string.IsNullOrEmpty(displayAttribute.Name))
            return displayAttribute.Name;

        var name = TypeReflectionExtensions.GetDisplayName(type);
        foreach (var suffix in _configurationSuffixSet)
        {
            var endWith = $" {suffix}";
            if (name.EndsWith(endWith, StringComparison.OrdinalIgnoreCase))
                name = name[..^endWith.Length];
        }
        return name;
    }

    #endregion

    #endregion

    #region De-/Serialization

    public IConfiguration Deserialize(ConfigurationInfo info, string json)
    {
        try
        {
            return (IConfiguration)typeof(ConfigurationService)
                .GetMethod(nameof(DeserializeInternal), BindingFlags.NonPublic | BindingFlags.Instance)!
                .MakeGenericMethod(info.Type)
                .Invoke(this, [json])!;
        }
        catch (TargetInvocationException ex)
        {
            if (ex.InnerException is null)
                throw;
            throw ex.InnerException;
        }
    }

    private TConfig DeserializeInternal<TConfig>(string json) where TConfig : class, IConfiguration, new()
        => typeof(TConfig).IsAssignableTo(typeof(INewtonsoftJsonConfiguration))
            ? JsonConvert.DeserializeObject<TConfig>(json, _newtonsoftJsonSerializerSettings)!
            : System.Text.Json.JsonSerializer.Deserialize<TConfig>(json, _systemTextJsonSerializerOptions)!;

    public string Serialize(IConfiguration config)
    {
        try
        {
            return (string)typeof(ConfigurationService)
                .GetMethod(nameof(SerializeInternal), BindingFlags.NonPublic | BindingFlags.Instance)!
                .MakeGenericMethod(config.GetType())
                .Invoke(this, [config])!;
        }
        catch (TargetInvocationException ex)
        {
            if (ex.InnerException is null)
                throw;
            throw ex.InnerException;
        }
    }

    private string SerializeInternal<TConfig>(TConfig config) where TConfig : class, IConfiguration, new()
        => typeof(TConfig).IsAssignableTo(typeof(INewtonsoftJsonConfiguration))
            ? JsonConvert.SerializeObject(config, _newtonsoftJsonSerializerSettings)
            : System.Text.Json.JsonSerializer.Serialize(config, _systemTextJsonSerializerOptions)!;

    // For the values that needs to be converted by the right library in the right way
    private JToken? Convert(Type type, object? value)
        => value is null ? null : type.IsAssignableTo(typeof(INewtonsoftJsonConfiguration))
            ? JToken.Parse(JsonConvert.SerializeObject(value, Formatting.None, _newtonsoftJsonSerializerSettings))
            : JToken.Parse(System.Text.Json.JsonSerializer.Serialize(value, _systemTextJsonSerializerOptions));

    #endregion

    #region ID Helpers

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

    #endregion
}
