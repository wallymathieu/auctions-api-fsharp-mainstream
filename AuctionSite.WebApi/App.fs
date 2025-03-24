namespace AuctionSite.WebApi

open System
open System.Threading
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Logging
open Giraffe
open System.Threading.Tasks
open FSharp.Control.Tasks.V2.ContextInsensitive
open AuctionSite.Domain
open AuctionSite.Money

/// Application state
type AppState = {
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
                                    return! RequestErrors.BAD_REQUEST (sprintf "%A" err) next ctx
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
                                return! RequestErrors.BAD_REQUEST (sprintf "%A" err) next ctx
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
                GET >=> routef "/auction/%d" (fun id -> getAuctionById appState id)
                POST >=> route "/auction" >=> createAuction onEvent appState getCurrentTime
                POST >=> routef "/auction/%d/bid" (fun id -> createBid onEvent appState getCurrentTime id)
            ]
            
        let notFoundHandler =
            RequestErrors.NOT_FOUND "Not Found"
                
        let errorHandler (ex: Exception) (logger: ILogger) =
            fun next ctx ->
                logger.LogError(ex, "An unhandled exception has occurred while executing the request.")
                ServerErrors.INTERNAL_ERROR "Internal Server Error" next ctx
                
        app.UseGiraffe(
            choose [
                auctionRoutes
                notFoundHandler
            ]
        )
        
    /// Configure services for the web application
    let configureServices (services: IServiceCollection) =
        services
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
