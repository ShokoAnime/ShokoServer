namespace Shoko.Models.Interfaces
{
    public interface IHash
    {
        string ED2KHash { get; set; }
        long FileSize { get; set; }
        int MyListID { get; set; }

        string Info { get; }
    }


}