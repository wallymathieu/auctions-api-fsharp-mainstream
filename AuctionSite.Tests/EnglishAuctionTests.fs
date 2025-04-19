module AuctionSite.Tests.EnglishAuctionTests

open System
open NUnit.Framework
open FsUnit
open AuctionSite.Domain
open AuctionSite.Money
open AuctionSite.Tests.SampleData
open AuctionStateTests

let timedAscAuction = sampleAuctionOfType (TimedAscending (TimedAscending.defaultOptions Currency.SEK))
let emptyAscAuctionState = Auction.emptyState timedAscAuction  |> function | Choice2Of2 s -> s | _ -> failwith "Expected TimedAscending state"
let stateHandler = TimedAscending.stateHandler

[<TestFixture>]
type EnglishAuctionTests() =

    [<Test>]
    member _.``Can add bid to empty state``() =
        let _, result1 = stateHandler.AddBid bid1 emptyAscAuctionState
        match result1 with
        | Ok () -> ()
        | Error err -> Assert.Fail (string err)

    [<Test>]
    member _.``Can add second bid``() =
        let state1, _ = stateHandler.AddBid bid1 emptyAscAuctionState
        let _, result2 = stateHandler.AddBid bid2 state1
        match result2 with
        | Ok () -> ()
        | Error err -> Assert.Fail (string err)

    [<Test>]
    member _.``Can end auction``() =
        let emptyEndedAscAuctionState = stateHandler.Inc sampleEndsAt emptyAscAuctionState
        emptyEndedAscAuctionState |> should equal (HasEnded([], sampleEndsAt, TimedAscending.defaultOptions Currency.SEK))

    [<Test>]
    member _.``Ended with two bids has correct state``() =
        let state1, _ = stateHandler.AddBid bid1 emptyAscAuctionState
        let state2, _ = stateHandler.AddBid bid2 state1
        let stateEndedAfterTwoBids = stateHandler.Inc sampleEndsAt state2
        
        stateEndedAfterTwoBids |> should equal (HasEnded([bid2; bid1], sampleEndsAt, TimedAscending.defaultOptions Currency.SEK))

    [<Test>]
    member _.``Cannot bid after auction has ended``() =
        let state1, _ = stateHandler.AddBid bid1 emptyAscAuctionState
        let state2, _ = stateHandler.AddBid bid2 state1
        let stateEndedAfterTwoBids = stateHandler.Inc sampleEndsAt state2
        
        let _, errAfterEnded = stateHandler.AddBid sampleBid stateEndedAfterTwoBids
        match errAfterEnded with
        | Ok () -> Assert.Fail "Did not expect success"
        | Error err -> err |> should equal (AuctionHasEnded 1L)

    [<Test>]
    member _.``Can get winner and price from an auction``() =
        let state1, _ = stateHandler.AddBid bid1 emptyAscAuctionState
        let state2, _ = stateHandler.AddBid bid2 state1
        let stateEndedAfterTwoBids = stateHandler.Inc sampleEndsAt state2
        
        let maybeAmountAndWinner = stateHandler.TryGetAmountAndWinner stateEndedAfterTwoBids
        maybeAmountAndWinner |> should equal (Some(bidAmount2, buyer2.UserId))

    [<Test>]
    member _.``Cannot place bid lower than highest bid``() =
        let state1, _ = stateHandler.AddBid bid1 emptyAscAuctionState
        let state2, _ = stateHandler.AddBid bid2 state1
        
        let _, maybeFail = stateHandler.AddBid bid_less_than_2 state2
        match maybeFail with
        | Ok () -> Assert.Fail "Did not expect ok"
        | Error err -> err |> should equal (MustPlaceBidOverHighestBid bidAmount2)

    [<Test>]
    member _.``Can parse TimedAscending options from string``() =
        let sampleTypStr = "English|VAC0|VAC0|0"
        let sampleTyp = TimedAscending.defaultOptions Currency.VAC
        
        let parsed = TimedAscendingOptions.TryParse sampleTypStr
        parsed |> should equal (Some sampleTyp)

    [<Test>]
    member _.``Can serialize TimedAscending options to string``() =
        let sampleTyp = TimedAscending.defaultOptions Currency.VAC
        let sampleTypStr = "English|VAC0|VAC0|0"
        
        let serialized = string sampleTyp
        serialized |> should equal sampleTypStr

    [<Test>]
    member _.``Can deserialize options with values``() =
        let sampleWithValuesTypStr = "English|VAC10|VAC20|30"
        let sampleWithValuesTyp = { 
            ReservePrice = createAmount Currency.VAC 10L
            MinRaise = createAmount Currency.VAC 20L
            TimeFrame = TimeSpan.FromSeconds(30.0)
        }
        
        let parsed = TimedAscendingOptions.TryParse sampleWithValuesTypStr
        parsed |> should equal (Some sampleWithValuesTyp)

    [<Test>]
    member _.``Can serialize options with values``() =
        let sampleWithValuesTyp = { 
            ReservePrice = createAmount Currency.VAC 10L
            MinRaise = createAmount Currency.VAC 20L
            TimeFrame = TimeSpan.FromSeconds(30.0)
        }
        let sampleWithValuesTypStr = "English|VAC10|VAC20|30"
        
        let serialized = string sampleWithValuesTyp
        serialized |> should equal sampleWithValuesTypStr

[<TestFixture>]
type TimedAscendingAuctionStateTests() =
    inherit IncrementSpec<TimedAscendingState>(emptyAscAuctionState, stateHandler)
