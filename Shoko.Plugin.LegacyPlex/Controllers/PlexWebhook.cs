using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.User;
using Shoko.Abstractions.User.Services;
using Shoko.Plugin.LegacyPlex.Jobs;
using Shoko.Plugin.LegacyPlex.Models.Connections;
using Shoko.Plugin.LegacyPlex.Models.Libraries;
using Shoko.QueueProcessor.Abstractions;
using Shoko.Server.Settings;

#if DEBUG
using Shoko.Plugin.LegacyPlex.Libraries;
using Shoko.Plugin.LegacyPlex.Models.Collection;
#endif

namespace Shoko.Plugin.LegacyPlex.Controllers;

/// <summary>
///   The legacy Plex routes at <c>/plex</c>: the webhook Plex posts plays to,
///   the Plex account linking, the server and library picks, and the sync.
/// </summary>
[ApiController, Tags("Plex")]
[Route("/plex")]
[ApiVersionNeutral]
public class PlexWebhook : Controller
{
    private readonly ILogger<PlexWebhook> _logger;
    private readonly IQueueScheduler _scheduler;
    private readonly IUserService _userService;
    private readonly IUserDataService _userDataService;
    private readonly IMetadataService _metadataService;
    private readonly PlexHelperProvider _plexHelpers;
    private readonly ISettingsProvider _settingsProvider;

    public PlexWebhook(
        ILogger<PlexWebhook> logger,
        IQueueScheduler scheduler,
        IUserService userService,
        IUserDataService userDataService,
        IMetadataService metadataService,
        PlexHelperProvider plexHelpers,
        ISettingsProvider settingsProvider
    )
    {
        _logger = logger;
        _scheduler = scheduler;
        _userService = userService;
        _userDataService = userDataService;
        _metadataService = metadataService;
        _plexHelpers = plexHelpers;
        _settingsProvider = settingsProvider;
    }

    private IUser? CurrentUser => _userService.GetUserFromHttpContext(HttpContext);

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

        _logger.LogTrace("{Event}: {Guid}", payload.Event, payload.Metadata.Guid);

        if (string.Equals(payload.Event, "media.scrobble", StringComparison.InvariantCultureIgnoreCase))
            await Scrobble(payload, CurrentUser);

        return Ok(); //doesn't need to be an ApiStatus.OK() since really, all I take is plex.
    }


    #region Plex events

    [NonAction]
    private async Task Scrobble(PlexEvent data, IUser? user)
    {
        var metadata = data.Metadata;
        var (episode, anime) = GetEpisode(metadata);
        if (episode == null)
        {
            _logger.LogInformation(
                "No episode returned, aborting scrobble. This might not have been a ShokoMetadata library");
            return;
        }

        _logger.LogTrace("Got anime: {Anime}, ep: {EpisodeNumber}", anime?.Title, episode.EpisodeNumber);

        user ??= _plexHelpers.GetUserByPlexUsername(data.Account.Title);
        if (user == null)
        {
            _logger.LogInformation(
                "Unable to determine who \"{AccountTitle}\" is in Shoko, make sure this is set under user settings in Desktop",
                data.Account.Title
            );
            return; //At this point in time, we don't want to scrobble for unknown users
        }

        await _userDataService.SetEpisodeWatchedStatus(episode, user, true, DateTime.UnixEpoch.AddSeconds(metadata.LastViewedAt));
    }

    #endregion

    [NonAction]
    private (IShokoEpisode?, IShokoSeries?) GetEpisode(PlexEvent.PlexMetadata metadata)
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

        var anime = _metadataService.GetShokoSeriesByID(animeId);
        if (anime is null)
        {
            return (null, null);
        }

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
            .Episodes.Where(a => a.Type == episodeType && a.EpisodeNumber == episodeNumber).ToList();

        //if only one possible match
        if (animeEps.Count == 1) return (animeEps.First(), anime);

        // Check for TMDB matches
        var result = animeEps.FirstOrDefault(a => a.LinkedEpisodes.Any(e => e.Source == MetadataSource.TMDB && e.SeasonNumber == series));
        if (result != null)
        {
            return (result, anime);
        }

        //catch all
        _logger.LogInformation("Unable to work out the metadata for {Guid}, this might be a clash of multiple episodes linked, but no tmdb link.", metadata.Guid);
        return (null, anime);
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
        var userID = CurrentUser!.LocalID;
        await _scheduler.Enqueue<SyncPlexWatchedStatesJob>(c => c.UserID = userID);
        return Ok();
    }

    [Authorize("admin")]
    [HttpGet("sync/all")]
    public async Task<ActionResult> SyncAll()
    {
        foreach (var user in _plexHelpers.GetLinkedUsers())
            await _scheduler.Enqueue<SyncPlexWatchedStatesJob>(c => c.UserID = user.JMMUserID);
        return Ok();
    }

    [Authorize("admin")]
    [HttpGet("sync/{userID:int}")]
    public async Task<ActionResult> SyncForUser([Range(1, int.MaxValue)] int userID)
    {
        var user = _plexHelpers.GetLinkedUsers().FirstOrDefault(linked => linked.JMMUserID == userID);
        if (user is null)
        {
            return BadRequest("Invalid User ID");
        }
        await _scheduler.Enqueue<SyncPlexWatchedStatesJob>(c => c.UserID = user.JMMUserID);
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
            var settings = _settingsProvider.GetSettings().Plex;
            if (ids.Count == 0)
            {
                settings.Libraries = [];
                _settingsProvider.SaveSettings();
                return Ok();
            }

            var dirs = h.GetDirectories();
            var selected = dirs.Where(d => ids.Contains(d.Key)).ToList();
            if (selected.Count == 0)
                return BadRequest("No directories found please ensure server token is set and try again");

            settings.Libraries = selected.Select(s => s.Key).ToList();
            _settingsProvider.SaveSettings();
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
    [HttpGet("libraries/{libraryID}")]
    public PlexLibrary[]? GetShowsForDirectory(int libraryID)
    {
        return CallPlexHelper(h => ((SVR_Directory?)h.GetDirectories().FirstOrDefault(d => d.Key == libraryID))?.GetShows());
    }
#endif

    [NonAction]
    private T CallPlexHelper<T>(Func<PlexHelper, T> act)
    {
        return act(_plexHelpers.GetForUser(CurrentUser!));
    }

    #region plexapi

#pragma warning disable 0649

    [DataContract]
    public class PlexEvent
    {
        [Required, DataMember(Name = "event")]
        public string Event = null!;

        [DataMember(Name = "user")] public bool User;

        [DataMember(Name = "owner")] public bool Owner;

        [Required, DataMember(Name = "Account")]
        public PlexAccount Account = null!;

        [Required, DataMember(Name = "Server")]
        public PlexBasicInfo Server = null!;

        [DataMember(Name = "Player")] public PlexPlayerInfo Player = null!;

        [Required, DataMember(Name = "Metadata")]
        public PlexMetadata Metadata = null!;

        [DataContract]
        public class PlexAccount
        {
            [DataMember(Name = "id")] public int Id;

            [DataMember(Name = "thumb")] public string Thumbnail = null!;

            [DataMember(Name = "title")] public string Title = null!;
        }

        [DataContract]
        public class PlexBasicInfo
        {
            [DataMember(Name = "title")] public string Title = null!;

            [DataMember(Name = "uuid")] public string Uuid = null!;
        }

        [DataContract]
        public class PlexPlayerInfo : PlexBasicInfo
        {
            [DataMember(Name = "local")] public bool Local;

            [DataMember(Name = "publicAddress")] public string PublicAddress = null!;
        }

        [DataContract]
        public class PlexMetadata
        {
            #region Library information

            [DataMember(Name = "librarySectionType")]
            public string LibrarySectionType = null!;

            [DataMember(Name = "librarySectionTitle")]
            public string LibrarySectionTitle = null!;

            [DataMember(Name = "librarySectionId")]
            public int LibrarySectionId;

            [DataMember(Name = "librarySectionKey")]
            public string LibrarySectionKey = null!;

            #endregion

            #region Item information

            [Required, DataMember(Name = "guid")] public string Guid = null!;

            [DataMember(Name = "key")] public string Key = null!;

            [DataMember(Name = "index")] public int? Index;

            [DataMember(Name = "type")] public string Type = null!;

            [DataMember(Name = "contentRating")] public string ContentRating = null!;

            [DataMember(Name = "studio")] public string Studio = null!;

            [DataMember(Name = "title")] public string Title = null!;

            [DataMember(Name = "originalTitle")] public string OriginalTitle = null!;

            [DataMember(Name = "summary")] public string Summary = null!;

            [DataMember(Name = "thumb")] public string Thumbnail = null!;

            [DataMember(Name = "art")] public string Art = null!;

            [DataMember(Name = "addedAt")] public int AddedAt;

            [DataMember(Name = "updatedAt")] public int UpdatedAt;

            [DataMember(Name = "lastViewedAt")] public int LastViewedAt;

            [DataMember(Name = "viewOffset")] public int ViewOffset;

            [DataMember(Name = "duration")] public int? Duration;

            [DataMember(Name = "Guid")] public PlexProviderInfo[] Providers = null!;

            #endregion

            #region Parent item information

            [Required, DataMember(Name = "parentGuid")]
            public string ParentGuid = null!;

            [DataMember(Name = "parentIndex")] public int ParentIndex;

            [DataMember(Name = "parentTitle")] public string ParentTitle = null!;

            [DataMember(Name = "parentThumb")] public string ParentThumbnail = null!;

            #endregion

            #region Grand-parent item information

            [Required, DataMember(Name = "grandParentGuid")]
            public string GrandParentGuid = null!;

            [DataMember(Name = "grandParentTitle")]
            public string GrandParentTitle = null!;

            [DataMember(Name = "grandparentThumb")]
            public string GrandParentThumbnail = null!;

            [DataMember(Name = "grandparentArt")] public string GrandparentArt = null!;

            [DataMember(Name = "grandparentTheme")]
            public string GrandparentTheme = null!;

            #endregion
        }

        [DataContract]
        public class PlexProviderInfo
        {
            [DataMember(Name = "id")] public string Id = null!;
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
