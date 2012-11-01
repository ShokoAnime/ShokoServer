using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ServiceModel;
using System.ServiceModel.Web;
using System.IO;

namespace JMMContracts
{
	[ServiceContract]
	public interface IJMMServerREST
	{
		[OperationContract]
		[WebGet(UriTemplate = "GetImage/{ImageType}/{ImageID}", RequestFormat = WebMessageFormat.Xml, ResponseFormat = WebMessageFormat.Xml, BodyStyle = WebMessageBodyStyle.Bare)]
		Stream GetImage(string ImageType, string ImageID);

		[OperationContract]
		[WebGet(UriTemplate = "GetImageUsingPath/{ServerImagePath}", RequestFormat = WebMessageFormat.Xml, ResponseFormat = WebMessageFormat.Xml, BodyStyle = WebMessageBodyStyle.Bare)]
		Stream GetImageUsingPath(string serverImagePath);
	}
}
