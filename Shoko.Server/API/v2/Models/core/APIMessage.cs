using System.Collections.Generic;
using System.IO;
using System.Net;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace Shoko.Server.API.v2.Models.core
{
    public class APIMessage : ActionResult
    {
        public int code { get; set; }
        public string message { get; set; }
        public (string, string) [] details { get; set; }

        /// <summary>
        /// An HTTP message with details about the result
        /// </summary>
        /// <param name="_code">The HTTP status code</param>
        /// <param name="_message">A summary of the message</param>
        public APIMessage(HttpStatusCode _code, string _message) : this((int) _code, _message)
        {
        }

        /// <summary>
        /// An HTTP message with details about the result
        /// </summary>
        /// <param name="_code">The HTTP status code</param>
        /// <param name="_message">A summary of the message</param>
        public APIMessage(int _code, string _message) : this(_code, _message,
            new List<(string, string)> {(_message, string.Empty)})
        {
        }

        /// <summary>
        /// An HTTP message with details about the result
        /// </summary>
        /// <param name="_code">The HTTP status code</param>
        /// <param name="_message">A summary of the message</param>
        /// <param name="_details">A list of tuples, with the first item being the field the message relates to, and the second the message</param>
        public APIMessage(HttpStatusCode _code, string _message, List<(string, string)> _details) : this((int) _code,
            _message, _details)
        {
        }

        /// <summary>
        /// An HTTP message with details about the result
        /// </summary>
        /// <param name="_code">The HTTP status code</param>
        /// <param name="_message">A summary of the message</param>
        /// <param name="_details">A list of tuples, with the first item being the field the message relates to, and the second the message</param>
        public APIMessage(int _code, string _message, List<(string, string)> _details)
        {
            code = _code;
            message = _message;
            details = _details.ToArray();
        }

        public override void ExecuteResult(ActionContext context)
        {
            context.HttpContext.Response.StatusCode = code;
            context.HttpContext.Response.ContentType = "application/json";
            if (code == (int) HttpStatusCode.NoContent) return;

            var serializer = new JsonSerializer();
            using (StreamWriter writer = new StreamWriter(context.HttpContext.Response.Body))
                serializer.Serialize(writer, new { code, message, details });
        }
    }

    public static class APIStatus
    {
        /// <summary>
        /// Status: 100 Processing
        /// </summary>
        /// <returns></returns>
        public static APIMessage Processing() => new APIMessage(100, "processing");

        public static APIMessage Processing(string custom_message) => new APIMessage(100, custom_message);

        /// <summary>
        /// Status: 200 OK
        /// </summary>
        /// <returns></returns>
        public static APIMessage OK() => new APIMessage(200, "ok");

        public static APIMessage OK(string custom_message) => new APIMessage(200, custom_message);

        /// <summary>
        /// Status: 400 Bad Request
        /// </summary>
        /// <returns></returns>
        public static APIMessage BadRequest() => new APIMessage(400, "Bad Request");

        /// <summary>
        /// Status: 400 Bad Request
        /// </summary>
        /// <returns></returns>
        public static APIMessage BadRequest(string custom_message) => new APIMessage(400, custom_message);

        /// <summary>
        /// Status: 401 Unauthorized
        /// </summary>
        /// <returns></returns>
        public static APIMessage Unauthorized() => new APIMessage(401, "Unauthorized");

        /// <summary>
        /// Status: 403 Admin Rights Needed
        /// </summary>
        /// <returns></returns>
        public static APIMessage AdminNeeded() => new APIMessage(403, "Admin rights needed");

        public static APIMessage AccessDenied() => new APIMessage(403, "Access Denied");

        /// <summary>
        /// Status: 404 Not Found
        /// </summary>
        /// <returns></returns>
        public static APIMessage NotFound(string message = "Not Found") => new APIMessage(404, message);

        /// <summary>
        /// Status: 500 Server Error
        /// </summary>
        /// <returns></returns>
        public static APIMessage InternalError(string custom_message = "Internal Error") => new APIMessage(500, custom_message);

        /// <summary>
        /// Status: 501 Not Implemented
        /// </summary>
        /// <returns></returns>
        public static APIMessage NotImplemented() => new APIMessage(501, "Not Implemented");

        public static APIMessage ServiceUnavailable() => new APIMessage(503, "Server is not Running");
    }
}