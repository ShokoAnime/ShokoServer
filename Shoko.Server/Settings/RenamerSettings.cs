using System.Collections.Generic;

namespace Shoko.Server.Settings;

public class RenamerSettings
{
    public Dictionary<string, bool> EnabledRenamers { get; set; } = [];

    public bool RenameOnImport { get; set; } = false;

    public bool MoveOnImport { get; set; } = false;

    public string DefaultRenamer { get; set; } = "Default";
}
