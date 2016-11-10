

using JMMServer.PlexAndKodi;
using Nancy;

namespace JMMServer.API.Module.apiv2
{
    public class Image : Nancy.NancyModule
    {
        public static int version = 1;

        public Image() : base("/api")
        {

            Get["/cover/{id}"] = x => { return GetCover((int)x.id); };
            Get["/fanart/{id}"] = x => { return GetFanart((int)x.id); };
            Get["/poster/{id}"] = x => { return GetPoster((int)x.id); };
            Get["/thumb/{type}/{id}"] = x => { return GetThumb((int)x.type, (int)x.id); };
            Get["/thumb/{type}/{id}/{ratio}"] = x => { return GetThumb((int)x.type, (int)x.id, x.ratio); };
            Get["/banner/{id}"] = x => { return GetImage((int)x.id, 4, false); };
            Get["/fanart/{id}"] = x => { return GetImage((int)x.id, 7, false); };

            Get["/image/{type}/{id}"] = x => { return GetImage((int)x.id, (int)x.type, false); };
            Get["/image/support/{name}"] = x => { return GetSupportImage(x.name); };

        }

        JMMServiceImplementationREST _rest = new JMMServiceImplementationREST();
        CommonImplementation _impl = new CommonImplementation();

        /// <summary>
        /// Return image with given Id type and information if its should be thumb
        /// </summary>
        /// <param name="id"></param>
        /// <param name="type"></param>
        /// <param name="thumb"></param>
        /// <returns></returns>
        private object GetImage(int id, int type, bool thumb)
        {
            string contentType;
            System.IO.Stream image = _rest.GetImage(type.ToString(), id.ToString(), thumb, out contentType);
            Nancy.Response response = new Nancy.Response();
            response = Response.FromStream(image, contentType);
            return response;
        }

        private object GetFanart(int serie_id)
        {
            //Request request = this.Request;
            //Entities.JMMUser user = (Entities.JMMUser)this.Context.CurrentUser;
            //JMMServiceImplementation _impl = new JMMServiceImplementation();
            //Contract_AnimeSeries ser = _impl.GetSeries(serie_id, user.JMMUserID);

            //Currently hack this, as the end result should find image for series id not image id.
            //TODO APIv2 This should return default image for series_id not image_id

            string contentType;
            System.IO.Stream image = _rest.GetImage("7".ToString(), serie_id.ToString(), false, out contentType);
            if (image == null)
            {
                image = _rest.GetImage("11".ToString(), serie_id.ToString(), false, out contentType);
            }
            if (image == null)
            {
                image = _rest.GetImage("8".ToString(), serie_id.ToString(), false, out contentType);
            }
            else
            {
                image = _rest.BlankImage();
            }
            Nancy.Response response = new Nancy.Response();
            response = Response.FromStream(image, contentType);
            return response;
        }
        private object GetCover(int serie_id)
        {
            //TODO APIv2 This should return default image for series_id not image_id
            string contentType;
            System.IO.Stream image = _rest.GetImage("1".ToString(), serie_id.ToString(), false, out contentType);
            if (image == null)
            {
                image = _rest.GetImage("5".ToString(), serie_id.ToString(), false, out contentType);
            }
            else
            {
                image = _rest.BlankImage();
            }
            Nancy.Response response = new Nancy.Response();
            response = Response.FromStream(image, contentType);
            return response;
        }
        private object GetPoster(int serie_id)
        {
            //TODO APIv2 This should return default image for series_id not image_id
            string contentType;
            System.IO.Stream image = _rest.GetImage("10".ToString(), serie_id.ToString(), false, out contentType);
            if (image == null)
            {
                image = _rest.GetImage("9".ToString(), serie_id.ToString(), false, out contentType);
            }
            else
            {
                image = _rest.BlankImage();
            }
            Nancy.Response response = new Nancy.Response();
            response = Response.FromStream(image, contentType);
            return response;
        }
        private object GetThumb(int thumb_type, int thumb_id, string ratio = "1.0")
        {
            string contentType;
            System.IO.Stream image = _rest.GetThumb(thumb_type.ToString(), thumb_id.ToString(), ratio, out contentType);
            Nancy.Response response = new Nancy.Response();
            response = Response.FromStream(image, contentType);
            return response;
        }

        /// <summary>
        /// Return SupportImage (build-in server)
        /// </summary>
        /// <param name="name">image file name</param>
        /// <returns></returns>
        private object GetSupportImage(string name)
        {
            System.IO.Stream image = _impl.GetSupportImage(name);
            Nancy.Response response = new Nancy.Response();
            response = Response.FromStream(image, "image/png");
            return response;
        }

    }
}