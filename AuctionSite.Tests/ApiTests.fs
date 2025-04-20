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


// Fixed time provider for tests
let getCurrentTime() = DateTime.Parse("2018-08-04T00:00:00Z")

// Create a test server for API tests
let createTestServer(testEvents: Event ResizeArray) =
    // Mock event handler for testing
    let onEvent (event: Event) = 
        task {
            testEvents.Add event
            return ()
        }
    
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
    testAsync "Can add auction" {
        let testEvents = ResizeArray<Event>()
        let server = createTestServer(testEvents)
        use server = server
        // Arrange
        let client = Helpers.getClientWithJwt server Helpers.sellerJwtPayload
        let content = Helpers.createJsonStringContent Helpers.firstAuctionReqJson

        // Act
        let! response = client.PostAsync("/auction", content) |> Async.AwaitTask
        
        // Assert
        response.StatusCode |> Expect.equal "Status code should be OK" HttpStatusCode.OK
        testEvents.Count |> Expect.equal "Should have one event" 1
        
        match testEvents |> List.ofSeq with
        | [AuctionAdded(_, auction)] ->
            auction.AuctionId |> Expect.equal "Auction ID should be 1" 1L
            auction.Title |> Expect.equal "Auction title should match" "First auction"
        | _ -> 
            failtest "Expected AuctionAdded event"
    }

    testAsync "Can get auctions after adding one" {
        let testEvents = ResizeArray<Event>()
        use server = createTestServer(testEvents)
        // Arrange
        let client = Helpers.getClientWithJwt server Helpers.sellerJwtPayload
        let content = Helpers.createJsonStringContent Helpers.firstAuctionReqJson
        
        // Add an auction first
        let! _ = client.PostAsync("/auction", content) |> Async.AwaitTask
        
        // Act
        let! response = client.GetAsync("/auctions") |> Async.AwaitTask
        
        // Assert
        response.StatusCode |> Expect.equal "Status code should be OK" HttpStatusCode.OK
        let! auctions = Helpers.readJsonContent<AuctionListItemResponse list>(response) |> Async.AwaitTask
        
        auctions.Length |> Expect.equal "Should have one auction" 1
        auctions[0].Id |> Expect.equal "Auction ID should be 1" 1L
        auctions[0].Title |> Expect.equal "Auction title should match" "First auction"
    }

    testAsync "Can get auction by ID after adding one" {
        let testEvents = ResizeArray<Event>()
        use server = createTestServer(testEvents)
        // Arrange
        let client = Helpers.getClientWithJwt server Helpers.sellerJwtPayload
        let content = Helpers.createJsonStringContent Helpers.firstAuctionReqJson
        
        // Add an auction first
        let! _ = client.PostAsync("/auction", content) |> Async.AwaitTask
        
        // Act
        let! response = client.GetAsync("/auction/1") |> Async.AwaitTask
        
        // Assert
        response.StatusCode |> Expect.equal "Status code should be OK" HttpStatusCode.OK
        let! auction = Helpers.readJsonContent<AuctionDetailResponse>(response) |> Async.AwaitTask
        
        auction.Id |> Expect.equal "Auction ID should be 1" 1L
        auction.Title |> Expect.equal "Auction title should match" "First auction"
        auction.Bids.Length |> Expect.equal "Should have no bids" 0
    }

    testAsync "Can place bid on auction" {
        let testEvents = ResizeArray<Event>()
        use server = createTestServer(testEvents)
        // Arrange
        let sellerClient = Helpers.getClientWithJwt server Helpers.sellerJwtPayload
        let buyerClient = Helpers.getClientWithJwt server Helpers.buyerJwtPayload
        
        // Add an auction first
        let! _ = sellerClient.PostAsync("/auction", Helpers.createJsonStringContent Helpers.firstAuctionReqJson) |> Async.AwaitTask
        
        // Act - place a bid
        let! response = buyerClient.PostAsync("/auction/1/bid", Helpers.createJsonStringContent Helpers.bidJson) |> Async.AwaitTask
        
        // Assert
        response.StatusCode |> Expect.equal "Status code should be OK" HttpStatusCode.OK
        testEvents.Count |> Expect.equal "Should have two events" 2  // One for auction add, one for bid
        
        // Check events
        match testEvents |> List.ofSeq with
        | [AuctionAdded _; BidAccepted(_, bid)] ->
            bid.ForAuction |> Expect.equal "Bid should be for auction 1" 1L
            bid.BidAmount |> Expect.equal "Bid amount should be VAC11" (createAmount Currency.VAC 11L)
        | _ -> 
            failtestf "Expected BidAccepted event %A" testEvents
            
        // Check that the bid is visible in the auction
        let! auctionResponse = buyerClient.GetAsync("/auction/1") |> Async.AwaitTask
        let! auction = Helpers.readJsonContent<AuctionDetailResponse>(auctionResponse) |> Async.AwaitTask
        
        auction.Bids.Length |> Expect.equal "Should have one bid" 1
        auction.Bids[0].Amount |> Expect.equal "Bid amount should be VAC11" "VAC11"
    }

    testAsync "Seller cannot bid on own auction" {
        let testEvents = ResizeArray<Event>()
        use server = createTestServer(testEvents)
        // Arrange
        let sellerClient = Helpers.getClientWithJwt server Helpers.sellerJwtPayload
        
        // Add an auction first
        let! _ = sellerClient.PostAsync("/auction", Helpers.createJsonStringContent Helpers.firstAuctionReqJson) |> Async.AwaitTask
        
        // Act - seller tries to bid on own auction
        let! response = sellerClient.PostAsync("/auction/1/bid", Helpers.createJsonStringContent Helpers.bidJson) |> Async.AwaitTask
        
        // Assert
        response.StatusCode |> Expect.equal "Status code should be BadRequest" HttpStatusCode.BadRequest
        testEvents.Count |> Expect.equal "Should have only one event" 1  // Only the auction add event
    }

    testAsync "Unauthorized request is rejected" {
        let testEvents = ResizeArray<Event>()
        use server = createTestServer(testEvents)
        // Arrange
        let client = server.CreateClient()  // No JWT header
        
        // Act
        let! response = client.PostAsync("/auction", Helpers.createJsonStringContent Helpers.firstAuctionReqJson) |> Async.AwaitTask
        
        // Assert
        response.StatusCode |> Expect.equal "Status code should be Unauthorized" HttpStatusCode.Unauthorized
    }

    testAsync "Cannot bid on non-existent auction" {
        let testEvents = ResizeArray<Event>()
        use server = createTestServer(testEvents)
        // Arrange
        let buyerClient = Helpers.getClientWithJwt server Helpers.buyerJwtPayload
        
        // Act - try to bid on auction that doesn't exist
        let! response = buyerClient.PostAsync("/auction/999/bid", Helpers.createJsonStringContent Helpers.bidJson) |> Async.AwaitTask
        
        // Assert
        response.StatusCode |> Expect.equal "Status code should be NotFound" HttpStatusCode.NotFound
    }

    testAsync "Cannot add same auction twice" {
        let testEvents = ResizeArray<Event>()
        use server = createTestServer(testEvents)
        // Arrange
        let client = Helpers.getClientWithJwt server Helpers.sellerJwtPayload
        let content = Helpers.createJsonStringContent Helpers.firstAuctionReqJson
        
        // Add auction first time
        let! _ = client.PostAsync("/auction", content) |> Async.AwaitTask
        
        // Act - try to add same auction again
        let! response = client.PostAsync("/auction", content) |> Async.AwaitTask
        
        // Assert
        response.StatusCode |> Expect.equal "Status code should be BadRequest" HttpStatusCode.BadRequest
    }
]
