module AuctionSite.WebApi.Program

open System
open System.IO
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open AuctionSite.Domain
open AuctionSite.Persistence.JsonFile

// Path to the events file — override with EVENTS_FILE environment variable
let eventsFile =
    Environment.GetEnvironmentVariable("EVENTS_FILE")
    |> Option.ofObj
    |> Option.defaultValue "tmp/events.jsonl"

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
    let builder = WebApplication.CreateBuilder(args)
    Handler.configureServices builder.Services
    let app = builder.Build()

    // Bind persistence warnings to the ASP.NET Core logger
    let logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("AuctionSite.Persistence")
    let warn (filePath: string) (_line: string) (ex: exn) =
        logger.LogWarning(ex, "Skipping malformed JSON line in {FilePath}", filePath)

    // Ensure directory exists and load events
    let eventsDir = Path.GetDirectoryName(eventsFile)
    if not (String.IsNullOrEmpty(eventsDir)) then
        Directory.CreateDirectory(eventsDir) |> ignore
    let events =
        match readEvents warn eventsFile |> Async.RunSynchronously with
        | Some e -> e
        | None -> []

    let initialState = Repository.eventsToAuctionStates events
    let appState = AppStateInit.initAppState initialState

    if app.Environment.IsDevelopment() then
        app.UseDeveloperExceptionPage() |> ignore
    app.UseCors("CorsPolicy") |> ignore
    Handler.configureApp app appState onEvent getCurrentTime

    printfn "Starting server on http://localhost:8080"
    app.Run("http://localhost:8080")
    0