namespace AuctionSite.Domain

open AuctionSite.Money

/// User identifier type
type UserId = string

/// Represents a user in the system
type User = 
    | BuyerOrSeller of UserId * string  // UserId and Name
    | Support of UserId
    
    /// Gets the user's ID
    member this.UserId =
        match this with
        | BuyerOrSeller(id, _) -> id
        | Support(id) -> id
        
    override this.ToString() =
        match this with
        | BuyerOrSeller(id, name) -> $"BuyerOrSeller|%s{id}|%s{name}"
        | Support(id) -> $"Support|%s{id}"

/// Functions for working with User objects
module User =
    /// Try to parse a user from string representation
    let tryParse (s: string) =
        let parts = s.Split('|')
        match parts with
        | [| "BuyerOrSeller"; id; name |] -> Some(BuyerOrSeller(id, name))
        | [| "Support"; id |] -> Some(Support(id))
        | _ -> None
        
    /// Gets the user ID from a User object
    let userId (user: User) = user.UserId

/// Auction identifier type
type AuctionId = int64

/// Error types that can occur in the auction system
type Errors = 
    | UnknownAuction of AuctionId
    | AuctionAlreadyExists of AuctionId
    | AuctionHasEnded of AuctionId
    | AuctionHasNotStarted of AuctionId
    | SellerCannotPlaceBids of UserId * AuctionId
    | CurrencyConversion of Currency
    | InvalidUserData of string
    | MustPlaceBidOverHighestBid of Amount
    | AlreadyPlacedBid

/// Standard error response format for API
type ErrorResponse = {
    Code: string
    Message: string
    Details: string option
}

/// Converts domain errors to standardized error responses
let toErrorResponse (error: Errors) =
    match error with
    | UnknownAuction id -> 
        { Code = "AUCTION_NOT_FOUND"; Message = $"Auction {id} not found"; Details = None }
    | AuctionAlreadyExists id -> 
        { Code = "AUCTION_EXISTS"; Message = $"Auction {id} already exists"; Details = None }
    | AuctionHasEnded id -> 
        { Code = "AUCTION_ENDED"; Message = $"Auction {id} has ended"; Details = None }
    | AuctionHasNotStarted id -> 
        { Code = "AUCTION_NOT_STARTED"; Message = $"Auction {id} has not started yet"; Details = None }
    | SellerCannotPlaceBids(userId, auctionId) -> 
        { Code = "SELLER_CANNOT_BID"; Message = $"User {userId} cannot bid on own auction {auctionId}"; Details = None }
    | CurrencyConversion currency -> 
        { Code = "INVALID_CURRENCY"; Message = $"Currency must be {currency}"; Details = None }
    | InvalidUserData msg -> 
        { Code = "INVALID_USER_DATA"; Message = "Invalid user data"; Details = Some msg }
    | MustPlaceBidOverHighestBid amount -> 
        { Code = "BID_TOO_LOW"; Message = $"Bid must be higher than current highest bid of {amount}"; Details = None }
    | AlreadyPlacedBid -> 
        { Code = "ALREADY_BID"; Message = "You have already placed a bid on this auction"; Details = None }


open System
open System.Text.Json.Serialization
open System.Text.Json

/// Repository interface for auction operations
type IAuctionRepository =
    /// Get an auction by ID
    abstract member GetAuction: AuctionId -> Option<Auction * AuctionState>
    /// Get all auctions
    abstract member GetAllAuctions: unit -> (Auction * AuctionState) list
    /// Handle a command and return the resulting event and updated repository
    abstract member Handle: Command -> Result<Event, Errors> * Repository

/// JSON converter for the User type
type UserJsonConverter() =
    inherit JsonConverter<User>()
    
    /// Serialize a User object to JSON
    override _.Write(writer: Utf8JsonWriter, user: User, _: JsonSerializerOptions) =
        writer.WriteStringValue(user.ToString())
    
    /// Deserialize a User object from JSON
    override _.Read(reader: byref<Utf8JsonReader>, _: Type, _: JsonSerializerOptions) =
        let userString = reader.GetString()
        match User.tryParse userString with
        | Some user -> user
        | None -> failwith $"Invalid user format: %s{userString}"
