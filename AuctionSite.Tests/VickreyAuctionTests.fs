module AuctionSite.Tests.VickreyAuctionTests

open NUnit.Framework
open FsUnit
open AuctionSite.Domain
open AuctionSite.Tests.SampleData
open AuctionSite.Tests.AuctionStateTests
// Create a Vickrey auction for testing
let vickreyAuction = sampleAuctionOfType (SingleSealedBid Vickrey)
let emptyVickreyAuctionState = Auction.emptyState vickreyAuction |> function | Choice1Of2 s -> s | _ -> failwith "Expected SingleSealedBid state"
let stateHandler = SingleSealedBid.stateHandler

[<TestFixture>]
type VickreyAuctionTests() =

    [<Test>]
    member _.``Can add bid to empty state``() =
        let _, result1 = stateHandler.AddBid bid1 emptyVickreyAuctionState
        match result1 with
        | Ok () -> ()
        | Error err -> Assert.Fail (string err)

    [<Test>]
    member _.``Can add second bid``() =
        let state1, _ = stateHandler.AddBid bid1 emptyVickreyAuctionState
        let _, result2 = stateHandler.AddBid bid2 state1
        match result2 with
        | Ok () -> ()
        | Error err -> Assert.Fail (string err)

    [<Test>]
    member _.``Can end auction``() =
        let state1, _ = stateHandler.AddBid bid1 emptyVickreyAuctionState
        let state2, _ = stateHandler.AddBid bid2 state1
        let stateEndedAfterTwoBids = stateHandler.Inc sampleEndsAt state2
        
        match stateEndedAfterTwoBids with
        | DisclosingBids(bids, expiry, opts) ->
            bids.Length |> should equal 2
            expiry |> should equal sampleEndsAt
            opts |> should equal Vickrey
        | _ -> Assert.Fail("Expected DisclosingBids state")

    [<Test>]
    member _.``Cannot place bid after auction has ended``() =
        let state1, _ = stateHandler.AddBid bid1 emptyVickreyAuctionState
        let stateEnded = stateHandler.Inc sampleEndsAt state1
        
        let _, result = stateHandler.AddBid bid2 stateEnded
        match result with
        | Ok result -> Assert.Fail (string result)
        | Error err -> err |> should equal (AuctionHasEnded sampleAuctionId)

    [<Test>]
    member _.``Get winner and price from an ended auction - winner pays second highest bid``() =
        // In Vickrey auction, the highest bidder wins but pays the second-highest bid amount
        let state1, _ = stateHandler.AddBid bid1 emptyVickreyAuctionState
        let state2, _ = stateHandler.AddBid bid2 state1  // bid2 is higher than bid1
        let stateEndedAfterTwoBids = stateHandler.Inc sampleEndsAt state2
        
        let maybeAmountAndWinner = stateHandler.TryGetAmountAndWinner stateEndedAfterTwoBids
        // Winner should be buyer2 (who placed bid2) but they pay the price of bid1
        maybeAmountAndWinner |> should equal (Some(bidAmount1, buyer2.UserId))

    [<Test>]
    member _.``Get winner and price from an ended auction with single bid``() =
        // When there's only one bid, the bidder pays their own bid amount
        let state1, _ = stateHandler.AddBid bid1 emptyVickreyAuctionState
        let stateEndedAfterOneBid = stateHandler.Inc sampleEndsAt state1
        
        let maybeAmountAndWinner = stateHandler.TryGetAmountAndWinner stateEndedAfterOneBid
        maybeAmountAndWinner |> should equal (Some(bidAmount1, buyer1.UserId))

    [<Test>]
    member _.``No winner when no bids placed``() =
        let stateEndedWithNoBids = stateHandler.Inc sampleEndsAt emptyVickreyAuctionState
        
        let maybeAmountAndWinner = stateHandler.TryGetAmountAndWinner stateEndedWithNoBids
        maybeAmountAndWinner |> should equal None

    [<Test>]
    member _.``Can parse Vickrey options from string``() =
        let sampleTypStr = "Vickrey"
        let parsed = SingleSealedBidOptions.TryParse sampleTypStr
        parsed |> should equal (Some Vickrey)

    [<Test>]
    member _.``Can serialize Vickrey options to string``() =
        let serialized = Vickrey.ToString()
        serialized |> should equal "Vickrey"

    [<Test>]
    member _.``Can parse Blind options from string``() =
        let sampleTypStr = "Blind"
        let parsed = SingleSealedBidOptions.TryParse sampleTypStr
        parsed |> should equal (Some Blind)

    [<Test>]
    member _.``Can serialize Blind options to string``() =
        let serialized = Blind.ToString()
        serialized |> should equal "Blind"

[<TestFixture>]
type VickreyAuctionStateTests() =
    inherit IncrementSpec<SingleSealedBidState>(emptyVickreyAuctionState, stateHandler)
