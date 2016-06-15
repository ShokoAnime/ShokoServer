using JMMContracts;

namespace JMMServer.Entities
{
    public class Playlist
    {
        public int PlaylistID { get; private set; }
        public string PlaylistName { get; set; }
        public string PlaylistItems { get; set; }
        public int DefaultPlayOrder { get; set; }
        public int PlayWatched { get; set; }
        public int PlayUnwatched { get; set; }

        public override string ToString()
        {
            return string.Format("Playlist: {0} - ({1})", PlaylistName, PlaylistItems);
        }

        public Contract_Playlist ToContract()
        {
            Contract_Playlist contract = new Contract_Playlist();

            contract.PlaylistID = PlaylistID;
            contract.PlaylistName = PlaylistName;
            contract.PlaylistItems = PlaylistItems;
            contract.DefaultPlayOrder = DefaultPlayOrder;
            contract.PlayWatched = PlayWatched;
            contract.PlayUnwatched = PlayUnwatched;

            return contract;
        }
    }
}