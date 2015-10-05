using System;

namespace JMMDatabase
{
    [Flags]
    public enum UpdateType
    {
        None=0,
        Properties=1,
        GroupFilter=2,
        User=4,
        ParentGroup=8,
        All=15
    }
}
