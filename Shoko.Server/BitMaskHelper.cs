namespace Shoko.Server
{
    public static class BitMaskHelper
    {
        public static bool IsSet<T>(T flags, T flag) where T : struct
        {
            int flagsValue = (int) (object) flags;
            int flagValue = (int) (object) flag;

            return (flagsValue & flagValue) != 0;
        }

        public static void Set<T>(ref T flags, T flag) where T : struct
        {
            int flagsValue = (int) (object) flags;
            int flagValue = (int) (object) flag;

            flags = (T) (object) (flagsValue | flagValue);
        }

        public static void Unset<T>(ref T flags, T flag) where T : struct
        {
            int flagsValue = (int) (object) flags;
            int flagValue = (int) (object) flag;

            flags = (T) (object) (flagsValue & ~flagValue);
        }
    }
}