using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel.Description;
using System.ServiceModel.Dispatcher;
using System.ServiceModel.Web;
using System.Text;
using System.Threading.Tasks;

namespace JMMServer.WCFCompression
{
    class MultiBehavior : WebHttpBehavior
    {
        protected override IDispatchMessageFormatter GetRequestDispatchFormatter(OperationDescription operationDescription, ServiceEndpoint endpoint)
        { 
            return new MultiDispatchFormatter(operationDescription,GetQueryStringConverter(operationDescription),endpoint, true);
        }
        protected override IDispatchMessageFormatter GetReplyDispatchFormatter(OperationDescription operationDescription, ServiceEndpoint endpoint)
        {
            return new MultiDispatchFormatter(operationDescription, GetQueryStringConverter(operationDescription), endpoint, false);
        }

        public override void ApplyDispatchBehavior(ServiceEndpoint endpoint, EndpointDispatcher endpointDispatcher)
        {
            base.ApplyDispatchBehavior(endpoint, endpointDispatcher);
        }

        public override WebMessageFormat DefaultOutgoingRequestFormat
        {
            get;
            set;
        }
        
    }
}
