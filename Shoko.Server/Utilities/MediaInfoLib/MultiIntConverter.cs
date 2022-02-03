using System;
using System.Linq;
using Newtonsoft.Json;
using NLog;

namespace Shoko.Server.Utilities.MediaInfoLib
{
    public class MultiIntConverter : JsonConverter
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotSupportedException();
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (!(reader.Value is string existingString)) return existingValue;
            try
            {
                string[] strings = existingString.Split(new[] {"/", " ", "-"}, StringSplitOptions.RemoveEmptyEntries);
                if (strings.Length == 1)
                {
                    if (int.TryParse(strings[0], out int result)) return result;
                    return existingValue;
                }

                if (strings.Length > 1)
                {
                    int max = strings.Max(a => int.TryParse(a, out int result) ? result : int.MinValue);
                    if (max == int.MinValue) return existingValue;
                    return max;
                }
            }
            catch (Exception e)
            {
                logger.Error($"Error converting object to Int. The value was: {existingString}. Exception: {e}");
            }

            return existingValue;
        }

        public override bool CanWrite => false;

        public override bool CanConvert(Type objectType)
        {
            return objectType.IsAssignableFrom(typeof(int));
        }
    }
}
