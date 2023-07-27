﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using Shoko.Commons.Extensions;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Server.Extensions;
using Shoko.Server.ImageDownload;
using Shoko.Server.Models;
using Shoko.Server.Providers.AniDB.HTTP.GetAnime;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Settings;

namespace Shoko.Server.Providers.AniDB.HTTP;

public class AnimeCreator
{
    private readonly ILogger<AnimeCreator> _logger;
    private readonly ISettingsProvider _settingsProvider;

    public AnimeCreator(ILogger<AnimeCreator> logger, ISettingsProvider settings)
    {
        _logger = logger;
        _settingsProvider = settings;
    }


    public void CreateAnime(ResponseGetAnime response, SVR_AniDB_Anime anime, int relDepth)
    {
        var settings = _settingsProvider.GetSettings();
        _logger.LogTrace("------------------------------------------------");
        _logger.LogTrace(
            "PopulateAndSaveFromHTTP: for {AnimeID} - {MainTitle} @ Depth: {RelationDepth}/{MaxRelationDepth}",
            response.Anime.AnimeID, response.Anime.MainTitle, relDepth,
            settings.AniDb.MaxRelationDepth
        );
        _logger.LogTrace("------------------------------------------------");

        var taskTimer = Stopwatch.StartNew();
        var totalTimer = Stopwatch.StartNew();

        if (!PopulateAnime(response.Anime, anime))
        {
            _logger.LogError("AniDB_Anime was unable to populate as it received invalid info. " +
                             "This is not an error on our end. It is AniDB's issue, " +
                             "as they did not return either an ID or a title for the anime");
            totalTimer.Stop();
            taskTimer.Stop();
            return;
        }

        // save now for FK purposes
        RepoFactory.AniDB_Anime.Save(anime, false);

        taskTimer.Stop();
        _logger.LogTrace("PopulateAnime in: {Time}", taskTimer.Elapsed);
        taskTimer.Restart();

        CreateEpisodes(response.Episodes, anime);
        taskTimer.Stop();
        _logger.LogTrace("CreateEpisodes in: {Time}", taskTimer.Elapsed);
        taskTimer.Restart();

        CreateTitles(response.Titles, anime);
        taskTimer.Stop();
        _logger.LogTrace("CreateTitles in: {Time}", taskTimer.Elapsed);
        taskTimer.Restart();

        CreateTags(response.Tags, anime);
        taskTimer.Stop();
        _logger.LogTrace("CreateTags in: {Time}", taskTimer.Elapsed);
        taskTimer.Restart();

        CreateCharacters(response.Characters, anime);
        taskTimer.Stop();
        _logger.LogTrace("CreateCharacters in: {Time}", taskTimer.Elapsed);
        taskTimer.Restart();

        CreateStaff(response.Staff, anime);
        taskTimer.Stop();
        _logger.LogTrace("CreateStaff in: {Time}", taskTimer.Elapsed);
        taskTimer.Restart();

        CreateResources(response.Resources, anime);
        taskTimer.Stop();
        _logger.LogTrace("CreateResources in: {Time}", taskTimer.Elapsed);
        taskTimer.Restart();

        CreateRelations(response.Relations);
        taskTimer.Stop();
        _logger.LogTrace("CreateRelations in: {Time}", taskTimer.Elapsed);
        taskTimer.Restart();

        CreateSimilarAnime(response.Similar);
        taskTimer.Stop();
        _logger.LogTrace("CreateSimilarAnime in: {Time}", taskTimer.Elapsed);
        taskTimer.Restart();

        RepoFactory.AniDB_Anime.Save(anime);
        totalTimer.Stop();
        _logger.LogTrace("TOTAL TIME in : {Time}", totalTimer.Elapsed);
        _logger.LogTrace("------------------------------------------------");
    }

    private static bool PopulateAnime(ResponseAnime animeInfo, SVR_AniDB_Anime anime)
    {
        // We need various values to be populated to be considered valid
        if (string.IsNullOrEmpty(animeInfo?.MainTitle) || animeInfo.AnimeID <= 0)
        {
            return false;
        }

        anime.AirDate = animeInfo.AirDate;
        anime.AllCinemaID = animeInfo.AllCinemaID;
        anime.AnimeID = animeInfo.AnimeID;
        anime.AnimeType = (int)animeInfo.AnimeType;
        anime.ANNID = animeInfo.ANNID;
        anime.AvgReviewRating = animeInfo.AvgReviewRating;
        anime.BeginYear = animeInfo.BeginYear;

        anime.DateTimeDescUpdated = DateTime.Now;
        anime.DateTimeUpdated = DateTime.Now;

        anime.Description = animeInfo.Description ?? string.Empty;
        anime.EndDate = animeInfo.EndDate;
        anime.EndYear = animeInfo.EndYear;
        anime.MainTitle = animeInfo.MainTitle;
        anime.AllTitles = string.Empty;
        anime.AllTags = string.Empty;
        anime.EpisodeCount = animeInfo.EpisodeCount;
        anime.EpisodeCountNormal = animeInfo.EpisodeCountNormal;
        anime.EpisodeCountSpecial = animeInfo.EpisodeCount - animeInfo.EpisodeCountNormal;
        anime.ImageEnabled = 1;
        anime.Picname = animeInfo.Picname;
        anime.Rating = animeInfo.Rating;
        anime.Restricted = animeInfo.Restricted;
        anime.ReviewCount = animeInfo.ReviewCount;
        anime.TempRating = animeInfo.TempRating;
        anime.TempVoteCount = animeInfo.TempVoteCount;
        anime.URL = animeInfo.URL;
        anime.VoteCount = animeInfo.VoteCount;
        return true;
    }

    private void CreateEpisodes(List<ResponseEpisode> rawEpisodeList, SVR_AniDB_Anime anime)
    {
        if (rawEpisodeList == null)
            return;

        var episodeCountSpecial = 0;
        var episodeCountNormal = 0;
        var epIDs = rawEpisodeList
            .Select(e => e.EpisodeID)
            .ToHashSet();
        var epsBelongingToThisAnime = RepoFactory.AniDB_Episode.GetByAnimeID(anime.AnimeID)
            .ToDictionary(e => e.EpisodeID);
        var epsBelongingToOtherAnime = epIDs
            .Where(id => !epsBelongingToThisAnime.ContainsKey(id))
            .Select(id => RepoFactory.AniDB_Episode.GetByEpisodeID(id))
            .Where(episode => episode != null)
            .ToList();
        var currentAniDBEpisodes = epsBelongingToThisAnime.Values
            .Concat(epsBelongingToOtherAnime)
            .ToDictionary(a => a.EpisodeID);
        var currentAniDBEpisodeTitles = currentAniDBEpisodes.Keys
            .ToDictionary(id => id, id => RepoFactory.AniDB_Episode_Title.GetByEpisodeID(id).ToHashSet());
        var epsToRemove = currentAniDBEpisodes.Values
            .Where(a => !epIDs.Contains(a.EpisodeID))
            .ToList();
        var epsToSave = new List<AniDB_Episode>();
        var titlesToRemove = new List<SVR_AniDB_Episode_Title>();
        var titlesToSave = new List<SVR_AniDB_Episode_Title>();

        foreach (var rawEpisode in rawEpisodeList)
        {
            // Load the titles for the episode now, since we might need to check
            // them even if we don't update the episode itself.
            if (!currentAniDBEpisodeTitles.TryGetValue(rawEpisode.EpisodeID, out var currentTitles))
                currentTitles = new();

            // Check if the existing record, if any, needs to be updated.
            if (currentAniDBEpisodes.TryGetValue(rawEpisode.EpisodeID, out var episode))
            {
                // The data we have stored is either in sync (or newer) than
                // the raw episode data, so skip updating the episode.
                if (episode.DateTimeUpdated >= rawEpisode.LastUpdated)
                {
                    // If the episode does not belong to the anime being
                    // processed, or if none of the titles changed since last
                    // time, then also skip updating the episode titles.
                    if (episode.AnimeID != rawEpisode.AnimeID)
                        continue;
                    if (currentTitles.Count == rawEpisode.Titles.Count &&
                        currentTitles.All(t1 => rawEpisode.Titles.Any(t2 => string.Equals(t1.Title, t2.Title))))
                        goto count;
                }
                // Update the existing record.
                else
                {
                    episode.AirDate = Commons.Utils.AniDB.GetAniDBDateAsSeconds(rawEpisode.AirDate);
                    episode.AnimeID = anime.AnimeID;
                    episode.DateTimeUpdated = rawEpisode.LastUpdated;
                    // episode.EpisodeID = rawEpisode.EpisodeID;
                    episode.EpisodeNumber = rawEpisode.EpisodeNumber;
                    episode.EpisodeType = (int)rawEpisode.EpisodeType;
                    episode.LengthSeconds = rawEpisode.LengthSeconds;
                    episode.Rating = rawEpisode.Rating.ToString(CultureInfo.InvariantCulture);
                    episode.Votes = rawEpisode.Votes.ToString(CultureInfo.InvariantCulture);
                    episode.Description = rawEpisode.Description ?? string.Empty;
                    epsToSave.Add(episode);
                }
            }
            // Create a new record.
            else
            {
                episode = new()
                {
                    AirDate = Commons.Utils.AniDB.GetAniDBDateAsSeconds(rawEpisode.AirDate),
                    AnimeID = rawEpisode.AnimeID,
                    DateTimeUpdated = rawEpisode.LastUpdated,
                    EpisodeID = rawEpisode.EpisodeID,
                    EpisodeNumber = rawEpisode.EpisodeNumber,
                    EpisodeType = (int)rawEpisode.EpisodeType,
                    LengthSeconds = rawEpisode.LengthSeconds,
                    Rating = rawEpisode.Rating.ToString(CultureInfo.InvariantCulture),
                    Votes = rawEpisode.Votes.ToString(CultureInfo.InvariantCulture),
                    Description = rawEpisode.Description ?? string.Empty
                };
                epsToSave.Add(episode);
            }

            // Convert the raw titles to their equivalent database model.
            var newTitles = rawEpisode.Titles
                .Select(rawtitle => new SVR_AniDB_Episode_Title
                {
                    AniDB_EpisodeID = rawEpisode.EpisodeID,
                    Language = rawtitle.Language,
                    Title = rawtitle.Title,
                })
                .ToList();

            // Mark the new titles to-be saved.
            titlesToSave.AddRange(newTitles.Where(a => !currentTitles.Contains(a)));

            // Remove outdated titles.
            if (currentTitles.Count > 0)
                titlesToRemove.AddRange(currentTitles.Where(a => !newTitles.Any(b => b.Equals(a))));

            // Since the HTTP API doesn't return a count of the number of normal
            // episodes and/or specials, then we will calculate it now.
            count: switch (episode.GetEpisodeTypeEnum())
            {
                case Shoko.Models.Enums.EpisodeType.Episode:
                    episodeCountNormal++;
                    break;

                case Shoko.Models.Enums.EpisodeType.Special:
                    episodeCountSpecial++;
                    break;
            }
        }

        if (epsToRemove.Count > 0)
        {
            _logger.LogTrace("Deleting the following episodes (no longer in AniDB)");
            foreach (var ep in epsToRemove)
            {
                _logger.LogTrace("AniDB Ep: {EpisodeID} Type: {EpisodeType} Number: {EpisodeNumber}", ep.EpisodeID,
                    ep.EpisodeType, ep.EpisodeNumber);
            }
        }

        RepoFactory.AniDB_Episode.Delete(epsToRemove);
        RepoFactory.AniDB_Episode.Save(epsToSave);
        RepoFactory.AniDB_Episode_Title.Delete(titlesToRemove);
        RepoFactory.AniDB_Episode_Title.Save(titlesToSave);

        var episodeCount = episodeCountSpecial + episodeCountNormal;
        anime.EpisodeCountNormal = episodeCountNormal;
        anime.EpisodeCountSpecial = episodeCountSpecial;
        anime.EpisodeCount = episodeCount;
    }

    private void CreateTitles(List<ResponseTitle> titles, SVR_AniDB_Anime anime)
    {
        if (titles == null)
        {
            return;
        }

        var allTitles = string.Empty;

        var titlesToDelete = RepoFactory.AniDB_Anime_Title.GetByAnimeID(anime.AnimeID);
        var titlesToSave = new List<SVR_AniDB_Anime_Title>();
        foreach (var rawtitle in titles)
        {
            if (string.IsNullOrEmpty(rawtitle?.Title))
            {
                continue;
            }

            var title = new SVR_AniDB_Anime_Title()
            {
                AnimeID = anime.AnimeID,
                Language = rawtitle.Language,
                Title = rawtitle.Title,
                TitleType = rawtitle.TitleType
            };
            titlesToSave.Add(title);

            if (allTitles.Length > 0)
            {
                allTitles += "|";
            }

            allTitles += rawtitle.Title;
        }

        anime.AllTitles = allTitles;
        RepoFactory.AniDB_Anime_Title.Delete(titlesToDelete);
        RepoFactory.AniDB_Anime_Title.Save(titlesToSave);
    }

    /// <summary>
    /// A dictionary containing the name overrides for tags whose name either
    /// doesn't makes much sense or is otherwise confusing.
    /// </summary>
    /// <remarks>
    /// We use the tag name since the id _can_ change sometimes.
    /// </remarks>
    internal static Dictionary<string, string> TagNameOverrideDict = new()
    {
        {"new", "original work"},
        {"original work", "source material"},
    };

    private AniDB_Tag FindOrCreateTag(ResponseTag rawTag)
    {
        var tag = RepoFactory.AniDB_Tag.GetByTagID(rawTag.TagID);

        // We're trying to add older details to an existing tag,
        // so skip updating the tag but still create the cross-reference.
        if (tag != null && tag.LastUpdated != DateTime.UnixEpoch && tag.LastUpdated >= rawTag.LastUpdated)
            return tag;

        if (tag == null)
        {
            // There are situations in which an ID may have changed, this is
            // usually due to it being moved, but may be for other reasons.
            var existingTags = RepoFactory.AniDB_Tag.GetBySourceName(rawTag.TagName);
            var lastUpdatedTag = existingTags
                .OrderByDescending(existingTag => existingTag.LastUpdated)
                .FirstOrDefault();

            // One (or more, but idc) of the existing tags are more recently
            // updated than the tag we're trying to create, so skip creating
            // the tag and instead use more recent tag.
            if (lastUpdatedTag != null && lastUpdatedTag.LastUpdated >= rawTag.LastUpdated)
                return lastUpdatedTag;

            var xrefsToRemap = existingTags
                .SelectMany(t => RepoFactory.AniDB_Anime_Tag.GetByTagID(t.TagID))
                .ToList();
            foreach (var xref in xrefsToRemap)
            {
                xref.TagID = rawTag.TagID;
                RepoFactory.AniDB_Anime_Tag.Save(xref);
            }

            // Delete the obsolete tag(s).
            RepoFactory.AniDB_Tag.Delete(existingTags);

            // While we're at it, clean up other unreferenced tags.
            RepoFactory.AniDB_Tag.Delete(RepoFactory.AniDB_Tag.GetAll()
                .Where(a => !RepoFactory.AniDB_Anime_Tag.GetByTagID(a.TagID).Any()).ToList());

            // Also clean up dead cross-references. They shouldn't exist,
            // but they sometime does for whatever reason. ¯\_(ツ)_/¯
            var orphanedXRefs = RepoFactory.AniDB_Anime_Tag.GetAll().Where(a =>
                RepoFactory.AniDB_Tag.GetByTagID(a.TagID) == null ||
                RepoFactory.AniDB_Anime.GetByAnimeID(a.AnimeID) == null).ToList();

            RepoFactory.AniDB_Anime_Tag.Delete(orphanedXRefs);

            tag = new AniDB_Tag();
        }

        TagNameOverrideDict.TryGetValue(rawTag.TagName, out var nameOverride);
        tag.TagID = rawTag.TagID;
        tag.ParentTagID = rawTag.ParentTagID;
        tag.TagNameSource = rawTag.TagName;
        tag.TagNameOverride = nameOverride;
        tag.TagDescription = rawTag.TagDescription ?? string.Empty;
        tag.GlobalSpoiler = rawTag.GlobalSpoiler;
        tag.Verified = rawTag.Verified;
        tag.LastUpdated = rawTag.LastUpdated;

        return tag;
    }

    public void CreateTags(List<ResponseTag> tags, SVR_AniDB_Anime anime)
    {
        if (tags == null)
        {
            return;
        }

        var allTags = string.Empty;

        var tagsToSave = new List<AniDB_Tag>();
        var xrefsToSave = new List<AniDB_Anime_Tag>();

        // find all the current links, and then later remove the ones that are no longer relevant
        var currentTags = RepoFactory.AniDB_Anime_Tag.GetByAnimeID(anime.AnimeID);
        var newTagIDs = new HashSet<int>();

        foreach (var rawtag in tags)
        {
            if (rawtag.TagID <= 0 || string.IsNullOrEmpty(rawtag.TagName))
                continue;

            var tag = FindOrCreateTag(rawtag);
            tagsToSave.Add(tag);

            newTagIDs.Add(tag.TagID);

            var xref = RepoFactory.AniDB_Anime_Tag.GetByAnimeIDAndTagID(rawtag.AnimeID, tag.TagID) ?? new();
            xref.AnimeID = rawtag.AnimeID;
            xref.TagID = tag.TagID;
            xref.LocalSpoiler = rawtag.LocalSpoiler;
            xref.Weight = rawtag.Weight;
            xrefsToSave.Add(xref);

            // Only add it to the cached array if the tag is verified. This
            // ensures the v1 and v2 api is only displaying verified tags.
            if (tag.Verified)
            {
                if (allTags.Length > 0)
                {
                    allTags += "|";
                }

                allTags += tag.TagName;
            }
        }

        anime.AllTags = allTags;

        var xrefsToDelete = currentTags.Where(curTag => !newTagIDs.Contains(curTag.TagID)).ToList();
        RepoFactory.AniDB_Tag.Save(tagsToSave);
        RepoFactory.AniDB_Anime_Tag.Save(xrefsToSave);
        RepoFactory.AniDB_Anime_Tag.Delete(xrefsToDelete);
    }

    private void CreateCharacters(List<ResponseCharacter> chars, SVR_AniDB_Anime anime)
    {
        if (chars == null) return;

        // delete all the existing cross references just in case one has been removed
        var animeChars =
            RepoFactory.AniDB_Anime_Character.GetByAnimeID(anime.AnimeID);

        try
        {
            RepoFactory.AniDB_Anime_Character.Delete(animeChars);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to Remove Characters for {MainTitle}", anime.MainTitle);
        }


        var chrsToSave = new List<AniDB_Character>();
        var xrefsToSave = new List<AniDB_Anime_Character>();

        var seiyuuToSave = new Dictionary<int, AniDB_Seiyuu>();
        var seiyuuXrefToSave = new List<AniDB_Character_Seiyuu>();

        // delete existing relationships to seiyuu's
        var charSeiyuusToDelete =
            chars.SelectMany(rawchar => RepoFactory.AniDB_Character_Seiyuu.GetByCharID(rawchar.CharacterID))
                .ToList();
        try
        {
            RepoFactory.AniDB_Character_Seiyuu.Delete(charSeiyuusToDelete);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to Remove Seiyuus for {MainTitle}", anime.MainTitle);
        }

        var charBasePath = ImageUtils.GetBaseAniDBCharacterImagesPath() + Path.DirectorySeparatorChar;
        var creatorBasePath = ImageUtils.GetBaseAniDBCreatorImagesPath() + Path.DirectorySeparatorChar;
        foreach (var rawchar in chars)
        {
            try
            {
                var chr = RepoFactory.AniDB_Character.GetByCharID(rawchar.CharacterID) ??
                          new AniDB_Character();

                if (chr.CharID != 0)
                {
                    // only update the fields that come from HTTP API
                    if (string.IsNullOrEmpty(rawchar?.CharacterName))
                    {
                        continue;
                    }

                    chr.CharDescription = rawchar.CharacterDescription ?? string.Empty;
                    chr.CharName = rawchar.CharacterName;
                    chr.PicName = rawchar.PicName ?? string.Empty;
                }
                else
                {
                    if (rawchar == null)
                    {
                        continue;
                    }

                    if (rawchar.CharacterID <= 0 || string.IsNullOrEmpty(rawchar.CharacterName))
                    {
                        continue;
                    }

                    chr.CharID = rawchar.CharacterID;
                    chr.CharDescription = rawchar.CharacterDescription ?? string.Empty;
                    chr.CharKanjiName = rawchar.CharacterKanjiName ?? string.Empty;
                    chr.CharName = rawchar.CharacterName;
                    chr.PicName = rawchar.PicName ?? string.Empty;
                }

                chrsToSave.Add(chr);

                var character = RepoFactory.AnimeCharacter.GetByAniDBID(chr.CharID);
                if (character == null)
                {
                    character = new AnimeCharacter
                    {
                        AniDBID = chr.CharID,
                        Name = chr.CharName,
                        AlternateName = rawchar.CharacterKanjiName,
                        Description = chr.CharDescription,
                        ImagePath = chr.GetPosterPath()?.Replace(charBasePath, "")
                    };
                    // we need an ID for xref
                    RepoFactory.AnimeCharacter.Save(character);
                }

                // create cross ref's between anime and character, but don't actually download anything
                var animeChar = new AniDB_Anime_Character();
                if (rawchar.AnimeID <= 0 || rawchar.CharacterID <= 0 || string.IsNullOrEmpty(rawchar.CharacterType))
                {
                    continue;
                }

                animeChar.CharID = rawchar.CharacterID;
                animeChar.AnimeID = rawchar.AnimeID;
                animeChar.CharType = rawchar.CharacterType;
                xrefsToSave.Add(animeChar);

                foreach (var rawSeiyuu in rawchar.Seiyuus)
                {
                    try
                    {
                        // save the link between character and seiyuu
                        var acc = RepoFactory.AniDB_Character_Seiyuu.GetByCharIDAndSeiyuuID(rawchar.CharacterID, rawSeiyuu.SeiyuuID);
                        if (acc == null)
                        {
                            acc = new AniDB_Character_Seiyuu { CharID = chr.CharID, SeiyuuID = rawSeiyuu.SeiyuuID };
                            seiyuuXrefToSave.Add(acc);
                        }

                        // save the seiyuu
                        var seiyuu = RepoFactory.AniDB_Seiyuu.GetBySeiyuuID(rawSeiyuu.SeiyuuID);
                        if (seiyuu == null)
                        {
                            seiyuu = new AniDB_Seiyuu();
                        }

                        seiyuu.PicName = rawSeiyuu.PicName;
                        seiyuu.SeiyuuID = rawSeiyuu.SeiyuuID;
                        seiyuu.SeiyuuName = rawSeiyuu.SeiyuuName;
                        seiyuuToSave[seiyuu.SeiyuuID] = seiyuu;

                        var staff = RepoFactory.AnimeStaff.GetByAniDBID(seiyuu.SeiyuuID);
                        if (staff == null)
                        {
                            staff = new AnimeStaff
                            {
                                // Unfortunately, most of the info is not provided
                                AniDBID = seiyuu.SeiyuuID,
                                Name = rawSeiyuu.SeiyuuName,
                                ImagePath = seiyuu.GetPosterPath()?.Replace(creatorBasePath, "")
                            };
                            // we need an ID for xref
                            RepoFactory.AnimeStaff.Save(staff);
                        }

                        var xrefAnimeStaff = RepoFactory.CrossRef_Anime_Staff.GetByParts(anime.AnimeID,
                            character.CharacterID,
                            staff.StaffID, StaffRoleType.Seiyuu);
                        if (xrefAnimeStaff != null)
                        {
                            continue;
                        }

                        var role = rawchar.CharacterType;
                        if (CrossRef_Anime_StaffRepository.Roles.ContainsKey(role))
                        {
                            role = CrossRef_Anime_StaffRepository.Roles[role].ToString().Replace("_", " ");
                        }

                        xrefAnimeStaff = new CrossRef_Anime_Staff
                        {
                            AniDB_AnimeID = anime.AnimeID,
                            Language = "Japanese",
                            RoleType = (int)StaffRoleType.Seiyuu,
                            Role = role,
                            RoleID = character.CharacterID,
                            StaffID = staff.StaffID
                        };
                        RepoFactory.CrossRef_Anime_Staff.Save(xrefAnimeStaff);
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e, "Unable to Populate and Save Seiyuus for {MainTitle}", anime.AnimeID);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to Populate and Save Characters for {MainTitle}", anime.AnimeID);
            }
        }

        try
        {
            RepoFactory.AniDB_Character.Save(chrsToSave);
            RepoFactory.AniDB_Anime_Character.Save(xrefsToSave);
            RepoFactory.AniDB_Seiyuu.Save(seiyuuToSave.Values.ToList());
            RepoFactory.AniDB_Character_Seiyuu.Save(seiyuuXrefToSave);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to Save Characters and Seiyuus for {MainTitle}", anime.MainTitle);
        }
    }

    private void CreateStaff(List<ResponseStaff> staffList, SVR_AniDB_Anime anime)
    {
        if (staffList == null) return;

        // delete all the existing cross references just in case one has been removed
        var animeStaff =
            RepoFactory.AniDB_Anime_Staff.GetByAnimeID(anime.AnimeID);

        try
        {
            RepoFactory.AniDB_Anime_Staff.Delete(animeStaff);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to Remove Staff for {MainTitle}", anime.MainTitle);
        }

        var animeStaffToSave = new List<AniDB_Anime_Staff>();
        var xRefToSave = new List<CrossRef_Anime_Staff>();
        foreach (var rawStaff in staffList)
        {
            try
            {
                // save the link between character and seiyuu
                var stf = RepoFactory.AniDB_Anime_Staff.GetByAnimeIDAndCreatorID(rawStaff.AnimeID, rawStaff.CreatorID);
                if (stf == null)
                {
                    stf = new AniDB_Anime_Staff
                    {
                        AnimeID = rawStaff.AnimeID,
                        CreatorID = rawStaff.CreatorID,
                        CreatorType = rawStaff.CreatorType
                    };
                    animeStaffToSave.Add(stf);
                }

                var staff = RepoFactory.AnimeStaff.GetByAniDBID(stf.CreatorID);
                if (staff == null)
                {
                    staff = new AnimeStaff
                    {
                        // Unfortunately, most of the info is not provided
                        AniDBID = rawStaff.CreatorID, Name = rawStaff.CreatorName
                    };
                    // we need an ID for xref
                    RepoFactory.AnimeStaff.Save(staff);
                }

                var roleType = rawStaff.CreatorType switch
                {
                    "Animation Work" => StaffRoleType.Studio,
                    "Work" => StaffRoleType.Studio,
                    "Original Work" => StaffRoleType.SourceWork,
                    "Music" => StaffRoleType.Music,
                    "Character Design" => StaffRoleType.CharacterDesign,
                    "Direction" => StaffRoleType.Director,
                    "Series Composition" => StaffRoleType.SeriesComposer,
                    "Chief Animation Direction" => StaffRoleType.Producer,
                    _ => StaffRoleType.Staff
                };

                var xrefAnimeStaff =
                    RepoFactory.CrossRef_Anime_Staff.GetByParts(anime.AnimeID, null, staff.StaffID, roleType);
                if (xrefAnimeStaff != null)
                {
                    continue;
                }

                var role = rawStaff.CreatorType;
                if (CrossRef_Anime_StaffRepository.Roles.ContainsKey(role))
                {
                    role = CrossRef_Anime_StaffRepository.Roles[role].ToString().Replace("_", " ");
                }

                xrefAnimeStaff = new CrossRef_Anime_Staff
                {
                    AniDB_AnimeID = anime.AnimeID,
                    Language = "Japanese",
                    RoleType = (int)roleType,
                    Role = role,
                    RoleID = null,
                    StaffID = staff.StaffID
                };
                xRefToSave.Add(xrefAnimeStaff);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to Populate and Save Staff for {MainTitle}", anime.MainTitle);
            }
        }

        try
        {
            RepoFactory.AniDB_Anime_Staff.Save(animeStaffToSave);
            RepoFactory.CrossRef_Anime_Staff.Save(xRefToSave);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to Save Staff for {MainTitle}", anime.MainTitle);
        }
    }

    private static void CreateResources(List<ResponseResource> resources, SVR_AniDB_Anime anime)
    {
        if (resources == null)
        {
            return;
        }

        var malLinks = new List<CrossRef_AniDB_MAL>();
        foreach (var resource in resources)
        {
            int id;
            switch (resource.ResourceType)
            {
                case AniDB_ResourceLinkType.ANN:
                    {
                        if (!int.TryParse(resource.ResourceID, out id))
                        {
                            break;
                        }

                        if (id == 0)
                        {
                            break;
                        }

                        anime.ANNID = id;
                        break;
                    }
                case AniDB_ResourceLinkType.ALLCinema:
                    {
                        if (!int.TryParse(resource.ResourceID, out id))
                        {
                            break;
                        }

                        if (id == 0)
                        {
                            break;
                        }

                        anime.AllCinemaID = id;
                        break;
                    }
                case AniDB_ResourceLinkType.VNDB:
                    {
                        if (!int.TryParse(resource.ResourceID, out id))
                        {
                            break;
                        }

                        if (id == 0)
                        {
                            break;
                        }

                        anime.VNDBID = id;
                        break;
                    }
                case AniDB_ResourceLinkType.Bangumi:
                    {
                        if (!int.TryParse(resource.ResourceID, out id))
                        {
                            break;
                        }

                        if (id == 0)
                        {
                            break;
                        }

                        anime.BangumiID = id;
                        break;
                    }
                case AniDB_ResourceLinkType.DotLain:
                    {
                        if (!int.TryParse(resource.ResourceID, out id))
                        {
                            break;
                        }

                        if (id == 0)
                        {
                            break;
                        }

                        anime.LainID = id;
                        break;
                    }
                case AniDB_ResourceLinkType.Site_JP:
                    {
                        if (string.IsNullOrEmpty(anime.Site_JP))
                            anime.Site_JP = resource.ResourceID;
                        else
                            anime.Site_JP += "|" + resource.ResourceID;
                        break;
                    }
                case AniDB_ResourceLinkType.Site_EN:
                    {
                        if (string.IsNullOrEmpty(anime.Site_EN))
                            anime.Site_EN = resource.ResourceID;
                        else
                            anime.Site_EN += "|" + resource.ResourceID;
                        break;
                    }
                case AniDB_ResourceLinkType.Wiki_EN:
                    {
                        anime.Wikipedia_ID = resource.ResourceID;
                        break;
                    }
                case AniDB_ResourceLinkType.Wiki_JP:
                    {
                        if (!int.TryParse(resource.ResourceID, out id))
                        {
                            break;
                        }

                        if (id == 0)
                        {
                            break;
                        }

                        anime.WikipediaJP_ID = resource.ResourceID;
                        break;
                    }
                case AniDB_ResourceLinkType.Syoboi:
                    {
                        if (!int.TryParse(resource.ResourceID, out id))
                        {
                            break;
                        }

                        if (id == 0)
                        {
                            break;
                        }

                        anime.SyoboiID = id;
                        break;
                    }
                case AniDB_ResourceLinkType.Anison:
                    {
                        if (!int.TryParse(resource.ResourceID, out id))
                        {
                            break;
                        }

                        if (id == 0)
                        {
                            break;
                        }

                        anime.AnisonID = id;
                        break;
                    }
                case AniDB_ResourceLinkType.Crunchyroll:
                    {
                        anime.CrunchyrollID = resource.ResourceID;
                        break;
                    }
                case AniDB_ResourceLinkType.Funimation:
                    {
                        anime.FunimationID = resource.ResourceID;
                        break;
                    }
                case AniDB_ResourceLinkType.HiDive:
                    {
                        anime.HiDiveID = resource.ResourceID;
                        break;
                    }
                case AniDB_ResourceLinkType.MAL:
                    {
                        if (!int.TryParse(resource.ResourceID, out id))
                        {
                            break;
                        }

                        if (id == 0)
                        {
                            break;
                        }

                        if (RepoFactory.CrossRef_AniDB_MAL.GetByMALID(id).Any(a => a.AnimeID == anime.AnimeID))
                        {
                            continue;
                        }

                        var xref = new CrossRef_AniDB_MAL
                        {
                            AnimeID = anime.AnimeID,
                            CrossRefSource = (int)CrossRefSource.AniDB,
                            MALID = id,
                            StartEpisodeNumber = 1,
                            StartEpisodeType = 1
                        };

                        malLinks.Add(xref);
                        break;
                    }
            }
        }

        RepoFactory.CrossRef_AniDB_MAL.Save(malLinks);
    }

    private static void CreateRelations(List<ResponseRelation> rels)
    {
        if (rels == null) return;

        var relsToSave = new List<SVR_AniDB_Anime_Relation>();
        foreach (var rawrel in rels)
        {
            if ((rawrel?.AnimeID ?? 0) <= 0 || rawrel.RelatedAnimeID <= 0)
            {
                continue;
            }

            var animeRel =
                RepoFactory.AniDB_Anime_Relation.GetByAnimeIDAndRelationID(rawrel.AnimeID,
                    rawrel.RelatedAnimeID) ?? new SVR_AniDB_Anime_Relation();
            animeRel.AnimeID = rawrel.AnimeID;
            animeRel.RelatedAnimeID = rawrel.RelatedAnimeID;
            animeRel.RelationType = rawrel.RelationType switch
            {
                RelationType.Prequel => "prequel",
                RelationType.Sequel => "sequel",
                RelationType.MainStory => "parent story",
                RelationType.SideStory => "side story",
                RelationType.FullStory => "full story",
                RelationType.Summary => "summary",
                RelationType.Other => "other",
                RelationType.AlternativeSetting => "alternative setting",
                RelationType.SameSetting => "same setting",
                RelationType.SharedCharacters => "character",
                _ => "other"
            };
            relsToSave.Add(animeRel);
        }

        RepoFactory.AniDB_Anime_Relation.Save(relsToSave);
    }

    private static void CreateSimilarAnime(List<ResponseSimilar> sims)
    {
        if (sims == null) return;

        var recsToSave = new List<AniDB_Anime_Similar>();

        foreach (var rawsim in sims)
        {
            var animeSim =
                RepoFactory.AniDB_Anime_Similar.GetByAnimeIDAndSimilarID(rawsim.AnimeID, rawsim.SimilarAnimeID) ??
                new AniDB_Anime_Similar();

            if (rawsim.AnimeID <= 0 || rawsim.Approval < 0 || rawsim.SimilarAnimeID <= 0 || rawsim.Total < 0)
            {
                continue;
            }

            animeSim.AnimeID = rawsim.AnimeID;
            animeSim.Approval = rawsim.Approval;
            animeSim.Total = rawsim.Total;
            animeSim.SimilarAnimeID = rawsim.SimilarAnimeID;
            recsToSave.Add(animeSim);
        }

        RepoFactory.AniDB_Anime_Similar.Save(recsToSave);
    }
}
