using System;
using System.Collections.Generic;
using System.Text;

namespace Shoko.Plugin.Abstractions
{
    public interface IRenamerProvider
    {
        public IEnumerable<IRenamer> GetRenamerInstances();
    }
}
