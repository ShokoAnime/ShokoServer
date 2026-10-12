using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NJsonSchema;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Config.Events;
using Shoko.Abstractions.Config.Services;
using Shoko.Abstractions.Exceptions;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.User;
using Shoko.Server.Models.Internal;
using Shoko.Server.Plugin;
using Shoko.Server.Repositories.Direct;
using Shoko.Server.Services.Configuration;

namespace Shoko.Server.Services;

/// <summary>
///   Keeps each user's configurations, and checks the registered
///   configurations' schema files at start-up.
/// </summary>
/// <param name="repository">Where the configurations are kept.</param>
/// <param name="registry">The registered configurations.</param>
/// <param name="pluginManager">Tells which plugin a configuration belongs to.</param>
/// <param name="configurationService">Generates and checks against the schemas.</param>
/// <param name="applicationPaths">Gives the data folder relative schema paths start from.</param>
/// <param name="logger">Where the schema files' state is reported.</param>
public class UserConfigurationService(
    UserConfigurationRepository repository,
    UserConfigurationRegistry registry,
    IPluginManager pluginManager,
    IConfigurationService configurationService,
    IApplicationPaths applicationPaths,
    ILogger<UserConfigurationService> logger
) : IUserConfigurationService, IHostedService
{
    #region Constants

    /// <summary>
    ///   The largest configuration kept, in characters of JSON text.
    /// </summary>
    public const int MaxDocumentLength = 1024 * 1024;

    #endregion

    #region Fields

    private readonly ConcurrentDictionary<string, SchemaState> _schemas = new(StringComparer.Ordinal);

    private readonly Lock _saveLock = new();

    #endregion

    #region Events

    /// <inheritdoc/>
    public event EventHandler<UserConfigurationSavedEventArgs>? Saved;

    /// <inheritdoc/>
    public event EventHandler<UserConfigurationDeletedEventArgs>? Deleted;

    #endregion

    #region Lifetime

    /// <summary>
    ///   Follows the plugins uninstalled with their data, and reports the
    ///   state of every registered schema: a missing file as a warning when
    ///   strict and at debug level otherwise, a broken one as an error.
    /// </summary>
    /// <param name="cancellationToken">Unused.</param>
    /// <returns>A completed task.</returns>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (pluginManager is PluginManager manager)
            manager.PluginDataPurged += OnPluginDataPurged;

        foreach (var registration in registry.All.Where(registration => registration.SchemaPath is not null || registration.SchemaJson is not null))
            GetSchemaState(registration);

        return Task.CompletedTask;
    }

    /// <summary>
    ///   Stops following the plugins uninstalled with their data.
    /// </summary>
    /// <param name="cancellationToken">Unused.</param>
    /// <returns>A completed task.</returns>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        if (pluginManager is PluginManager manager)
            manager.PluginDataPurged -= OnPluginDataPurged;

        return Task.CompletedTask;
    }

    /// <summary>
    ///   Deletes every configuration saved for a plugin uninstalled with its
    ///   data, for every user.
    /// </summary>
    /// <param name="sender">The plugin manager.</param>
    /// <param name="pluginID">The plugin's ID.</param>
    private void OnPluginDataPurged(object? sender, Guid pluginID)
    {
        lock (_saveLock)
            repository.DeleteByPluginID(pluginID);
    }

    #endregion

    #region Info

    /// <inheritdoc/>
    public IReadOnlyList<UserConfigurationInfo> GetAllInfos()
        => [.. registry.All.Select(registration => GetInfo(registration, null)).OrderBy(info => info.ID, StringComparer.Ordinal)];

    /// <inheritdoc/>
    public IReadOnlyList<UserConfigurationInfo> GetInfoForUser(IUser user)
    {
        var stored = repository.GetByUserID(GetUserID(user)).ToDictionary(configuration => configuration.ConfigurationID, StringComparer.Ordinal);
        var infos = new List<UserConfigurationInfo>();
        foreach (var registration in registry.All)
        {
            stored.Remove(registration.ID, out var configuration);
            infos.Add(GetInfo(registration, configuration));
        }

        infos.AddRange(stored.Values.Select(configuration => GetStoredInfo(configuration, true)));
        return [.. infos.OrderBy(info => info.ID, StringComparer.Ordinal)];
    }

    /// <summary>
    ///   Describes a stored configuration nothing registered.
    /// </summary>
    /// <param name="stored">The stored configuration.</param>
    /// <param name="isStored">Whether it is still stored, to give its save time.</param>
    /// <returns>The description.</returns>
    private UserConfigurationInfo GetStoredInfo(UserConfiguration stored, bool isStored)
        => new()
        {
            ID = stored.ConfigurationID,
            PluginID = stored.PluginID,
            Name = stored.ConfigurationID,
            Description = null,
            IsRegistered = false,
            HasSchema = false,
            ConfigurationInfo = null,
            PluginInfo = pluginManager.GetPluginInfo(stored.PluginID),
            UpdatedAt = isStored ? stored.UpdatedAt : null,
        };

    /// <summary>
    ///   Describes a registered configuration, with what is stored for it.
    /// </summary>
    /// <param name="registration">The registration.</param>
    /// <param name="stored">What is stored for the user, if anything.</param>
    /// <returns>The description.</returns>
    private UserConfigurationInfo GetInfo(UserConfigurationRegistration registration, UserConfiguration? stored)
    {
        var pluginInfo = pluginManager.GetPluginInfo(registration.PluginType);
        return new()
        {
            ID = registration.ID,
            PluginID = pluginInfo?.ID ?? stored?.PluginID ?? Guid.Empty,
            Name = registration.DisplayName,
            Description = registration.Description,
            IsRegistered = true,
            HasSchema = registration.ConfigurationType is not null || registration.SchemaPath is not null || registration.SchemaJson is not null,
            ConfigurationInfo = registration.ConfigurationType is null ? null : GetSchemaState(registration).Configuration,
            PluginInfo = pluginInfo,
            UpdatedAt = stored?.UpdatedAt,
        };
    }

    /// <inheritdoc/>
    public string? GetSchema(string configurationID)
    {
        ArgumentNullException.ThrowIfNull(configurationID);
        return registry.Get(configurationID) is { } registration ? GetSchemaState(registration).Text : null;
    }

    #endregion

    #region Documents

    /// <inheritdoc/>
    public string? Load(IUser user, string configurationID)
    {
        ArgumentNullException.ThrowIfNull(configurationID);
        return repository.GetByUserIDAndConfigurationID(GetUserID(user), UserConfigurationRegistry.Normalize(configurationID))?.Configuration;
    }

    /// <inheritdoc/>
    public string? LoadMasked(IUser user, string configurationID)
    {
        ArgumentNullException.ThrowIfNull(configurationID);
        if (GetConfiguration(configurationID) is not { } info)
            return Load(user, configurationID);

        return configurationService.MaskSecrets(info, configurationService.Serialize(LoadConfiguration(info, user, configurationID)));
    }

    /// <inheritdoc/>
    public TConfig Load<TConfig>(IUser user, string configurationID) where TConfig : class, IUserConfiguration
        => (TConfig)LoadConfiguration(GetConfigurationInfo(typeof(TConfig)), user, configurationID);

    /// <inheritdoc/>
    public void Save(IUser user, string configurationID, string json)
    {
        ArgumentNullException.ThrowIfNull(configurationID);
        ArgumentNullException.ThrowIfNull(json);
        if (registry.Get(configurationID) is not { } registration)
            throw Refused(nameof(configurationID), $"The user configuration \"{configurationID}\" is not registered, so it cannot be saved.");

        JObject incoming;
        try
        {
            incoming = JObject.Parse(json);
        }
        catch (JsonException)
        {
            throw Refused(nameof(json), "The configuration must be a JSON object.");
        }

        // A secret handed out masked keeps its stored value, as the configuration system's saves do.
        if (registration.ConfigurationType is not null && GetSchemaState(registration).Configuration is { } typed &&
            ConfigurationSecrets.ContainsSecrets(typed.Type) && configurationService is ConfigurationService service)
        {
            var current = Load(user, configurationID) is { } previous ? JToken.Parse(previous) : null;
            var (restored, errors) = service.RestoreMaskedSecrets(incoming, current, typed.Type);
            if (errors.Count > 0)
                throw new GenericValidationException("The configuration could not take back its stored secrets.", errors);
            incoming = (JObject)restored;
        }

        json = incoming.ToString(Formatting.None);

        if (json.Length > MaxDocumentLength)
            throw Refused(nameof(json), $"The configuration is {json.Length} characters long, more than the {MaxDocumentLength} kept.");

        var schema = GetSchemaState(registration);
        if (schema.Schema is not null)
        {
            var errors = schema.Configuration is { } info ? configurationService.Validate(info, json) : configurationService.Validate(json, schema.Schema);
            if (errors.Count > 0)
                throw new GenericValidationException($"The configuration does not fit the schema of {registration.DisplayName} ({registration.ID}).", errors);
        }
        else if (schema.Problem is { } problem && registration.Strict)
        {
            throw Refused(nameof(configurationID), problem);
        }

        var userID = GetUserID(user);
        var key = registration.ID;
        var pluginID = pluginManager.GetPluginInfo(registration.PluginType)?.ID ?? Guid.Empty;
        UserConfiguration stored;
        string? previousJson;
        lock (_saveLock)
        {
            stored = repository.GetByUserIDAndConfigurationID(userID, key) ?? new UserConfiguration { JMMUserID = userID, ConfigurationID = key };
            previousJson = stored.UserConfigurationID is 0 ? null : stored.Configuration;
            stored.PluginID = pluginID;
            stored.Configuration = json;
            stored.UpdatedAt = DateTime.UtcNow;
            repository.Save(stored);
        }

        // Built here rather than in the task, so it carries the actor of this save.
        var savedEventArgs = new UserConfigurationSavedEventArgs
        {
            ConfigurationInfo = GetInfo(registration, stored),
            User = user,
            ChangedPaths = JsonPathDiff.GetChangedPaths(previousJson, json),
            Actor = ActorContext.CurrentActor,
        };
        Task.Run(() => Saved?.Invoke(this, savedEventArgs));
    }

    /// <inheritdoc/>
    public void Save<TConfig>(IUser user, string configurationID, TConfig configuration) where TConfig : class, IUserConfiguration
    {
        ArgumentNullException.ThrowIfNull(configuration);
        GetConfigurationInfo(typeof(TConfig));
        Save(user, configurationID, configurationService.Serialize(configuration));
    }

    /// <inheritdoc/>
    public bool Delete(IUser user, string configurationID)
    {
        ArgumentNullException.ThrowIfNull(configurationID);
        UserConfiguration? stored;
        lock (_saveLock)
        {
            stored = repository.GetByUserIDAndConfigurationID(GetUserID(user), UserConfigurationRegistry.Normalize(configurationID));
            if (stored is null)
                return false;

            repository.Delete(stored);
        }

        var deletedEventArgs = new UserConfigurationDeletedEventArgs
        {
            ConfigurationInfo = registry.Get(configurationID) is { } registration ? GetInfo(registration, null) : GetStoredInfo(stored, false),
            User = user,
            Actor = ActorContext.CurrentActor,
        };
        Task.Run(() => Deleted?.Invoke(this, deletedEventArgs));
        return true;
    }

    #endregion

    #region Schemas

    /// <summary>
    ///   A schema as last read, and why there is none.
    /// </summary>
    /// <param name="Schema">The schema, or <c>null</c> when there is none.</param>
    /// <param name="Text">The schema as JSON text, or <c>null</c> when there is none.</param>
    /// <param name="Problem">Why a schema the registration named cannot be used, or <c>null</c>.</param>
    /// <param name="FileWrittenAt">When the schema file was last written, for a file that exists.</param>
    /// <param name="Configuration">The configuration type, for a typed configuration.</param>
    private sealed record SchemaState(JsonSchema? Schema, string? Text, string? Problem, DateTime? FileWrittenAt, ConfigurationInfo? Configuration = null);

    private static readonly SchemaState _noSchema = new(null, null, null, null);

    /// <summary>
    ///   Gets a registered configuration's schema, reading a schema file again
    ///   when it changed and reporting each change of its state once.
    /// </summary>
    /// <param name="registration">The configuration.</param>
    /// <returns>The schema's state.</returns>
    private SchemaState GetSchemaState(UserConfigurationRegistration registration)
    {
        var key = registration.ID;
        if (registration.SchemaPath is null)
            return _schemas.GetOrAdd(key, _ => ReadFixedSchema(registration));

        var path = ResolveSchemaPath(registration.SchemaPath);
        DateTime? writtenAt = path is not null && File.Exists(path) ? File.GetLastWriteTimeUtc(path) : null;
        if (_schemas.TryGetValue(key, out var current) && current.FileWrittenAt == writtenAt)
            return current;

        var next = ReadSchemaFile(registration, path, writtenAt);
        _schemas[key] = next;
        if (current?.Problem != next.Problem)
            Report(registration, next);

        return next;
    }

    /// <summary>
    ///   Reads a schema that never changes: the configuration type's, for a
    ///   typed configuration, or one given as JSON text.
    /// </summary>
    private SchemaState ReadFixedSchema(UserConfigurationRegistration registration)
    {
        if (registration.ConfigurationType is { } type)
        {
            if (configurationService.GetConfigurationInfo(type) is not { } info)
            {
                var missing = new SchemaState(null, null, $"The user configuration {registration.DisplayName} ({registration.ID}) names {type.Name}, which is not a registered configuration.", null);
                Report(registration, missing);
                return missing;
            }

            return new(info.Schema, configurationService.GetSchema(info), null, null, info);
        }

        if (registration.SchemaJson is not { } json)
            return _noSchema;

        try
        {
            var schema = JsonSchema.FromJsonAsync(json).GetAwaiter().GetResult();
            return new(schema, json, null, null);
        }
        catch (Exception ex)
        {
            var state = new SchemaState(null, null, $"The schema of the user configuration {registration.DisplayName} ({registration.ID}) is not a valid JSON schema: {ex.Message}", null);
            Report(registration, state);
            return state;
        }
    }

    /// <summary>
    ///   Reads a registered configuration's schema file.
    /// </summary>
    private static SchemaState ReadSchemaFile(UserConfigurationRegistration registration, string? path, DateTime? writtenAt)
    {
        if (path is null)
            return new(null, null, $"The schema path \"{registration.SchemaPath}\" of the user configuration {registration.DisplayName} ({registration.ID}) leaves the data folder.", null);

        if (writtenAt is null)
            return new(null, null, $"The schema file \"{path}\" of the user configuration {registration.DisplayName} ({registration.ID}) does not exist.", null);

        try
        {
            var text = File.ReadAllText(path);
            var schema = JsonSchema.FromJsonAsync(text).GetAwaiter().GetResult();
            return new(schema, text, null, writtenAt);
        }
        catch (Exception ex)
        {
            return new(null, null, $"The schema file \"{path}\" of the user configuration {registration.DisplayName} ({registration.ID}) cannot be read: {ex.Message}", writtenAt);
        }
    }

    /// <summary>
    ///   Resolves a schema path against the data folder, refusing a relative
    ///   path that leaves it.
    /// </summary>
    /// <param name="schemaPath">The normalized schema path.</param>
    /// <returns>The full path, or <c>null</c> when it leaves the data folder.</returns>
    private string? ResolveSchemaPath(string schemaPath)
    {
        if (Path.IsPathRooted(schemaPath))
            return Path.GetFullPath(schemaPath);

        var root = Path.GetFullPath(applicationPaths.DataPath);
        var full = Path.GetFullPath(Path.Join(root, schemaPath));
        return full.StartsWith(Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar, StringComparison.Ordinal) ? full : null;
    }

    /// <summary>
    ///   Reports a registered configuration's schema state: a missing file as
    ///   a warning when strict and at debug level otherwise, anything else wrong as an
    ///   error, and a schema that became usable at information level.
    /// </summary>
    private void Report(UserConfigurationRegistration registration, SchemaState state)
    {
        if (state.Problem is null)
        {
            if (registration.SchemaPath is not null)
                logger.LogInformation(
                    "The schema of the user configuration {Name} ({ConfigurationID}) is in place; its saves are validated.",
                    registration.DisplayName,
                    registration.ID
                );
            return;
        }

        if (state.FileWrittenAt is null && registration.SchemaPath is not null && ResolveSchemaPath(registration.SchemaPath) is not null)
        {
            if (registration.Strict)
                logger.LogWarning("{Problem} Saves of the configuration are refused until it exists.", state.Problem);
            else
                logger.LogDebug("{Problem} Saves of the configuration are not validated until it exists.", state.Problem);
            return;
        }

        if (registration.Strict)
            logger.LogError("{Problem} Saves of the configuration are refused until it is fixed.", state.Problem);
        else
            logger.LogError("{Problem} Saves of the configuration are not validated until it is fixed.", state.Problem);
    }

    #endregion

    #region Helpers

    /// <summary>
    ///   Gets the configuration type of a registered typed configuration.
    /// </summary>
    /// <param name="configurationID">The configuration's ID, in any case.</param>
    /// <returns>The configuration type, or <c>null</c> for any other configuration.</returns>
    private ConfigurationInfo? GetConfiguration(string configurationID)
        => registry.Get(configurationID) is { ConfigurationType: not null } registration ? GetSchemaState(registration).Configuration : null;

    /// <summary>
    ///   Loads a typed configuration through its configuration type, as the
    ///   configuration system loads a base configuration: its defaults when
    ///   none is stored.
    /// </summary>
    /// <param name="info">The configuration type.</param>
    /// <param name="user">The user.</param>
    /// <param name="configurationID">The configuration's ID, in any case.</param>
    /// <returns>The configuration.</returns>
    private IConfiguration LoadConfiguration(ConfigurationInfo info, IUser user, string configurationID)
        => Load(user, configurationID) is { } stored ? configurationService.Deserialize(info, stored) : configurationService.New(info);

    /// <summary>
    ///   Gets the configuration type a typed read or write goes through.
    /// </summary>
    /// <param name="type">The configuration's type.</param>
    /// <returns>The configuration type.</returns>
    /// <exception cref="InvalidOperationException">The type is not a registered configuration type.</exception>
    private ConfigurationInfo GetConfigurationInfo(Type type)
        => configurationService.GetConfigurationInfo(type)
            ?? throw new InvalidOperationException($"{type.FullName} is not a registered configuration, so it cannot be read or written as a user configuration.");

    private static int GetUserID(IUser user)
    {
        ArgumentNullException.ThrowIfNull(user);
        return user.LocalID;
    }

    private static GenericValidationException Refused(string key, string message)
        => new(message, new Dictionary<string, IReadOnlyList<string>> { [key] = [message] });

    #endregion
}
