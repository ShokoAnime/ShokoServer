using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Server.Databases;
using Shoko.Server.Models.Internal;

namespace Shoko.Server.Repositories.Direct;

/// <summary>
/// Direct repository for <see cref="UserConfiguration"/>, the users'
/// configurations.
/// </summary>
/// <remarks>
/// A handful of rows per user, read when a web UI, an app or a plugin asks
/// for one, so they are not kept in memory.
/// </remarks>
/// <param name="databaseFactory">The database factory.</param>
public class UserConfigurationRepository(DatabaseFactory databaseFactory) : BaseDirectRepository<UserConfiguration, int>(databaseFactory)
{
    /// <summary>
    /// Gets every configuration of a user.
    /// </summary>
    /// <param name="userID">The user's ID.</param>
    /// <returns>The configurations.</returns>
    public virtual IReadOnlyList<UserConfiguration> GetByUserID(int userID)
    {
        using var session = _databaseFactory.SessionFactory.OpenSession();
        return session.Query<UserConfiguration>().Where(row => row.JMMUserID == userID).ToList();
    }

    /// <summary>
    /// Gets one of a user's configurations.
    /// </summary>
    /// <param name="userID">The user's ID.</param>
    /// <param name="configurationID">The configuration's ID, in lower case.</param>
    /// <returns>The configuration, or <c>null</c> when none is stored.</returns>
    public virtual UserConfiguration? GetByUserIDAndConfigurationID(int userID, string configurationID)
    {
        using var session = _databaseFactory.SessionFactory.OpenSession();
        return session.Query<UserConfiguration>().FirstOrDefault(row => row.JMMUserID == userID && row.ConfigurationID == configurationID);
    }

    /// <summary>
    /// Gets every configuration saved for a plugin's registrations.
    /// </summary>
    /// <param name="pluginID">The plugin's ID.</param>
    /// <returns>The configurations.</returns>
    public virtual IReadOnlyList<UserConfiguration> GetByPluginID(Guid pluginID)
    {
        using var session = _databaseFactory.SessionFactory.OpenSession();
        return session.Query<UserConfiguration>().Where(row => row.PluginID == pluginID).ToList();
    }

    /// <summary>
    /// Deletes every configuration of a user.
    /// </summary>
    /// <param name="userID">The user's ID.</param>
    public virtual void DeleteByUserID(int userID)
        => Delete(GetByUserID(userID));

    /// <summary>
    /// Deletes every configuration saved for a plugin's registrations, for
    /// every user.
    /// </summary>
    /// <param name="pluginID">The plugin's ID.</param>
    public virtual void DeleteByPluginID(Guid pluginID)
        => Delete(GetByPluginID(pluginID));
}
