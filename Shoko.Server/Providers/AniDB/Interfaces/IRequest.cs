using System.Threading.Tasks;

namespace Shoko.Server.Providers.AniDB.Interfaces;

public interface IRequest<T, T1> where T : IResponse<T1> where T1 : class
{
    Task<T> Send();
}

public interface IRequest
{
    object Send();
}
