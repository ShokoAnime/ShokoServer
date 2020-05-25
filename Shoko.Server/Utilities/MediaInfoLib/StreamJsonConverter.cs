using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using Shoko.Models.MediaInfo;

namespace Shoko.Server.Utilities.MediaInfoLib
{
    public class StreamJsonConverter : JsonConverter
        {
            public override bool CanConvert(Type objectType)
            {
                return objectType == typeof(Stream);
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                if (reader.TokenType == JsonToken.Null)
                    return null;

                var obj = JObject.Load(reader);

                Stream stream;

                string type = obj.GetValue("@type", StringComparison.OrdinalIgnoreCase)?.ToString();

                if (type == null) type = obj.GetValue("type", StringComparison.OrdinalIgnoreCase)?.ToString();

                if (type == "General" || type == ((int)StreamType.General).ToString())
                {
                    JsonObjectContract contract =
                        (JsonObjectContract) serializer.ContractResolver.ResolveContract(typeof(GeneralStream));
                    stream = existingValue as GeneralStream ?? (GeneralStream) contract.DefaultCreator?.Invoke();
                }
                else if (type == "Video" || type == ((int)StreamType.Video).ToString())
                {
                    JsonObjectContract contract =
                        (JsonObjectContract) serializer.ContractResolver.ResolveContract(typeof(VideoStream));
                    stream = existingValue as VideoStream ?? (VideoStream) contract.DefaultCreator?.Invoke();
                }
                else if (type == "Audio" || type == ((int)StreamType.Audio).ToString())
                {
                    JsonObjectContract contract =
                        (JsonObjectContract) serializer.ContractResolver.ResolveContract(typeof(AudioStream));
                    stream = existingValue as AudioStream ?? (AudioStream) contract.DefaultCreator?.Invoke();
                }
                else if (type == "Text" || type == ((int)StreamType.Text).ToString())
                {
                    JsonObjectContract contract =
                        (JsonObjectContract) serializer.ContractResolver.ResolveContract(typeof(TextStream));
                    stream = existingValue as TextStream ?? (TextStream) contract.DefaultCreator?.Invoke();
                }
                else if (type == "Menu" || type == ((int)StreamType.Menu).ToString())
                {
                    JsonObjectContract contract =
                        (JsonObjectContract) serializer.ContractResolver.ResolveContract(typeof(MenuStream));
                    stream = existingValue as MenuStream ?? (MenuStream) contract.DefaultCreator?.Invoke();
                }
                else
                    return null;

                if (stream == null) return null;

                using (var subReader = obj.CreateReader())
                {
                    serializer.Populate(subReader, stream);
                }
                return stream;
            }

            public override bool CanWrite => false;

            public override bool CanRead => true;

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                throw new NotSupportedException();
            }
        }
}
