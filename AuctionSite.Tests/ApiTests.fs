module AuctionSite.Tests.ApiTests

open System
open System.Net
open System.Net.Http
open System.Text
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.TestHost
open NUnit.Framework
open FsUnit
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

// Test fixture for API tests
[<TestFixture>]
type ApiTests() =
    let mutable server: TestServer = null
    let mutable repository: Repository = Map.empty

    // Set up test server
    [<SetUp>]
    member _.Setup() =
        // Clear events from previous tests
        testEvents <- []
        
        // Initialize server
        let appState = AppStateInit.initAppState repository
        
        let hostBuilder = WebHostBuilder()
                            .ConfigureServices(Handler.configureServices) 
                            .Configure(fun app -> 
                                Handler.configureApp app appState onEvent getCurrentTime)
        
        server <- new TestServer(hostBuilder)

    // Tear down test server
    [<TearDown>]
    member _.TearDown() =
        if server <> null then
            server.Dispose()

    // Test adding an auction
    [<Test>]
    member _.``Can add auction``() = task {
        // Arrange
        let client = Helpers.getClientWithJwt server Helpers.sellerJwtPayload
        let content = Helpers.createJsonStringContent Helpers.firstAuctionReqJson

        // Act
        let! response = client.PostAsync("/auction", content)
        
        // Assert
        response.StatusCode |> should equal HttpStatusCode.OK
        testEvents.Length |> should equal 1
        
        match testEvents with
        | [AuctionAdded(_, auction)] ->
            auction.AuctionId |> should equal 1L
            auction.Title |> should equal "First auction"
        | _ -> 
            Assert.Fail("Expected AuctionAdded event")
    }

    // Test getting auctions
    [<Test>]
    member _.``Can get auctions after adding one``() = task {
        // Arrange
        let client = Helpers.getClientWithJwt server Helpers.sellerJwtPayload
        let content = Helpers.createJsonStringContent Helpers.firstAuctionReqJson
        
        // Add an auction first
        let! _ = client.PostAsync("/auction", content)
        
        // Act
        let! response = client.GetAsync("/auctions")
        
        // Assert
        response.StatusCode |> should equal HttpStatusCode.OK
        let! auctions = Helpers.readJsonContent<AuctionListItemResponse list> response
        
        auctions.Length |> should equal 1
        auctions[0].Id |> should equal 1L
        auctions[0].Title |> should equal "First auction"
    }

    // Test getting auction by ID
    [<Test>]
    member _.``Can get auction by ID after adding one``() = task {
        // Arrange
        let client = Helpers.getClientWithJwt server Helpers.sellerJwtPayload
        let content = Helpers.createJsonStringContent Helpers.firstAuctionReqJson
        
        // Add an auction first
        let! _ = client.PostAsync("/auction", content)
        
        // Act
        let! response = client.GetAsync("/auction/1")
        
        // Assert
        response.StatusCode |> should equal HttpStatusCode.OK
        let! auction = Helpers.readJsonContent<AuctionDetailResponse> response
        
        auction.Id |> should equal 1L
        auction.Title |> should equal "First auction"
        auction.Bids.Length |> should equal 0
    }

    // Test placing a bid
    [<Test>]
    member _.``Can place bid on auction``() = task {
        // Arrange
        let sellerClient = Helpers.getClientWithJwt server Helpers.sellerJwtPayload
        let buyerClient = Helpers.getClientWithJwt server Helpers.buyerJwtPayload
        
        // Add an auction first
        let! _ = sellerClient.PostAsync("/auction", Helpers.createJsonStringContent Helpers.firstAuctionReqJson)
        
        // Act - place a bid
        let! response = buyerClient.PostAsync("/auction/1/bid", Helpers.createJsonStringContent Helpers.bidJson)
        
        // Assert
        response.StatusCode |> should equal HttpStatusCode.OK
        testEvents.Length |> should equal 2  // One for auction add, one for bid
        
        // Check events
        match testEvents with
        | [BidAccepted(_, bid); AuctionAdded _] ->
            bid.ForAuction |> should equal 1L
            bid.BidAmount |> should equal (createAmount Currency.VAC 11L)
        | _ -> 
            Assert.Fail("Expected BidAccepted event")
            
        // Check that the bid is visible in the auction
        let! auctionResponse = buyerClient.GetAsync("/auction/1")
        let! auction = Helpers.readJsonContent<AuctionDetailResponse> auctionResponse
        
        auction.Bids.Length |> should equal 1
        auction.Bids[0].Amount |> should equal "VAC11"
    }

    // Test seller cannot bid on own auction
    [<Test>]
    member _.``Seller cannot bid on own auction``() = task {
        // Arrange
        let sellerClient = Helpers.getClientWithJwt server Helpers.sellerJwtPayload
        
        // Add an auction first
        let! _ = sellerClient.PostAsync("/auction", Helpers.createJsonStringContent Helpers.firstAuctionReqJson)
        
        // Act - seller tries to bid on own auction
        let! response = sellerClient.PostAsync("/auction/1/bid", Helpers.createJsonStringContent Helpers.bidJson)
        
        // Assert
        response.StatusCode |> should equal HttpStatusCode.BadRequest
        testEvents.Length |> should equal 1  // Only the auction add event
    }

    // Test unauthorized access
    [<Test>]
    member _.``Unauthorized request is rejected``() = task {
        // Arrange
        let client = server.CreateClient()  // No JWT header
        
        // Act
        let! response = client.PostAsync("/auction", Helpers.createJsonStringContent Helpers.firstAuctionReqJson)
        
        // Assert
        response.StatusCode |> should equal HttpStatusCode.Unauthorized
    }

    // Test bid on non-existent auction
    [<Test>]
    member _.``Cannot bid on non-existent auction``() = task {
        // Arrange
        let buyerClient = Helpers.getClientWithJwt server Helpers.buyerJwtPayload
        
        // Act - try to bid on auction that doesn't exist
        let! response = buyerClient.PostAsync("/auction/999/bid", Helpers.createJsonStringContent Helpers.bidJson)
        
        // Assert
        response.StatusCode |> should equal HttpStatusCode.NotFound
    }

    // Test adding duplicate auction
    [<Test>]
    member _.``Cannot add same auction twice``() = task {
        // Arrange
        let client = Helpers.getClientWithJwt server Helpers.sellerJwtPayload
        let content = Helpers.createJsonStringContent Helpers.firstAuctionReqJson
        
        // Add auction first time
        let! _ = client.PostAsync("/auction", content)
        
        // Act - try to add same auction again
        let! response = client.PostAsync("/auction", content)
        
        // Assert
        response.StatusCode |> should equal HttpStatusCode.BadRequest
    }