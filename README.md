# Auction Site F#

A F# implementation of an auction system that supports different types of auctions.

## Project Structure

The project is structured into several F# projects:

- **AuctionSite.Domain**: Core domain logic with auction types and business rules
- **AuctionSite.Persistence**: Data storage using JSON files
- **AuctionSite.WebApi**: Web API using Giraffe
- **AuctionSite.Tests**: Tests for the application

## Auction Types

The system supports the following auction types:

1. **TimedAscending (English)**: An open ascending-price auction where participants place increasingly higher bids, with the auction ending at a predetermined expiry time or after a period of inactivity.

2. **SingleSealedBid**:
   - **Blind**: A sealed first-price auction where the highest bidder pays the price they submitted.
   - **Vickrey**: A sealed second-price auction where the highest bidder pays the second-highest bid amount.

## Getting Started

### Prerequisites

- .NET SDK 9.0 or higher

### Building the Project

```bash
dotnet build
```

### Running the Tests

```bash
dotnet test
```

### Running the Web API

```bash
dotnet run --project AuctionSite.WebApi
```

The server will start on http://localhost:8080 with the following endpoints:

- `GET /auctions` - Get all auctions
- `GET /auction/{id}` - Get auction by ID
- `POST /auction` - Create a new auction (requires authentication)
- `POST /auction/{id}/bid` - Place a bid on an auction (requires authentication)

## Authentication

Authentication uses JWT tokens in the `x-jwt-payload` header. The payload should be Base64 encoded and contain the following structure:

```json
{
  "sub": "user-id",
  "name": "User Name",
  "u_typ": "0"  // 0 for BuyerOrSeller, 1 for Support
}
```

Note that the x-jwt-payload header is a decoded JWT and not an actual JWT, since this app is supposed to be deployed behind a front-proxy or api gateway.

## Technical Decisions

- **Functional Design**: The implementation follows a functional approach using F# discriminated unions and immutable data structures.
- **Railway-Oriented Programming**: Error handling uses the Result type with a custom computation expression for elegant error handling pipelines.
- **Web Framework**: Giraffe is used as a functional-first web framework built on ASP.NET Core.
- **Serialization**: System.Text.Json is used for JSON serialization.
- **Testing**: NUnit and FsUnit are used for testing.

## Original Implementation

This project is a F# port of a Haskell implementation of an auction system.
