using System;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Server.Repositories;
using Shoko.Server.Server;
using Shoko.Server.Settings;
using Xunit;

namespace Shoko.Tests;

public class RepositoryLockingTests
{
    [Fact]
    public async Task ReadLock_AllowsConcurrentReaders()
    {
        using var settingsScope = new SettingsScope(CreateServerSettings());
        using var firstEntered = new ManualResetEventSlim(false);
        using var secondEntered = new ManualResetEventSlim(false);
        using var release = new ManualResetEventSlim(false);

        var first = Task.Run(() =>
        {
            BaseRepository.ReadLock(() =>
            {
                firstEntered.Set();
                Assert.True(release.Wait(TimeSpan.FromSeconds(5)));
            });
        });

        Assert.True(firstEntered.Wait(TimeSpan.FromSeconds(5)));

        var second = Task.Run(() => BaseRepository.ReadLock(() => secondEntered.Set()));

        Assert.True(secondEntered.Wait(TimeSpan.FromSeconds(5)));
        release.Set();

        await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task WriteLock_SerializesReadersAndWriters()
    {
        using var settingsScope = new SettingsScope(CreateServerSettings());
        using var writerEntered = new ManualResetEventSlim(false);
        using var releaseWriter = new ManualResetEventSlim(false);
        using var readerEntered = new ManualResetEventSlim(false);
        using var secondWriterEntered = new ManualResetEventSlim(false);

        var writer = Task.Run(() =>
        {
            BaseRepository.WriteLock(() =>
            {
                writerEntered.Set();
                Assert.True(releaseWriter.Wait(TimeSpan.FromSeconds(5)));
            });
        });

        Assert.True(writerEntered.Wait(TimeSpan.FromSeconds(5)));

        var reader = Task.Run(() => BaseRepository.ReadLock(() => readerEntered.Set()));
        var secondWriter = Task.Run(() => BaseRepository.WriteLock(() => secondWriterEntered.Set()));

        Assert.False(readerEntered.Wait(TimeSpan.FromMilliseconds(200)));
        Assert.False(secondWriterEntered.Wait(TimeSpan.FromMilliseconds(200)));

        releaseWriter.Set();

        Assert.True(readerEntered.Wait(TimeSpan.FromSeconds(5)));
        Assert.True(secondWriterEntered.Wait(TimeSpan.FromSeconds(5)));
        await Task.WhenAll(writer, reader, secondWriter).WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task MixedReadWriteLoad_CompletesWithoutDeadlock()
    {
        using var settingsScope = new SettingsScope(CreateServerSettings());
        using var releaseWriter = new ManualResetEventSlim(false);
        using var writerEntered = new ManualResetEventSlim(false);
        using var readerEntered = new ManualResetEventSlim(false);

        var writer = Task.Run(() =>
        {
            BaseRepository.WriteLock(() =>
            {
                writerEntered.Set();
                Assert.True(releaseWriter.Wait(TimeSpan.FromSeconds(5)));
            });
        });

        Assert.True(writerEntered.Wait(TimeSpan.FromSeconds(5)));

        var reader = Task.Run(() => BaseRepository.ReadLock(() => readerEntered.Set()));
        var secondWriter = Task.Run(() => BaseRepository.WriteLock(() => { }));

        Assert.False(readerEntered.Wait(TimeSpan.FromMilliseconds(200)));

        releaseWriter.Set();

        Assert.True(readerEntered.Wait(TimeSpan.FromSeconds(5)));
        await Task.WhenAll(writer, reader, secondWriter).WaitAsync(TimeSpan.FromSeconds(5));
    }

    private static ServerSettings CreateServerSettings()
    {
        return new ServerSettings
        {
            Database =
            {
                UseDatabaseLock = true,
                Type = Constants.DatabaseType.SQLite,
            },
        };
    }

    private sealed class SettingsScope : IDisposable
    {
        private readonly ISettingsProvider? _previous;

        public SettingsScope(IServerSettings settings)
        {
            try { _previous = ISettingsProvider.Instance; }
            catch (InvalidOperationException) { _previous = null; }
            ISettingsProvider.Instance = new TestSettingsProvider(settings);
        }

        public void Dispose()
        {
            ISettingsProvider.Instance = _previous!;
        }
    }

    private sealed class TestSettingsProvider : ISettingsProvider
    {
        private readonly IServerSettings _settings;

        public TestSettingsProvider(IServerSettings settings)
        {
            _settings = settings;
        }

        public IServerSettings GetSettings(bool copy = false)
        {
            return _settings;
        }

        public void SaveSettings(IServerSettings settings)
        {
        }

        public void SaveSettings()
        {
        }

        public void DebugSettingsToLog()
        {
        }
    }
}
