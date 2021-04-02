using System;

namespace Shoko.Server.Providers.AniDB.UDP
{
    // This file will have a lot of repeated enums, but it's more specific, and eventually, the others should be removed
    [Flags]
    public enum GetFile_State
    {
        None = 0,
        FILE_CRCOK = 1, //file matched official CRC (displayed with green background in AniDB)
        FILE_CRCERR = 2, // file DID NOT match official CRC (displayed with red background in AniDB)
        FILE_ISV2 = 4, // file is version 2
        FILE_ISV3 = 8, // file is version 3
        FILE_ISV4 = 16, // file is version 4
        FILE_ISV5 = 32, // file is version 5
        FILE_UNC = 64, // file is uncensored
        FILE_CEN = 128, // file is censored
        FILE_CHAPTERED = 4096 // file is chaptered, 0 means both not set and false
    }

    public enum GetFile_Quality
    {
        Unknown,
        VeryHigh,
        High,
        Medium,
        Low,
        VeryLow,
        Corrupted,
        EyeCancer
    }

    public enum GetFile_Source
    {
        Unknown,
        TV,
        Web,
        DVD,
        BluRay,
        VHS,
        HDDVD,
        HKDVD,
        HDTV,
        DTV,
        Camcorder,
        VCD,
        SVCD,
        LaserDisc
    }
    
    public enum MyList_State
    {
        Unknown,
        HDD,
        Disk,
        Deleted,
        Remote
    }

    public enum MyList_FileState
    {
        Normal = 0,
        Corrupted = 1,
        Self_Edited = 2,
        Self_Ripped = 10,
        On_DVD = 11,
        On_VHS = 12,
        On_TV = 13,
        In_Theaters = 14,
        Streamed = 15,
        Other = 100
    }
}
