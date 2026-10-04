This is where the new system for AniDB will be held.
This should have test driven development in mind wherever possible.
It's fine to have reused code in here, but reused code should be organized and redocumented for the future.

Actual call handling is kept in the connection handlers: `AniDBUDPConnectionHandler` (`UDP/`) and
`AniDBHttpConnectionHandler` (`HTTP/`), both deriving from `ConnectionHandler`.
Requests are calls to AniDB, with the details of the call explained in each file.
Responses are returned from Requests. Some Responses are generic or are reused by AniDB. A UDP request that returns no
response data uses the `Void` class.

The flow of code through this system should mostly happen as creating a Request through `IRequestFactory.Create<T>()`
(optionally configuring it), calling `SendAsync()`, then examining the Response of the Request. If the response errors
or gives an unexpected response, it will error to be caught and handled by the caller.
