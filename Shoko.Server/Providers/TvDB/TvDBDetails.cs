﻿using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using Shoko.Models.Server;
using Shoko.Server.Repositories;

namespace Shoko.Models.TvDB;

public class TvDBDetails
{
    private static Logger logger = LogManager.GetCurrentClassLogger();

    public int TvDBID { get; set; }

    public TvDBDetails(int tvDBID)
    {
        TvDBID = tvDBID;

        PopulateTvDBEpisodes();
    }

    private Dictionary<int, TvDB_Episode> dictTvDBEpisodes;

    public Dictionary<int, TvDB_Episode> DictTvDBEpisodes
    {
        get
        {
            if (dictTvDBEpisodes == null)
            {
                try
                {
                    if (TvDBEpisodes != null)
                    {
                        var start = DateTime.Now;

                        dictTvDBEpisodes = new Dictionary<int, TvDB_Episode>();
                        // create a dictionary of absolute episode numbers for tvdb episodes
                        // sort by season and episode number
                        // ignore season 0, which is used for specials
                        var eps = TvDBEpisodes;


                        var i = 1;
                        foreach (var ep in eps)
                        {
                            dictTvDBEpisodes[i] = ep;
                            i++;
                        }

                        var ts = DateTime.Now - start;
                    }
                }
                catch (Exception ex)
                {
                    logger.Error(ex, ex.ToString());
                }
            }

            return dictTvDBEpisodes;
        }
    }

    private Dictionary<int, int> dictTvDBSeasons;

    public Dictionary<int, int> DictTvDBSeasons
    {
        get
        {
            if (dictTvDBSeasons == null)
            {
                try
                {
                    if (TvDBEpisodes != null)
                    {
                        var start = DateTime.Now;

                        dictTvDBSeasons = new Dictionary<int, int>();
                        // create a dictionary of season numbers and the first episode for that season

                        var eps = TvDBEpisodes;
                        var i = 1;
                        var lastSeason = -999;
                        foreach (var ep in eps)
                        {
                            if (ep.SeasonNumber != lastSeason)
                            {
                                dictTvDBSeasons[ep.SeasonNumber] = i;
                            }

                            lastSeason = ep.SeasonNumber;
                            i++;
                        }

                        var ts = DateTime.Now - start;
                        //logger.Trace("Got TvDB Seasons in {0} ms", ts.TotalMilliseconds);
                    }
                }
                catch (Exception ex)
                {
                    logger.Error(ex, ex.ToString());
                }
            }

            return dictTvDBSeasons;
        }
    }

    private Dictionary<int, int> dictTvDBSeasonsSpecials;

    public Dictionary<int, int> DictTvDBSeasonsSpecials
    {
        get
        {
            if (dictTvDBSeasonsSpecials == null)
            {
                try
                {
                    if (TvDBEpisodes != null)
                    {
                        var start = DateTime.Now;

                        dictTvDBSeasonsSpecials = new Dictionary<int, int>();
                        // create a dictionary of season numbers and the first episode for that season

                        var eps = TvDBEpisodes;
                        var i = 1;
                        var lastSeason = -999;
                        foreach (var ep in eps)
                        {
                            if (ep.SeasonNumber > 0)
                            {
                                continue;
                            }

                            var thisSeason = 0;
                            if (ep.AirsBeforeSeason.HasValue)
                            {
                                thisSeason = ep.AirsBeforeSeason.Value;
                            }

                            if (ep.AirsAfterSeason.HasValue)
                            {
                                thisSeason = ep.AirsAfterSeason.Value;
                            }

                            if (thisSeason != lastSeason)
                            {
                                dictTvDBSeasonsSpecials[thisSeason] = i;
                            }

                            lastSeason = thisSeason;
                            i++;
                        }

                        var ts = DateTime.Now - start;
                        //logger.Trace("Got TvDB Seasons in {0} ms", ts.TotalMilliseconds);
                    }
                }
                catch (Exception ex)
                {
                    logger.Error(ex, ex.ToString());
                }
            }

            return dictTvDBSeasonsSpecials;
        }
    }

    private void PopulateTvDBEpisodes()
    {
        try
        {
            tvDBEpisodes = RepoFactory.TvDB_Episode.GetBySeriesID(TvDBID)
                .OrderBy(a => a.SeasonNumber)
                .ThenBy(a => a.EpisodeNumber)
                .ToList();
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
        }
    }

    private List<TvDB_Episode> tvDBEpisodes;

    public List<TvDB_Episode> TvDBEpisodes
    {
        get
        {
            if (tvDBEpisodes == null)
            {
                PopulateTvDBEpisodes();
            }

            return tvDBEpisodes;
        }
    }
}
