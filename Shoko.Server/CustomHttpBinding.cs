using System;
using System.ServiceModel.Channels;

namespace Shoko.Server
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
                transport.MaxBufferSize = (int) value;
            }
        }

        public override BindingElementCollection CreateBindingElements()
        {
            BindingElement security = SecurityBindingElement.CreateSecureConversationBindingElement(
                SecurityBindingElement.CreateUserNameForSslBindingElement(true));


            MessageEncodingBindingElement encoding =
                (MessageEncodingBindingElement) new BinaryMessageEncodingBindingElement();

            return new BindingElementCollection(new[]
            {
                security,
                encoding,
                transport,
            });
        }
    }

    public class BinaryOverHTTPBinding : Binding
    {
        private HttpTransportBindingElement transport;
        private BinaryMessageEncodingBindingElement encoding;

        public BinaryOverHTTPBinding()
            : base()
        {
            this.InitializeValue();
        }

        public override BindingElementCollection CreateBindingElements()
        {
            BindingElementCollection elements = new BindingElementCollection();
            elements.Add(this.encoding);
            elements.Add(this.transport);
            return elements;
        }

        public override string Scheme
        {
            get { return this.transport.Scheme; }
        }

        private void InitializeValue()
        {
            this.transport = new HttpTransportBindingElement();
            this.transport.MaxBufferSize = int.MaxValue;
            this.transport.MaxReceivedMessageSize = int.MaxValue;
            this.transport.TransferMode = System.ServiceModel.TransferMode.Streamed;

            this.ReceiveTimeout = TimeSpan.MaxValue;
            this.SendTimeout = TimeSpan.MaxValue;
            this.CloseTimeout = TimeSpan.MaxValue;
            this.Name = "FileStreaming";

            //this.Security.Mode = BasicHttpSecurityMode.None;

            this.encoding = new BinaryMessageEncodingBindingElement();
            this.encoding.ReaderQuotas.MaxArrayLength = int.MaxValue;
            this.encoding.ReaderQuotas.MaxBytesPerRead = int.MaxValue;
        }
    }
}