using Shoko.Plugin.Abstractions.Enums;

namespace Shoko.Plugin.Abstractions.Extensions
{
    public static class NetworkAvailabilityExtensions
    {
        public static bool HasInternet(this NetworkAvailability value)
            => value is NetworkAvailability.Internet or NetworkAvailability.PartialInternet;
    }
}
