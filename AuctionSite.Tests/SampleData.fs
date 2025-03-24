module AuctionSite.Tests.SampleData

open System
open AuctionSite.Domain
open AuctionSite.Money

// Sample data for tests
let sampleAuctionId = 1L
let sampleTitle = "auction"
let sampleStartsAt = DateTime.Parse("2016-01-01 08:28:00.607875Z")
let sampleEndsAt = DateTime.Parse("2016-02-01 08:28:00.607875Z")
let sampleBidTime = DateTime.Parse("2016-02-01 07:28:00.607875Z")
let sampleSeller = BuyerOrSeller("Sample_Seller", "Seller")
let sampleBuyer = BuyerOrSeller("Sample_Buyer", "Buyer")

// Create a sample auction with the specified type
let sampleAuctionOfType typ =
    {
        AuctionId = sampleAuctionId
        Title = sampleTitle
        StartsAt = sampleStartsAt
        Expiry = sampleEndsAt
        Seller = sampleSeller
        AuctionCurrency = Currency.SEK
        Type = typ
    }

// Sample auction with Vickrey auction type
let sampleAuction = sampleAuctionOfType (SingleSealedBid Vickrey)

// Create a SEK amount
let sek amount = createAmount Currency.SEK amount

// Sample bid
let sampleBid = {
    ForAuction = sampleAuctionId
    Bidder = sampleBuyer
    At = sampleBidTime
    BidAmount = sek 100L
}

// Sample buyers
let buyer1 = BuyerOrSeller("Buyer_1", "Buyer 1")
let buyer2 = BuyerOrSeller("Buyer_2", "Buyer 2")
let buyer3 = BuyerOrSeller("Buyer_3", "Buyer 3")

// Sample bid amounts
let bidAmount1 = sek 10L
let bidAmount2 = sek 12L

// Sample bids
let bid1 = {
    Bidder = buyer1
    BidAmount = bidAmount1
    ForAuction = sampleAuctionId
    At = sampleStartsAt.AddSeconds(1.0)
}

let bid2 = {
    Bidder = buyer2
    BidAmount = bidAmount2
    ForAuction = sampleAuctionId
    At = sampleStartsAt.AddSeconds(2.0)
}

let bid_less_than_2 = {
    Bidder = buyer3
    BidAmount = sek 11L
    ForAuction = sampleAuctionId
    At = sampleStartsAt.AddSeconds(3.0)
}
