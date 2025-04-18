namespace AuctionSite.Domain

open System

/// Commands that can be executed in the system
type Command =
    | AddAuction of DateTime * Auction
    | PlaceBid of DateTime * Bid

/// Events that occurred in the system as a result of commands
type Event =
    | AuctionAdded of DateTime * Auction
    | BidAccepted of DateTime * Bid

/// Repository type for storing auctions and their states
type Repository = Map<AuctionId, Auction * AuctionState>

/// Functions for working with the auction repository
module Repository =
    /// Convert a list of events to a repository of auction states
    let eventsToAuctionStates (events: Event list) : Repository =
        let folder (repo: Repository) (event: Event) =
            match event with
            | AuctionAdded(_, auction) ->
                let emptyState = Auction.emptyState auction
                Map.add auction.AuctionId (auction, emptyState) repo
            | BidAccepted(_, bid) ->
                match Map.tryFind bid.ForAuction repo with
                | Some(auction, state) ->
                    let stateHandler = Auction.createStateHandler()
                    let nextState, _ = stateHandler.AddBid bid state
                    Map.add bid.ForAuction (auction, nextState) repo
                | None -> 
                    failwith "Could not find auction"
        
        events |> List.fold folder Map.empty
        
    /// Get all auctions from the repository
    let auctions (repo: Repository) : Auction list =
        repo |> Map.toList |> List.map (fun (_, (auction, _)) -> auction)
        
    /// Handle a command and return an event and updated repository
    let handle (command: Command) (repository: Repository) : Result<Event, Errors> * Repository =
        match command with
        | AddAuction(time, auction) ->
            let auctionId = auction.AuctionId
            if not (Map.containsKey auctionId repository) then
                let emptyState = Auction.emptyState auction
                let nextRepository = Map.add auctionId (auction, emptyState) repository
                Ok(AuctionAdded(time, auction)), nextRepository
            else
                Error(AuctionAlreadyExists auctionId), repository
                
        | PlaceBid(time, bid) ->
            let auctionId = bid.ForAuction
            match Map.tryFind auctionId repository with
            | Some(auction, state) ->
                match auction,bid with
                | Auction.ValidBid ->
                    let stateHandler = Auction.createStateHandler()
                    let nextState, bidResult = stateHandler.AddBid bid state
                    let nextRepository = Map.add auctionId (auction, nextState) repository
                    
                    match bidResult with
                    | Ok() -> Ok(BidAccepted(time, bid)), nextRepository
                    | Error err -> Error err, repository
                | Auction.InvalidBid err -> 
                    Error err, repository
            | None ->
                Error(UnknownAuction auctionId), repository

