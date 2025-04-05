using System;
using System.Collections.Generic;

namespace Shoko.Server.Providers.AniDB.UDP.Info;

public class ResponseGetFile
{
    /// AniDB File ID
    public int FileID { get; set; }

    /// AniDB Anime ID
    public int AnimeID { get; set; }

    /// AniDB Release Group ID, if available
    public int? GroupID { get; set; }

    public string GroupName { get; set; }

    public string GroupShortName { get; set; }

    // AniDB Episode IDs for episodes that this file may link to. The eid is listed here as 100%
    public List<EpisodeXRef> EpisodeIDs { get; set; }

    public List<EpisodeXRef> OtherEpisodes { get; set; }

    // Is the file deprecated/replaced
    public bool Deprecated { get; set; }

    // the version, will be higher than 1 if it's replacing a deprecated file
    public int Version { get; set; }

    // Mostly for AniDB's use. Does the CRC in the filename match the actual CRC
    public bool? CRCMatches { get; set; }

    // Chaptered. This is likely to be wrong, as it is undocumented
    public bool Chaptered { get; set; }

    // trinary state for Censoring. Null means no data is marked.
    public bool? Censored { get; set; }

    // quality. Usually high or very high for new releases. for old stuff, it can vary
    public GetFile_Quality Quality { get; set; }

    // source. Where this release came from, ie TV, Webrip, etc
    public GetFile_Source Source { get; set; }

    // Audio Languages
    public List<string> AudioLanguages { get; set; }

    // Subtitle Languages
    public List<string> SubtitleLanguages { get; set; }

    // Description of the file. Usually blank
    public string Description { get; set; }

    // Filename as reported in AVDump
    public string Filename { get; set; }

    public DateOnly? ReleasedAt { get; set; }

    public class EpisodeXRef
    {
        // AniDB Episode ID
        public int EpisodeID { get; set; }

        // Percentage of the Episode it represents. May not be exact, and has no info of where in the episode it lies
        public byte Percentage { get; set; }
    }
}
