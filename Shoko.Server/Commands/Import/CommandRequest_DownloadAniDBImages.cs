using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Xml;
using Microsoft.Extensions.Logging;
using Shoko.Commons.Properties;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Commands.Attributes;
using Shoko.Server.Commands.Generic;
using Shoko.Server.ImageDownload;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Repositories;
using Shoko.Server.Server;
using Shoko.Server.Settings;

namespace Shoko.Server.Commands.Import;

[Command(CommandRequestType.DownloadAniDBImages)]
public class CommandRequest_DownloadAniDBImages : CommandRequestImplementation
{
    private const string DownloadImage = "Downloading Image {0}: {1}";

    private const string FailedToDownloadNoID = "Images failed to download: Can\'t find {EntityType} with ID: {AnimeID}";

    private readonly IUDPConnectionHandler _handler;

    private readonly ISettingsProvider _settingsProvider;

    public int AnimeID { get; set; }

    public bool ForceDownload { get; set; }

    public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority1;

    public override QueueStateStruct PrettyDescription => new()
    {
        message = DownloadImage,
        queueState = QueueStateEnum.DownloadImage,
        extraParams = new[] { Resources.Command_ValidateAllImages_AniDBPosters, AnimeID.ToString() }
    };

    public QueueStateStruct PrettyDescriptionCharacters => new()
    {
        message = DownloadImage,
        queueState = QueueStateEnum.DownloadImage,
        extraParams = new[] { Resources.Command_ValidateAllImages_AniDBCharacters, AnimeID.ToString() }
    };

    public QueueStateStruct PrettyDescriptionCreators => new()
    {
        message = DownloadImage,
        queueState = QueueStateEnum.DownloadImage,
        extraParams = new[] { Resources.Command_ValidateAllImages_AniDBSeiyuus, AnimeID.ToString() }
    };

    protected override void Process()
    {
        Logger.LogInformation("Processing CommandRequest_DownloadAniDBImages: {AnimeID}", AnimeID);

        try
        {
            var settings = _settingsProvider.GetSettings();
            var anime = RepoFactory.AniDB_Anime.GetByAnimeID(AnimeID);
            if (anime == null)
            {
                Logger.LogWarning(FailedToDownloadNoID, "anime", AnimeID);
                return;
            }

            var requests = new List<ImageDownloadRequest>()
            {
                new ImageDownloadRequest(anime, ForceDownload, _handler.ImageServerUrl),
            };

            var characterOffset = requests.Count;
            if (settings.AniDb.DownloadCharacters)
            {
                var characters = RepoFactory.AniDB_Anime_Character.GetByAnimeID(AnimeID)
                    .Select(xref => RepoFactory.AniDB_Character.GetByCharID(xref.CharID))
                    .Where(a => !string.IsNullOrEmpty(a?.PicName))
                    .DistinctBy(a => a.CharID)
                    .ToList();
                if (characters == null || characters.Count == 0)
                {
                    Logger.LogWarning(FailedToDownloadNoID, "characters for anime", AnimeID);
                    return;
                }

                requests.AddRange(characters.Select(character => new ImageDownloadRequest(character, ForceDownload, _handler.ImageServerUrl)));
            }

            var creatorOffset = requests.Count;
            if (settings.AniDb.DownloadCreators)
            {
                // Get all voice-actors working on this anime.
                var voiceActors =  RepoFactory.AniDB_Anime_Character.GetByAnimeID(AnimeID)
                    .SelectMany(xref => RepoFactory.AniDB_Character_Seiyuu.GetByCharID(xref.CharID))
                    .Select(xref => RepoFactory.AniDB_Seiyuu.GetBySeiyuuID(xref.SeiyuuID))
                    .Where(va => !string.IsNullOrEmpty(va?.PicName));
                // Get all staff members working on this anime.
                var staffMembers = RepoFactory.AniDB_Anime_Staff.GetByAnimeID(AnimeID)
                    .Select(xref => RepoFactory.AniDB_Seiyuu.GetBySeiyuuID(xref.CreatorID))
                    .Where(staff => !string.IsNullOrEmpty(staff?.PicName));
                // Concatenate the streams into a single list.
                var creators = voiceActors
                    .Concat(staffMembers)
                    .DistinctBy(creator => creator.SeiyuuID)
                    .ToList();

                if (creators == null || creators.Count == 0)
                {
                    Logger.LogWarning(FailedToDownloadNoID, "creators for anime", AnimeID);
                    return;
                }

                requests.AddRange(creators.Select(va => new ImageDownloadRequest(va, ForceDownload, _handler.ImageServerUrl)));
            }

            for (var index = 0; index < requests.Count; index++)
            {
                // Update the queue state.
                if (index == characterOffset)
                    ShokoService.CmdProcessorImages.QueueState = PrettyDescriptionCharacters;
                else if (index == creatorOffset)
                    ShokoService.CmdProcessorImages.QueueState = PrettyDescriptionCreators;

                // Process the
                var req = requests[index];
                try
                {
                    // If this has any issues, it will throw an exception, so the catch below will handle it.
                    if (req.DownloadNow())
                        Logger.LogInformation("Image downloaded: {FilePath} from {DownloadUrl}", req.FilePath, req.DownloadUrl);
                    else
                        Logger.LogWarning("Image failed to download; {FilePath} from {DownloadUrl}", req.FilePath, req.DownloadUrl);
                }
                catch (WebException e)
                {
                    Logger.LogWarning(
                        "Error processing CommandRequest_DownloadAniDBImages: {Url} ({AnimeID}) - {Ex}",
                        req.DownloadUrl,
                        AnimeID,
                        e.Message);
                }
                catch (Exception e)
                {
                    Logger.LogError(e,
                        "Error processing CommandRequest_DownloadAniDBImages: {Url} ({AnimeID}) - {Ex}",
                        req.DownloadUrl,
                        AnimeID,
                        e);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error processing CommandRequest_DownloadAniDBImages: {AnimeID}", AnimeID);
        }
    }

    public override void GenerateCommandID()
    {
        CommandID = $"CommandRequest_DownloadImage_{AnimeID}_{ForceDownload}";
    }

    public override bool LoadFromDBCommand(CommandRequest cq)
    {
        CommandID = cq.CommandID;
        CommandRequestID = cq.CommandRequestID;
        Priority = cq.Priority;
        CommandDetails = cq.CommandDetails;
        DateTimeUpdated = cq.DateTimeUpdated;

        // read xml to get parameters
        if (CommandDetails.Trim().Length <= 0) return false;

        var docCreator = new XmlDocument();
        docCreator.LoadXml(CommandDetails);

        // populate the fields
        AnimeID = int.Parse(TryGetProperty(docCreator, "CommandRequest_DownloadAniDBImages", "AnimeID"));
        ForceDownload =
            bool.Parse(TryGetProperty(docCreator, "CommandRequest_DownloadAniDBImages", "ForceDownload"));

        return true;
    }

    public override CommandRequest ToDatabaseObject()
    {
        GenerateCommandID();

        var cq = new CommandRequest
        {
            CommandID = CommandID,
            CommandType = CommandType,
            Priority = Priority,
            CommandDetails = ToXML(),
            DateTimeUpdated = DateTime.Now
        };
        return cq;
    }

    public CommandRequest_DownloadAniDBImages(ILoggerFactory loggerFactory, IUDPConnectionHandler handler, ISettingsProvider settingsProvider) :
        base(loggerFactory)
    {
        _handler = handler;
        _settingsProvider = settingsProvider;
    }

    protected CommandRequest_DownloadAniDBImages()
    {
    }
}
