# Web Api

The Web Api is intentionally simplistic.

- Types : Contains the types that are consumed and returned from the API 
- Jwt : The jwt payload decoding (since a front-proxy or api gateway is supposed to decode the JWT)
- App : The Web Api implementation using Giraffe. Endpoints and their implementation.
 
## Limitations

### Concurrency

Concurrent writes to `AppState.Auctions` are serialized with a `SemaphoreSlim(1,1)` so the in-memory state is consistent for a single server instance. This does not handle multi-instance deployments — for that, a database with optimistic concurrency (row versions) or a distributed actor framework would be needed.

### Administration

In a commercial site you would expect there to be back office staff such as support personal that can view information not accessible to regular users.

