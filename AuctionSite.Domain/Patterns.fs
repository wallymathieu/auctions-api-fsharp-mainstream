module AuctionSite.Domain.Patterns
open System

let (|Int32|) (str: string) = match Int32.TryParse str with | true, i -> Some i | _ -> None 
let (|Int64|) (str: string) = match Int64.TryParse str with | true, i -> Some i | _ -> None 
