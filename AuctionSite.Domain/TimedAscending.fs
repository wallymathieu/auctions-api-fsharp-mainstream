namespace AuctionSite.Domain

open System
open AuctionSite.Money
open AuctionSite.Domain.Patterns

/// Options for TimedAscending (English) auctions
type TimedAscendingOptions = {
    /// Minimum sale price. If the final bid does not reach this price, the item remains unsold.
    ReservePrice: Amount
    
    /// Minimum amount by which the next bid must exceed the current highest bid.
    MinRaise: Amount
    
    /// Time frame after which the auction ends if no new bids are placed.
    TimeFrame: TimeSpan
}

/// State for TimedAscending auctions
type TimedAscendingState =
    /// The auction hasn't started yet
    | AwaitingStart of DateTime * DateTime * TimedAscendingOptions
    /// The auction is in progress
    | OnGoing of Bid list * DateTime * TimedAscendingOptions
    /// The auction has ended
    | HasEnded of Bid list * DateTime * TimedAscendingOptions

/// Functions for working with TimedAscending auctions
module TimedAscending =
    /// Create default options for a TimedAscending auction
    let defaultOptions (currency: Currency) =
        {
            ReservePrice = { Currency = currency; Value = 0L }
            MinRaise = { Currency = currency; Value = 0L }
            TimeFrame = TimeSpan.Zero
        }
        
    /// Create an empty state for a TimedAscending auction
    let emptyState (startsAt: DateTime) (expiry: DateTime) (options: TimedAscendingOptions) : TimedAscendingState =
        AwaitingStart(startsAt, expiry, options)
        
    /// Parse TimedAscendingOptions from string
    let tryParseOptions (s: string) =
        let parts = s.Split('|') |> List.ofArray
        match parts with
        | ["English"; Amount reservePrice; Amount minRaise; Int32 seconds]->
            if reservePrice.Currency = minRaise.Currency then
                Some {
                    ReservePrice = reservePrice
                    MinRaise = minRaise
                    TimeFrame = TimeSpan.FromSeconds(float seconds)
                }
            else None
        | _ -> None
        
    /// Convert TimedAscendingOptions to string
    let optionsToString (options: TimedAscendingOptions) =
        let seconds = int options.TimeFrame.TotalSeconds
        $"English|%s{string options.ReservePrice}|%s{string options.MinRaise}|%d{seconds}"
    
    /// Implementation of the IState interface for TimedAscendingState
    let rec stateHandler =
        { new IState<TimedAscendingState> with
            member _.Inc (now: DateTime) (state: TimedAscendingState) =
                match state with
                | AwaitingStart(start, startingExpiry, opt) ->
                    if now > start then
                        if now < startingExpiry then
                            // Transition from AwaitingStart to OnGoing
                            OnGoing([], startingExpiry, opt)
                        else
                            // Transition directly from AwaitingStart to HasEnded
                            HasEnded([], startingExpiry, opt)
                    else
                        // Stay in AwaitingStart
                        state
                | OnGoing(bids, nextExpiry, opt) ->
                    if now < nextExpiry then
                        // Stay in OnGoing
                        state
                    else
                        // Transition from OnGoing to HasEnded
                        HasEnded(bids, nextExpiry, opt)
                | HasEnded _ ->
                    // Stay in HasEnded
                    state
                    
            member _.AddBid (bid: Bid) (state: TimedAscendingState) =
                let now = bid.At
                let auctionId = bid.ForAuction
                let bidAmount = bid.BidAmount
                
                // Advance state to current time
                let nextState = stateHandler.Inc now state
                
                match nextState with
                | AwaitingStart _ ->
                    nextState, Error(AuctionHasNotStarted auctionId)
                | OnGoing(bids, nextExpiry, opt) ->
                    match bids with
                    | [] ->
                        // First bid - extend expiry if needed
                        let nextExpiry' = max nextExpiry (now.Add(opt.TimeFrame))
                        OnGoing(bid :: bids, nextExpiry', opt), Ok()
                    | highestBid :: _ ->
                        // Check if bid is high enough
                        let highestBidAmount = highestBid.BidAmount
                        let nextExpiry' = max nextExpiry (now.Add(opt.TimeFrame))
                        let minRaiseAmount = opt.MinRaise
                        
                        if isGreaterThan bidAmount (add highestBidAmount minRaiseAmount) then
                            // Bid is high enough
                            OnGoing(bid :: bids, nextExpiry', opt), Ok()
                        else
                            // Bid is too low
                            nextState, Error(MustPlaceBidOverHighestBid highestBidAmount)
                | HasEnded _ ->
                    nextState, Error(AuctionHasEnded auctionId)
                    
            member _.GetBids (state: TimedAscendingState) =
                match state with
                | OnGoing(bids, _, _) -> bids
                | HasEnded(bids, _, _) -> bids
                | AwaitingStart _ -> []
                    
            member _.TryGetAmountAndWinner (state: TimedAscendingState) =
                match state with
                | HasEnded(bid :: _, _, opt) ->
                    if isGreaterThan bid.BidAmount opt.ReservePrice then
                        Some(bid.BidAmount, bid.Bidder.UserId)
                    else
                        None
                | _ -> None
                        
            member _.HasEnded (state: TimedAscendingState) =
                match state with
                | HasEnded _ -> true
                | _ -> false
        }