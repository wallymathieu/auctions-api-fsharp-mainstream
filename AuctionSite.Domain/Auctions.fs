namespace AuctionSite.Domain

open System
open System.Text.Json.Serialization
open AuctionSite.Money

/// Types of auctions supported in the system
type AuctionType =
    | TimedAscending of TimedAscendingOptions
    | SingleSealedBid of SingleSealedBidOptions
    
    override this.ToString() =
        match this with
        | TimedAscending opts -> TimedAscending.optionsToString opts
        | SingleSealedBid opts -> opts.ToString()
        
    static member TryParse(s: string) =
        match TimedAscending.tryParseOptions s with
        | Some opts -> Some(TimedAscending opts)
        | None -> 
            match SingleSealedBidOptions.TryParse s with
            | Some opts -> Some(SingleSealedBid opts)
            | None -> None

/// <summary>
/// Represents an auction in the system
/// </summary>
type Auction = {
    /// <summary>Unique identifier for the auction</summary>
    [<JsonPropertyName("id")>]
    AuctionId: AuctionId
    
    /// <summary>Date and time when the auction starts</summary>
    [<JsonPropertyName("startsAt")>]
    StartsAt: DateTime
    
    /// <summary>Title of the auction</summary>
    [<JsonPropertyName("title")>]
    Title: string
    
    /// <summary>Initial expiry time for the auction</summary>
    [<JsonPropertyName("expiry")>]
    Expiry: DateTime
    
    /// <summary>User who created the auction</summary>
    [<JsonPropertyName("user")>]
    Seller: User
    
    /// <summary>Type of auction (TimedAscending or SingleSealedBid)</summary>
    [<JsonPropertyName("type")>]
    Type: AuctionType
    
    /// <summary>Currency used for bids in this auction</summary>
    [<JsonPropertyName("currency")>]
    AuctionCurrency: Currency
}

/// Represents the state of an auction - either SingleSealedBid or TimedAscending
type AuctionState = Choice<SingleSealedBidState, TimedAscendingState>

/// Module containing functions for working with auctions
module Auction =
    /// Create a new auction
    let create id startsAt title expiry seller typ currency = {
        AuctionId = id
        StartsAt = startsAt
        Title = title
        Expiry = expiry
        Seller = seller
        Type = typ
        AuctionCurrency = currency
    }
    
    /// Validate a bid for an auction
    let validateBid (bid: Bid) (auction: Auction) : Result<unit, Errors> =
        if bid.Bidder.UserId = auction.Seller.UserId then
            Error(SellerCannotPlaceBids(bid.Bidder.UserId, auction.AuctionId))
        elif bid.BidAmount.Currency <> auction.AuctionCurrency then
            Error(CurrencyConversion auction.AuctionCurrency)
        else
            Ok()
    
    /// Create an empty state for an auction
    let emptyState (auction: Auction) : AuctionState =
        match auction.Type with
        | SingleSealedBid opt -> 
            Choice1Of2(SingleSealedBid.emptyState auction.Expiry opt)
        | TimedAscending opt -> 
            Choice2Of2(TimedAscending.emptyState auction.StartsAt auction.Expiry opt)
            
    /// Create a state handler for an auction
    let createStateHandler () =
        EitherState.createEitherState
            SingleSealedBid.stateHandler
            TimedAscending.stateHandler
