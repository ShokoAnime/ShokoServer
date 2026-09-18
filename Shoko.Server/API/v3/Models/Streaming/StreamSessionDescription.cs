using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Newtonsoft.Json.Linq;
using Shoko.Abstractions.Video.Streaming;

namespace Shoko.Server.API.v3.Models.Streaming;

/// <summary>
/// What an active stream session offers beyond the stream itself.
/// </summary>
public class StreamSessionDescription
{
    /// <summary>
    /// The stream session ID.
    /// </summary>
    [Required]
    public Guid ID { get; init; }

    /// <summary>
    /// The ID of the transform that produced the session's rendition, if known.
    /// </summary>
    public string? TransformID { get; init; }

    /// <summary>
    /// How the session's stream is delivered.
    /// </summary>
    [Required]
    public StreamDeliveryMode DeliveryMode { get; init; }

    /// <summary>
    /// The URL of the stream: the master playlist for HLS, or the byte-range stream for progressive delivery.
    /// </summary>
    [Required]
    public string StreamUrl { get; init; } = string.Empty;

    /// <summary>
    /// The video tracks, in ordinal order.
    /// </summary>
    [Required]
    public List<StreamTrackDescription> VideoTracks { get; init; } = [];

    /// <summary>
    /// The audio tracks, in ordinal order.
    /// </summary>
    [Required]
    public List<StreamTrackDescription> AudioTracks { get; init; } = [];

    /// <summary>
    /// The subtitle tracks, in ordinal order.
    /// </summary>
    [Required]
    public List<Subtitle> Subtitles { get; init; } = [];

    /// <summary>
    /// The attachments, e.g. fonts.
    /// </summary>
    [Required]
    public List<Attachment> Attachments { get; init; } = [];

    /// <summary>
    /// The chapters, in playback order.
    /// </summary>
    [Required]
    public List<StreamChapterDescription> Chapters { get; init; } = [];

    /// <summary>
    /// Any other resources the rendition serves.
    /// </summary>
    [Required]
    public List<Extra> Extras { get; init; } = [];

    /// <summary>
    /// Rendition-specific data, in a shape defined by the transform.
    /// </summary>
    public JObject? Metadata { get; init; }

    public StreamSessionDescription() { }

    public StreamSessionDescription(Guid id, string? transformID, StreamDeliveryMode deliveryMode, string streamUrl, StreamDescription? description, Func<string, string> resolveUrl)
    {
        ID = id;
        TransformID = transformID;
        DeliveryMode = deliveryMode;
        StreamUrl = streamUrl;
        if (description is null)
            return;

        VideoTracks = [.. description.VideoTracks];
        AudioTracks = [.. description.AudioTracks];
        Subtitles = description.Subtitles.Select(subtitle => new Subtitle(subtitle, resolveUrl)).ToList();
        Attachments = description.Attachments.Select(attachment => new Attachment(attachment, resolveUrl)).ToList();
        Chapters = [.. description.Chapters];
        Extras = description.Extras.Select(extra => new Extra(extra, resolveUrl)).ToList();
        Metadata = description.Metadata;
    }

    /// <summary>
    /// Resolves a resource path against the session's resource root, carrying the request's query string (e.g. <c>apikey</c>) onto it.
    /// </summary>
    public static string ResolveUrl(string resourceRoot, string path, string query)
    {
        var url = resourceRoot + path.TrimStart('/');
        if (query is not { Length: > 1 })
            return url;

        return url.Contains('?') ? url + "&" + query[1..] : url + query;
    }

    /// <summary>
    /// A subtitle track of a stream session.
    /// </summary>
    public class Subtitle(StreamSubtitleDescription subtitle, Func<string, string> resolveUrl)
    {
        /// <inheritdoc cref="StreamSubtitleDescription.Ordinal"/>
        [Required]
        public int Ordinal { get; init; } = subtitle.Ordinal;

        /// <inheritdoc cref="StreamSubtitleDescription.Format"/>
        [Required]
        public string Format { get; init; } = subtitle.Format;

        /// <summary>
        /// The URL of the subtitle track.
        /// </summary>
        [Required]
        public string Url { get; init; } = resolveUrl(subtitle.Path);

        /// <summary>
        /// The URLs of any further files the track needs, e.g. the <c>.sub</c> of a VobSub pair.
        /// </summary>
        [Required]
        public List<string> CompanionUrls { get; init; } = subtitle.CompanionPaths.Select(resolveUrl).ToList();

        /// <inheritdoc cref="StreamSubtitleDescription.Language"/>
        public string? Language { get; init; } = subtitle.Language;

        /// <inheritdoc cref="StreamSubtitleDescription.Title"/>
        public string? Title { get; init; } = subtitle.Title;

        /// <inheritdoc cref="StreamSubtitleDescription.IsDefault"/>
        [Required]
        public bool IsDefault { get; init; } = subtitle.IsDefault;

        /// <inheritdoc cref="StreamSubtitleDescription.IsForced"/>
        [Required]
        public bool IsForced { get; init; } = subtitle.IsForced;

        /// <inheritdoc cref="StreamSubtitleDescription.IsExternal"/>
        [Required]
        public bool IsExternal { get; init; } = subtitle.IsExternal;

        /// <inheritdoc cref="StreamSubtitleDescription.Rank"/>
        public int? Rank { get; init; } = subtitle.Rank;
    }

    /// <summary>
    /// An attachment of a stream session.
    /// </summary>
    public class Attachment(StreamAttachmentDescription attachment, Func<string, string> resolveUrl)
    {
        /// <inheritdoc cref="StreamAttachmentDescription.Index"/>
        [Required]
        public int Index { get; init; } = attachment.Index;

        /// <inheritdoc cref="StreamAttachmentDescription.FileName"/>
        [Required]
        public string FileName { get; init; } = attachment.FileName;

        /// <inheritdoc cref="StreamAttachmentDescription.ContentType"/>
        [Required]
        public string ContentType { get; init; } = attachment.ContentType;

        /// <summary>
        /// The URL of the attachment.
        /// </summary>
        [Required]
        public string Url { get; init; } = resolveUrl(attachment.Path);

        /// <inheritdoc cref="StreamAttachmentDescription.Size"/>
        public long? Size { get; init; } = attachment.Size;
    }

    /// <summary>
    /// Any other resource of a stream session.
    /// </summary>
    public class Extra(StreamExtraDescription extra, Func<string, string> resolveUrl)
    {
        /// <inheritdoc cref="StreamExtraDescription.Kind"/>
        [Required]
        public string Kind { get; init; } = extra.Kind;

        /// <inheritdoc cref="StreamExtraDescription.ContentType"/>
        [Required]
        public string ContentType { get; init; } = extra.ContentType;

        /// <summary>
        /// The URL of the resource.
        /// </summary>
        [Required]
        public string Url { get; init; } = resolveUrl(extra.Path);
    }
}
