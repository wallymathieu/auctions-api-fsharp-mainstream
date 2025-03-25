module AuctionSite.WebApi.Program

open System
open System.IO
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.Hosting
open AuctionSite.Domain
open AuctionSite.Persistence.JsonFile

// Path to the events file
let eventsFile = "tmp/events.jsonl"

// Event handler function
let onEvent (event: Event) =
    task {
        do! writeEvents eventsFile [event]
        return ()
    }

// Current time provider
let getCurrentTime() = DateTime.UtcNow

// Main entry point
[<EntryPoint>]
let main args =
    // Ensure directory exists
    Directory.CreateDirectory(Path.GetDirectoryName(eventsFile)) |> ignore
    
    // Initialize application
    let events = 
        match readEvents eventsFile |> Async.RunSynchronously with
        | Some e -> e
        | None -> []
        
    let initialState = Repository.eventsToAuctionStates events
    let appState = AppStateInit.initAppState initialState
    
    // Build the host
    let builder = WebApplication.CreateBuilder(args)
    
    // Configure services
    Handler.configureServices builder.Services
    
    // Build the app
    let app = builder.Build()
    
    // Configure app
    if app.Environment.IsDevelopment() then
        app.UseDeveloperExceptionPage() |> ignore
    
    app.UseCors("CorsPolicy") |> ignore
    
    // Configure routing
    Handler.configureApp app appState onEvent getCurrentTime
    
    // Start the server
    printfn "Starting server on http://localhost:8080"
    app.Run("http://localhost:8080")
    
    0 // Return success