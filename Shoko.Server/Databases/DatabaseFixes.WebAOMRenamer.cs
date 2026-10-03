using Shoko.Server.Repositories;
using Shoko.Server.Services;

namespace Shoko.Server.Databases;

public partial class DatabaseFixes
{
    #region WebAOM Renamer | Steps

    /// <summary>
    ///   Points the relocation presets of the WebAOM renamer the core shipped
    ///   to the renamer in its bundled plugin. Their settings are kept as they
    ///   are, since the settings type has the same values.
    /// </summary>
    public static void MoveWebAOMPresetsToPlugin()
    {
        var presets = WebAOMRenamerMigration.RepointPresets(RepoFactory.StoredRelocationPreset.GetByProviderID(WebAOMRenamerMigration.LegacyProviderID));
        if (presets.Count is 0)
            return;

        RepoFactory.StoredRelocationPreset.Save(presets);
        _logger.Info($"Moved {presets.Count} relocation presets of the WebAOM renamer to its bundled plugin.");
    }

    #endregion
}
