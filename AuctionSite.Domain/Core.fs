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
        | BuyerOrSeller(id, name) -> sprintf "BuyerOrSeller|%s|%s" id name
        | Support(id) -> sprintf "Support|%s" id

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


open System
open System.Text.Json.Serialization
open System.Text.Json

/// JSON converter for the User type
type UserJsonConverter() =
    inherit JsonConverter<User>()
    
    /// Serialize a User object to JSON
    override _.Write(writer: Utf8JsonWriter, user: User, options: JsonSerializerOptions) =
        writer.WriteStringValue(user.ToString())
    
    /// Deserialize a User object from JSON
    override _.Read(reader: byref<Utf8JsonReader>, _: Type, options: JsonSerializerOptions) =
        let userString = reader.GetString()
        match User.tryParse userString with
        | Some user -> user
        | None -> failwith $"Invalid user format: %s{userString}"
