using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;
using System.Text;
using System.Threading.Tasks;

namespace JMMServer.WCFCompression
{
    public interface ISerializer
    {
        Message Serialize(OperationDescription operation, MessageVersion version, object[] parameters, object result);
        void Deserialize(OperationDescription operation, Dictionary<string, int> parameterNames, Message message, object[] paramters);
    }
}
