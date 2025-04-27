module AuctionSite.Tests.EnglishAuctionTests

open System
open Expecto
open Expecto.Flip
open AuctionSite.Domain
open AuctionSite.Money
open AuctionSite.Tests.SampleData
open AuctionSite.Tests.AuctionStateTests

let timedAscAuction = sampleAuctionOfType (TimedAscending (TimedAscending.defaultOptions Currency.SEK))
let emptyAscAuctionState = Auction.emptyState timedAscAuction  |> function | Choice2Of2 s -> s | _ -> failwith "Expected TimedAscending state"
let stateHandler = TimedAscending.stateHandler

[<Tests>]
let englishAuctionTests = testList "English Auction Tests" [
    test "Can add bid to empty state" {
        let _, result1 = stateHandler.AddBid bid1 emptyAscAuctionState
        match result1 with
        | Ok () -> ()
        | Error err -> failtest (string err)
    }

    test "Can add second bid" {
        let state1, _ = stateHandler.AddBid bid1 emptyAscAuctionState
        let _, result2 = stateHandler.AddBid bid2 state1
        match result2 with
        | Ok () -> ()
        | Error err -> failtest (string err)
    }

    test "Can end auction" {
        let emptyEndedAscAuctionState = stateHandler.Inc sampleEndsAt emptyAscAuctionState
        emptyEndedAscAuctionState |> Expect.equal "Empty ended auction state should match expected" (HasEnded([], sampleEndsAt, TimedAscending.defaultOptions Currency.SEK))
    }

    test "Ended with two bids has correct state" {
        let state1, _ = stateHandler.AddBid bid1 emptyAscAuctionState
        let state2, _ = stateHandler.AddBid bid2 state1
        let stateEndedAfterTwoBids = stateHandler.Inc sampleEndsAt state2
        
        stateEndedAfterTwoBids |> Expect.equal "Ended state with two bids should match expected" (HasEnded([bid2; bid1], sampleEndsAt, TimedAscending.defaultOptions Currency.SEK))
    }

    test "Cannot bid after auction has ended" {
        let state1, _ = stateHandler.AddBid bid1 emptyAscAuctionState
        let state2, _ = stateHandler.AddBid bid2 state1
        let stateEndedAfterTwoBids = stateHandler.Inc sampleEndsAt state2
        
        let _, errAfterEnded = stateHandler.AddBid sampleBid stateEndedAfterTwoBids
        match errAfterEnded with
        | Ok () -> failtest "Did not expect success"
        | Error err -> err |> Expect.equal "Should get AuctionHasEnded error" (AuctionHasEnded 1L)
    }

    test "Can get winner and price from an auction" {
        let state1, _ = stateHandler.AddBid bid1 emptyAscAuctionState
        let state2, _ = stateHandler.AddBid bid2 state1
        let stateEndedAfterTwoBids = stateHandler.Inc sampleEndsAt state2
        
        let maybeAmountAndWinner = stateHandler.TryGetAmountAndWinner stateEndedAfterTwoBids
        maybeAmountAndWinner |> Expect.equal "Winner and price should match expected" (Some(bidAmount2, buyer2.UserId))
    }

    test "Cannot place bid lower than highest bid" {
        let state1, _ = stateHandler.AddBid bid1 emptyAscAuctionState
        let state2, _ = stateHandler.AddBid bid2 state1
        
        let _, maybeFail = stateHandler.AddBid bid_less_than_2 state2
        match maybeFail with
        | Ok () -> failtest "Did not expect ok"
        | Error err -> err |> Expect.equal "Should get MustPlaceBidOverHighestBid error" (MustPlaceBidOverHighestBid bidAmount2)
    }

    test "Can parse TimedAscending options from string" {
        let sampleTypStr = "English|0|0|0"
        let sampleTyp = TimedAscending.defaultOptions Currency.VAC
        
        let parsed = TimedAscendingOptions.TryParse sampleTypStr
        parsed |> Expect.equal "Parsed options should match expected" (Some sampleTyp)
    }

    test "Can serialize TimedAscending options to string" {
        let sampleTyp = TimedAscending.defaultOptions Currency.VAC
        let sampleTypStr = "English|0|0|0"
        
        let serialized = string sampleTyp
        serialized |> Expect.equal "Serialized options should match expected" sampleTypStr
    }

    test "Can deserialize options with values" {
        let sampleWithValuesTypStr = "English|10|20|30"
        let sampleWithValuesTyp = { 
            ReservePrice = 10L
            MinRaise = 20L
            TimeFrame = TimeSpan.FromSeconds(30.0)
        }
        
        let parsed = TimedAscendingOptions.TryParse sampleWithValuesTypStr
        parsed |> Expect.equal "Parsed options with values should match expected" (Some sampleWithValuesTyp)
    }

    test "Can serialize options with values" {
        let sampleWithValuesTyp = { 
            ReservePrice = 10L
            MinRaise = 20L
            TimeFrame = TimeSpan.FromSeconds(30.0)
        }
        let sampleWithValuesTypStr = "English|10|20|30"
        
        let serialized = string sampleWithValuesTyp
        serialized |> Expect.equal "Serialized options with values should match expected" sampleWithValuesTypStr
    }
]

[<Tests>]
let timedAscendingStateTests = incrementSpec "TimedAscending State Tests" emptyAscAuctionState stateHandler
