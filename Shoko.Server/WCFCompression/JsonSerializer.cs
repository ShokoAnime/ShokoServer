using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;
using System.ServiceModel.Web;
using System.Text;
using System.Threading.Tasks;

namespace Shoko.Server.WCFCompression
{
    public class JsonSerializer : ISerializer
    {
        public Message Serialize(OperationDescription operation, MessageVersion version, object[] parameters, object result)
        {
            byte[] body;
            var serializer = new Newtonsoft.Json.JsonSerializer();

            using (var ms = new MemoryStream())
            {
                using (var sw = new StreamWriter(ms, Encoding.UTF8))
                {
                    using (Newtonsoft.Json.JsonWriter writer = new Newtonsoft.Json.JsonTextWriter(sw))
                    {
                        //writer.Formatting = Newtonsoft.Json.Formatting.Indented;
                        serializer.Serialize(writer, result);
                        sw.Flush();
                        body = ms.ToArray();
                    }
                }
            }
            System.ServiceModel.Channels.Message replyMessage = System.ServiceModel.Channels.Message.CreateMessage(version, operation.Messages[1].Action, new RawBodyWriter(body));
            replyMessage.Properties.Add(WebBodyFormatMessageProperty.Name, new WebBodyFormatMessageProperty(WebContentFormat.Raw));
            var respProp = new HttpResponseMessageProperty();
            respProp.Headers[HttpResponseHeader.ContentType] = "application/json";
            replyMessage.Properties.Add(HttpResponseMessageProperty.Name, respProp);
            return replyMessage;
        }

        public void Deserialize(OperationDescription operation, Dictionary<string, int> parameterNames, Message message, object[] parameters)
        {
            object bodyFormatProperty;
            if (!message.Properties.TryGetValue(WebBodyFormatMessageProperty.Name, out bodyFormatProperty) ||
                (bodyFormatProperty as WebBodyFormatMessageProperty).Format != WebContentFormat.Raw)
            {
                throw new InvalidOperationException(
                    "Incoming messages must have a body format of Raw. Is a ContentTypeMapper set on the WebHttpBinding?");
            }

            var bodyReader = message.GetReaderAtBodyContents();
            bodyReader.ReadStartElement("Binary");
            byte[] rawBody = bodyReader.ReadContentAsBase64();
            using (var ms = new MemoryStream(rawBody))
            using (var sr = new StreamReader(ms))
            {
                var serializer = new Newtonsoft.Json.JsonSerializer();
                if (parameters.Length == 1)
                {
                    // single parameter, assuming bare
                    parameters[0] = serializer.Deserialize(sr, operation.Messages[0].Body.Parts[0].Type);
                }
                else
                {
                    // multiple parameter, needs to be wrapped
                    Newtonsoft.Json.JsonReader reader = new Newtonsoft.Json.JsonTextReader(sr);
                    reader.Read();
                    if (reader.TokenType != Newtonsoft.Json.JsonToken.StartObject)
                    {
                        throw new InvalidOperationException("Input needs to be wrapped in an object");
                    }

                    reader.Read();
                    while (reader.TokenType == Newtonsoft.Json.JsonToken.PropertyName)
                    {
                        var parameterName = reader.Value as string;
                        reader.Read();
                        if (parameterNames.ContainsKey(parameterName))
                        {
                            int parameterIndex = parameterNames[parameterName];
                            parameters[parameterIndex] = serializer.Deserialize(reader,
                                operation.Messages[0].Body.Parts[parameterIndex].Type);
                        }
                        else
                        {
                            reader.Skip();
                        }

                        reader.Read();
                    }

                    reader.Close();
                }
                sr.Close();
                ms.Close();
            }
        }
    }
}
