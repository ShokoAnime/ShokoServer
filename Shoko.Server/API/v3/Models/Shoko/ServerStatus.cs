namespace Shoko.Server.API.v3
{
    public class ServerStatus
    {
        /// <summary>
        /// The progress message for starting up
        /// </summary>
        public string StartupMessage { get; set; }

        /// <summary>
        /// The state of startup.
        /// </summary>
        public StartupState State { get; set; }

        /// <summary>
        /// Uptime in hh:mm:ss or null if not started. Uses hours may be greater than a day.
        /// </summary>
        public string Uptime { get; set; }

        public enum StartupState
        {
            /// <summary>
            /// Starting up
            /// </summary>
            Starting = 1,
            /// <summary>
            /// Finished starting
            /// </summary>
            Started = 2,
            /// <summary>
            /// There was an error while starting
            /// </summary>
            Failed = 3,
            /// <summary>
            /// Waiting for setup
            /// </summary>
            Waiting = 4
        }
    }
}