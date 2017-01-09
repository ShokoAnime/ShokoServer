using System.ServiceModel.Channels;
using System.ServiceModel.Description;
using System.ServiceModel.Dispatcher;

namespace Shoko.Server.WCFCompression
{
    public class CompressionSelectionEndpointBehavior : IEndpointBehavior
    {
        private SerializationFilter _ser;

        public CompressionSelectionEndpointBehavior(SerializationFilter ser)
        {
            _ser = ser;
        }

        public void AddBindingParameters(ServiceEndpoint endpoint, BindingParameterCollection bindingParameters)
        {
        }

        public void ApplyClientBehavior(ServiceEndpoint endpoint, ClientRuntime clientRuntime)
        {
            foreach (ClientOperation operation in clientRuntime.Operations)
            {
                operation.ParameterInspectors.Add(new PlexKodiFilterInspector(_ser));
            }
        }

        public void ApplyDispatchBehavior(ServiceEndpoint endpoint, EndpointDispatcher endpointDispatcher)
        {
            foreach (DispatchOperation operation in endpointDispatcher.DispatchRuntime.Operations)
            {
                operation.ParameterInspectors.Add(new PlexKodiFilterInspector(_ser));
            }
            endpointDispatcher.DispatchRuntime.MessageInspectors.Add(new CompressionSelectionMessageInspector());

        }

        public void Validate(ServiceEndpoint endpoint)
        {
        }
    }

    public enum SerializationFilter
    {
        Plex,
        Kodi
    }
}