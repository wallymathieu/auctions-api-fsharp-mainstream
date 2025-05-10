module AuctionSite.Tests.Program

open Expecto

// Collect all tests from different modules
module Tests =
    let allTests = 
        testList "All Tests" [
            EnglishAuctionTests.englishAuctionTests
            EnglishAuctionTests.timedAscendingStateTests
            VickreyAuctionTests.vickreyAuctionTests
            VickreyAuctionTests.vickreyAuctionStateTests
            BlindAuctionTests.blindAuctionTests
            BlindAuctionTests.blindAuctionStateTests
            SerializationTests.serializationTests
            ApiTests.apiTests
            MartenTests.martenTests
        ]

[<EntryPoint>]
let main args =
    runTestsWithCLIArgs [||] args Tests.allTests
    |> exit
