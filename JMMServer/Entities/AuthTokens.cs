namespace JMMServer.Entities
{
    public class AuthTokens
    {
        public int AuthID { get; set; }
        public int UserID { get; set; }
        public string DeviceName { get; set; }
        public string Token { get; set; }

        public AuthTokens(int userId, string deviceName, string token)
        {
            UserID = userId;
            DeviceName = deviceName;
            Token = token;
        }

        public AuthTokens()
        {

        }
    }
}
