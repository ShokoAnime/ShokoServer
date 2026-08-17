namespace Shoko.Server.API.v2.Models.core;

public class Credentials
{
    public string login { get; set; } = null!;
    public string password { get; set; } = null!;
    public ushort port { get; set; }
    public string token { get; set; } = null!;
    public string refresh_token { get; set; } = null!;
    public string apikey { get; set; } = null!;
    public ushort apiport { get; set; }
}
