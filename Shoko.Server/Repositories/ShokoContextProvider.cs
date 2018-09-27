using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Shoko.Server.Databases;

namespace Shoko.Server.Repositories
{
    public class ShokoContextProvider
    {
        private ThreadLocal<ShokoContext> _instances;
        public ShokoContextProvider(DatabaseTypes type, string connectionstring)
        {
            _instances = new ThreadLocal<ShokoContext>(() => new ShokoContext(type,connectionstring));
        }

        public ShokoContext GetContext() => _instances.Value;
    }
}
