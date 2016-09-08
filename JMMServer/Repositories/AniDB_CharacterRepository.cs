﻿using System;
using System.Collections.Generic;
using System.Linq;
using JMMContracts;
using JMMServer.Collections;
using JMMServer.Entities;
using JMMServer.Repositories.NHibernate;
using NHibernate;
using NHibernate.Criterion;

namespace JMMServer.Repositories
{
    public class AniDB_CharacterRepository
    {
        public void Save(AniDB_Character obj)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                // populate the database
                using (var transaction = session.BeginTransaction())
                {
                    session.SaveOrUpdate(obj);
                    transaction.Commit();
                }
            }
        }

        public AniDB_Character GetByID(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return session.Get<AniDB_Character>(id);
            }
        }

        public List<AniDB_Character> GetAll()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var objs = session
                    .CreateCriteria(typeof(AniDB_Character))
                    .List<AniDB_Character>();

                return new List<AniDB_Character>(objs);
            }
        }

        public AniDB_Character GetByCharID(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return GetByCharID(session.Wrap(), id);
            }
        }

        public AniDB_Character GetByCharID(ISessionWrapper session, int id)
        {
            AniDB_Character cr = session
                .CreateCriteria(typeof(AniDB_Character))
                .Add(Restrictions.Eq("CharID", id))
                .UniqueResult<AniDB_Character>();
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
                .AddEntity("chr", typeof(AniDB_Character))
                .AddEntity("seiyuu", typeof(AniDB_Seiyuu))
                .AddScalar("CharType", NHibernateUtil.String)
                .SetParameterList("animeIds", animeIds)
                .List<object[]>()
                .Select(r => new AnimeCharacterAndSeiyuu((int)r[0], (AniDB_Character)r[1], (AniDB_Seiyuu)r[2], (string)r[3]))
                .ToLookup(ac => ac.AnimeID);

            return animeChars;
        }

        public void Delete(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                // populate the database
                using (var transaction = session.BeginTransaction())
                {
                    AniDB_Character cr = GetByID(id);
                    if (cr != null)
                    {
                        session.Delete(cr);
                        transaction.Commit();
                    }
                }
            }
        }
    }

    public class AnimeCharacterAndSeiyuu
    {
        public AnimeCharacterAndSeiyuu(int animeID, AniDB_Character character, AniDB_Seiyuu seiyuu = null, string characterType = null)
        {
            if (character == null)
                throw new ArgumentNullException(nameof(character));

            AnimeID = animeID;
            Character = character;
            Seiyuu = seiyuu;
            CharacterType = characterType ?? String.Empty;
        }

        public Contract_AniDB_Character ToContract()
        {
            return Character.ToContract(CharacterType, Seiyuu);
        }

        public int AnimeID { get; private set; }

        public AniDB_Character Character { get; private set; }

        public AniDB_Seiyuu Seiyuu { get; private set; }

        public string CharacterType { get; private set; }
    }
}