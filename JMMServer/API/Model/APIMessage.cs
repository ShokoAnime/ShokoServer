using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JMMServer.API.Model
{
    public class APIMessage
    {
        public int code { get; set; }
        public string message { get; set; }

        public APIMessage(int _code, string _message)
        {
            code = _code;
            message = _message;
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
            return new APIMessage(400, "bad request");
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

        public static APIMessage internalError()
        {
            return new APIMessage(500, "internal error");
        }

        public static APIMessage internalError(string custom_message)
        {
            return new APIMessage(500, custom_message);
        }

        public static APIMessage notImplemented()
        {
            return new APIMessage(501, "Not Implemented");
        }


    }
}
