module AuctionSite.Tests.VickreyAuctionTests

open Expecto
open Expecto.Flip
open AuctionSite.Domain
open AuctionSite.Tests.SampleData
open AuctionSite.Tests.AuctionStateTests

// Create a Vickrey auction for testing
let vickreyAuction = sampleAuctionOfType (SingleSealedBid Vickrey)
let emptyVickreyAuctionState = Auction.emptyState vickreyAuction |> function | Choice1Of2 s -> s | _ -> failwith "Expected SingleSealedBid state"
let stateHandler = SingleSealedBid.stateHandler

[<Tests>]
let vickreyAuctionTests = testList "Vickrey Auction Tests" [
    test "Can add bid to empty state" {
        let _, result1 = stateHandler.AddBid bid1 emptyVickreyAuctionState
        match result1 with
        | Ok () -> ()
        | Error err -> failtest (string err)
    }

    test "Can add second bid" {
        let state1, _ = stateHandler.AddBid bid1 emptyVickreyAuctionState
        let _, result2 = stateHandler.AddBid bid2 state1
        match result2 with
        | Ok () -> ()
        | Error err -> failtest (string err)
    }

    test "Can end auction" {
        let state1, _ = stateHandler.AddBid bid1 emptyVickreyAuctionState
        let state2, _ = stateHandler.AddBid bid2 state1
        let stateEndedAfterTwoBids = stateHandler.Inc sampleEndsAt state2
        
        match stateEndedAfterTwoBids with
        | DisclosingBids(bids, expiry, opts) ->
            bids.Length |> Expect.equal "Should have 2 bids" 2
            expiry |> Expect.equal "Expiry should match sample end time" sampleEndsAt
            opts |> Expect.equal "Options should be Vickrey" Vickrey
        | _ -> failtest "Expected DisclosingBids state"
    }

    test "Cannot place bid after auction has ended" {
        let state1, _ = stateHandler.AddBid bid1 emptyVickreyAuctionState
        let stateEnded = stateHandler.Inc sampleEndsAt state1
        
        let _, result = stateHandler.AddBid bid2 stateEnded
        match result with
        | Ok _ -> failtest "Did not expect success"
        | Error err -> err |> Expect.equal "Should get AuctionHasEnded error" (AuctionHasEnded sampleAuctionId)
    }

    test "Get winner and price from an ended auction - winner pays second highest bid" {
        // In Vickrey auction, the highest bidder wins but pays the second-highest bid amount
        let state1, _ = stateHandler.AddBid bid1 emptyVickreyAuctionState
        let state2, _ = stateHandler.AddBid bid2 state1  // bid2 is higher than bid1
        let stateEndedAfterTwoBids = stateHandler.Inc sampleEndsAt state2
        
        let maybeAmountAndWinner = stateHandler.TryGetAmountAndWinner stateEndedAfterTwoBids
        // Winner should be buyer2 (who placed bid2) but they pay the price of bid1
        maybeAmountAndWinner |> Expect.equal "Winner should be buyer2 paying bid1 amount" (Some(bidAmount1, buyer2.UserId))
    }

    test "Get winner and price from an ended auction with single bid" {
        // When there's only one bid, the bidder pays their own bid amount
        let state1, _ = stateHandler.AddBid bid1 emptyVickreyAuctionState
        let stateEndedAfterOneBid = stateHandler.Inc sampleEndsAt state1
        
        let maybeAmountAndWinner = stateHandler.TryGetAmountAndWinner stateEndedAfterOneBid
        maybeAmountAndWinner |> Expect.equal "Winner should be buyer1 paying their own bid amount" (Some(bidAmount1, buyer1.UserId))
    }

    test "No winner when no bids placed" {
        let stateEndedWithNoBids = stateHandler.Inc sampleEndsAt emptyVickreyAuctionState
        
        let maybeAmountAndWinner = stateHandler.TryGetAmountAndWinner stateEndedWithNoBids
        maybeAmountAndWinner |> Expect.isNone "Should have no winner when no bids placed"
    }

    test "Can parse Vickrey options from string" {
        let sampleTypStr = "Vickrey"
        let parsed = SingleSealedBidOptions.TryParse sampleTypStr
        parsed |> Expect.equal "Parsed options should be Vickrey" (Some Vickrey)
    }

    test "Can serialize Vickrey options to string" {
        let serialized = Vickrey.ToString()
        serialized |> Expect.equal "Serialized options should be 'Vickrey'" "Vickrey"
    }

    test "Can parse Blind options from string" {
        let sampleTypStr = "Blind"
        let parsed = SingleSealedBidOptions.TryParse sampleTypStr
        parsed |> Expect.equal "Parsed options should be Blind" (Some Blind)
    }

    test "Can serialize Blind options to string" {
        let serialized = Blind.ToString()
        serialized |> Expect.equal "Serialized options should be 'Blind'" "Blind"
    }
]

[<Tests>]
let vickreyAuctionStateTests = incrementSpec "Vickrey Auction State Tests" emptyVickreyAuctionState stateHandler
