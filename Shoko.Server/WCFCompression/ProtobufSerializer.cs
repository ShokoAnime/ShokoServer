using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;
using System.ServiceModel.Web;
using System.Text;
using System.Threading.Tasks;
using ProtoBuf;
using ProtoBuf.Meta;

namespace Shoko.Server.WCFCompression
{
    public class ProtobufSerializer : ISerializer
    {
        public Message Serialize(OperationDescription operation, MessageVersion version, object[] parameters, object result)
        {
            byte[] body=null;
            using (var ms = new MemoryStream())
            {
                Type t = result.GetType();
                ProtoBuf.Serializer.Serialize(ms,result);
                body = ms.ToArray();
            }
            System.ServiceModel.Channels.Message replyMessage = System.ServiceModel.Channels.Message.CreateMessage(version, operation.Messages[1].Action, new RawBodyWriter(body));
            replyMessage.Properties.Add(WebBodyFormatMessageProperty.Name, new WebBodyFormatMessageProperty(WebContentFormat.Raw));
            var respProp = new HttpResponseMessageProperty();
            respProp.Headers[HttpResponseHeader.ContentType] = "application/x-protobuf";
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
            {
                if (parameters.Length == 1)
                {
                    
                    // single parameter, assuming bare
                    parameters[0] = ProtoBuf.Meta.RuntimeTypeModel.Default.Deserialize(ms, null, operation.Messages[0].Body.Parts[0].Type);
                }
                else
                {
                    throw new InvalidOperationException("We don't support multiple protobuf parameters");                    
                }
                ms.Close();
            }
        }
    }
}
