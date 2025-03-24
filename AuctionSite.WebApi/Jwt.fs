namespace AuctionSite.WebApi

open System
open System.Text
open System.Text.Json
open AuctionSite.Domain

/// JWT related functionality
module Jwt =
    /// JWT payload structure
    type JwtUser = {
        Sub: string
        Name: string option
        UTyp: string
    }
    
    /// Convert a JWT user to a domain user
    let toUser (jwtUser: JwtUser) : Result<User, Errors> =
        match jwtUser.UTyp with
        | "0" -> 
            match jwtUser.Name with
            | Some name -> Ok(BuyerOrSeller(jwtUser.Sub, name))
            | None -> Error(InvalidUserData "Missing name for BuyerOrSeller")
        | "1" -> 
            Ok(Support(jwtUser.Sub))
        | _ -> 
            Error(InvalidUserData "Invalid user type")
    
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
            let options = JsonSerializerOptions()
            options.PropertyNameCaseInsensitive <- true
            let jwtUser = JsonSerializer.Deserialize<JwtUser>(jsonPayload, options)
            Ok jwtUser
        with
        | ex -> Error ex.Message
    
    /// Decode a JWT payload header value to a domain User
    let decodeJwtUser (jwtPayload: string) : Result<User, string> =
        result {
            let! decoded = decodeBase64 jwtPayload
            let! jwtUser = parseJwtPayload decoded
            match toUser jwtUser with
            | Ok user -> return user
            | Error err -> return! Error (sprintf "Invalid user data: %A" err)
        }