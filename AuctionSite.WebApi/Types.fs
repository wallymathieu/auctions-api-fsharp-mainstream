namespace AuctionSite.WebApi

open System
open System.Text.Json.Serialization
open AuctionSite.Money
open AuctionSite.Domain

/// Error message for API responses
type ApiError = {
    Message: string
}

/// Request for placing a bid
type BidRequest = {
    Amount: int64
}

/// Request for adding an auction
type AddAuctionRequest = {
    [<JsonPropertyName("id")>]
    Id: AuctionId Option
    [<JsonPropertyName("startsAt")>]
    StartsAt: DateTime
    [<JsonPropertyName("title")>]
    Title: string
    [<JsonPropertyName("endsAt")>]
    EndsAt: DateTime
    [<JsonPropertyName("currency")>]
    Currency: Currency option
    [<JsonPropertyName("type")>]
    Type: string option  // This will be parsed into AuctionType
}

/// Response for an auction bid
type AuctionBidResponse = {
    Amount: string
    Bidder: string
}

/// Response for a single auction in the list
type AuctionListItemResponse = {
    Id: AuctionId
    StartsAt: DateTime
    Title: string
    Expiry: DateTime
    Currency: Currency
}

/// Response with auctions list
type AuctionsResponse = {
    Auctions: AuctionListItemResponse list
}

/// Detailed auction response with bids
type AuctionDetailResponse = {
    Id: AuctionId
    StartsAt: DateTime
    Title: string
    Expiry: DateTime
    Currency: Currency
    Bids: AuctionBidResponse list
    Winner: string option
    WinnerPrice: Amount option
}

/// Convert from domain models to API responses
module ResponseConverters =
    /// Convert a bid to an API response
    let toAuctionBidResponse (bid: Bid) : AuctionBidResponse = {
        Amount = string bid.BidAmount
        Bidder = bid.Bidder.ToString()
    }
    
    /// Convert an auction to a list item response
    let toAuctionListItemResponse (auction: Auction) : AuctionListItemResponse = {
        Id = auction.AuctionId
        StartsAt = auction.StartsAt
        Title = auction.Title
        Expiry = auction.Expiry
        Currency = auction.AuctionCurrency
    }
    
    /// Convert an auction and its state to a detailed response
    let toAuctionDetailResponse (auction: Auction) (state: AuctionState) : AuctionDetailResponse =
        let stateHandler = Auction.createStateHandler()
        let bids = stateHandler.GetBids state
        let winnerInfo = stateHandler.TryGetAmountAndWinner state
        
        {
            Id = auction.AuctionId
            StartsAt = auction.StartsAt
            Title = auction.Title
            Expiry = auction.Expiry
            Currency = auction.AuctionCurrency
            Bids = bids |> List.map toAuctionBidResponse
            Winner = winnerInfo |> Option.map snd
            WinnerPrice = winnerInfo |> Option.map fst
        }
    
/// Convert from API requests to domain models
module RequestConverters =
    /// Convert a bid request to a domain bid
    let toBid (req: BidRequest) (auctionId: AuctionId) (bidder: User) (time: DateTime) (currency: Currency) : Bid = {
        ForAuction = auctionId
        Bidder = bidder
        At = time
        BidAmount = createAmount currency req.Amount
    }
    
    /// Convert an auction request to a domain auction
    let toAuction (req: AddAuctionRequest) nextId (seller: User) : Auction =
        let currency = defaultArg req.Currency Currency.VAC
        let auctionType = 
            match req.Type with
            | Some typeStr ->
                match AuctionType.TryParse typeStr with
                | Some t -> t
                | None -> TimedAscending (TimedAscending.defaultOptions currency)
            | None -> 
                TimedAscending (TimedAscending.defaultOptions currency)
                
        {
            AuctionId = Option.defaultWith nextId req.Id
            StartsAt = req.StartsAt
            Title = req.Title
            Expiry = req.EndsAt
            Seller = seller
            Type = auctionType
            AuctionCurrency = currency
        }