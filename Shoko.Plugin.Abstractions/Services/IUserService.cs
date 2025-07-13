using System;
using System.Collections.Generic;
using Shoko.Plugin.Abstractions.DataModels.Shoko;
using Shoko.Plugin.Abstractions.Events;

namespace Shoko.Plugin.Abstractions.Services;

/// <summary>
/// User manager.
/// </summary>
public interface IUserService
{
    /// <summary>
    /// Dispatched when a user is added.
    /// </summary>
    event EventHandler<UserChangedEventArgs>? UserAdded;

    /// <summary>
    /// Dispatched when a user is updated.
    /// </summary>
    event EventHandler<UserChangedEventArgs>? UserUpdated;

    /// <summary>
    /// Dispatched when a user is removed.
    /// </summary>
    event EventHandler<UserChangedEventArgs>? UserRemoved;

    /// <summary>
    /// Get all users as a queryable list.
    /// </summary>
    /// <returns>The users.</returns>
    IEnumerable<IShokoUser> GetUsers();

    /// <summary>
    /// Get a user by ID.
    /// </summary>
    /// <param name="id">The ID.</param>
    /// <returns>The user.</returns>
    IShokoUser? GetUserByID(int id);
}
