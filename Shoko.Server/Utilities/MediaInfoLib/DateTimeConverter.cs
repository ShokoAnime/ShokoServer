using System;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using NLog;

namespace Shoko.Server.Utilities.MediaInfoLib
{
    public class DateTimeConverter : IsoDateTimeConverter
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();
        
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (!(existingValue is string existingString)) return null;
            try
            {
                existingString = existingString.Replace("T", " ");
                existingString = existingString.Replace("Z", "");
                string[] strings = existingString.Split(new[] {" / "}, StringSplitOptions.RemoveEmptyEntries);
                if (strings.Length == 1)
                {
                    return string.IsNullOrEmpty(DateTimeFormat)
                        ? DateTime.Parse(strings[0], Culture, DateTimeStyles)
                        : DateTime.ParseExact(strings[0], DateTimeFormat, Culture, DateTimeStyles);
                }

                if (strings.Length > 1)
                {
                    // Return Min when we can't match a pattern, in the hopes that one matches.
                    if(string.IsNullOrEmpty(DateTimeFormat))
                    {
                        DateTime result = strings.Max(a =>
                            DateTime.TryParse(a, Culture, DateTimeStyles, out DateTime date)
                                ? date
                                : DateTime.MinValue);

                        if (result == DateTime.MinValue) throw new JsonReaderException();
                        return result;
                    }
                    else
                    {
                        DateTime result = strings.Max(a =>
                            DateTime.TryParseExact(a, DateTimeFormat, Culture, DateTimeStyles, out DateTime date)
                                ? date
                                : DateTime.MinValue);

                        if (result == DateTime.MinValue) throw new JsonReaderException();
                        return result;
                    }
                }
            }
            catch (Exception e)
            {
                logger.Error($"Error converting object to Date. The value was: {existingString}. Exception: {e}");
            }

            return null;
        }
    }
}