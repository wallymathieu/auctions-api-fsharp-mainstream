module AuctionSite.WebApi.Listen

open System
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting

let defaultUrl = "http://localhost:8080"

let urls (configured: string) =
    if String.IsNullOrWhiteSpace configured then defaultUrl else configured

let configure (builder: WebApplicationBuilder) =
    builder.WebHost.UseUrls(urls builder.Configuration[WebHostDefaults.ServerUrlsKey]) |> ignore
