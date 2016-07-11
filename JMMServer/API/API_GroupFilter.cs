using System;
using Nancy;

namespace JMMServer.API
{
    public class API_GroupFilter: Nancy.NancyModule
    {
        public API_GroupFilter()
        {
            Get["/GetSupportImage/{name}"] = parameter => { return GetSupportImage(parameter.name); };
            Get["/GetFilters/{id}"] = parameter => { return GetFilters(parameter.id); };
        }

        PlexAndKodi.IProvider _prov = new PlexAndKodi.Kodi.KodiProvider();
        PlexAndKodi.CommonImplementation _impl = new PlexAndKodi.CommonImplementation();

        private object GetFilters(int id)
        {
            JMMContracts.PlexAndKodi.MediaContainer media = _impl.GetFilters(_prov, id.ToString());
            Response response = new Response();
            response = Response.AsJson<JMMContracts.PlexAndKodi.MediaContainer>(media);
            response.ContentType = "application/json";
            return response;
        }

        private object GetSupportImage(string name)
        {
            System.IO.Stream image = _impl.GetSupportImage(name);
            Response response = new Response();
            response = Response.FromStream(image, "image/jpeg");
            return response;
        }

    }
}
