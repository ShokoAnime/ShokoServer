using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Shoko.Database;
using Shoko.Database.Models;

namespace Shoko.CLI
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");
            ShokoDBContext ctx = new ShokoDBContext();
            VideoLocalPlace vlp = ctx.VideoLocalPlace.First(x => x.VideoLocalPlaceId == 69);
            VideoLocal vl = vlp.VideoLocal; //null

            var test = ctx.AniDbAnime.First();
            var test1 = ctx.AniDbAnimeUpdate.First();

        }
    }
}
