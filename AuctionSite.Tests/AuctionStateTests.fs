module AuctionSite.Tests.AuctionStateTests

open Expecto
open Expecto.Flip
open AuctionSite.Domain
open AuctionSite.Tests.SampleData

// Base tests for auction states that can be reused in specific auction type tests
let incrementSpec<'T when 'T : equality> (name: string) (baseState: 'T) (stateHandler: IState<'T>) =
    testList name [
        test "can increment twice" {
            let s = stateHandler.Inc sampleBidTime baseState
            let s2 = stateHandler.Inc sampleBidTime s
            s |> Expect.equal "States should be equal after incrementing twice" s2
        }
        
        test "won't end just after start" {
            let state = stateHandler.Inc (sampleStartsAt.AddSeconds(1.0)) baseState
            stateHandler.HasEnded state |> Expect.isFalse "Auction should not have ended just after start"
        }
        
        test "won't end just before end" {
            let state = stateHandler.Inc (sampleEndsAt.AddSeconds(-1.0)) baseState
            stateHandler.HasEnded state |> Expect.isFalse "Auction should not have ended just before end"
        }
        
        test "won't end just before start" {
            let state = stateHandler.Inc (sampleStartsAt.AddSeconds(-1.0)) baseState
            stateHandler.HasEnded state |> Expect.isFalse "Auction should not have ended just before start"
        }
        
        test "will have ended just after end" {
            let state = stateHandler.Inc (sampleEndsAt.AddSeconds(1.0)) baseState
            stateHandler.HasEnded state |> Expect.isTrue "Auction should have ended just after end"
        }
    ]
