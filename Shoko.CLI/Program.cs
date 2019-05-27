using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace Shoko.CLI
{
    class Program
    {
        static void Main(string[] args)
        {
            Shoko.Core.ShokoServer.SetupAutofac();
        }
    }
}
