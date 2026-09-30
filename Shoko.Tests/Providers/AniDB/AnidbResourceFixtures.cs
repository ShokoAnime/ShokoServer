namespace Shoko.Tests.Providers.AniDB;

/// <summary>
/// Anime documents cut from AniDB's cached HTTP XML, keeping the resources of
/// the anime and of a few episodes exactly as AniDB sent them.
/// </summary>
internal static class AnidbResourceFixtures
{
    /// <summary>
    /// Anime 16984: two official sites, two official streams, a TMDB entry
    /// with its kind, and episodes with two Crunchyroll episode IDs each.
    /// </summary>
    public const string Anime16984 = """
        <?xml version="1.0" encoding="UTF-8"?><anime id="16984" restricted="false">
            <type>Web</type>
            <episodecount>12</episodecount>
            <startdate>2022-07-30</startdate>
            <enddate>2022-10-08</enddate>
            <titles>
                <title xml:lang="x-zht" type="main">Shi Cao Lao Long Bei Guan Yi E Long Zhi Ming</title>
            </titles>
            <resources>
                <resource type="1">
                    <externalentity>
                        <identifier>24986</identifier>
                    </externalentity>
                </resource>
                <resource type="2">
                    <externalentity>
                        <identifier>50421</identifier>
                    </externalentity>
                </resource>
                <resource type="4">
                    <externalentity>
                        <url>https://soushokudragon.jp/</url>
                    </externalentity>
                    <externalentity>
                        <url>https://weibo.com/u/7723662904</url>
                    </externalentity>
                </resource>
                <resource type="6">
                    <externalentity>
                        <identifier>A_Herbivorous_Dragon_of_5,000_Years_Gets_Unfairly_Villainized</identifier>
                    </externalentity>
                </resource>
                <resource type="7">
                    <externalentity>
                        <identifier>齢5000年の草食ドラゴン、いわれなき邪竜認定</identifier>
                    </externalentity>
                </resource>
                <resource type="8">
                    <externalentity>
                        <identifier>6557</identifier>
                    </externalentity>
                </resource>
                <resource type="9">
                    <externalentity>
                        <identifier>384591</identifier>
                    </externalentity>
                </resource>
                <resource type="10">
                    <externalentity>
                        <identifier>24706</identifier>
                    </externalentity>
                </resource>
                <resource type="23">
                    <externalentity>
                        <identifier>soushoku_dragon</identifier>
                    </externalentity>
                </resource>
                <resource type="26">
                    <externalentity>
                        <identifier>@user-yx2jg4xk1i</identifier>
                    </externalentity>
                </resource>
                <resource type="28">
                    <externalentity>
                        <identifier>G1XHJV2NJ</identifier>
                    </externalentity>
                </resource>
                <resource type="33">
                    <externalentity>
                        <identifier>食草老龙被冠以恶龙之名</identifier>
                    </externalentity>
                </resource>
                <resource type="34">
                    <externalentity>
                        <url>https://www.bilibili.tv/en/media/2072897/</url>
                    </externalentity>
                    <externalentity>
                        <url>https://www.bilibili.tv/en/media/1063156</url>
                    </externalentity>
                </resource>
                <resource type="38">
                    <externalentity>
                        <identifier>358724</identifier>
                    </externalentity>
                </resource>
                <resource type="39">
                    <externalentity>
                        <identifier>35679825</identifier>
                    </externalentity>
                </resource>
                <resource type="44">
                    <externalentity>
                        <identifier>139161</identifier>
                        <identifier>tv</identifier>
                    </externalentity>
                </resource>
                <resource type="47">
                    <externalentity>
                        <identifier>bangumi/media/md28235387</identifier>
                    </externalentity>
                </resource>
            </resources>
            <episodes>
                <episode id="257718" update="2022-09-03">
                    <epno type="1">1</epno>
                    <length>15</length>
                    <airdate>2022-07-30</airdate>
                    <rating votes="4">3.75</rating>
                    <title xml:lang="en">Please Eat Me Up, Great Evil Dragon!</title>
                    <resources>
                        <resource type="28">
                            <externalentity>
                                <identifier>G2XU0Q9EN</identifier>
                            </externalentity>
                            <externalentity>
                                <identifier>G8WUN89G9</identifier>
                            </externalentity>
                        </resource>
                    </resources>
                </episode>
                <episode id="257719" update="2022-09-03">
                    <epno type="1">2</epno>
                    <length>15</length>
                    <airdate>2022-07-30</airdate>
                    <rating votes="1">3.31</rating>
                    <title xml:lang="en">Let`s Set Out on an Expedition, Great Evil Dragon!</title>
                    <resources>
                        <resource type="28">
                            <externalentity>
                                <identifier>G8WUNM5Z5</identifier>
                            </externalentity>
                            <externalentity>
                                <identifier>GZ7UV8N27</identifier>
                            </externalentity>
                        </resource>
                    </resources>
                </episode>
            </episodes>
        </anime>
        """;

    /// <summary>
    /// Anime 4459: two MyAnimeList IDs, an AnimeNfo ID with its slug, an
    /// IMDb and a TMDB entry, and type 31, which Shoko does not know, plus
    /// type 35 from anime 1078, a URL of a type Shoko does not know. Its
    /// episode has no resources.
    /// </summary>
    public const string Anime4459 = """
        <?xml version="1.0" encoding="UTF-8"?><anime id="4459" restricted="false">
            <type>TV Series</type>
            <episodecount>26</episodecount>
            <startdate>2006-04-09</startdate>
            <enddate>2006-10-08</enddate>
            <titles>
                <title xml:lang="x-jat" type="main">Gun-dou Musashi</title>
            </titles>
            <resources>
                <resource type="2">
                    <externalentity>
                        <identifier>1200</identifier>
                    </externalentity>
                    <externalentity>
                        <identifier>10270</identifier>
                    </externalentity>
                </resource>
                <resource type="3">
                    <externalentity>
                        <identifier>3734</identifier>
                        <identifier>uphzlk</identifier>
                    </externalentity>
                </resource>
                <resource type="31">
                    <externalentity>
                        <identifier>12339</identifier>
                    </externalentity>
                </resource>
                <resource type="35">
                    <externalentity>
                        <url>https://chinesedora.com/database/animation/doraemon</url>
                    </externalentity>
                </resource>
                <resource type="43">
                    <externalentity>
                        <identifier>tt0846454</identifier>
                    </externalentity>
                </resource>
                <resource type="44">
                    <externalentity>
                        <identifier>26768</identifier>
                        <identifier>tv</identifier>
                    </externalentity>
                </resource>
            </resources>
            <episodes>
                <episode id="49323" update="2011-10-20">
                    <epno type="1">1</epno>
                    <length>30</length>
                    <airdate>2006-04-09</airdate>
                    <rating votes="3">1.04</rating>
                    <title xml:lang="en">Musashi Gun Road</title>
                </episode>
            </episodes>
        </anime>
        """;
}
