using Microsoft.AspNetCore.Mvc;
using Shoko.Models.Enums;

namespace Shoko.Server.API.v3
{
    [ApiController]
    [Route("/apiv3/image")]
    public class ImageController : BaseController
    {
        
        /// <summary>
        /// /apiv3/image/tvdb/fanart/12
        /// returns an image
        /// </summary>
        /// <param name="source"></param>
        /// <param name="type"></param>
        /// <param name="id"></param>
        /// <returns>200 on found, 400 if the type or source are invalid, and 404 if the id is not found</returns>
        [HttpGet("{source}/{type}/{id}")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        public ActionResult GetImage(string source, string type, int id)
        {
            source = source.ToLower();
            type = type.ToLower();
            ImageEntityType? sourceType = GetImageTypeFromSourceAndType(source, type);

            if (sourceType == null) return BadRequest();
            string path = Image.GetImagePath(sourceType.Value, id);
            if (string.IsNullOrEmpty(path)) return NotFound();
            return File(System.IO.File.OpenRead(path), MimeTypes.GetMimeType(path));
        }

        /// <summary>
        /// Gets the enum ImageEntityType from the text url segments
        /// </summary>
        /// <param name="source"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        [NonAction]
        private ImageEntityType? GetImageTypeFromSourceAndType(string source, string type)
        {
            switch (source)
            {
                case "anidb":
                    switch (type)
                    {
                        case "poster": return ImageEntityType.AniDB_Cover;
                        case "character": return ImageEntityType.Character;
                        case "staff": return ImageEntityType.Staff;
                    }
                    break;
                case "tvdb":
                    switch (type)
                    {
                        case "poster": return ImageEntityType.TvDB_Cover;
                        case "fanart": return ImageEntityType.TvDB_FanArt;
                        case "banner": return ImageEntityType.TvDB_Banner;
                        case "thumb": return ImageEntityType.TvDB_Episode;
                    }
                    break;
                case "moviedb":
                    switch (type)
                    {
                        case "poster": return ImageEntityType.MovieDB_Poster;
                        case "fanart": return ImageEntityType.MovieDB_FanArt;
                    }
                    break;
            }

            return null;
        }
    }
}