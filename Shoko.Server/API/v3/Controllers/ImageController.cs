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
        /// Returns the image for the given <paramref name="source"/>, <paramref name="type"/> and <paramref name="value"/>.
        /// </summary>
        /// <param name="source">AniDB, TvDB, MovieDB, Shoko</param>
        /// <param name="type">Poster, Fanart, Banner, Thumb, Static</param>
        /// <param name="value">Usually the ID, but the resource name in the case of image/Shoko/Static/{value}</param>
        /// <returns>200 on found, 400/404 if the type or source are invalid, and 404 if the id is not found</returns>
        [HttpGet("{source}/{type}/{value}")]
        [ProducesResponseType(typeof(FileStreamResult), 200), ProducesResponseType(404)]
        public ActionResult GetImage([FromRoute] Image.ImageSource source, [FromRoute] Image.ImageType type, [FromRoute] string value)
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
        /// <param name="filePath"></param>
        /// <returns></returns>
        [NonAction]
        public ActionResult GetStaticImage(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return NotFound("The requested resource does not exist.");

            var fileName = Path.GetFileNameWithoutExtension(filePath);
            var buffer = (byte[]) Resources.ResourceManager.GetObject(fileName);
            if ((buffer == null) || (buffer.Length == 0))
                return NotFound("The requested resource does not exist.");

            return File(buffer, Mime.GetMimeMapping(fileName) ?? "image/png");
        }
    }
}
