using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Quartz;
using Shoko.Server.Plex.Models.Connections;
using Shoko.Server.Plex.Models.Libraries;
using Shoko.Abstractions.Enums;
using Shoko.Abstractions.Services;
using Shoko.Server.Extensions;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Plex;
using Shoko.Server.Repositories;
using Shoko.Server.Scheduling;
using Shoko.Server.Scheduling.Jobs.Plex;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

#if DEBUG
using Shoko.Server.Plex.Models.Collection;
using Shoko.Server.Plex.Libraries;
#endif

namespace Shoko.Server.API.v0.Controllers;

[ApiController]
[Route("/plex")]
[ApiVersionNeutral]
public class PlexWebhook : BaseController
{
    private readonly ILogger<PlexWebhook> _logger;
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly IUserDataService _userDataService;

    public PlexWebhook(ILogger<PlexWebhook> logger, ISettingsProvider settingsProvider, ISchedulerFactory schedulerFactory, IUserDataService userDataService) : base(settingsProvider)
    {
        _logger = logger;
        _schedulerFactory = schedulerFactory;
        _userDataService = userDataService;
    }

    //The second one is to just make sure
    [HttpPost]
    [HttpPost("/plex.json")]
    public async Task<ActionResult> WebhookPost([FromForm, ModelBinder(BinderType = typeof(PlexBinder))] PlexEvent payload)
    {
        /*PlexEvent eventData = JsonConvert.DeserializeObject<PlexEvent>(this.Context.Request.Form.payload,
            new JsonSerializerSettings() {ContractResolver = new CamelCasePropertyNamesContractResolver()});*/
        if (payload?.Metadata == null) return BadRequest("Need a valid payload");
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        _logger.LogTrace($"{payload.Event}: {payload.Metadata?.Guid}");

        if (payload.Event.EqualsInvariantIgnoreCase("media.scrobble"))
            await Scrobble(payload, User);

        return Ok(); //doesn't need to be an ApiStatus.OK() since really, all I take is plex.
    }


    #region Plex events

    [NonAction]
    private async Task Scrobble(PlexEvent data, JMMUser user)
    {
        var metadata = data.Metadata;
        var (episode, anime) = GetEpisode(metadata);
        if (episode == null)
        {
            _logger.LogInformation(
                "No episode returned, aborting scrobble. This might not have been a ShokoMetadata library");
            return;
        }

        _logger.LogTrace("Got anime: {Anime}, ep: {EpisodeNumber}", anime, episode.AniDB_Episode.EpisodeNumber);

        user ??= RepoFactory.JMMUser.GetAll().FirstOrDefault(u => u.GetPlexUsers().Contains(data.Account.Title));
        if (user == null)
        {
            _logger.LogInformation("Unable to determine who \"{AccountTitle}\" is in Shoko, make sure this is set under user settings in Desktop", data.Account.Title);
            return; //At this point in time, we don't want to scrobble for unknown users
        }

        await _userDataService.SetEpisodeWatchedStatus(episode, user, true, FromUnixTime(metadata.LastViewedAt));
    }

    #endregion

    [NonAction]
    private (AnimeEpisode, AnimeSeries) GetEpisode(PlexEvent.PlexMetadata metadata)
    {
        var guid = new Uri(metadata.Guid);
        if (guid.Scheme != "com.plexapp.agents.shoko" && guid.Scheme != "com.plexapp.agents.shokorelay")
        {
            return (null, null);
        }

        var animeId = int.Parse(guid.Authority);
        var series = int.Parse(guid.AbsolutePath.Split('/')[1]);
        var episodeNumber = int.Parse(guid.AbsolutePath.Split('/')[2]);

        //if (!metadata.Guid.StartsWith("com.plexapp.agents.shoko://")) return (null, null);

        //string[] parts = metadata.Guid.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
        //int animeId = int.Parse(parts[1]);
        //int series = int.Parse(parts[2]);

        var anime = RepoFactory.AnimeSeries.GetByID(animeId);

        EpisodeType episodeType;
        switch
            (series) //I hate magic number's but this is just about how I can do this, also the rest of this is for later.
        {
            case -4:
                episodeType = EpisodeType.Parody;
                break;
            case -3:
                episodeType = EpisodeType.Trailer;
                break;
            case -2:
                episodeType = EpisodeType.Other;
                break;
            case -1:
                episodeType = EpisodeType.Credits;
                break;
            case 0:
                episodeType = EpisodeType.Special;
                break;
            default:
                episodeType = EpisodeType.Episode;
                break;
        }

        if (episodeType != EpisodeType.Episode ||
            metadata.Index == 0) //metadata.index = 0 when it's something else.
        {
            return (null, anime); //right now no clean way to detect the episode. I could do by title.
        }


        var animeEps = anime
            .AnimeEpisodes.Where(a => a.EpisodeType == episodeType && a.AniDB_Episode?.EpisodeNumber == episodeNumber).ToList();

        //if only one possible match
        if (animeEps.Count == 1) return (animeEps.First(), anime);

        // Check for Tmdb matches
        AnimeEpisode result;
        if ((result = animeEps.FirstOrDefault(a => a.TmdbEpisodes.Any(e => e.SeasonNumber == series))) != null)
        {
            return (result, anime);
        }

        //catch all
        _logger.LogInformation($"Unable to work out the metadata for {metadata.Guid}, this might be a clash of multiple episodes linked, but no tmdb link.");
        return (null, anime);
    }

    [NonAction]
    public DateTime FromUnixTime(long unixTime)
    {
        return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(unixTime);
    }

    [Authorize]
    [HttpGet("loginurl")]
    public string GetLoginUrl()
    {
        return CallPlexHelper(h => h.LoginUrl);
    }

    [Authorize]
    [HttpGet("pin/authenticated")]
    public bool IsAuthenticated()
    {
        return CallPlexHelper(h => h.IsAuthenticated);
    }

    [Authorize]
    [HttpGet("token/invalidate")]
    public bool InvalidateToken()
    {
        return CallPlexHelper(h =>
        {
            h.InvalidateToken();
            return true;
        });
    }

    [Authorize]
    [HttpGet("sync")]
    public async Task<ActionResult> Sync()
    {
        var scheduler = await _schedulerFactory.GetScheduler();
        await scheduler.StartJob<SyncPlexWatchedStatesJob>(c => c.User = HttpContext.GetUser());
        return Ok();
    }

    [Authorize("admin")]
    [HttpGet("sync/all")]
    public async Task<ActionResult> SyncAll()
    {
        await Utils.ShokoServer.SyncPlex();
        return Ok();
    }

    [Authorize("admin")]
    [HttpGet("sync/{id:int}")]
    public async Task<ActionResult> SyncForUser(int id)
    {
        var user = RepoFactory.JMMUser.GetByID(id);
        if (string.IsNullOrEmpty(user.PlexToken))
        {
            return BadRequest("Invalid User ID");
        }

        var scheduler = await _schedulerFactory.GetScheduler();
        await scheduler.StartJob<SyncPlexWatchedStatesJob>(c => c.User = HttpContext.GetUser());
        return Ok();
    }

    [Authorize]
    [HttpGet("libraries")]
    public ActionResult<Directory[]> GetLibraries()
    {
        var result = CallPlexHelper(h => h.GetDirectories());

        if (result.Length == 0)
            return NotFound("No directories found please ensure server token is set and try again");

        return result;
    }

    [Authorize, HttpPost("libraries")]
    public ActionResult SetLibraries([FromBody] List<int> ids)
    {
        return CallPlexHelper<ActionResult>(h =>
        {
            if (ids.Count == 0)
            {
                SettingsProvider.GetSettings().Plex.Libraries = [];
                SettingsProvider.SaveSettings();
                return Ok();
            }

            var dirs = h.GetDirectories();
            var selected = dirs.Where(d => ids.Contains(d.Key)).ToList();
            if (selected.Count == 0)
                return BadRequest("No directories found please ensure server token is set and try again");

            SettingsProvider.GetSettings().Plex.Libraries = selected.Select(s => s.Key).ToList();
            SettingsProvider.SaveSettings();
            return Ok();
        });
    }

    [HttpGet("server/list"), Authorize]
    public ActionResult<List<MediaDevice>> Servers()
    {
        return CallPlexHelper(h => h.GetPlexServers());
    }

    [HttpPost("server"), Authorize]
    public ActionResult SetServer([FromBody] string clientId)
    {
        return CallPlexHelper<ActionResult>(h =>
        {
            var servers = h.GetPlexServers();
            var server = servers.FirstOrDefault(s => s.ClientIdentifier == clientId);
            if (server == null)
                return BadRequest("Invalid Client ID");

            h.UseServer(server);
            return Ok();
        });
    }
#if DEBUG

    [Authorize]
    [HttpGet("libraries/{id}")]
    public PlexLibrary[] GetShowsForDirectory(int id)
    {
        return CallPlexHelper(h => ((SVR_Directory)h.GetDirectories().FirstOrDefault(d => d.Key == id))?.GetShows());
    }
#endif

    [NonAction]
    private T CallPlexHelper<T>(Func<PlexHelper, T> act)
    {
        var user = HttpContext.GetUser();
        return act(PlexHelper.GetForUser(user));
    }

    #region plexapi

#pragma warning disable 0649

    [DataContract]
    public class PlexEvent
    {
        [Required, DataMember(Name = "event")]
        public string Event;

        [DataMember(Name = "user")] public bool User;

        [DataMember(Name = "owner")] public bool Owner;

        [Required, DataMember(Name = "Account")]
        public PlexAccount Account;

        [Required, DataMember(Name = "Server")]
        public PlexBasicInfo Server;

        [DataMember(Name = "Player")] public PlexPlayerInfo Player;

        [Required, DataMember(Name = "Metadata")]
        public PlexMetadata Metadata;

        [DataContract]
        public class PlexAccount
        {
            [DataMember(Name = "id")] public int Id;

            [DataMember(Name = "thumb")] public string Thumbnail;

            [DataMember(Name = "title")] public string Title;
        }

        [DataContract]
        public class PlexBasicInfo
        {
            [DataMember(Name = "title")] public string Title;

            [DataMember(Name = "uuid")] public string Uuid;
        }

        [DataContract]
        public class PlexPlayerInfo : PlexBasicInfo
        {
            [DataMember(Name = "local")] public bool Local;

            [DataMember(Name = "publicAddress")] public string PublicAddress;
        }

        [DataContract]
        public class PlexMetadata
        {
            #region Library information

            [DataMember(Name = "librarySectionType")]
            public string LibrarySectionType;

            [DataMember(Name = "librarySectionTitle")]
            public string LibrarySectionTitle;

            [DataMember(Name = "librarySectionId")]
            public int LibrarySectionId;

            [DataMember(Name = "librarySectionKey")]
            public string LibrarySectionKey;

            #endregion

            #region Item information

            [Required, DataMember(Name = "guid")] public string Guid;

            [DataMember(Name = "key")] public string Key;

            [DataMember(Name = "index")] public int? Index;

            [DataMember(Name = "type")] public string Type;

            [DataMember(Name = "contentRating")] public string ContentRating;

            [DataMember(Name = "studio")] public string Studio;

            [DataMember(Name = "title")] public string Title;

            [DataMember(Name = "originalTitle")] public string OriginalTitle;

            [DataMember(Name = "summary")] public string Summary;

            [DataMember(Name = "thumb")] public string Thumbnail;

            [DataMember(Name = "art")] public string Art;

            [DataMember(Name = "addedAt")] public int AddedAt;

            [DataMember(Name = "updatedAt")] public int UpdatedAt;

            [DataMember(Name = "lastViewedAt")] public int LastViewedAt;

            [DataMember(Name = "viewOffset")] public int ViewOffset;

            [DataMember(Name = "duration")] public int? Duration;

            [DataMember(Name = "Guid")] public PlexProviderInfo[] Providers;

            #endregion

            #region Parent item information

            [Required, DataMember(Name = "parentGuid")]
            public string ParentGuid;

            [DataMember(Name = "parentIndex")] public int ParentIndex;

            [DataMember(Name = "parentTitle")] public string ParentTitle;

            [DataMember(Name = "parentThumb")] public string ParentThumbnail;

            #endregion

            #region Grand-parent item information

            [Required, DataMember(Name = "grandParentGuid")]
            public string GrandParentGuid;

            [DataMember(Name = "grandParentTitle")]
            public string GrandParentTitle;

            [DataMember(Name = "grandparentThumb")]
            public string GrandParentThumbnail;

            [DataMember(Name = "grandparentArt")] public string GrandparentArt;

            [DataMember(Name = "grandparentTheme")]
            public string GrandparentTheme;

            #endregion
        }

        [DataContract]
        public class PlexProviderInfo
        {
            [DataMember(Name = "id")] public string Id;
        }
    }
}

internal class PlexBinder : IModelBinder //credit to https://stackoverflow.com/a/46344854
{
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        ArgumentNullException.ThrowIfNull(bindingContext);

        // Check the value sent in
        var valueProviderResult = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);
        if (valueProviderResult == ValueProviderResult.None)
        {
            bindingContext.Result = ModelBindingResult.Failed();
            return Task.CompletedTask;
        }

        bindingContext.ModelState.SetModelValue(bindingContext.ModelName, valueProviderResult);

        // Attempt to convert the input value
        var valueAsString = valueProviderResult.FirstValue;
        if (valueAsString == null)
        {
            bindingContext.Result = ModelBindingResult.Failed();
            return Task.CompletedTask;
        }
        var result = JsonConvert.DeserializeObject(valueAsString, bindingContext.ModelType);
        if (result == null)
        {
            bindingContext.Result = ModelBindingResult.Failed();
            return Task.CompletedTask;
        }

        bindingContext.Result = ModelBindingResult.Success(result);
        return Task.CompletedTask;

    }
}

#pragma warning restore 0649

#endregion
