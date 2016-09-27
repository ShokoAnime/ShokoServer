using Nancy;
using System.IO;

namespace JMMServer.API
{

	public class ByteArrayResponse : Response
	{
		/// <summary>
		/// Byte array response
		/// </summary>
		/// <param name="body">Byte array to be the body of the response</param>
		/// <param name="contentType">Content type to use</param>
		public ByteArrayResponse(byte[] body, string contentType = null)
		{
			this.ContentType = contentType ?? "application/octet-stream";

			this.Contents = stream =>
			{
				using (var writer = new BinaryWriter(stream))
				{
					writer.Write(body);
				}
			};
		}
	}
}