using System.Text.Json.Serialization;
using Newtonsoft.Json.Converters;

namespace Shoko.Abstractions.Plugin.Enums;

/// <summary>
///   Why a plugin version is in a <see cref="Models.PluginChangePlan"/>.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
[Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
public enum PluginChangeReason
{
    /// <summary>
    ///   It was asked for.
    /// </summary>
    Requested = 0,

    /// <summary>
    ///   It depends on a plugin being uninstalled, so it goes too when the
    ///   uninstall is forced.
    /// </summary>
    Dependent = 1,

    /// <summary>
    ///   It was installed as a dependency, and nothing installed on purpose
    ///   needs it anymore.
    /// </summary>
    Orphaned = 2,

    /// <summary>
    ///   It is enabled on demand, and no enabled plugin needs it anymore.
    /// </summary>
    NoLongerNeeded = 3,

    /// <summary>
    ///   A required dependency of it is gone.
    /// </summary>
    MissingDependency = 4,

    /// <summary>
    ///   It was installed on purpose, not as a dependency.
    /// </summary>
    InstalledOnPurpose = 5,

    /// <summary>
    ///   Other plugins still need it.
    /// </summary>
    NeededByOthers = 6,

    /// <summary>
    ///   It is an orphan, but pinned.
    /// </summary>
    Pinned = 7,

    /// <summary>
    ///   It is an orphan, but kept.
    /// </summary>
    Kept = 8,

    /// <summary>
    ///   It is an orphan, but the orphans were asked to be kept.
    /// </summary>
    OrphansKept = 9,

    /// <summary>
    ///   It cannot be uninstalled, as it ships with the server.
    /// </summary>
    CannotUninstall = 10,
}
