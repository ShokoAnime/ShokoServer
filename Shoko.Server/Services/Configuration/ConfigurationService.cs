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
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Config.Attributes;
using Shoko.Abstractions.Config.Enums;
using Shoko.Abstractions.Config.Exceptions;
using Shoko.Abstractions.Events;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.Services;
using Shoko.Abstractions.User;
using Shoko.Abstractions.Utilities;
using Shoko.Server.Extensions;
using Shoko.Server.Plugin;
using Shoko.Server.Settings;

#nullable enable
namespace Shoko.Server.Services.Configuration;

public partial class ConfigurationService : IConfigurationService
{
    private readonly ILogger<ConfigurationService> _logger;

    private readonly ILoggerFactory _loggerFactory;

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
        _loggerFactory = loggerFactory;
        _applicationPaths = applicationPaths;
        _pluginManager = pluginManager;
        _newtonsoftJsonSerializerSettings = new()
        {
            Formatting = Formatting.Indented,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
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

        var wrappedSchema = _jsonSchemaGenerator.GetSchemaForType(typeof(ServerSettings));
        wrappedSchema.Schema.Id = GetID(typeof(ServerSettings)).ToString();
        _configurationTypes[Guid.Empty] = new(this)
        {
            // We first map the server settings to the empty guid because the id
            // namespace depends on the plugin info which is not available yet.
            ID = Guid.Empty,
            Path = Path.Join(_applicationPaths.DataPath, typeof(ServerSettings).GetCustomAttribute<StorageLocationAttribute>()!.RelativePath),
            Name = wrappedSchema.Schema.Title!,
            Description = string.Empty,
            HasCustomNewFactory = wrappedSchema.HasCustomNewFactory,
            HasCustomValidation = wrappedSchema.HasCustomValidation,
            HasCustomActions = wrappedSchema.HasCustomActions,
            HasCustomLoad = wrappedSchema.HasCustomLoad,
            HasCustomSave = wrappedSchema.HasCustomSave,
            HasLiveEdit = wrappedSchema.HasLiveEdit,
            Type = typeof(ServerSettings),
            ContextualType = typeof(ServerSettings).ToContextualType(),
            Schema = wrappedSchema.Schema,
            // We're not going to be using this before .AddParts is called.
            PluginInfo = null!,
        };
    }

    #region Configuration Info

    public void AddParts(IEnumerable<Type> configurationTypes)
    {
        if (_loaded) return;
        _loaded = true;

        ArgumentNullException.ThrowIfNull(configurationTypes);

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
            HasCustomNewFactory = serverSettingsInfo.HasCustomNewFactory,
            HasCustomValidation = serverSettingsInfo.HasCustomValidation,
            HasCustomActions = serverSettingsInfo.HasCustomActions,
            HasCustomLoad = serverSettingsInfo.HasCustomLoad,
            HasCustomSave = serverSettingsInfo.HasCustomSave,
            HasLiveEdit = serverSettingsInfo.HasLiveEdit,
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

        foreach (var configurationType in configurationTypes)
        {
            if (configurationType == typeof(ServerSettings))
                continue;

            var pluginInfo = _pluginManager.GetPluginInfo(configurationType.Assembly)!;
            var id = GetID(configurationType, pluginInfo);
            var contextualType = configurationType.ToContextualType();
            var wrappedSchema = _jsonSchemaGenerator.GetSchemaForType(configurationType);
            wrappedSchema.Schema.Id = id.ToString();
            var description = TypeReflectionExtensions.GetDescription(contextualType);
            var name = wrappedSchema.Schema.Title!;
            string? path = null;
            // If it's a base config then it should not have a path as it cannot be loaded or saved.
            if (!configurationType.IsAssignableTo(typeof(IBaseConfiguration)))
            {
                if (contextualType.GetAttribute<StorageLocationAttribute>(false) is { } storageLocationAttribute)
                {
                    if (storageLocationAttribute.InMemoryOnly)
                    {
                        path = null;
                    }
                    else if (!string.IsNullOrWhiteSpace(storageLocationAttribute.RelativePath))
                    {
                        var relativePath = storageLocationAttribute.RelativePath;
                        if (!relativePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                            relativePath += ".json";
                        path = Path.Join(_applicationPaths.DataPath, relativePath);
                    }
                    else if (!string.IsNullOrWhiteSpace(storageLocationAttribute.FileName))
                    {
                        var fileName = storageLocationAttribute.FileName;
                        if (!fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                            fileName += ".json";
                        path = Path.Join(_applicationPaths.ConfigurationsPath, pluginInfo.ID.ToString(), fileName);
                    }
                    else
                    {
                        var fileName = name
                            .RemoveInvalidPathCharacters()
                            .Replace(' ', '-')
                            .ToLower();
                        path = Path.Join(_applicationPaths.ConfigurationsPath, pluginInfo.ID.ToString(), fileName + ".json");
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
            }

            _configurationTypes[id] = new(this)
            {
                ID = id,
                Path = path,
                Name = name,
                Description = description,
                Type = configurationType,
                ContextualType = contextualType,
                HasCustomNewFactory = wrappedSchema.HasCustomNewFactory,
                HasCustomValidation = wrappedSchema.HasCustomValidation,
                HasCustomActions = wrappedSchema.HasCustomActions,
                HasCustomLoad = wrappedSchema.HasCustomLoad,
                HasCustomSave = wrappedSchema.HasCustomSave,
                HasLiveEdit = wrappedSchema.HasLiveEdit,
                Schema = wrappedSchema.Schema,
                PluginInfo = pluginInfo,
            };
        }

        _logger.LogTrace("Loaded {ConfigurationCount} configurations.", _configurationTypes.Count);
    }

    public ConfigurationProvider<TConfig> CreateProvider<TConfig>() where TConfig : class, IConfiguration, new()
        => new(this);

    public IEnumerable<ConfigurationInfo> GetAllConfigurationInfos()
        => _configurationTypes.Values
            .OrderByDescending(p => typeof(CorePlugin) == p.PluginInfo.PluginType)
            .ThenBy(p => p.PluginInfo.Name)
            .ThenBy(p => p.Name)
            .ThenBy(p => p.ID);

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

    public IReadOnlyDictionary<string, IReadOnlyList<string>> Validate(ConfigurationInfo info, IConfiguration config)
    {
        var json = Serialize(config);
        try
        {
            var (_, errors) = ((JToken, Dictionary<string, IReadOnlyList<string>>))typeof(ConfigurationService)
                .GetMethod(nameof(ValidateInternal), BindingFlags.NonPublic | BindingFlags.Instance)!
                .MakeGenericMethod(info.Type)
                .Invoke(this, [info, json, config, false, false])!;
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

        if (errorDict.Count is 0 && typeof(TConfig).IsAssignableTo(typeof(IConfigurationWithCustomValidation<TConfig>)))
        {
            config ??= DeserializeInternal<TConfig>(json);

            _logger.LogTrace("Calling custom validation for {Type}.", info.Name);
            errorDict = ((IReadOnlyDictionary<string, IReadOnlyList<string>>)typeof(TConfig)
                .GetMethod(nameof(IConfigurationWithCustomValidation<TConfig>.Validate), BindingFlags.Public | BindingFlags.Static)!
                .Invoke(null, [config, this, _pluginManager])!).ToDictionary(a => a.Key, a => a.Value.ToList());
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

    #region Actions

    public ConfigurationActionResult PerformCustomAction(ConfigurationInfo info, IConfiguration configuration, string path, string actionID, IUser? user = null, Uri? uri = null)
    {
        try
        {
            return (ConfigurationActionResult)typeof(ConfigurationService)
                .GetMethod(nameof(PerformCustomActionInternal), BindingFlags.NonPublic | BindingFlags.Static)!
                .MakeGenericMethod(configuration.GetType())
                .Invoke(this, [_loggerFactory, _pluginManager, this, info, configuration, path, actionID, user, uri])!;
        }
        catch (TargetInvocationException ex)
        {
            if (ex.InnerException is null)
                throw;
            throw ex.InnerException;
        }
    }

    public ConfigurationActionResult PerformCustomAction<TConfig>(TConfig configuration, string path, string actionID, IUser? user = null, Uri? uri = null) where TConfig : class, IConfiguration, new()
        => PerformCustomActionInternal(_loggerFactory, _pluginManager, this, GetConfigurationInfo<TConfig>(), configuration, path, actionID, user, uri);

    private static ConfigurationActionResult PerformCustomActionInternal<TConfig>(
        ILoggerFactory loggerFactory,
        IPluginManager pluginManager,
        IConfigurationService configurationService,
        ConfigurationInfo info,
        TConfig configuration,
        string path,
        string actionID,
        IUser? user,
        Uri? uri
    ) where TConfig : class, IConfiguration, new()
    {
        var (type, schema, target, methodInfo) = GetContextualTypeForConfigurationInfo(info, path, configuration, actionID: actionID);
        if (target is null || methodInfo is null)
            return new($"Unable to find action with ID \"{actionID}\" for configuration \"{info.Name}\"", DisplayColorTheme.Warning);

        return RunAction(loggerFactory, pluginManager, configurationService, info, configuration, path, target, methodInfo, schema, type, ReactiveEventType.All, user, uri);
    }

    public ConfigurationActionResult PerformReactiveAction(ConfigurationInfo info, IConfiguration configuration, string path, ConfigurationActionType actionType, ReactiveEventType reactiveEventType = ReactiveEventType.All, IUser? user = null, Uri? uri = null)
    {
        try
        {
            return (ConfigurationActionResult)typeof(ConfigurationService)
                .GetMethod(nameof(PerformReactiveActionInternal), BindingFlags.NonPublic | BindingFlags.Static)!
                .MakeGenericMethod(configuration.GetType())
                .Invoke(this, [_loggerFactory, _pluginManager, this, info, configuration, path, actionType, reactiveEventType, user, uri])!;
        }
        catch (TargetInvocationException ex)
        {
            if (ex.InnerException is null)
                throw;
            throw ex.InnerException;
        }
    }

    public ConfigurationActionResult PerformReactiveAction<TConfig>(TConfig configuration, string path, ConfigurationActionType actionType, ReactiveEventType reactiveEventType = ReactiveEventType.All, IUser? user = null, Uri? uri = null) where TConfig : class, IConfiguration, new()
        => PerformReactiveActionInternal(_loggerFactory, _pluginManager, this, GetConfigurationInfo<TConfig>(), configuration, path, actionType, reactiveEventType, user, uri);

    private static ConfigurationActionResult PerformReactiveActionInternal<TConfig>(
        ILoggerFactory loggerFactory,
        IPluginManager pluginManager,
        IConfigurationService configurationService,
        ConfigurationInfo info,
        TConfig configuration,
        string path,
        ConfigurationActionType actionType,
        ReactiveEventType reactiveEventType,
        IUser? user,
        Uri? uri
    ) where TConfig : class, IConfiguration, new()
    {
        var (type, schema, target, methodInfo) = GetContextualTypeForConfigurationInfo(info, path, configuration, actionType: actionType, reactiveEventType: reactiveEventType);
        // In case we're unable to find the reactive action requested, silently return as to not spam the client UI as we do for custom actions.
        if (target is null || methodInfo is null)
            return new();

        return RunAction(loggerFactory, pluginManager, configurationService, info, configuration, path, target, methodInfo, schema, type, reactiveEventType, user, uri);
    }

    #region Actions | Internals

    [GeneratedRegex(@"(?<!\\)\.")]
    private static partial Regex SplitPathToPartsRegex();

    [GeneratedRegex(@"(?<!\\)""")]
    private static partial Regex InvalidQuoteRegex();

    [GeneratedRegex(@"(?<=\w|\]|^)\[", RegexOptions.Compiled | RegexOptions.ECMAScript)]
    private static partial Regex IndexNotationFixRegex();

    private static (ContextualType, JsonSchema, object?, MethodInfo?) GetContextualTypeForConfigurationInfo(ConfigurationInfo info, string path, object config, ConfigurationActionType? actionType = null, string? actionID = null, ReactiveEventType reactiveEventType = ReactiveEventType.All)
    {
        var schema = info.Schema;
        var type = info.ContextualType;
        var value = config;
        var innovationValue = (object?)null;
        var innovationMethodInfo = (MethodInfo?)null;
        var isNewtonsoftJson = info.Type.IsAssignableTo(typeof(INewtonsoftJsonConfiguration));
        if (actionType.HasValue && reactiveEventType is not ReactiveEventType.NewValue)
        {
            if (reactiveEventType is ReactiveEventType.All)
            {
                var method = type.Methods
                    .FirstOrDefault(method => method.GetAttribute<ConfigurationActionAttribute>(false) is { ActionType: ConfigurationActionType.LiveEdit, ReactiveEventType: ReactiveEventType.All });
                if (method is not null)
                {
                    innovationValue = value;
                    innovationMethodInfo = method.MethodInfo;
                }
            }
            else
            {
                var method = type.Methods.FirstOrDefault(method => method.GetAttribute<ConfigurationActionAttribute>(false) is { ActionType: ConfigurationActionType.LiveEdit, ReactiveEventType: var methodEventType } && methodEventType == reactiveEventType) ??
                    type.Methods.FirstOrDefault(method => method.GetAttribute<ConfigurationActionAttribute>(false) is { ActionType: ConfigurationActionType.LiveEdit, ReactiveEventType: ReactiveEventType.All });
                if (method is not null)
                {
                    innovationValue = value;
                    innovationMethodInfo = method.MethodInfo;
                }
            }
        }

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

                    var key = part[2..^2];
                    if (value is IReadOnlyDictionary<string, object> dictionary)
                        value = dictionary[key];
                    else if (value is IDictionary<string, object> dictionary2)
                        value = dictionary2[key];
                    else
                        throw new InvalidConfigurationActionException($"Invalid path \"{path}\"", nameof(path));
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
                    if (value is IEnumerable<object> enumerable)
                    {
                        try
                        {
                            value = enumerable.ElementAt(int.Parse(part[1..^1]));
                        }
                        catch (ArgumentOutOfRangeException)
                        {
                            throw new InvalidConfigurationActionException($"Invalid path \"{path}\"", nameof(path));
                        }
                    }
                    else
                    {
                        throw new InvalidConfigurationActionException($"Invalid path \"{path}\"", nameof(path));
                    }
                }
            }
            // Classes
            else
            {
                schema = schema.Properties.TryGetValue(part, out var propertySchema)
                    ? propertySchema.Reference is { } referencedSchema0 ? referencedSchema0 : propertySchema
                    : throw new InvalidConfigurationActionException($"Invalid path \"{path}\"", nameof(path));
                var propertyInfo = type.Properties.FirstOrDefault(x => GetJsonName(x, isNewtonsoftJson) == part) ??
                    throw new InvalidConfigurationActionException($"Invalid path \"{path}\"", nameof(path));
                value = propertyInfo.GetValue(value);
                type = propertyInfo.PropertyType;
            }

            if (actionType is ConfigurationActionType.LiveEdit && reactiveEventType is not ReactiveEventType.NewValue)
            {
                if (reactiveEventType is ReactiveEventType.All)
                {
                    var method = type.Methods
                        .FirstOrDefault(method => method.GetAttribute<ConfigurationActionAttribute>(false) is { ActionType: ConfigurationActionType.LiveEdit, ReactiveEventType: ReactiveEventType.All });
                    if (method is not null)
                    {
                        innovationValue = value;
                        innovationMethodInfo = method.MethodInfo;
                    }
                }
                else
                {
                    var method = type.Methods.FirstOrDefault(method => method.GetAttribute<ConfigurationActionAttribute>(false) is { ActionType: ConfigurationActionType.LiveEdit, ReactiveEventType: var methodEventType } && methodEventType == reactiveEventType) ??
                        type.Methods.FirstOrDefault(method => method.GetAttribute<ConfigurationActionAttribute>(false) is { ActionType: ConfigurationActionType.LiveEdit, ReactiveEventType: ReactiveEventType.All });
                    if (method is not null)
                    {
                        innovationValue = value;
                        innovationMethodInfo = method.MethodInfo;
                    }
                }
            }
        }

        if (!actionType.HasValue)
        {
            if (string.IsNullOrEmpty(actionID))
                throw new InvalidConfigurationActionException($"Invalid action with ID \"{actionID}\" for path \"{path}\"", nameof(actionID));
            var method = type.Methods
                .FirstOrDefault(method => string.Equals(method.Name, actionID, StringComparison.Ordinal));
            if (method is not null)
            {
                innovationValue = value;
                innovationMethodInfo = method.MethodInfo;
            }
        }
        else if (actionType is ConfigurationActionType.LiveEdit && reactiveEventType is ReactiveEventType.NewValue)
        {
            var method = type.Methods
                .FirstOrDefault(method => method.GetAttribute<ConfigurationActionAttribute>(false) is { ActionType: ConfigurationActionType.LiveEdit, ReactiveEventType: ReactiveEventType.NewValue }) ??
                type.Methods.FirstOrDefault(method => method.GetAttribute<ConfigurationActionAttribute>(false) is { ActionType: ConfigurationActionType.LiveEdit, ReactiveEventType: ReactiveEventType.All });
            if (method is not null)
            {
                innovationValue = value;
                innovationMethodInfo = method.MethodInfo;
            }
        }

        return (type, schema, innovationValue, innovationMethodInfo);
    }

    public static string GetJsonName(ContextualPropertyInfo property, bool isNewtonsoftJson)
    {
        if (isNewtonsoftJson)
        {
            if (property.GetAttribute<JsonPropertyAttribute>(false) is { } jsonPropertyAttribute)
                return jsonPropertyAttribute.PropertyName ?? property.Name;
        }
        else
        {
            if (property.GetAttribute<JsonPropertyNameAttribute>(false) is { } jsonPropertyNameAttribute)
                return jsonPropertyNameAttribute.Name ?? property.Name;
        }

        return property.Name;
    }

    private static ConfigurationActionResult RunAction<TConfig>(
        ILoggerFactory loggerFactory,
        IPluginManager pluginManager,
        IConfigurationService configurationService,
        ConfigurationInfo info,
        TConfig configuration,
        string path,
        object target,
        MethodInfo methodInfo,
        JsonSchema schema,
        ContextualType type,
        ReactiveEventType reactiveEventType,
        IUser? user,
        Uri? uri
    ) where TConfig : class, IConfiguration, new()
    {
        var logger = loggerFactory.CreateLogger<TConfig>();
        var genericContext = new ConfigurationActionContext<TConfig>
        {
            Logger = logger,
            Configuration = configuration,
            Info = info,
            ConfigurationService = configurationService,
            PluginManager = pluginManager,
            Path = path,
            ReactiveEventType = reactiveEventType,
            Schema = schema,
            Type = type,
            User = user,
            Uri = uri,
        };
        var argumentList = new object?[] {
          logger,
          path,
          reactiveEventType,
          info,
          configuration,
          genericContext,
          schema,
          type,
          user,
          uri,
        };
        return methodInfo.Invoke<ConfigurationActionResult>(pluginManager, target, argumentList) ?? new();
    }

    #endregion

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
        if (typeof(TConfig).IsAssignableTo(typeof(IConfigurationWithNewFactory<TConfig>)))
            return (TConfig)typeof(TConfig).GetMethod(nameof(IConfigurationWithNewFactory<TConfig>.New), BindingFlags.Public | BindingFlags.Static)!
                .Invoke(null, [this, _pluginManager])!;

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
                .Invoke(this, [info, copy])!;
        }
        catch (TargetInvocationException ex)
        {
            if (ex.InnerException is null)
                throw;
            throw ex.InnerException;
        }
    }

    public TConfig Load<TConfig>(bool copy = false) where TConfig : class, IConfiguration, new()
        => LoadInternal<TConfig>(GetConfigurationInfo<TConfig>(), copy);

    private TConfig LoadInternal<TConfig>(ConfigurationInfo info, bool copy) where TConfig : class, IConfiguration, new()
    {
        if (info.IsBase)
            return New<TConfig>();

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
            if (typeof(TConfig).IsAssignableTo(typeof(IConfigurationWithMigrations)))
                json = (string)typeof(TConfig)
                    .GetMethod(nameof(IConfigurationWithMigrations.ApplyMigrations), BindingFlags.Public | BindingFlags.Static)!
                    .Invoke(null, [json, _applicationPaths])!;

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
        if (info.IsBase)
            return false;

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
            else if (info.PluginInfo is { IsInstalled: false, IsEnabled: false })
            {
                _logger.LogWarning("Unable to save a physical copy of the configuration for {Name} after plugin has been uninstalled. Skipping save.", info.Name);
                return false;
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

    private Guid GetID(Type type)
        => _loaded && _pluginManager.GetPluginInfo(type.Assembly) is { } pluginInfo
            ? GetID(type, pluginInfo)
            : Guid.Empty;

    private static Guid GetID(Type type, PluginInfo pluginInfo)
        => UuidUtility.GetV5($"Configuration={type.FullName!}", pluginInfo.ID);

    #endregion
}
