namespace AuctionSite.WebApi

open System
open System.Text
open System.Text.Json
open System.Text.Json.Serialization
open AuctionSite.Domain

/// JWT related functionality
module Jwt =
    /// JWT payload structure
    type JwtUser = {
        [<JsonPropertyName("sub")>]
        Sub: string
        [<JsonPropertyName("name")>]
        Name: string option
        [<JsonPropertyName("u_typ")>]
        UTyp: string
    }
    
    /// Convert a JWT user to a domain user
    let (|User|InvalidUser|) (jwtUser: JwtUser) =
        match jwtUser.UTyp with
        | "0" -> 
            match jwtUser.Name with
            | Some name -> User(BuyerOrSeller(jwtUser.Sub, name))
            | None -> InvalidUser(InvalidUserData "Missing name for BuyerOrSeller")
        | "1" -> 
            User(Support(jwtUser.Sub))
        | _ -> 
            InvalidUser(InvalidUserData "Invalid user type")
    
    /// Decode base64 string
    let decodeBase64 (base64: string) : Option<string> =
        try
            let bytes = Convert.FromBase64String(base64)
            Some(Encoding.UTF8.GetString(bytes))
        with
        | :? FormatException -> None

    /// Parse JWT payload to JwtUser
    let parseJwtPayload (jsonPayload: string) : Option<JwtUser> =
        try
            Some(JsonSerializer.Deserialize<JwtUser>(jsonPayload, Serialization.serializerOptions()))
        with
        | :? JsonException -> None
    
    /// Decode a JWT payload header value to a domain User
    /// Note that we are returning extra information that we typically do not want to include for unauthorized attackers.
    /// This could be used in logging, but should not be returned to the user.
    let decodeJwtUser (jwtPayload: string) : Result<User, Errors> =
        decodeBase64 jwtPayload 
        |> Option.bind parseJwtPayload
        |> function
            | Some (User user) -> Ok user
            | Some (InvalidUser err) -> Error err
            | None -> Error (InvalidUserData "Unable to interpret payload")
