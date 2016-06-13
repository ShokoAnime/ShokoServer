using System.Net;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Dispatcher;

namespace JMMServer.WCFCompression
{
    class CompressionSelectionMessageInspector : IDispatchMessageInspector
    {
        public object AfterReceiveRequest(ref Message request, IClientChannel channel, InstanceContext instanceContext)
        {
            CompressionType type = CompressionType.None;

            object propObj;
            if (request.Properties.TryGetValue(HttpRequestMessageProperty.Name, out propObj))
            {
                var prop = (HttpRequestMessageProperty) propObj;
                var acceptEncoding = prop.Headers[HttpRequestHeader.AcceptEncoding];
                if (acceptEncoding != null)
                {
                    if (acceptEncoding.Contains("gzip"))
                        type = CompressionType.Gzip;
                }
            }
            return type;
        }

        public void BeforeSendReply(ref Message reply, object correlationState)
        {
            CompressionType type = (CompressionType) correlationState;
            if (type == CompressionType.Gzip)
            {
                // Add property to be used by encoder
                HttpResponseMessageProperty resp;
                object respObj;
                if (!reply.Properties.TryGetValue(HttpResponseMessageProperty.Name, out respObj))
                {
                    resp = new HttpResponseMessageProperty();
                    reply.Properties.Add(HttpResponseMessageProperty.Name, resp);
                }
                else
                {
                    resp = (HttpResponseMessageProperty) respObj;
                }
                resp.Headers[HttpResponseHeader.ContentEncoding] = "gzip";
            }
        }
    }
}