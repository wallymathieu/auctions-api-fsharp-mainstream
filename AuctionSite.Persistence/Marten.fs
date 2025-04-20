namespace AuctionSite.Persistence

open System
open System.Threading.Tasks
open Marten
open Marten.Events
open Weasel.Core
open AuctionSite.Domain

/// Module for Marten document store persistence
module MartenDb =
    
    /// Configuration for Marten connection
    type MartenConfig = {
        ConnectionString: string
        SchemaName: string
    }
    
    /// Default configuration with localhost PostgreSQL
    let defaultConfig = {
        ConnectionString = "Host=localhost;Database=auction_site;Username=postgres;Password=postgres"
        SchemaName = "auction_site"
    }
    
    /// Create a document store with the given configuration
    let createDocumentStore (config: MartenConfig) =
        let store = DocumentStore.For(fun options ->
            options.Connection(config.ConnectionString)
            options.DatabaseSchemaName <- config.SchemaName
            
            // Configure event store
            options.Events.DatabaseSchemaName <- config.SchemaName
            
            // Auto-create database objects
            options.AutoCreateSchemaObjects <- AutoCreate.All
            
            // Register serialization for domain types
            options.RegisterDocumentType<Command>()
            options.RegisterDocumentType<Event>()
        )
        store
    
    /// Read commands from Marten
    let readCommands (store: IDocumentStore) : Async<Command list option> = async {
        use session = store.OpenSession()
        let! commands = session.Query<Command>().ToListAsync() |> Async.AwaitTask
        return 
            if commands.Count = 0 then None
            else Some (commands |> Seq.toList)
    }
    
    /// Write commands to Marten
    let writeCommands (store: IDocumentStore) (commands: Command list) : Async<unit> = async {
        use session = store.OpenSession()
        for command in commands do
            session.Store(command)
        do! session.SaveChangesAsync() |> Async.AwaitTask
    }
    
    /// Read events from Marten
    let readEvents (store: IDocumentStore) : Async<Event list option> = async {
        use session = store.OpenSession()
        let! events = session.Query<Event>().ToListAsync() |> Async.AwaitTask
        return 
            if events.Count = 0 then None
            else Some (events |> Seq.toList)
    }
    
    /// Write events to Marten
    let writeEvents (store: IDocumentStore) (events: Event list) : Async<unit> = async {
        use session = store.OpenSession()
        for event in events do
            session.Store(event)
        do! session.SaveChangesAsync() |> Async.AwaitTask
    }
    
    /// Store events in the event store
    let appendEvents (store: IDocumentStore) (streamId: string) (events: obj list) : Async<unit> = async {
        use session = store.OpenSession()
        session.Events.Append(Guid.Parse(streamId), events)
        do! session.SaveChangesAsync() |> Async.AwaitTask
    }
    
    /// Get all events for a stream
    let getEvents (store: IDocumentStore) (streamId: string) : Async<obj list> = async {
        use session = store.OpenSession()
        let! events = session.Events.FetchStreamAsync(Guid.Parse(streamId)) |> Async.AwaitTask
        return events |> Seq.map (fun e -> e.Data) |> Seq.toList
    }
