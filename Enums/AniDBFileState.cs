using System;

namespace Shoko.Models.Enums
{
    [Flags]
    public enum AniDBFileState
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
    }
}