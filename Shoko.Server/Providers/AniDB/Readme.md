This is where the new system for AniDB will be held.
This should have test driven development in mind wherever possible.
It's fine to have reused code in here, but reused code should be organized and redocumented for the future.

Actual call handling is kept in AniDBConnectionHandler.cs.
Requests are calls to AniDB, with the details of the call explained in each file.
Responses are returned from Requests. Some Responses are generic or are reused by AniDB. Currently, no response data is given as a Void class.

The flow of code through this system should mostly happen as instantiating a Request, calling Init(), calling Execute, then examining the Response of the Request. If the response errors or gives an unexpected response, it will error to be caught and handled by the caller.
