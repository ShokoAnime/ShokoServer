namespace AniDBAPI.Commands
{
    public interface IAniDBHTTPCommand
    {
        enHelperActivityType GetStartEventType();
        enHelperActivityType Process();
        string GetKey();
    }
}