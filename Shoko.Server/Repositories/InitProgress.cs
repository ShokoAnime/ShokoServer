
using System;

namespace Shoko.Server.Repositories
{
    public class InitProgress : EventArgs
    {
        public string Title { get; set; }
        public int Step { get; set; }
        public int Total { get; set; }
    }
}
