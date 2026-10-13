# Shoko.QueueProcessor

A lightweight, persistent, attribute-driven background job queue for .NET 10. It is the scheduler [Shoko Server](https://shokoanime.com) runs on, packaged so plugins and other applications can use it too.

- **Persistent**: jobs survive restarts, stored through EF Core on SQLite, MySQL or SQL Server.
- **DI-native**: jobs are resolved from the service provider, with constructor injection.
- **Attribute-driven**: concurrency limits, mutual exclusion, retries and dispatch conditions are declared on the job type.
- **Deduplicating**: queuing a job that is already waiting or running does nothing.

## Getting started

```
dotnet add package Shoko.QueueProcessor
```

A Shoko plugin references it without its runtime assets, since the server supplies the assembly:

```xml
<PackageReference Include="Shoko.QueueProcessor" Version="..." ExcludeAssets="runtime" />
```

Define a job by implementing `IQueueJob`, register the assembly's jobs, and queue through `IQueueScheduler`.

## Documentation

- [Full guide](https://github.com/ShokoAnime/ShokoServer/blob/master/Shoko.QueueProcessor/README.md): setup, job chains, concurrency and acquisition filters, retries
- [Issues](https://github.com/ShokoAnime/ShokoServer/issues) and [Discord](https://discord.gg/vpeHDsg)
