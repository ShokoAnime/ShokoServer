using System;
using Shoko.Abstractions.Relocation;
using Shoko.Abstractions.Utilities;
using Shoko.Server.Utilities;

#nullable enable
namespace Shoko.Server.Models.Shoko;

public class StoredRelocationPipe : IStoredRelocationPipe
{
    private int _storedRelocationPipeID;

    private Guid? _id;

    #region Database Fields

    /// <summary>
    /// Local ID of the relocation pipe. Used for database primary key and to
    /// construct the GUID.
    /// </summary>
    public int StoredRelocationPipeID
    {
        get => _storedRelocationPipeID;
        set
        {
            _id = null;
            _storedRelocationPipeID = value;
        }
    }

    public string Name { get; set; } = string.Empty;

    public Guid ProviderID { get; set; }

    public byte[]? Configuration { get; set; }

    #endregion

    public Guid ID
        => _id ??= UuidUtility.GetV5($"StoredRelocationPipe-{StoredRelocationPipeID}");

    public bool IsDefault
        => Utils.SettingsProvider.GetSettings().Plugins.Renamer.DefaultRenamer == Name;
}
