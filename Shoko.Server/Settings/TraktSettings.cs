using Shoko.Models.Enums;

namespace Shoko.Server.Settings
{
    public class TraktSettings
    {
        public bool Enabled { get; set; } = false;

        public string PIN { get; set; } = string.Empty;

        public string AuthToken { get; set; } = string.Empty;

        public string RefreshToken { get; set; } = string.Empty;

        public string TokenExpirationDate { get; set; } = string.Empty;

        public ScheduledUpdateFrequency UpdateFrequency { get; set; } = ScheduledUpdateFrequency.Daily;

        public ScheduledUpdateFrequency SyncFrequency { get; set; } = ScheduledUpdateFrequency.Daily;
    }
}