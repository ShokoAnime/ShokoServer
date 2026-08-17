namespace Shoko.Server.Providers.AniDB.UDP.Connection;

public class ResponseLogin
{
    public string SessionID { get; set; } = null!;
    public string? ImageServer { get; set; }
}
