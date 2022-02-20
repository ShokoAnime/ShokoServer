using System;
using System.IO;
using System.Resources;
using Microsoft.AspNetCore.Mvc;
using Shoko.Models.Enums;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.Properties;
using Mime = MimeMapping.MimeUtility;

namespace Shoko.Server.API.v3.Controllers
{
    [ApiController, Route("/api/v{version:apiVersion}/[controller]"), ApiV3]
    public class ImageController : BaseController
    {
        
        /// <summary>
        /// /api/v3/image/tvdb/fanart/12
        /// returns an image
        /// </summary>
        /// <param name="source">AniDB, TvDB, MovieDB, Shoko</param>
        /// <param name="type">Poster, Fanart, Banner, Thumb, Static</param>
        /// <param name="value">Usually the ID, but the resource name in the case of image/Shoko/Static/{value}</param>
        /// <returns>200 on found, 400/404 if the type or source are invalid, and 404 if the id is not found</returns>
        [HttpGet("{source}/{type}/{value}")]
        [ProducesResponseType(typeof(FileStreamResult),200), ProducesResponseType(400), ProducesResponseType(404)]
        public ActionResult GetImage(string source, string type, string value)
        {
            var sourceType = Image.GetImageTypeFromSourceAndType(source, type) ?? ImageEntityType.None;
            if (sourceType == ImageEntityType.None)
                return NotFound("The requested resource does not exist.");

            if (sourceType == ImageEntityType.Static)
                return GetStaticImage(value);

            if (!int.TryParse(value, out int id))
                return NotFound("The requested resource does not exist.");

            string path = Image.GetImagePath(sourceType, id);
            if (string.IsNullOrEmpty(path))
                return NotFound("The requested resource does not exist.");
            return File(System.IO.File.OpenRead(path), Mime.GetMimeMapping(path));
        }
        
        /// <summary>
        /// Gets a static server resource
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        [NonAction]
        public ActionResult GetStaticImage(string name)
        {
            if (string.IsNullOrEmpty(name))
                return NotFound("The requested resource does not exist.");
            name = Path.GetFileNameWithoutExtension(name);
            ResourceManager man = Resources.ResourceManager;
            byte[] dta = (byte[]) man.GetObject(name);
            if ((dta == null) || (dta.Length == 0))
                return NotFound("The requested resource does not exist.");
            MemoryStream ms = new MemoryStream(dta);
            ms.Seek(0, SeekOrigin.Begin);

            return File(ms, "image/png");
        }
    }
}
