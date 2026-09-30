using System.Text.Json.Serialization;
using Newtonsoft.Json.Converters;

namespace Shoko.Abstractions.Core;

/// <summary>
///   Where a <see cref="RestartReason"/> came from.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
[Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
public enum RestartReasonSource
{
    /// <summary>
    ///   One or more saved configurations changed members marked with
    ///   <see cref="Config.Attributes.RequiresRestartAttribute"/>. A single
    ///   reason stands for all of them; the configuration service lists the
    ///   members.
    /// </summary>
    Configuration = 0,

    /// <summary>
    ///   One or more plugins were enabled, disabled, installed, uninstalled or
    ///   switched to another version, and the changes only apply after a
    ///   restart. A single reason stands for all of them; each plugin's info
    ///   tells its own state. An enabled plugin that cannot load never raises
    ///   it, since a restart would not load it either.
    /// </summary>
    PluginState = 1,

    /// <summary>
    ///   A plugin raised a reason of its own.
    /// </summary>
    Plugin = 2,
}
