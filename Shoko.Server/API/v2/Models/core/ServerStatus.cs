namespace Shoko.Server.API.v2.Models.core
{
    public class ServerStatus
    {
        // The message that is usually displayed at the top of the server UI during startup
        public string startup_state { get; set; }
        // Is the server running
        public bool server_started { get; set; }
        // Is the first run setting flag marked
        public bool first_run { get; set; }
        // Did the server fail to start
        public bool startup_failed { get; set; }
        // Why did it fail
        public string startup_failed_error_message { get; set; }
    }
}