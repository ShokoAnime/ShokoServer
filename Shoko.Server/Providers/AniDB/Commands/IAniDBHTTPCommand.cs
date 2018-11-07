namespace Shoko.Server.Providers.AniDB.Commands
{
    public interface IAniDBHTTPCommand
    {
        enHelperActivityType GetStartEventType();
        enHelperActivityType Process();
        string GetKey();
    }
}