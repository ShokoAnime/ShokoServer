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
        public static APIMessage processing()
        {
            return new APIMessage(100, "processing");
        }

        public static APIMessage processing(string custom_message)
        {
            return new APIMessage(100, custom_message);
        }

        public static APIMessage statusOK()
        {
            return new APIMessage(200, "ok");
        }

        public static APIMessage statusOK(string custom_message)
        {
            return new APIMessage(200, custom_message);
        }

        public static APIMessage badRequest()
        {
            return new APIMessage(400, "Bad Request");
        }

        public static APIMessage badRequest(string custom_message)
        {
            return new APIMessage(400, custom_message);
        }

        public static APIMessage unauthorized()
        {
            return new APIMessage(401, "Unauthorized");
        }

        public static APIMessage adminNeeded()
        {
            return new APIMessage(403, "Admin rights needed");
        }

        public static APIMessage accessDenied()
        {
            return new APIMessage(403, "Access Denied");
        }

        public static APIMessage notFound404(string message = "Not Found")
        {
            return new APIMessage(404, message);
        }

        public static APIMessage internalError(string custom_message = "Internal Error")
        {
            return new APIMessage(500, custom_message);
        }

        public static APIMessage notImplemented()
        {
            return new APIMessage(501, "Not Implemented");
        }
    }
}