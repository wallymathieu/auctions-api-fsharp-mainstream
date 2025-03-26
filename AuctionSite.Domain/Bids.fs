namespace AuctionSite.Domain

open System
open System.Text.Json.Serialization
open AuctionSite.Money

/// <summary>
/// Represents a bid placed in an auction
/// </summary>
type Bid = {
    /// <summary>The ID of the auction this bid is for</summary>
    [<JsonPropertyName("auction")>]
    ForAuction: AuctionId
    
    /// <summary>The user who placed the bid</summary>
    [<JsonPropertyName("user")>]
    Bidder: User
    
    /// <summary>The timestamp when the bid was placed</summary>
    [<JsonPropertyName("at")>]
    At: DateTime
    
    /// <summary>The amount of the bid</summary>
    [<JsonPropertyName("amount")>]
    BidAmount: Amount
}

module Bid =
    /// Creates a new bid
    let create auctionId bidder time amount = {
        ForAuction = auctionId
        Bidder = bidder
        At = time
        BidAmount = amount
    }
    
    /// Sort bids by amount in descending order
    let sortByAmountDescending bids =
        bids |> List.sortByDescending _.BidAmount.Value

