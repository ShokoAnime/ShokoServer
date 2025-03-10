
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Shoko.Plugin.Abstractions.Config;
using Shoko.Plugin.Abstractions.Config.Attributes;
using Shoko.Plugin.Abstractions.Config.Enums;

namespace Shoko.Plugin.ConfigurationHell.Configurations;

/// <summary>
/// Practical example of the configuration for the Shokofin plugin for Jellyfin
/// as a Shoko IConfiguration implementation.
/// </summary>
[SectionType(DisplaySectionType.Tab)]
[HideDefaultSaveAction]
public class ShokofinConfiguration : IConfiguration
{
    #region Connection Settings

    /// <summary>
    /// Settings controlling the connection to Shoko from Jellyfin.
    /// </summary>
    public ConnectionSettings Connection { get; set; } = new();

    /// <summary>
    /// Settings controlling the connection to Shoko from Jellyfin.
    /// </summary>
    [CustomAction(
        "Connect",
        Description = "Establish a connection to Shoko using the provided credentials.",
        Theme = DisplayColorTheme.Primary,
        ToggleWhenMemberIsSet = nameof(ApiKey),
        ToggleWhenSetTo = null,
        HideByDefault = true
    )]
    [CustomAction(
        "Disconnect",
        Description = "Reset the connection. Be sure to stop any tasks using this plugin before you press the button.",
        Theme = DisplayColorTheme.Secondary,
        ToggleWhenMemberIsSet = nameof(ApiKey),
        ToggleWhenSetTo = null,
        HideByDefault = false
    )]
    public class ConnectionSettings
    {
        /// <summary>
        /// This is the private URL leading to where Shoko is running. It will
        /// be used internally in Jellyfin in addition to all images sent to
        /// clients and redirects back to Shoko if you don't set a public host
        /// URL below. It should include both the protocol and the IP/DNS name.
        /// </summary>
        [Required]
        [Url]
        [Display(Name = "Private Host URL")]
        [DefaultValue("http://localhost:8111")]
        [Visibility(
            DisplayVisibility.Disabled,
            ToggleWhenMemberIsSet = nameof(ApiKey),
            ToggleWhenSetTo = null,
            ToggleVisibilityTo = DisplayVisibility.Visible
        )]
        public string Host { get; set; } = "http://localhost:8111";

        /// <summary>
        /// Optional. This is the public URL leading to where Shoko is running.
        /// It can be used to redirect to Shoko if you click on a Shoko ID in
        /// the UI if Shoko and/or Jellyfin is running within a container and
        /// you cannot access Shoko from the host URL provided in the connection
        /// settings section above. It will also be used for images from the
        /// plugin when viewing the "Edit Images" modal in clients. It should
        /// include both the protocol and the IP/DNS name.
        /// </summary>
        [Url]
        [Display(Name = "Public Host URL")]
        [Visibility(
            DisplayVisibility.Disabled,
            ToggleWhenMemberIsSet = nameof(ApiKey),
            ToggleWhenSetTo = null,
            ToggleVisibilityTo = DisplayVisibility.Visible
        )]
        public string? PublicHost { get; set; }

        /// <summary>
        /// The username of your administrator account in Shoko.
        /// </summary>
        [Required]
        [DefaultValue("Default")]
        [Visibility(
            DisplayVisibility.Disabled,
            ToggleWhenMemberIsSet = nameof(ApiKey),
            ToggleWhenSetTo = null,
            ToggleVisibilityTo = DisplayVisibility.Visible
        )]
        public string Username { get; set; } = "Default";

        /// <summary>
        /// The password of your administrator account in Shoko.
        /// </summary>
        [PasswordPropertyText]
        [Visibility(
            DisplayVisibility.Hidden,
            ToggleWhenMemberIsSet = nameof(ApiKey),
            ToggleWhenSetTo = null,
            ToggleVisibilityTo = DisplayVisibility.Visible
        )]
        public string? Password { get; set; }

        /// <summary>
        /// The version of Shoko we're connected to.
        /// </summary>
        /// <example>1.0.0.0</example>
        [Display(Name = "Shoko Version")]
        [Visibility(
            DisplayVisibility.ReadOnly,
            ToggleWhenMemberIsSet = nameof(ApiKey),
            ToggleWhenSetTo = null,
            ToggleVisibilityTo = DisplayVisibility.Hidden
        )]
        public Version? ServerVersion { get; set; }

        /// <summary>
        /// API Key.
        /// </summary>
        [Visibility(DisplayVisibility.Hidden)]
        public string? ApiKey { get; set; }
    }

    #endregion

    #region Metadata

    /// <summary>
    /// Configure how the plugin handles metadata for Shoko managed entities.
    /// </summary>
    [Visibility(
        DisplayVisibility.Visible,
        ToggleWhenMemberIsSet = $"{nameof(Connection)}.{nameof(Connection.ApiKey)}",
        ToggleWhenSetTo = null,
        ToggleVisibilityTo = DisplayVisibility.Hidden
    )]
    public MetadataSettings Metadata { get; set; } = new();

    /// <summary>
    /// Settings for the metadata.
    /// </summary>
    [CustomAction("Save", Theme = DisplayColorTheme.Primary, DisableIfNoChanges = true)]
    public class MetadataSettings
    {
        #region Metadata | Title

        /// <summary>
        /// Settings related to title metadata.
        /// </summary>
        [Display(Name = "Title Settings")]
        public MetadataTitleSettings Title { get; set; } = new();

        /// <summary>
        /// Settings related to title metadata.
        /// </summary>
        public class MetadataTitleSettings
        {
            /// <summary>
            /// The metadata providers to use as the source of the main title for entities, in priority order.
            /// </summary>
            [Display(Name = "Main Title Source")]
            [DefaultValue(new TitleProvider[] { TitleProvider.Shoko_Default })]
            public TitleProvider[] MainTitleSource { get; set; } = [TitleProvider.Shoko_Default];

            /// <summary>
            /// The metadata providers to use as the source of the alternate/original title for entities, in priority order.
            /// </summary>
            [Display(Name = "Alternate/Original Title Source")]
            [DefaultValue(new TitleProvider[] { })]
            public TitleProvider[] AlternateTitleSource { get; set; } = [];

            /// <summary>
            /// Adds the type and number to the title of non-standard episodes such as specials. (e.g. S1)
            /// </summary>
            [Display(Name = "Add Prefix to Episodes")]
            [DefaultValue(true)]
            public bool AddPrefixToEpisodes { get; set; } = true;

            /// <summary>
            /// Allows for any titles to be utilized if an official title is not present in the given language. Only applies to the AniDB title selectors above.
            /// </summary>
            [Badge("Advanced", Theme = DisplayColorTheme.Important)]
            [Display(Name = "Allow Any Title")]
            public bool AllowAnyTitle { get; set; }

            /// <summary>
            /// Determines which provider and method to use to look-up the title.
            /// </summary>
            public enum TitleProvider
            {
                /// <summary>
                /// Let Shoko decide what to display.
                /// </summary>
                [Display(Name = "Shoko | Let Shoko Decide")]
                Shoko_Default = 1,

                /// <summary>
                /// Use the default title as provided by AniDB.
                /// </summary>
                [Display(Name = "AniDB | Default Title")]
                AniDB_Default = 2,

                /// <summary>
                /// Use the selected metadata language for the library as provided by
                /// AniDB.
                /// </summary>
                [Display(Name = "AniDB | Follow metadata language in library")]
                AniDB_LibraryLanguage = 3,

                /// <summary>
                /// Use the title in the origin language as provided by AniDB.
                /// </summary>
                [Display(Name = "AniDB | Use the language from the media's country of origin")]
                AniDB_CountryOfOrigin = 4,

                /// <summary>
                /// Use the default title as provided by TheMovieDb.
                /// </summary>
                [Display(Name = "TheMovieDb | Default Title")]
                TMDB_Default = 5,

                /// <summary>
                /// Use the selected metadata language for the library as provided by
                /// TheMovieDb.
                /// </summary>
                [Display(Name = "TheMovieDb | Follow metadata language in library")]
                TMDB_LibraryLanguage = 6,

                /// <summary>
                /// Use the title in the origin language as provided by TheMovieDb.
                /// </summary>
                [Display(Name = "TheMovieDb | Use the language from the media's country of origin")]
                TMDB_CountryOfOrigin = 7,
            }
        }

        #endregion

        #region Metadata | Description

        /// <summary>
        /// Settings related to description metadata.
        /// </summary>
        [Display(Name = "Description Settings")]
        public MetadataDescriptionSettings Description { get; set; } = new();

        /// <summary>
        /// Settings related to description metadata.
        /// </summary>
        public class MetadataDescriptionSettings
        {
            /// <summary>
            /// Prettifies AniDB descriptions.
            /// </summary>
            [Badge("Debug", Theme = DisplayColorTheme.Warning)]
            [Display(Name = "Cleanup AniDB Descriptions")]
            [DefaultValue(true)]
            public bool CleanupAnidbDescription { get; set; } = true;

            /// <summary>
            /// Prettifies AniDB descriptions and convert them to markdown.
            /// </summary>
            [Display(Name = "Convert AniDB Descriptions")]
            [Visibility(
                ToggleWhenMemberIsSet = nameof(CleanupAnidbDescription),
                ToggleWhenSetTo = false,
                ToggleVisibilityTo = DisplayVisibility.Disabled
            )]
            [DefaultValue(false)]
            public bool ConvertAnidbDescription { get; set; } = false;

            /// <summary>
            /// The metadata providers to use as the source of descriptions for entities, in priority order.
            /// </summary>
            [Display(Name = "Description Source")]
            [DefaultValue(new DescriptionProvider[] { DescriptionProvider.Shoko })]
            public DescriptionProvider[] DescriptionSource { get; set; } = [DescriptionProvider.Shoko];

            /// <summary>
            /// Determines which provider to use to provide the descriptions.
            /// </summary>
            public enum DescriptionProvider
            {
                /// <summary>
                /// Provide the Shoko Group description for the show, if the show is
                /// constructed using Shoko's groups feature.
                /// </summary>
                [Display(Name = "Shoko | Let Shoko Decide")]
                Shoko = 1,

                /// <summary>
                /// Provide the description from AniDB.
                /// </summary>
                [Display(Name = "AniDB | Follow metadata language in library")]
                AniDB = 2,

                /// <summary>
                /// Provide the description from TheMovieDb.
                /// </summary>
                [Display(Name = "TheMovieDb | Follow metadata language in library")]
                TheMovieDb = 3,
            }
        }

        #endregion
    }

    #endregion

    #region Library

    /// <summary>
    /// Configure how the plugin manages the library.
    /// </summary>
    [Visibility(
        DisplayVisibility.Visible,
        ToggleWhenMemberIsSet = $"{nameof(Connection)}.{nameof(Connection.ApiKey)}",
        ToggleWhenSetTo = null,
        ToggleVisibilityTo = DisplayVisibility.Hidden
    )]
    public LibrarySettings Library { get; set; } = new();

    /// <summary>
    /// Settings for the library.
    /// </summary>
    [CustomAction("Save", Theme = DisplayColorTheme.Primary, DisableIfNoChanges = true)]
    public class LibrarySettings
    {
        #region Library | Basic

        /// <summary>
        /// Settings related to the library.
        /// </summary>
        [Display(Name = "Basic Settings")]
        public LibraryBasicSettings Basic { get; set; } = new();

        /// <summary>
        /// Settings related to the library.
        /// </summary>
        public class LibraryBasicSettings
        {
            /// <summary>
            /// Determines how to structure the libraries.
            /// </summary>
            [Display(Name = "Library Structure Mode")]
            [DefaultValue(LibraryStructureType.AniDB_Anime)]
            public LibraryStructureType LibraryStructure { get; set; } = LibraryStructureType.AniDB_Anime;

            /// <summary>
            /// Determines how to order seasons within shows when using groups for shows.
            /// </summary>
            [Visibility(
                DisplayVisibility.Disabled,
                ToggleWhenMemberIsSet = nameof(LibraryStructure),
                ToggleWhenSetTo = LibraryStructureType.Shoko_Groups,
                ToggleVisibilityTo = DisplayVisibility.Visible
            )]
            [Badge("Experimental", Theme = DisplayColorTheme.Dangerous)]
            [DefaultValue(SeasonOrderingType.Default)]
            [Display(Name = "Shoko Group's Season Ordering")]
            public SeasonOrderingType SeasonOrdering { get; set; } = SeasonOrderingType.Default;

            /// <summary>
            /// Determines how specials are placed within seasons. Warning:
            /// Modifying this setting requires a recreation (read as; delete
            /// existing then create a new) of any libraries using this plugin —
            /// otherwise you will have mixed metadata.
            /// </summary>
            [Display(Name = "Specials Placement")]
            [DefaultValue(SpecialsPlacementType.AfterSeason)]
            public SpecialsPlacementType SpecialsPlacement { get; set; } = SpecialsPlacementType.AfterSeason;

            /// <summary>
            /// Library structure type to use for series.
            /// </summary>
            public enum LibraryStructureType
            {
                /// <summary>
                /// Structure the libraries as AniDB anime.
                /// </summary>
                [Display(Name = "AniDB Anime Structure")]
                AniDB_Anime,

                /// <summary>
                /// Structure the libraries using Shoko's group structure.
                /// </summary>
                [Display(Name = "Shoko Group Structure")]
                Shoko_Groups,

                /// <summary>
                /// Structure the libraries as TheMovieDb series and/or movies.
                /// </summary>
                [Display(Name = "TheMovieDb Series & Movies Structure")]
                TMDB_SeriesAndMovies,
            }

            /// <summary>
            /// Season ordering type.
            /// </summary>
            public enum SeasonOrderingType
            {
                /// <summary>
                /// Let Shoko decide the order.
                /// </summary>
                [Display(Name = "Let Shoko Decide")]
                Default = 0,

                /// <summary>
                /// Order seasons by release date.
                /// </summary>
                [Display(Name = "Release Date")]
                ReleaseDate = 1,

                /// <summary>
                /// Order seasons based on the chronological order of relations.
                /// </summary>
                [Display(Name = "Chronological (Use Indirect Relations)")]
                Chronological = 2,

                /// <summary>
                /// Order seasons based on the chronological order of only direct relations.
                /// </summary>
                [Display(Name = "Chronological (Ignore Indirect Relations)")]
                ChronologicalIgnoreIndirect = 3,
            }

            /// <summary>
            /// Specials placement type.
            /// </summary>
            public enum SpecialsPlacementType
            {
                /// <summary>
                /// Always place the specials after the normal episodes in the season.
                /// </summary>
                [Display(Name = "Always place specials after normal episodes")]
                AfterSeason = 0,

                /// <summary>
                /// Place the specials in-between normal episodes based on the time the episodes aired.
                /// </summary>
                [Display(Name = "Place specials in-between normal episodes based on the time the episodes aired")]
                InBetweenSeasonByAirDate = 1,

                /// <summary>
                /// Place the specials in-between normal episodes based upon the data from TheMovieDb.
                /// </summary>
                [Display(Name = "Loosely place specials in-between normal episodes based upon the data from TheMovieDb")]
                InBetweenSeasonByTMDB = 2,

                /// <summary>
                /// Use a mix of <see cref="InBetweenSeasonByTMDB" /> and <see cref="InBetweenSeasonByAirDate" />.
                /// </summary>
                [Display(Name = "Loosely place specials in-between normal episodes based upon the data from TheMovieDb or place specials in-between normal episodes based on the time the episodes aired")]
                InBetweenSeasonMixed = 3,

                /// <summary>
                /// Always exclude the specials from the season.
                /// </summary>
                [Display(Name = "Always exclude the specials from the season")]
                Excluded = 4,
            }
        }

        #endregion
    }

    #endregion

    #region VFS

    /// <summary>
    /// Settings related to the Virtual File System (VFS).
    /// </summary>
    [Visibility(
        DisplayVisibility.Visible,
        ToggleWhenMemberIsSet = $"{nameof(Connection)}.{nameof(Connection.ApiKey)}",
        ToggleWhenSetTo = null,
        ToggleVisibilityTo = DisplayVisibility.Hidden
    )]
    public VirtualFileSystemSettings VFS { get; set; } = new();

    /// <summary>
    /// Settings related to the Virtual File System (VFS).
    /// </summary>
    [CustomAction("Save", Theme = DisplayColorTheme.Primary, DisableIfNoChanges = true)]
    public class VirtualFileSystemSettings
    {
        /// <summary>
        /// Basic settings related to the Virtual File System (VFS).
        /// </summary>
        [Display(Name = "Basic Settings")]
        public BasicVirtualFileSystemSettings Basic { get; set; } = new();

        /// <summary>
        /// Basic settings related to the Virtual File System (VFS).
        /// </summary>
        public class BasicVirtualFileSystemSettings
        {
            /// <summary>
            /// Gets or sets a value indicating whether to add trailers to entities within the VFS.
            /// </summary>
            [Display(Name = "Add Trailers")]
            public bool AddTrailers { get; set; }

            /// <summary>
            /// Gets or sets a value indicating whether to add credits as theme videos to entities within the VFS.
            /// </summary>
            [Display(Name = "Add Credits as Theme Videos")]
            public bool AddCreditsAsThemeVideos { get; set; }

            /// <summary>
            /// Gets or sets a value indicating whether to add credits as special features to entities within the VFS.
            /// </summary>
            [Display(Name = "Add Credits as Special Features")]
            public bool AddCreditsAsSpecialFeatures { get; set; }

            /// <summary>
            /// Gets or sets a value indicating whether to add the full or short release group name to all automatically linked files in the VFS. "No Group" will be used for all manually linked files.
            /// </summary>
            [Display(Name = "Add Release Group to Path")]
            public bool AddReleaseGroup { get; set; }

            /// <summary>
            /// Gets or sets a value indicating whether to add the standardized resolution (e.g. 480p, 1080p, 4K, etc.) to all files in the VFS if available.
            /// </summary>
            [Display(Name = "Add Resolution to Path")]
            public bool AddResolution { get; set; }
        }

        /// <summary>
        /// Settings related to the location of the Virtual File System (VFS).
        /// </summary>
        [Display(Name = "VFS Location Settings")]
        public VirtualFileSystemLocationSettings VFS_Location { get; set; } = new();

        /// <summary>
        /// Settings related to the location of the Virtual File System (VFS).
        /// </summary>
        public class VirtualFileSystemLocationSettings
        {
            /// <summary>
            /// Gets or sets a value indicating whether to resolve links before the VFS.
            /// </summary>
            [DefaultValue(false)]
            [Badge("Debug", Theme = DisplayColorTheme.Warning)]
            [Display(Name = "Resolve Links Before VFS")]
            public bool ResolveLinksBeforeVFS { get; set; } = false;

            /// <summary>
            /// Gets or sets a value indicating whether to attach the VFS to libraries.
            /// </summary>
            [DefaultValue(true)]
            [Badge("Debug", Theme = DisplayColorTheme.Warning)]
            [Display(Name = "Attach VFS to Libraries")]
            public bool AttachToLibraries { get; set; } = true;

            /// <summary>
            /// Gets or sets a value indicating whether to perform iterative file checks.
            /// </summary>
            [DefaultValue(false)]
            [Badge("Debug", Theme = DisplayColorTheme.Warning)]
            [Display(Name = "Iterative File Checks")]
            public bool PerformIterativeFileChecks { get; set; } = false;

            /// <summary>
            /// Change where the VFS structure will be placed. Changing this
            /// setting will cause your library to "remove" and "re-add" itself
            /// because of the path changes. You will need to manually move your
            /// VFS root if you plan to keep it when toggling this setting.
            /// Trick-play files will need to be backed-up beforehand and moved
            /// back the next library scan if you want to avoid regenerating
            /// them after you change this setting.
            /// <br/>
            /// <br/>
            /// <strong>You have been warned.</strong>
            /// </summary>
            [DefaultValue(VirtualFileSystemLocation.Default)]
            [Badge("Debug", Theme = DisplayColorTheme.Warning)]
            [Display(Name = "VFS Location")]
            public VirtualFileSystemLocation VFS_Location { get; set; } = VirtualFileSystemLocation.Default;

            /// <summary>
            /// An absolute path, or a relative path from the Jellyfin Data
            /// Directory, to the custom root directory of where the VFS will
            /// be placed. You decide.
            /// </summary>
            [Badge("Debug", Theme = DisplayColorTheme.Warning)]
            [Visibility(
                DisplayVisibility.Hidden,
                ToggleWhenMemberIsSet = nameof(VFS_Location),
                ToggleWhenSetTo = nameof(VirtualFileSystemLocation.Custom),
                ToggleVisibilityTo = DisplayVisibility.Visible
            )]
            [Display(Name = "Custom VFS Root Location")]
            public string? CustomLocation { get; set; }
        }

        /// <summary>
        /// Where to place the Virtual File System (VFS).
        /// </summary>
        public enum VirtualFileSystemLocation
        {
            /// <summary>
            /// The default location for the VFS, which is in the Jellyfin data directory.
            /// </summary>
            [Display(Name = "Jellyfin Data Directory")]
            Default = 0,

            /// <summary>
            /// A custom location for the VFS.
            /// </summary>
            [Display(Name = "Custom Directory")]
            Custom = 1,
        }
    }

    #endregion

    #region User

    /// <summary>
    /// User settings.
    /// </summary>
    [Visibility(
        DisplayVisibility.Visible,
        ToggleWhenMemberIsSet = $"{nameof(Connection)}.{nameof(Connection.ApiKey)}",
        ToggleWhenSetTo = null,
        ToggleVisibilityTo = DisplayVisibility.Hidden
    )]
    public List<UserSettings> Users { get; set; } = [];

    /// <summary>
    /// User settings.
    /// </summary>
    [CustomAction(
        "Remove",
        Theme = DisplayColorTheme.Dangerous,
        ToggleWhenMemberIsSet = nameof(Token),
        ToggleWhenSetTo = null
    )]
    [CustomAction("Save", Theme = DisplayColorTheme.Primary, DisableIfNoChanges = true)]
    public class UserSettings
    {
        /// <summary>
        /// The displayed key in the UI for the user.
        /// </summary>
        [Key]
        [Required]
        [JsonInclude]
        [Visibility(DisplayVisibility.Hidden)]
        public string Key => $"{Username} ({UserId})";

        /// <summary>
        /// The Jellyfin user id this configuration is for.
        /// </summary>
        [Required]
        [Visibility(DisplayVisibility.Hidden)]
        public Guid UserId { get; set; } = Guid.Empty;

        /// <summary>
        /// Enables watch-state synchronization for the user.
        /// </summary>
        public bool EnableSynchronization { get; set; }

        /// <summary>
        /// Enable syncing user data when an item have been added/updated.
        /// </summary>
        public bool SyncUserDataOnImport { get; set; }

        /// <summary>
        /// Enable the stop event for syncing after video playback.
        /// </summary>
        public bool SyncUserDataAfterPlayback { get; set; }

        /// <summary>
        /// Enable the play/pause/resume(/stop) events for syncing under/during
        /// video playback.
        /// </summary>
        public bool SyncUserDataUnderPlayback { get; set; }

        /// <summary>
        /// Enable the scrobble event for live syncing under/during video
        /// playback.
        /// </summary>
        public bool SyncUserDataUnderPlaybackLive { get; set; }

        /// <summary>
        /// Enabling user data sync. for restricted videos (H).
        /// </summary>
        public bool SyncRestrictedVideos { get; set; }
        /// <summary>
        /// The username of your administrator account in Shoko.
        /// </summary>
        [Required]
        [DefaultValue("Default")]
        [Visibility(
            DisplayVisibility.Disabled,
            ToggleWhenMemberIsSet = nameof(Token),
            ToggleWhenSetTo = null,
            ToggleVisibilityTo = DisplayVisibility.Visible
        )]
        public string Username { get; set; } = "Default";

        /// <summary>
        /// The password of your administrator account in Shoko.
        /// </summary>
        [PasswordPropertyText]
        [Visibility(
            DisplayVisibility.Hidden,
            ToggleWhenMemberIsSet = nameof(Token),
            ToggleWhenSetTo = null,
            ToggleVisibilityTo = DisplayVisibility.Visible
        )]
        public string? Password { get; set; }

        /// <summary>
        /// User Token.
        /// </summary>
        [Visibility(DisplayVisibility.Hidden)]
        public string? Token { get; set; }
    }

    #endregion

    #region SignalR

    /// <summary>
    /// Settings for the SignalR connection to Shoko.
    /// </summary>
    [Visibility(
        DisplayVisibility.Visible,
        ToggleWhenMemberIsSet = $"{nameof(Connection)}.{nameof(Connection.ApiKey)}",
        ToggleWhenSetTo = null,
        ToggleVisibilityTo = DisplayVisibility.Hidden
    )]
    public SignalRSettings SignalR { get; set; } = new();

    /// <summary>
    /// Settings for the SignalR connection to Shoko.
    /// </summary>
    [CustomAction("Save", Theme = DisplayColorTheme.Primary, DisableIfNoChanges = true)]
    public class SignalRSettings
    {
        /// <summary>
        /// Information about the SignalR connection to Shoko.
        /// </summary>
        [Display(Name = "Connection Status")]
        public SignalRConnectionInfo Connection { get; set; } = new();

        /// <summary>
        /// SignalR connection information.
        /// </summary>
        [CustomAction(
            "Connect",
            Description = "Establish a SignalR connection to Shoko.",
            Theme = DisplayColorTheme.Primary,
            ToggleWhenMemberIsSet = nameof(Enabled),
            ToggleWhenSetTo = true
        )]
        [CustomAction(
            "Disconnect",
            Description = "Terminate the SignalR connection to Shoko.",
            Theme = DisplayColorTheme.Secondary,
            ToggleWhenMemberIsSet = nameof(Enabled),
            ToggleWhenSetTo = false
        )]
        public class SignalRConnectionInfo
        {
            /// <summary>
            /// Current connection status.
            /// </summary>
            [Display(Name = "Connection Status")]
            [DefaultValue("Disabled")]
            [Visibility(DisplayVisibility.ReadOnly)]
            public string Status { get; set; } = "Disabled";

            /// <summary>
            /// 
            /// </summary>
            [Visibility(DisplayVisibility.Hidden)]
            public bool Enabled { get; set; } = false;
        }
    }


    #endregion
}
