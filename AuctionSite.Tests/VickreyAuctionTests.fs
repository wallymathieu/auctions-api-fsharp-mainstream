module AuctionSite.Tests.VickreyAuctionTests

open NUnit.Framework
open FsUnit
open AuctionSite.Domain
open AuctionSite.Tests.SampleData
open AuctionSite.Tests.AuctionStateTests
open AuctionSite.Tests.AuctionTestFixtures

[<TestFixture>]
type VickreyAuctionTests() =
    // Create a test fixture for Vickrey auction
    let fixture = AuctionTestFixture<SingleSealedBidState>(SingleSealedBid Vickrey, SingleSealedBid.stateHandler)
    let stateHandler = fixture.StateHandler
    let emptyVickreyAuctionState = fixture.EmptyState

    [<Test>]
    member _.``Can add bid to empty state``() =
        fixture.CanAddBidToEmptyState()

    [<Test>]
    member _.``Can add second bid``() =
        fixture.CanAddSecondBid()

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
        fixture.CannotPlaceBidAfterAuctionHasEnded()

    [<Test>]
    member _.``Increment state tests``() =
        fixture.RunIncrementStateTests()

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
        fixture.NoWinnerWhenNoBidsPlaced()

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
