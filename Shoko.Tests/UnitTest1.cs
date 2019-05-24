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
            Assert.IsInstanceOf<AnimeSeries>(row);
        }
    }
}