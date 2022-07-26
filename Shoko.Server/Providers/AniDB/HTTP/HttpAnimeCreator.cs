using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using NHibernate;
using Shoko.Commons.Extensions;
using Shoko.Commons.Utils;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Plugin.Abstractions.Extensions;
using Shoko.Server.Extensions;
using Shoko.Server.ImageDownload;
using Shoko.Server.Models;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Providers.AniDB.Http;
using Shoko.Server.Providers.AniDB.Http.GetAnime;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.NHibernate;
using Shoko.Server.Settings;
using EpisodeType = Shoko.Models.Enums.EpisodeType;

namespace Shoko.Server.AniDB_API
{
    public class HttpAnimeCreator
    {
        private readonly ILogger<HttpAnimeCreator> _logger;

        public HttpAnimeCreator(ILogger<HttpAnimeCreator> logger)
        {
            _logger = logger;
        }


        public bool CreateAnime(ISession session, ResponseGetAnime response, SVR_AniDB_Anime anime, int relDepth)
        {
            _logger.LogTrace("------------------------------------------------");
            _logger.LogTrace(
                "PopulateAndSaveFromHTTP: for {AnimeID} - {MainTitle} @ Depth: {RelationDepth}/{MaxRelationDepth}", response.Anime.AnimeID, response.Anime.MainTitle, relDepth,
                ServerSettings.Instance.AniDb.MaxRelationDepth
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
                return false;
            }

            // save now for FK purposes
            RepoFactory.AniDB_Anime.Save(anime, generateTvDBMatches: false);

            taskTimer.Stop();
            _logger.LogTrace("PopulateAnime in : {Time:ss.ffff}", taskTimer.Elapsed);
            taskTimer.Restart();

            CreateEpisodes(response.Episodes, anime);
            taskTimer.Stop();
            _logger.LogTrace("CreateEpisodes in : {Time:ss.ffff}", taskTimer.Elapsed);
            taskTimer.Restart();

            CreateTitles(response.Titles, anime);
            taskTimer.Stop();
            _logger.LogTrace("CreateTitles in : {Time:ss.ffff}", taskTimer.Elapsed);
            taskTimer.Restart();

            CreateTags(response.Tags, anime);
            taskTimer.Stop();
            _logger.LogTrace("CreateTags in : {Time:ss.ffff}", taskTimer.Elapsed);
            taskTimer.Restart();

            CreateCharacters(session, response.Characters, anime);
            taskTimer.Stop();
            _logger.LogTrace("CreateCharacters in : {Time:ss.ffff}", taskTimer.Elapsed);
            taskTimer.Restart();

            CreateStaff(session, response.Staff, anime);
            taskTimer.Stop();
            _logger.LogTrace("CreateStaff in : {Time:ss.ffff}", taskTimer.Elapsed);
            taskTimer.Restart();

            CreateResources(response.Resources, anime);
            taskTimer.Stop();
            _logger.LogTrace("CreateResources in : {Time:ss.ffff}", taskTimer.Elapsed);
            taskTimer.Restart();

            CreateRelations(session, response.Relations, anime);
            taskTimer.Stop();
            _logger.LogTrace("CreateRelations in : {Time:ss.ffff}", taskTimer.Elapsed);
            taskTimer.Restart();

            CreateSimilarAnime(session, response.Similar, anime);
            taskTimer.Stop();
            _logger.LogTrace("CreateSimilarAnime in : {Time:ss.ffff}", taskTimer.Elapsed);
            taskTimer.Restart();

            RepoFactory.AniDB_Anime.Save(anime);
            totalTimer.Stop();
            _logger.LogTrace("TOTAL TIME in : {Time:ss.ffff}", totalTimer.Elapsed);
            _logger.LogTrace("------------------------------------------------");
            return true;
        }
        
        private static bool PopulateAnime(ResponseAnime animeInfo, SVR_AniDB_Anime anime)
        {
            // We need various values to be populated to be considered valid
            if (string.IsNullOrEmpty(animeInfo?.MainTitle) || animeInfo.AnimeID <= 0) return false;
            anime.AirDate = animeInfo.AirDate;
            anime.AllCinemaID = animeInfo.AllCinemaID;
            anime.AnimeID = animeInfo.AnimeID;
            anime.AnimePlanetID = animeInfo.AnimePlanetID;
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

        private void CreateEpisodes(List<ResponseEpisode> eps, SVR_AniDB_Anime anime)
        {
            if (eps == null) return;

            var episodeCountSpecial = 0;
            var episodeCountNormal = 0;

            var currentAniDBEpisodes=RepoFactory.AniDB_Episode.GetByAnimeID(anime.AnimeID).ToDictionary(a=>a.EpisodeID,a=>a);
            var currentAnimeEpisodes = currentAniDBEpisodes.Select(a => RepoFactory.AnimeEpisode.GetByAniDBEpisodeID(a.Key)).Where(a=>a!=null).ToDictionary(a => a.AniDB_EpisodeID, a => a);
            var oldtitles = currentAniDBEpisodes.Select(a => RepoFactory.AniDB_Episode_Title.GetByEpisodeID(a.Key)).Where(a=>a!=null).SelectMany(a => a).ToList();
            RepoFactory.AniDB_Episode_Title.Delete(oldtitles);
            
            var epsToSave = new List<AniDB_Episode>();
            var titlesToSave = new List<AniDB_Episode_Title>();

            foreach (var epraw in eps)
            {
                AniDB_Episode epNew = null;
                if (currentAniDBEpisodes.ContainsKey(epraw.EpisodeID))
                {
                    epNew = currentAniDBEpisodes[epraw.EpisodeID];
                    currentAniDBEpisodes.Remove(epraw.EpisodeID);
                    if (currentAnimeEpisodes.ContainsKey(epraw.EpisodeID))
                        currentAnimeEpisodes.Remove(epraw.EpisodeID);
                }

                epNew ??= new AniDB_Episode();
                epNew.AirDate = AniDB.GetAniDBDateAsSeconds(epraw.AirDate);
                epNew.AnimeID = epraw.AnimeID;
                epNew.DateTimeUpdated = DateTime.Now;
                epNew.EpisodeID = epraw.EpisodeID;
                epNew.EpisodeNumber = epraw.EpisodeNumber;
                epNew.EpisodeType = (int) epraw.EpisodeType;
                epNew.LengthSeconds = epraw.LengthSeconds;
                epNew.Rating = epraw.Rating.ToString(CultureInfo.InvariantCulture);
                epNew.Votes = epraw.Votes.ToString(CultureInfo.InvariantCulture);
                epNew.Description = epraw.Description ?? string.Empty;
                
                epsToSave.Add(epNew);

                // Titles
                titlesToSave.AddRange(epraw.Titles);

                // since the HTTP api doesn't return a count of the number of specials, we will calculate it here
                if (epNew.GetEpisodeTypeEnum() == EpisodeType.Episode)
                    episodeCountNormal++;

                if (epNew.GetEpisodeTypeEnum() == EpisodeType.Special)
                    episodeCountSpecial++;
            }

            if (currentAniDBEpisodes.Count > 0)
            {
                _logger.LogTrace("Deleting the following episodes (no longer in AniDB)");
                foreach (var ep in currentAniDBEpisodes.Values)
                {
                    _logger.LogTrace("AniDB Ep: {EpisodeID} Type: {EpisodeType} Number: {EpisodeNumber}", ep.EpisodeID, ep.EpisodeType, ep.EpisodeNumber);
                }
                foreach (var ep in currentAnimeEpisodes.Values)
                {
                    _logger.LogTrace("Shoko Ep: {AnimeEpisodeID} AniEp: {AniDBEpisodeID}", ep.AnimeEpisodeID, ep.AniDB_EpisodeID);
                }
            }
            RepoFactory.AnimeEpisode.Delete(currentAnimeEpisodes.Values.ToList());
            RepoFactory.AniDB_Episode.Delete(currentAniDBEpisodes.Values.ToList());
            RepoFactory.AniDB_Episode.Save(epsToSave);
            RepoFactory.AniDB_Episode_Title.Save(titlesToSave);

            var episodeCount = episodeCountSpecial + episodeCountNormal;
            anime.EpisodeCountNormal = episodeCountNormal;
            anime.EpisodeCountSpecial = episodeCountSpecial;
            anime.EpisodeCount = episodeCount;
        }

        private void CreateTitles(List<ResponseTitle> titles, SVR_AniDB_Anime anime)
        {
            if (titles == null) return;

            var allTitles = string.Empty;

            var titlesToDelete = RepoFactory.AniDB_Anime_Title.GetByAnimeID(anime.AnimeID);
            var titlesToSave = new List<AniDB_Anime_Title>();
            foreach (var rawtitle in titles)
            {

                if (string.IsNullOrEmpty(rawtitle?.Title)) continue;
                if (rawtitle.AnimeID <= 0) continue;
                var title = new AniDB_Anime_Title();
                title.AnimeID = rawtitle.AnimeID;
                // TODO db migration
                title.Language = rawtitle.Language.GetString();
                title.Title = rawtitle.Title;
                title.TitleType = rawtitle.TitleType.ToString();
                titlesToSave.Add(title);

                if (allTitles.Length > 0) allTitles += "|";
                allTitles += rawtitle.Title;
            }

            anime.AllTitles = allTitles;
            RepoFactory.AniDB_Anime_Title.Delete(titlesToDelete);
            RepoFactory.AniDB_Anime_Title.Save(titlesToSave);
        }

        private void CreateTags(List<ResponseTag> tags, SVR_AniDB_Anime anime)
        {
            if (tags == null) return;

            var allTags = string.Empty;

            var tagsToSave = new List<AniDB_Tag>();
            var xrefsToSave = new List<AniDB_Anime_Tag>();

            // find all the current links, and then later remove the ones that are no longer relevant
            var currentTags = RepoFactory.AniDB_Anime_Tag.GetByAnimeID(anime.AnimeID);
            var newTagIDs = new List<int>();

            foreach (var rawtag in tags)
            {
                var tag = RepoFactory.AniDB_Tag.GetByTagID(rawtag.TagID);

                if (tag == null)
                {
                    // There are situations in which an ID may have changed, this is usually due to it being moved
                    var existingTags = RepoFactory.AniDB_Tag.GetByName(rawtag.TagName).ToList();
                    var xrefsToRemap = existingTags.SelectMany(a => RepoFactory.AniDB_Anime_Tag.GetByTagID(a.TagID))
                        .ToList();
                    foreach (var xref in xrefsToRemap)
                    {
                        xref.TagID = rawtag.TagID;
                        RepoFactory.AniDB_Anime_Tag.Save(xref);
                    }
                    // Delete the obsolete tag(s)
                    RepoFactory.AniDB_Tag.Delete(existingTags);

                    // While we're at it, clean up other unreferenced tags
                    RepoFactory.AniDB_Tag.Delete(RepoFactory.AniDB_Tag.GetAll()
                        .Where(a => !RepoFactory.AniDB_Anime_Tag.GetByTagID(a.TagID).Any()).ToList());
                    
                    // Also clean up dead xrefs (shouldn't happen, but sometimes does)
                    var orphanedXRefs = RepoFactory.AniDB_Anime_Tag.GetAll().Where(a =>
                        RepoFactory.AniDB_Tag.GetByTagID(a.TagID) == null ||
                        RepoFactory.AniDB_Anime.GetByAnimeID(a.AnimeID) == null).ToList();
                    
                    RepoFactory.AniDB_Anime_Tag.Delete(orphanedXRefs);

                    tag = new AniDB_Tag();
                }

                if (string.IsNullOrEmpty(rawtag.TagName)) continue;
                if (rawtag.TagID <= 0) continue;
                tag.TagID = rawtag.TagID;
                tag.GlobalSpoiler = rawtag.GlobalSpoiler ? 1 : 0;
                tag.LocalSpoiler = rawtag.LocalSpoiler ? 1 : 0;
                tag.Spoiler = 0;
                tag.TagCount = 0;
                tag.TagDescription = rawtag.TagDescription ?? string.Empty;
                tag.TagName = rawtag.TagName;
                tagsToSave.Add(tag);

                newTagIDs.Add(tag.TagID);

                var animeTag = RepoFactory.AniDB_Anime_Tag.GetByAnimeIDAndTagID(rawtag.AnimeID, rawtag.TagID) ?? new AniDB_Anime_Tag();
                animeTag.AnimeID = rawtag.AnimeID;
                animeTag.TagID = rawtag.TagID;
                animeTag.Approval = 100;
                animeTag.Weight = rawtag.Weight;
                xrefsToSave.Add(animeTag);

                if (allTags.Length > 0) allTags += "|";
                allTags += tag.TagName;
            }

            anime.AllTags = allTags;

            var xrefsToDelete = currentTags.Where(curTag => !newTagIDs.Contains(curTag.TagID)).ToList();
            RepoFactory.AniDB_Tag.Save(tagsToSave);
            RepoFactory.AniDB_Anime_Tag.Save(xrefsToSave);
            RepoFactory.AniDB_Anime_Tag.Delete(xrefsToDelete);
        }

        private void CreateCharacters(ISession session, List<ResponseCharacter> chars, SVR_AniDB_Anime anime)
        {
            if (chars == null) return;


            var sessionWrapper = session.Wrap();

            // delete all the existing cross references just in case one has been removed
            var animeChars =
                RepoFactory.AniDB_Anime_Character.GetByAnimeID(sessionWrapper, anime.AnimeID);

            try
            {
                RepoFactory.AniDB_Anime_Character.Delete(animeChars);
            }
            catch (Exception ex)
            {
                _logger.LogError("Unable to Remove Characters for {MainTitle}: {Ex}", anime.MainTitle, ex);
            }


            var chrsToSave = new List<AniDB_Character>();
            var xrefsToSave = new List<AniDB_Anime_Character>();

            var seiyuuToSave = new Dictionary<int, AniDB_Seiyuu>();
            var seiyuuXrefToSave = new List<AniDB_Character_Seiyuu>();

            // delete existing relationships to seiyuu's
            var charSeiyuusToDelete = chars.SelectMany(rawchar => RepoFactory.AniDB_Character_Seiyuu.GetByCharID(session, rawchar.CharacterID)).ToList();
            try
            {
                RepoFactory.AniDB_Character_Seiyuu.Delete(charSeiyuusToDelete);
            }
            catch (Exception ex)
            {
                _logger.LogError("Unable to Remove Seiyuus for {MainTitle}: {Ex}", anime.MainTitle, ex);
            }

            var charBasePath = ImageUtils.GetBaseAniDBCharacterImagesPath() + Path.DirectorySeparatorChar;
            var creatorBasePath = ImageUtils.GetBaseAniDBCreatorImagesPath() + Path.DirectorySeparatorChar;
            foreach (var rawchar in chars)
            {
                try
                {
                    var chr = RepoFactory.AniDB_Character.GetByCharID(sessionWrapper, rawchar.CharacterID) ??
                              new AniDB_Character();

                    if (chr.CharID != 0)
                    {
                        // only update the fields that come from HTTP API
                        if (string.IsNullOrEmpty(rawchar?.CharacterName)) continue;
                        chr.CharDescription = rawchar.CharacterDescription ?? string.Empty;
                        chr.CharName = rawchar.CharacterName;
                        chr.PicName = rawchar.PicName ?? string.Empty;
                    }
                    else
                    {
                        if (rawchar == null) continue;
                        if (rawchar.CharacterID <= 0 || string.IsNullOrEmpty(rawchar.CharacterName)) continue;
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
                            ImagePath = chr.GetPosterPath()?.Replace(charBasePath, ""),
                        };
                        // we need an ID for xref
                        RepoFactory.AnimeCharacter.Save(character);
                    }

                    // create cross ref's between anime and character, but don't actually download anything
                    var animeChar = new AniDB_Anime_Character();
                    if (rawchar.AnimeID <= 0 || rawchar.CharacterID <= 0 || string.IsNullOrEmpty(rawchar.CharacterType)) continue;
                    animeChar.CharID = rawchar.CharacterID;
                    animeChar.AnimeID = rawchar.AnimeID;
                    animeChar.CharType = rawchar.CharacterType;
                    xrefsToSave.Add(animeChar);

                    foreach (var rawSeiyuu in rawchar.Seiyuus)
                    {
                        try
                        {
                            // save the link between character and seiyuu
                            var acc = RepoFactory.AniDB_Character_Seiyuu.GetByCharIDAndSeiyuuID(session,
                                rawchar.CharacterID,
                                rawSeiyuu.SeiyuuID);
                            if (acc == null)
                            {
                                acc = new AniDB_Character_Seiyuu { CharID = chr.CharID, SeiyuuID = rawSeiyuu.SeiyuuID };
                                seiyuuXrefToSave.Add(acc);
                            }

                            // save the seiyuu
                            var seiyuu = RepoFactory.AniDB_Seiyuu.GetBySeiyuuID(session, rawSeiyuu.SeiyuuID);
                            if (seiyuu == null) seiyuu = new AniDB_Seiyuu();
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
                                    ImagePath = seiyuu.GetPosterPath()?.Replace(creatorBasePath, ""),
                                };
                                // we need an ID for xref
                                RepoFactory.AnimeStaff.Save(staff);
                            }

                            var xrefAnimeStaff = RepoFactory.CrossRef_Anime_Staff.GetByParts(anime.AnimeID, character.CharacterID,
                                staff.StaffID, StaffRoleType.Seiyuu);
                            if (xrefAnimeStaff != null) continue;
                            var role = rawchar.CharacterType;
                            if (CrossRef_Anime_StaffRepository.Roles.ContainsKey(role))
                                role = CrossRef_Anime_StaffRepository.Roles[role].ToString().Replace("_", " ");
                            xrefAnimeStaff = new CrossRef_Anime_Staff
                            {
                                AniDB_AnimeID = anime.AnimeID,
                                Language = "Japanese",
                                RoleType = (int) StaffRoleType.Seiyuu,
                                Role = role,
                                RoleID = character.CharacterID,
                                StaffID = staff.StaffID,
                            };
                            RepoFactory.CrossRef_Anime_Staff.Save(xrefAnimeStaff);
                        }
                        catch (Exception e)
                        {
                            _logger.LogError("Unable to Populate and Save Seiyuus for {MainTitle}: {Ex}", anime.AnimeID, e);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError("Unable to Populate and Save Characters for {MainTitle}: {Ex}", anime.AnimeID, ex);
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
                _logger.LogError("Unable to Save Characters and Seiyuus for {MainTitle}: {Ex}", anime.MainTitle, ex);
            }
        }

        private void CreateStaff(ISession session, List<ResponseStaff> staffList, SVR_AniDB_Anime anime)
        {
            if (staffList == null) return;

            var sessionWrapper = session.Wrap();

            // delete all the existing cross references just in case one has been removed
            var animeStaff =
                RepoFactory.AniDB_Anime_Staff.GetByAnimeID(sessionWrapper, anime.AnimeID);

            try
            {
                RepoFactory.AniDB_Anime_Staff.Delete(animeStaff);
            }
            catch (Exception ex)
            {
                _logger.LogError("Unable to Remove Staff for {MainTitle}: {Ex}", anime.MainTitle, ex);
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
                            CreatorType = rawStaff.CreatorType,
                        };
                        animeStaffToSave.Add(stf);
                    }

                    var staff = RepoFactory.AnimeStaff.GetByAniDBID(stf.CreatorID);
                    if (staff == null)
                    {
                        staff = new AnimeStaff
                        {
                            // Unfortunately, most of the info is not provided
                            AniDBID = rawStaff.CreatorID,
                            Name = rawStaff.CreatorName,
                        };
                        // we need an ID for xref
                        RepoFactory.AnimeStaff.Save(staff);
                    }

                    var roleType = rawStaff.CreatorType switch
                    {
                        "Animation Work" => StaffRoleType.Studio,
                        "Original Work" => StaffRoleType.SourceWork,
                        "Music" => StaffRoleType.Music,
                        "Character Design" => StaffRoleType.CharacterDesign,
                        "Direction" => StaffRoleType.Director,
                        "Series Composition" => StaffRoleType.SeriesComposer,
                        "Chief Animation Direction" => StaffRoleType.Producer,
                        _ => StaffRoleType.Staff,
                    };

                    var xrefAnimeStaff = RepoFactory.CrossRef_Anime_Staff.GetByParts(anime.AnimeID, null, staff.StaffID, roleType);
                    if (xrefAnimeStaff != null) continue;
                    var role = rawStaff.CreatorType;
                    if (CrossRef_Anime_StaffRepository.Roles.ContainsKey(role)) role = CrossRef_Anime_StaffRepository.Roles[role].ToString().Replace("_", " ");
                    xrefAnimeStaff = new CrossRef_Anime_Staff
                    {
                        AniDB_AnimeID = anime.AnimeID,
                        Language = "Japanese",
                        RoleType = (int) roleType,
                        Role = role,
                        RoleID = null,
                        StaffID = staff.StaffID,
                    };
                    xRefToSave.Add(xrefAnimeStaff);
                }
                catch (Exception ex)
                {
                    _logger.LogError("Unable to Populate and Save Staff for {MainTitle}: {Ex}", anime.MainTitle, ex);
                }
            }
            try
            {
                RepoFactory.AniDB_Anime_Staff.Save(animeStaffToSave);
                RepoFactory.CrossRef_Anime_Staff.Save(xRefToSave);
            }
            catch (Exception ex)
            {
                _logger.LogError("Unable to Save Staff for {MainTitle}: {Ex}", anime.MainTitle, ex);
            }
        }

        private static void CreateResources(List<ResponseResource> resources, SVR_AniDB_Anime anime)
        {
            if (resources == null) return;
            var malLinks = new List<CrossRef_AniDB_MAL>();
            foreach (var resource in resources)
            {
                int id;
                switch (resource.ResourceType)
                {
                    case AniDB_ResourceLinkType.ANN:
                    {
                        if (!int.TryParse(resource.ResourceID, out id)) break; 
                        if (id == 0) break;
                        anime.ANNID = id;
                        break;
                    }
                    case AniDB_ResourceLinkType.ALLCinema:
                    {
                        if (!int.TryParse(resource.ResourceID, out id)) break; 
                        if (id == 0) break;
                        anime.AllCinemaID = id;
                        break;
                    }
                    case AniDB_ResourceLinkType.AnimeNFO:
                    {
                        if (!int.TryParse(resource.ResourceID, out id)) break; 
                        if (id == 0) break;
                        anime.AnimeNfo = id;
                        break;
                    }
                    case AniDB_ResourceLinkType.Site_JP:
                    {
                        anime.Site_JP = resource.ResourceID;
                        break;
                    }
                    case AniDB_ResourceLinkType.Site_EN:
                    {
                        anime.Site_EN = resource.ResourceID;
                        break;
                    }
                    case AniDB_ResourceLinkType.Wiki_EN:
                    {
                        anime.Wikipedia_ID = resource.ResourceID;
                        break;
                    }
                    case AniDB_ResourceLinkType.Wiki_JP:
                    {
                        if (!int.TryParse(resource.ResourceID, out id)) break; 
                        if (id == 0) break;
                        anime.WikipediaJP_ID = resource.ResourceID;
                        break;
                    }
                    case AniDB_ResourceLinkType.Syoboi:
                    {
                        if (!int.TryParse(resource.ResourceID, out id)) break; 
                        if (id == 0) break;
                        anime.SyoboiID = id;
                        break;
                    }
                    case AniDB_ResourceLinkType.Anison:
                    {
                        if (!int.TryParse(resource.ResourceID, out id)) break; 
                        if (id == 0) break;
                        anime.AnisonID = id;
                        break;
                    }
                    case AniDB_ResourceLinkType.Crunchyroll:
                    {
                        anime.CrunchyrollID = resource.ResourceID;
                        break;
                    }
                    case AniDB_ResourceLinkType.MAL:
                    {
                        if (!int.TryParse(resource.ResourceID, out id)) break; 
                        if (id == 0) break;
                        if (RepoFactory.CrossRef_AniDB_MAL.GetByMALID(id).Any(a => a.AnimeID == anime.AnimeID)) continue;
                        var xref = new CrossRef_AniDB_MAL
                        {
                            AnimeID = anime.AnimeID,
                            CrossRefSource = (int) CrossRefSource.AniDB,
                            MALID = id,
                            StartEpisodeNumber = 1,
                            StartEpisodeType = 1,
                        };

                        malLinks.Add(xref);
                        break;
                    }
                }
            }
            RepoFactory.CrossRef_AniDB_MAL.Save(malLinks);
        }

        private static void CreateRelations(ISession session, List<ResponseRelation> rels, SVR_AniDB_Anime anime)
        {
            if (rels == null) return;

            var relsToSave = new List<SVR_AniDB_Anime_Relation>();
            foreach (var rawrel in rels)
            {
                if ((rawrel?.AnimeID ?? 0) <= 0 || rawrel.RelatedAnimeID <= 0) continue;
                var animeRel = RepoFactory.AniDB_Anime_Relation.GetByAnimeIDAndRelationID(session, rawrel.AnimeID, rawrel.RelatedAnimeID) ?? new SVR_AniDB_Anime_Relation();
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
                    _ => "other",
                };
                relsToSave.Add(animeRel);
            }
            RepoFactory.AniDB_Anime_Relation.Save(relsToSave);
        }

        private static void CreateSimilarAnime(ISession session, List<ResponseSimilar> sims, SVR_AniDB_Anime anime)
        {
            if (sims == null) return;


            var recsToSave = new List<AniDB_Anime_Similar>();

            foreach (var rawsim in sims)
            {
                var animeSim = RepoFactory.AniDB_Anime_Similar.GetByAnimeIDAndSimilarID(session,
                    rawsim.AnimeID,
                    rawsim.SimilarAnimeID);
                if (animeSim == null) animeSim = new AniDB_Anime_Similar();
                if (rawsim.AnimeID <= 0 || rawsim.Approval < 0 || rawsim.SimilarAnimeID <= 0 || rawsim.Total < 0) continue;
                animeSim.AnimeID = rawsim.AnimeID;
                animeSim.Approval = rawsim.Approval;
                animeSim.Total = rawsim.Total;
                animeSim.SimilarAnimeID = rawsim.SimilarAnimeID;
                recsToSave.Add(animeSim);
            }
            RepoFactory.AniDB_Anime_Similar.Save(recsToSave);
        }
    }
}
