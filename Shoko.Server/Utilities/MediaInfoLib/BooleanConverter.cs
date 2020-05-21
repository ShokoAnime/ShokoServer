using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using Shoko.Models.MediaInfo;

namespace Shoko.Server.Utilities.MediaInfoLib
{
    public class BooleanConverter : JsonConverter
        {
            public override bool CanConvert(Type objectType)
            {
                return objectType.IsAssignableFrom(typeof(bool));
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                if (reader.TokenType == JsonToken.Null)
                    return null;

                if (existingValue == null) return null;

                string value = existingValue as string;
                if (value == null) return existingValue;

                if (bool.TryParse(value, out bool result)) return result;

                if (value.Equals("yes", StringComparison.InvariantCultureIgnoreCase)) return true;
                if (value.Equals("no", StringComparison.InvariantCultureIgnoreCase)) return false;

                return existingValue;
            }

            public override bool CanWrite { get { return false; } }

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                throw new NotImplementedException();
            }
        }
}