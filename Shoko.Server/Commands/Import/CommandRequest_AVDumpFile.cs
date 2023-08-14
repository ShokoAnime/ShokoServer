using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using System.Xml.Serialization;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Plugin.Abstractions;
using Shoko.Server.Commands.Attributes;
using Shoko.Server.Commands.Generic;
using Shoko.Server.Server;

namespace Shoko.Server.Commands;

[Serializable]
[Command(CommandRequestType.AVDumpFile)]
public class CommandRequest_AVDumpFile : CommandRequestImplementation
{
    [XmlIgnore]
    public virtual Dictionary<int, string> Videos { get; set; }

    [XmlArray("Videos"), XmlArrayItem("Item")]
    public virtual Item[] VideoItems
    {
        get => Videos.Select(pair => new Item() { Key = pair.Key, Value = pair.Value }).OrderBy(item => item.Key).ToArray();
        set => Videos = value.ToDictionary(v => v.Key, v => v.Value);
    }

    [XmlIgnore, JsonIgnore]
    public virtual AVDumpHelper.AVDumpSession Result { get; protected set; } = null;

    public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority3;

    public override QueueStateStruct PrettyDescription => Videos.Count == 1 ? new()
    {
        message = "AVDump 1 file: {0} — {1}",
        queueState = QueueStateEnum.AVDumpFile,
        extraParams = new[] { Videos.Keys.First().ToString(), Videos.Values.First() },
    }
    : new()
    {
        message = "AVDump {0} files: {1}",
        queueState = QueueStateEnum.AVDumpFile,
        extraParams = new[] { Videos.Count.ToString(), string.Join(", ", Videos.Keys) },
    };

    [XmlIgnore, JsonIgnore]
    private double Progress { get; set; } = 0;

    protected override void Process()
    {
        int? sessionId = null;
        EventHandler<AVDumpEventArgs> eventHandler = (_, eventArgs) =>
        {
            // Guard against concurrent dumps.
            if (sessionId.HasValue ? eventArgs.SessionID != sessionId.Value : eventArgs.CommandID != CommandRequestID )
                return;

            switch (eventArgs.Type)
            {
                case AVDumpEventType.Started:
                    sessionId = eventArgs.SessionID.Value;
                    OnStart();
                    break;
                case AVDumpEventType.Progress:
                    OnProgressUpdate(eventArgs.Progress.Value);
                    break;
            }
        };

        try {
            ShokoEventHandler.Instance.AVDumpEvent += eventHandler;
            Result = AVDumpHelper.DumpFiles(Videos, CommandRequestID);
        }
        finally
        {
            ShokoEventHandler.Instance.AVDumpEvent -= eventHandler;
        }
    }

    private void OnStart()
    {
        if (Processor == null)
            return;

        Processor.QueueState = Videos.Count == 1 ? new()
        {
            message = "AVDumping 1 file: {0} — {1}",
            queueState = QueueStateEnum.AVDumpFile,
            extraParams = new[] { Videos.Keys.First().ToString(), Videos.Values.First() },
        }
        : new()
        {
            message = "AVDumping {0} files: {1}",
            queueState = QueueStateEnum.AVDumpFile,
            extraParams = new[] { Videos.Count.ToString(), string.Join(", ", Videos.Keys) },
        };
    }

    private void OnProgressUpdate(double progress)
    {
        if (Processor == null)
            return;

        // We don't need every progress change for the queue, so limit it to
        // one update per whole percent.
        var safeProgress = Math.Floor(progress);
        if (safeProgress <= Progress)
            return;

        Progress = safeProgress;
        Processor.QueueState = Videos.Count == 1 ? new()
        {
            message = "AVDumping 1 file: {0} — {1} — {2}%",
            queueState = QueueStateEnum.AVDumpFile,
            extraParams = new[] { Videos.Keys.First().ToString(), Videos.Values.First(), Math.Round(progress, 2).ToString() },
        }
        : new()
        {
            message = "AVDumping {0} files: {1} — {2}%",
            queueState = QueueStateEnum.AVDumpFile,
            extraParams = new[] { Videos.Count.ToString(), string.Join(", ", Videos.Keys), Math.Round(progress, 2).ToString() },
        };
    }

    public override void GenerateCommandID()
    {
        CommandID = $"CommandRequest_AVDumpFile_{string.Join(",", Videos.Keys.OrderBy(videoId => videoId))}";
    }

    public override bool LoadFromCommandDetails()
    {
        // read xml to get parameters
        if (CommandDetails.Trim().Length <= 0) return false;

        var docCreator = XDocument.Parse(CommandDetails);

        // populate the fields
        Videos = docCreator.Element("CommandRequest_AVDumpFile")!
                    .Element("Videos")!
                    .Elements()
                    .ToDictionary(x => int.Parse(x.Attribute("key")!.Value), x => x.Value);

        return true;
    }

    public CommandRequest_AVDumpFile(ILoggerFactory loggerFactory) : base(loggerFactory)
    {
    }

    protected CommandRequest_AVDumpFile()
    {
    }

    public class Item
    {
        [XmlAttribute("key")]
        public int Key;

        [XmlText]
        public string Value;
    }
}
