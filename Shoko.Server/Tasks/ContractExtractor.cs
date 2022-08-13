using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NHibernate;
using NHibernate.Criterion;
using NHibernate.Criterion.Lambda;
using Shoko.Models.Client;
using Shoko.Server.Databases;
using Shoko.Server.LZ4;
using Shoko.Server.Models;
using Shoko.Server.Repositories.NHibernate;

namespace Shoko.Server.Tasks
{
    internal class ContractExtractor
    {
        private static Dictionary<string, Action<ISessionWrapper, ZipArchive>> ContractExtractors =
            new Dictionary<string, Action<ISessionWrapper, ZipArchive>>(StringComparer.OrdinalIgnoreCase);

        static ContractExtractor()
        {
            ContractExtractors.Add("AniDB_Anime", (sw, za) =>
            {
                ExtractContracts<SVR_AniDB_Anime, CL_AniDB_AnimeDetailed>(sw, za, pb =>
                {
                    pb.Select(CreateEntryNameProjection<SVR_AniDB_Anime>("AniDB_Anime\\a", a => a.AnimeID));
                    pb.Select(s => s.ContractSize);
                    pb.Select(s => s.ContractBlob);
                    return pb;
                });
            });
            ContractExtractors.Add("AnimeGroup", (sw, za) =>
            {
                ExtractContracts<SVR_AnimeGroup, CL_AnimeGroup_User>(sw, za, pb =>
                {
                    pb.Select(CreateEntryNameProjection<SVR_AnimeGroup>("AnimeGroup\\g", a => a.AnimeGroupID));
                    pb.Select(s => s.ContractSize);
                    pb.Select(s => s.ContractBlob);
                    return pb;
                });
            });
            ContractExtractors.Add("AnimeSeries", (sw, za) =>
            {
                ExtractContracts<SVR_AnimeSeries, CL_AnimeSeries_User>(sw, za, pb =>
                {
                    pb.Select(CreateEntryNameProjection<SVR_AnimeSeries>("AnimeSeries\\g", a => a.AnimeGroupID, "_s",
                        a => a.AnimeSeriesID));
                    pb.Select(s => s.ContractSize);
                    pb.Select(s => s.ContractBlob);
                    return pb;
                });
            });
        }

        private static IProjection CreateEntryNameProjection<T>(string prefix, Expression<Func<T, object>> keyCol)
        {
            return Projections.SqlFunction("concat", NHibernateUtil.String,
                Projections.Constant(prefix),
                Projections.Cast(NHibernateUtil.String, Projections.Property(keyCol)),
                Projections.Constant(".json"));
        }

        private static IProjection CreateEntryNameProjection<T>(string prefix1, Expression<Func<T, object>> keyCol1,
            string prefix2, Expression<Func<T, object>> keyCol2)
        {
            return Projections.SqlFunction("concat", NHibernateUtil.String,
                Projections.Constant(prefix1),
                Projections.Cast(NHibernateUtil.String, Projections.Property(keyCol1)),
                Projections.Constant(prefix2),
                Projections.Cast(NHibernateUtil.String, Projections.Property(keyCol2)),
                Projections.Constant(".json"));
        }

        private static IProjection CreateEntryNameProjection<T>(string prefix1, Expression<Func<T, object>> keyCol1,
            string prefix2, Expression<Func<T, object>> keyCol2,
            string prefix3, Expression<Func<T, object>> keyCol3)
        {
            return Projections.SqlFunction("concat", NHibernateUtil.String,
                Projections.Constant(prefix1),
                Projections.Cast(NHibernateUtil.String, Projections.Property(keyCol1)),
                Projections.Constant(prefix2),
                Projections.Cast(NHibernateUtil.String, Projections.Property(keyCol2)),
                Projections.Constant(prefix3),
                Projections.Cast(NHibernateUtil.String, Projections.Property(keyCol3)),
                Projections.Constant(".json"));
        }

        /// <summary>
        /// Gets the contracts as embedded json files within a zip archive.
        /// </summary>
        /// <param name="entityType">The type of the entity to dump (can be <see cref="string.Empty"/> or <c>null</c> to dump all).</param>
        /// <returns>The zip archive <see cref="System.IO.Stream"/>.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="session"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException">The specified <paramref name="entityType"/> is not valid.</exception>
        public Stream GetContractsAsZipStream(string entityType)
        {
            using (IStatelessSession session = DatabaseFactory.SessionFactory.OpenStatelessSession())
            {
                return GetContractsAsZipStream(session.Wrap(), entityType);
            }
        }

        /// <summary>
        /// Gets the contracts as embedded json files within a zip archive.
        /// </summary>
        /// <param name="session">The NHibernate session.</param>
        /// <param name="entityType">The type of the entity to dump (can be <see cref="string.Empty"/> or <c>null</c> to dump all).</param>
        /// <returns>The zip archive <see cref="System.IO.Stream"/>.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="session"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException">The specified <paramref name="entityType"/> is not valid.</exception>
        public Stream GetContractsAsZipStream(ISessionWrapper session, string entityType)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));

            MemoryStream buffer = new MemoryStream();

            using (ZipArchive zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
            {
                if (string.IsNullOrEmpty(entityType))
                {
                    // When no entityType has been specified, we'll just dump everything
                    foreach (var contractDumpAction in ContractExtractors.Values)
                    {
                        contractDumpAction(session, zip);
                    }
                }
                else
                {

                    if (ContractExtractors.TryGetValue(entityType, out Action<ISessionWrapper, ZipArchive> contractDumpAction))
                    {
                        contractDumpAction(session, zip);
                    }
                    else
                    {
                        throw new ArgumentException("Invalid entityType specified", nameof(entityType));
                    }
                }
            }

            buffer.Position = 0;

            return buffer;
        }

        private static void ExtractContracts<TEntity, TContract>(ISessionWrapper session, ZipArchive zip,
            Func<QueryOverProjectionBuilder<TEntity>, QueryOverProjectionBuilder<TEntity>> fieldBuilder)
            where TEntity : class
            where TContract : class
        {
            var records = session.QueryOver<TEntity>()
                .SelectList(fieldBuilder)
                .List<object[]>();
            var jsonSerializer = new JsonSerializer
            {
                Formatting = Formatting.Indented,
                NullValueHandling = NullValueHandling.Include
            };
            var zipLock = new object();

            // We should be able to get a bit of a perf boost using parallelism. However, since we're locking on
            // writing to the zip archive, using any more than 2 threads would be pointless in this case
            Parallel.ForEach(records, new ParallelOptions {MaxDegreeOfParallelism = 2}, record =>
            {
                string zipEntryName = record[0].ToString();
                int contractLen = (int) record[1];
                byte[] contractBinary = (byte[]) record[2];

                if (contractLen > 0 && contractBinary != null)
                {
                    TContract contract = CompressionHelper.DeserializeObject<TContract>(contractBinary, contractLen);

                    lock (zipLock)
                    {
                        ZipArchiveEntry archiveEntry = zip.CreateEntry(zipEntryName, CompressionLevel.Optimal);

                        using (StreamWriter writer = new StreamWriter(archiveEntry.Open(), Encoding.UTF8))
                        {
                            jsonSerializer.Serialize(writer, contract);
                        }
                    }
                }
            });
        }
    }
}