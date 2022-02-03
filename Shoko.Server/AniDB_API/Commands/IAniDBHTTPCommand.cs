namespace AniDBAPI.Commands
{
    public interface IAniDBHTTPCommand
    {
        AniDBUDPResponseCode GetStartEventType();
        AniDBUDPResponseCode Process();
        string GetKey();
    }
}