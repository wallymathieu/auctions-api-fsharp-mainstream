module AuctionSite.Tests.BlindAuctionTests

open NUnit.Framework
open FsUnit
open AuctionSite.Domain
open AuctionSite.Tests.SampleData
open AuctionSite.Tests.AuctionStateTests
// Create a Blind auction for testing
let blindAuction = sampleAuctionOfType (SingleSealedBid Blind)
let emptyBlindAuctionState = Auction.emptyState blindAuction |> function | Choice1Of2 s -> s | _ -> failwith "Expected SingleSealedBid state"
let stateHandler = SingleSealedBid.stateHandler

[<TestFixture>]
type BlindAuctionTests() =
    [<Test>]
    member _.``Can add bid to empty state``() =
        let _, result1 = stateHandler.AddBid bid1 emptyBlindAuctionState
        match result1 with
        | Ok () -> ()
        | Error err -> Assert.Fail (string err)

    [<Test>]
    member _.``Can add second bid``() =
        let state1, _ = stateHandler.AddBid bid1 emptyBlindAuctionState
        let _, result2 = stateHandler.AddBid bid2 state1
        match result2 with
        | Ok () -> ()
        | Error err -> Assert.Fail (string err)

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
        let stateEndedWithNoBids = stateHandler.Inc sampleEndsAt emptyBlindAuctionState
        
        let maybeAmountAndWinner = stateHandler.TryGetAmountAndWinner stateEndedWithNoBids
        maybeAmountAndWinner |> should equal None

    [<Test>]
    member _.``Cannot place bid after auction has ended``() =
        let state1, _ = stateHandler.AddBid bid1 emptyBlindAuctionState
        let stateEnded = stateHandler.Inc sampleEndsAt state1
        
        let _, result = stateHandler.AddBid bid2 stateEnded
        match result with
        | Ok result -> Assert.Fail (string result)
        | Error err -> err |> should equal (AuctionHasEnded sampleAuctionId)

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

[<TestFixture>]
type BlindAuctionStateTests() =
    inherit IncrementSpec<SingleSealedBidState>(emptyBlindAuctionState, stateHandler)
