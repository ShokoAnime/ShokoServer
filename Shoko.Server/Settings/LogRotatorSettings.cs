namespace Shoko.Server.Settings
{
    public class LogRotatorSettings
    {
        public bool Enabled { get; set; } = true;

        public bool Zip { get; set; } = true;

        public bool Delete { get; set; } = true;

        public string Delete_Days { get; set; } = "";
    }
}