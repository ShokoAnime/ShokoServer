using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Shoko.Abstractions.Exceptions;
using Shoko.Abstractions.User.Events;
using Shoko.Abstractions.User.Update;

namespace Shoko.Abstractions.User.Services;

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
    IEnumerable<IUser> GetUsers();

    /// <summary>
    /// Get a user by ID.
    /// </summary>
    /// <param name="id">
    ///   The user ID.
    /// </param>
    /// <returns>
    ///   The user if found, otherwise <c>null</c>.
    /// </returns>
    IUser? GetUserByID(int id);

    /// <summary>
    /// Get a user by username.
    /// </summary>
    /// <param name="username">
    ///   The username.
    /// </param>
    /// <returns>
    ///   The user if found, otherwise <c>null</c>.
    /// </returns>
    IUser? GetUserByUsername(string username);

    /// <summary>
    ///   Get a user from an HTTP context.
    /// </summary>
    /// <param name="context">
    ///   The HTTP context.
    /// </param>
    /// <returns>
    ///   The user if authenticated through the HTTP context, otherwise <c>null</c>.
    /// </returns>
    IUser? GetUserFromHttpContext(HttpContext context);

    /// <summary>
    ///   Get a user from a SignalR hub context.
    /// </summary>
    /// <param name="context">
    ///   The hub caller context.
    /// </param>
    /// <returns>
    ///   The user if authenticated through the hub context, otherwise <c>null</c>.
    /// </returns>
    IUser? GetUserFromHubCallerContext(HubCallerContext context);

    /// <summary>
    ///   Creates a user with the specified data in
    ///   <paramref name="initialData"/>.
    /// </summary>
    /// <param name="initialData">
    ///   The initial user data to set for the new user.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="initialData"/> is <c>null</c>, or
    ///   <see name="initialData.Username"/> is <c>null</c> or empty, or
    ///   <see name="initialData.Password"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="GenericValidationException">
    ///   Attempting to apply the <paramref name="initialData"/> caused
    ///   validation errors to occur.
    /// </exception>
    /// <returns>
    ///   The newly created user.
    /// </returns>
    Task<IUser> CreateUser(UserUpdate initialData);

    /// <summary>
    ///   Resets the user's password back to an empty password.
    /// </summary>
    /// <param name="user">
    ///   The user to reset the password for.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="user"/> is <c>null</c>.
    /// </exception>
    /// <returns>
    ///   A task representing the asynchronous operation.
    /// </returns>
    Task ResetUserPassword(IUser user);

    /// <summary>
    ///   Changes the user's password.
    /// </summary>
    /// <param name="user">
    ///   The user to change the password for.
    /// </param>
    /// <param name="newPassword">
    ///   The new password.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="user"/> or <paramref name="newPassword"/> is
    ///   <c>null</c>.
    /// </exception>
    /// <returns>
    ///   A task representing the asynchronous operation.
    /// </returns>
    Task ChangeUserPassword(IUser user, string newPassword);

    /// <summary>
    ///   Updates a user.
    /// </summary>
    /// <param name="user">
    ///   The user to update.
    /// </param>
    /// <param name="updateData">
    ///   The update data.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="user"/> or <paramref name="updateData"/> is
    ///   <c>null</c>.
    /// </exception>
    /// <exception cref="GenericValidationException">
    ///   Attempting to apply the <paramref name="updateData"/> caused
    ///   validation errors to occur.
    /// </exception>
    /// <returns>
    ///   A task representing the asynchronous operation.
    /// </returns>
    Task<IUser> UpdateUser(IUser user, UserUpdate updateData);

    /// <summary>
    ///   Delete a user.
    /// </summary>
    /// <param name="user">
    ///   The user to delete.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="user"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///   <paramref name="user"/> is not stored in the database, or it is the
    ///   last administrator in the database.
    /// </exception>
    /// <returns>
    ///   A task representing the asynchronous operation.
    /// </returns>
    Task DeleteUser(IUser user);

    /// <summary>
    ///   Attempt to authenticate a user using the given username and password.
    /// </summary>
    /// <param name="username">
    ///   The user's username.
    /// </param>
    /// <param name="password">
    ///   The user's password.
    /// </param>
    /// <returns>
    ///   The user if found and the password is correct, otherwise <c>null</c>.
    /// </returns>
    IUser? AuthenticateUser(string username, string password);

    /// <summary>
    ///   Get all API tokens for a user.
    /// </summary>
    /// <param name="user">
    ///   The user to list tokens for.
    /// </param>
    /// <returns>
    ///   The API tokens registered for the user.
    /// </returns>
    IReadOnlyList<ApiToken> GetApiTokensForUser(IUser user);

    /// <summary>
    ///   Generate a new API token for the given user.
    /// </summary>
    /// <param name="user">
    ///   The user to generate the token for.
    /// </param>
    /// <param name="deviceName">
    ///   Device name to set for the API key.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="user"/> is <c>null</c> or
    ///   <paramref name="deviceName"/> is <c>null</c> or empty.
    /// </exception>
    /// <returns>
    ///   The newly created or existing API token for the user and device.
    /// </returns>
    Task<ApiToken> GenerateApiTokenForUser(IUser user, string deviceName);

    /// <summary>
    ///   Generate a new API token for the given user with an expiration time.
    /// </summary>
    /// <param name="user">
    ///   The user to generate the token for.
    /// </param>
    /// <param name="deviceName">
    ///   Device name to set for the API key.
    /// </param>
    /// <param name="expiresAt">
    ///   The time at which the token expires. Must be at least 57 seconds from
    ///   <see cref="DateTime.Now"/>.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="user"/> is <c>null</c> or
    ///   <paramref name="deviceName"/> is <c>null</c> or empty.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///   <paramref name="expiresAt"/> is less than 57 seconds from
    ///   <see cref="DateTime.Now"/>.
    /// </exception>
    /// <returns>
    ///   The newly created API token for the user and device.
    /// </returns>
    Task<ApiToken> GenerateApiTokenForUser(IUser user, string deviceName, DateTime expiresAt);

    /// <summary>
    ///   Get the API token from the HTTP context.
    /// </summary>
    /// <param name="context">
    ///   The HTTP context.
    /// </param>
    /// <returns>
    ///   The API token if authenticated through the HTTP context, otherwise <c>null</c>.
    /// </returns>
    ApiToken? GetApiTokenFromHttpContext(HttpContext context);

    /// <summary>
    ///   Invalidate a API token for the given user and device.
    /// </summary>
    /// <param name="user">
    ///   The user to invalidate the token for.
    /// </param>
    /// <param name="deviceName">
    ///   The device name to invalidate the token for.
    /// </param>
    /// <returns>
    ///   A task representing the asynchronous operation.
    /// </returns>
    Task<bool> InvalidateApiDeviceForUser(IUser user, string deviceName);

    /// <summary>
    ///   Invalidate all API tokens for the given user.
    /// </summary>
    /// <param name="user">
    ///   The user to invalidate the tokens for.
    /// </param>
    /// <returns>
    ///   A task representing the asynchronous operation.
    /// </returns>
    Task<bool> InvalidateApiTokensForUser(IUser user);

    /// <summary>
    ///   Invalidate the given API token.
    /// </summary>
    /// <param name="token">
    ///   The token to invalidate.
    /// </param>
    /// <returns>
    ///   A task representing the asynchronous operation.
    /// </returns>
    Task<bool> InvalidateApiToken(ApiToken token);

    /// <summary>
    ///   Invalidate the given API token.
    /// </summary>
    /// <param name="token">
    ///   The token to invalidate.
    /// </param>
    /// <returns>
    ///   A task representing the asynchronous operation.
    /// </returns>
    Task<bool> InvalidateApiToken(string token);
}
