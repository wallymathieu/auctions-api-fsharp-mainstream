module AuctionSite.Tests.BlindAuctionTests

open NUnit.Framework
open FsUnit
open AuctionSite.Domain
open AuctionSite.Tests.SampleData
open AuctionSite.Tests.AuctionStateTests
open AuctionSite.Tests.AuctionTestFixtures

[<TestFixture>]
type BlindAuctionTests() =
    // Create a test fixture for Blind auction
    let fixture = AuctionTestFixture<SingleSealedBidState>(SingleSealedBid Blind, SingleSealedBid.stateHandler)
    let stateHandler = fixture.StateHandler
    let emptyBlindAuctionState = fixture.EmptyState

    [<Test>]
    member _.``Can add bid to empty state``() =
        fixture.CanAddBidToEmptyState()

    [<Test>]
    member _.``Can add second bid``() =
        fixture.CanAddSecondBid()

    [<Test>]
    member _.``Can end auction``() =
        let state1, _ = stateHandler.AddBid bid1 emptyBlindAuctionState
        let state2, _ = stateHandler.AddBid bid2 state1
        let stateEndedAfterTwoBids = stateHandler.Inc sampleEndsAt state2
        
        match stateEndedAfterTwoBids with
        | DisclosingBids(bids, expiry, opts) ->
            bids.Length |> should equal 2
            expiry |> should equal sampleEndsAt
            opts |> should equal Blind
        | _ -> Assert.Fail("Expected DisclosingBids state")

    [<Test>]
    member _.``Get winner and price from an ended auction - winner pays their own bid``() =
        // In Blind auction, the highest bidder wins and pays their own bid amount
        let state1, _ = stateHandler.AddBid bid1 emptyBlindAuctionState
        let state2, _ = stateHandler.AddBid bid2 state1  // bid2 is higher than bid1
        let stateEndedAfterTwoBids = stateHandler.Inc sampleEndsAt state2
        
        let maybeAmountAndWinner = stateHandler.TryGetAmountAndWinner stateEndedAfterTwoBids
        // Winner should be buyer2 (who placed bid2) and they pay their own bid amount
        maybeAmountAndWinner |> should equal (Some(bidAmount2, buyer2.UserId))

    [<Test>]
    member _.``Get winner and price from an ended auction with single bid``() =
        let state1, _ = stateHandler.AddBid bid1 emptyBlindAuctionState
        let stateEndedAfterOneBid = stateHandler.Inc sampleEndsAt state1
        
        let maybeAmountAndWinner = stateHandler.TryGetAmountAndWinner stateEndedAfterOneBid
        maybeAmountAndWinner |> should equal (Some(bidAmount1, buyer1.UserId))

    [<Test>]
    member _.``No winner when no bids placed``() =
        fixture.NoWinnerWhenNoBidsPlaced()

    [<Test>]
    member _.``Cannot place bid after auction has ended``() =
        fixture.CannotPlaceBidAfterAuctionHasEnded()

    [<Test>]
    member _.``Increment state tests``() =
        fixture.RunIncrementStateTests()

    [<Test>]
    member _.``Bids are sorted by amount in descending order when ended``() =
        // Create bids with different amounts
        let lowBid = { bid1 with BidAmount = sek 5L }
        let midBid = { bid2 with BidAmount = sek 10L }
        let highBid = { 
            ForAuction = sampleAuctionId
            Bidder = buyer3
            At = sampleStartsAt.AddSeconds(3.0)
            BidAmount = sek 15L 
        }
        
        // Add bids in random order
        let state1, _ = stateHandler.AddBid midBid emptyBlindAuctionState
        let state2, _ = stateHandler.AddBid lowBid state1
        let state3, _ = stateHandler.AddBid highBid state2
        
        // End the auction
        let endedState = stateHandler.Inc sampleEndsAt state3
        
        // Check that bids are sorted by amount (highest first)
        match endedState with
        | DisclosingBids(bids, _, _) ->
            bids.Length |> should equal 3
            bids[0].BidAmount |> should equal (sek 15L)
            bids[1].BidAmount |> should equal (sek 10L)
            bids[2].BidAmount |> should equal (sek 5L)
        | _ -> Assert.Fail("Expected DisclosingBids state")
