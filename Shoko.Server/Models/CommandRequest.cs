using System;
using System.Runtime.Serialization;
using System.Xml.Serialization;
using Shoko.Commons.Queue;
using Shoko.Server.Commands;
using Shoko.Server.Commands.Interfaces;
using Shoko.Server.Server;

namespace Shoko.Server.Models;

public abstract class CommandRequest
{
    [XmlIgnore, IgnoreDataMember]
    public virtual int CommandRequestID { get; set; }

    [XmlIgnore, IgnoreDataMember]
    public virtual int Priority { get; set; }

    [XmlIgnore, IgnoreDataMember]
    public virtual int CommandType { get; set; }

    [XmlIgnore, IgnoreDataMember]
    public virtual string CommandID { get; set; }

    [XmlIgnore, IgnoreDataMember]
    public virtual string CommandDetails { get; set; }

    [XmlIgnore, IgnoreDataMember]
    public virtual DateTime DateTimeUpdated { get; set; }

    [XmlIgnore, IgnoreDataMember]
    public abstract CommandRequestPriority DefaultPriority { get; }

    [XmlIgnore, IgnoreDataMember]
    public abstract QueueStateStruct PrettyDescription { get; }

    [XmlIgnore, IgnoreDataMember]
    public abstract CommandConflict ConflictBehavior { get; }

    [XmlIgnore, IgnoreDataMember]
    public virtual ICommandProcessor Processor { get; set; }

    public abstract void PostInit();

    public abstract void ProcessCommand();

    public abstract bool LoadFromCommandDetails(string commandDetails);

    public abstract void UpdateCommandDetails();

    public abstract void GenerateCommandID();
}
