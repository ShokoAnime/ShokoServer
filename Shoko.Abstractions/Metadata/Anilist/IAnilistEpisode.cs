using System;
using Shoko.Abstractions.Metadata.Containers;

namespace Shoko.Abstractions.Metadata.Anilist;

/// <summary>
/// An AniList episode.
/// </summary>
public interface IAnilistEpisode : IEpisode, IWithCreationDate, IWithUpdateDate { }
