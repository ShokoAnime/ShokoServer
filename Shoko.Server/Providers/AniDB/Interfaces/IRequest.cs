namespace Shoko.Server.Providers.AniDB.Interfaces
{
    public interface IRequest<out T> where T : IResponse
    {
        T Execute();
    }
}
