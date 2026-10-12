using System;

namespace Shoko.Server.Models.Internal;

/// <summary>
/// One user's stored user configuration.
/// </summary>
public class UserConfiguration
{
    #region Database Columns

    /// <summary>
    /// Local database ID.
    /// </summary>
    public int UserConfigurationID { get; set; }

    /// <summary>
    /// The user the configuration belongs to.
    /// </summary>
    public int JMMUserID { get; set; }

    /// <summary>
    /// The configuration's ID.
    /// </summary>
    public string ConfigurationID { get; set; } = string.Empty;

    /// <summary>
    /// The plugin that registered the configuration when it was last saved.
    /// </summary>
    public Guid PluginID { get; set; }

    /// <summary>
    /// The configuration, as JSON text.
    /// </summary>
    public string Configuration { get; set; } = "{}";

    /// <summary>
    /// When the document was last saved, in UTC.
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    #endregion
}
