using System;
using System.Linq;
using System.Xml;
using Microsoft.Extensions.Logging;
using Shoko.Commons.Queue;
using Shoko.Models.Enums;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Commands.AniDB;
using Shoko.Server.Commands.Attributes;
using Shoko.Server.Commands.Generic;
using Shoko.Server.Models;
using Shoko.Server.Repositories;
using Shoko.Server.Server;
using Shoko.Server.Settings;

namespace Shoko.Server.Commands;

[Serializable]
[Command(CommandRequestType.LinkFileManually)]
public class CommandRequest_LinkFileManually : CommandRequestImplementation
{
    private readonly ICommandRequestFactory _commandFactory;
    private readonly IServerSettings _settings;
    public virtual int VideoLocalID { get; set; }
    public virtual int EpisodeID { get; set; }
    public virtual int Percentage { get; set; }

    private SVR_AnimeEpisode _episode;
    private SVR_VideoLocal _vlocal;

    public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority3;

    public override QueueStateStruct PrettyDescription
    {
        get
        {
            if (_vlocal != null && _episode != null)
            {
                return new QueueStateStruct
                {
                    message = "Linking File: {0} to Episode: {1}",
                    queueState = QueueStateEnum.LinkFileManually,
                    extraParams = new[] { _vlocal.FileName, _episode.Title }
                };
            }

            return new QueueStateStruct
            {
                message = "Linking File: {0} to Episode: {1}",
                queueState = QueueStateEnum.LinkFileManually,
                extraParams = new[] { VideoLocalID.ToString(), EpisodeID.ToString() }
            };
        }
    }

    protected override void Process()
    {
        var xref = new CrossRef_File_Episode
        {
            Hash = _vlocal.ED2KHash,
            FileName = _vlocal.FileName,
            FileSize = _vlocal.FileSize,
            CrossRefSource = (int)CrossRefSource.User,
            AnimeID = _episode.AniDB_Episode.AnimeID,
            EpisodeID = _episode.AniDB_EpisodeID,
            Percentage = Percentage is > 0 and <= 100 ? Percentage : 100,
            EpisodeOrder = 1
        };

        RepoFactory.CrossRef_File_Episode.Save(xref);

        ProcessFileQualityFilter();

        _vlocal.Places.ForEach(a => { a.RenameAndMoveAsRequired(); });

        // Set the import date.
        _vlocal.DateTimeImported = DateTime.Now;
        RepoFactory.VideoLocal.Save(_vlocal);

        var ser = _episode.GetAnimeSeries();
        ser.EpisodeAddedDate = DateTime.Now;
        RepoFactory.AnimeSeries.Save(ser, false, true);

        //Update will re-save
        ser.QueueUpdateStats();

        foreach (var grp in ser.AllGroupsAbove)
        {
            grp.EpisodeAddedDate = DateTime.Now;
            RepoFactory.AnimeGroup.Save(grp, false, false);
        }

        ShokoEventHandler.Instance.OnFileMatched(_vlocal.GetBestVideoLocalPlace(), _vlocal);

        if (_settings.AniDb.MyList_AddFiles)
        {
            var cmdAddFile = _commandFactory.Create<CommandRequest_AddFileToMyList>(c => c.Hash = _vlocal.Hash);
            cmdAddFile.Save();
        }
    }

    private void ProcessFileQualityFilter()
    {
        if (!_settings.FileQualityFilterEnabled) return;

        var videoLocals = _episode.GetVideoLocals();
        if (videoLocals == null) return;

        videoLocals.Sort(FileQualityFilter.CompareTo);
        var keep = videoLocals.Take(FileQualityFilter.Settings.MaxNumberOfFilesToKeep).ToList();
        foreach (var vl2 in keep) videoLocals.Remove(vl2);

        if (videoLocals.Contains(_vlocal)) videoLocals.Remove(_vlocal);

        videoLocals = videoLocals.Where(FileQualityFilter.CheckFileKeep).ToList();

        foreach (var toDelete in videoLocals) toDelete.Places.ForEach(a => a.RemoveRecordAndDeletePhysicalFile());
    }

    /// <summary>
    /// This should generate a unique key for a command
    /// It will be used to check whether the command has already been queued before adding it
    /// </summary>
    public override void GenerateCommandID()
    {
        CommandID = $"CommandRequest_LinkFileManually_{VideoLocalID}_{EpisodeID}";
    }

    public override bool LoadFromCommandDetails()
    {
        // read xml to get parameters
        if (CommandDetails.Trim().Length <= 0) return false;

        var docCreator = new XmlDocument();
        docCreator.LoadXml(CommandDetails);

        // populate the fields
        VideoLocalID = int.Parse(TryGetProperty(docCreator, "CommandRequest_LinkFileManually", "VideoLocalID"));
        EpisodeID = int.Parse(TryGetProperty(docCreator, "CommandRequest_LinkFileManually", "EpisodeID"));
        Percentage = int.Parse(TryGetProperty(docCreator, "CommandRequest_LinkFileManually", "Percentage"));
        _vlocal = RepoFactory.VideoLocal.GetByID(VideoLocalID);
        if (_vlocal == null)
        {
            Logger.LogWarning("VideoLocal object {VideoLocalID} not found", VideoLocalID);
            return false;
        }

        _episode = RepoFactory.AnimeEpisode.GetByID(EpisodeID);
        if (_episode?.GetAnimeSeries() == null)
        {
            Logger.LogWarning("Local Episode or Series object {EpisodeID} not found", EpisodeID);
            return false;
        }

        return true;
    }

    public CommandRequest_LinkFileManually(ILoggerFactory loggerFactory, ICommandRequestFactory commandFactory, ISettingsProvider settingsProvider) :
        base(loggerFactory)
    {
        _commandFactory = commandFactory;
        _settings = settingsProvider.GetSettings();
    }

    protected CommandRequest_LinkFileManually()
    {
    }
}
