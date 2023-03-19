namespace Shoko.Server.Settings;

public class HasherSettings
{
    public bool MD5 { get; set; } = true;
    public bool SHA1 { get; set; } = true;
    public bool CRC { get; set; } = true;
    public bool ForceGeneratesAllHashes { get; set; }
}
