using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NHibernate;
using Shoko.Server.Databases;
using Shoko.Server.Models.Internal;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Telemetry.Database;
using Xunit;

namespace Shoko.IntegrationTests;

/// <summary>
/// Runs every way NHibernate reaches the database through the tracing batcher, on each backend. Its re-implemented interface
/// is the kind of thing only a real session can prove. The server's own session factory keeps NHibernate's batcher, so
/// whatever is recorded here came from the factory these tests build.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class DatabaseTelemetryTests(DatabaseMigrationFixture fixture)
{
    #region Helpers

    private static ISessionFactory? _sessionFactory;

    private ISessionFactory SessionFactory
    {
        get
        {
            Assert.True(fixture.Success, fixture.FailureMessage);
            if (_sessionFactory is not null)
                return _sessionFactory;

            DatabaseTelemetry.Enable(captureStatements: false);
            try
            {
                return _sessionFactory = fixture.Services.GetRequiredService<DatabaseFactory>().Instance!.CreateSessionFactory();
            }
            finally
            {
                DatabaseTelemetry.Disable();
            }
        }
    }

    private static ScheduledAction NewAction()
        => new() { ActionID = Guid.NewGuid(), CreatedAt = new DateTime(2026, 11, 5, 4, 0, 0, DateTimeKind.Utc) };

    private sealed class Recorder : IDisposable
    {
        private readonly ActivityListener _spanListener;

        private readonly MeterListener _meterListener = new();

        public ConcurrentQueue<Activity> Spans { get; } = new();

        public ConcurrentQueue<Dictionary<string, object?>> Durations { get; } = new();

        public Recorder()
        {
            _spanListener = new()
            {
                ShouldListenTo = source => source.Name == DatabaseTelemetry.Name,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStopped = Spans.Enqueue,
            };
            ActivitySource.AddActivityListener(_spanListener);

            _meterListener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == DatabaseTelemetry.Name)
                    listener.EnableMeasurementEvents(instrument);
            };
            _meterListener.SetMeasurementEventCallback<double>((_, _, tags, _) => Durations.Enqueue(tags.ToArray().ToDictionary(tag => tag.Key, tag => tag.Value)));
            _meterListener.Start();
        }

        public void AssertOneNew(string operation)
        {
            var spans = Named(operation);
            Assert.Single(spans);
            Assert.Equal($"{operation} ScheduledAction", spans[0].OperationName);
            Assert.Equal("ScheduledAction", spans[0].GetTagItem("db.collection.name"));
            Assert.NotNull(spans[0].GetTagItem("db.namespace"));
            Assert.Equal(ActivityKind.Client, spans[0].Kind);
            Assert.Equal(operation, spans[0].GetTagItem("db.operation.name"));
            Assert.NotNull(spans[0].GetTagItem("db.system.name"));
            Assert.Equal(ActivityStatusCode.Unset, spans[0].Status);
            Assert.Null(spans[0].GetTagItem("db.query.text"));
            Assert.Single(Durations, duration => Equals(duration["db.operation.name"], operation));
        }

        public List<Activity> Named(string operation)
            => Spans.Where(span => Equals(span.GetTagItem("db.operation.name"), operation)).ToList();

        public void Clear()
        {
            Spans.Clear();
            Durations.Clear();
        }

        public void Dispose()
        {
            _spanListener.Dispose();
            _meterListener.Dispose();
        }
    }

    #endregion

    #region Tests

    [Fact]
    public void EveryPathOfASession_IsReported()
    {
        using var session = SessionFactory.OpenSession();
        using var recorder = new Recorder();
        var action = NewAction();

        using var transaction = session.BeginTransaction();

        // An identity insert runs right away.
        session.Save(action);
        Assert.NotEmpty(recorder.Named("INSERT"));
        recorder.Clear();

        // A reader, going through the query's loader.
        session.Evict(action);
        action = session.Get<ScheduledAction>(action.ScheduledActionID);
        recorder.AssertOneNew("SELECT");
        recorder.Clear();

        // The persister's way, through AddToBatch.
        action.LastRunAt = action.CreatedAt;
        session.Flush();
        recorder.AssertOneNew("UPDATE");
        recorder.Clear();

        // A bulk statement, through ExecuteNonQuery on the interface.
        var deleted = session.CreateQuery("delete ScheduledAction where ActionID = :id").SetParameter("id", action.ActionID).ExecuteUpdate();
        Assert.Equal(1, deleted);
        recorder.AssertOneNew("DELETE");
        session.Evict(action);

        transaction.Rollback();
    }

    [Fact]
    public async Task EveryAsynchronousPathOfASession_IsReported()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var session = SessionFactory.OpenSession();
        using var recorder = new Recorder();
        var action = NewAction();

        using var transaction = session.BeginTransaction();

        await session.SaveAsync(action, cancellationToken);
        Assert.NotEmpty(recorder.Named("INSERT"));
        recorder.Clear();

        await session.EvictAsync(action, cancellationToken);
        action = await session.GetAsync<ScheduledAction>(action.ScheduledActionID, cancellationToken);
        recorder.AssertOneNew("SELECT");
        recorder.Clear();

        action.LastRunAt = action.CreatedAt;
        await session.FlushAsync(cancellationToken);
        recorder.AssertOneNew("UPDATE");
        recorder.Clear();

        var deleted = await session.CreateQuery("delete ScheduledAction where ActionID = :id").SetParameter("id", action.ActionID).ExecuteUpdateAsync(cancellationToken);
        Assert.Equal(1, deleted);
        recorder.AssertOneNew("DELETE");
        await session.EvictAsync(action, cancellationToken);

        await transaction.RollbackAsync(cancellationToken);
    }

    [Fact]
    public void ARepository_ReportsWhatItDoesAroundTheStatements()
    {
        Assert.True(fixture.Success, fixture.FailureMessage);
        var repository = fixture.Services.GetRequiredService<ScheduledActionRepository>();
        var action = NewAction();
        var spans = new ConcurrentQueue<Activity>();
        var thread = Environment.CurrentManagedThreadId;
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == DatabaseTelemetry.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = span =>
            {
                if (Environment.CurrentManagedThreadId == thread)
                    spans.Enqueue(span);
            },
        };
        ActivitySource.AddActivityListener(listener);

        DatabaseTelemetry.Enable(captureStatements: false);
        try
        {
            repository.Save(action);
            repository.GetByActionID(action.ActionID);
            repository.Delete(action);
            repository.Populate(displayName: false, cancellationToken: TestContext.Current.CancellationToken);
        }
        finally
        {
            DatabaseTelemetry.Disable();
        }

        // Reads come from the cache, so nothing is reported for them.
        Assert.Equal(["repository.save ScheduledAction", "repository.delete ScheduledAction", "repository.populate ScheduledAction"], spans.Select(span => span.OperationName));
        Assert.Equal(1, spans.First().GetTagItem("shoko.repository.count"));
        Assert.All(spans, span => Assert.Equal("ScheduledAction", span.GetTagItem("shoko.repository.entity")));
        Assert.IsType<int>(spans.Last().GetTagItem("shoko.repository.count"));
    }

    [Fact]
    public void AFailingStatement_ReportsTheErrorsTypeButNotItsMessage()
    {
        using var session = SessionFactory.OpenSession();
        using var recorder = new Recorder();

        Assert.ThrowsAny<Exception>(() => session.CreateSQLQuery("DELETE FROM NoSuchTableForTelemetry").ExecuteUpdate());

        var span = Assert.Single(recorder.Named("DELETE"));
        Assert.Equal(ActivityStatusCode.Error, span.Status);
        Assert.Null(span.StatusDescription);
        Assert.Empty(span.Events);
        Assert.NotNull(span.GetTagItem("error.type"));
        Assert.Equal("NoSuchTableForTelemetry", span.GetTagItem("db.collection.name"));
        Assert.Contains(recorder.Durations, duration => duration.ContainsKey("error.type"));
    }

    [Fact]
    public void CapturedStatements_HoldTheirParametersButNotTheValues()
    {
        var sessionFactory = SessionFactory;
        using var session = sessionFactory.OpenSession();
        using var recorder = new Recorder();
        var value = Guid.NewGuid();

        DatabaseTelemetry.Enable(captureStatements: true);
        try
        {
            session.CreateQuery("from ScheduledAction where ActionID = :id").SetParameter("id", value).List<ScheduledAction>();
        }
        finally
        {
            DatabaseTelemetry.Disable();
        }

        var statement = Assert.IsType<string>(Assert.Single(recorder.Named("SELECT")).GetTagItem("db.query.text"));
        Assert.Contains("ScheduledAction", statement);
        Assert.DoesNotContain(value.ToString(), statement, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ActionID", statement);

        // Written into the statement by hand rather than as a parameter, as schema steps and fixes do.
        recorder.Clear();
        DatabaseTelemetry.Enable(captureStatements: true);
        try
        {
            session.CreateSQLQuery($"SELECT * FROM ScheduledAction WHERE PluginID = '{value}'").List();
        }
        finally
        {
            DatabaseTelemetry.Disable();
        }

        Assert.Equal("SELECT * FROM ScheduledAction WHERE PluginID = ?", Assert.Single(recorder.Named("SELECT")).GetTagItem("db.query.text"));
    }

    #endregion
}
