using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Shoko.Commons.Extensions;
using Shoko.Models.Client;
using Shoko.Models.Enums;
using Shoko.Models.Server.CrossRef;
using Shoko.Server.Repositories;
using TMDbLib.Objects.Lists;

namespace Shoko.Server.Models
{
    public class SVR_CrossRef_AniDB_Provider : CL_CrossRef_AniDB_Provider
    {
        private readonly Lazy<ProviderEpisodeList> _episodes;

        private readonly Lazy<ProviderEpisodeList> _episodesOverride;

        public SVR_CrossRef_AniDB_Provider()
        {
            _episodes = new Lazy<ProviderEpisodeList>(() => new ProviderEpisodeList(EpisodesData, (str) => EpisodesData = str));
            _episodesOverride = new Lazy<ProviderEpisodeList>(() => new ProviderEpisodeList(EpisodesOverrideData, (str) => EpisodesOverrideData = str));

        }

        [JsonIgnore]
        public ProviderEpisodeList EpisodesList => _episodes.Value;

        [JsonIgnore]
        public ProviderEpisodeList EpisodesListOverride => _episodesOverride.Value;

        public override List<CrossRef_AniDB_ProviderEpisode> Episodes => EpisodesList.Episodes;

        public override List<CrossRef_AniDB_ProviderEpisode> EpisodesOverride => EpisodesListOverride.Episodes;


        public CrossRef_AniDB_ProviderEpisode GetFromAniDBEpisode(int episodeid)
        {
            return EpisodesListOverride.GetByAnimeEpisodeId(episodeid) ?? EpisodesList.GetByAnimeEpisodeId(episodeid);
        }
        public CrossRef_AniDB_ProviderEpisode GetFromCrossRefEpisode(string crossrefepisodeid)
        {
            return EpisodesListOverride.GetByProviderId(crossrefepisodeid) ?? EpisodesList.GetByProviderId(crossrefepisodeid);
        }
        public CrossRef_AniDB_ProviderEpisode GetFromCrossRefEpisode(int crossrefepisodeid)
        {
            string cr = crossrefepisodeid.ToString();
            return EpisodesListOverride.GetByProviderId(cr) ?? EpisodesList.GetByProviderId(cr);
        }
        public class ProviderEpisodeList
        {
            private readonly Dictionary<int, CrossRef_AniDB_ProviderEpisode> _dict = new Dictionary<int, CrossRef_AniDB_ProviderEpisode>();
            private bool _needPersistance;
            private readonly Dictionary<string, int> _providerDict = new Dictionary<string, int>();
            private readonly Action<string> _setter;
            public bool NeedPersitance => _needPersistance;
            public ProviderEpisodeList(string original, Action<string> setter)
            {
                if (!string.IsNullOrEmpty(original))
                {
                    _dict = JsonConvert.DeserializeObject<List<CrossRef_AniDB_ProviderEpisode>>(original).ToDictionary(a => a.AniDBEpisodeID, a => a);
                    _providerDict = _dict.ToDictionary(a => a.Value.ProviderEpisodeID, a => a.Key);
                }

                _setter = setter;
            }

            public List<CrossRef_AniDB_ProviderEpisode> Episodes => _dict.Values.ToList();

            public void Persist()
            {
                if (_needPersistance)
                {
                    lock (_dict)
                    {
                        //Mantain always the same order, so, we can compare in the webcache for equals
                        _setter(JsonConvert.SerializeObject(_dict.Values.OrderBy(a => a.AniDBEpisodeID).ToList(), Formatting.None).Replace("\r", string.Empty).Replace("\n", string.Empty));
                        _needPersistance = false;
                    }
                }
            }

            public CrossRef_AniDB_ProviderEpisode GetByAnimeEpisodeId(int animeEpisodeId)
            {
                lock (_dict)
                {
                    if (_dict.ContainsKey(animeEpisodeId))
                        return _dict[animeEpisodeId];
                }

                return null;
            }

            public CrossRef_AniDB_ProviderEpisode GetByProviderId(string providerEpisodeId)
            {
                lock (_dict)
                {
                    if (_providerDict.ContainsKey(providerEpisodeId))
                        return _dict[_providerDict[providerEpisodeId]];
                }

                return null;
            }
            public void DeleteAllUnverifiedLinks()
            {
                lock (_dict)
                {
                    foreach (CrossRef_AniDB_ProviderEpisode c in _dict.Values.Where(a => a.MatchRating != MatchRating.UserVerified).ToList())
                    {
                        _dict.Remove(c.AniDBEpisodeID);
                        _providerDict.Remove(c.ProviderEpisodeID);
                        _needPersistance = true;
                    }
                }
            }
            public void DeleteFromAnimeEpisodeId(int animeEpisodeId)
            {
                lock (_dict)
                {
                    if (_dict.ContainsKey(animeEpisodeId))
                    {
                        CrossRef_AniDB_ProviderEpisode prov = _dict[animeEpisodeId];
                        _dict.Remove(animeEpisodeId);
                        _providerDict.Remove(prov.ProviderEpisodeID);
                        _needPersistance = true;
                    }
                }

            }
            public void DeleteFromProviderEpisodeId(string providerEpisodeId)
            {
                lock (_dict)
                {
                    if (_providerDict.ContainsKey(providerEpisodeId))
                    {
                        _dict.Remove(_providerDict[providerEpisodeId]);
                        _providerDict.Remove(providerEpisodeId);
                        _needPersistance = true;
                    }
                }
            }
            public void AddOrUpdate(int animeepisodeId, string providerEpisodeId, int season, int episodeNumber, EpisodeType type, MatchRating rating)
            {
                lock (_dict)
                {
                    if (_dict.ContainsKey(animeepisodeId))
                    {
                        CrossRef_AniDB_ProviderEpisode r = _dict[animeepisodeId];
                        if (r.ProviderEpisodeID != providerEpisodeId || r.MatchRating != rating || r.Season!=season || r.Number!=episodeNumber || r.Type!=type)
                        {
                            _providerDict.Remove(r.ProviderEpisodeID);
                            r.ProviderEpisodeID = providerEpisodeId;
                            r.MatchRating = rating;
                            r.Season = season;
                            r.Number = episodeNumber;
                            r.Type = type;
                            _providerDict.Add(r.ProviderEpisodeID, animeepisodeId);
                            _needPersistance = true;
                        }
                    }
                    else
                    {
                        CrossRef_AniDB_ProviderEpisode r = new CrossRef_AniDB_ProviderEpisode();
                        r.AniDBEpisodeID = animeepisodeId;
                        r.ProviderEpisodeID = providerEpisodeId;
                        r.MatchRating = rating;
                        r.Season = season;
                        r.Number = episodeNumber;
                        r.Type = type;
                        _dict.Add(animeepisodeId, r);
                        _providerDict[providerEpisodeId] = animeepisodeId;
                        _needPersistance = true;
                    }
                }
            }
        }
    }
}