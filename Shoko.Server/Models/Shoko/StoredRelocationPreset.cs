using System;
using Shoko.Abstractions.Utilities;
using Shoko.Abstractions.Video.Relocation;
using Shoko.Server.Settings;

#nullable enable
namespace Shoko.Server.Models.Shoko;

public class StoredRelocationPreset : IStoredRelocationPreset
{
    private int _storedRelocationPresetID;

    private Guid? _id;

    #region Database Fields

    /// <summary>
    /// Local ID of the relocation preset. Used for database primary key and to
    /// construct the GUID.
    /// </summary>
    public int StoredRelocationPresetID
    {
        get => _storedRelocationPresetID;
        set
        {
            _id = null;
            _storedRelocationPresetID = value;
        }
    }

    public string Name { get; set; } = string.Empty;

    public Guid ProviderID { get; set; }

    public byte[]? Configuration { get; set; }

    #endregion

    public Guid ID
        => _id ??= UuidUtility.GetV5($"StoredRelocationPipe-{StoredRelocationPresetID}");

    public bool IsDefault
        => ISettingsProvider.Instance.GetSettings().Plugins.Renamer.DefaultRenamer == Name;
}
