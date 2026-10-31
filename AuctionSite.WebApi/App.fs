namespace AuctionSite.WebApi

open System
open System.Threading
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Logging
open Giraffe
open System.Threading.Tasks
open FSharp.Control.Tasks.V2.ContextInsensitive
open AuctionSite.Domain

/// Application state
type AppState = {
    mutable Auctions: Repository
    Lock: SemaphoreSlim
}

/// Module for initializing application state
module AppStateInit =
    /// Initialize the application state
    let initAppState (initialAuctions: Repository) : AppState =
        { Auctions = initialAuctions; Lock = new SemaphoreSlim(1, 1) }

/// Handlers for the web API
module Handler =
    /// Event handler function type
    type EventHandler = Event -> Task<unit>
    
    /// Time provider function type
    type TimeProvider = unit -> DateTime
    
    /// Get an auction by ID
    let getAuction (appState: AppState) (auctionId: AuctionId) =
        Map.tryFind auctionId appState.Auctions
        
    /// Get all auctions
    let getAuctions (appState: AppState) =
        Repository.auctions appState.Auctions
    
    /// Handle authentication and run a function
    let withAuth (handler: User -> HttpHandler) : HttpHandler =
        fun next ctx ->
            task {
                let authHeader = ctx.TryGetRequestHeader "x-jwt-payload"
                match authHeader with
                | Some authValue ->
                    match Jwt.decodeJwtUser authValue with
                    | Ok user -> return! handler user next ctx
                    | Error _ -> return! RequestErrors.UNAUTHORIZED "Bearer" "Auction API" "Unauthorized" next ctx
                | None -> 
                    return! RequestErrors.UNAUTHORIZED "Bearer" "Auction API" "Unauthorized" next ctx
            }

    /// Read the JSON request body, or None when it cannot be read as a 'T
    let tryBindJson<'T> (ctx: HttpContext) : Task<'T option> =
        task {
            try
                let! value = ctx.BindJsonAsync<'T>()
                return if isNull (box value) then None else Some value
            with
            | :? Text.Json.JsonException -> return None
        }

    /// Read the JSON request body and run a function, or respond 400 when the body cannot be read
    let withJsonBody<'T> (handler: 'T -> HttpHandler) : HttpHandler =
        fun next ctx ->
            task {
                match! tryBindJson<'T> ctx with
                | Some value -> return! handler value next ctx
                | None -> return! RequestErrors.BAD_REQUEST "Invalid request body" next ctx
            }
    
    /// Create a bid
    let createBid (onEvent: EventHandler) (appState: AppState) (getCurrentTime: TimeProvider) (auctionId: AuctionId) : HttpHandler =
        withAuth (fun user ->
            withJsonBody (fun (bidReq: BidRequest) ->
                fun next ctx ->
                    task {
                        let now = getCurrentTime()
                        do! appState.Lock.WaitAsync() |> Async.AwaitTask
                        try
                            match getAuction appState auctionId with
                            | Some (auction, _) ->
                                let bid = RequestConverters.toBid bidReq auctionId user now auction.AuctionCurrency
                                let command = PlaceBid(now, bid)
                                let eventResult, updatedRepo = Repository.handle command appState.Auctions
                                match eventResult with
                                | Ok event ->
                                    appState.Auctions <- updatedRepo
                                    do! onEvent event
                                    return! Successful.OK event next ctx
                                | Error (UnknownAuction _) ->
                                    return! RequestErrors.NOT_FOUND "Auction not found" next ctx
                                | Error err ->
                                    return! RequestErrors.BAD_REQUEST $"%A{err}" next ctx
                            | None ->
                                return! RequestErrors.NOT_FOUND "Auction not found" next ctx
                        finally
                            appState.Lock.Release() |> ignore
                    }))
    
    /// Get an auction by ID
    let getAuctionById (appState: AppState) (auctionId: AuctionId) : HttpHandler =
        fun next ctx ->
            task {
                match getAuction appState auctionId with
                | Some(auction, auctionState) ->
                    let response = ResponseConverters.toAuctionDetailResponse auction auctionState
                    return! Successful.OK response next ctx
                | None ->
                    return! RequestErrors.NOT_FOUND "Auction not found" next ctx
            }
    
    /// Create an auction
    let createAuction (onEvent: EventHandler) (appState: AppState) (getCurrentTime: TimeProvider) : HttpHandler =
        withAuth (fun user ->
            withJsonBody (fun (auctionReq: AddAuctionRequest) ->
                fun next ctx ->
                    task {
                        let now = getCurrentTime()
                        do! appState.Lock.WaitAsync() |> Async.AwaitTask
                        try
                            let auction = RequestConverters.toAuction auctionReq user
                            let command = AddAuction(now, auction)
                            let eventResult, updatedRepo = Repository.handle command appState.Auctions
                            match eventResult with
                            | Ok event ->
                                appState.Auctions <- updatedRepo
                                do! onEvent event
                                return! Successful.OK event next ctx
                            | Error err ->
                                return! RequestErrors.BAD_REQUEST $"%A{err}" next ctx
                        finally
                            appState.Lock.Release() |> ignore
                    }))
    
    /// Get all auctions
    let getAllAuctions (appState: AppState) : HttpHandler =
        fun next ctx ->
            task {
                let auctions = getAuctions appState
                let response = auctions |> List.map ResponseConverters.toAuctionListItemResponse
                return! Successful.OK response next ctx
            }
    
    /// Configure the web application
    let configureApp (app: IApplicationBuilder) (appState: AppState) (onEvent: EventHandler) (getCurrentTime: TimeProvider) =
        let auctionRoutes =
            choose [
                GET >=> route "/auctions" >=> getAllAuctions appState
                GET >=> routef "/auctions/%d" (getAuctionById appState)
                POST >=> route "/auctions" >=> createAuction onEvent appState getCurrentTime
                POST >=> routef "/auctions/%d/bids" (createBid onEvent appState getCurrentTime)
            ]
            
        let notFoundHandler =
            RequestErrors.NOT_FOUND "Not Found"
                
        let errorHandler (ex: Exception) (logger: ILogger) =
            fun next ctx ->
                logger.LogError(ex, "An unhandled exception has occurred while executing the request.")
                ServerErrors.INTERNAL_ERROR "Internal Server Error" next ctx
                
        app
            .UseGiraffeErrorHandler(errorHandler)
            .UseGiraffe(
                choose [
                    auctionRoutes
                    notFoundHandler
                ]
        )

    /// Configure services for the web application
    let configureServices (services: IServiceCollection) =
        services
            .AddSingleton<Json.ISerializer>(Json.FsharpFriendlySerializer(jsonOptions=Serialization.serializerOptions()))
            .AddGiraffe()
            .AddCors(fun options ->
                options.AddPolicy("CorsPolicy", fun builder ->
                    builder
                        .AllowAnyOrigin()
                        .AllowAnyMethod()
                        .AllowAnyHeader()
                        |> ignore
                )
            ) |> ignore
