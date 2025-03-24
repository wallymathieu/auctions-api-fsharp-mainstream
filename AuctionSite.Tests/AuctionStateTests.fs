module AuctionSite.Tests.AuctionStateTests

open System
open NUnit.Framework
open FsUnit
open AuctionSite.Domain
open AuctionSite.Money
open AuctionSite.Tests.SampleData

// Base tests for auction states that can be reused in specific auction type tests
let incrementSpec (baseState: 'T) (stateHandler: IState<'T>) =
    
    // Define test methods that can be called from other test classes
    let canIncrementTwice() =
        let s = stateHandler.Inc sampleBidTime baseState
        let s2 = stateHandler.Inc sampleBidTime s
        s |> should equal s2
        
    let wontEndJustAfterStart() =
        let state = stateHandler.Inc (sampleStartsAt.AddSeconds(1.0)) baseState
        stateHandler.HasEnded state |> should equal false
        
    let wontEndJustBeforeEnd() =
        let state = stateHandler.Inc (sampleEndsAt.AddSeconds(-1.0)) baseState
        stateHandler.HasEnded state |> should equal false
        
    let wontEndJustBeforeStart() =
        let state = stateHandler.Inc (sampleStartsAt.AddSeconds(-1.0)) baseState
        stateHandler.HasEnded state |> should equal false
        
    let willHaveEndedJustAfterEnd() =
        let state = stateHandler.Inc (sampleEndsAt.AddSeconds(1.0)) baseState
        stateHandler.HasEnded state |> should equal true
    
    // Return the test methods so they can be used by test fixtures
    {| 
        CanIncrementTwice = canIncrementTwice
        WontEndJustAfterStart = wontEndJustAfterStart
        WontEndJustBeforeEnd = wontEndJustBeforeEnd
        WontEndJustBeforeStart = wontEndJustBeforeStart
        WillHaveEndedJustAfterEnd = willHaveEndedJustAfterEnd
    |}