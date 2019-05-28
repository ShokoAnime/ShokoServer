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
            Core.ShokoServer.Init();

            while (true)
            {
                System.Threading.Thread.Sleep(10_000);
            }
        }
    }
}
