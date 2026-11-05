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
            var spans = Spans.Where(span => span.OperationName == operation).ToList();
            Assert.Single(spans);
            Assert.Equal(ActivityKind.Client, spans[0].Kind);
            Assert.Equal(operation, spans[0].GetTagItem("db.operation.name"));
            Assert.NotNull(spans[0].GetTagItem("db.system.name"));
            Assert.Equal(ActivityStatusCode.Unset, spans[0].Status);
            Assert.Null(spans[0].GetTagItem("db.query.text"));
            Assert.Single(Durations, duration => Equals(duration["db.operation.name"], operation));
        }

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
        Assert.Contains(recorder.Spans, span => span.OperationName == "INSERT");
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
        Assert.Contains(recorder.Spans, span => span.OperationName == "INSERT");
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
    public void AFailingStatement_ReportsTheErrorsTypeButNotItsMessage()
    {
        using var session = SessionFactory.OpenSession();
        using var recorder = new Recorder();

        Assert.ThrowsAny<Exception>(() => session.CreateSQLQuery("DELETE FROM NoSuchTableForTelemetry").ExecuteUpdate());

        var span = recorder.Spans.Single(span => span.OperationName == "DELETE");
        Assert.Equal(ActivityStatusCode.Error, span.Status);
        Assert.Null(span.StatusDescription);
        Assert.NotNull(span.GetTagItem("error.type"));
        Assert.DoesNotContain(span.Tags, tag => tag.Value?.Contains("NoSuchTableForTelemetry") is true);
        Assert.Contains(recorder.Durations, duration => duration.ContainsKey("error.type"));
    }

    [Fact]
    public void CapturedStatements_HoldTheirParametersButNotTheValues()
    {
        var sessionFactory = SessionFactory;
        using var session = sessionFactory.OpenSession();
        using var recorder = new Recorder();
        const string Value = "telemetry-secret-value";

        DatabaseTelemetry.Enable(captureStatements: true);
        try
        {
            session.CreateQuery("from ScheduledAction where PluginID = :id").SetParameter("id", Value).List<ScheduledAction>();
        }
        finally
        {
            DatabaseTelemetry.Disable();
        }

        var statement = Assert.IsType<string>(recorder.Spans.Single(span => span.OperationName == "SELECT").GetTagItem("db.query.text"));
        Assert.Contains("ScheduledAction", statement);
        Assert.DoesNotContain(Value, statement);
    }

    #endregion
}
