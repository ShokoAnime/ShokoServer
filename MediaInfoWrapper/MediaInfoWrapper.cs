using System;
using System.Globalization;
using System.IO;
using Newtonsoft.Json;
using Shoko.Models.PlexAndKodi;

namespace MediaInfoWrapper
{
    internal class MediaInfoWrapper
    {
        public static void Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.Out.WriteLine("You must specify a file path. If desired, also a timeout in minutes.");
                return;
            }

            string path = args[0];
            if (!File.Exists(path))
            {
                Console.Out.WriteLine("The specified location does not exist.");
                return;
            }

            int timeout = 30;

            if (args.Length > 1)
            {
                string strTimeout = args[1];
                if (int.TryParse(strTimeout, out int time)) timeout = time;
            }

            try
            {
                Media m = MediaInfoParser.Convert(path, timeout);
                string json = JsonConvert.SerializeObject(m,
                    new JsonSerializerSettings
                    {
                        DefaultValueHandling = DefaultValueHandling.Include, Culture = CultureInfo.InvariantCulture,
                        Formatting = Formatting.None
                    });
                Console.Out.WriteLine(json);
            }
            catch (Exception e)
            {
                Console.Out.WriteLine("Error");
                Console.Out.WriteLine(e);
            }
        }
    }
}