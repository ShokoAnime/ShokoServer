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
using System.Xml.Serialization;
using Shoko.Server.PlexAndKodi;

namespace Shoko.Server.WCFCompression
{
    public class XmlSerializer : ISerializer
    {
        public Message Serialize(OperationDescription operation, MessageVersion version, object[] parameters, object result)
        {
            byte[] body;
            var serializer = new System.Xml.Serialization.XmlSerializer(result.GetType());
            XmlSerializerNamespaces ns = new XmlSerializerNamespaces();
            ns.Add("", "");
            using (var ms = new MemoryStream())
            {
                using (var sw = new StreamWriter(ms, Encoding.UTF8))
                {
                    //writer.Formatting = Newtonsoft.Json.Formatting.Indented;
                    serializer.Serialize(sw, result,ns);
                    sw.Flush();
                    body = ms.ToArray();
                }
            }

            System.ServiceModel.Channels.Message replyMessage = System.ServiceModel.Channels.Message.CreateMessage(version, operation.Messages[1].Action, new RawBodyWriter(body));
            replyMessage.Properties.Add(WebBodyFormatMessageProperty.Name, new WebBodyFormatMessageProperty(WebContentFormat.Raw));
            var respProp = new HttpResponseMessageProperty();
            respProp.Headers[HttpResponseHeader.ContentType] = "application/xml";
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
                var serializer = new System.Xml.Serialization.XmlSerializer(operation.Messages[0].Body.Parts[0].Type);
                if (parameters.Length == 1)
                {
                    // single parameter, assuming bare
                    parameters[0] = serializer.Deserialize(sr);
                }
                else
                {
                    throw new InvalidOperationException("We don't support multiple xml parameters");                    
                }
                sr.Close();
                ms.Close();
            }
        }
    }
}
