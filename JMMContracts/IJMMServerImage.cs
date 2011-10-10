using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ServiceModel;

namespace JMMContracts
{
	[ServiceContract]
	public interface IJMMServerImage
	{
		[OperationContract]
		byte[] GetImage(string entityID, int entityType, bool thumnbnailOnly);
	}
}
