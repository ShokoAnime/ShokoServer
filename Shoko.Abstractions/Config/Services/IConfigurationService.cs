using System;
using System.Collections.Generic;
using NJsonSchema;
using Shoko.Abstractions.Config.Enums;
using Shoko.Abstractions.Config.Events;
using Shoko.Abstractions.Config.Exceptions;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.User;

namespace Shoko.Abstractions.Config.Services;

/// <summary>
/// Service responsible for managing configurations implementing the
/// <see cref="IConfiguration"/> interface.
/// </summary>
public interface IConfigurationService
{
    /// <summary>
    ///   Dispatched when a configuration is saved.
    /// </summary>
    event EventHandler<ConfigurationSavedEventArgs>? Saved;

    /// <summary>
    ///   Dispatched when a configuration that requires a restart is saved.
    /// </summary>
    event EventHandler<ConfigurationRequiresRestartEventArgs>? RequiresRestart;

    /// <summary>
    ///   Indicates which configurations need a restart of the server for it's
    ///   changes to take effect. The key is the configuration ID and the value
    ///   is a set of property paths that require a restart.
    /// </summary>
    public IReadOnlyDictionary<Guid, IReadOnlySet<string>> RestartPendingFor { get; }

    /// <summary>
    ///   The environment variables that have been successfully loaded into
    ///   the configurations. The key is the configuration ID and the value
    ///   is a set of environment variable names loaded by that configuration.
    /// </summary>
    public IReadOnlyDictionary<Guid, IReadOnlySet<string>> LoadedEnvironmentVariables { get; }

    /// <summary>
    ///   Create a new <see cref="ConfigurationProvider{TConfig}"/> instance for
    ///   the specified configuration type.
    /// </summary>
    /// <typeparam name="TConfig">
    ///   The configuration type.
    /// </typeparam>
    /// <returns>
    ///   The newly created configuration provider.
    /// </returns>
    ConfigurationProvider<TConfig> CreateProvider<TConfig>() where TConfig : class, IConfiguration, new();

    /// <summary>
    ///   Gets all <see cref="ConfigurationInfo" />s registered with the system.
    /// </summary>
    /// <returns>
    ///   The registered <see cref="ConfigurationInfo" />s.
    /// </returns>
    IEnumerable<ConfigurationInfo> GetAllConfigurationInfos();

    /// <summary>
    ///   Gets all registered <see cref="ConfigurationInfo" />s for the
    ///   specified plugin instance.
    /// </summary>
    /// <param name="plugin">
    ///   The plugin instance.
    /// </param>
    /// <returns>
    ///   The <see cref="ConfigurationInfo" />s for the plugin.
    /// </returns>
    IReadOnlyList<ConfigurationInfo> GetConfigurationInfo(IPlugin plugin);

    /// <summary>
    ///   Gets the <see cref="ConfigurationInfo" /> for the specified
    ///   configuration ID, if the configuration ID is registered with the
    ///   system.
    /// </summary>
    /// <param name="configurationId">
    ///   The configuration ID.
    /// </param>
    /// <returns>
    ///   The <see cref="ConfigurationInfo" />, or <c>null</c> if not found.
    /// </returns>
    ConfigurationInfo? GetConfigurationInfo(Guid configurationId);

    /// <summary>
    ///   Gets the <see cref="ConfigurationInfo" /> for the specified
    ///   configuration type.
    /// </summary>
    /// <param name="type">
    ///   The type of the configuration.
    /// </param>
    /// <returns>
    ///   The <see cref="ConfigurationInfo" />.
    /// </returns>
    ConfigurationInfo? GetConfigurationInfo(Type type);

    /// <summary>
    ///   Gets the <see cref="ConfigurationInfo" /> for the specified
    ///   configuration type.
    /// </summary>
    /// <typeparam name="TConfig">
    ///   The type of the configuration.
    /// </typeparam>
    /// <returns>
    ///   The <see cref="ConfigurationInfo" />.
    /// </returns>
    ConfigurationInfo GetConfigurationInfo<TConfig>() where TConfig : class, IConfiguration, new();

    /// <summary>
    ///   Validates a stringified JSON configuration against the specified
    ///   <see cref="ConfigurationInfo" />'s schema.
    /// </summary>
    /// <param name="info">
    ///   The <see cref="ConfigurationInfo" />.
    /// </param>
    /// <param name="json">
    ///   The stringified JSON configuration.
    /// </param>
    /// <returns>
    ///   A read-only dictionary of validation errors per property path.
    /// </returns>
    IReadOnlyDictionary<string, IReadOnlyList<string>> Validate(ConfigurationInfo info, string json);

    /// <summary>
    ///   Validates a stringified JSON configuration against the specified
    ///   <see cref="ConfigurationInfo" />'s schema.
    /// </summary>
    /// <param name="info">
    ///   The <see cref="ConfigurationInfo" />.
    /// </param>
    /// <param name="config">
    ///   The configuration instance.
    /// </param>
    /// <returns>
    ///   A read-only dictionary of validation errors per property path.
    /// </returns>
    IReadOnlyDictionary<string, IReadOnlyList<string>> Validate(ConfigurationInfo info, IConfiguration config);

    /// <summary>
    ///   Validates a configuration instance against it's schema.
    /// </summary>
    /// <typeparam name="TConfig">
    ///   The type of the configuration.
    /// </typeparam>
    /// <param name="config">
    ///   The configuration instance.
    /// </param>
    /// <returns>
    ///   A read-only dictionary of validation errors per property path.
    /// </returns>
    IReadOnlyDictionary<string, IReadOnlyList<string>> Validate<TConfig>(TConfig config) where TConfig : class, IConfiguration, new();

    /// <summary>
    ///   Validates a stringified JSON configuration against the specified
    ///   schema using the custom validator.
    /// </summary>
    /// <param name="json">
    ///   The stringified JSON configuration.
    /// </param>
    /// <param name="schema">
    ///   The JSON schema.
    /// </param>
    /// <returns>
    ///   A read-only dictionary of validation errors per property path.
    /// </returns>
    IReadOnlyDictionary<string, IReadOnlyList<string>> Validate(string json, JsonSchema schema);

    /// <summary>
    ///   Perform a custom action on the configuration instance against the
    ///   specified <see cref="ConfigurationInfo" />'s action handler.
    /// </summary>
    /// <param name="info">
    ///   The <see cref="ConfigurationInfo" /> to load the action handler for.
    /// </param>
    /// <param name="configuration">
    ///   The configuration instance to perform the action on.
    /// </param>
    /// <param name="path">
    ///   The path leading to the value related to the action being performed to
    ///   the configuration.
    /// </param>
    /// <param name="actionID">
    ///   The ID of the custom action to perform.
    /// </param>
    /// <param name="user">
    ///   The user performing the action, if applicable.
    /// </param>
    /// <param name="uri">
    ///   The base URI used to access the server by the user, if applicable.
    /// </param>
    /// <exception cref="InvalidConfigurationActionException">
    ///   Thrown when an action is invalid. Be it because the action does not
    ///   exist or because the path for where to look for the action is invalid.
    /// </exception>
    /// <returns>
    ///   The result of the action.
    /// </returns>
    ConfigurationActionResult PerformCustomAction(ConfigurationInfo info, IConfiguration configuration, string path, string actionID, IUser? user = null, Uri? uri = null);

    /// <summary>
    ///   Perform a custom action on the configuration.
    /// </summary>
    /// <typeparam name="TConfig">
    ///   The type of the configuration.
    /// </typeparam>
    /// <param name="configuration">
    ///   The configuration instance to perform the action on.
    /// </param>
    /// <param name="path">
    ///   The path leading to the value related to the action being performed to
    ///   the configuration.
    /// </param>
    /// <param name="actionID">
    ///   The ID of the custom action to perform.
    /// </param>
    /// <param name="user">
    ///   The user performing the action, if applicable.
    /// </param>
    /// <param name="uri">
    ///   The base URI used to access the server by the user, if applicable.
    /// </param>
    /// <exception cref="InvalidConfigurationActionException">
    /// Thrown when an action is invalid. Be it because the action does not
    /// exist or because the path for where to look for the action is invalid.
    /// </exception>
    /// <returns>
    ///   The result of the action.
    /// </returns>
    ConfigurationActionResult PerformCustomAction<TConfig>(TConfig configuration, string path, string actionID, IUser? user = null, Uri? uri = null) where TConfig : class, IConfiguration, new();

    /// <summary>
    ///   Report that a value has changed in the configuration instance for
    ///   the specified <see cref="ConfigurationInfo" />'s changed handler.
    /// </summary>
    /// <param name="info">
    ///   The <see cref="ConfigurationInfo" /> to load the action handler for.
    /// </param>
    /// <param name="configuration">
    ///   The configuration instance to perform the action on.
    /// </param>
    /// <param name="path">
    ///   The path leading to the value related to the action being performed to
    ///   the configuration.
    /// </param>
    /// <param name="actionType">
    ///   The reaction action type to perform.
    /// </param>
    /// <param name="reactiveEventType">
    ///   The reactive event type to perform.
    /// </param>
    /// <param name="user">
    ///   The user performing the action, if applicable.
    /// </param>
    /// <param name="uri">
    ///   The base URI used to access the server by the user, if applicable.
    /// </param>
    /// <exception cref="InvalidConfigurationActionException">
    /// Thrown when the specified path is invalid.
    /// </exception>
    /// <returns>
    ///   The result of the action.
    /// </returns>
    ConfigurationActionResult PerformReactiveAction(ConfigurationInfo info, IConfiguration configuration, string path, ConfigurationActionType actionType, ReactiveEventType reactiveEventType = ReactiveEventType.All, IUser? user = null, Uri? uri = null);

    /// <summary>
    ///   Report that a value has changed in the configuration.
    /// </summary>
    /// <typeparam name="TConfig">
    ///   The type of the configuration.
    /// </typeparam>
    /// <param name="configuration">
    ///   The configuration instance to perform the action on.
    /// </param>
    /// <param name="path">
    ///   The path leading to the value related to the action being performed to
    ///   the configuration.
    /// </param>
    /// <param name="actionType">
    ///   The reaction action type to perform.
    /// </param>
    /// <param name="reactiveEventType">
    ///   The reactive event type to perform.
    /// </param>
    /// <param name="user">
    ///   The user performing the action, if applicable.
    /// </param>
    /// <param name="uri">
    ///   The base URI used to access the server by the user, if applicable.
    /// </param>
    /// <exception cref="InvalidConfigurationActionException">
    /// Thrown when the specified path is invalid.
    /// </exception>
    /// <returns>
    ///   The result of the action.
    /// </returns>
    ConfigurationActionResult PerformReactiveAction<TConfig>(TConfig configuration, string path, ConfigurationActionType actionType, ReactiveEventType reactiveEventType = ReactiveEventType.All, IUser? user = null, Uri? uri = null) where TConfig : class, IConfiguration, new();

    /// <summary>
    ///   Creates a new configuration instance for the specified
    ///   <see cref="ConfigurationInfo" />'s type without saving it and without
    ///   storing it in the in-memory cache.
    /// </summary>
    /// <param name="info">
    ///   The <see cref="ConfigurationInfo" />.
    /// </param>
    /// <returns>
    ///   The new configuration instance.
    /// </returns>
    IConfiguration New(ConfigurationInfo info);

    /// <summary>
    ///   Creates a new configuration instance for
    ///   <typeparamref name="TConfig"/> without saving it and without storing
    ///   it in the in-memory cache.
    /// </summary>
    /// <typeparam name="TConfig">
    ///   The type of the configuration.
    /// </typeparam>
    /// <returns>
    ///   The new configuration instance.
    /// </returns>
    TConfig New<TConfig>() where TConfig : class, IConfiguration, new();

    /// <summary>
    ///   Loads the configuration instance for the specified
    ///   <see cref="ConfigurationInfo" /> from the in-memory cache, from the
    ///   disk. It will validate the configuration before loading it and create
    ///   a new configuration file if one does not exist on disk.
    /// </summary>
    /// <param name="info">
    ///   The <see cref="ConfigurationInfo" />.
    /// </param>
    /// <param name="copy">
    ///   Set to true to get a copy of the configuration instead of the
    ///   in-memory cached instance.
    /// </param>
    /// <exception cref="ConfigurationValidationException">
    ///   Thrown when a configuration fails validation.
    /// </exception>
    /// <returns>
    ///   The loaded configuration instance. A base configuration is never
    ///   stored, so for one of those this is a new default instance.
    /// </returns>
    IConfiguration Load(ConfigurationInfo info, bool copy = false);

    /// <summary>
    ///   Loads the configuration instance for <typeparamref name="TConfig"/>
    ///   from the in-memory cache or from the disk. It will validate the
    ///   configuration before loading it and create a new configuration file if
    ///   one does not exist on disk.
    /// </summary>
    /// <typeparam name="TConfig">
    ///   The type of the configuration.
    /// </typeparam>
    /// <param name="copy">
    ///   Set to true to get a copy of the configuration instead of the
    ///   in-memory cached instance.
    /// </param>
    /// <exception cref="ConfigurationValidationException">
    ///   Thrown when a configuration fails validation.
    /// </exception>
    /// <returns>
    ///   The loaded configuration instance. A base configuration is never
    ///   stored, so for one of those this is a new default instance.
    /// </returns>
    TConfig Load<TConfig>(bool copy = false) where TConfig : class, IConfiguration, new();

    /// <summary>
    ///   Saves the configuration instance for the specified
    ///   <see cref="ConfigurationInfo" />. This will validate the configuration
    ///   before saving it.
    /// </summary>
    /// <param name="info">
    ///   The <see cref="ConfigurationInfo" />.
    /// </param>
    /// <param name="json">
    ///   The stringified configuration to save.
    /// </param>
    /// <exception cref="ConfigurationValidationException">
    ///   Thrown when a configuration fails validation.
    /// </exception>
    /// <returns>
    ///   A boolean indicating whether the configuration was saved to disk. If
    ///   set to <c>false</c> then there was no change to the configuration, or
    ///   it is a base configuration, which is never stored.
    /// </returns>
    bool Save(ConfigurationInfo info, IConfiguration json);

    /// <summary>
    ///   Saves the stringified JSON configuration for the specified
    ///   <see cref="ConfigurationInfo" />. This will validate the configuration
    ///   before saving it, and it will replace the currently loaded in-memory
    ///   configuration instance after saving.
    /// </summary>
    /// <param name="info">
    ///   The <see cref="ConfigurationInfo" />.
    /// </param>
    /// <param name="json">
    ///   The stringified configuration to save.
    /// </param>
    /// <exception cref="ConfigurationValidationException">
    ///   Thrown when a configuration fails validation.
    /// </exception>
    /// <returns>
    ///   A boolean indicating whether the configuration was saved to disk. If
    ///   set to <c>false</c> then there was no change to the configuration, or
    ///   it is a base configuration, which is never stored.
    /// </returns>
    bool Save(ConfigurationInfo info, string json);

    /// <summary>
    ///   Saves the currently loaded in-memory configuration instance to disk.
    ///   This will validate the configuration before saving it, and it will
    ///   replace the currently loaded in-memory configuration instance after
    ///   saving.
    /// </summary>
    /// <typeparam name="TConfig">
    ///   The type of the configuration.
    /// </typeparam>
    /// <exception cref="ConfigurationValidationException">
    ///   Thrown when a configuration fails validation.
    /// </exception>
    /// <returns>
    ///   A boolean indicating whether the configuration was saved to disk. If
    ///   set to <c>false</c> then there was no change to the configuration, or
    ///   it is a base configuration, which is never stored.
    /// </returns>
    bool Save<TConfig>() where TConfig : class, IConfiguration, new();

    /// <summary>
    ///   Saves the configuration instance. This will validate the configuration
    ///   before saving it, and it will replace the currently loaded in-memory
    ///   configuration instance after saving.
    /// </summary>
    /// <typeparam name="TConfig">
    ///   The type of the configuration.
    /// </typeparam>
    /// <param name="config">The configuration to save.</param>
    /// <exception cref="ConfigurationValidationException">
    ///   Thrown when a configuration fails validation.
    /// </exception>
    /// <returns>
    ///   A boolean indicating whether the configuration was saved to disk. If
    ///   set to <c>false</c> then there was no change to the configuration, or
    ///   it is a base configuration, which is never stored.
    /// </returns>
    bool Save<TConfig>(TConfig config) where TConfig : class, IConfiguration, new();

    /// <summary>
    ///   Saves the stringified JSON configuration. This will validate the
    ///   configuration before saving it, and it will replace the currently
    ///   loaded in-memory configuration instance after saving.
    /// </summary>
    /// <typeparam name="TConfig">
    ///   The type of the configuration.
    /// </typeparam>
    /// <param name="json">
    ///   The stringified configuration to save.
    /// </param>
    /// <exception cref="ConfigurationValidationException">
    ///   Thrown when a configuration fails validation.
    /// </exception>
    /// <returns>
    ///   A boolean indicating whether the configuration was saved to disk. If
    ///   set to <c>false</c> then there was no change to the configuration, or
    ///   it is a base configuration, which is never stored.
    /// </returns>
    bool Save<TConfig>(string json) where TConfig : class, IConfiguration, new();

    /// <summary>
    ///   Gets the cached, serialized JSON schema for the specified
    ///   <see cref="ConfigurationInfo" />.
    /// </summary>
    /// <param name="info">
    ///   The <see cref="ConfigurationInfo" />.
    /// </param>
    /// <returns>
    ///   The serialized JSON schema for the configuration.
    /// </returns>
    string GetSchema(ConfigurationInfo info);

    /// <summary>
    ///   Generates a JSON schema for the specified type using the custom schema
    ///   generator.
    /// </summary>
    /// <param name="type">
    ///   The type.
    /// </param>
    /// <returns>
    ///   The JSON schema.
    /// </returns>
    JsonSchema GenerateSchema(Type type);

    /// <summary>
    ///   Serializes the specified configuration to JSON.
    /// </summary>
    /// <param name="config">
    ///   The configuration to serialize.
    /// </param>
    /// <returns>
    ///   The serialized JSON configuration.
    /// </returns>
    string Serialize(IConfiguration config);

    /// <summary>
    ///   Serializes the specified configuration to JSON with every secret
    ///   replaced by <see cref="ConfigurationSecrets.Sentinel"/>.
    /// </summary>
    /// <remarks>
    ///   This is what every outward-facing surface should serialize with — the
    ///   REST API, a plugin handing a configuration to a client of its own —
    ///   so that all of them mask the same properties the same way. Code that
    ///   needs the real credentials, such as a plugin reading its own settings,
    ///   should keep using <see cref="Load{TConfig}(bool)"/> and the object
    ///   graph instead, which is never masked.
    /// </remarks>
    /// <param name="config">
    ///   The configuration to serialize.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   Thrown when <paramref name="config"/> is <c>null</c>.
    /// </exception>
    /// <returns>
    ///   The serialized JSON configuration, with secrets masked.
    /// </returns>
    string SerializeWithMasking(IConfiguration config);

    /// <summary>
    ///   Replaces every set secret in an already serialized configuration with
    ///   <see cref="ConfigurationSecrets.Sentinel"/>.
    /// </summary>
    /// <param name="info">
    ///   The <see cref="ConfigurationInfo" />.
    /// </param>
    /// <param name="json">
    ///   The serialized configuration to mask.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   Thrown when <paramref name="info"/> or <paramref name="json"/> is
    ///   <c>null</c>.
    /// </exception>
    /// <exception cref="Newtonsoft.Json.JsonReaderException">
    ///   Thrown when <paramref name="json"/> is not valid JSON.
    /// </exception>
    /// <returns>
    ///   The serialized configuration, with secrets masked.
    /// </returns>
    string MaskSecrets(ConfigurationInfo info, string json);

    /// <summary>
    ///   Puts the stored secrets back into an incoming serialized configuration,
    ///   so a document that was handed out masked can be saved again without
    ///   destroying the credentials it was masking.
    /// </summary>
    /// <remarks>
    ///   <see cref="Save(ConfigurationInfo, string)"/> and its overloads already
    ///   do this before validating, so a caller that only saves does not need to
    ///   call this. It is public for the surfaces that do something else with an
    ///   incoming document first — validating it, running a custom action on it —
    ///   and need the real values in hand for that.
    /// </remarks>
    /// <param name="info">
    ///   The <see cref="ConfigurationInfo" />.
    /// </param>
    /// <param name="json">
    ///   The incoming serialized configuration.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   Thrown when <paramref name="info"/> or <paramref name="json"/> is
    ///   <c>null</c>.
    /// </exception>
    /// <exception cref="ConfigurationValidationException">
    ///   Thrown when a secret cannot be restored: when the sentinel arrives with
    ///   no stored value behind it, or when it arrives inside a list whose
    ///   elements carry no stable identity to match them by.
    /// </exception>
    /// <exception cref="Newtonsoft.Json.JsonReaderException">
    ///   Thrown when <paramref name="json"/> is not valid JSON.
    /// </exception>
    /// <returns>
    ///   The serialized configuration, with secrets restored.
    /// </returns>
    string RestoreMaskedSecrets(ConfigurationInfo info, string json);

    /// <summary>
    ///   Deserializes the specified JSON configuration.
    /// </summary>
    /// <param name="info">
    ///   The <see cref="ConfigurationInfo" />.
    /// </param>
    /// <param name="json">
    ///   The JSON configuration to deserialize.
    /// </param>
    /// <returns>
    ///   The deserialized configuration.
    /// </returns>
    IConfiguration Deserialize(ConfigurationInfo info, string json);
}
