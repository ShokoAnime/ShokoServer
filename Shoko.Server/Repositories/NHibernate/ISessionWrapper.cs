using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using NHibernate;

namespace Shoko.Server.Repositories.NHibernate;

public interface ISessionWrapper : IDisposable
{
    ICriteria CreateCriteria<T>() where T : class;

    ICriteria CreateCriteria(Type type);

    IQuery CreateQuery(string query);

    ISQLQuery CreateSQLQuery(string query);

    IQueryOver<T, T> QueryOver<T>() where T : class;
    IQueryable<T> Query<T>() where T : class;

    TObj Get<TObj>(object id);
    Task<TObj> GetAsync<TObj>(object id);

    ITransaction BeginTransaction();

    void Insert(object entity);

    void Update(object entity);

    void Delete(object entity);

    Task InsertAsync(object entity);

    Task UpdateAsync(object entity);

    Task DeleteAsync(object entity);

    IDbConnection Connection { get; }
}
