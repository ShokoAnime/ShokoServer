using System.Net;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Dispatcher;
using System.ServiceModel.Web;
using System.Text.RegularExpressions;

namespace JMMServer.WCFCompression
{
    
    class CompressionSelectionMessageInspector : IDispatchMessageInspector
    {
        static readonly Regex jsonContentTypes = new Regex(@"[application|text]\/json");
        static readonly Regex protoContentTypes = new Regex(@"[application\/x\-|application\/vnd\.google\.]protobuf");

        public object AfterReceiveRequest(ref Message request, IClientChannel channel, InstanceContext instanceContext)
        {
            PassObject p=new PassObject();
            p.CompressionType=CompressionType.None;
            p.ContentType = "application/xml";
            object propObj;
            if (request.Properties.TryGetValue(HttpRequestMessageProperty.Name, out propObj))
            {
                var prop = (HttpRequestMessageProperty)propObj;
                var accept = prop.Headers[HttpRequestHeader.Accept];
                if (accept != null)
                {
                    if (jsonContentTypes.IsMatch(accept))
                    {
                        p.ContentType = "application/json";
                    }
                    else if (protoContentTypes.IsMatch(accept))
                    {
                        p.ContentType = "application/x-protobuff";
                    }
                }

                var acceptEncoding = prop.Headers[HttpRequestHeader.AcceptEncoding];
                if (acceptEncoding != null)
                {
                    if (acceptEncoding.Contains("gzip"))
                        p.CompressionType = CompressionType.Gzip;
                }
            }
            return p;
        }

        public void BeforeSendReply(ref Message reply, object correlationState)
        {
            PassObject p = (PassObject) correlationState;
            HttpResponseMessageProperty resp;
            object respObj;
            if (!reply.Properties.TryGetValue(HttpResponseMessageProperty.Name, out respObj))
            {
                resp = new HttpResponseMessageProperty();
                reply.Properties.Add(HttpResponseMessageProperty.Name, resp);
            }
            else
            {
                resp = (HttpResponseMessageProperty)respObj;
            }
            if (p.CompressionType == CompressionType.Gzip)
            {
                resp.Headers[HttpResponseHeader.ContentEncoding] = "gzip";
            }
            resp.Headers[HttpResponseHeader.ContentType] = p.ContentType;
        }

        public class PassObject
        {
            public CompressionType CompressionType { get; set; }
            public string ContentType { get; set; }
        }
    }
}