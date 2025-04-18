namespace AuctionSite.Domain

open System
open AuctionSite.Money

/// Interface that all auction state implementations must follow
type IState<'TState> =
    /// Advance state to the given time
    abstract member Inc: DateTime -> 'TState -> 'TState
    
    /// Add a bid to the state
    abstract member AddBid: Bid -> 'TState -> 'TState * Result<unit, Errors>
    
    /// Get all bids in the state
    abstract member GetBids: 'TState -> Bid list
    
    /// Try to get the winning amount and winner, if the auction has a winner
    abstract member TryGetAmountAndWinner: 'TState -> (Amount * UserId) option
    
    /// Check if the auction has ended
    abstract member HasEnded: 'TState -> bool

/// Module containing functions for working with Choice<'TLeft, 'TRight> state types
module ChoiceState =
    /// Create state wrapper for Choice<'TLeft, 'TRight> where both types implement IState
    let createChoiceState<'TLeft, 'TRight, 'TLeftState, 'TRightState when 'TLeft :> IState<'TLeftState> and 'TRight :> IState<'TRightState>>
        (leftState: 'TLeft) (rightState: 'TRight) =
        
        let inc (time: DateTime) (state: Choice<'TLeftState, 'TRightState>) =
            match state with
            | Choice1Of2 s -> Choice1Of2 (leftState.Inc time s)
            | Choice2Of2 s -> Choice2Of2 (rightState.Inc time s)
            
        let addBid (bid: Bid) (state: Choice<'TLeftState, 'TRightState>) =
            match state with
            | Choice1Of2 s -> 
                let nextState, result = leftState.AddBid bid s
                Choice1Of2 nextState, result
            | Choice2Of2 s ->
                let nextState, result = rightState.AddBid bid s
                Choice2Of2 nextState, result
                
        let getBids (state: Choice<'TLeftState, 'TRightState>) =
            match state with
            | Choice1Of2 s -> leftState.GetBids s
            | Choice2Of2 s -> rightState.GetBids s
            
        let tryGetAmountAndWinner (state: Choice<'TLeftState, 'TRightState>) =
            match state with
            | Choice1Of2 s -> leftState.TryGetAmountAndWinner s
            | Choice2Of2 s -> rightState.TryGetAmountAndWinner s
            
        let hasEnded (state: Choice<'TLeftState, 'TRightState>) =
            match state with
            | Choice1Of2 s -> leftState.HasEnded s
            | Choice2Of2 s -> rightState.HasEnded s
            
        { new IState<Choice<'TLeftState, 'TRightState>> with
            member _.Inc time state = inc time state
            member _.AddBid bid state = addBid bid state
            member _.GetBids state = getBids state
            member _.TryGetAmountAndWinner state = tryGetAmountAndWinner state
            member _.HasEnded state = hasEnded state
        }