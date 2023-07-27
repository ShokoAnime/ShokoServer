using System;

namespace Shoko.Server.Models;

// Normally this would be in models, but clients don't need to know about it.
public class AniDB_FileUpdate
{
    public int AniDB_FileUpdateID { get; set; }
    public long FileSize { get; set; }
    public string Hash { get; set; }
    public bool HasResponse { get; set; }
    public DateTime UpdatedAt { get; set; }
}
