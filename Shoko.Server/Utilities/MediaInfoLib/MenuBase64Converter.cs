using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Shoko.Server.Utilities.MediaInfoLib
{
    public class MenuBase64Converter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType.IsAssignableFrom(typeof(Dictionary<string,string>));
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return null;
            
            var obj = JObject.Load(reader);

            if (reader.Path != "extra") return obj.ToObject<Dictionary<string, string>>(); // Only continue if we are changing "extra" object in menu stream. Otherwise return as is.
            
            var sanitizedDictionary = new Dictionary<string, string>();

            foreach (var (key, value) in obj)
            {
                switch (value?.Type)
                {
                    case JTokenType.String:
                        sanitizedDictionary[key] = value?.ToString();
                        break;
                    case JTokenType.Object:
                    {
                        var base64EncodedBytes = Convert.FromBase64String(value?["#value"]?.ToString() ?? "");
                        sanitizedDictionary[key] = Encoding.UTF8.GetString(base64EncodedBytes).Trim();
                        break;
                    }
                    default:
                        throw new NotImplementedException();
                }
            }

            return sanitizedDictionary;
        }
        
        public override bool CanWrite => false;

        public override bool CanRead => true;

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }
}
