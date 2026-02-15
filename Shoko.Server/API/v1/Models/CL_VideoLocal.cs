using System;
using System.Collections.Generic;

namespace Shoko.Server.API.v1.Models;

public class CL_VideoLocal
{
    public int VideoLocalID { get; set; }
    public string Hash { get; set; }
    public string CRC32 { get; set; }
    public string MD5 { get; set; }
    public string SHA1 { get; set; }
    public int HashSource { get; set; }
    public long FileSize { get; set; }
    public int IsIgnored { get; set; }
    public DateTime DateTimeUpdated { get; set; }
    public DateTime DateTimeCreated { get; set; }
    public DateTime? DateTimeImported { get; set; }
    public int IsVariation { get; set; }
    public string FileName { get; set; }
    public int IsWatched { get; set; }
    public DateTime? WatchedDate { get; set; }
    public long ResumePosition { get; set; }
    public List<CL_VideoLocal_Place> Places { get; set; }
    public CL_Media Media { get; set; }
    public long Duration { get; set; }
}
