module AuctionSite.Tests.Program

open Expecto

// Collect all tests from different modules
module Tests =
    let allTests = 
        testList "All Tests" [
            AuctionSite.Tests.EnglishAuctionTests.englishAuctionTests
            AuctionSite.Tests.EnglishAuctionTests.timedAscendingStateTests
            AuctionSite.Tests.VickreyAuctionTests.vickreyAuctionTests
            AuctionSite.Tests.VickreyAuctionTests.vickreyAuctionStateTests
            AuctionSite.Tests.BlindAuctionTests.blindAuctionTests
            AuctionSite.Tests.BlindAuctionTests.blindAuctionStateTests
            AuctionSite.Tests.SerializationTests.serializationTests
            AuctionSite.Tests.ApiTests.apiTests
        ]

[<EntryPoint>]
let main args =
    runTestsWithArgs defaultConfig args Tests.allTests
