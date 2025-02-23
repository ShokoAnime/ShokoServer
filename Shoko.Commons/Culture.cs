using System.Globalization;

namespace Shoko.Commons
{
    public static class Culture
    {
        public static CultureInfo Global { get; set; } = CultureInfo.CurrentCulture;
    }
}
