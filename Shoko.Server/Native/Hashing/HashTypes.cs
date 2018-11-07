using System;

namespace Shoko.Server.Native.Hashing
{
    [Flags]
    public enum HashTypes
    {
        ED2K=1,
        CRC=2,
        MD5=4,
        SHA1=8
    }
}