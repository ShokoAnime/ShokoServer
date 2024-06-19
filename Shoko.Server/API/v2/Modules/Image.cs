using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Quartz;
using Shoko.Commons.Extensions;
using Shoko.Models.Enums;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.v2.Models.core;
using Shoko.Server.Extensions;
using Shoko.Server.ImageDownload;
using Shoko.Server.Properties;
using Shoko.Server.Repositories;
using Shoko.Server.Scheduling;
using Shoko.Server.Scheduling.Jobs.Shoko;
using Shoko.Server.Settings;
using Mime = MimeMapping.MimeUtility;

namespace Shoko.Server.API.v2.Modules;

[ApiController]
[Route("/api/image")]
[Route("/api/v2/image")]
[ApiVersion("2.0")]
public class Image : BaseController
{
    private readonly ILogger<Image> _logger;
    private readonly ISchedulerFactory _schedulerFactory;

    [HttpGet("validateall")]
    public async Task<ActionResult> ValidateAll()
    {
        var scheduler = await _schedulerFactory.GetScheduler();
        await scheduler.StartJobNow<ValidateAllImagesJob>();
        return APIStatus.OK();
    }

    /// <summary>
    /// Return image with given id, type
    /// </summary>
    /// <param name="id">image id</param>
    /// <param name="type">image type</param>
    /// <returns>image body inside stream</returns>
    [HttpGet("{type}/{id}")]
    public FileResult GetImage(int type, int id)
    {
        var path = GetImagePath(type, id);

        if (string.IsNullOrEmpty(path))
        {
            Response.StatusCode = 404;
            return File(MissingImage(), "image/png");
        }

        return File(System.IO.File.OpenRead(path), Mime.GetMimeMapping(path));
    }

    /// <summary>
    /// Return thumb with given id, type
    /// </summary>
    /// <param name="id">image id</param>
    /// <param name="type">image type</param>
    /// <param name="ratio">new image ratio</param>
    /// <returns>resize image body inside stream</returns>
    [HttpGet("thumb/{type}/{id}/{ratio?}")]
    public FileResult GetThumb(int type, int id, string ratio = "0")
    {
        string contentType;
        ratio = ratio.Replace(',', '.');
        if (!float.TryParse(ratio, NumberStyles.AllowDecimalPoint, CultureInfo.CreateSpecificCulture("en-EN"),
                out var newratio))
        {
            newratio = 0.6667f;
        }

        var path = GetImagePath(type, id);

        if (string.IsNullOrEmpty(path))
        {
            Response.StatusCode = 404;
            return File(MissingImage(), "image/png");
        }

        var fs = System.IO.File.OpenRead(path);
        contentType = Mime.GetMimeMapping(path);
        var im = System.Drawing.Image.FromStream(fs);
        return File(ResizeImageToRatio(im, newratio), contentType);
    }

    /// <summary>
    /// Return SupportImage (build-in server)
    /// </summary>
    /// <param name="name">image file name</param>
    /// <returns></returns>
    [HttpGet("support/{name}")]
    [InitFriendly]
    [DatabaseBlockedExempt]
    public ActionResult GetSupportImage(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return APIStatus.NotFound();
        }

        name = Path.GetFileNameWithoutExtension(name);
        var man = Resources.ResourceManager;
        var dta = (byte[])man.GetObject(name);
        if (dta == null || dta.Length == 0)
        {
            return APIStatus.NotFound();
        }

        var ms = new MemoryStream(dta);
        ms.Seek(0, SeekOrigin.Begin);

        return File(ms, "image/png");
    }

    [HttpGet("support/{name}/{ratio}")]
    [InitFriendly]
    [DatabaseBlockedExempt]
    public ActionResult GetSupportImage(string name, string ratio)
    {
        if (string.IsNullOrEmpty(name))
        {
            return APIStatus.NotFound();
        }

        ratio = ratio.Replace(',', '.');
        float.TryParse(ratio, NumberStyles.AllowDecimalPoint,
            CultureInfo.CreateSpecificCulture("en-EN"), out var newratio);

        name = Path.GetFileNameWithoutExtension(name);
        var man = Resources.ResourceManager;
        var dta = (byte[])man.GetObject(name);
        if (dta == null || dta.Length == 0)
        {
            return APIStatus.NotFound();
        }

        var ms = new MemoryStream(dta);
        ms.Seek(0, SeekOrigin.Begin);
        var im = System.Drawing.Image.FromStream(ms);

        return File(ResizeImageToRatio(im, newratio), "image/png");
    }

    /// <summary>
    /// Internal function that return valid image file path on server that exist
    /// </summary>
    /// <param name="id">image id</param>
    /// <param name="type">image type</param>
    /// <returns>string</returns>
    internal string GetImagePath(int type, int id)
    {
        var imageType = (ImageEntityType)type;
        string path;

        switch (imageType)
        {
            // 1
            case ImageEntityType.AniDB_Cover:
                var anime = RepoFactory.AniDB_Anime.GetByAnimeID(id);
                if (anime == null)
                {
                    return null;
                }

                path = anime.PosterPath;
                if (System.IO.File.Exists(path))
                {
                    return path;
                }
                else
                {
                    path = string.Empty;
                    _logger.LogTrace("Could not find AniDB_Cover image: {Poster}", anime.PosterPath);
                }

                break;

            // 2
            case ImageEntityType.AniDB_Character:
                var chr = RepoFactory.AniDB_Character.GetByCharID(id);
                if (chr == null)
                {
                    return null;
                }

                path = chr.GetPosterPath();
                if (System.IO.File.Exists(path))
                {
                    return path;
                }
                else
                {
                    path = string.Empty;
                    _logger.LogTrace("Could not find AniDB_Character image: {Poster}", chr.GetPosterPath());
                }

                break;

            // 3
            case ImageEntityType.AniDB_Creator:
                var creator = RepoFactory.AniDB_Seiyuu.GetBySeiyuuID(id);
                if (creator == null)
                {
                    return null;
                }

                path = creator.GetPosterPath();
                if (System.IO.File.Exists(path))
                {
                    return path;
                }
                else
                {
                    path = string.Empty;
                    _logger.LogTrace("Could not find AniDB_Creator image: {Poster}", creator.GetPosterPath());
                }

                break;

            // 4
            case ImageEntityType.TvDB_Banner:
                var wideBanner = RepoFactory.TvDB_ImageWideBanner.GetByID(id);
                if (wideBanner == null)
                {
                    return null;
                }

                path = wideBanner.GetFullImagePath();
                if (System.IO.File.Exists(path))
                {
                    return path;
                }
                else
                {
                    path = string.Empty;
                    _logger.LogTrace("Could not find TvDB_Banner image: {Poster}", wideBanner.GetFullImagePath());
                }

                break;

            // 5
            case ImageEntityType.TvDB_Cover:
                var poster = RepoFactory.TvDB_ImagePoster.GetByID(id);
                if (poster == null)
                {
                    return null;
                }

                path = poster.GetFullImagePath();
                if (System.IO.File.Exists(path))
                {
                    return path;
                }
                else
                {
                    path = string.Empty;
                    _logger.LogTrace("Could not find TvDB_Cover image: {Poster}", poster.GetFullImagePath());
                }

                break;

            // 6
            case ImageEntityType.TvDB_Episode:
                var ep = RepoFactory.TvDB_Episode.GetByID(id);
                if (ep == null)
                {
                    return null;
                }

                path = ep.GetFullImagePath();
                if (System.IO.File.Exists(path))
                {
                    return path;
                }
                else
                {
                    path = string.Empty;
                    _logger.LogTrace("Could not find TvDB_Episode image: {Poster}", ep.GetFullImagePath());
                }

                break;

            // 7
            case ImageEntityType.TvDB_FanArt:
                var fanart = RepoFactory.TvDB_ImageFanart.GetByID(id);
                if (fanart == null)
                {
                    return null;
                }

                path = fanart.GetFullImagePath();
                if (System.IO.File.Exists(path))
                {
                    return path;
                }

                path = string.Empty;
                _logger.LogTrace("Could not find TvDB_FanArt image: {Poster}", fanart.GetFullImagePath());
                break;

            // 8
            case ImageEntityType.MovieDB_FanArt:
                var mFanart = RepoFactory.MovieDB_Fanart.GetByID(id);
                if (mFanart == null)
                {
                    return null;
                }

                mFanart = RepoFactory.MovieDB_Fanart.GetByOnlineID(mFanart.URL);
                if (mFanart == null)
                {
                    return null;
                }

                path = mFanart.GetFullImagePath();
                if (System.IO.File.Exists(path))
                {
                    return path;
                }
                else
                {
                    path = string.Empty;
                    _logger.LogTrace("Could not find MovieDB_FanArt image: {Poster}", mFanart.GetFullImagePath());
                }

                break;

            // 9
            case ImageEntityType.MovieDB_Poster:
                var mPoster = RepoFactory.MovieDB_Poster.GetByID(id);
                if (mPoster == null)
                {
                    return null;
                }

                mPoster = RepoFactory.MovieDB_Poster.GetByOnlineID(mPoster.URL);
                if (mPoster == null)
                {
                    return null;
                }

                path = mPoster.GetFullImagePath();
                if (System.IO.File.Exists(path))
                {
                    return path;
                }
                else
                {
                    path = string.Empty;
                    _logger.LogTrace("Could not find MovieDB_Poster image: {Poster}", mPoster.GetFullImagePath());
                }

                break;

            case ImageEntityType.Character:
                var character = RepoFactory.AnimeCharacter.GetByID(id);
                if (character == null)
                {
                    return null;
                }

                path = ImageUtils.GetBaseAniDBCharacterImagesPath() + Path.DirectorySeparatorChar + character.ImagePath;
                if (System.IO.File.Exists(path))
                {
                    return path;
                }
                else
                {
                    path = string.Empty;
                    _logger.LogTrace("Could not find Character image: {Poster}",
                        ImageUtils.GetBaseAniDBCharacterImagesPath() + Path.DirectorySeparatorChar + character.ImagePath);
                }

                break;

            case ImageEntityType.Staff:
                var staff = RepoFactory.AnimeStaff.GetByID(id);
                if (staff == null)
                {
                    return null;
                }

                path = ImageUtils.GetBaseAniDBCreatorImagesPath() + Path.DirectorySeparatorChar + staff.ImagePath;
                if (System.IO.File.Exists(path))
                {
                    return path;
                }
                else
                {
                    path = string.Empty;
                    _logger.LogTrace("Could not find Staff image: {Poster}",
                        ImageUtils.GetBaseAniDBCreatorImagesPath() + Path.DirectorySeparatorChar + staff.ImagePath);
                }

                break;

            default:
                path = string.Empty;
                break;
        }

        return path;
    }

    /// <summary>
    /// Return random image with given type and not from restricted content
    /// </summary>
    /// <param name="type">image type</param>
    /// <returns>image body inside stream</returns>
    [HttpGet("{type}/random")]
    public FileResult GetRandomImage(int type)
    {
        var path = GetRandomImagePath(type);

        if (string.IsNullOrEmpty(path))
        {
            Response.StatusCode = 404;
            return File(MissingImage(), "image/png");
        }

        return File(System.IO.File.OpenRead(path), Mime.GetMimeMapping(path));
    }

    private string GetRandomImagePath(int type)
    {
        var imageType = (ImageEntityType)type;
        string path;

        switch (imageType)
        {
            // 1
            case ImageEntityType.AniDB_Cover:
                var anime = RepoFactory.AniDB_Anime.GetAll()
                    .Where(a => a?.PosterPath != null && !a.GetAllTags().Contains("18 restricted"))
                    .GetRandomElement();
                if (anime == null)
                {
                    return null;
                }

                path = anime.PosterPath;
                if (System.IO.File.Exists(path))
                {
                    return path;
                }
                else
                {
                    path = string.Empty;
                    _logger.LogTrace("Could not find AniDB_Cover image: {Poster}", anime.PosterPath);
                }

                break;

            // 2
            case ImageEntityType.AniDB_Character:
                var chr = RepoFactory.AniDB_Anime.GetAll()
                    .Where(a => a != null && !a.GetAllTags().Contains("18 restricted"))
                    .SelectMany(a => a.Characters).Select(a => a.GetCharacter()).Where(a => a != null)
                    .GetRandomElement();
                if (chr == null)
                {
                    return null;
                }

                path = chr.GetPosterPath();
                if (System.IO.File.Exists(path))
                {
                    return path;
                }
                else
                {
                    path = string.Empty;
                    _logger.LogTrace("Could not find AniDB_Character image: {Poster}", chr.GetPosterPath());
                }

                break;

            // 3 -- this will likely be slow
            case ImageEntityType.AniDB_Creator:
                var creator = RepoFactory.AniDB_Anime.GetAll()
                    .Where(a => a != null && !a.GetAllTags().Contains("18 restricted"))
                    .SelectMany(a => a.Characters)
                    .SelectMany(a => RepoFactory.AniDB_Character_Seiyuu.GetByCharID(a.CharID))
                    .Select(a => RepoFactory.AniDB_Seiyuu.GetBySeiyuuID(a.SeiyuuID)).Where(a => a != null)
                    .GetRandomElement();
                if (creator == null)
                {
                    return null;
                }

                path = creator.GetPosterPath();
                if (System.IO.File.Exists(path))
                {
                    return path;
                }
                else
                {
                    path = string.Empty;
                    _logger.LogTrace("Could not find AniDB_Creator image: {Poster}", creator.GetPosterPath());
                }

                break;

            // 4
            case ImageEntityType.TvDB_Banner:
                // TvDB doesn't allow H content, so we get to skip the check!
                var wideBanner = RepoFactory.TvDB_ImageWideBanner.GetAll().GetRandomElement();
                if (wideBanner == null)
                {
                    return null;
                }

                path = wideBanner.GetFullImagePath();
                if (System.IO.File.Exists(path))
                {
                    return path;
                }
                else
                {
                    path = string.Empty;
                    _logger.LogTrace("Could not find TvDB_Banner image: {Poster}", wideBanner.GetFullImagePath());
                }

                break;

            // 5
            case ImageEntityType.TvDB_Cover:
                // TvDB doesn't allow H content, so we get to skip the check!
                var poster = RepoFactory.TvDB_ImagePoster.GetAll().GetRandomElement();
                if (poster == null)
                {
                    return null;
                }

                path = poster.GetFullImagePath();
                if (System.IO.File.Exists(path))
                {
                    return path;
                }
                else
                {
                    path = string.Empty;
                    _logger.LogTrace("Could not find TvDB_Cover image: {Poster}", poster.GetFullImagePath());
                }

                break;

            // 6
            case ImageEntityType.TvDB_Episode:
                // TvDB doesn't allow H content, so we get to skip the check!
                var ep = RepoFactory.TvDB_Episode.GetAll().GetRandomElement();
                if (ep == null)
                {
                    return null;
                }

                path = ep.GetFullImagePath();
                if (System.IO.File.Exists(path))
                {
                    return path;
                }
                else
                {
                    path = string.Empty;
                    _logger.LogTrace("Could not find TvDB_Episode image: {Poster}", ep.GetFullImagePath());
                }

                break;

            // 7
            case ImageEntityType.TvDB_FanArt:
                // TvDB doesn't allow H content, so we get to skip the check!
                var fanart = RepoFactory.TvDB_ImageFanart.GetAll().GetRandomElement();
                if (fanart == null)
                {
                    return null;
                }

                path = fanart.GetFullImagePath();
                if (System.IO.File.Exists(path))
                {
                    return path;
                }

                path = string.Empty;
                _logger.LogTrace("Could not find TvDB_FanArt image: {Poster}", fanart.GetFullImagePath());
                break;

            // 8
            case ImageEntityType.MovieDB_FanArt:
                var mFanart = RepoFactory.MovieDB_Fanart.GetAll().GetRandomElement();
                if (mFanart == null)
                {
                    return null;
                }

                path = mFanart.GetFullImagePath();
                if (System.IO.File.Exists(path))
                {
                    return path;
                }
                else
                {
                    path = string.Empty;
                    _logger.LogTrace("Could not find MovieDB_FanArt image: {Poster}", mFanart.GetFullImagePath());
                }

                break;

            // 9
            case ImageEntityType.MovieDB_Poster:
                var mPoster = RepoFactory.MovieDB_Poster.GetAll().GetRandomElement();
                if (mPoster == null)
                {
                    return null;
                }

                path = mPoster.GetFullImagePath();
                if (System.IO.File.Exists(path))
                {
                    return path;
                }
                else
                {
                    path = string.Empty;
                    _logger.LogTrace("Could not find MovieDB_Poster image: {Poster}", mPoster.GetFullImagePath());
                }

                break;

            case ImageEntityType.Character:
                var character = RepoFactory.AniDB_Anime.GetAll()
                    .Where(a => a != null && !a.GetAllTags().Contains("18 restricted"))
                    .SelectMany(a => RepoFactory.CrossRef_Anime_Staff.GetByAnimeID(a.AnimeID))
                    .Where(a => a.RoleType == (int)StaffRoleType.Seiyuu && a.RoleID.HasValue)
                    .Select(a => RepoFactory.AnimeCharacter.GetByID(a.RoleID.Value)).GetRandomElement();
                if (character == null)
                {
                    return null;
                }

                path = ImageUtils.GetBaseAniDBCharacterImagesPath() + Path.DirectorySeparatorChar + character.ImagePath;
                if (System.IO.File.Exists(path))
                {
                    return path;
                }
                else
                {
                    path = string.Empty;
                    _logger.LogTrace("Could not find Character image: {Poster}",
                        ImageUtils.GetBaseAniDBCharacterImagesPath() + Path.DirectorySeparatorChar +
                        character.ImagePath);
                }

                break;

            case ImageEntityType.Staff:
                var staff = RepoFactory.AniDB_Anime.GetAll()
                    .Where(a => a != null && !a.GetAllTags().Contains("18 restricted"))
                    .SelectMany(a => RepoFactory.CrossRef_Anime_Staff.GetByAnimeID(a.AnimeID))
                    .Select(a => RepoFactory.AnimeStaff.GetByID(a.StaffID)).GetRandomElement();
                if (staff == null)
                {
                    return null;
                }

                path = ImageUtils.GetBaseAniDBCreatorImagesPath() + Path.DirectorySeparatorChar + staff.ImagePath;
                if (System.IO.File.Exists(path))
                {
                    return path;
                }
                else
                {
                    path = string.Empty;
                    _logger.LogTrace("Could not find Staff image: {Poster}",
                        ImageUtils.GetBaseAniDBCreatorImagesPath() + Path.DirectorySeparatorChar + staff.ImagePath);
                }

                break;

            default:
                path = string.Empty;
                break;
        }

        return path;
    }

    /// <summary>
    /// Internal function that return image for missing image
    /// </summary>
    /// <returns>Stream</returns>
    internal Stream MissingImage()
    {
        var dta = Resources.blank;
        var ms = new MemoryStream(dta);
        ms.Seek(0, SeekOrigin.Begin);
        return ms;
    }

    internal static System.Drawing.Image ResizeImage(System.Drawing.Image im, int width, int height)
    {
        var dest = new Bitmap(width, height);
        using (var g = Graphics.FromImage(dest))
        {
            g.InterpolationMode = width >= im.Width
                ? InterpolationMode.HighQualityBilinear
                : InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.DrawImage(im, 0, 0, width, height);
        }

        return dest;
    }

    internal Stream ResizeImageToRatio(System.Drawing.Image im, float newratio)
    {
        float calcwidth = im.Width;
        float calcheight = im.Height;

        if (Math.Abs(newratio) < 0.1F)
        {
            var stream = new MemoryStream();
            im.Save(stream, ImageFormat.Png);
            stream.Seek(0, SeekOrigin.Begin);
            return stream;
        }

        float nheight;
        do
        {
            nheight = calcwidth / newratio;
            if (nheight > im.Height + 0.5F)
            {
                calcwidth = calcwidth * (im.Height / nheight);
            }
            else
            {
                calcheight = nheight;
            }
        } while (nheight > im.Height + 0.5F);

        var newwidth = (int)Math.Round(calcwidth);
        var newheight = (int)Math.Round(calcheight);
        var x = 0;
        var y = 0;
        if (newwidth < im.Width)
        {
            x = (im.Width - newwidth) / 2;
        }

        if (newheight < im.Height)
        {
            y = (im.Height - newheight) / 2;
        }

        var im2 = ResizeImage(im, newwidth, newheight);
        var g = Graphics.FromImage(im2);
        g.DrawImage(im, new Rectangle(0, 0, im2.Width, im2.Height), new Rectangle(x, y, im2.Width, im2.Height),
            GraphicsUnit.Pixel);
        var ms = new MemoryStream();
        im2.Save(ms, ImageFormat.Png);
        ms.Seek(0, SeekOrigin.Begin);
        return ms;
    }

    public Image(ISettingsProvider settingsProvider, ISchedulerFactory schedulerFactory, ILogger<Image> logger) : base(settingsProvider)
    {
        _schedulerFactory = schedulerFactory;
        _logger = logger;
    }
}
