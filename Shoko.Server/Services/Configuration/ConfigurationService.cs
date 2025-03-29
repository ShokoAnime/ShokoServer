using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
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
using Shoko.Plugin.Abstractions.Config.Enums;
using Shoko.Plugin.Abstractions.Config.Exceptions;
using Shoko.Plugin.Abstractions.DataModels.Shoko;
using Shoko.Plugin.Abstractions.Events;
using Shoko.Plugin.Abstractions.Plugin;
using Shoko.Plugin.Abstractions.Services;
using Shoko.Server.Extensions;
using Shoko.Server.Plugin;
using Shoko.Server.Settings;

#nullable enable
namespace Shoko.Server.Services.Configuration;

public partial class ConfigurationService : IConfigurationService, ISchemaProcessor, ISchemaNameGenerator
{
    private readonly ILogger<ConfigurationService> _logger;

    private readonly IApplicationPaths _applicationPaths;

    private readonly IPluginManager _pluginManager;

    private readonly JsonSerializerSettings _newtonsoftJsonSerializerSettings;

    private readonly System.Text.Json.JsonSerializerOptions _systemTextJsonSerializerOptions;

    private readonly ConcurrentDictionary<Guid, ConfigurationInfo> _configurationTypes = [];

    private readonly ConcurrentDictionary<ConfigurationInfo, string> _serializedSchemas = [];

    private readonly ConcurrentDictionary<Guid, IConfiguration> _loadedConfigurations = [];

    private readonly ConcurrentDictionary<Guid, string> _savedMemoryConfigurations = [];

    private bool _loaded = false;

    internal readonly Dictionary<Guid, Dictionary<string, string?>> InternalRestartPendingFor = [];

    internal readonly Dictionary<Guid, Dictionary<string, (string Override, string? Original)>> InternalLoadedEnvironmentVariables = [];

    public IReadOnlyDictionary<Guid, IReadOnlySet<string>> RestartPendingFor => InternalRestartPendingFor.ToDictionary(a => a.Key, a => a.Value.Keys.ToHashSet() as IReadOnlySet<string>);

    public IReadOnlyDictionary<Guid, IReadOnlySet<string>> LoadedEnvironmentVariables => InternalLoadedEnvironmentVariables.ToDictionary(a => a.Key, a => a.Value.Keys.ToHashSet() as IReadOnlySet<string>);

    public event EventHandler<ConfigurationSavedEventArgs>? Saved;

    public event EventHandler<ConfigurationRequiresRestartEventArgs>? RequiresRestart;

    public ConfigurationService(ILoggerFactory loggerFactory, IApplicationPaths applicationPaths, IPluginManager pluginManager)
    {
        _logger = loggerFactory.CreateLogger<ConfigurationService>();
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
            PreferredObjectCreationHandling = JsonObjectCreationHandling.Replace,
            ReferenceHandler = ReferenceHandler.IgnoreCycles,
        };
        _systemTextJsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());

        var serverSettingsDefinition = new ServerSettingsDefinition(loggerFactory.CreateLogger<ServerSettingsDefinition>(), new(this));
        var serverSettingsName = GetDisplayName(typeof(ServerSettings).ToContextualType());
        var serverSettingsSchema = GetSchemaForType(typeof(ServerSettings));
        _configurationTypes[Guid.Empty] = new(this)
        {
            // We first map the server settings to the empty guid because the id
            // namespace depends on the plugin info which is not available yet.
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
        };
    }

    #region Configuration Info

    private static readonly HashSet<string> _configurationSuffixSet = ["Setting", "Conf", "Config", "Configuration"];

    public void AddParts(IEnumerable<Type> configurationTypes, IEnumerable<Type> configurationDefinitions)
    {
        if (_loaded) return;
        _loaded = true;

        ArgumentNullException.ThrowIfNull(configurationTypes);
        ArgumentNullException.ThrowIfNull(configurationDefinitions);

        _logger.LogInformation("Initializing service.");

        // Set the server settings to the proper ID before trying to enumerate the config type and definitions.
        var serverSettingsID = GetID(typeof(ServerSettings));
        var serverSettingsInfo = _configurationTypes[Guid.Empty];
        _configurationTypes[serverSettingsID] = new(this)
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
        };
        _configurationTypes.TryRemove(Guid.Empty, out _);
        _serializedSchemas[_configurationTypes[serverSettingsID]] = _serializedSchemas[serverSettingsInfo];
        _serializedSchemas.TryRemove(serverSettingsInfo, out _);
        _loadedConfigurations[serverSettingsID] = _loadedConfigurations[Guid.Empty];
        _loadedConfigurations.TryRemove(Guid.Empty, out _);
        if (InternalLoadedEnvironmentVariables.TryGetValue(Guid.Empty, out var envVarDict))
        {
            InternalLoadedEnvironmentVariables[serverSettingsID] = envVarDict;
            InternalLoadedEnvironmentVariables.Remove(Guid.Empty, out _);
        }

        var configurationDefinitionDict = configurationDefinitions
            .Except([typeof(ServerSettingsDefinition)])
            .Select(Loader.CreateInstance<IConfigurationDefinition>)
            .WhereNotNull()
            .ToDictionary(GetID);
        foreach (var configurationType in configurationTypes)
        {
            if (configurationType == typeof(ServerSettings))
                continue;

            var pluginInfo = _pluginManager.GetPluginInfo(Loader.GetTypes<IPlugin>(configurationType.Assembly).First(t => _pluginManager.GetPluginInfo(t) is not null))!;
            var id = GetID(configurationType, pluginInfo);
            var definition = configurationDefinitionDict.GetValueOrDefault(id);
            var contextualType = configurationType.ToContextualType();
            var description = TypeReflectionExtensions.GetDescription(contextualType);
            var name = GetDisplayName(contextualType);
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
            _configurationTypes[id] = new(this)
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
        }

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

    public ConfigurationInfo GetConfigurationInfo(Type type)
        => GetConfigurationInfo(GetID(type))!;

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
            var (_, errors) = ((JToken, Dictionary<string, IReadOnlyList<string>>))typeof(ConfigurationService)
                .GetMethod(nameof(ValidateInternal), BindingFlags.NonPublic | BindingFlags.Instance)!
                .MakeGenericMethod(info.Type)
                .Invoke(this, [info, json, null, false, false])!;
            return errors;
        }
        catch (TargetInvocationException ex)
        {
            if (ex.InnerException is null)
                throw;
            throw ex.InnerException;
        }
    }

    public IReadOnlyDictionary<string, IReadOnlyList<string>> Validate<TConfig>(TConfig config) where TConfig : class, IConfiguration, new()
        => ValidateInternal(GetConfigurationInfo<TConfig>(), SerializeInternal(config), config).Errors;

    private (JToken Token, Dictionary<string, IReadOnlyList<string>> Errors) ValidateInternal<TConfig>(ConfigurationInfo info, string json, TConfig? config, bool saveValidation = false, bool loadValidation = false) where TConfig : class, IConfiguration, new()
    {
        var (token, results) = new ShokoJsonSchemaValidator<TConfig>(this._logger, this, info, _loadedConfigurations.TryGetValue(info.ID, out var config0) ? (config0 as TConfig) ?? config : config, saveValidation, loadValidation).Validate(json);
        if (loadValidation)
            json = token.ToString();

        var errorDict = new Dictionary<string, List<string>>();
        foreach (var error in GetAllValidationErrorsForCollection(results))
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

        return (token, errorDict.Select(a => KeyValuePair.Create(a.Key, (IReadOnlyList<string>)a.Value)).ToDictionary());
    }

    private static IEnumerable<ValidationError> GetAllValidationErrorsForCollection(ICollection<ValidationError> errors)
    {
        foreach (var error in errors)
        {

            switch (error)
            {
                case MultiTypeValidationError multiTypeValidationError:
                {
                    yield return error;
                    foreach (var (type, collection) in multiTypeValidationError.Errors)
                        foreach (var subError in GetAllValidationErrorsForCollection(collection))
                            yield return subError;
                    break;
                }

                case ChildSchemaValidationError childSchemaValidationError:
                {
                    // Only emit the error if it's not 'not one of' and the inner error is not a 'null expected'.
                    var childErrors = childSchemaValidationError.Errors.ToDictionary();
                    if (childSchemaValidationError.Kind is ValidationErrorKind.NotOneOf && childErrors.Count > 1 && childErrors.FirstOrDefault(kp => kp.Value.Count == 1 && kp.Value.First().Kind is ValidationErrorKind.NullExpected).Key is { } errorSchema)
                        childErrors.Remove(errorSchema);
                    else
                        yield return error;
                    foreach (var (schema, collection) in childErrors)
                        foreach (var subError in GetAllValidationErrorsForCollection(collection))
                            yield return subError;
                    break;
                }

                default:
                {
                    yield return error;
                    break;
                }
            }
        }
    }

    private static string GetErrorMessage(ValidationError validationError)
    {
        return validationError.Kind switch
        {
            (ValidationErrorKind)1_003 => "Unable to load environment variables multiple times for the same configuration.",
            (ValidationErrorKind)1_002 => "Failed to parse environment variable.",
            (ValidationErrorKind)1_001 => "Unable to set value when an environment variable override is in use.",
            _ => TypeReflectionExtensions.GetDisplayName(validationError.Kind.ToString()),
        };
    }

    #endregion

    #region Custom Actions

    public ConfigurationActionResult PerformAction(ConfigurationInfo info, IConfiguration configuration, string path, string action, IShokoUser? user = null)
    {
        try
        {
            return (ConfigurationActionResult)typeof(ConfigurationService)
                .GetMethod(nameof(PerformActionInternal), BindingFlags.NonPublic | BindingFlags.Static)!
                .MakeGenericMethod(configuration.GetType())
                .Invoke(this, [info, configuration, path, action, user])!;
        }
        catch (TargetInvocationException ex)
        {
            if (ex.InnerException is null)
                throw;
            throw ex.InnerException;
        }
    }

    public ConfigurationActionResult PerformAction<TConfig>(TConfig configuration, string path, string action, IShokoUser? user = null) where TConfig : class, IConfiguration, new()
        => PerformActionInternal(GetConfigurationInfo<TConfig>(), configuration, path, action, user);

    [GeneratedRegex(@"(?<!\\)\.")]
    private static partial Regex SplitPathToPartsRegex();

    [GeneratedRegex(@"(?<!\\)""")]
    private static partial Regex InvalidQuoteRegex();

    private static ConfigurationActionResult PerformActionInternal<TConfig>(ConfigurationInfo info, TConfig configuration, string path, string action, IShokoUser? user) where TConfig : class, IConfiguration, new()
    {
        var schema = info.Schema;
        var type = info.ContextualType;

        // TODO: FIX UP THIS SH*T to support parsing "A.B[0]['D.E'].F['G\'H'].I" etc., where we check each defined property for the schema
        // and if it's not a known property, check patterns and additional properties.

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
                if (part[^1] != ']' || part.Length < 3 || part[^2] == '\\')
                    throw new InvalidConfigurationActionException($"Invalid path \"{path}\"", nameof(path));

                // Dictionary
                if (part[1] == '"')
                {
                    // Make sure the dictionary key is a valid string
                    if (part[^2] != '"' || path.Length < 5 || path[^3] == '\\' || InvalidQuoteRegex().IsMatch(part[2..^2]))
                        throw new InvalidConfigurationActionException($"Invalid path \"{path}\"", nameof(path));

                    if (schema.AdditionalItemsSchema is null || !(type.IsAssignableToTypeName("IReadOnlyDictionary", TypeNameStyle.Name) || type.IsAssignableToTypeName("IDictionary", TypeNameStyle.Name)))
                        throw new InvalidConfigurationActionException($"Invalid path \"{path}\"", nameof(path));

                    schema = schema.AdditionalItemsSchema.Reference is { } referencedSchema2
                        ? referencedSchema2 : schema.AdditionalItemsSchema;
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
            return provider.PerformAction(configuration, path, action, type, user);
        return new("Configuration does not support custom actions!", DisplayColorTheme.Warning) { RefreshConfiguration = false };
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
            var json = SerializeInternal(config);
            SaveInternal(info, json, config);
            var (token, errors) = ValidateInternal(info, json, config, loadValidation: true);
            config = DeserializeInternal<TConfig>(token.ToString());
            _loadedConfigurations[info.ID] = config;
            if (errors.Count > 0)
                throw new ConfigurationValidationException("load", info, errors);
            return copy ? config.DeepClone() : config;
        }

        lock (info)
        {
            var json = File.ReadAllText(info.Path);
            if (info.Definition is IConfigurationDefinitionWithMigrations { } provider)
                json = provider.ApplyMigrations(json);

            EnsureSchemaExists(info);

            var (token, errors) = ValidateInternal<TConfig>(info, json, null, loadValidation: true);
            if (errors.Count > 0)
                throw new ConfigurationValidationException("load", info, errors);

            config = DeserializeInternal<TConfig>(token.ToString());
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

    private bool SaveInternal<TConfig>(ConfigurationInfo info, string originalJson, TConfig? config = null) where TConfig : class, IConfiguration, new()
    {
        var storedJson = AddSchemaProperty(info, originalJson);
        var pendingRestart = InternalRestartPendingFor.Count > 0;
        var (token, errors) = ValidateInternal(info, storedJson, config, saveValidation: true);
        if (errors.Count > 0)
            throw new ConfigurationValidationException("save", info, errors);

        storedJson = token.ToString();
        lock (info)
        {
            if (info.Path is null)
            {
                if (_savedMemoryConfigurations.TryGetValue(info.ID, out var oldJson) && oldJson is not null && oldJson.Equals(storedJson, StringComparison.Ordinal))
                {
                    _logger.LogTrace("In-memory configuration for {Name} is unchanged. Skipping save.", info.Name);
                    return false;
                }

                _logger.LogTrace("Saving in-memory configuration for {Name}.", info.Name);
                _savedMemoryConfigurations[info.ID] = storedJson;
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
                    if (oldJson.Equals(storedJson, StringComparison.Ordinal))
                    {
                        _logger.LogTrace("Configuration for {Name} is unchanged. Skipping save.", info.Name);
                        return false;
                    }
                }

                _logger.LogTrace("Saving configuration for {Name}.", info.Name);
                File.WriteAllText(info.Path, storedJson);
                _logger.LogTrace("Saved configuration for {Name}.", info.Name);
            }

            // We deserialize the original JSON for the UI, but store the serialized version that
            // may have been modified by the schema validation to prevent writing the environment
            // variables overrides to disk.
            _loadedConfigurations[info.ID] = config ?? DeserializeInternal<TConfig>(originalJson);
        }

        Task.Run(() => Saved?.Invoke(this, new ConfigurationSavedEventArgs() { ConfigurationInfo = info }));

        var needsRestart = InternalRestartPendingFor.Count > 0;
        if (needsRestart != pendingRestart)
        {
            if (needsRestart)
                _logger.LogInformation("A restart is required for some some configuration to take effect.");
            else
                _logger.LogInformation("A restart is no longer required for some configuration to take effect.");
            Task.Run(() => RequiresRestart?.Invoke(this, new() { RequiresRestart = needsRestart }));
        }

        return true;
    }

    #endregion

    #region Schema

    public string GetSchema(ConfigurationInfo info)
    {
        if (_serializedSchemas.TryGetValue(info, out var schemaJson))
            return schemaJson;

        lock (info)
        {
            if (_serializedSchemas.TryGetValue(info, out schemaJson))
                return schemaJson;

            schemaJson = AddSchemaPropertyToSchema(info.Schema.ToJson());
            _serializedSchemas[info] = schemaJson;
            return schemaJson;
        }
    }

    private void EnsureSchemaExists(ConfigurationInfo info)
    {
        if (info.Path is null)
            return;

        var schemaJson = GetSchema(info);
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

    private string AddSchemaProperty(ConfigurationInfo info, string json)
    {
        if (json.Contains("$schema") || info.Path is null)
            return json;

        var newLine = Environment.NewLine;
        if (json[0] == '{')
        {
            var baseUri = JsonConvert.SerializeObject($"file://{Path.ChangeExtension(info.Path, ".schema.json")}", _newtonsoftJsonSerializerSettings);
            if (json[1..(1 + newLine.Length)] == newLine)
            {
                var nextIndex = 1 + newLine.Length;
                if (json[nextIndex] == ' ')
                {
                    var spaceLength = 0;
                    for (var i = nextIndex; i < json.Length; i++)
                    {
                        if (json[i] != ' ')
                            break;
                        spaceLength++;
                    }
                    return "{" + newLine + new string(' ', spaceLength) + "\"$schema\": " + baseUri + "," + json[1..];
                }

                if (json[nextIndex] == '\t')
                {
                    var tabLength = 0;
                    for (var i = nextIndex; i < json.Length; i++)
                    {
                        if (json[i] != '\t')
                            break;
                        tabLength++;
                    }
                    return "{" + newLine + new string('\t', tabLength) + "\"$schema\": " + baseUri + "," + json[1..];
                }
            }

            return "{\"$schema\":" + baseUri + "," + json[1..];
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
        var isNewtonsoftJson = type.IsAssignableTo(typeof(INewtonsoftJsonConfiguration));
        var generator = isNewtonsoftJson
            ? GetNewtonsoftSchemaForType()
            : GetSystemTextJsonSchemaForType();
        generator.Settings.SchemaProcessors.Add(this);

        // Handle built-in types NJsonSchema doesn't.
        generator.Settings.TypeMappers.Add(new PrimitiveTypeMapper(typeof(Version), s =>
        {
            s.Type = JsonObjectType.String;
            s.Format = "version";
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
                if (classDefinition.TryGetValue(ElementLabel, out var classLabel))
                {
                    classDefinition.Remove(ElementLabel);
                    subSchema.Title = (string)classLabel!;
                }
                subSchema.ExtensionData ??= new Dictionary<string, object?>();
                subSchema.ExtensionData.Add(UiDefinition, classDefinition);
            }
            foreach (var tuple in subSchema.Properties)
            {
                var (propertyKey, schemaValue) = tuple;
                if (schemaValue.Item is not null)
                    propertyKey += "+List";
                if (schemaValue.AdditionalPropertiesSchema is not null)
                    propertyKey += "+Dict";
                if (!propertyDict.TryGetValue(propertyKey, out var propertyDefinition))
                    continue;

                if (propertyDefinition.TryGetValue(ElementLabel, out var propertyLabel
                ))
                {
                    propertyDefinition.Remove(ElementLabel);
                    schemaValue.Title = (string)propertyLabel!;
                }
                schemaValue.ExtensionData ??= new Dictionary<string, object?>();
                schemaValue.ExtensionData.Add(UiDefinition, propertyDefinition);
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

    #region Schema | Constants

    internal const string UiDefinition = "x-uiDefinition";

    private const string ElementType = "elementType";

    private const string ElementSize = "elementSize";

    private const string ElementLabel = "label";

    private const string ElementActions = "actions";

    private const string ElementVisibility = "visibility";

    private const string ElementBadge = "badge";

    private const string ElementPrimaryKey = "primaryKey";

    internal const string ElementEnvironmentVariable = "envVar";

    internal const string ElementEnvironmentVariableOverridable = "envVarOverridable";

    internal const string ElementRequiresRestart = "requiresRestart";

    private const string SectionType = "sectionType";

    private const string SectionName = "sectionName";

    private const string SectionAppendFloatingAtEnd = "sectionAppendFloatingAtEnd";

    private const string ListType = "listType";

    private const string ListSortable = "listSortable";

    private const string ListUniqueItems = "listUniqueItems";

    private const string ListHideAddAction = "listHideAddAction";

    private const string ListHideRemoveAction = "listHideRemoveAction";

    private const string ListElementType = "listElementType";

    private const string RecordElementType = "recordElementType";

    private const string RecordType = "recordType";

    private const string RecordSortable = "recordSortable";

    private const string RecordHideAddAction = "recordHideAddAction";

    private const string RecordHideRemoveAction = "recordHideRemoveAction";

    private const string CodeBlockLanguage = "codeLanguage";

    private const string CodeBlockAutoFormatOnLoad = "codeAutoFormatOnLoad";

    private const string EnumDefinitions = "enumDefinitions";

    private const string EnumIsFlag = "enumIsFlag";

    #endregion

    #region Schema | ISchemaProcessor

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
            if (schema.AdditionalPropertiesSchema is not null)
                propertyKey += "+Dict";
            if (!_schemaCache[schemaKey].PropertyUIDefinitions.TryAdd(propertyKey, uiDict))
                uiDict = _schemaCache[schemaKey].PropertyUIDefinitions[propertyKey];

            uiDict.TryAdd(ElementType, Convert(DisplayElementType.Auto));
            uiDict.TryAdd(ElementSize, Convert(DisplayElementSize.Normal));
            if (info.GetAttribute<KeyAttribute>(false) is { })
                uiDict.Add(ElementPrimaryKey, true);

            if (info.GetAttribute<DisplayAttribute>(false) is { } displayAttribute && !string.IsNullOrWhiteSpace(displayAttribute.Name))
                uiDict.TryAdd(ElementLabel, displayAttribute.Name);
            else
                uiDict.TryAdd(ElementLabel, TypeReflectionExtensions.GetDisplayName(info.Name));

            if (info.GetAttribute<SectionNameAttribute>(false) is { } sectionNameAttribute)
                uiDict.Add(SectionName, sectionNameAttribute.Name);

            if (info.GetAttribute<VisibilityAttribute>(false) is { } visibilityAttribute)
            {
                var visibilityDict = new Dictionary<string, object?>()
                {
                    { "default", Convert(visibilityAttribute.Visibility) },
                    { "advanced", visibilityAttribute.Advanced },
                };
                if (visibilityAttribute.HasToggle)
                {
                    var toggleDict = new Dictionary<string, object?>()
                    {
                        { "path", visibilityAttribute.ToggleWhenMemberIsSet },
                        { "value", Convert(visibilityAttribute.ToggleWhenSetTo, _currentType!) },
                        { "visibility", Convert(visibilityAttribute.ToggleVisibilityTo) },
                    };
                    visibilityDict.Add("toggle", toggleDict);
                }
                uiDict.Add(ElementVisibility, visibilityDict);
                uiDict[ElementSize] = Convert(visibilityAttribute.Size);
            }

            if (info.GetAttribute<RequiresRestartAttribute>(false) is { })
            {
                uiDict.Add(ElementRequiresRestart, true);
            }
            else
            {
                uiDict.Add(ElementRequiresRestart, false);
            }

            if (info.GetAttribute<EnvironmentVariableAttribute>(false) is { } environmentVariableAttribute && !string.IsNullOrWhiteSpace(environmentVariableAttribute.Name))
            {
                uiDict.Add(ElementEnvironmentVariable, environmentVariableAttribute.Name);
                uiDict.Add(ElementEnvironmentVariableOverridable, environmentVariableAttribute.AllowOverride);
            }

            if (info.GetAttribute<BadgeAttribute>(false) is { } badgeAttribute && !string.IsNullOrWhiteSpace(badgeAttribute.Name))
            {
                var badgeDict = new Dictionary<string, object?>
                {
                    { "name", badgeAttribute.Name },
                    { "theme", Convert(badgeAttribute.Theme) },
                };
                uiDict.Add(ElementBadge, badgeDict);
            }

            if (schema.Item is { } itemSchema)
            {
                uiDict[ElementType] = Convert(DisplayElementType.List);
                uiDict.Add(ListElementType, Convert(DisplayElementSize.Normal));
                if (info.GetAttribute<ListAttribute>(false) is { } listAttribute)
                {
                    uiDict.Add(ListType, Convert(listAttribute.ListType));
                    uiDict.Add(ListSortable, listAttribute.Sortable);
                    uiDict.Add(ListUniqueItems, listAttribute.UniqueItems);
                    uiDict.Add(ListHideAddAction, listAttribute.HideAddAction);
                    uiDict.Add(ListHideRemoveAction, listAttribute.HideRemoveAction);
                }
                else
                {
                    uiDict.Add(ListType, Convert(DisplayListType.Auto));
                    uiDict.Add(ListSortable, true);
                    uiDict.Add(ListUniqueItems, false);
                    uiDict.Add(ListHideAddAction, false);
                    uiDict.Add(ListHideRemoveAction, false);
                }

                // Only set if the referenced schema is a class definition
                if (itemSchema.HasReference && _schemaKeys.TryGetValue(itemSchema.ActualSchema, out var referencedSchemaKey))
                {
                    var referencedDict = _schemaCache[referencedSchemaKey].ClassUIDefinition;
                    foreach (var (key, value) in referencedDict)
                    {
                        if (key is ElementType && !Equals(value, Convert(DisplayElementType.Auto)))
                            uiDict[ListElementType] = value;
                        else if (key is SectionType or ElementPrimaryKey)
                            uiDict.TryAdd(key, value);
                    }
                }

                var innerDict = _schemaCache[schemaKey].PropertyUIDefinitions[propertyKey[..^5]];
                foreach (var (key, value) in innerDict)
                {
                    if (key is ElementType && !Equals(value, Convert(DisplayElementType.Auto)))
                        uiDict[ListElementType] = value;
                    else if (key is not ElementType)
                        uiDict.TryAdd(key, value);
                }

                if (Equals(uiDict[ListType], Convert(DisplayListType.ComplexDropdown)))
                {
                    if (!Equals(uiDict[ListElementType], Convert(DisplayElementType.SectionContainer)))
                        throw new NotSupportedException("Dropdown lists are not supported for non-class list items.");
                    if (!uiDict.ContainsKey(ElementPrimaryKey))
                        throw new NotSupportedException("Dropdown lists must have a primary key set.");
                }
                if (Equals(uiDict[ListType], Convert(DisplayListType.ComplexTab)))
                {
                    if (!Equals(uiDict[ListElementType], Convert(DisplayElementType.SectionContainer)))
                        throw new NotSupportedException("Tab lists are not supported for non-class list items.");
                    if (!uiDict.ContainsKey(ElementPrimaryKey))
                        throw new NotSupportedException("Tab lists must have a primary key set.");
                }
                else if (Equals(uiDict[ListType], Convert(DisplayListType.EnumCheckbox)))
                {
                    if (!Equals(uiDict[ListElementType], Convert(DisplayElementType.Enum)))
                        throw new NotSupportedException("Checkbox lists are not supported for non-enum list items.");
                }
            }
            else if (schema.AdditionalPropertiesSchema is { } recordSchema)
            {
                var (keyType, valueType) = GetTKeyAndTValue(info.PropertyType.Type);
                AssertKeyUsable(keyType);

                uiDict[ElementType] = Convert(DisplayElementType.Record);
                uiDict.Add(RecordElementType, Convert(DisplayElementSize.Normal));
                if (info.GetAttribute<RecordAttribute>(false) is { } recordAttribute)
                {
                    uiDict.Add(RecordType, Convert(recordAttribute.RecordType));
                    uiDict.Add(RecordSortable, recordAttribute.Sortable);
                    uiDict.Add(RecordHideAddAction, recordAttribute.HideAddAction);
                    uiDict.Add(RecordHideRemoveAction, recordAttribute.HideRemoveAction);
                }
                else
                {
                    uiDict.Add(RecordType, Convert(DisplayRecordType.Auto));
                    uiDict.Add(RecordSortable, true);
                    uiDict.Add(RecordHideAddAction, false);
                    uiDict.Add(RecordHideRemoveAction, false);
                }

                // Only set if the referenced schema is a class definition
                if (recordSchema.HasReference && _schemaKeys.TryGetValue(recordSchema.ActualSchema, out var referencedSchemaKey))
                {
                    var referencedDict = _schemaCache[referencedSchemaKey].ClassUIDefinition;
                    foreach (var (key, value) in referencedDict)
                    {
                        if (key is ElementType && !Equals(value, Convert(DisplayElementType.Auto)))
                            uiDict[RecordElementType] = value;
                        else if (key is SectionType or ElementPrimaryKey)
                            uiDict.TryAdd(key, value);
                    }
                }

                var innerDict = _schemaCache[schemaKey].PropertyUIDefinitions[propertyKey[..^5]];
                foreach (var (key, value) in innerDict)
                {
                    if (key is ElementType && !Equals(value, Convert(DisplayElementType.Auto)))
                        uiDict[RecordElementType] = value;
                    else if (key is not ElementType and not ElementSize)
                        uiDict.TryAdd(key, value);
                }
            }
            else if (schema.IsEnumeration)
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
                uiDict[ElementType] = Convert(DisplayElementType.Enum);
                uiDict.Add(EnumDefinitions, enumList);
                uiDict.Add(EnumIsFlag, schema.IsFlagEnumerable);
            }
            else if (info.GetAttribute<CodeEditorAttribute>(false) is { } codeBlockAttribute)
            {
                uiDict[ElementType] = Convert(DisplayElementType.CodeBlock);
                uiDict[CodeBlockLanguage] = Convert(codeBlockAttribute.Language);
                uiDict[CodeBlockAutoFormatOnLoad] = codeBlockAttribute.AutoFormatOnLoad;
            }
            else if (info.GetAttribute<TextAreaAttribute>(false) is not null)
            {
                uiDict[ElementType] = Convert(DisplayElementType.TextArea);
            }
            else if (info.GetAttribute<PasswordPropertyTextAttribute>(false) is not null)
            {
                uiDict[ElementType] = Convert(DisplayElementType.Password);
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
                uiDict.Add(ElementType, Convert(DisplayElementType.SectionContainer));
                if (contextualType.GetAttribute<DisplayAttribute>(false) is { } displayAttribute && !string.IsNullOrWhiteSpace(displayAttribute.Name))
                    uiDict.Add(ElementLabel, displayAttribute.Name);

                if (contextualType.GetAttribute<SectionAttribute>(false) is { } sectionTypeAttribute)
                {
                    uiDict.Add(SectionType, Convert(sectionTypeAttribute.SectionType));
                    if (!string.IsNullOrWhiteSpace(sectionTypeAttribute.DefaultSectionName))
                        uiDict.Add(SectionName, sectionTypeAttribute.DefaultSectionName);
                    uiDict.Add(SectionAppendFloatingAtEnd, sectionTypeAttribute.AppendFloatingSectionsAtEnd);
                }
                else
                {
                    uiDict.Add(SectionType, Convert(DisplaySectionType.FieldSet));
                }

                var propertyDefinitions = _schemaCache[schemaKey].PropertyUIDefinitions;
                var primaryKey = propertyDefinitions.FirstOrDefault(x => x.Value.ContainsKey(ElementPrimaryKey) && x.Value[ElementPrimaryKey] is true).Key;
                if (!string.IsNullOrEmpty(primaryKey))
                    uiDict.Add(ElementPrimaryKey, primaryKey);

                var actions = contextualType.GetAttributes<CustomActionAttribute>(false).ToList();
                var hideSaveAction = contextualType.GetAttribute<HideDefaultSaveActionAttribute>(false) is not null;
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
                        { "theme", Convert(action.Theme) },
                        { "position", Convert(action.Position) },
                    };
                    if (!string.IsNullOrEmpty(action.SectionName))
                        actionDict.Add(SectionName, action.SectionName);
                    if (action.HasToggle)
                    {
                        actionDict.Add("toggle", new Dictionary<string, object?>
                        {
                            { "path", action.ToggleWhenMemberIsSet },
                            { "value", Convert(action.ToggleWhenSetTo, _currentType!) },
                        });
                        actionDict.Add("inverseToggle", action.InverseToggle);
                    }
                    else
                    {
                        actionDict.Add("toggle", null);
                        actionDict.Add("inverseToggle", false);
                    }
                    actionDict.Add("disableIfNoChanges", action.DisableIfNoChanges);
                    actionList.Add(actionDict);
                }
                uiDict.Add(ElementActions, actionsDict);
            }
        }
    }

    private bool IsNewtonsoftJson() => _currentType!.IsAssignableTo(typeof(INewtonsoftJsonConfiguration));

    private static (bool isDictionary, bool isReadonlyDictionary) IsDictionary(Type type)
    {
        var interfaces = type.GetInterfaces();
        var isExtendingReadonlyDictionary = (type.IsGenericType && type.IsInterface && type.GetGenericTypeDefinition() == typeof(IReadOnlyDictionary<,>)) || interfaces.Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IReadOnlyDictionary<,>));
        var isExtendingWritableDictionary = (type.IsGenericType && type.IsInterface && type.GetGenericTypeDefinition() == typeof(IDictionary<,>)) || interfaces.Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDictionary<,>));
        return (isExtendingReadonlyDictionary || isExtendingWritableDictionary, isExtendingReadonlyDictionary);
    }

    private static (Type KeyType, Type ValueType) GetTKeyAndTValue(Type type)
    {
        Type[] arguments;
        var (isQualified, isReadonlyDictionary) = IsDictionary(type);
        if (!isQualified)
            throw new InvalidOperationException($"Type {type.Name} does not implement IReadOnlyDictionary<,> or IDictionary<,>.");

        if (!isReadonlyDictionary)
        {
            if (type.IsGenericType && type.IsInterface && type.GetGenericTypeDefinition() == typeof(IDictionary<,>))
                arguments = type.GetGenericArguments();
            else
                arguments = type.GetInterfaces().First(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDictionary<,>)).GetGenericArguments();
        }
        else
        {
            if (type.IsGenericType && type.IsInterface && type.GetGenericTypeDefinition() == typeof(IReadOnlyDictionary<,>))
                arguments = type.GetGenericArguments();
            else
                arguments = type.GetInterfaces().First(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IReadOnlyDictionary<,>)).GetGenericArguments();
        }

        return (arguments[0], arguments[1]);
    }

    private void AssertKeyUsable(Type keyType)
    {
        if (keyType == typeof(string) || keyType.GetTypeInfo().IsEnum)
            return;

        if (keyType.GetCustomAttribute<SerializableAttribute>() is not null)
            return;

        if (!IsNewtonsoftJson() && keyType.GetCustomAttribute<JsonSerializableAttribute>() is not null)
            return;

        var interfaces = keyType.GetInterfaces();
        if (interfaces.Any(i => i == typeof(ISerializable)))
            return;

        throw new ArgumentException($"Type \"{keyType.FullName!}\" is not serializable to text and therefore cannot be used as a key in a dictionary inside a configuration.", nameof(keyType));
    }

    private static string GetPropertyKey(ContextualPropertyInfo info)
    {
        if (info.GetAttribute<JsonPropertyAttribute>(false) is { } jsonPropertyAttribute)
            return jsonPropertyAttribute.PropertyName ?? info.Name;
        if (info.GetAttribute<JsonPropertyNameAttribute>(false) is { } jsonPropertyNameAttribute)
            return jsonPropertyNameAttribute.Name ?? info.Name;
        return info.Name;
    }

    #endregion

    #region Schema | ISchemaNameGenerator

    string ISchemaNameGenerator.Generate(Type type)
        => GetDisplayName(type.ToContextualType());

    private static string GetDisplayName(ContextualType contextualType)
    {
        if (contextualType.GetAttribute<DisplayAttribute>(false) is { } displayAttribute && !string.IsNullOrEmpty(displayAttribute.Name))
            return displayAttribute.Name;

        var name = TypeReflectionExtensions.GetDisplayName(contextualType);
        label:
        foreach (var suffix in _configurationSuffixSet)
        {
            if (name == suffix)
            {
                // I don't want to deal with generic types rn, so bail.
                if (contextualType.Type.IsGenericType)
                    break;

                name = contextualType.Type.FullName!.Split('.').SkipLast(1).Join('.');
                goto label;
            }

            var endsWith = $" {suffix}";
            if (name.EndsWith(endsWith, StringComparison.OrdinalIgnoreCase))
                name = name[..^endsWith.Length];
            if (name.EndsWith($"{endsWith}s", StringComparison.OrdinalIgnoreCase))
                name = name[..^endsWith.Length];
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
    private JToken? Convert(object? value, Type type)
        => value is null ? null : type is null || type.IsAssignableTo(typeof(INewtonsoftJsonConfiguration))
            ? JToken.Parse(JsonConvert.SerializeObject(value, Formatting.None, _newtonsoftJsonSerializerSettings))
            : JToken.Parse(System.Text.Json.JsonSerializer.Serialize(value, _systemTextJsonSerializerOptions));


    // For the values that needs to be converted by the right library in the right way
    private string Convert(object value)
        => JsonConvert.DeserializeObject<string>(JsonConvert.SerializeObject(value, Formatting.None, _newtonsoftJsonSerializerSettings))!;

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
