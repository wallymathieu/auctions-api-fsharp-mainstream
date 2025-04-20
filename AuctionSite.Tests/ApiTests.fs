module AuctionSite.Tests.ApiTests

open System
open System.Net
open System.Net.Http
open System.Text
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.TestHost
open Expecto
open Expecto.Flip
open AuctionSite.Domain
open AuctionSite.WebApi
open System.Text.Json
open AuctionSite.Money

// Helper functions for tests
module Helpers =
    // JWT payload headers for testing
    let sellerJwtPayload = "eyJzdWIiOiJhMSIsICJuYW1lIjoiVGVzdCIsICJ1X3R5cCI6IjAifQo="
    let buyerJwtPayload = "eyJzdWIiOiJhMiIsICJuYW1lIjoiQnV5ZXIiLCAidV90eXAiOiIwIn0K"

    // Generate HTTP client with JWT header
    let getClientWithJwt (server: TestServer) (jwtPayload: string) =
        let client = server.CreateClient()
        client.DefaultRequestHeaders.Add("x-jwt-payload", jwtPayload)
        client

    // Sample JSON data
    let firstAuctionReqJson = """{"id":1,"startsAt":"2018-01-01T10:00:00.000Z","endsAt":"2019-01-01T10:00:00.000Z","title":"First auction", "currency":"VAC"}"""
    let bidJson = """{"amount":11}"""

    let serialize<'T> (value: 'T) = 
        JsonSerializer.Serialize(value, Serialization.serializerOptions())

    let deserialize<'T> (json: string) = 
        JsonSerializer.Deserialize<'T>(json, Serialization.serializerOptions())

    // Create HTTP content from object
    let createJsonContent<'T> (value: 'T) =
        let json = serialize value
        new StringContent(json, Encoding.UTF8, "application/json")

    // Create HTTP content from JSON string
    let createJsonStringContent (json: string) =
        new StringContent(json, Encoding.UTF8, "application/json")

    // Read HTTP content as JSON object
    let readJsonContent<'T> (response: HttpResponseMessage) = task {
        let! content = response.Content.ReadAsStringAsync()
        return deserialize<'T> content
    }

// Mock event handler for testing
let mutable testEvents: Event list = []
let onEvent (event: Event) = 
    task {
        testEvents <- event :: testEvents
        return ()
    }

// Fixed time provider for tests
let getCurrentTime() = DateTime.Parse("2018-08-04T00:00:00Z")

// Create a test server for API tests
let createTestServer() =
    // Clear events from previous tests
    testEvents <- []
    
    // Initialize server
    let repository: Repository = Map.empty
    let appState = AppStateInit.initAppState repository
    
    let hostBuilder = WebHostBuilder()
                        .ConfigureServices(Handler.configureServices) 
                        .Configure(fun app -> 
                            Handler.configureApp app appState onEvent getCurrentTime)
    
    new TestServer(hostBuilder)

// API tests
let apiTests = testList "API Tests" [
    testTask "Can add auction" {
        use server = createTestServer()
        // Arrange
        let client = Helpers.getClientWithJwt server Helpers.sellerJwtPayload
        let content = Helpers.createJsonStringContent Helpers.firstAuctionReqJson

        // Act
        let! response = client.PostAsync("/auction", content)
        
        // Assert
        response.StatusCode |> Expect.equal "Status code should be OK" HttpStatusCode.OK
        testEvents.Length |> Expect.equal "Should have one event" 1
        
        match testEvents with
        | [AuctionAdded(_, auction)] ->
            auction.AuctionId |> Expect.equal "Auction ID should be 1" 1L
            auction.Title |> Expect.equal "Auction title should match" "First auction"
        | _ -> 
            failtest "Expected AuctionAdded event"
    }

    testTask "Can get auctions after adding one" {
        use server = createTestServer()
        // Arrange
        let client = Helpers.getClientWithJwt server Helpers.sellerJwtPayload
        let content = Helpers.createJsonStringContent Helpers.firstAuctionReqJson
        
        // Add an auction first
        let! _ = client.PostAsync("/auction", content)
        
        // Act
        let! response = client.GetAsync("/auctions")
        
        // Assert
        response.StatusCode |> Expect.equal "Status code should be OK" HttpStatusCode.OK
        let! auctions = Helpers.readJsonContent<AuctionListItemResponse list> response
        
        auctions.Length |> Expect.equal "Should have one auction" 1
        auctions[0].Id |> Expect.equal "Auction ID should be 1" 1L
        auctions[0].Title |> Expect.equal "Auction title should match" "First auction"
    }

    testTask "Can get auction by ID after adding one" {
        use server = createTestServer()
        // Arrange
        let client = Helpers.getClientWithJwt server Helpers.sellerJwtPayload
        let content = Helpers.createJsonStringContent Helpers.firstAuctionReqJson
        
        // Add an auction first
        let! _ = client.PostAsync("/auction", content)
        
        // Act
        let! response = client.GetAsync("/auction/1")
        
        // Assert
        response.StatusCode |> Expect.equal "Status code should be OK" HttpStatusCode.OK
        let! auction = Helpers.readJsonContent<AuctionDetailResponse> response
        
        auction.Id |> Expect.equal "Auction ID should be 1" 1L
        auction.Title |> Expect.equal "Auction title should match" "First auction"
        auction.Bids.Length |> Expect.equal "Should have no bids" 0
    }

    testTask "Can place bid on auction" {
        use server = createTestServer()
        // Arrange
        let sellerClient = Helpers.getClientWithJwt server Helpers.sellerJwtPayload
        let buyerClient = Helpers.getClientWithJwt server Helpers.buyerJwtPayload
        
        // Add an auction first
        let! _ = sellerClient.PostAsync("/auction", Helpers.createJsonStringContent Helpers.firstAuctionReqJson)
        
        // Act - place a bid
        let! response = buyerClient.PostAsync("/auction/1/bid", Helpers.createJsonStringContent Helpers.bidJson)
        
        // Assert
        response.StatusCode |> Expect.equal "Status code should be OK" HttpStatusCode.OK
        testEvents.Length |> Expect.equal "Should have two events" 2  // One for auction add, one for bid
        
        // Check events
        match testEvents with
        | [BidAccepted(_, bid); AuctionAdded _] ->
            bid.ForAuction |> Expect.equal "Bid should be for auction 1" 1L
            bid.BidAmount |> Expect.equal "Bid amount should be VAC11" (createAmount Currency.VAC 11L)
        | _ -> 
            failtest "Expected BidAccepted event"
            
        // Check that the bid is visible in the auction
        let! auctionResponse = buyerClient.GetAsync("/auction/1")
        let! auction = Helpers.readJsonContent<AuctionDetailResponse> auctionResponse
        
        auction.Bids.Length |> Expect.equal "Should have one bid" 1
        auction.Bids[0].Amount |> Expect.equal "Bid amount should be VAC11" "VAC11"
    }

    testTask "Seller cannot bid on own auction" {
        use server = createTestServer()
        // Arrange
        let sellerClient = Helpers.getClientWithJwt server Helpers.sellerJwtPayload
        
        // Add an auction first
        let! _ = sellerClient.PostAsync("/auction", Helpers.createJsonStringContent Helpers.firstAuctionReqJson)
        
        // Act - seller tries to bid on own auction
        let! response = sellerClient.PostAsync("/auction/1/bid", Helpers.createJsonStringContent Helpers.bidJson)
        
        // Assert
        response.StatusCode |> Expect.equal "Status code should be BadRequest" HttpStatusCode.BadRequest
        testEvents.Length |> Expect.equal "Should have only one event" 1  // Only the auction add event
    }

    testTask "Unauthorized request is rejected" {
        use server = createTestServer()
        // Arrange
        let client = server.CreateClient()  // No JWT header
        
        // Act
        let! response = client.PostAsync("/auction", Helpers.createJsonStringContent Helpers.firstAuctionReqJson)
        
        // Assert
        response.StatusCode |> Expect.equal "Status code should be Unauthorized" HttpStatusCode.Unauthorized
    }

    testTask "Cannot bid on non-existent auction" {
        use server = createTestServer()
        // Arrange
        let buyerClient = Helpers.getClientWithJwt server Helpers.buyerJwtPayload
        
        // Act - try to bid on auction that doesn't exist
        let! response = buyerClient.PostAsync("/auction/999/bid", Helpers.createJsonStringContent Helpers.bidJson)
        
        // Assert
        response.StatusCode |> Expect.equal "Status code should be NotFound" HttpStatusCode.NotFound
    }

    testTask "Cannot add same auction twice" {
        use server = createTestServer()
        // Arrange
        let client = Helpers.getClientWithJwt server Helpers.sellerJwtPayload
        let content = Helpers.createJsonStringContent Helpers.firstAuctionReqJson
        
        // Add auction first time
        let! _ = client.PostAsync("/auction", content)
        
        // Act - try to add same auction again
        let! response = client.PostAsync("/auction", content)
        
        // Assert
        response.StatusCode |> Expect.equal "Status code should be BadRequest" HttpStatusCode.BadRequest
    }
]
