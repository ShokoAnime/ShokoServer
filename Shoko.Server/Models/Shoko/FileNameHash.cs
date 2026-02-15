using System;

#nullable enable
namespace Shoko.Server.Models.Shoko;

public class FileNameHash
{
    public int FileNameHashID { get; set; }

    public string FileName { get; set; } = string.Empty;

    public long FileSize { get; set; }

    public string Hash { get; set; } = string.Empty;

    public DateTime DateTimeUpdated { get; set; }
}
