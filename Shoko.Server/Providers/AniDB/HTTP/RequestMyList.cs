using System;
using NLog;

namespace Shoko.Server.Providers.AniDB.Http
{
    public class RequestMyList : HttpBaseRequest<ResponseMyList>
    {
        public Logger Logger = LogManager.GetCurrentClassLogger();

        protected override string BaseCommand => $"http://api.anidb.net:9001/httpapi?client=animeplugin&clientver=1&protover=1&request=mylist&user={Username}&pass={Password}";
        
        private string Username { get; set; }
        private string Password { get; set; }
        
        protected override HttpBaseResponse<ResponseMyList> ParseResponse(HttpBaseResponse<string> data)
        {
            try
            {
                return new HttpBaseResponse<ResponseMyList>() {Code = data.Code, Response = null};
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error in AniDBHTTPHelper.GetMyListXMLFromAPI: {0}");
                return new HttpBaseResponse<ResponseMyList>() {Code = data.Code, Response = null};
            }
        }
    }
}
