using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;
using System.ServiceModel.Dispatcher;
using System.ServiceModel.Web;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;

namespace Shoko.Server.WCFCompression
{
    public class MultiDispatchFormatter : IDispatchMessageFormatter
    {
        private OperationDescription operation;
        private Dictionary<string, int> parameterNames;
        private ServiceEndpoint ep;


        internal Dictionary<int, string> pathMapping;
        internal Dictionary<int, KeyValuePair<string, Type>> queryMapping;

        QueryStringConverter qsc;
        int totalNumUTVars;
        UriTemplate uriTemplate;
        Uri baseAddress;


        static readonly Regex jsonContentTypes = new Regex(@"[application|text]\/json");
        static readonly Regex protoContentTypes = new Regex(@"[application\/x\-|application\/vnd\.google\.]protobuf");

        public MultiDispatchFormatter(OperationDescription operation, QueryStringConverter q, ServiceEndpoint e, bool isRequest)
        {
            this.operation = operation;
            if (isRequest)
            {
                int operationParameterCount = operation.Messages[0].Body.Parts.Count;
                if (operationParameterCount > 1)
                {
                    this.parameterNames = new Dictionary<string, int>();
                    for (int i = 0; i < operationParameterCount; i++)
                    {
                        this.parameterNames.Add(operation.Messages[0].Body.Parts[i].Name, i);
                    }
                }
            }
            ep = e;
            qsc = q;
            this.baseAddress = ep.Address.Uri;
            Populate(out this.pathMapping, out this.queryMapping, out this.totalNumUTVars, out this.uriTemplate, operation, qsc);
        }

        public void DeserializeRequest(Message message, object[] parameters)
        {
            object propObj;
            object[] bodyParameters = new object[parameters.Length - this.totalNumUTVars];

            if (bodyParameters.Length != 0)
            {

                if (message.Properties.TryGetValue(HttpRequestMessageProperty.Name, out propObj))
                {
                    var prop = (HttpRequestMessageProperty) propObj;
                    var contenttype = prop.Headers[HttpRequestHeader.ContentType];
                    if (contenttype != null)
                    {
                        if (jsonContentTypes.IsMatch(contenttype))
                        {
                            JsonSerializer js = new JsonSerializer();
                            js.Deserialize(operation, parameterNames, message, parameters);
                            return;
                        }
                        if (protoContentTypes.IsMatch(contenttype))
                        {
                            ProtobufSerializer js=new ProtobufSerializer();
                            js.Deserialize(operation, parameterNames, message, parameters);
                            return;
                        }
                    }
                }
                XmlSerializer x = new XmlSerializer();
                x.Deserialize(operation, parameterNames, message, parameters);
            }
            int j = 0;
            UriTemplateMatch utmr = null;
            string UTMRName = "UriTemplateMatchResults";
            if (message.Properties.ContainsKey(UTMRName))
            {
                utmr = message.Properties[UTMRName] as UriTemplateMatch;
            }
            else
            {
                if (message.Headers.To != null && message.Headers.To.IsAbsoluteUri)
                {
                    utmr = this.uriTemplate.Match(this.baseAddress, message.Headers.To);
                }
            }
            NameValueCollection nvc = (utmr == null) ? new NameValueCollection() : utmr.BoundVariables;
            for (int i = 0; i < parameters.Length; ++i)
            {
                if (this.pathMapping.ContainsKey(i) && utmr != null)
                {
                    parameters[i] = nvc[this.pathMapping[i]];
                }
                else if (this.queryMapping.ContainsKey(i) && utmr != null)
                {
                    string queryVal = nvc[this.queryMapping[i].Key];
                    parameters[i] = this.qsc.ConvertStringToValue(queryVal, this.queryMapping[i].Value);
                }
                else
                {
                    parameters[i] = bodyParameters[j];
                    ++j;
                }
            }
        }

        public Message SerializeReply(MessageVersion messageVersion, object[] parameters, object result)
        {
            Message request = OperationContext.Current.RequestContext.RequestMessage;
            object propObj;
            if (result is Stream)
            {
                byte[] data;
                if (result is MemoryStream)
                {
                    data = ((MemoryStream) result).ToArray();
                }
                else
                {
                    using (MemoryStream ms = new MemoryStream())
                    {
                        ((Stream)result).CopyTo(ms);
                        data=ms.ToArray();
                    }
                }
                System.ServiceModel.Channels.Message replyMessage = System.ServiceModel.Channels.Message.CreateMessage(messageVersion, operation.Messages[1].Action, new RawBodyWriter(data));
                replyMessage.Properties.Add(WebBodyFormatMessageProperty.Name, new WebBodyFormatMessageProperty(WebContentFormat.Raw));
                return replyMessage;
            }
            if (request.Properties.TryGetValue(HttpRequestMessageProperty.Name, out propObj))
            {
                var prop = (HttpRequestMessageProperty) propObj;
                var accept = prop.Headers[HttpRequestHeader.Accept];
                if (accept != null)
                {
                    if (jsonContentTypes.IsMatch(accept))
                    {
                        JsonSerializer j = new JsonSerializer();
                        return j.Serialize(operation, messageVersion, parameters, result);
                    }
                    if (protoContentTypes.IsMatch(accept))
                    {
                        ProtobufSerializer j=new ProtobufSerializer();
                        return j.Serialize(operation, messageVersion, parameters, result);
                    }
                }
            }
            XmlSerializer x=new XmlSerializer();
            return x.Serialize(operation, messageVersion, parameters, result);
        }

        private static void Populate(out Dictionary<int, string> pathMapping, out Dictionary<int, KeyValuePair<string, Type>> queryMapping, out int totalNumUTVars, out UriTemplate uriTemplate,
  OperationDescription operationDescription, QueryStringConverter qsc)
        {
            pathMapping = new Dictionary<int, string>();
            queryMapping = new Dictionary<int, KeyValuePair<string, Type>>();
            string utString = GetUTStringOrDefault(operationDescription);
            uriTemplate = new UriTemplate(utString);
            List<string> neededPathVars = new List<string>(uriTemplate.PathSegmentVariableNames);
            List<string> neededQueryVars = new List<string>(uriTemplate.QueryValueVariableNames);
            Dictionary<string, byte> alreadyGotVars = new Dictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
            totalNumUTVars = neededPathVars.Count + neededQueryVars.Count;
            for (int i = 0; i < operationDescription.Messages[0].Body.Parts.Count; ++i)
            {
                MessagePartDescription mpd = operationDescription.Messages[0].Body.Parts[i];
                string parameterName = XmlConvert.DecodeName(mpd.Name);
                if (alreadyGotVars.ContainsKey(parameterName))
                {
                    throw new InvalidOperationException();
                }
                List<string> neededPathCopy = new List<string>(neededPathVars);
                foreach (string pathVar in neededPathCopy)
                {
                    if (string.Compare(parameterName, pathVar, StringComparison.OrdinalIgnoreCase) == 0)
                    {
                        if (mpd.Type != typeof(string))
                        {
                            throw new InvalidOperationException();
                        }
                        pathMapping.Add(i, parameterName);
                        alreadyGotVars.Add(parameterName, 0);
                        neededPathVars.Remove(pathVar);
                    }
                }
                List<string> neededQueryCopy = new List<string>(neededQueryVars);
                foreach (string queryVar in neededQueryCopy)
                {
                    if (string.Compare(parameterName, queryVar, StringComparison.OrdinalIgnoreCase) == 0)
                    {
                        if (!qsc.CanConvert(mpd.Type))
                        {
                            throw new InvalidOperationException();
                        }
                        queryMapping.Add(i, new KeyValuePair<string, Type>(parameterName, mpd.Type));
                        alreadyGotVars.Add(parameterName, 0);
                        neededQueryVars.Remove(queryVar);
                    }
                }
            }
            if (neededPathVars.Count != 0)
            {
                throw new InvalidOperationException();
            }
            if (neededQueryVars.Count != 0)
            {
                throw new InvalidOperationException();
            }
        }
        private static string GetUTStringOrDefault(OperationDescription operationDescription)
        {
            string utString = GetWebUriTemplate(operationDescription);
            if (utString == null && GetWebMethod(operationDescription) == "GET")
            {
                utString = MakeDefaultGetUTString(operationDescription);
            }
            if (utString == null)
            {
                utString = operationDescription.Name;
            }
            return utString;
        }
        private static string MakeDefaultGetUTString(OperationDescription od)
        {
            StringBuilder sb = new StringBuilder(XmlConvert.DecodeName(od.Name));
            //sb.Append("/*"); // note: not + "/*", see 8988 and 9653
            if (!IsUntypedMessage(od.Messages[0]))
            {
                sb.Append("?");
                foreach (MessagePartDescription mpd in od.Messages[0].Body.Parts)
                {
                    string parameterName = XmlConvert.DecodeName(mpd.Name);
                    sb.Append(parameterName);
                    sb.Append("={");
                    sb.Append(parameterName);
                    sb.Append("}&");
                }
                sb.Remove(sb.Length - 1, 1);
            }
            return sb.ToString();
        }
        private static bool IsUntypedMessage(MessageDescription message)
        {

            if (message == null)
            {
                return false;
            }
            return (message.Body.ReturnValue != null && message.Body.Parts.Count == 0 && message.Body.ReturnValue.Type == typeof(Message)) ||
                (message.Body.ReturnValue == null && message.Body.Parts.Count == 1 && message.Body.Parts[0].Type == typeof(Message));
        }
        private static void EnsureOk(WebGetAttribute wga, WebInvokeAttribute wia, OperationDescription od)
        {
            if (wga != null && wia != null)
            {
                throw new InvalidOperationException();
            }
        }
        private static string GetWebUriTemplate(OperationDescription od)
        {
            // return exactly what is on the attribute
            WebGetAttribute wga = od.Behaviors.Find<WebGetAttribute>();
            WebInvokeAttribute wia = od.Behaviors.Find<WebInvokeAttribute>();
            EnsureOk(wga, wia, od);
            if (wga != null)
            {
                return wga.UriTemplate;
            }
            else if (wia != null)
            {
                return wia.UriTemplate;
            }
            else
            {
                return null;
            }
        }
        private static string GetWebMethod(OperationDescription od)
        {
            WebGetAttribute wga = od.Behaviors.Find<WebGetAttribute>();
            WebInvokeAttribute wia = od.Behaviors.Find<WebInvokeAttribute>();
            EnsureOk(wga, wia, od);
            if (wga != null)
            {
                return "GET";
            }
            else if (wia != null)
            {
                return wia.Method ?? "POST";
            }
            else
            {
                return "POST";
            }
        }

    }
}
