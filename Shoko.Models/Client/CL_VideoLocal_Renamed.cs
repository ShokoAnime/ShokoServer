namespace Shoko.Models.Client
{
    public class CL_VideoLocal_Renamed
    {
        public int VideoLocalID { get; set; }
        public CL_VideoLocal VideoLocal { get; set; }
        public string NewFileName { get; set; }
        public string NewDestination { get; set; } // null if not moved, string with error if errored
        public bool Success { get; set; }
    }
}