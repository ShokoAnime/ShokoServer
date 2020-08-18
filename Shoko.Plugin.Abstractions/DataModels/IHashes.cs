namespace Shoko.Plugin.Abstractions.DataModels
{
    public interface IHashes
    {
        string CRC { get; }
        string MD5 { get; }
        string ED2K { get; }
        string SHA1 { get; }
    }
}