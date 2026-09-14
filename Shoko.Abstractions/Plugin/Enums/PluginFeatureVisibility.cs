namespace Shoko.Abstractions.Plugin.Enums;

/// <summary>
///   Who a <see cref="Models.PluginFeature"/> is advertised to. Each level
///   also includes every level below it.
/// </summary>
public enum PluginFeatureVisibility
{
    /// <summary>
    ///   Advertised to every client, including unauthenticated ones. Anything
    ///   in the feature's metadata is visible to them too.
    /// </summary>
    Anonymous = 1,

    /// <summary>
    ///   Advertised to authenticated users.
    /// </summary>
    Authenticated = 2,

    /// <summary>
    ///   Advertised to administrators only.
    /// </summary>
    Admin = 3,
}
