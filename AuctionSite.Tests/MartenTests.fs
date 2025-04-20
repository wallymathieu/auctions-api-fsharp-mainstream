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
        
        testTask "Can write and read commands" {
            use container = createPostgresContainer()
            do! container.StartAsync()
            
            let config = createTestConfig container
            use store = createDocumentStore config
            
            // Create some test commands
            let auctionId = Guid.NewGuid().ToString()
            let userId = Guid.NewGuid().ToString()
            let commands = [
                Commands.createAuction {
                    AuctionId = auctionId
                    Title = "Test Auction"
                    Description = "Test Description"
                    StartingPrice = Money.create "USD" 100.0m
                    ReservePrice = Some (Money.create "USD" 200.0m)
                    AuctionType = AuctionType.TimedAscending
                    StartDate = DateTime.UtcNow
                    EndDate = DateTime.UtcNow.AddDays(7.0)
                    CreatedBy = userId
                }
            ]
            
            // Write commands
            do! writeCommands store commands
            
            // Read commands back
            let! readResult = readCommands store
            
            match readResult with
            | Some readCommands ->
                Expect.isNonEmpty readCommands "Should have read commands"
                let cmd = readCommands |> List.find (function
                    | Commands.CreateAuction c when c.AuctionId = auctionId -> true
                    | _ -> false)
                
                match cmd with
                | Commands.CreateAuction c ->
                    Expect.equal c.Title "Test Auction" "Title should match"
                    Expect.equal c.CreatedBy userId "User ID should match"
                | _ -> failwith "Wrong command type"
            | None -> failwith "Expected to read commands"
            
            do! container.StopAsync()
        }
        
        testTask "Can write and read events" {
            use container = createPostgresContainer()
            do! container.StartAsync()
            
            let config = createTestConfig container
            use store = createDocumentStore config
            
            // Create some test events
            let auctionId = Guid.NewGuid().ToString()
            let userId = Guid.NewGuid().ToString()
            let events = [
                Events.auctionCreated {
                    AuctionId = auctionId
                    Title = "Test Auction"
                    Description = "Test Description"
                    StartingPrice = Money.create "USD" 100.0m
                    ReservePrice = Some (Money.create "USD" 200.0m)
                    AuctionType = AuctionType.TimedAscending
                    StartDate = DateTime.UtcNow
                    EndDate = DateTime.UtcNow.AddDays(7.0)
                    CreatedBy = userId
                    CreatedAt = DateTime.UtcNow
                }
            ]
            
            // Write events
            do! writeEvents store events
            
            // Read events back
            let! readResult = readEvents store
            
            match readResult with
            | Some readEvents ->
                Expect.isNonEmpty readEvents "Should have read events"
                let evt = readEvents |> List.find (function
                    | Events.AuctionCreated e when e.AuctionId = auctionId -> true
                    | _ -> false)
                
                match evt with
                | Events.AuctionCreated e ->
                    Expect.equal e.Title "Test Auction" "Title should match"
                    Expect.equal e.CreatedBy userId "User ID should match"
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
