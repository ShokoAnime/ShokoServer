using System;

namespace Shoko.Models.Enums
{
    [Flags]
    public enum GroupFilterType
    {
        UserDefined = 1,
        ContinueWatching = 2,
        All = 4,
        Directory = 8,
        Tag = 16,
        Year = 32,
    }
}