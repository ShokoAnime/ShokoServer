using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ServiceModel;
using System.ServiceModel.Web;
using System.IO;
using Stream = System.IO.Stream;

namespace JMMContracts
{
	[ServiceContract]
	public interface IJMMServerREST
	{
		[OperationContract]
		[WebGet(UriTemplate = "GetImage/{ImageType}/{ImageID}")]
		Stream GetImage(string ImageType, string ImageID);

        [OperationContract]
        [WebGet(UriTemplate = "GetThumb/{ImageType}/{ImageID}/{Ratio}")]
        Stream GetThumb(string ImageType, string ImageID, string Ratio);
        /*
        [OperationContract]
        [WebInvoke(UriTemplate = "GetStream/{cmd}/{arg}/{opt}",Method="*")]
        Stream GetStream(string cmd, string arg,string opt);*/
        [OperationContract]
        [WebGet(UriTemplate = "GetSupportImage/{name}/{Ratio}")]
        Stream GetSupportImage(string name, string ratio);

        [OperationContract]
		[WebGet(UriTemplate = "GetImageUsingPath/{ServerImagePath}")]
		Stream GetImageUsingPath(string serverImagePath);
	}
}
