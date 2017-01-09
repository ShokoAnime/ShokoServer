namespace AniDBAPI
{
    public interface IHash
    {
        string ED2KHash { get; set; }
        long FileSize { get; set; }
        string Info { get; }
    }

    public class IHashDummy : IHash
    {
        public long FileSize { get; set; }
        public string ED2KHash { get; set; }
        public string Info { get; set; }
    }
}