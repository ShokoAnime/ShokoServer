using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ServiceModel;
using System.IO;

namespace JMMContracts
{
	[ServiceContract]
	public interface IJMMServerStreaming
	{
		[OperationContract]
		Stream Download(string fileName);
	}
}
