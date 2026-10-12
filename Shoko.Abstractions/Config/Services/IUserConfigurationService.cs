using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Config.Events;
using Shoko.Abstractions.Exceptions;
using Shoko.Abstractions.User;

namespace Shoko.Abstractions.Config.Services;

/// <summary>
///   Keeps user configurations: one JSON object per user and configuration,
///   as JSON text like the configuration system's, or as a configuration for
///   typed ones.
/// </summary>
/// <remarks>
///   User configurations are registered with
///   <see cref="UserConfigurationExtensions.AddUserConfiguration{TPlugin}(IServiceCollection, string, Action{UserConfigurationOptions}?)"/>.
/// </remarks>
public interface IUserConfigurationService
{
    #region Events

    /// <summary>
    ///   Dispatched when a user configuration is saved, so a plugin can react
    ///   to a change made in its generated UI or by a client.
    /// </summary>
    event EventHandler<UserConfigurationSavedEventArgs>? Saved;

    /// <summary>
    ///   Dispatched when a stored user configuration is deleted.
    /// </summary>
    event EventHandler<UserConfigurationDeletedEventArgs>? Deleted;

    #endregion

    #region Info

    /// <summary>
    ///   Lists the registered user configurations, by ID.
    /// </summary>
    /// <returns>The registered configurations, none of them stored for anyone.</returns>
    IReadOnlyList<UserConfigurationInfo> GetAllInfos();

    /// <summary>
    ///   Lists the registered user configurations and the ones stored for a
    ///   user, by ID, with when each was saved.
    /// </summary>
    /// <param name="user">The user.</param>
    /// <returns>The configurations, registered and stored alike.</returns>
    IReadOnlyList<UserConfigurationInfo> GetInfoForUser(IUser user);

    /// <summary>
    ///   The schema a registered user configuration is validated against.
    /// </summary>
    /// <param name="configurationID">The configuration's ID, in any case.</param>
    /// <returns>
    ///   The schema as JSON text, or <c>null</c> when the configuration is not
    ///   registered, has no schema, or its schema file cannot be read.
    /// </returns>
    string? GetSchema(string configurationID);

    #endregion

    #region Load and Save

    /// <summary>
    ///   Reads a user's configuration, registered or not.
    /// </summary>
    /// <param name="user">The user.</param>
    /// <param name="configurationID">The configuration's ID, in any case.</param>
    /// <returns>The configuration as JSON text, or <c>null</c> when none is stored.</returns>
    string? Load(IUser user, string configurationID);

    /// <summary>
    ///   Reads a user's configuration as it is handed out to a client: a typed
    ///   one through its configuration type, its defaults when none is stored,
    ///   with every secret masked, as
    ///   <see cref="IConfigurationService.SerializeWithMasking"/> does; any other
    ///   as stored.
    /// </summary>
    /// <remarks>
    ///   Use it for every outward-facing surface. <see cref="Save(IUser, string, string)"/>
    ///   restores the masked secrets of a document handed out this way.
    /// </remarks>
    /// <param name="user">The user.</param>
    /// <param name="configurationID">The configuration's ID, in any case.</param>
    /// <returns>The configuration as JSON text, or <c>null</c> when an untyped one is not stored.</returns>
    string? LoadMasked(IUser user, string configurationID);

    /// <summary>
    ///   Reads a user's configuration as its configuration type, through the
    ///   configuration system.
    /// </summary>
    /// <typeparam name="TConfig">The configuration type.</typeparam>
    /// <param name="user">The user.</param>
    /// <param name="configurationID">The configuration's ID, in any case.</param>
    /// <returns>The configuration, or a new one with its defaults when none is stored.</returns>
    /// <exception cref="InvalidOperationException"><typeparamref name="TConfig"/> is not a registered configuration type.</exception>
    TConfig Load<TConfig>(IUser user, string configurationID) where TConfig : class, IUserConfiguration;

    /// <summary>
    ///   Saves a user's registered configuration, replacing the stored one,
    ///   after checking it against its schema. A secret of a typed
    ///   configuration that arrives masked keeps its stored value first.
    /// </summary>
    /// <param name="user">The user.</param>
    /// <param name="configurationID">The configuration's ID, in any case.</param>
    /// <param name="json">The configuration as JSON text, a JSON object.</param>
    /// <exception cref="ArgumentNullException"><paramref name="json"/> is <c>null</c>.</exception>
    /// <exception cref="GenericValidationException">
    ///   The configuration is not registered, is not a JSON object, is too
    ///   large or does not fit its schema, its strict schema file is missing
    ///   or broken, or a masked secret cannot be restored.
    /// </exception>
    void Save(IUser user, string configurationID, string json);

    /// <summary>
    ///   Saves a user's registered configuration from its configuration type,
    ///   through the configuration system.
    /// </summary>
    /// <typeparam name="TConfig">The configuration type.</typeparam>
    /// <param name="user">The user.</param>
    /// <param name="configurationID">The configuration's ID, in any case.</param>
    /// <param name="configuration">The configuration.</param>
    /// <exception cref="ArgumentNullException"><paramref name="configuration"/> is <c>null</c>.</exception>
    /// <exception cref="GenericValidationException">
    ///   The configuration is not registered, is too large or does not fit
    ///   its schema.
    /// </exception>
    /// <exception cref="InvalidOperationException"><typeparamref name="TConfig"/> is not a registered configuration type.</exception>
    void Save<TConfig>(IUser user, string configurationID, TConfig configuration) where TConfig : class, IUserConfiguration;

    /// <summary>
    ///   Deletes a user's configuration, registered or not.
    /// </summary>
    /// <param name="user">The user.</param>
    /// <param name="configurationID">The configuration's ID, in any case.</param>
    /// <returns><c>true</c> when a configuration was stored and is now gone.</returns>
    bool Delete(IUser user, string configurationID);

    #endregion
}
