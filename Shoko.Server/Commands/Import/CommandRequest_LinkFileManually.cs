﻿using System;
using System.Linq;
using System.Xml;
using Microsoft.Extensions.Logging;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Commands.AniDB;
using Shoko.Server.Commands.Attributes;
using Shoko.Server.Commands.Generic;
using Shoko.Server.Extensions;
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
    public int VideoLocalID { get; set; }
    public int EpisodeID { get; set; }
    public int Percentage { get; set; }

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
        var xref = new CrossRef_File_Episode();
        try
        {
            xref.PopulateManually(_vlocal, _episode);
            if (Percentage is > 0 and <= 100)
            {
                xref.Percentage = Percentage;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error populating XREF: {VideoLocal}", _vlocal.ToStringDetailed());
            throw;
        }

        RepoFactory.CrossRef_File_Episode.Save(xref);

        if (ServerSettings.Instance.FileQualityFilterEnabled)
        {
            var videoLocals = _episode.GetVideoLocals();
            if (videoLocals != null)
            {
                videoLocals.Sort(FileQualityFilter.CompareTo);
                var keep = videoLocals.Take(FileQualityFilter.Settings.MaxNumberOfFilesToKeep)
                    .ToList();
                foreach (var vl2 in keep)
                {
                    videoLocals.Remove(vl2);
                }

                if (videoLocals.Contains(_vlocal))
                {
                    videoLocals.Remove(_vlocal);
                }

                videoLocals = videoLocals.Where(FileQualityFilter.CheckFileKeep).ToList();

                foreach (var toDelete in videoLocals)
                {
                    toDelete.Places.ForEach(a => a.RemoveRecordAndDeletePhysicalFile());
                }
            }
        }

        _vlocal.Places.ForEach(a => { a.RenameAndMoveAsRequired(); });

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

        ShokoEventHandler.Instance.OnFileMatched(_vlocal.GetBestVideoLocalPlace());

        if (ServerSettings.Instance.AniDb.MyList_AddFiles)
        {
            var cmdAddFile = _commandFactory.Create<CommandRequest_AddFileToMyList>(c => c.Hash = _vlocal.Hash);
            cmdAddFile.Save();
        }
    }

    /// <summary>
    /// This should generate a unique key for a command
    /// It will be used to check whether the command has already been queued before adding it
    /// </summary>
    public override void GenerateCommandID()
    {
        CommandID = $"CommandRequest_LinkFileManually_{VideoLocalID}_{EpisodeID}";
    }

    public override bool LoadFromDBCommand(CommandRequest cq)
    {
        CommandID = cq.CommandID;
        CommandRequestID = cq.CommandRequestID;
        Priority = cq.Priority;
        CommandDetails = cq.CommandDetails;
        DateTimeUpdated = cq.DateTimeUpdated;

        // read xml to get parameters
        if (CommandDetails.Trim().Length > 0)
        {
            var docCreator = new XmlDocument();
            docCreator.LoadXml(CommandDetails);

            // populate the fields
            VideoLocalID = int.Parse(TryGetProperty(docCreator, "CommandRequest_LinkFileManually", "VideoLocalID"));
            EpisodeID = int.Parse(TryGetProperty(docCreator, "CommandRequest_LinkFileManually", "EpisodeID"));
            Percentage = int.Parse(TryGetProperty(docCreator, "CommandRequest_LinkFileManually", "Percentage"));
            _vlocal = RepoFactory.VideoLocal.GetByID(VideoLocalID);
            if (null == _vlocal)
            {
                Logger.LogInformation("VideoLocal object {VideoLocalID} not found", VideoLocalID);
                return false;
            }

            _episode = RepoFactory.AnimeEpisode.GetByID(EpisodeID);
        }

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

    public CommandRequest_LinkFileManually(ILoggerFactory loggerFactory, ICommandRequestFactory commandFactory) :
        base(loggerFactory)
    {
        _commandFactory = commandFactory;
    }

    protected CommandRequest_LinkFileManually()
    {
    }
}
