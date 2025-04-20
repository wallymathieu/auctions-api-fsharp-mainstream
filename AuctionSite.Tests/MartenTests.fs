module AuctionSite.Tests.MartenTests

open System
open System.Threading.Tasks
open Expecto
open DotNet.Testcontainers
open DotNet.Testcontainers.Containers
open DotNet.Testcontainers.Builders
open DotNet.Testcontainers.Configurations
open Testcontainers.PostgreSql
open AuctionSite.Domain
open AuctionSite.Persistence
open AuctionSite.Persistence.MartenDb
open SampleData

/// Helper to create a PostgreSQL container for testing
let createPostgresContainer() =
    let postgresBuilder = 
        (new PostgreSqlBuilder())
            .WithImage("postgres:16")
            .WithPortBinding(5432, true)
            .WithDatabase("auction_test")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(5432))
    
    postgresBuilder.Build()

/// Helper to create a Marten config for the test container
let createTestConfig (container: IContainer) =
    let port = container.GetMappedPublicPort(5432)
    {
        ConnectionString = sprintf "Host=localhost;Port=%d;Database=auction_test;Username=postgres;Password=postgres" port
        SchemaName = "auction_test"
    }

/// Tests for Marten persistence
[<Tests>]
let martenTests =
    testList "Marten Persistence Tests" [
        testTask "Can connect to PostgreSQL container" {
            use container = createPostgresContainer()
            do! container.StartAsync()
            
            let config = createTestConfig container
            use store = createDocumentStore config
            
            // Just verify we can connect
            Expect.isNotNull store "Document store should be created"
            
            do! container.StopAsync()
        }
        
        testTask "Can write and read events" {
            use container = createPostgresContainer()
            do! container.StartAsync()
            
            let config = createTestConfig container
            use store = createDocumentStore config
            
            // Create some test events
            let events = [
                AuctionAdded(sampleStartsAt, sampleAuction)
                BidAccepted(sampleBidTime, bid1)
            ]
            
            // Write events
            do! writeEvents store events
            
            // Read events back
            let! readResult = readEvents store
            
            match readResult with
            | Some readEvents ->
                Expect.isNonEmpty readEvents "Should have read events"
                let evt = readEvents |> List.find (function
                    | Event.AuctionAdded(_, auction) when auction.AuctionId = sampleAuctionId -> true
                    | _ -> false)
                
                match evt with
                | Event.AuctionAdded(_, auction) ->
                    Expect.equal auction.Title "Test Auction" "Title should match"
                    Expect.equal auction.Seller sampleSeller "User ID should match"
                | _ -> failwith "Wrong event type"
            | None -> failwith "Expected to read events"
            
            do! container.StopAsync()
        }
        
        testTask "Can append and retrieve events from event store" {
            use container = createPostgresContainer()
            do! container.StartAsync()
            
            let config = createTestConfig container
            use store = createDocumentStore config
            
            // Create a stream ID
            let streamId = Guid.NewGuid().ToString()
            
            // Create some events to append
            let events = [
                {| EventType = "UserRegistered"; UserId = Guid.NewGuid().ToString(); Email = "test@example.com" |} :> obj
                {| EventType = "UserLoggedIn"; UserId = Guid.NewGuid().ToString(); Timestamp = DateTime.UtcNow |} :> obj
            ]
            
            // Append events to the stream
            do! appendEvents store streamId events
            
            // Retrieve events from the stream
            let! retrievedEvents = getEvents store streamId
            
            Expect.equal retrievedEvents.Length events.Length "Should retrieve the same number of events"
            
            do! container.StopAsync()
        }
    ]
