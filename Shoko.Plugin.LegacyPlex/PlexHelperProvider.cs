using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Core.Services;
using Shoko.Abstractions.User;
using Shoko.Abstractions.Video.Services;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Settings;

namespace Shoko.Plugin.LegacyPlex;

/// <summary>
///   Hands out the Plex client of each user, and reads the Plex data the
///   core keeps: the users' Plex tokens and Plex users, and the picked server
///   and libraries in the server settings.
/// </summary>
/// <param name="loggerFactory">Creates the clients' logger.</param>
/// <param name="httpClientFactory">Creates the HTTP clients Plex is asked with.</param>
/// <param name="settingsProvider">The server settings, holding the picked server and libraries.</param>
/// <param name="jmmUsers">The users, holding their Plex token and Plex users.</param>
/// <param name="systemService">Names the server's version to Plex.</param>
/// <param name="videoService">Finds the files Plex plays.</param>
public class PlexHelperProvider(
    ILoggerFactory loggerFactory,
    IHttpClientFactory httpClientFactory,
    ISettingsProvider settingsProvider,
    JMMUserRepository jmmUsers,
    ISystemService systemService,
    IVideoService videoService
)
{
    private readonly ConcurrentDictionary<int, PlexHelper> _cache = new();

    #region Clients

    /// <summary>
    ///   Gets the Plex client of a user, kept for as long as the server runs.
    /// </summary>
    /// <param name="user">The user.</param>
    /// <returns>The user's Plex client.</returns>
    public PlexHelper GetForUser(IUser user)
        => _cache.GetOrAdd(
            user.LocalID,
            userID => new PlexHelper(
                userID,
                loggerFactory.CreateLogger<PlexHelper>(),
                httpClientFactory,
                settingsProvider,
                jmmUsers,
                systemService,
                videoService
            )
        );

    #endregion

    #region Users

    /// <summary>
    ///   Lists the users with a linked Plex account.
    /// </summary>
    /// <returns>The users holding a Plex token.</returns>
    public IReadOnlyList<JMMUser> GetLinkedUsers()
        => [.. jmmUsers.GetAll().Where(user => !string.IsNullOrEmpty(user.PlexToken))];

    /// <summary>
    ///   Finds the user a Plex user's plays count for.
    /// </summary>
    /// <param name="plexUsername">The Plex user's name, in any case.</param>
    /// <returns>The first user naming the Plex user, or <c>null</c> when none does.</returns>
    public JMMUser? GetUserByPlexUsername(string plexUsername)
        => jmmUsers.GetAll().FirstOrDefault(user =>
            (user.PlexUsers ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Contains(plexUsername, StringComparer.InvariantCultureIgnoreCase)
        );

    #endregion
}
