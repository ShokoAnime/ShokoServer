namespace Shoko.Plugin.Abstractions.DataModels.Shoko;

/// <summary>
/// Shoko user.
/// </summary>
public interface IShokoUser
{
    /// <summary>
    /// Unique ID.
    /// </summary>
    int ID { get; }

    /// <summary>
    /// Username.
    /// </summary>
    string Username { get; }
}
