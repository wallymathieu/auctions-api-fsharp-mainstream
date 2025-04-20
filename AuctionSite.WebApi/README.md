# Web Api

The Web Api is intentionally simplistic.

- Types : Contains the types that are consumed and returned from the API 
- Jwt : The jwt payload decoding (since a front-proxy or api gateway is supposed to decode the JWT)
- App : The Web Api implementation using Giraffe. Endpoints and their implementation.
 
## Limitations

### Concurrency

We are not dealing with what could happen if you send bids at the same time. How persistence is dealt with is overly simplistic. This could of course (as we often see) be the case even for production apps.

What happens when multiple bidders send the same bid at the same time? Just looking at the WebApi part you note that [AppState.Auctions](./AuctionSite.WebApi/App.fs#L14) is not thread safe.

In a real world case, you could have Terms of service with explicit limitations in order to limit the software complexity. You could also design user interface to not expect an immediate response.

### Administration

In a commercial site you would expect there to be back office staff such as support personal that can view information not accessible to regular users.

