module AuctionSite.Tests.AuctionTestFixtures

open NUnit.Framework
open FsUnit
open AuctionSite.Domain
open AuctionSite.Tests.SampleData
open AuctionSite.Tests.AuctionStateTests

// Common test fixture for all auction types
type AuctionTestFixture<'T>(auctionType: AuctionType, stateHandler: IState<'T>) =
    // Create an auction for testing
    let auction = sampleAuctionOfType auctionType
    let emptyState = 
        match Auction.emptyState auction with
        | Choice1Of2 s when auctionType.IsSingleSealedBid() -> s :?> 'T
        | Choice2Of2 s when auctionType.IsTimedAscending() -> s :?> 'T
        | _ -> failwithf "Unexpected state type for %A" auctionType
    
    // Properties
    member _.Auction = auction
    member _.EmptyState = emptyState
    member _.StateHandler = stateHandler
    
    // Common test methods
    member this.CanAddBidToEmptyState() =
        let _, result1 = stateHandler.AddBid bid1 emptyState
        match result1 with
        | Ok () -> ()
        | Error err -> Assert.Fail (string err)

    member this.CanAddSecondBid() =
        let state1, _ = stateHandler.AddBid bid1 emptyState
        let _, result2 = stateHandler.AddBid bid2 state1
        match result2 with
        | Ok () -> ()
        | Error err -> Assert.Fail (string err)
        
    member this.CannotPlaceBidAfterAuctionHasEnded() =
        let state1, _ = stateHandler.AddBid bid1 emptyState
        let stateEnded = stateHandler.Inc sampleEndsAt state1
        
        let _, result = stateHandler.AddBid bid2 stateEnded
        match result with
        | Ok result -> Assert.Fail (string result)
        | Error err -> err |> should equal (AuctionHasEnded sampleAuctionId)
        
    member this.NoWinnerWhenNoBidsPlaced() =
        let stateEndedWithNoBids = stateHandler.Inc sampleEndsAt emptyState
        
        let maybeAmountAndWinner = stateHandler.TryGetAmountAndWinner stateEndedWithNoBids
        maybeAmountAndWinner |> should equal None
        
    member this.RunIncrementStateTests() =
        // Get the increment spec test methods
        let tests = incrementSpec emptyState stateHandler
        
        tests.CanIncrementTwice()
        tests.WontEndJustAfterStart()
        tests.WontEndJustBeforeEnd()
        tests.WontEndJustBeforeStart()
        tests.WillHaveEndedJustAfterEnd()
