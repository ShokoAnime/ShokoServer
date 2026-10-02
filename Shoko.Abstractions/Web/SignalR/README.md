# Feeds on the Aggregate Hub

The server has one SignalR hub for clients to follow what happens, at
`/signalr/aggregate`, open to any signed-in user. It is split into **feeds**:
`queue`, `file`, `metadata`, `restart` and the rest of the core's, each a
subscription a client joins and leaves on the same connection. A plugin adds
feeds of its own the same way, by registering an `IEventEmitter` in DI, and
its clients need no second connection.

The hub only goes one way. Clients join and leave feeds, and feeds send to
clients; there are no calls from a client into a feed. Anything a client asks
of your plugin goes through your API instead (see
[Routes a plugin serves](../Services/README.md#routes-a-plugin-serves)).

---

## Writing a feed

Derive from `EventEmitter` and name the feed:

```csharp
public class TemplateFeed(ITemplateService templates) : EventEmitter, IDisposable
{
    public override string Name => "template";

    // Sent as "template:connected" to a connection joining the feed.
    protected override object[] GetInitialMessages()
        => [templates.GetStatus()];

    public async Task OnStatusChanged(TemplateStatus status)
        => await SendAsync("status.changed", status);
}
```

and register it from `IPluginServiceRegistration.RegisterServices`:

```csharp
serviceCollection.AddEventEmitter<TemplateFeed>();
```

`AddEventEmitter<T>()` registers the feed as a singleton, both as itself and
as an `IEventEmitter`, so your services send through the same instance the hub
lists.

What the base class does for you:

- **Joining and leaving.** It keeps the feed's connections, per user, and adds
  them to and removes them from the SignalR group named after the feed. A
  connection closing leaves every feed on its own.
- **Refusing users.** Override `CanConnect(IUser)` to keep users out, for
  instance `=> user.IsAdmin` for a feed of administrators. A refused user is
  never added and is sent nothing.
- **Initial messages.** `GetInitialMessages()` is sent as `<name>:connected`
  to each connection that joins, unless it is empty. Override
  `GetInitialMessagesForUser(connectionId, user, lastConnectedAt)` instead for
  a state that depends on the user or the connection; `lastConnectedAt` is
  when the client says it was last connected, so it can be sent only what
  changed since.
- **Sending.** `SendAsync(subject, args)` goes to every connection on the
  feed, and `SendToUserAsync(user, subject, args)` to one user's connections
  on it. Clients receive both as `<name>:<subject>`.
- **Sending to some users.** `SendWhereAsync(predicate, subject, args)` goes
  to the users the predicate accepts, asked once per user on the feed. Use it
  for anything about a series, episode or file, as
  `SendWhereAsync(user => user.IsAllowedToSee(series), …)`, so users kept from
  a series by their restricted tags hear nothing about it.
  `SendPerUserAsync(subject, getArgs)` builds the arguments per user instead,
  for a message listing several entities: return `null` to skip a user, and
  the same array to users who get the same message.
- **Cleaning up.** Override `OnConnectionRemoved(connectionId)` to drop what
  you keep per connection.

The server attaches each feed to the hub before any client can join; a feed
sending before then sends nothing. For sends these methods don't cover,
`HubContext` is the hub's context once attached.

Payloads are serialized with Newtonsoft.Json and the API's contract resolver,
the same as the REST API's.

## Naming a feed

**Name your feeds after your plugin**: `myplugin` for a single feed, or
`myplugin.audit` and `myplugin.notifications` for several. Names are matched
ignoring case. Nothing prefixes the name for you: when two feeds share a name,
the server keeps the one registered first and logs a warning naming both types.

## What a client does

With the JavaScript client, signed in with the API key as usual:

```js
const connection = new HubConnectionBuilder()
    .withUrl("/signalr/aggregate?feeds=template", { accessTokenFactory: () => apiKey })
    .build();
connection.on("template:connected", status => { /* … */ });
connection.on("template:status.changed", status => { /* … */ });
await connection.start();
```

The hub's methods for feeds are `feed.list_all`, `feed.list_joined`,
`feed.join_single(feed, lastConnectedAt?)`, `feed.join_many(feeds, lastConnectedAt?)`,
`feed.leave_single(feed)`, `feed.leave_many(feeds)`,
`feed.replace_all(feeds, lastConnectedAt?)` and `feed.clear_all`. A join
returns `false` when the feed is unknown, already joined, or refused the user.
