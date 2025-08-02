using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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

public partial class ConfigurationService : IConfigurationService
{
    private readonly ILogger<ConfigurationService> _logger;

    private readonly IApplicationPaths _applicationPaths;

    private readonly IPluginManager _pluginManager;

    private readonly JsonSerializerSettings _newtonsoftJsonSerializerSettings;

    private readonly System.Text.Json.JsonSerializerOptions _systemTextJsonSerializerOptions;

    private readonly ShokoJsonSchemaGenerator _jsonSchemaGenerator;

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
        _jsonSchemaGenerator = new(_newtonsoftJsonSerializerSettings, _systemTextJsonSerializerOptions);

        var serverSettingsDefinition = new ServerSettingsDefinition(loggerFactory.CreateLogger<ServerSettingsDefinition>(), new(this));
        var serverSettingsSchema = _jsonSchemaGenerator.GetSchemaForType(typeof(ServerSettings));
        _configurationTypes[Guid.Empty] = new(this)
        {
            // We first map the server settings to the empty guid because the id
            // namespace depends on the plugin info which is not available yet.
            ID = Guid.Empty,
            Path = Path.Join(_applicationPaths.DataPath, serverSettingsDefinition.RelativePath),
            Name = serverSettingsSchema.Title!,
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

            var pluginInfo = Loader.GetTypes<IPlugin>(configurationType.Assembly).Aggregate((PluginInfo?)null, (p, t) => p ?? _pluginManager.GetPluginInfo(t))!;
            var id = GetID(configurationType, pluginInfo);
            var definition = configurationDefinitionDict.GetValueOrDefault(id);
            var contextualType = configurationType.ToContextualType();
            var schema = _jsonSchemaGenerator.GetSchemaForType(configurationType);
            var description = TypeReflectionExtensions.GetDescription(contextualType);
            var name = schema.Title!;
            string? path = null;
            if (definition is IConfigurationDefinitionWithCustomSaveLocation { } p0)
            {
                var relativePath = p0.RelativePath;
                if (!relativePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                    relativePath += ".json";
                path = Path.Join(_applicationPaths.DataPath, relativePath);
            }
            else if (definition is IConfigurationDefinitionWithCustomSaveName { } p1)
            {
                // If name is empty or null, then treat it as an in-memory configuration.
                if (!string.IsNullOrEmpty(p1.Name))
                {
                    var fileName = p1.Name;
                    if (!fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                        fileName += ".json";
                    path = Path.Join(_applicationPaths.ConfigurationsPath, pluginInfo.ID.ToString(), fileName);
                }
            }
            else
            {
                var fileName = name
                    .RemoveInvalidPathCharacters()
                    .Replace(' ', '-')
                    .ToLower();
                path = Path.Join(_applicationPaths.ConfigurationsPath, pluginInfo.ID.ToString(), fileName + ".json");
            }

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
            json = token.ToJson();

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

    [GeneratedRegex(@"(?<=\w|\]|^)\[", RegexOptions.Compiled | RegexOptions.ECMAScript)]
    private static partial Regex IndexNotationFixRegex();

    private static ConfigurationActionResult PerformActionInternal<TConfig>(ConfigurationInfo info, TConfig configuration, string path, string action, IShokoUser? user) where TConfig : class, IConfiguration, new()
    {
        var schema = info.Schema;
        var type = info.ContextualType;
        var parts = path.Replace(IndexNotationFixRegex(), ".[").Split(SplitPathToPartsRegex(), StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
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
            config = DeserializeInternal<TConfig>(token.ToJson());
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

            config = DeserializeInternal<TConfig>(token.ToJson());
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

        storedJson = token.ToJson();
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

    #endregion

    #region ID Helpers

    /// <summary>
    /// Gets a unique ID for a configuration generated from its class name.
    /// </summary>
    /// <param name="provider">The provider.</param>
    /// <returns><see cref="Guid" />.</returns>
    private Guid GetID(IConfigurationDefinition provider) => GetID(provider.ConfigurationType);

    private Guid GetID(Type type)
        => _loaded && Loader.GetTypes<IPlugin>(type.Assembly).Aggregate((PluginInfo?)null, (p, t) => p ?? _pluginManager.GetPluginInfo(t)) is { } pluginInfo
            ? GetID(type, pluginInfo)
            : Guid.Empty;

    private static Guid GetID(Type type, PluginInfo pluginInfo)
        => UuidUtility.GetV5($"Configuration={type.FullName!}", pluginInfo.ID);

    #endregion
}
