module AuctionSite.Tests.BlindAuctionTests

open Expecto
open Expecto.Flip
open AuctionSite.Domain
open AuctionSite.Tests.SampleData
open AuctionSite.Tests.AuctionStateTests

// Create a Blind auction for testing
let blindAuction = sampleAuctionOfType (SingleSealedBid Blind)
let emptyBlindAuctionState = Auction.emptyState blindAuction |> function | Choice1Of2 s -> s | _ -> failwith "Expected SingleSealedBid state"
let stateHandler = SingleSealedBid.stateHandler

let blindAuctionTests = testList "Blind Auction Tests" [
    test "Can add bid to empty state" {
        let _, result1 = stateHandler.AddBid bid1 emptyBlindAuctionState
        match result1 with
        | Ok () -> ()
        | Error err -> failtest (string err)
    }

    test "Can add second bid" {
        let state1, _ = stateHandler.AddBid bid1 emptyBlindAuctionState
        let _, result2 = stateHandler.AddBid bid2 state1
        match result2 with
        | Ok () -> ()
        | Error err -> failtest (string err)
    }

    test "Can end auction" {
        let state1, _ = stateHandler.AddBid bid1 emptyBlindAuctionState
        let state2, _ = stateHandler.AddBid bid2 state1
        let stateEndedAfterTwoBids = stateHandler.Inc sampleEndsAt state2
        
        match stateEndedAfterTwoBids with
        | DisclosingBids(bids, expiry, opts) ->
            bids.Length |> Expect.equal "Should have 2 bids" 2
            expiry |> Expect.equal "Expiry should match sample end time" sampleEndsAt
            opts |> Expect.equal "Options should be Blind" Blind
        | _ -> failtest "Expected DisclosingBids state"
    }

    test "Get winner and price from an ended auction - winner pays their own bid" {
        // In Blind auction, the highest bidder wins and pays their own bid amount
        let state1, _ = stateHandler.AddBid bid1 emptyBlindAuctionState
        let state2, _ = stateHandler.AddBid bid2 state1  // bid2 is higher than bid1
        let stateEndedAfterTwoBids = stateHandler.Inc sampleEndsAt state2
        
        let maybeAmountAndWinner = stateHandler.TryGetAmountAndWinner stateEndedAfterTwoBids
        // Winner should be buyer2 (who placed bid2) and they pay their own bid amount
        maybeAmountAndWinner |> Expect.equal "Winner should be buyer2 paying their own bid amount" (Some(bidAmount2, buyer2.UserId))
    }

    test "Get winner and price from an ended auction with single bid" {
        let state1, _ = stateHandler.AddBid bid1 emptyBlindAuctionState
        let stateEndedAfterOneBid = stateHandler.Inc sampleEndsAt state1
        
        let maybeAmountAndWinner = stateHandler.TryGetAmountAndWinner stateEndedAfterOneBid
        maybeAmountAndWinner |> Expect.equal "Winner should be buyer1 paying their own bid amount" (Some(bidAmount1, buyer1.UserId))
    }

    test "No winner when no bids placed" {
        let stateEndedWithNoBids = stateHandler.Inc sampleEndsAt emptyBlindAuctionState
        
        let maybeAmountAndWinner = stateHandler.TryGetAmountAndWinner stateEndedWithNoBids
        maybeAmountAndWinner |> Expect.isNone "Should have no winner when no bids placed"
    }

    test "Cannot place bid after auction has ended" {
        let state1, _ = stateHandler.AddBid bid1 emptyBlindAuctionState
        let stateEnded = stateHandler.Inc sampleEndsAt state1
        
        let _, result = stateHandler.AddBid bid2 stateEnded
        match result with
        | Ok _ -> failtest "Did not expect success"
        | Error err -> err |> Expect.equal "Should get AuctionHasEnded error" (AuctionHasEnded sampleAuctionId)
    }

    test "Bids are sorted by amount in descending order when ended" {
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
            bids.Length |> Expect.equal "Should have 3 bids" 3
            bids[0].BidAmount |> Expect.equal "Highest bid should be 15 SEK" (sek 15L)
            bids[1].BidAmount |> Expect.equal "Middle bid should be 10 SEK" (sek 10L)
            bids[2].BidAmount |> Expect.equal "Lowest bid should be 5 SEK" (sek 5L)
        | _ -> failtest "Expected DisclosingBids state"
    }
]

let blindAuctionStateTests = incrementSpec "Blind Auction State Tests" emptyBlindAuctionState stateHandler
