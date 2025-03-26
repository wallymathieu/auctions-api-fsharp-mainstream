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
    
    /// Validate a bid request
    let validateBidRequest (req: BidRequest) =
        if req.Amount <= 0L then
            Error { Code = "INVALID_AMOUNT"; Message = "Bid amount must be positive"; Details = None }
        else
            Ok req

    /// Create a bid
    let createBid (onEvent: EventHandler) (appState: AppState) (getCurrentTime: TimeProvider) (auctionId: AuctionId) : HttpHandler =
        fun next ctx ->
            task {
                try
                    let! bidReq = ctx.BindJsonAsync<BidRequest>()
                    
                    // Validate request
                    match validateBidRequest bidReq with
                    | Error errorResponse ->
                        return! RequestErrors.BAD_REQUEST errorResponse next ctx
                    | Ok validatedReq ->
                        return! withAuth (fun user ->
                            fun next ctx ->
                                task {
                                    let now = getCurrentTime()
                                    let repository = appState.Auctions
                                    
                                    // Find the auction
                                    match getAuction appState auctionId with
                                    | Some (auction, _) ->
                                        // Create bid
                                        let bid = RequestConverters.toBid validatedReq auctionId user now auction.AuctionCurrency
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
                                    let errorResponse = toErrorResponse (UnknownAuction auctionId)
                                    return! RequestErrors.NOT_FOUND errorResponse next ctx
                                | Error err ->
                                    let errorResponse = toErrorResponse err
                                    return! RequestErrors.BAD_REQUEST errorResponse next ctx
                            | None ->
                                let errorResponse = toErrorResponse (UnknownAuction auctionId)
                                return! RequestErrors.NOT_FOUND errorResponse next ctx
                        }
                ) next ctx
                with ex ->
                    let logger = ctx.GetLogger()
                    logger.LogError(ex, "Error creating bid")
                    return! ServerErrors.INTERNAL_ERROR "An error occurred processing your request" next ctx
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
    
    /// Validate an auction request
    let validateAuctionRequest (req: AddAuctionRequest) =
        if String.IsNullOrWhiteSpace(req.Title) then
            Error { Code = "INVALID_TITLE"; Message = "Title cannot be empty"; Details = None }
        elif req.EndsAt <= req.StartsAt then
            Error { Code = "INVALID_DATES"; Message = "End time must be after start time"; Details = None }
        else
            Ok req
            
    /// Create an auction
    let createAuction (onEvent: EventHandler) (appState: AppState) (getCurrentTime: TimeProvider) : HttpHandler =
        fun next ctx ->
            task {
                try
                    let! auctionReq = ctx.BindJsonAsync<AddAuctionRequest>()
                    
                    // Validate request
                    match validateAuctionRequest auctionReq with
                    | Error errorResponse ->
                        return! RequestErrors.BAD_REQUEST errorResponse next ctx
                    | Ok validatedReq ->
                        return! withAuth (fun user ->
                            fun next ctx -> 
                                task {
                                    let now = getCurrentTime()
                                    let repository = appState.Auctions
                                    
                                    // Create auction
                                    let auction = RequestConverters.toAuction validatedReq user
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
                                let errorResponse = toErrorResponse err
                                return! RequestErrors.BAD_REQUEST errorResponse next ctx
                        }
                ) next ctx
                with ex ->
                    let logger = ctx.GetLogger()
                    logger.LogError(ex, "Error creating auction")
                    return! ServerErrors.INTERNAL_ERROR "An error occurred processing your request" next ctx
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
                GET >=> routef "/auction/%d" (getAuctionById appState)
                POST >=> route "/auction" >=> createAuction onEvent appState getCurrentTime
                POST >=> routef "/auction/%d/bid" (createBid onEvent appState getCurrentTime)
            ]
            
        let notFoundHandler =
            RequestErrors.NOT_FOUND "Not Found"
                
        let errorHandler (ex: Exception) (logger: ILogger) =
            fun next ctx ->
                logger.LogError(ex, "An unhandled exception has occurred while executing the request.")
                let errorResponse = {
                    Code = "INTERNAL_ERROR"
                    Message = "An unexpected error occurred"
                    Details = if ctx.Environment.IsDevelopment() then Some(ex.Message) else None
                }
                ServerErrors.INTERNAL_ERROR errorResponse next ctx
                
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
