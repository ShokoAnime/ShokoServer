using System;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using NLog;

namespace Shoko.Server.Utilities.MediaInfoLib;

public class DateTimeConverter : IsoDateTimeConverter
{
    private static Logger logger = LogManager.GetCurrentClassLogger();

    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
        if (!(reader.Value is string existingString))
        {
            return existingValue;
        }

        try
        {
            existingString = existingString.Replace("UTC", "");
            existingString = existingString.Replace("T", " ");
            existingString = existingString.Replace("Z", "");
            var strings = existingString.Split(new[] { " / " }, StringSplitOptions.RemoveEmptyEntries);
            if (strings.Length == 1)
            {
                return string.IsNullOrEmpty(DateTimeFormat)
                    ? DateTime.Parse(strings[0].Trim(), Culture, DateTimeStyles)
                    : DateTime.ParseExact(strings[0].Trim(), DateTimeFormat, Culture, DateTimeStyles);
            }

            if (strings.Length > 1)
            {
                // Return Min when we can't match a pattern, in the hopes that one matches.
                if (string.IsNullOrEmpty(DateTimeFormat))
                {
                    var result = strings.Max(a =>
                        DateTime.TryParse(a.Trim(), Culture, DateTimeStyles, out var date)
                            ? date
                            : DateTime.MinValue);

                    if (result == DateTime.MinValue)
                    {
                        throw new JsonReaderException();
                    }

                    return result;
                }
                else
                {
                    var result = strings.Max(a =>
                        DateTime.TryParseExact(a.Trim(), DateTimeFormat, Culture, DateTimeStyles, out var date)
                            ? date
                            : DateTime.MinValue);

                    if (result == DateTime.MinValue)
                    {
                        throw new JsonReaderException();
                    }

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

    public override bool CanRead => true;

    public override bool CanWrite => false;
}
