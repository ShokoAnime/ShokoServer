
using System;

namespace Shoko.Server.Repositories
{
    public class InitProgress
    {
        public string Title { get; set; }
        public int Step { get; set; }
        public int Total { get; set; }
    }
    [Flags]
    public enum RepositoryCache
    {
        SupportsCache,
        SupportsDirectAccess
    }
}
