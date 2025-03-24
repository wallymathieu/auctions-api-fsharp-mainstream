namespace AuctionSite.Domain

open System
open System.Text.Json
open System.Text.Json.Serialization
open AuctionSite.Money

/// Represents a bid placed in an auction
type Bid = {
    [<JsonPropertyName("auction")>]
    ForAuction: AuctionId
    [<JsonPropertyName("user")>]
    Bidder: User
    [<JsonPropertyName("at")>]
    At: DateTime
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
        bids |> List.sortByDescending (fun bid -> bid.BidAmount.Value)

