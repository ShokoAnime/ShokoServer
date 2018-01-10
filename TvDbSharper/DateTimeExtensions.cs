namespace TvDbSharper
{
    using System;

    public static class DateTimeExtensions
    {
        private static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public static int ToUnixEpochTime(this DateTime time)
        {
            return (int)(time - Epoch).TotalSeconds;
        }
    }

    public static class LongExtensions
    {
        private static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public static DateTime ToDateTime(this long? seconds)
        {
            return Epoch.AddSeconds(seconds.Value);
        }

        public static DateTime ToDateTime(this long seconds)
        {
            return Epoch.AddSeconds(seconds);
        }
    }
}