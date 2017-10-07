using NLog;

namespace Shoko.Server.API.v2.Models.core
{
    public class APIMessage
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public int code { get; set; }
        public string message { get; set; }

        public APIMessage(int _code, string _message)
        {
            code = _code;
            message = _message;
            if (_code < 200 && _code > 299)
            {
                logger.Warn("Nancy >>> " + _code.ToString() + ":" + _message);
            }
            else
            {
#if DEBUG
                logger.Debug("Nancy >>> " + _code.ToString() + ":" + _message);
#endif
            }
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