using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shoko.Server.Settings.DI
{
    public interface IConfiguration<T> : IDisposable
    {
        public T Instance { get; }
    }
}
