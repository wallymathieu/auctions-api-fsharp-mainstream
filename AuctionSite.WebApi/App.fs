namespace AuctionSite.WebApi

open System
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
    /// NOTE: This is overly simplistic and not thread safe.
    /// You could look into thread lock, actors or use a message queue for updates.
    /// Using thread lock to synchronize state is not recommended in a web server 
    /// context since we cannot assume that there will only be one server instance.
    /// </br>
    /// In order to use actors in a web scenario, you would need a distributed actor framework.
    /// </br>
    /// In a real-world application, you could use a database with row versions and throw an error
    /// if there are conflicting writes.
    mutable Auctions: Repository
}

/// Module for initializing application state
module AppStateInit =
    /// Initialize the application state
    let initAppState (initialAuctions: Repository) : AppState =
        { Auctions = initialAuctions }

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
    
    /// Create a bid
    let createBid (onEvent: EventHandler) (appState: AppState) (getCurrentTime: TimeProvider) (auctionId: AuctionId) : HttpHandler =
        fun next ctx ->
            task {
                let! bidReq = ctx.BindJsonAsync<BidRequest>()
                
                return! withAuth (fun user ->
                    fun next ctx ->
                        task {
                            let now = getCurrentTime()
                            let repository = appState.Auctions
                            
                            // Find the auction
                            match getAuction appState auctionId with
                            | Some (auction, _) ->
                                // Create bid
                                let bid = RequestConverters.toBid bidReq auctionId user now auction.AuctionCurrency
                                let command = PlaceBid(now, bid)
                                
                                // Process command
                                let eventResult, updatedRepo = Repository.handle command repository
                                
                                match eventResult with
                                | Ok event ->
                                    // Update application state
                                    appState.Auctions <- updatedRepo
                                    
                                    // Trigger event handler
                                    do! onEvent event
                                    
                                    // Return success
                                    return! Successful.OK event next ctx
                                | Error (UnknownAuction _) ->
                                    return! RequestErrors.NOT_FOUND "Auction not found" next ctx
                                | Error err ->
                                    return! RequestErrors.BAD_REQUEST $"%A{err}" next ctx
                            | None ->
                                return! RequestErrors.NOT_FOUND "Auction not found" next ctx
                        }
                ) next ctx
            }
    
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
        fun next ctx ->
            task {
                let! auctionReq = ctx.BindJsonAsync<AddAuctionRequest>()
                
                return! withAuth (fun user ->
                    fun next ctx -> 
                        task {
                            let now = getCurrentTime()
                            let repository = appState.Auctions
                            
                            // Create auction
                            let auction = RequestConverters.toAuction auctionReq user
                            let command = AddAuction(now, auction)
                            
                            // Process command
                            let eventResult, updatedRepo = Repository.handle command repository
                            
                            match eventResult with
                            | Ok event ->
                                // Update application state
                                appState.Auctions <- updatedRepo
                                
                                // Trigger event handler
                                do! onEvent event
                                
                                // Return success
                                return! Successful.OK event next ctx
                            | Error err ->
                                return! RequestErrors.BAD_REQUEST $"%A{err}" next ctx
                        }
                ) next ctx
            }
    
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
