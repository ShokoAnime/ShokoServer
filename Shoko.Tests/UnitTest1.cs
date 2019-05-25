using NUnit.Framework;
using Shoko.Database;
using Shoko.Database.Models;
using System.Linq;

namespace Tests
{
    public class Tests
    {
        private ShokoDBContext db;

        [SetUp]
        public void Setup()
        {
            db = new ShokoDBContext();
        }

        [Test]
        public void TestAniDB_Anime()
        {
            var row = db.AniDbAnime.FirstOrDefault();
            Assert.IsNotNull(row, "Query responded with no results");
            Assert.IsNotEmpty(row.MainTitle);
        }

        [Test]
        public void TestAniDB_AnimeUpdate()
        {
            var row = db.AniDbAnimeUpdate.FirstOrDefault();
            Assert.IsNotNull(row, "Query responded with no results");
            Assert.IsNotNull(row.UpdatedAt);
        }

        [Test]
        public void TestCloudAccount()
        {
            var row = db.CloudAccount.FirstOrDefault();
            Assert.IsNotNull(row, "Query responded with no results");
        }

        [Test]
        public void TestImportFolder()
        {
            var row = db.ImportFolder.First();
            Assert.NotNull(row, "Query responded with no results");
            Assert.AreEqual("/anime/", row.Location);
        }

        [Test]
        public void TestVideoLocal()
        {
            var row = db.VideoLocal.First();
            Assert.IsNotNull(row);
            Assert.NotZero(row.VideoLocalPlaces.Count);
            Assert.AreEqual(row.VideoLocalPlaces.First().VideoLocalId, row.VideoLocalId);
        }

        [Test]
        public void TestVideoLocalPlace()
        {
            var row = db.VideoLocalPlace.First();
            Assert.IsNotNull(row);
            Assert.IsNotNull(row.VideoLocal, "VideoLocal_Place.VideoLocal was null");
            Assert.AreEqual(row.VideoLocalId, row.VideoLocal.VideoLocalId);

            Assert.IsNotNull(row.ImportFolder);
            Assert.AreEqual(row.ImportFolderId, row.ImportFolder.ImportFolderID);
        }

        [Test]
        public void Test_AniDBAnimeCategory()
        {
            var row = db.AniDbAnimeCategory.FirstOrDefault();
            Assert.IsNotNull(row, "Query responded with no results");
        }

        [Test]
        public void Test_AniDbAnimeDefaultImage()
        {
            var row = db.AniDbAnimeDefaultImage.FirstOrDefault();
            Assert.IsNotNull(row, "Query responded with no results");
        }

        [Test]
        public void Test_AniDbAnimeRelation()
        {
            var row = db.AniDbAnimeRelation.FirstOrDefault();
            Assert.IsNotNull(row, "Query responded with no results");
        }

        [Test]
        public void Test_AniDbAnimeReview()
        {
            var row = db.AniDbAnimeReview.FirstOrDefault();
            Assert.IsNotNull(row, "Query responded with no results");
        }

        [Test]
        public void Test_AniDbAnimeSimilar()
        {
            var row = db.AniDbAnimeSimilar.FirstOrDefault();
            Assert.IsNotNull(row, "Query responded with no results");
        }

        [Test]
        public void Test_AniDbAnimeTag()
        {
            var row = db.AniDbAnimeTag.FirstOrDefault();
            Assert.IsNotNull(row, "Query responded with no results");

        }

        [Test]
        public void Test_AniDbAnimeTitle()
        {
            var row = db.AniDbAnimeTitle.FirstOrDefault();
            Assert.IsNotNull(row, "Query responded with no results");
        }

        [Test]
        public void Test_AniDbCategory()
        {
            var row = db.AniDbCategory.FirstOrDefault();
            Assert.IsNotNull(row, "Query responded with no results");
        }

        [Test]
        public void Test_AniDbCharacter()
        {
            var row = db.AniDbCharacter.FirstOrDefault();
            Assert.IsNotNull(row, "Query responded with no results");
        }

        [Test]
        public void Test_AniDbCharacterSeiyuu()
        {
            var row = db.AniDbCharacterSeiyuu.FirstOrDefault();
            Assert.IsNotNull(row, "Query responded with no results");
        }

        [Test]
        public void Test_AniDbEpisode()
        {
            var row = db.AniDbEpisode.FirstOrDefault();
            Assert.IsNotNull(row, "Query responded with no results");
        }

        [Test]
        public void Test_AniDbEpisodeTitle()
        {
            var row = db.AniDbEpisodeTitle.FirstOrDefault();
            Assert.IsNotNull(row, "Query responded with no results");
        }

        [Test]
        public void Test_AniDbFile()
        {
            var row = db.AniDbFile.FirstOrDefault();
            Assert.IsNotNull(row, "Query responded with no results");
        }

        [Test]
        public void Test_AniDbGroupStatus()
        {
            var row = db.AniDbGroupStatus.FirstOrDefault();
            Assert.IsNotNull(row, "Query responded with no results");
        }

        [Test]
        public void Test_AniDbMylistStatus()
        {
            var row = db.AniDbMylistStatus.FirstOrDefault();
            Assert.IsNotNull(row, "Query responded with no results");
        }

        [Test]
        public void Test_AniDbReccomendation()
        {
            var row = db.AniDbReccomendation.FirstOrDefault();
            Assert.IsNotNull(row, "Query responded with no results");
        }

        [Test]
        public void Test_AniDbSeiyuu()
        {
            var row = db.AniDbSeiuu.FirstOrDefault();
            Assert.IsNotNull(row, "Query responded with no results");
        }

        [Test]
        public void Test_AniDbTag()
        {
            var row = db.AniDbTag.FirstOrDefault();
            Assert.IsNotNull(row, "Query responded with no results");
        }

        [Test]
        public void Test_AniDbVote()
        {
            var row = db.AniDbVote.FirstOrDefault();
            Assert.IsNotNull(row, "Query responded with no results");
        }

        [Test]
        public void Test_AnimeCharacter()
        {
            var row = db.AnimeCharacter.FirstOrDefault();
            Assert.IsNotNull(row, "Query responded with no results");
        }

        [Test]
        public void Test_AnimeEpisode()
        {
            var row = db.AnimeEpisode.FirstOrDefault();
            Assert.IsNotNull(row, "Query responded with no results");
        }

        [Test]
        public void Test_AnimeEpisodeUser()
        {
            var row = db.AnimeEpisodeUser.FirstOrDefault();
            Assert.IsNotNull(row, "Query responded with no results");
        }

        [Test]
        public void Test_AnimeGroup()
        {
            var row = db.AnimeGroup.FirstOrDefault();
            Assert.IsNotNull(row, "Query responded with no results");
        }

        [Test]
        public void Test_AnimeGroupUser()
        {
            var row = db.AnimeGroupUser.FirstOrDefault();
            Assert.IsNotNull(row, "Query responded with no results");
        }

        [Test]
        public void Test_AnimeSeries()
        {
            var row = db.AnimeSeries.FirstOrDefault();
            Assert.IsNotNull(row, "Query responded with no results");
            
        }

        [Test]
        public void AnimeSeriesUser()
        {
            var row = db.AnimeSeriesUser.FirstOrDefault();
            Assert.IsNotNull(row, "Query responded with no results");
            
        }

        [Test]
        public void AnimeStaff()
        {
            var row = db.AnimeStaff.FirstOrDefault();
            Assert.IsNotNull(row, "Query responded with no results");
            
        }

        [Test]
        public void AuthTokens()
        {
            var row = db.AuthTokens.FirstOrDefault();
            Assert.IsNotNull(row, "Query responded with no results");
            
        }

        [Test]
        public void BookmarkedAnime()
        {
            var row = db.BookmarkedAnime.FirstOrDefault();
            Assert.IsNotNull(row, "Query responded with no results");
            
        }

        [Test]
        public void CommandRequest()
        {
            var row = db.CommandRequest.FirstOrDefault();
            Assert.IsNotNull(row, "Query responded with no results");
            
        }

        [Test]
        public void CrossRefAniDBMAL()
        {
            var row = db.CrossRefAniDBMAL.FirstOrDefault();
            Assert.IsNotNull(row, "Query responded with no results");
            
        }

        [Test]
        public void CrossRefAniDBOther()
        {
            var row = db.CrossRefAniDBOther.FirstOrDefault();
            Assert.IsNotNull(row, "Query responded with no results");
            
        }

        [Test]
        public void CrossRefAniDBTrakt()
        {
            var row = db.CrossRefAniDBTrakt.FirstOrDefault();
            Assert.IsNotNull(row, "Query responded with no results");
            
        }

        [Test]
        public void CrossRefAniDbTraktEpisode()
        {
            var row = db.CrossRefAniDbTraktEpisode.FirstOrDefault();
            Assert.IsNotNull(row, "Query responded with no results");
            
        }

        [Test]
        public void CrossRefAniDbTraktV2()
        {
            var row = db.CrossRefAniDbTraktV2.FirstOrDefault();
            Assert.IsNotNull(row, "Query responded with no results");
            
        }

        [Test]
        public void CrossRefAniDBTvDB()
        {
            var row = db.CrossRefAniDBTvDB.FirstOrDefault();
            Assert.IsNotNull(row, "Query responded with no results");
            
        }

        [Test]
        public void CrossRefAniDBTvDBEpisode()
        {
            var row = db.CrossRefAniDBTvDBEpisode.FirstOrDefault();
            Assert.IsNotNull(row, "Query responded with no results");
            
        }

        [Test]
        public void CrossRefAniDBTvDBEpisodeOverride()
        {
            var row = db.CrossRefAniDBTvDBEpisodeOverride.FirstOrDefault();
            Assert.IsNotNull(row, "Query responded with no results");
            
        }

        [Test]
        public void CrossRefAnimeStaff()
        {
            var row = db.CrossRefAnimeStaff.FirstOrDefault();
            Assert.IsNotNull(row, "Query responded with no results");
            
        }

        [Test]
        public void CrossRefCustomTag()
        {
            var row = db.CrossRefCustomTag.FirstOrDefault();
            Assert.IsNotNull(row, "Query responded with no results");
            
        }

        [Test]
        public void CrossRefFileEpisode()
        {
            var row = db.CrossRefFileEpisode.FirstOrDefault();
            Assert.IsNotNull(row, "Query responded with no results");
            
        }

        [Test]
        public void CrossRefLanguagesAniDBFile()
        {
            var row = db.CrossRefLanguagesAniDBFile.FirstOrDefault();
            Assert.IsNotNull(row, "Query responded with no results");
            
        }

        [Test]
        public void CrossRefSubtitlesAniDBFile()
        {
            var row = db.CrossRefSubtitlesAniDBFile.FirstOrDefault();
            Assert.IsNotNull(row, "Query responded with no results");
            
        }

        [Test]
        public void CustomTags()
        {
            var row = db.CustomTag.FirstOrDefault();
            Assert.IsNotNull(row, "Query responded with no results");
            
        }

        [Test]
        public void DuplicateFiles()
        {
            var row = db.DuplicateFile.FirstOrDefault();
            Assert.IsNotNull(row, "Query responded with no results");
            
        }

        [Test]
        public void FileFfdshowPresets()
        {
            var row = db.FileFfdshowPreset.FirstOrDefault();
            Assert.IsNotNull(row, "Query responded with no results");
            
        }

        [Test]
        public void FileNameHashes()
        {
            var row = db.FileNameHash.FirstOrDefault();
            Assert.IsNotNull(row, "Query responded with no results");
            
        }

        [Test]
        public void GroupFilters()
        {
            var row = db.GroupFilter.FirstOrDefault();
            Assert.IsNotNull(row, "Query responded with no results");
            
        }

        [Test]
        public void GroupFilterConditions()
        {
            var row = db.GroupFilterCondition.FirstOrDefault();
            Assert.IsNotNull(row, "Query responded with no results");
            
        }

        [Test]
        public void IgnoreAnimes()
        {
            var row = db.IgnoreAnime.FirstOrDefault();
            Assert.IsNotNull(row, "Query responded with no results");
            
        }

        [Test]
        public void ImportFolder()
        {
            var row = db.ImportFolder.FirstOrDefault();
            Assert.IsNotNull(row, "Query responded with no results");
            
        }

        [Test]
        public void ShokoUsers()
        {
            var row = db.ShokoUsers.FirstOrDefault();
            Assert.IsNotNull(row, "Query responded with no results");
            
        }

        [Test]
        public void Languages()
        {
            var row = db.Language.FirstOrDefault();
            Assert.IsNotNull(row, "Query responded with no results");
            
        }


        [Test]
        public void MovieDBFanarts()
        {
            var row = db.MovieDBFanarts.FirstOrDefault();
            Assert.IsNotNull(row, "Query responded with no results");
            
        }

        [Test]
        public void MovieDBMovie()
        {
            var row = db.MovieDBMovie.FirstOrDefault();
            Assert.IsNotNull(row, "Query responded with no results");
            
        }

        [Test]
        public void MovieDBPoster()
        {
            var row = db.MovieDBPoster.FirstOrDefault();
            Assert.IsNotNull(row, "Query responded with no results");
            
        }

        [Test]
        public void Playlists()
        {
            var row = db.Playlist.FirstOrDefault();
            Assert.IsNotNull(row, "Query responded with no results");
            
        }

        [Test]
        public void RenameScripts()
        {
            var row = db.RenameScript.FirstOrDefault();
            Assert.IsNotNull(row, "Query responded with no results");
            
        }

        [Test]
        public void Scans()
        {
            var row = db.Scan.FirstOrDefault();
            Assert.IsNotNull(row, "Query responded with no results");
            
        }


        [Test]
        public void ScanFiles()
        {
            var row = db.ScanFile.FirstOrDefault();
            Assert.IsNotNull(row, "Query responded with no results");
            
        }

        [Test]
        public void ScheduledUpdates()
        {
            var row = db.ScheduledUpdate.FirstOrDefault();
            Assert.IsNotNull(row, "Query responded with no results");
            
        }

        [Test]
        public void TraktEpisodes()
        {
            var row = db.TraktEpisodes.FirstOrDefault();
            Assert.IsNotNull(row, "Query responded with no results");
            
        }

        [Test]
        public void TraktFriends()
        {
            var row = db.TraktFriends.FirstOrDefault();
            Assert.IsNotNull(row, "Query responded with no results");
            
        }

        [Test]
        public void TraktSeasons()
        {
            var row = db.TraktSeasons.FirstOrDefault();
            Assert.IsNotNull(row, "Query responded with no results");
            
        }

        [Test]
        public void TvDBEpisodes()
        {
            var row = db.TvDBEpisodes.FirstOrDefault();
            Assert.IsNotNull(row, "Query responded with no results");
            
        }

        [Test]
        public void TvDBImageFanarts()
        {
            var row = db.TvDBImageFanarts.FirstOrDefault();
            Assert.IsNotNull(row, "Query responded with no results");
            
        }

        [Test]
        public void TvDBImagePosters()
        {
            var row = db.TvDBImagePosters.FirstOrDefault();
            Assert.IsNotNull(row, "Query responded with no results");
            
        }

        [Test]
        public void TvDBImageWideBanners()
        {
            var row = db.TvDBImageWideBanners.FirstOrDefault();
            Assert.IsNotNull(row, "Query responded with no results");
            
        }

        [Test]
        public void TvDBSeries()
        {
            var row = db.TvDBSeries.FirstOrDefault();
            Assert.IsNotNull(row, "Query responded with no results");
            
        }
    }
}