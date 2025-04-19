module AuctionSite.Tests.AuctionStateTests

open FsUnit
open AuctionSite.Domain
open AuctionSite.Tests.SampleData
open NUnit.Framework

// Base tests for auction states that can be reused in specific auction type tests
type IncrementSpec<'T when 'T : equality> (baseState: 'T, stateHandler: IState<'T>) =
    
    [<Test>]
    member _.canIncrementTwice() =
        let s = stateHandler.Inc sampleBidTime baseState
        let s2 = stateHandler.Inc sampleBidTime s
        s |> should equal s2
        
    [<Test>]
    member _.wontEndJustAfterStart() =
        let state = stateHandler.Inc (sampleStartsAt.AddSeconds(1.0)) baseState
        stateHandler.HasEnded state |> should equal false
        
    [<Test>]
    member _.wontEndJustBeforeEnd() =
        let state = stateHandler.Inc (sampleEndsAt.AddSeconds(-1.0)) baseState
        stateHandler.HasEnded state |> should equal false
        
    [<Test>]
    member _.wontEndJustBeforeStart() =
        let state = stateHandler.Inc (sampleStartsAt.AddSeconds(-1.0)) baseState
        stateHandler.HasEnded state |> should equal false
        
    [<Test>]
    member _.willHaveEndedJustAfterEnd() =
        let state = stateHandler.Inc (sampleEndsAt.AddSeconds(1.0)) baseState
        stateHandler.HasEnded state |> should equal true
