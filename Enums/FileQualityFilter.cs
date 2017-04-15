namespace Shoko.Models
{
    public static class FileQualityFilter
    {
        public enum FileQualityFilterType
        {
            RESOLUTION,
            SOURCE,
            VERSION,
            AUDIOSTREAMCOUNT,
            VIDEOCODEC,
            AUDIOCODEC,
            CHAPTER,
            SUBGROUP,
            SUBSTREAMCOUNT
        }

        public enum FileQualityFilterOperationType
        {
            EQUALS,
            LESS_EQ,
            GREATER_EQ,
            IN,
            NOTIN
        }
    }
}