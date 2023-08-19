using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml;
using System.Xml.Serialization;
using Microsoft.Extensions.Logging;
using Shoko.Server.Commands.Attributes;
using Shoko.Server.Models;
using ILoggerFactory = Microsoft.Extensions.Logging.ILoggerFactory;

namespace Shoko.Server.Commands.Generic;

public abstract class CommandRequestImplementation : CommandRequest
{
    [XmlIgnore][JsonIgnore] protected readonly ILogger Logger;

    // ignoring the base properties so that when we serialize we only get the properties
    // defined in the concrete class

    [XmlIgnore][JsonIgnore] public virtual bool BubbleExceptions { get; set; } = false;

    protected CommandRequestImplementation(ILoggerFactory loggerFactory) : this()
    {
        Logger = loggerFactory.CreateLogger(GetType());
    }

    [SuppressMessage("ReSharper", "VirtualMemberCallInConstructor")]
    protected CommandRequestImplementation()
    {
        CommandType = (int?)GetType().GetCustomAttribute<CommandAttribute>()?.RequestType ?? -1;
        Priority = (int)DefaultPriority;
    }

    /// <summary>
    /// Inherited classes to provide the implementation of how to process this command
    /// </summary>
    protected abstract void Process();

    public override void PostInit() { }

    public override void ProcessCommand()
    {
        try
        {
            Process();
        }
        catch (Exception e)
        {
            if (BubbleExceptions) throw;

            Logger.LogError(e, "Error processing {Type}: {CommandDetails}", GetType().Name, ToJson());
        }
    }

    public override CommandConflict ConflictBehavior => CommandConflict.Ignore;

    protected virtual string GetCommandDetails()
    {
        return ToXML();
    }

    public override bool LoadFromCommandDetails(string commandDetails)
    {
        CommandDetails = commandDetails;
        return Load();
    }

    protected abstract bool Load();

    public override void UpdateCommandDetails()
    {
        CommandDetails = GetCommandDetails();
    }

    private string ToXML()
    {
        var ns = new XmlSerializerNamespaces();
        ns.Add("", string.Empty);

        var serializer = new XmlSerializer(GetType());
        var settings = new XmlWriterSettings
        {
            OmitXmlDeclaration = true // Remove the <?xml version="1.0" encoding="utf-8"?>
        };
        var sb = new StringBuilder();
        var writer = XmlWriter.Create(sb, settings);
        serializer.Serialize(writer, this, ns);

        return sb.ToString();
    }

    private string ToJson()
    {
        return JsonSerializer.Serialize(this, GetType(), new JsonSerializerOptions
        {
            WriteIndented = false, MaxDepth = 5, IgnoreReadOnlyProperties = true, IncludeFields = false
        });
    }

}
