using System;
using System.ServiceModel;
using System.ServiceModel.Channels;

namespace JMMServer
{
    public class CustomHttpBinding : CustomBinding
    {
        private readonly HttpTransportBindingElement transport;

        public CustomHttpBinding()
        {
            transport = new HttpTransportBindingElement();
        }

        public long MaxMessageSize
        {
            set
            {
                transport.MaxReceivedMessageSize = value;
                transport.MaxBufferSize = (int)value;
            }
        }

        public override BindingElementCollection CreateBindingElements()
        {
            BindingElement security = SecurityBindingElement.CreateSecureConversationBindingElement(
                SecurityBindingElement.CreateUserNameForSslBindingElement(true));


            MessageEncodingBindingElement encoding = new BinaryMessageEncodingBindingElement();

            return new BindingElementCollection(new[]
            {
                security,
                encoding,
                transport
            });
        }
    }

    public class BinaryOverHTTPBinding : Binding
    {
        private BinaryMessageEncodingBindingElement encoding;
        private HttpTransportBindingElement transport;

        public BinaryOverHTTPBinding()
        {
            InitializeValue();
        }

        public override string Scheme
        {
            get { return transport.Scheme; }
        }

        public override BindingElementCollection CreateBindingElements()
        {
            var elements = new BindingElementCollection();
            elements.Add(encoding);
            elements.Add(transport);
            return elements;
        }

        private void InitializeValue()
        {
            transport = new HttpTransportBindingElement();
            transport.MaxBufferSize = int.MaxValue;
            transport.MaxReceivedMessageSize = int.MaxValue;
            transport.TransferMode = TransferMode.Streamed;

            ReceiveTimeout = TimeSpan.MaxValue;
            SendTimeout = TimeSpan.MaxValue;
            CloseTimeout = TimeSpan.MaxValue;
            Name = "FileStreaming";

            //this.Security.Mode = BasicHttpSecurityMode.None;

            encoding = new BinaryMessageEncodingBindingElement();
            encoding.ReaderQuotas.MaxArrayLength = int.MaxValue;
            encoding.ReaderQuotas.MaxBytesPerRead = int.MaxValue;
        }
    }
}