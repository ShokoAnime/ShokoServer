using System;

namespace Shoko.Server.Repositories
{
    [Flags]
    public enum RepositoryCache
    {
        SupportsCache,
        SupportsDirectAccess
    }
}