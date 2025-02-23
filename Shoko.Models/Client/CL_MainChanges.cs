using System;

namespace Shoko.Models.Client
{
    public class CL_MainChanges
    {
        public CL_Changes<CL_GroupFilter> Filters { get; set; }=new CL_Changes<CL_GroupFilter>();
        public CL_Changes<CL_AnimeGroup_User> Groups { get; set; }=new CL_Changes<CL_AnimeGroup_User>();
        public CL_Changes<CL_AnimeSeries_User> Series { get; set; }=new CL_Changes<CL_AnimeSeries_User>();
        public DateTime LastChange { get; set; }
    }
}
