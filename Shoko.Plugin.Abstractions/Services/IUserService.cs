using System.Linq;
using Shoko.Plugin.Abstractions.DataModels.Shoko;

namespace Shoko.Plugin.Abstractions.Services;

/// <summary>
/// User manager.
/// </summary>
public interface IUserService
{
    /// <summary>
    /// Get all users as a queryable list.
    /// </summary>
    /// <returns>The users.</returns>
    IQueryable<IShokoUser> GetUsers();

    /// <summary>
    /// Get a user by ID.
    /// </summary>
    /// <param name="id">The ID.</param>
    /// <returns>The user.</returns>
    IShokoUser? GetUserByID(int id);
}
