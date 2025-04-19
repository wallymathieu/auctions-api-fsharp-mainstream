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
    let decodeBase64 (base64: string) : Result<string, string> =
        try
            let bytes = Convert.FromBase64String(base64)
            let decodedString = Encoding.UTF8.GetString(bytes)
            Ok decodedString
        with
        | ex -> Error ex.Message
    
    /// Parse JWT payload to JwtUser
    let parseJwtPayload (jsonPayload: string) : Result<JwtUser, string> =
        try
            let jwtUser = JsonSerializer.Deserialize<JwtUser>(jsonPayload, Serialization.serializerOptions())
            Ok jwtUser
        with
        | ex -> Error ex.Message
    
    /// Decode a JWT payload header value to a domain User
    let decodeJwtUser (jwtPayload: string) : Result<User, string> =
        decodeBase64 jwtPayload 
        |> Result.map parseJwtPayload
        |> Result.bind (function
            | Ok (User user) -> Ok user
            | Ok (InvalidUser err) -> Error $"Invalid user data: %A{err}"
            | Error err -> Error $"Invalid user data: %A{err}")
