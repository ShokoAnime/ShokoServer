using System;
using System.Globalization;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Shoko.Models.PlexAndKodi;

namespace MediaInfoWrapper
{
    internal class MediaInfoWrapper
    {
        public static void Main(string[] args)
        {
            // Allow UNICODE!!
            bool asciiOnly;
            try
            {
                // try UTF-16
                Console.OutputEncoding = Encoding.Unicode;
                asciiOnly = false;
            }
            catch
            {
                try
                {
                    Console.OutputEncoding = Encoding.UTF8;
                    asciiOnly = false;
                }
                catch
                {
                    Console.Error.WriteLine("Unable to set console output to unicode. This may not be a problem, but Stream titles with unicode will be stripped.");
                    asciiOnly = true;
                }
            }
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
                if (asciiOnly)
                {
                    var bytes = Encoding.Unicode.GetBytes(json);
                    var qmASCIIEncoder = Encoding.GetEncoding(Encoding.ASCII.EncodingName,
                        new EncoderReplacementFallback("?"),
                        new DecoderExceptionFallback());
                    json = Encoding.ASCII.GetString(Encoding.Convert(Encoding.Unicode, qmASCIIEncoder, bytes));
                }
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
