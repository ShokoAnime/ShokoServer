// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using Shoko.Abstractions.User;

#nullable enable
namespace Shoko.Server.API.SignalR.Aggregate;

public interface IEventEmitter
{
    /// <summary>
    /// The group name for this emitter.
    /// </summary>
    string Group { get; }

    /// <summary>
    /// Check if a client is listening to this emitter.
    /// </summary>
    /// <param name="connectionId">The connection id</param>
    /// <returns>True if the client is listening, false otherwise.</returns>
    bool IsListening(string connectionId);

    /// <summary>
    /// Connect a client and send the initial messages for that client.
    /// </summary>
    /// <param name="connectionId">The connection id</param>
    /// <param name="user">The user.</param>
    /// <param name="lastConnectedAt">If provided by the client, this is the last time the client was connected.</param>
    /// <returns>An asynchronous task that completes when the initial messages are sent.</returns>
    Task<bool> ConnectAsync(string connectionId, IUser user, DateTime? lastConnectedAt = null);

    /// <summary>
    /// Disconnect a client from the emitter on the hub.
    /// </summary>
    /// <param name="connectionId">The connection id.</param>
    /// <returns>An asynchronous task that completes when the client is disconnected.</returns>
    Task<bool> DisconnectAsync(string connectionId);

    /// <summary>
    /// Send a message to all connected clients on the hub.
    /// </summary>
    /// <param name="subject">Message subject key.</param>
    /// <param name="args">The message arguments.</param>
    /// <returns>An asynchronous task that completes when the message is sent.</returns>
    Task SendAsync(string subject, params object[] args);

    /// <summary>
    /// Send a message to all connected clients for the user on the hub.
    /// </summary>
    /// <param name="user">The user.</param>
    /// <param name="subject">Message subject key.</param>
    /// <param name="args">The message arguments.</param>
    /// <returns>An asynchronous task that completes when the message is sent.</returns>
    Task SendToUserAsync(IUser user, string subject, params object[] args);
}
