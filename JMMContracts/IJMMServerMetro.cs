using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ServiceModel;

namespace JMMContracts
{
	[ServiceContract]
	public interface IJMMServerMetro
	{
		[OperationContract]
		List<Contract_AnimeGroup> GetAllGroups(int userID);
	}
}
