using NHibernate;

namespace JMMServer.Repositories.NHibernate
{
    internal static class StatelessSessionExtensions
    {
        public static ISessionWrapper Wrap(this IStatelessSession session)
        {
            return new StatelessSessionWrapper(session);
        }
    }
}