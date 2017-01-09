using System;
using System.Linq;
using Shoko.Models.Server;
using NHibernate;
using NHibernate.Criterion;
using Shoko.Models.Client;
using Shoko.Server.Collections;
using Shoko.Server.Databases;
using Shoko.Server.Entities;
using Shoko.Server.Repositories.NHibernate;

namespace Shoko.Server.Repositories.Direct
{
    public class AniDB_CharacterRepository : BaseDirectRepository<SVR_AniDB_Character, int>
    {
        private AniDB_CharacterRepository()
        {
            
        }

        public static AniDB_CharacterRepository Create()
        {
            return new AniDB_CharacterRepository();
        }
        public SVR_AniDB_Character GetByCharID(int id)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                return GetByCharID(session.Wrap(), id);
            }
        }

        public SVR_AniDB_Character GetByCharID(ISessionWrapper session, int id)
        {
            SVR_AniDB_Character cr = session
                .CreateCriteria(typeof(SVR_AniDB_Character))
                .Add(Restrictions.Eq("CharID", id))
                .UniqueResult<SVR_AniDB_Character>();
            return cr;
        }

        public ILookup<int, AnimeCharacterAndSeiyuu> GetCharacterAndSeiyuuByAnime(ISessionWrapper session, int[] animeIds)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));
            if (animeIds == null)
                throw new ArgumentNullException(nameof(animeIds));

            if (animeIds.Length == 0)
            {
                return EmptyLookup<int, AnimeCharacterAndSeiyuu>.Instance;
            }

            // The below query makes sure that only one seiyuu is returned for each anime/character combiniation
            var animeChars = session.CreateSQLQuery(@"
                SELECT animeChr.AnimeID, {chr.*}, {seiyuu.*}, animeChr.CharType
                    FROM AniDB_Anime_Character AS animeChr
                        INNER JOIN AniDB_Character AS chr
                            ON chr.CharID = animeChr.CharID
                        LEFT OUTER JOIN (
                            SELECT ac.AnimeID, ac.CharID, MIN(cs.SeiyuuID) AS SeiyuuID
                                FROM AniDB_Anime_Character ac
                                    INNER JOIN AniDB_Character_Seiyuu cs
                                        ON cs.CharID = ac.CharID
                                GROUP BY ac.AnimeID, ac.CharID
                            ) AS chrSeiyuu
                            ON chrSeiyuu.CharID = chr.CharID
                                AND chrSeiyuu.AnimeID = animeChr.AnimeID
                        LEFT OUTER JOIN AniDB_Seiyuu AS seiyuu
                            ON seiyuu.SeiyuuID = chrSeiyuu.SeiyuuID
                    WHERE animeChr.AnimeID IN (:animeIds)")
                .AddScalar("AnimeID", NHibernateUtil.Int32)
                .AddEntity("chr", typeof(SVR_AniDB_Character))
                .AddEntity("seiyuu", typeof(SVR_AniDB_Seiyuu))
                .AddScalar("CharType", NHibernateUtil.String)
                .SetParameterList("animeIds", animeIds)
                .List<object[]>()
                .Select(r => new AnimeCharacterAndSeiyuu((int)r[0], (SVR_AniDB_Character)r[1], (SVR_AniDB_Seiyuu)r[2], (string)r[3]))
                .ToLookup(ac => ac.AnimeID);

            return animeChars;
        }

    }

    public class AnimeCharacterAndSeiyuu
    {
        public AnimeCharacterAndSeiyuu(int animeID, SVR_AniDB_Character character, SVR_AniDB_Seiyuu seiyuu = null, string characterType = null)
        {
            if (character == null)
                throw new ArgumentNullException(nameof(character));

            AnimeID = animeID;
            Character = character;
            Seiyuu = seiyuu;
            CharacterType = characterType ?? String.Empty;
        }

        public CL_AniDB_Character ToClient()
        {
            return Character.ToClient(CharacterType, Seiyuu);
        }

        public int AnimeID { get; private set; }

        public SVR_AniDB_Character Character { get; private set; }

        public SVR_AniDB_Seiyuu Seiyuu { get; private set; }

        public string CharacterType { get; private set; }
    }
}